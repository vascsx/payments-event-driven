using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.EventHandlers;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Application.UseCases;

namespace Payments.EventDriven.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Use Cases
        services.AddScoped<ICreatePaymentUseCase, CreatePaymentUseCase>();
        services.AddScoped<IProcessPaymentUseCase, ProcessPaymentUseCase>();
        services.AddScoped<IGetPaymentUseCase, GetPaymentUseCase>();
        services.AddScoped<IDeletePaymentUseCase, DeletePaymentUseCase>();

        // Event Handlers - Arquitetura extensível
        // Para adicionar novo tipo de pagamento: apenas crie um novo handler e registre aqui
        services.AddScoped<IEventHandler, DefaultPaymentHandler>();
        services.AddScoped<IEventHandler, DarfPaymentHandler>();
        services.AddScoped<IEventHandler, DarjPaymentHandler>();
        services.AddScoped<IEventHandler, PaymentDeletedHandler>();
        
        // Registra cada handler também por seu tipo concreto (necessário para factory)
        services.AddScoped<DefaultPaymentHandler>();
        services.AddScoped<DarfPaymentHandler>();
        services.AddScoped<DarjPaymentHandler>();
        services.AddScoped<PaymentDeletedHandler>();

        services.AddScoped<IEventHandlerFactory, EventHandlerFactory>();

        return services;
    }
}