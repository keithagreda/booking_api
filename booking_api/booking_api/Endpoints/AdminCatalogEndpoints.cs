using booking_api.DTOs;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class AdminCatalogEndpoints
{
    public static WebApplication MapAdminCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin Catalog")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        // Games
        group.MapPost("/games", async (CreateGameRequest body, IAdminCatalogService svc) =>
            await Wrap(async () => Results.Json(await svc.CreateGameAsync(body), statusCode: 201)));
        group.MapPut("/games/{id:guid}", async (Guid id, UpdateGameRequest body, IAdminCatalogService svc) =>
            await Wrap(async () => Results.Ok(await svc.UpdateGameAsync(id, body))));
        group.MapDelete("/games/{id:guid}", async (Guid id, IAdminCatalogService svc) =>
            await Wrap(async () => { await svc.DeleteGameAsync(id); return Results.NoContent(); }));

        // Rooms
        group.MapGet("/rooms", async (Guid? gameId, IAdminCatalogService svc) =>
            Results.Ok(await svc.ListRoomsAsync(gameId)));
        group.MapPost("/rooms", async (CreateRoomRequest body, IAdminCatalogService svc) =>
            await Wrap(async () => Results.Json(await svc.CreateRoomAsync(body), statusCode: 201)));
        group.MapPut("/rooms/{id:guid}", async (Guid id, UpdateRoomRequest body, IAdminCatalogService svc) =>
            await Wrap(async () => Results.Ok(await svc.UpdateRoomAsync(id, body))));
        group.MapDelete("/rooms/{id:guid}", async (Guid id, IAdminCatalogService svc) =>
            await Wrap(async () => { await svc.DeleteRoomAsync(id); return Results.NoContent(); }));

        // Schedule windows
        group.MapGet("/rooms/{roomId:guid}/schedule",
            async (Guid roomId, DateTime? from, DateTime? to, IAdminCatalogService svc) =>
                Results.Ok(await svc.ListWindowsAsync(roomId, from, to)));
        group.MapPost("/rooms/{roomId:guid}/schedule",
            async (Guid roomId, CreateScheduleWindowRequest body, IAdminCatalogService svc) =>
                await Wrap(async () => Results.Json(await svc.CreateWindowAsync(roomId, body), statusCode: 201)));
        group.MapPut("/schedule/{windowId:guid}",
            async (Guid windowId, UpdateScheduleWindowRequest body, IAdminCatalogService svc) =>
                await Wrap(async () => Results.Ok(await svc.UpdateWindowAsync(windowId, body))));
        group.MapDelete("/schedule/{windowId:guid}",
            async (Guid windowId, IAdminCatalogService svc) =>
                await Wrap(async () => { await svc.DeleteWindowAsync(windowId); return Results.NoContent(); }));

        return app;
    }

    private static async Task<IResult> Wrap(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
    }
}
