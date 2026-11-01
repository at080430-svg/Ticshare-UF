using Microsoft.EntityFrameworkCore;
using UFWebsite.Data;
using UFWebsite.Models;
 
namespace UFWebsite.Services;
 
// All status changes for listings and orders live here, so the API and the
// admin panel follow exactly the same rules.
public class TicketFlow(AppDbContext db, IConfiguration config)
{
    public decimal ServiceFee => config.GetValue("Ticshare:ServiceFee", 20m);
    public int MinutesToPay => config.GetValue("Ticshare:MinutesToPay", 30);
 
    // A buyer reserves a ticket. Returns null if someone else got it first.
    public async Task<Order?> ReserveAsync(int listingId, string buyerId)
    {
        await ReleaseExpiredReservationsAsync();
 
        var listing = await db.Listings.Include(l => l.Match)
            .FirstOrDefaultAsync(l => l.Id == listingId);
        if (listing == null || listing.SellerId == buyerId || listing.Match.StartTime < SwedishTime.Now)
            return null;
 
        // "Only change it if it is still Available". If two buyers click at the same
        // moment, the database lets exactly one of them through (1 row changed).
        var changed = await db.Listings
            .Where(l => l.Id == listingId && l.Status == ListingStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, ListingStatus.Reserved));
        if (changed == 0) return null;
 
        var order = new Order
        {
            ListingId = listingId,
            BuyerId = buyerId,
            Reference = await NewReferenceAsync(),
            TicketPrice = listing.Price,
            ServiceFee = ServiceFee,
            TotalAmount = listing.Price + ServiceFee,
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
 
    // Admin has seen the Swish payment arrive (or a simulated one in development).
    public async Task<bool> ConfirmPaymentAsync(int orderId, decimal amount, string? swishReference,
        string recordedBy, bool simulated)
    {
        var order = await db.Orders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status != OrderStatus.AwaitingPayment || amount != order.TotalAmount)
            return false;
 
        order.Status = OrderStatus.Paid;
        order.PaidAt = DateTime.UtcNow;
        order.Listing.Status = ListingStatus.Sold;
        db.Payments.Add(new Payment
        {
            OrderId = order.Id, Type = PaymentType.Incoming, Amount = amount,
            SwishReference = swishReference, RecordedBy = recordedBy, IsSimulated = simulated,
        });
        await db.SaveChangesAsync();
        return true;
    }
 
    // Seller has transferred the ticket in the club's own system.
    public async Task<bool> MarkTicketSentAsync(int orderId, string sellerId)
    {
        var order = await db.Orders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Listing.SellerId != sellerId || order.Status != OrderStatus.Paid)
            return false;
 
        order.Status = OrderStatus.TicketSent;
        order.TicketSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
 
    // Buyer confirms the ticket arrived (admin can also do this after the match).
    public async Task<bool> MarkCompletedAsync(int orderId, string? buyerId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status != OrderStatus.TicketSent) return false;
        if (buyerId != null && order.BuyerId != buyerId) return false;
 
        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
 
    // Admin has swished the money to the seller. Only allowed after the match.
    public async Task<bool> MarkPaidOutAsync(int orderId, string? swishReference, string recordedBy)
    {
        var order = await db.Orders.Include(o => o.Listing).ThenInclude(l => l.Match)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status != OrderStatus.Completed
            || order.Listing.Match.StartTime > SwedishTime.Now)
            return false;
 
        order.Status = OrderStatus.PaidOut;
        order.PaidOutAt = DateTime.UtcNow;
        db.Payments.Add(new Payment
        {
            OrderId = order.Id, Type = PaymentType.Payout, Amount = order.TicketPrice,
            SwishReference = swishReference, RecordedBy = recordedBy,
        });
        await db.SaveChangesAsync();
        return true;
    }
 
    // Admin has sent the money back to the buyer. The ticket is blocked so it
    // can never be sold again on Ticshare.
    public async Task<bool> RefundAsync(int orderId, string reason, string recordedBy)
    {
        var order = await db.Orders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status is not (OrderStatus.Paid or OrderStatus.TicketSent or OrderStatus.Completed))
            return false;
 
        order.Status = OrderStatus.Refunded;
        order.AdminNote = reason;
        order.Listing.Status = ListingStatus.Blocked;
        db.Payments.Add(new Payment
        {
            OrderId = order.Id, Type = PaymentType.Refund, Amount = order.TotalAmount,
            RecordedBy = recordedBy,
        });
        await db.SaveChangesAsync();
        return true;
    }
 
    // Admin cancels an order that was never paid. The ticket goes back on sale.
    public async Task<bool> CancelUnpaidAsync(int orderId)
    {
        var order = await db.Orders.Include(o => o.Listing).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null || order.Status != OrderStatus.AwaitingPayment) return false;
 
        order.Status = OrderStatus.Cancelled;
        if (order.Listing.Status == ListingStatus.Reserved)
            order.Listing.Status = ListingStatus.Available;
        await db.SaveChangesAsync();
        return true;
    }
 
    // Reservations that were never paid in time go back on sale.
    public async Task ReleaseExpiredReservationsAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(-MinutesToPay);
        var expired = await db.Orders.Include(o => o.Listing)
            .Where(o => o.Status == OrderStatus.AwaitingPayment && o.CreatedAt < deadline)
            .ToListAsync();
 
        foreach (var order in expired)
        {
            order.Status = OrderStatus.Cancelled;
            if (order.Listing.Status == ListingStatus.Reserved)
                order.Listing.Status = ListingStatus.Available;
        }
        await db.SaveChangesAsync();
    }
 
    // Short code the buyer writes in the Swish message, so admin can match payment to order.
    private async Task<string> NewReferenceAsync()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I mix-ups
        while (true)
        {
            var code = "TS-" + new string(Enumerable.Range(0, 5)
                .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
            if (!await db.Orders.AnyAsync(o => o.Reference == code)) return code;
        }
    }
}
 