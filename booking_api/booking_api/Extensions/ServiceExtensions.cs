using booking_api.Data;
using booking_api.Services;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }

    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3Settings>(configuration.GetSection("S3"));
        services.AddSingleton<IS3Service, S3Service>();

        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IOpenPlayService, OpenPlayService>();
        services.AddScoped<IMatchmakingService, MatchmakingService>();
        services.AddScoped<IDisplayService, DisplayService>();
        services.AddScoped<IAdminCatalogService, AdminCatalogService>();
        services.AddSingleton<ILiveBroadcaster, LiveBroadcaster>();

        services.AddHostedService<HoldExpiryWorker>();

        return services;
    }
}
