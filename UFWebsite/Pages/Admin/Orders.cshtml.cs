using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
using UFWebsite.Services;

namespace UFWebsite.Pages.Admin;

public class OrdersModel(AppDbContext db, TicketFlow flow) : PageModel
{
    public List<Order> AwaitingPayment { get; set; } = new();
    public List<Order> InProgress { get; set; } = new();
    public List<Order> ReadyForPayout { get; set; } = new();
    public List<Order> Recent { get; set; } = new();

    public async Task OnGetAsync()
    {
        await flow.ReleaseExpiredReservationsAsync();

        var query = db.Orders.Include(o => o.Buyer)
            .Include(o => o.Listing).ThenInclude(l => l.Seller)
            .Include(o => o.Listing).ThenInclude(l => l.Match).ThenInclude(m => m.HomeClub)
            .Include(o => o.Listing).ThenInclude(l => l.Match).ThenInclude(m => m.AwayClub);

        AwaitingPayment = await query.Where(o => o.Status == OrderStatus.AwaitingPayment)
            .OrderBy(o => o.CreatedAt).ToListAsync();
        InProgress = await query.Where(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.TicketSent)
            .OrderBy(o => o.Listing.Match.StartTime).ToListAsync();
        ReadyForPayout = await query.Where(o => o.Status == OrderStatus.Completed
                && o.Listing.Match.StartTime < SwedishTime.Now)
            .OrderBy(o => o.Listing.Match.StartTime).ToListAsync();
        Recent = await query.OrderByDescending(o => o.CreatedAt).Take(100).ToListAsync();
    }

    public async Task<IActionResult> OnPostConfirmPaymentAsync(int id, string amount, string? swishReference)
    {
        // Accept both "270" and "270,00".
        var parsed = decimal.TryParse(amount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var sum);
        var ok = parsed && await flow.ConfirmPaymentAsync(id, sum, swishReference, User.Identity!.Name!, simulated: false);
        return Done(ok, "Betalning registrerad. Säljaren kan nu föra över biljetten.",
            "Beloppet stämmer inte eller ordern väntar inte på betalning. Stämmer inte beloppet: swisha tillbaka pengarna.");
    }

    public async Task<IActionResult> OnPostCancelAsync(int id) =>
        Done(await flow.CancelUnpaidAsync(id), "Ordern avbruten, biljetten är till salu igen.", "Kunde inte avbryta.");

    public async Task<IActionResult> OnPostCompleteAsync(int id) =>
        Done(await flow.MarkCompletedAsync(id, buyerId: null), "Markerad som levererad.", "Kunde inte ändra status.");

    public async Task<IActionResult> OnPostRefundAsync(int id, string reason) =>
        Done(await flow.RefundAsync(id, reason, User.Identity!.Name!),
            "Registrerad som återbetald och biljetten är spärrad. Glöm inte att swisha tillbaka pengarna.",
            "Kunde inte återbetala.");

    public async Task<IActionResult> OnPostPaidOutAsync(int id, string? swishReference) =>
        Done(await flow.MarkPaidOutAsync(id, swishReference, User.Identity!.Name!),
            "Utbetalning registrerad.", "Kunde inte registrera utbetalningen (är matchen spelad?).");

    private IActionResult Done(bool ok, string success, string failure)
    {
        TempData["Message"] = ok ? success : failure;
        return RedirectToPage();
    }
}
