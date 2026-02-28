using Microsoft.Extensions.Hosting;
using Payments.EventDriven.Infrastructure;
using Payments.EventDriven.Processor.Workers;

namespace Payments.EventDriven.Processor;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<HostOptions>(options =>
        {
            // Keep the worker running even if an exception occurs in the background service
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<PaymentConsumerWorker>();

        var host = builder.Build();
        host.Run();
    }
}