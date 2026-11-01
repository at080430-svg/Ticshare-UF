using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
 
namespace UFWebsite.Endpoints;
 
public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
 
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
 
        auth.MapPost("/register", async (RegisterRequest req, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            var user = new ApplicationUser
            {
                UserName = req.Email, Email = req.Email, FullName = req.FullName.Trim(),
            };
            // Identity hashes the password. Nobody, not even admin, can read it afterwards.
            var result = await users.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
 
            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Ok(new { user.Email, user.FullName });
        });
 
        auth.MapPost("/login", async (LoginRequest req, SignInManager<ApplicationUser> signIn, AppDbContext db) =>
        {
            // lockoutOnFailure: 5 wrong passwords locks the account for 5 minutes.
            var result = await signIn.PasswordSignInAsync(req.Email, req.Password,
                isPersistent: true, lockoutOnFailure: true);
 
            db.LoginEvents.Add(new LoginEvent { Email = req.Email, Succeeded = result.Succeeded });
            await db.SaveChangesAsync();
 
            if (result.IsLockedOut) return Results.Problem("Kontot är tillfälligt låst. Försök igen om 5 minuter.", statusCode: 423);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        });
 
        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Ok();
        });
 
        auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user == null) return Results.Unauthorized();
            return Results.Ok(new
            {
                user.Email, user.FullName, user.SwishNumber,
                IsAdmin = await users.IsInRoleAsync(user, "Admin"),
            });
        });
 
        // GDPR: the user can delete their account. Orders must be kept for
        // bookkeeping, so we anonymise the person instead of deleting the rows.
        auth.MapDelete("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, AppDbContext db) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user == null) return Results.Unauthorized();
 
            var hasOpenDeals = await db.Orders.AnyAsync(o =>
                (o.BuyerId == user.Id || o.Listing.SellerId == user.Id) &&
                o.Status != OrderStatus.PaidOut && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded);
            if (hasOpenDeals)
                return Results.BadRequest(new { error = "Du har pågående köp eller försäljningar. Kontakta oss." });
 
            await signIn.SignOutAsync();
            var anon = $"deleted-{user.Id}@ticshare.invalid";
            user.FullName = "Raderad användare";
            user.Email = user.UserName = anon;
            user.NormalizedEmail = user.NormalizedUserName = anon.ToUpperInvariant();
            user.PhoneNumber = null;
            user.SwishNumber = null;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            await users.RemovePasswordAsync(user);
            await users.UpdateAsync(user);
            return Results.Ok();
        }).RequireAuthorization();
    }
}
 