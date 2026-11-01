using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UFWebsite.Models;

namespace UFWebsite.Data;

// IdentityDbContext adds the user, role and login tables for us.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<SeasonCard> SeasonCards => Set<SeasonCard>();
    public DbSet<ClubCardRecord> ClubCardRecords => Set<ClubCardRecord>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LoginEvent> LoginEvents => Set<LoginEvent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Save statuses as readable text ("Available") instead of numbers.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
        // All money: up to 99 999 999.99 kr.
        builder.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // required by Identity

        // The same season card can only be registered once per season
        // (a rejected registration doesn't count, so it can be sent in again).
        modelBuilder.Entity<SeasonCard>()
            .HasIndex(c => new { c.ClubId, c.Season, c.CardNumber }).IsUnique()
            .HasFilter("[Status] <> 'Rejected'");

        modelBuilder.Entity<ClubCardRecord>()
            .HasIndex(r => new { r.ClubId, r.Season, r.CardNumber }).IsUnique();

        // One seat can only have one active listing per match.
        // Rejected and cancelled listings don't count, so the seller can try again.
        modelBuilder.Entity<Listing>()
            .HasIndex(l => new { l.SeasonCardId, l.MatchId }).IsUnique()
            .HasFilter("[Status] <> 'Rejected' AND [Status] <> 'Cancelled'");

        modelBuilder.Entity<Order>().HasIndex(o => o.Reference).IsUnique();

        // SQL Server refuses several delete chains between the same tables,
        // and we never want to lose orders or payments, so nothing cascades.
        var ourTables = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == typeof(Club).Namespace);
        foreach (var fk in ourTables.SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}