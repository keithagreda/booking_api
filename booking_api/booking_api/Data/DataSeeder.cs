using booking_api.Models;
using Microsoft.AspNetCore.Identity;

namespace booking_api.Data;

public static class DataSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var existingAdmin = await userManager.FindByEmailAsync("admin@sportshub.com");
        if (existingAdmin is not null)
            return;

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
}
