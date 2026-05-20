using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Strips core, extended, and custom metadata properties from Office Open XML files
/// (.docx, .xlsx, .pptx) using Open XML SDK.
/// </summary>
public sealed class OfficeOpenXmlStripper : IFileStripper
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".xlsx", ".pptx"
    };

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    public async Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        File.Copy(sourcePath, destPath, overwrite: true);

        var findings = new List<Finding>();
        var extension = Path.GetExtension(destPath).ToLowerInvariant();

        await Task.Run(() =>
        {
            switch (extension)
            {
                case ".docx":
                    using (var doc = WordprocessingDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                case ".xlsx":
                    using (var doc = SpreadsheetDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                case ".pptx":
                    using (var doc = PresentationDocument.Open(destPath, true))
                    {
                        ExtractAndClearProperties(doc, findings);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported Office file extension: {extension}");
            }
        }, cancellationToken);

        return findings;
    }

    private void ExtractAndClearProperties(OpenXmlPackage package, List<Finding> findings)
    {
        // 1. Clear Core Package Properties
        var props = package.PackageProperties;
        AddFindingIfNotEmpty(findings, "Document/Core", "Creator", props.Creator);
        AddFindingIfNotEmpty(findings, "Document/Core", "LastModifiedBy", props.LastModifiedBy);
        AddFindingIfNotEmpty(findings, "Document/Core", "Title", props.Title);
        AddFindingIfNotEmpty(findings, "Document/Core", "Subject", props.Subject);
        AddFindingIfNotEmpty(findings, "Document/Core", "Keywords", props.Keywords);
        AddFindingIfNotEmpty(findings, "Document/Core", "Description", props.Description);
        AddFindingIfNotEmpty(findings, "Document/Core", "Created", props.Created?.ToString());
        AddFindingIfNotEmpty(findings, "Document/Core", "Modified", props.Modified?.ToString());
        AddFindingIfNotEmpty(findings, "Document/Core", "LastPrinted", props.LastPrinted?.ToString());

        props.Creator = null;
        props.LastModifiedBy = null;
        props.Title = null;
        props.Subject = null;
        props.Keywords = null;
        props.Description = null;
        props.Category = null;
        props.ContentStatus = null;
        props.Language = null;
        props.Revision = null;
        props.Version = null;
        props.Created = null;
        props.Modified = null;
        props.LastPrinted = null;

        // 2. Clear Extended File Properties (Company, Manager, Application)
        var extPart = package.GetPartsOfType<ExtendedFilePropertiesPart>().FirstOrDefault();
        if (extPart?.Properties is not null)
        {
            var extProps = extPart.Properties;
            AddFindingIfNotEmpty(findings, "Document/Extended", "Company", extProps.Company?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "Manager", extProps.Manager?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "Application", extProps.Application?.Text);
            AddFindingIfNotEmpty(findings, "Document/Extended", "TotalTime", extProps.TotalTime?.Text);

            extProps.Company?.Remove();
            extProps.Manager?.Remove();
            extProps.Application?.Remove();
            extProps.TotalTime?.Remove();
            extProps.Save();
        }

        // 3. Clear Custom Document Properties
        var customPart = package.GetPartsOfType<CustomFilePropertiesPart>().FirstOrDefault();
        if (customPart?.Properties is not null)
        {
            foreach (var prop in customPart.Properties.Elements<CustomDocumentProperty>())
            {
                AddFindingIfNotEmpty(findings, "Document/Custom", prop.Name?.Value ?? "Property", prop.InnerText);
            }
            customPart.Properties.RemoveAllChildren<CustomDocumentProperty>();
            customPart.Properties.Save();
        }
    }

    private void AddFindingIfNotEmpty(List<Finding> findings, string category, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            findings.Add(new Finding(category, name, value));
        }
    }
}
