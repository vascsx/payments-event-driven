using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Application.UseCases;

namespace Payments.EventDriven.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<IProcessPaymentUseCase, ProcessPaymentUseCase>();
        services.AddScoped<IGetPaymentUseCase, GetPaymentUseCase>();
        services.AddScoped<IDeletePaymentUseCase, DeletePaymentUseCase>();

        return services;
    }
}