using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class BookingEndpoints
{
    public static WebApplication MapBookingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bookings").WithTags("Bookings").RequireAuthorization();

        group.MapPost("/", async (CreateRegularBookingRequest request, HttpContext http, IBookingService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                var booking = await svc.CreateRegularAsync(userId, request);
                return Results.Created($"/api/bookings/{booking.Id}", booking);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapGet("/mine", async (HttpContext http, IBookingService svc) =>
        {
            var userId = http.User.GetUserId();
            var bookings = await svc.GetMineAsync(userId);
            return Results.Ok(bookings);
        });

        group.MapGet("/{id:guid}", async (Guid id, IBookingService svc) =>
        {
            var booking = await svc.GetAsync(id);
            return booking is null ? Results.NotFound() : Results.Ok(booking);
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext http, IBookingService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                var booking = await svc.CancelAsync(id, userId);
                return Results.Ok(booking);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/payment/proof", async (Guid id, HttpRequest req, HttpContext http, IPaymentService svc) =>
        {
            if (!req.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data required." });

            var form = await req.ReadFormAsync();
            var file = form.Files["proof"] ?? form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "Proof file is required." });

            var gcashReference = form["gcashReference"].ToString();

            try
            {
                var userId = http.User.GetUserId();
                await using var stream = file.OpenReadStream();
                var payment = await svc.SubmitProofAsync(id, userId, stream, file.ContentType, string.IsNullOrWhiteSpace(gcashReference) ? null : gcashReference);
                return Results.Ok(payment);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (UnauthorizedAccessException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        }).DisableAntiforgery();

        return app;
    }
}
