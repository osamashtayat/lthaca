using System.Globalization;
using System.IO.Compression;
using System.Text;
using Ithaca.Application.Common;
using Ithaca.Domain.Films;
using Ithaca.Domain.Imports;
using Ithaca.Domain.Library;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Imports;

/// <summary>One film of an export, its four files' rows merged (§13 job rule 1). Not stored as such: it becomes an <see cref="ImportItem"/>.</summary>
public sealed record ExportFilm(string Title, int? Year, string? LetterboxdUri, bool Watched, bool Liked, bool OnWatchlist, decimal? Rating);

/// <summary>
/// Reads an uploaded Letterboxd export in memory, in the order of api-contract.md §13 "Upload": type by content (ZIP
/// signature, else a UTF-8 CSV with a Letterboxd header, identified by its file name); from a ZIP only
/// <c>watched.csv</c>, <c>ratings.csv</c>, <c>watchlist.csv</c> and <c>likes/films.csv</c>, every other entry
/// (<c>profile.csv</c>, <c>reviews.csv</c>…) left unopened; entry count, compression ratio and total size counted while
/// reading (headers aren't trusted); then the film cap; skipped rows; nothing left. Nothing is written anywhere.
/// </summary>
public sealed class LetterboxdExportReader(IOptions<ImportOptions> options)
{
    /// <summary>The entries read from a ZIP, by exact path. Everything else is skipped unopened.</summary>
    public static readonly IReadOnlyDictionary<string, ExportFile> ZipEntries = new Dictionary<string, ExportFile>(StringComparer.Ordinal)
    {
        ["watched.csv"] = ExportFile.Watched,
        ["ratings.csv"] = ExportFile.Ratings,
        ["watchlist.csv"] = ExportFile.Watchlist,
        ["likes/films.csv"] = ExportFile.Likes,
    };

