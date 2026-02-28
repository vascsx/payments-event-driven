using Microsoft.EntityFrameworkCore;
using Payments.EventDriven.Application;
using Payments.EventDriven.Filters;
using Payments.EventDriven.Infrastructure;
using Payments.EventDriven.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Add Application and Infrastructure services
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOutboxPublisher();

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PaymentDbContext>();
        
        // Apply pending migrations
        if (context.Database.GetPendingMigrations().Any())
        {
            app.Logger.LogInformation("Applying pending migrations...");
            context.Database.Migrate();
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

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
