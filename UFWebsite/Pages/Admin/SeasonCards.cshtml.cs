using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;

namespace UFWebsite.Pages.Admin;

public class SeasonCardsModel(AppDbContext db) : PageModel
{
    public record Row(SeasonCard Card, bool InClubList);
    public List<Row> Pending { get; set; } = new();

    public async Task OnGetAsync()
    {
        var cards = await db.SeasonCards.Include(c => c.User).Include(c => c.Club)
            .Where(c => c.Status == SeasonCardStatus.PendingReview)
            .OrderBy(c => c.SubmittedAt).ToListAsync();

        foreach (var card in cards)
        {
            var inList = await db.ClubCardRecords.AnyAsync(r =>
                r.ClubId == card.ClubId && r.Season == card.Season && r.CardNumber == card.CardNumber);
            Pending.Add(new Row(card, inList));
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id) =>
        await SetStatus(id, SeasonCardStatus.Approved, null);

    public async Task<IActionResult> OnPostRejectAsync(int id, string reason) =>
        await SetStatus(id, SeasonCardStatus.Rejected, reason);

    private async Task<IActionResult> SetStatus(int id, SeasonCardStatus status, string? reason)
    {
        var card = await db.SeasonCards.FindAsync(id);
        if (card == null || card.Status != SeasonCardStatus.PendingReview) return NotFound();

        card.Status = status;
        card.RejectionReason = reason;
        card.ReviewedAt = DateTime.UtcNow;
        card.ReviewedBy = User.Identity!.Name;
        await db.SaveChangesAsync();
        TempData["Message"] = $"Kort {card.CardNumber}: {status}";
        return RedirectToPage();
    }
}
