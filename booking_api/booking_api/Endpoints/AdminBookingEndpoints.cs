using booking_api.Data;
using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Models;
using booking_api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Endpoints;

public static class AdminBookingEndpoints
{
    public static WebApplication MapAdminBookingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/bookings")
            .WithTags("Admin Bookings")
            .RequireAuthorization(p => p.RequireRole("Admin"));

        group.MapGet("/", async (AppDbContext db,
            int? page,
            int? pageSize,
            string? status,
            string? search,
            DateOnly? from,
            DateOnly? to,
            Guid? roomId,
            CancellationToken ct) =>
        {
            var p = page ?? 1;
            var ps = pageSize ?? 50;
            if (p < 1) p = 1;
            if (ps > 200) ps = 200;

            var query = db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Game)
                .Include(b => b.BookedByUser)
                .Include(b => b.Payment)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var parsed))
                query = query.Where(b => b.Status == parsed);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(b =>
                    b.BookedByUser.FirstName.Contains(search) ||
                    b.BookedByUser.LastName.Contains(search) ||
                    b.BookedByUser.Email!.Contains(search) ||
                    (b.Room != null && b.Room.Name.Contains(search)));

            if (from.HasValue)
            {
                var start = DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                query = query.Where(b => b.StartTime >= start);
            }

            if (to.HasValue)
            {
                var end = DateTime.SpecifyKind(to.Value.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
                query = query.Where(b => b.StartTime <= end);
            }

            if (roomId.HasValue)
                query = query.Where(b => b.RoomId == roomId.Value);

            var total = await query.CountAsync(ct);

            var items = await query
                .OrderByDescending(b => b.StartTime)
                .Skip((p - 1) * ps)
                .Take(ps)
                .ToListAsync(ct);

            var dtos = items.Select(b => new AdminBookingListDto(
                b.Id,
                b.RoomId,
                b.Room?.Name ?? "",
                b.Room?.Game?.Name ?? "",
                b.BookedByUserId,
                $"{b.BookedByUser.FirstName} {b.BookedByUser.LastName}",
                b.BookedByUser.Email ?? "",
                b.BookedByUser.IsProvisional,
                b.Type,
                b.Status,
                b.StartTime,
                b.EndTime,
                b.TotalAmount,
                b.Notes,
                b.HoldExpiresAt,
                b.Payment != null ? new PaymentSummaryDto(
                    b.Payment.Id,
                    b.Payment.Method,
                    b.Payment.Status,
                    b.Payment.Amount,
                    b.Payment.ReferenceNumber,
                    b.Payment.Remarks,
                    null,
                    b.Payment.ReviewedAt
                ) : null
            )).ToList();

            return Results.Ok(new { Items = dtos, Total = total, Page = p, PageSize = ps });
        });

        group.MapPost("/", async (HttpContext http,
            AppDbContext db,
            UserManager<User> userManager,
            ITrustScoreService trust,
            CreateAdminBookingRequest request,
            CancellationToken ct) =>
        {
            if (request.Hours < 1)
                return Results.BadRequest(new { error = "Hours must be at least 1." });

            var user = await userManager.FindByIdAsync(request.UserId.ToString());
            if (user is null)
                return Results.NotFound(new { error = "User not found." });

            var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == request.RoomId, ct);
            if (room is null)
                return Results.NotFound(new { error = "Room not found." });

            var start = DateTime.SpecifyKind(request.StartTime, DateTimeKind.Utc);
            var end = start.AddHours(request.Hours);

            var now = DateTime.UtcNow;

            var conflict = await db.Bookings.AnyAsync(b =>
                b.RoomId == room.Id
                && b.Type == BookingType.Regular
                && b.StartTime < end && b.EndTime > start
                && (b.Status == BookingStatus.Approved
                    || b.Status == BookingStatus.ProofSubmitted
                    || (b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt != null && b.HoldExpiresAt > now)),
                ct);

            if (conflict)
                return Results.Conflict(new { error = "Slot is no longer available." });

            var amount = room.HourlyRate * request.Hours;

            var booking = new Booking
            {
                RoomId = room.Id,
                BookedByUserId = user.Id,
                StartTime = start,
                EndTime = end,
                Type = BookingType.Regular,
                TotalAmount = amount,
                Notes = request.Notes
            };

            var paymentMethod = request.PaymentMethod;
            var isOutstanding = paymentMethod == PaymentMethod.Cash && string.IsNullOrEmpty(request.ReferenceNumber);

            if (isOutstanding)
            {
                booking.Status = BookingStatus.Approved;
                booking.Payment = new Payment
                {
                    BookingId = booking.Id,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Outstanding,
                    Amount = amount,
                    Remarks = request.Remarks
                };
                user.OutstandingBalance += amount;
            }
            else
            {
                booking.Status = BookingStatus.Approved;
                booking.Payment = new Payment
                {
                    BookingId = booking.Id,
                    Method = paymentMethod,
                    Status = PaymentStatus.Approved,
                    Amount = amount,
                    ReferenceNumber = request.ReferenceNumber,
                    Remarks = request.Remarks,
                    ReviewedByUserId = http.User.GetUserId(),
                    ReviewedAt = DateTime.UtcNow
                };

                await trust.AdjustAsync(
                    user.Id,
                    TrustAdjustmentReason.BookingApproved,
                    1f,
                    "Admin-created booking, paid immediately",
                    booking.Id,
                    http.User.GetUserId(),
                    ct);
            }

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/admin/bookings/{booking.Id}", new { booking.Id });
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var booking = await db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Game)
                .Include(b => b.BookedByUser)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == id, ct);

            if (booking is null) return Results.NotFound();

            var dto = new AdminBookingListDto(
                booking.Id,
                booking.RoomId,
                booking.Room?.Name ?? "",
                booking.Room?.Game?.Name ?? "",
                booking.BookedByUserId,
                $"{booking.BookedByUser.FirstName} {booking.BookedByUser.LastName}",
                booking.BookedByUser.Email ?? "",
                booking.BookedByUser.IsProvisional,
                booking.Type,
                booking.Status,
                booking.StartTime,
                booking.EndTime,
                booking.TotalAmount,
                booking.Notes,
                booking.HoldExpiresAt,
                booking.Payment != null ? new PaymentSummaryDto(
                    booking.Payment.Id,
                    booking.Payment.Method,
                    booking.Payment.Status,
                    booking.Payment.Amount,
                    booking.Payment.ReferenceNumber,
                    booking.Payment.Remarks,
                    null,
                    booking.Payment.ReviewedAt
                ) : null
            );

            return Results.Ok(dto);
        });

        group.MapPut("/{id:guid}", async (Guid id, AppDbContext db, UpdateAdminBookingRequest request, CancellationToken ct) =>
        {
            var booking = await db.Bookings.FindAsync([id], cancellationToken: ct);
            if (booking is null) return Results.NotFound();

            if (request.StartTime.HasValue) booking.StartTime = DateTime.SpecifyKind(request.StartTime.Value, DateTimeKind.Utc);
            if (request.Hours.HasValue) booking.EndTime = booking.StartTime.AddHours(request.Hours.Value);
            if (request.EndTime.HasValue) booking.EndTime = DateTime.SpecifyKind(request.EndTime.Value, DateTimeKind.Utc);
            if (request.Notes != null) booking.Notes = request.Notes;
            if (request.TotalAmount.HasValue) booking.TotalAmount = request.TotalAmount.Value;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Booking updated." });
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http, ITrustScoreService trust, CancellationToken ct) =>
        {
            var booking = await db.Bookings.FindAsync([id], cancellationToken: ct);
            if (booking is null) return Results.NotFound();

            if (booking.Status is BookingStatus.Cancelled or BookingStatus.Expired)
                return Results.BadRequest(new { error = "Booking is already cancelled or expired." });

            var prevStatus = booking.Status;
            booking.Status = BookingStatus.Cancelled;

            if (booking.Payment != null && booking.Payment.Status == PaymentStatus.Outstanding)
            {
                var user = await db.Users.FindAsync([booking.BookedByUserId], cancellationToken: ct);
                if (user != null)
                {
                    user.OutstandingBalance -= booking.Payment.Amount;
                    if (user.OutstandingBalance < 0) user.OutstandingBalance = 0;
                }
            }

            await db.SaveChangesAsync(ct);

            if (prevStatus == BookingStatus.Approved)
            {
                await trust.AdjustAsync(
                    booking.BookedByUserId,
                    TrustAdjustmentReason.BookingCancelled,
                    -1f,
                    "Booking cancelled by admin",
                    booking.Id,
                    http.User.GetUserId(),
                    ct);
            }

            return Results.Ok(new { message = "Booking cancelled." });
        });

        group.MapPost("/{id:guid}/settle", async (Guid id, HttpContext http,
            IPaymentService paymentService,
            SettlePaymentRequest request,
            CancellationToken ct) =>
        {
            try
            {
                var booking = await paymentService.SettleOutstandingAsync(
                    id,
                    request.Method,
                    request.ReferenceNumber,
                    request.Remarks,
                    http.User.GetUserId(),
                    ct);

                return Results.Ok(booking);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        return app;
    }
}
