using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
 
namespace UFWebsite.Pages.Admin;
 
public class MatchesModel(AppDbContext db) : PageModel
{
    public List<Club> Clubs { get; set; } = new();

    // The clubs grouped by sport, for the dropdowns under "Ny match".
    public IEnumerable<IGrouping<string, Club>> ClubsBySport =>
        Clubs.GroupBy(c => c.Sport).OrderBy(g => g.Key);
    public List<Match> Matches { get; set; } = new();
 
    public async Task OnGetAsync()
    {
        Clubs = await db.Clubs.OrderBy(c => c.Sport).ThenBy(c => c.Name).ToListAsync();
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
        var sports = await db.Clubs.Where(c => c.Id == homeClubId || c.Id == awayClubId)
            .Select(c => c.Sport).Distinct().CountAsync();
        if (sports > 1)
        {
            TempData["Message"] = "Lagen måste spela samma sport.";
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
 
    // Only matches without any listings can be deleted, so no orders or payments are lost.
    public async Task<IActionResult> OnPostDeleteMatchAsync(int id)
    {
        var match = await db.Matches.Include(m => m.Listings).FirstOrDefaultAsync(m => m.Id == id);
        if (match == null) return NotFound();
        if (match.Listings.Count > 0)
        {
            TempData["Message"] = "Matchen har biljetter och kan inte tas bort. Dölj den istället.";
            return RedirectToPage();
        }
        db.Matches.Remove(match);
        await db.SaveChangesAsync();
        TempData["Message"] = "Matchen är borttagen.";
        return RedirectToPage();
    }
 
    // Only clubs that nothing points to can be deleted (no matches, season cards or club lists).
    public async Task<IActionResult> OnPostDeleteClubAsync(int id)
    {
        var club = await db.Clubs.FindAsync(id);
        if (club == null) return NotFound();
 
        var inUse = await db.Matches.AnyAsync(m => m.HomeClubId == id || m.AwayClubId == id)
            || await db.SeasonCards.AnyAsync(c => c.ClubId == id)
            || await db.ClubCardRecords.AnyAsync(r => r.ClubId == id);
        if (inUse)
        {
            TempData["Message"] = $"{club.Name} används av matcher eller säsongskort och kan inte tas bort.";
            return RedirectToPage();
        }
        db.Clubs.Remove(club);
        await db.SaveChangesAsync();
        TempData["Message"] = $"{club.Name} är borttagen.";
        return RedirectToPage();
    }
}
 