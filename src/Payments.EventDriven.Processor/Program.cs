using Microsoft.Extensions.Hosting;
using Payments.EventDriven.Application;
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
            // Stop the host so the container restarts when the worker crashes
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
        });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<PaymentConsumerWorker>();

        var host = builder.Build();
        host.Run();
    }
}