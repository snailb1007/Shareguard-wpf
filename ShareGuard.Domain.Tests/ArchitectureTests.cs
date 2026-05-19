using System.Reflection;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var domainAssembly = Assembly.Load("ShareGuard.Domain");
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("ShareGuard.Application", referencedAssemblies);
        Assert.DoesNotContain("ShareGuard.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("ShareGuard.App", referencedAssemblies);
    }
}
