using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class AdminPaymentEndpoints
{
    public static WebApplication MapAdminPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/payments")
            .WithTags("Admin Payments")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapGet("/", async (IPaymentService svc) =>
        {
            var payments = await svc.ListForReviewAsync();
            return Results.Ok(payments);
        });

        group.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, IPaymentService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                var payment = await svc.ApproveAsync(id, userId);
                return Results.Ok(payment);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id:guid}/reject", async (Guid id, RejectPaymentRequest body, HttpContext http, IPaymentService svc) =>
        {
            try
            {
                var userId = http.User.GetUserId();
                var payment = await svc.RejectAsync(id, userId, body.Reason);
                return Results.Ok(payment);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        return app;
    }
}
