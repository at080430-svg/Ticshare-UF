using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;

namespace UFWebsite.Pages.Admin;

public class ListingsModel(AppDbContext db) : PageModel
{
    public List<Listing> Pending { get; set; } = new();
    public List<Listing> All { get; set; } = new();

    public async Task OnGetAsync()
    {
        var query = db.Listings
            .Include(l => l.Seller).Include(l => l.SeasonCard)
            .Include(l => l.Match).ThenInclude(m => m.HomeClub)
            .Include(l => l.Match).ThenInclude(m => m.AwayClub);

        Pending = await query.Where(l => l.Status == ListingStatus.PendingReview)
            .OrderBy(l => l.CreatedAt).ToListAsync();
        All = await query.OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id) =>
        await SetStatus(id, ListingStatus.Available, null);

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason) =>
        await SetStatus(id, ListingStatus.Rejected, reason);

    private async Task<IActionResult> SetStatus(int id, ListingStatus status, string? reason)
    {
        var listing = await db.Listings.FindAsync(id);
        if (listing == null || listing.Status != ListingStatus.PendingReview) return NotFound();

        listing.Status = status;
        listing.RejectionReason = reason;
        listing.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Message"] = $"Biljett #{listing.Id}: {status}";
        return RedirectToPage();
    }
}
