namespace Payments.EventDriven.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services)
    {
        services.AddControllers();

        return services;
    }
}