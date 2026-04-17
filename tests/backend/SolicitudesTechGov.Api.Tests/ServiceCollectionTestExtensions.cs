using Microsoft.Extensions.DependencyInjection;

namespace SolicitudesTechGov.Api.Tests;

internal static class ServiceCollectionTestExtensions
{
    internal static void RemoveServiceDescriptors(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
        {
            services.Remove(descriptor);
        }
    }
}
