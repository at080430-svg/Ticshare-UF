using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;

namespace UFWebsite.Pages.Admin;

public class MatchesModel(AppDbContext db) : PageModel
{
    public List<Club> Clubs { get; set; } = new();
    public List<Match> Matches { get; set; } = new();

    public async Task OnGetAsync()
    {
        Clubs = await db.Clubs.OrderBy(c => c.Name).ToListAsync();
        Matches = await db.Matches.Include(m => m.HomeClub).Include(m => m.AwayClub).Include(m => m.Listings)
            .OrderByDescending(m => m.StartTime).ToListAsync();
    }

    public async Task<IActionResult> OnPostAddMatchAsync(int homeClubId, int awayClubId,
        DateTime startTime, string arena, string? competition)
    {
        if (homeClubId == awayClubId)
        {
            TempData["Message"] = "Ett lag kan inte möta sig självt.";
            return RedirectToPage();
        }
        db.Matches.Add(new Match
        {
            HomeClubId = homeClubId, AwayClubId = awayClubId, StartTime = startTime,
            Arena = arena, Competition = competition,
        });
        await db.SaveChangesAsync();
        TempData["Message"] = "Matchen är tillagd. Publicera den när den ska synas på sajten.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddClubAsync(string name, string sport, string city,
        bool allowsResale, string? resaleRules, VerificationMethod verificationMethod)
    {
        db.Clubs.Add(new Club
        {
            Name = name, Sport = sport, City = city, AllowsResale = allowsResale,
            ResaleRules = resaleRules, VerificationMethod = verificationMethod,
        });
        await db.SaveChangesAsync();
        TempData["Message"] = $"{name} är tillagd.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTogglePublishAsync(int id)
    {
        var match = await db.Matches.FindAsync(id);
        if (match == null) return NotFound();
        match.IsPublished = !match.IsPublished;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
