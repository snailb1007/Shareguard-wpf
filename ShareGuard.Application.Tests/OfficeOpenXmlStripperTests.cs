using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class OfficeOpenXmlStripperTests
{
    private string CreateWordFixture(string path, string creator, string company, string customPropName, string customPropValue)
    {
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(new Paragraph(new Run(new Text("Test content")))));

            doc.PackageProperties.Creator = creator;
            doc.PackageProperties.Title = "Original Title";

            var extPart = doc.AddExtendedFilePropertiesPart();
            extPart.Properties = new DocumentFormat.OpenXml.ExtendedProperties.Properties(
                new DocumentFormat.OpenXml.ExtendedProperties.Company(company),
                new DocumentFormat.OpenXml.ExtendedProperties.Application("Microsoft Word")
            );
            extPart.Properties.Save();

            var customPart = doc.AddCustomFilePropertiesPart();
            customPart.Properties = new DocumentFormat.OpenXml.CustomProperties.Properties(
                new DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty
                {
                    Name = customPropName,
                    FormatId = "{D5CDD502-2E9C-101B-9397-08002B2CF9AE}",
                    PropertyId = 2,
                    VTLPWSTR = new DocumentFormat.OpenXml.VariantTypes.VTLPWSTR(customPropValue)
                }
            );
            customPart.Properties.Save();
        }
        return path;
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldRemoveCoreExtendedAndCustomProperties()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"office-strip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var sourcePath = Path.Combine(tempDir, "document.docx");
        var destPath = Path.Combine(tempDir, "document.clean.docx");

        CreateWordFixture(sourcePath, "Author Name", "Acme Corp", "MyCustomField", "CustomValue");

        try
        {
            var stripper = new OfficeOpenXmlStripper();
            var findings = await stripper.StripMetadataAsync(sourcePath, destPath, TestContext.Current.CancellationToken);

            // Assert output file was created
            Assert.True(File.Exists(destPath));

            // Verify findings lists correct stripped info
            Assert.Contains(findings, f => f.Category == "Document/Core" && f.FieldName == "Creator" && f.Value == "Author Name");
            Assert.Contains(findings, f => f.Category == "Document/Extended" && f.FieldName == "Company" && f.Value == "Acme Corp");
            Assert.Contains(findings, f => f.Category == "Document/Custom" && f.FieldName == "MyCustomField" && f.Value == "CustomValue");

            // Reopen clean document and verify elements are gone
            using (var cleanDoc = WordprocessingDocument.Open(destPath, false))
            {
                Assert.Null(cleanDoc.PackageProperties.Creator);
                Assert.Null(cleanDoc.PackageProperties.Title);

                var extPart = cleanDoc.ExtendedFilePropertiesPart;
                if (extPart?.Properties is not null)
                {
                    Assert.Null(extPart.Properties.Company);
                    Assert.Null(extPart.Properties.Application);
                }

                var customPart = cleanDoc.CustomFilePropertiesPart;
                if (customPart?.Properties is not null)
                {
                    Assert.Empty(customPart.Properties.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>());
                }
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
        var stripper = new OfficeOpenXmlStripper();
        Assert.True(stripper.CanHandle(".docx"));
        Assert.True(stripper.CanHandle(".xlsx"));
        Assert.True(stripper.CanHandle(".pptx"));
        Assert.False(stripper.CanHandle(".pdf"));
        Assert.False(stripper.CanHandle(".jpg"));
        Assert.False(stripper.CanHandle(".doc"));
    }
}
