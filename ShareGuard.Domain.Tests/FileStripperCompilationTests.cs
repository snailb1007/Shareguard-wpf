using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class FileStripperCompilationTests
{
    private class DummyFileStripper : IFileStripper
    {
        public bool CanHandle(string extension) => extension == ".txt";
        public Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Finding>());
        }
    }

    [Fact]
    public void IFileStripper_CanBeImplemented()
    {
        IFileStripper stripper = new DummyFileStripper();
        Assert.NotNull(stripper);
    }
}
