using Microsoft.AspNetCore.Identity;
using UFWebsite.Models;

namespace UFWebsite.Data;

public static class SeedData
{
    // Creates the Admin role and the first admin account when the site starts.
    // The email and password come from "user secrets", never from the code.
    public static async Task CreateAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (!await roles.RoleExistsAsync("Admin"))
            await roles.CreateAsync(new IdentityRole("Admin"));

        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;

        var admin = await users.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, FullName = "Ticshare Admin" };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new Exception("Could not create admin: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        if (!await users.IsInRoleAsync(admin, "Admin"))
            await users.AddToRoleAsync(admin, "Admin");
    }
}