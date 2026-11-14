using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Imports;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>
/// <c>POST /api/imports</c> form: field <c>file</c>, the Letterboxd ZIP or one CSV from it. The one request type in the Api,
/// because <see cref="IFormFile"/> is an ASP.NET Core type Application can't reference (api-contract.md §13 "Upload types").
/// </summary>
public sealed record CreateImportRequest([Required, FromForm(Name = "file")][property: FromForm(Name = "file")] IFormFile File)
{
    /// <summary>The only conversion: hand the service a stream, a name and a length. No checks here (the service decides).</summary>
    public ImportUpload ToImportUpload() => new(File.OpenReadStream(), File.FileName, File.Length);
}
