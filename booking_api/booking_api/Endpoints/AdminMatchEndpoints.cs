using booking_api.Extensions;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class AdminMatchEndpoints
{
    public static WebApplication MapAdminMatchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/matches")
            .WithTags("Admin Matches")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPost("/{id:guid}/end", async (Guid id, HttpContext http, IOpenPlayService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                return Results.Ok(await svc.EndMatchAsync(id, userId));
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        return app;
    }
}
