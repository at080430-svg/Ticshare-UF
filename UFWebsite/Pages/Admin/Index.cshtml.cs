using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
using UFWebsite.Services;

namespace UFWebsite.Pages.Admin;

public class IndexModel(AppDbContext db) : PageModel
{
    public int CardsToReview { get; set; }
    public int ListingsToReview { get; set; }
    public int AwaitingPayment { get; set; }
    public int ReadyForPayout { get; set; }
    public List<LoginEvent> RecentLogins { get; set; } = new();

    public async Task OnGetAsync()
    {
        CardsToReview = await db.SeasonCards.CountAsync(c => c.Status == SeasonCardStatus.PendingReview);
        ListingsToReview = await db.Listings.CountAsync(l => l.Status == ListingStatus.PendingReview);
        AwaitingPayment = await db.Orders.CountAsync(o => o.Status == OrderStatus.AwaitingPayment);
        ReadyForPayout = await db.Orders.CountAsync(o =>
            o.Status == OrderStatus.Completed && o.Listing.Match.StartTime < SwedishTime.Now);
        RecentLogins = await db.LoginEvents.OrderByDescending(e => e.At).Take(30).ToListAsync();
    }
}
