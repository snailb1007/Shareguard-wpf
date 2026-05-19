using Microsoft.Extensions.DependencyInjection;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Infrastructure layer service registrations will be added here
        return services;
    }
}
