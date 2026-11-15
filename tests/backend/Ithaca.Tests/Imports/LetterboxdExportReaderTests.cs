using System.IO.Compression;
using System.Text;
using Ithaca.Application.Common;
using Ithaca.Application.Imports;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Imports;

/// <summary>
/// The upload checks of api-contract.md §13 without a database: type by content, the four files only, the ZIP limits counted
/// while reading, the film cap, skipped rows. Archives are generated per test.
/// </summary>
public sealed class LetterboxdExportReaderTests
{
    private const string Header = "Date,Name,Year,Letterboxd URI";
    private const string RatingsHeader = "Date,Name,Year,Letterboxd URI,Rating";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Read_FixtureExport_MergesTheFourFilesPerFilm()
    {
        var films = await ReadOkAsync(ImportTestKit.FixtureZip(), "letterboxd-osama-2026-11-15.zip");

        films.Count.ShouldBe(10);
        var fightClub = films.Single(f => f.Title == "Fight Club");
        fightClub.ShouldBe(new ExportFilm("Fight Club", 1999, "https://boxd.it/2a1m", Watched: true, Liked: true, OnWatchlist: false, Rating: 4.5m));
        films.Single(f => f.Title == "Inception").ShouldBe(new ExportFilm("Inception", 2010, "https://boxd.it/1skk", true, false, true, null));
        films.Single(f => f.Title == "Dune: Part Three").ShouldBe(new ExportFilm("Dune: Part Three", 2026, "https://boxd.it/zz06", false, false, true, null));
        films.Single(f => f.Title == "Untitled Project").Year.ShouldBeNull("an empty year is allowed");
        films.Single(f => f.Title == "Hamlet").Rating.ShouldBeNull("its ratings row has no rating and was skipped");
        films.Single(f => f.Title == "Lost Reel").Rating.ShouldBeNull("rating 7 isn't a half step in 0.5-5");
        films.ShouldContain(f => f.Title == "Nowhere Film, The \"Sequel\"", "quoted field with a comma and doubled quotes");
        films.ShouldNotContain(f => f.Title == "Broken Year", "a non-numeric year skips the row");
        films.Select(f => f.Title).ShouldNotContain("Placeholder review text.");
    }

    [Theory]
    [InlineData("watched.csv", true, false, false)]
    [InlineData("watchlist.csv", false, false, true)]
    [InlineData("films.csv", false, true, false)]
    [InlineData("WATCHED.CSV", true, false, false)]
    public async Task Read_SingleCsv_IsIdentifiedByItsName(string fileName, bool watched, bool liked, bool watchlist)
    {
        var films = await ReadOkAsync(Csv(Header, "2024-01-01,Glasshouse,2025,https://boxd.it/abc"), fileName);

        films.ShouldHaveSingleItem().ShouldBe(new ExportFilm("Glasshouse", 2025, "https://boxd.it/abc", watched, liked, watchlist, null));
    }

    [Fact]
    public async Task Read_SingleRatingsCsv_KeepsHalfStars()
    {
        var films = await ReadOkAsync(Csv(RatingsHeader, "2024-01-01,A,2001,https://boxd.it/a,0.5", "2024-01-01,B,2002,https://boxd.it/b,3.5", "2024-01-01,C,2003,https://boxd.it/c,5"), "ratings.csv");

        films.Select(f => f.Rating).ShouldBe([0.5m, 3.5m, 5m]);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("5.5")]
    [InlineData("3.25")]
    [InlineData("-1")]
    [InlineData("four")]
    [InlineData("")]
    public async Task Read_BadRating_SkipsTheRow(string rating)
    {
        var films = await ReadOkAsync(Csv(RatingsHeader, $"2024-01-01,Bad,2001,https://boxd.it/a,{rating}", "2024-01-01,Good,2002,https://boxd.it/b,4"), "ratings.csv");

        films.ShouldHaveSingleItem().Title.ShouldBe("Good");
    }