    /// <summary>A single uploaded CSV, by its file name (A18): <c>films.csv</c> is the likes file.</summary>
    private static readonly Dictionary<string, ExportFile> SingleFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["watched.csv"] = ExportFile.Watched,
        ["ratings.csv"] = ExportFile.Ratings,
        ["watchlist.csv"] = ExportFile.Watchlist,
        ["films.csv"] = ExportFile.Likes,
    };

    private const string NameColumn = "Name";
    private const string YearColumn = "Year";
    private const string UriColumn = "Letterboxd URI";
    private const string RatingColumn = "Rating";

    /// <summary>Strict UTF-8: invalid bytes (a renamed binary) fail instead of becoming U+FFFD.</summary>
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<Result<IReadOnlyList<ExportFilm>>> ReadAsync(ImportUpload upload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upload);
        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var films = new FilmSet(options.Value.MaxFilms);
        var budget = new ByteBudget(options.Value.MaxUncompressedBytes);
        try
        {
            var read = IsZip(buffer) ? ReadZip(buffer, films, budget) : ReadSingleCsv(buffer, upload.FileName, films, budget);
            if (!read.IsSuccess)
            {
                return read.Error;
            }
        }
        catch (LimitExceededException e)
        {
            return e.Error;
        }
        catch (Exception e) when (e is InvalidDataException or DecoderFallbackException or NotSupportedException)
        {
            return ImportErrors.InvalidFile; // a corrupt archive, an unsupported compression method, or bytes that aren't UTF-8
        }

        return films.Count == 0 ? ImportErrors.NothingToImport : Result<IReadOnlyList<ExportFilm>>.Success(films.ToList());
    }

    /// <summary>A ZIP starts with a local-file header, or an end-of-directory record when empty.</summary>
    private static bool IsZip(MemoryStream buffer)
    {
        var bytes = buffer.GetBuffer();
        return buffer.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K'
            && ((bytes[2] == 3 && bytes[3] == 4) || (bytes[2] == 5 && bytes[3] == 6));
    }

    private Result ReadZip(MemoryStream buffer, FilmSet films, ByteBudget budget)
    {
        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Count > options.Value.MaxZipEntries)
        {
            return ImportErrors.InvalidFile;
        }

        var found = false;
        foreach (var entry in zip.Entries)
        {
            if (!ZipEntries.TryGetValue(entry.FullName, out var file))
            {
                continue;
            }

            found = true;
            var maxEntryBytes = Math.Max(entry.CompressedLength, 1) * options.Value.MaxCompressionRatio;
            using var counted = new CountingStream(entry.Open(), maxEntryBytes, budget);
            var parsed = ReadCsv(counted, file, films);
            if (!parsed.IsSuccess)
            {
                return parsed;
            }
        }

        return found ? Result.Success() : ImportErrors.InvalidFile;
    }

    private static Result ReadSingleCsv(MemoryStream buffer, string fileName, FilmSet films, ByteBudget budget)
    {
        if (!SingleFiles.TryGetValue(Path.GetFileName(fileName ?? ""), out var file))
        {
            return ImportErrors.InvalidFile;
        }

        using var counted = new CountingStream(buffer, long.MaxValue, budget, leaveOpen: true);
        return ReadCsv(counted, file, films);
    }

    private static Result ReadCsv(Stream stream, ExportFile file, FilmSet films)
    {
        using var text = new StreamReader(stream, StrictUtf8, detectEncodingFromByteOrderMarks: false);
        var csv = new CsvRecordReader(text);
        if (csv.Next() is not { } header)
        {
            return ImportErrors.InvalidFile;
        }

        var columns = header.Select((name, index) => (name: name.Trim().TrimStart('﻿'), index))
            .GroupBy(c => c.name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().index, StringComparer.Ordinal);
        if (!columns.TryGetValue(NameColumn, out var nameAt) || !columns.TryGetValue(UriColumn, out var uriAt)
            || (file == ExportFile.Ratings && !columns.ContainsKey(RatingColumn)))
        {
            return ImportErrors.InvalidFile; // not a Letterboxd export header
        }

        var yearAt = columns.GetValueOrDefault(YearColumn, -1);
        var ratingAt = file == ExportFile.Ratings ? columns[RatingColumn] : -1;
        while (csv.Next() is { } row)
        {
            if (ToRow(row, nameAt, yearAt, uriAt, ratingAt) is { } valid)
            {
                films.Add(valid, file);
            }
        }

        return Result.Success();
    }

    /// <summary>A usable row, or null for one §13 says to skip (empty or invalid name, non-numeric year, bad rating).</summary>
    private static ExportRow? ToRow(List<string> row, int nameAt, int yearAt, int uriAt, int ratingAt)
    {
        var name = Field(row, nameAt).Trim();
        if (!Film.IsValidTitle(name) || name.Any(char.IsControl))
        {
            return null;
        }

        int? year = null;
        var yearText = Field(row, yearAt).Trim();
        if (yearText.Length > 0)
        {
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out var y) || y is < 1 or > 9999)
            {
                return null;
            }

            year = y;
        }

        decimal? rating = null;
        if (ratingAt >= 0)
        {
            if (!decimal.TryParse(Field(row, ratingAt).Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var r)
                || !UserFilm.IsValidRating(r))
            {
                return null;
            }

            rating = r;
        }

        var uri = Field(row, uriAt).Trim();
        var keptUri = uri.Length is > 0 and <= ImportItem.LetterboxdUriMaxLength && !uri.Any(char.IsControl) ? uri : null;
        return new ExportRow(name, year, keptUri, rating);
    }

    private static string Field(List<string> row, int index) => index >= 0 && index < row.Count ? row[index] : "";

    private sealed record ExportRow(string Title, int? Year, string? Uri, decimal? Rating);

    /// <summary>Films merged per Letterboxd URI, else per normalised title + year, in first-seen order; refuses more than the cap.</summary>
    private sealed class FilmSet(int maxFilms)
    {
        private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
        private readonly List<ExportFilm> _films = [];

        public int Count => _films.Count;

        public void Add(ExportRow row, ExportFile file)
        {
            var key = row.Uri is { } uri ? $"u:{uri}" : $"t:{TitleNormalizer.Normalize(row.Title)}|{row.Year}";
            if (!_index.TryGetValue(key, out var at))
            {
                if (_films.Count >= maxFilms)
                {
                    throw new LimitExceededException(ImportErrors.TooManyFilms);
                }

                at = _films.Count;
                _index[key] = at;
                _films.Add(new ExportFilm(row.Title, row.Year, row.Uri, false, false, false, null));
            }

            var film = _films[at];
            _films[at] = file switch
            {
                ExportFile.Watched => film with { Watched = true },
                ExportFile.Ratings => film with { Rating = film.Rating ?? row.Rating },
                ExportFile.Watchlist => film with { OnWatchlist = true },
                _ => film with { Liked = true },
            };
        }

        public List<ExportFilm> ToList() => _films;
    }

    /// <summary>Uncompressed bytes left for all files read from one upload.</summary>
    private sealed class ByteBudget(long max)
    {
        private long _used;

        public void Spend(int bytes)
        {
            _used += bytes;
            if (_used > max)
            {
                throw new LimitExceededException(ImportErrors.FileTooLarge);
            }
        }
    }

    /// <summary>Counts the bytes actually decompressed: per entry against the ratio, in total against the budget.</summary>
    private sealed class CountingStream(Stream inner, long maxBytes, ByteBudget budget, bool leaveOpen = false) : Stream
    {
        private long _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            _read += n;
            if (_read > maxBytes)
            {
                throw new LimitExceededException(ImportErrors.InvalidFile); // compression ratio over the limit: a ZIP bomb
            }

            budget.Spend(n);
            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>A limit tripped while reading; carries the error to answer with. Never leaves this class.</summary>
    private sealed class LimitExceededException(Error error) : Exception(error.Code)
    {
        public Error Error { get; } = error;
    }
}

/// <summary>The four export files the import reads (ADR-0024 decision 2).</summary>
public enum ExportFile
{
    Watched,
    Ratings,
    Watchlist,
    Likes,
}
