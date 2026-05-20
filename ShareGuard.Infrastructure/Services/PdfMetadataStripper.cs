using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Strips standard document properties from PDFs using PDFsharp.
/// </summary>
public sealed class PdfMetadataStripper : IFileStripper
{
    public bool CanHandle(string extension)
    {
        return ".pdf".Equals(extension, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        var findings = new List<Finding>();

        await Task.Run(() =>
        {
            using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);

            var info = document.Info;

            AddFindingIfNotEmpty(findings, "Document/PDF", "Title", info.Title);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Author", info.Author);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Subject", info.Subject);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Keywords", info.Keywords);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Creator", info.Creator);
            AddFindingIfNotEmpty(findings, "Document/PDF", "Producer", info.Producer);

            info.Title = string.Empty;
            info.Author = string.Empty;
            info.Subject = string.Empty;
            info.Keywords = string.Empty;
            info.Creator = string.Empty;
            // Producer is read-only in PDFsharp 6.x; clear via the Elements dictionary
            info.Elements.SetString("/Producer", string.Empty);

            document.Save(destPath);
        }, cancellationToken);

        return findings;
    }

    private static void AddFindingIfNotEmpty(List<Finding> findings, string category, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new Finding(category, name, value));
        }
    }
}
