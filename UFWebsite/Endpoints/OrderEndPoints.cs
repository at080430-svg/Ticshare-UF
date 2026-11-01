using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Services;
 
namespace UFWebsite.Endpoints;
 
public record BuyRequest(int ListingId);
 
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/api/orders").RequireAuthorization();
 
        // Step 1 of buying: reserve the ticket and get the Swish details.
        orders.MapPost("/", async (BuyRequest req, ClaimsPrincipal user, TicketFlow flow, IConfiguration config) =>
        {
            var order = await flow.ReserveAsync(req.ListingId, user.Id());
            if (order == null)
                return Results.Conflict(new { error = "Biljetten är inte längre tillgänglig." });
 
            return Results.Ok(new
            {
                order.Id,
                order.Reference,
                order.TotalAmount,
                SwishNumber = config["Ticshare:SwishNumber"],
                PayBefore = order.CreatedAt.AddMinutes(flow.MinutesToPay),
            });
        });
 
        orders.MapGet("/mine", async (ClaimsPrincipal user, AppDbContext db) =>
            await db.Orders.Where(o => o.BuyerId == user.Id())
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id, o.Reference, o.TotalAmount, o.Status, o.CreatedAt,
                    Match = o.Listing.Match.HomeClub.Name + " – " + o.Listing.Match.AwayClub.Name,
                    o.Listing.Match.StartTime,
                    o.Listing.SeasonCard.Section, o.Listing.SeasonCard.Row, o.Listing.SeasonCard.Seat,
                })
                .ToListAsync());
 
        orders.MapPost("/{id:int}/received", async (int id, ClaimsPrincipal user, TicketFlow flow) =>
            await flow.MarkCompletedAsync(id, user.Id()) ? Results.Ok() : Results.BadRequest());
 
        // Only while developing: pretend the buyer swished. Never reachable on the live site.
        if (app.Environment.IsDevelopment())
        {
            orders.MapPost("/{id:int}/simulate-payment", async (int id, ClaimsPrincipal user, AppDbContext db, TicketFlow flow) =>
            {
                var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.BuyerId == user.Id());
                if (order == null) return Results.NotFound();
                var ok = await flow.ConfirmPaymentAsync(id, order.TotalAmount, "SIMULERAD", "simulator", simulated: true);
                return ok ? Results.Ok() : Results.BadRequest();
            });
        }
    }
}
 