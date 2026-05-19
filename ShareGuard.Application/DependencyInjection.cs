using Microsoft.Extensions.DependencyInjection;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application layer service registrations will be added here
        return services;
    }
}
