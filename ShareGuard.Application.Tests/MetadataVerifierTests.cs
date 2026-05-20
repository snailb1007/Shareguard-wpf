using ShareGuard.Application.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Xunit;

namespace ShareGuard.Application.Tests;

public class MetadataVerifierTests
{
    private string CreateImageWithExif(string path, bool hasGps)
    {
        using (var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
        {
            if (hasGps)
            {
                var profile = new ExifProfile();
                profile.SetValue(ExifTag.GPSLatitude, new SixLabors.ImageSharp.Rational[] { new(37, 1), new(46, 1), new(30, 1) });
                profile.SetValue(ExifTag.GPSLatitudeRef, "N");
                profile.SetValue(ExifTag.GPSLongitude, new SixLabors.ImageSharp.Rational[] { new(122, 1), new(25, 1), new(10, 1) });
                profile.SetValue(ExifTag.GPSLongitudeRef, "W");
                image.Metadata.ExifProfile = profile;
            }
            image.Save(path);
        }
        return path;
    }

    [Fact]
    public void VerifyNoSensitiveMetadata_WithCleanFile_ShouldReturnTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"verifier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var cleanImg = Path.Combine(tempDir, "clean.jpg");
        CreateImageWithExif(cleanImg, hasGps: false);

        try
        {
            var result = MetadataVerifier.VerifyNoSensitiveMetadata(cleanImg);
            Assert.True(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void VerifyNoSensitiveMetadata_WithLeakingFile_ShouldReturnFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"verifier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dirtyImg = Path.Combine(tempDir, "dirty.jpg");
        CreateImageWithExif(dirtyImg, hasGps: true);

        try
        {
            var result = MetadataVerifier.VerifyNoSensitiveMetadata(dirtyImg);
            Assert.False(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
