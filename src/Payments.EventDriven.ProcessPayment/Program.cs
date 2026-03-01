using Payments.EventDriven.Application;
using Payments.EventDriven.Infrastructure;
using Payments.EventDriven.ProcessPayment.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<EventRouterWorker>();

var host = builder.Build();
host.Run();
