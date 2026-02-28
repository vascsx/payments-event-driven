namespace Payments.EventDriven.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseApiConfiguration(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.MapControllers();

        return app;
    }
}