using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
using UFWebsite.Services;

namespace UFWebsite.Endpoints;

// Public endpoints: anyone can browse matches and tickets without logging in.
public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        app.MapGet("/api/clubs", async (AppDbContext db) =>
            await db.Clubs.OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Sport, c.AllowsResale, c.ResaleRules })
                .ToListAsync());

        // Cities that have upcoming matches, for the city dropdown in the search bar.
        app.MapGet("/api/cities", async (AppDbContext db) =>
            await db.Matches.Where(m => m.IsPublished && m.StartTime > SwedishTime.Now)
                .Select(m => m.HomeClub.City).Distinct().OrderBy(c => c).ToListAsync());

        // ?sport=Ishockey&q=Brynäs&city=Gävle (all optional)
        app.MapGet("/api/matches", async (AppDbContext db, TicketFlow flow, string? sport, string? q, string? city) =>
        {
            await flow.ReleaseExpiredReservationsAsync();

            var query = db.Matches.Where(m => m.IsPublished && m.StartTime > SwedishTime.Now);
            if (!string.IsNullOrEmpty(sport) && sport != "Alla")
                query = query.Where(m => m.HomeClub.Sport == sport);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var text = q.Trim();
                query = query.Where(m => m.HomeClub.Name.Contains(text)
                    || m.AwayClub.Name.Contains(text) || m.Arena.Contains(text));
            }
            if (!string.IsNullOrEmpty(city))
                query = query.Where(m => m.HomeClub.City == city);

            return await query.OrderBy(m => m.StartTime)
                .Select(m => new
                {
                    m.Id,
                    m.HomeClubId,
                    Home = m.HomeClub.Name,
                    Away = m.AwayClub.Name,
                    m.HomeClub.Sport,
                    m.StartTime,
                    m.Arena,
                    m.HomeClub.City,
                    m.Competition,
                    TicketsForSale = m.Listings.Count(l => l.Status == ListingStatus.Available),
                    LowestPrice = m.Listings.Where(l => l.Status == ListingStatus.Available)
                        .Min(l => (decimal?)l.Price),
                    // The club's normal price for the cheapest ticket, shown crossed out.
                    LowestFaceValue = m.Listings.Where(l => l.Status == ListingStatus.Available)
                        .OrderBy(l => l.Price).Select(l => (decimal?)l.FaceValue).FirstOrDefault(),
                })
                .ToListAsync();
        });

        app.MapGet("/api/matches/{id:int}", async (int id, AppDbContext db, TicketFlow flow) =>
        {
            await flow.ReleaseExpiredReservationsAsync();

            var match = await db.Matches
                .Where(m => m.Id == id && m.IsPublished)
                .Select(m => new
                {
                    m.Id,
                    Home = m.HomeClub.Name,
                    Away = m.AwayClub.Name,
                    m.HomeClub.Sport,
                    m.StartTime,
                    m.Arena,
                    m.Competition,
                    flow.ServiceFee,
                    Tickets = m.Listings
                        .Where(l => l.Status == ListingStatus.Available)
                        .OrderBy(l => l.Price)
                        .Select(l => new { l.Id, l.Price, l.SeasonCard.Section, l.SeasonCard.Row, l.SeasonCard.Seat })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            return match == null ? Results.NotFound() : Results.Ok(match);
        });
    }
}
 