using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;

namespace UFWebsite.Pages.Admin;

public class UsersModel(AppDbContext db) : PageModel
{
    public record Row(string FullName, string? Email, string? SwishNumber, DateTime CreatedAt, int Cards, bool IsAdmin);
    public List<Row> Users { get; set; } = new();

    public async Task OnGetAsync()
    {
        var adminRoleId = await db.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();

        // Note: no PasswordHash here. We only select the columns admins need.
        Users = await db.Users.OrderByDescending(u => u.CreatedAt)
            .Select(u => new Row(u.FullName, u.Email, u.SwishNumber, u.CreatedAt,
                db.SeasonCards.Count(c => c.UserId == u.Id),
                db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == adminRoleId)))
            .ToListAsync();
    }
}
