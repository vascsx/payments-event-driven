using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.UseCases;

namespace Payments.EventDriven.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreatePaymentUseCase>();

        return services;
    }
}