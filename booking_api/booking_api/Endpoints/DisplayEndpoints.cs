using booking_api.Services;

namespace booking_api.Endpoints;

public static class DisplayEndpoints
{
    public static WebApplication MapDisplayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/display").WithTags("Display");

        group.MapGet("/rooms", async (IDisplayService svc) =>
            Results.Ok(await svc.GetSnapshotAsync()))
            .AllowAnonymous();

        return app;
    }
}
