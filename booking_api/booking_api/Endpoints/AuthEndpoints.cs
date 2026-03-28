using booking_api.DTOs;
using booking_api.Extensions;
using booking_api.Services;

namespace booking_api.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService) =>
        {
            try
            {
                var response = await authService.RegisterAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, IAuthService authService) =>
        {
            try
            {
                var response = await authService.LoginAsync(request);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 401);
            }
        })
        .AllowAnonymous();

        group.MapGet("/me", async (HttpContext httpContext, IAuthService authService) =>
        {
            var userId = httpContext.User.GetUserId();
            var user = await authService.GetCurrentUserAsync(userId);
            return Results.Ok(user);
        })
        .RequireAuthorization();

        return app;
    }
}