    [Fact]
    public async Task Read_EdgeRows_AreParsedOrSkippedAsTheContractSays()
    {
        var csv = string.Join("\n",
            "﻿" + Header,                                     // a BOM before the header
            "\"2024-01-01\nlate\",\"Line, Break\",2001,https://boxd.it/a", // a line break and a comma inside quotes
            "",                                                     // a blank line
            "2024-01-01,   ,2001,https://boxd.it/b",               // a blank name: skipped
            "2024-01-01,No Year,,",                                 // no year, no URI: kept, identified by title
            "2024-01-01,No Year,,",                                 // the same film again: merged
            "2024-01-01,Short Row",                                 // fewer fields than the header: year and URI empty
            "2024-01-01,Ctrl\u0001Char,2001,https://boxd.it/c",    // a control character in the name: skipped
            "2024-01-01,\"Two\nLines\",2001,https://boxd.it/e",   // a line break in the name is a control character too
            $"2024-01-01,{new string('x', 301)},2001,https://boxd.it/d"); // longer than a film title may be: skipped

        var films = await ReadOkAsync(Encoding.UTF8.GetBytes(csv), "watched.csv");

        films.Select(f => f.Title).ShouldBe(["Line, Break", "No Year", "Short Row"]);
        films[1].ShouldBe(new ExportFilm("No Year", null, null, true, false, false, null));
    }

    [Fact]
    public async Task Read_SameTitleAndYearWithoutUri_IsOneFilmAcrossFiles()
    {
        var zip = ImportTestKit.Zip(
            ("watched.csv", $"{Header}\n2024-01-01,Amélie,2001,\n"),
            ("likes/films.csv", $"{Header}\n2024-01-01,AMELIE!,2001,\n"));

        var films = await ReadOkAsync(zip, "export.zip");

        films.ShouldHaveSingleItem().ShouldBe(new ExportFilm("Amélie", 2001, null, true, true, false, null));
    }

