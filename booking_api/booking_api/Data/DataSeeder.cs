using booking_api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace booking_api.Data;

public static class DataSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var existingAdmin = await userManager.FindByEmailAsync("admin@sportshub.com");
        if (existingAdmin is null)
        {
            var admin = new User
            {
                UserName = "admin@sportshub.com",
                Email = "admin@sportshub.com",
                FirstName = "System",
                LastName = "Admin",
                Role = Role.Admin,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");

            if (result.Succeeded)
                Console.WriteLine($"Seeded admin user: {admin.Email}");
            else
                Console.WriteLine($"Failed to seed admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await SeedGamesAndRoomsAsync(scope.ServiceProvider);
    }

    private static async Task SeedGamesAndRoomsAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();

        if (await db.Games.AnyAsync())
            return;

        var badminton = new Game { Name = "Badminton", Description = "Indoor badminton courts" };
        var tableTennis = new Game { Name = "Table Tennis", Description = "Table tennis courts" };
        db.Games.AddRange(badminton, tableTennis);

        var rooms = new[]
        {
            new Room { Name = "Court 1", GameId = badminton.Id, Capacity = 4, HourlyRate = 250m },
            new Room { Name = "Court 2", GameId = badminton.Id, Capacity = 4, HourlyRate = 250m },
            new Room { Name = "Court 3", GameId = badminton.Id, Capacity = 4, HourlyRate = 300m },
            new Room { Name = "Table A", GameId = tableTennis.Id, Capacity = 4, HourlyRate = 200m },
            new Room { Name = "Table B", GameId = tableTennis.Id, Capacity = 4, HourlyRate = 200m }
        };
        db.Rooms.AddRange(rooms);

        var todayUtc = DateTime.UtcNow.Date;
        db.RoomStatusWindows.Add(new RoomStatusWindow
        {
            RoomId = rooms[2].Id,
            Status = RoomStatus.OpenPlay,
            StartTime = todayUtc.AddHours(18),
            EndTime = todayUtc.AddHours(22),
            SeatRate = 100m,
            MatchSize = 4,
            QueueCap = 16,
            Notes = "Evening open play"
        });

        await db.SaveChangesAsync();
        Console.WriteLine("Seeded games, rooms, and a sample open-play window.");
    }
}
