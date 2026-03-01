using Microsoft.EntityFrameworkCore;
using Payments.EventDriven.Application;
using Payments.EventDriven.Filters;
using Payments.EventDriven.Infrastructure;
using Payments.EventDriven.Infrastructure.HealthChecks;
using Payments.EventDriven.Infrastructure.Persistence;
using Payments.EventDriven.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddTransient<ExceptionMiddleware>();

// Add Application and Infrastructure services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PaymentDbContext>("database")
    .AddCheck<OutboxHealthCheck>("outbox");

var app = builder.Build();

// Apply database migrations based on configuration flag
var runMigrations = builder.Configuration.GetValue<bool>("Database:RunMigrationsOnStartup", false);
if (runMigrations)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    try
    {
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            app.Logger.LogInformation("Applying pending migrations...");
            await context.Database.MigrateAsync();
            app.Logger.LogInformation("Migrations applied successfully!");
        }
        else
        {
            app.Logger.LogInformation("No pending migrations.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}
else
{
    app.Logger.LogInformation("Auto-migration disabled (Database:RunMigrationsOnStartup = false)");
}

// Configure the HTTP request pipeline
app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
