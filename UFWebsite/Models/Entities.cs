using Microsoft.AspNetCore.Identity;
 
namespace UFWebsite.Models;
 
// ---------- Statuses ----------
 
public enum SeasonCardStatus { PendingReview, Approved, Rejected }
 
// PendingReview = waiting for admin, Available = approved and for sale,
// Reserved = a buyer is paying, Sold = paid,
// Rejected = refused by admin, Cancelled = seller withdrew it,
// Blocked = stopped after a refund or fraud
public enum ListingStatus { PendingReview, Available, Reserved, Sold, Rejected, Cancelled, Blocked }
 
// AwaitingPayment -> Paid -> TicketSent -> Completed -> PaidOut
// Side tracks: Cancelled (never paid), Refunded (money sent back)
public enum OrderStatus { AwaitingPayment, Paid, TicketSent, Completed, PaidOut, Cancelled, Refunded }
 
public enum PaymentType { Incoming, Payout, Refund }
 
public enum VerificationMethod { Manual, ClubList, Api }
 
// ---------- Tables ----------
 
// Identity creates the AspNetUsers table. We only add our own columns.
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public string? SwishNumber { get; set; }   // where the seller gets paid
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
 
public class Club
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sport { get; set; } = "";              // Fotboll, Ishockey, Basket, Innebandy
    public string City { get; set; } = "";
    public bool AllowsResale { get; set; }              // does the club allow resale?
    public string? ResaleRules { get; set; }            // e.g. "Max face value"
    public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Manual;
}
 
public class Match
{
    public int Id { get; set; }
    public int HomeClubId { get; set; }
    public Club HomeClub { get; set; } = null!;
    public int AwayClubId { get; set; }
    public Club AwayClub { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public string Arena { get; set; } = "";
    public string? Competition { get; set; }
    public bool IsPublished { get; set; }               // only shown on the site once admin publishes it
    public List<Listing> Listings { get; set; } = new();
}
 
public class SeasonCard
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public string Season { get; set; } = "";            // e.g. "2026"
    public string CardNumber { get; set; } = "";        // the season card ID from the club
    public string Section { get; set; } = "";
    public string Row { get; set; } = "";
    public string Seat { get; set; } = "";
    public bool SellerAcceptedTerms { get; set; }       // seller confirms they are allowed to resell
    public SeasonCardStatus Status { get; set; } = SeasonCardStatus.PendingReview;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }
}
 
// If a club sends a list of valid season cards, it is imported here.
public class ClubCardRecord
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public string Season { get; set; } = "";
    public string CardNumber { get; set; } = "";
}
 
// A "ticket" on the site = one seat from a season card for one match.
public class Listing
{
    public int Id { get; set; }
    public int SeasonCardId { get; set; }
    public SeasonCard SeasonCard { get; set; } = null!;
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public string SellerId { get; set; } = "";
    public ApplicationUser Seller { get; set; } = null!;
    public decimal Price { get; set; }                  // what the seller receives
    public decimal FaceValue { get; set; }              // the club's normal price for the match
    public ListingStatus Status { get; set; } = ListingStatus.PendingReview;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
 
public class Order
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";         // e.g. TS-7K3Q9, written in the Swish message
    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public string BuyerId { get; set; } = "";
    public ApplicationUser Buyer { get; set; } = null!;
    public decimal TicketPrice { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal TotalAmount { get; set; }            // what the buyer swishes
    public OrderStatus Status { get; set; } = OrderStatus.AwaitingPayment;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? TicketSentAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PaidOutAt { get; set; }
    public string? AdminNote { get; set; }
    public List<Payment> Payments { get; set; } = new();
}
 
// Every money movement is logged: in from buyer, out to seller, back on refund.
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public PaymentType Type { get; set; }
    public decimal Amount { get; set; }
    public string? SwishReference { get; set; }         // reference from the Swish app / bank statement
    public bool IsSimulated { get; set; }
    public string RecordedBy { get; set; } = "";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
 
public class LoginEvent
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public bool Succeeded { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}