    [Fact]
    public async Task Read_EntriesOtherThanTheFour_AreNeverOpened()
    {
        var profile = Encoding.UTF8.GetBytes("Date Joined,Username,Given Name,Family Name,Email Address\n2020-01-01,someone,A,B,someone@example.com\n");
        var reviews = Encoding.UTF8.GetBytes("Date,Name,Year,Letterboxd URI,Rating,Rewatch,Review\n2020-01-01,X,2000,https://boxd.it/x,4,,A review.\n");
        var watched = Encoding.UTF8.GetBytes($"{Header}\n2024-01-01,Glasshouse,2025,https://boxd.it/abc\n");
        var zip = ImportTestKit.Zip([("profile.csv", profile), ("reviews.csv", reviews), ("watched.csv", watched)], CompressionLevel.NoCompression);
        var unreadable = ImportTestKit.CorruptEntry(ImportTestKit.CorruptEntry(zip, "profile.csv", profile), "reviews.csv", reviews);

        (await ReadOkAsync(unreadable, "export.zip")).ShouldHaveSingleItem().Title.ShouldBe("Glasshouse");

        // Control: the same corruption in a file we do read is noticed.
        (await ReadAsync(ImportTestKit.CorruptEntry(zip, "watched.csv", watched), "export.zip")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_FilesInsideFoldersOrNestedArchives_AreNotRead()
    {
        var inner = ImportTestKit.Zip(("watched.csv", $"{Header}\n2024-01-01,Glasshouse,2025,https://boxd.it/abc\n"));
        var zip = ImportTestKit.Zip([("export/watched.csv", Encoding.UTF8.GetBytes($"{Header}\n2024-01-01,A,2001,\n")), ("deleted/watched.csv", inner), ("lists/x.zip", inner)]);

        (await ReadAsync(zip, "export.zip")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_ZipBomb_IsInvalidFile()
    {
        var row = "2024-01-01,Same Film Again And Again,2001,https://boxd.it/same\n";
        var bomb = new StringBuilder(Header + "\n");
        while (bomb.Length < 8_000_000)
        {
            bomb.Append(row);
        }

        var result = await ReadAsync(ImportTestKit.Zip(("watched.csv", bomb.ToString())), "export.zip");

        result.Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_MoreUncompressedThanTheLimit_IsFileTooLarge()
    {
        var csv = Csv(Header, [.. Enumerable.Range(1, 50).Select(i => $"2024-01-01,Film {i},2001,https://boxd.it/{i}")]);
        var zip = ImportTestKit.Zip([("watched.csv", csv), ("watchlist.csv", csv)], CompressionLevel.NoCompression);

        var result = await ReadAsync(zip, "export.zip", new ImportOptions { MaxUncompressedBytes = csv.Length + 10 });

        result.Error.ShouldBe(ImportErrors.FileTooLarge);
    }

    [Fact]
    public async Task Read_TooManyEntries_IsInvalidFile()
    {
        var entries = Enumerable.Range(0, 6).Select(i => ($"lists/list-{i}.csv", "x")).Append(("watched.csv", $"{Header}\n2024-01-01,A,2001,\n")).ToArray();

        var result = await ReadAsync(ImportTestKit.Zip(entries), "export.zip", new ImportOptions { MaxZipEntries = 5 });

        result.Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_MoreFilmsThanTheCap_IsTooManyFilms()
    {
        var csv = Csv(Header, "2024-01-01,A,2001,", "2024-01-01,B,2002,", "2024-01-01,A,2001,", "2024-01-01,C,2003,");

        (await ReadAsync(csv, "watched.csv", new ImportOptions { MaxFilms = 3 })).IsSuccess.ShouldBeTrue("3 distinct films");
        (await ReadAsync(csv, "watched.csv", new ImportOptions { MaxFilms = 2 })).Error.ShouldBe(ImportErrors.TooManyFilms);
    }

    [Theory]
    [InlineData("watched.csv")]
    [InlineData("export.zip")]
    public async Task Read_RenamedBinary_IsInvalidFile(string fileName)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xFF, 0xFE, 0x00, 0x01, 0xC3, 0x28 };

        (await ReadAsync(png, fileName)).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_TruncatedZip_IsInvalidFile()
    {
        var zip = ImportTestKit.FixtureZip();

        (await ReadAsync(zip[..(zip.Length / 2)], "export.zip")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Theory]
    [InlineData("diary.csv")]
    [InlineData("profile.csv")]
    [InlineData("reviews.csv")]
    [InlineData("watched.txt")]
    public async Task Read_CsvWithAnotherName_IsInvalidFile(string fileName)
    {
        (await ReadAsync(Csv(Header, "2024-01-01,A,2001,"), fileName)).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Theory]
    [InlineData("Date,Title,Year,Letterboxd URI")] // no Name column
    [InlineData("Date,Name,Year")]                  // no Letterboxd URI column
    [InlineData("just some text")]
    public async Task Read_UnrecognisedHeader_IsInvalidFile(string header)
    {
        (await ReadAsync(Csv(header, "2024-01-01,A,2001,"), "watched.csv")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_RatingsWithoutRatingColumn_IsInvalidFile()
    {
        (await ReadAsync(Csv(Header, "2024-01-01,A,2001,"), "ratings.csv")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_ZipWithoutAnyOfTheFour_IsInvalidFile()
    {
        var zip = ImportTestKit.Zip(("profile.csv", "Date Joined,Username\n"), ("diary.csv", $"{Header}\n2024-01-01,A,2001,\n"));

        (await ReadAsync(zip, "export.zip")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_EmptyFile_IsInvalidFile()
    {
        (await ReadAsync([], "watched.csv")).Error.ShouldBe(ImportErrors.InvalidFile);
        (await ReadAsync(ImportTestKit.Zip(), "export.zip")).Error.ShouldBe(ImportErrors.InvalidFile);
    }

    [Fact]
    public async Task Read_NoUsableRows_IsNothingToImport()
    {
        (await ReadAsync(Csv(Header), "watched.csv")).Error.ShouldBe(ImportErrors.NothingToImport);
        (await ReadAsync(Csv(Header, "2024-01-01,,2001,", "2024-01-01,A,abc,"), "watched.csv")).Error.ShouldBe(ImportErrors.NothingToImport);
    }

    private static byte[] Csv(string header, params string[] rows) => Encoding.UTF8.GetBytes(string.Join("\r\n", [header, .. rows]) + "\r\n");

    private static async Task<IReadOnlyList<ExportFilm>> ReadOkAsync(byte[] bytes, string fileName)
    {
        var result = await ReadAsync(bytes, fileName);
        result.Error.ShouldBeNull();
        return result.Value;
    }

    private static async Task<Result<IReadOnlyList<ExportFilm>>> ReadAsync(byte[] bytes, string fileName, ImportOptions? options = null)
    {
        using var stream = new MemoryStream(bytes);
        return await new LetterboxdExportReader(Options.Create(options ?? new ImportOptions())).ReadAsync(new ImportUpload(stream, fileName, bytes.Length), Ct);
    }
}
