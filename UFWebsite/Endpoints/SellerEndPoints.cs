using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
using UFWebsite.Services;
 
namespace UFWebsite.Endpoints;
 
public record SeasonCardRequest(int ClubId, string Season, string CardNumber,
    string Section, string Row, string Seat, bool AcceptedTerms);
public record ListingRequest(int SeasonCardId, int MatchId, decimal Price, decimal FaceValue);
public record SwishRequest(string SwishNumber);
 
// Everything a logged-in seller does. RequireAuthorization() = must be logged in.
public static class SellerEndpoints
{
    public static void MapSellerEndpoints(this WebApplication app)
    {
        var me = app.MapGroup("/api/me").RequireAuthorization();
 
        me.MapPut("/swish", async (SwishRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var digits = new string(req.SwishNumber.Where(char.IsDigit).ToArray());
            if (digits.Length is < 10 or > 12)
                return Results.BadRequest(new { error = "Ange ett giltigt mobilnummer." });
 
            var u = await db.Users.FirstAsync(x => x.Id == user.Id());
            u.SwishNumber = digits;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
 
        // ----- Season cards -----
 
        me.MapGet("/seasoncards", async (ClaimsPrincipal user, AppDbContext db) =>
            await db.SeasonCards.Where(c => c.UserId == user.Id())
                .OrderByDescending(c => c.SubmittedAt)
                .Select(c => new
                {
                    c.Id, Club = c.Club.Name, c.ClubId, c.Season, c.CardNumber,
                    c.Section, c.Row, c.Seat, c.Status, c.RejectionReason,
                })
                .ToListAsync());
 
        me.MapPost("/seasoncards", async (SeasonCardRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var club = await db.Clubs.FindAsync(req.ClubId);
            if (club == null) return Results.BadRequest(new { error = "Okänd klubb." });
            if (!club.AllowsResale)
                return Results.BadRequest(new { error = $"{club.Name} tillåter inte vidareförsäljning via Ticshare." });
            if (!req.AcceptedTerms)
                return Results.BadRequest(new { error = "Du måste intyga att du får sälja vidare biljetter från ditt säsongskort." });
 
            var card = new SeasonCard
            {
                UserId = user.Id(), ClubId = req.ClubId, Season = req.Season.Trim(),
                CardNumber = req.CardNumber.Trim(), Section = req.Section.Trim(),
                Row = req.Row.Trim(), Seat = req.Seat.Trim(), SellerAcceptedTerms = true,
            };
            db.SeasonCards.Add(card);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "Det här säsongskortet är redan registrerat." });
            }
            return Results.Ok(new { card.Id, card.Status });
        });
 
        // ----- Listings (tickets for sale) -----
 
        me.MapGet("/listings", async (ClaimsPrincipal user, AppDbContext db) =>
            await db.Listings.Where(l => l.SellerId == user.Id())
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    Match = l.Match.HomeClub.Name + " – " + l.Match.AwayClub.Name,
                    l.Match.StartTime,
                    l.Price, l.Status, l.RejectionReason,
                    // The latest order on this ticket, if any. The seller needs the buyer's
                    // name and email to transfer the ticket in the club's system.
                    Order = db.Orders.Where(o => o.ListingId == l.Id)
                        .OrderByDescending(o => o.CreatedAt)
                        .Select(o => new
                        {
                            o.Id, o.Status,
                            BuyerName = o.Status == OrderStatus.AwaitingPayment ? null : o.Buyer.FullName,
                            BuyerEmail = o.Status == OrderStatus.AwaitingPayment ? null : o.Buyer.Email,
                        })
                        .FirstOrDefault(),
                })
                .ToListAsync());
 
        me.MapPost("/listings", async (ListingRequest req, ClaimsPrincipal user, AppDbContext db) =>
        {
            var userId = user.Id();
            var seller = await db.Users.FirstAsync(u => u.Id == userId);
            if (string.IsNullOrEmpty(seller.SwishNumber))
                return Results.BadRequest(new { error = "Lägg till ditt Swish-nummer först, så vi kan betala dig." });
 
            var card = await db.SeasonCards.Include(c => c.Club)
                .FirstOrDefaultAsync(c => c.Id == req.SeasonCardId && c.UserId == userId);
            if (card == null || card.Status != SeasonCardStatus.Approved)
                return Results.BadRequest(new { error = "Säsongskortet är inte godkänt ännu." });
 
            var match = await db.Matches.FindAsync(req.MatchId);
            if (match == null || !match.IsPublished || match.StartTime < SwedishTime.Now)
                return Results.BadRequest(new { error = "Matchen finns inte eller har redan spelats." });
 
            // A season card is valid for the club's home matches.
            if (match.HomeClubId != card.ClubId)
                return Results.BadRequest(new { error = $"Kortet gäller bara {card.Club.Name}s hemmamatcher." });
 
            if (req.Price <= 0 || req.Price > req.FaceValue)
                return Results.BadRequest(new { error = "Priset får inte vara högre än biljettens ordinarie pris." });
 
            var listing = new Listing
            {
                SeasonCardId = card.Id, MatchId = match.Id, SellerId = userId,
                Price = req.Price, FaceValue = req.FaceValue,
            };
            db.Listings.Add(listing);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "Du har redan lagt ut den här platsen för matchen." });
            }
            return Results.Ok(new { listing.Id, listing.Status });
        });
 
        me.MapDelete("/listings/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db) =>
        {
            var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id && l.SellerId == user.Id());
            if (listing == null) return Results.NotFound();
            if (listing.Status is not (ListingStatus.PendingReview or ListingStatus.Available))
                return Results.BadRequest(new { error = "Biljetten är redan reserverad eller såld." });
 
            listing.Status = ListingStatus.Cancelled;
            await db.SaveChangesAsync();
            return Results.Ok();
        });
 
        me.MapPost("/sales/{orderId:int}/ticket-sent", async (int orderId, ClaimsPrincipal user, TicketFlow flow) =>
            await flow.MarkTicketSentAsync(orderId, user.Id()) ? Results.Ok() : Results.BadRequest());
    }
 
    // Small helper: the logged-in user's id.
    public static string Id(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
 