using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class PdfMetadataStripperTests
{
    private string CreatePdfFixture(string path, string author, string creator)
    {
        using (var doc = new PdfDocument())
        {
            doc.AddPage(); // PDFsharp requires at least one page
            doc.Info.Author = author;
            doc.Info.Creator = creator;
            doc.Info.Title = "Original PDF Title";
            doc.Save(path);
        }
        return path;
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldClearPdfInfoDictionary()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"pdf-strip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "document.pdf");
        var destPath = Path.Combine(tempDir, "document.clean.pdf");

        CreatePdfFixture(sourcePath, "Jane Doe", "InDesign PDF Creator");

        try
        {
            var stripper = new PdfMetadataStripper();
            var findings = await stripper.StripMetadataAsync(sourcePath, destPath, TestContext.Current.CancellationToken);

            Assert.True(File.Exists(destPath));

            // Verify findings captured correct info
            Assert.Contains(findings, f => f.Category == "Document/PDF" && f.FieldName == "Author" && f.Value == "Jane Doe");
            Assert.Contains(findings, f => f.Category == "Document/PDF" && f.FieldName == "Creator" && f.Value == "InDesign PDF Creator");

            // Reopen clean PDF and assert Info dictionary is cleared
            using (var cleanDoc = PdfReader.Open(destPath, PdfDocumentOpenMode.Import))
            {
                Assert.Empty(cleanDoc.Info.Author);
                Assert.Empty(cleanDoc.Info.Creator);
                Assert.Empty(cleanDoc.Info.Title);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CanHandle_ShouldReturnCorrectResults()
    {
        var stripper = new PdfMetadataStripper();
        Assert.True(stripper.CanHandle(".pdf"));
        Assert.True(stripper.CanHandle(".PDF"));
        Assert.False(stripper.CanHandle(".docx"));
        Assert.False(stripper.CanHandle(".jpg"));
    }
}
