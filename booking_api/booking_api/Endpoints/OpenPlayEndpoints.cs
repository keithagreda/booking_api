using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class OpenPlayEndpoints
{
    public static WebApplication MapOpenPlayEndpoints(this WebApplication app)
    {
        var pub = app.MapGroup("/api/openplay").WithTags("Open Play");

        pub.MapGet("/windows", async (IOpenPlayService svc) =>
            Results.Ok(await svc.ListLiveAsync()))
            .AllowAnonymous();

        pub.MapGet("/windows/{id:guid}", async (Guid id, IOpenPlayService svc) =>
        {
            try { return Results.Ok(await svc.GetStateAsync(id)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        }).AllowAnonymous();

        pub.MapPost("/windows/{id:guid}/join", async (Guid id, JoinOpenPlayRequest body, HttpContext http, IOpenPlayService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                var response = await svc.JoinAsync(id, userId, body.PartnerUserId);
                return Results.Created($"/api/openplay/parties/{response.PartyId}", response);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).RequireAuthorization();

        pub.MapPost("/parties/{id:guid}/accept", async (Guid id, HttpContext http, IOpenPlayService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                return Results.Ok(await svc.AcceptPartyAsync(id, userId));
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).RequireAuthorization();

        pub.MapPost("/windows/{id:guid}/leave", async (Guid id, HttpContext http, IOpenPlayService svc) =>
        {
            var userId = http.User.GetUserId();
            return Results.Ok(await svc.LeaveAsync(id, userId));
        }).RequireAuthorization();

        return app;
    }
}
