using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UFWebsite.Data;
using UFWebsite.Models;

namespace UFWebsite.Pages.Admin;

public class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
    AppDbContext db) : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await signIn.PasswordSignInAsync(Email, Password, isPersistent: false, lockoutOnFailure: true);
        db.LoginEvents.Add(new LoginEvent { Email = Email, Succeeded = result.Succeeded });
        await db.SaveChangesAsync();

        if (result.Succeeded)
        {
            var user = await users.FindByEmailAsync(Email);
            if (user != null && await users.IsInRoleAsync(user, "Admin"))
                return RedirectToPage("/Admin/Index");

            // Correct password, but not an admin: log straight out again.
            await signIn.SignOutAsync();
        }
        Error = result.IsLockedOut ? "Kontot är låst i 5 minuter." : "Fel e-post eller lösenord, eller inte admin.";
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Admin/Login");
    }
}
