using CoupleFinance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoupleFinance.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CardPurchase> CardPurchases => Set<CardPurchase>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<ProjectionSnapshot> ProjectionSnapshots => Set<ProjectionSnapshot>();
    public DbSet<InvestmentAsset> InvestmentAssets => Set<InvestmentAsset>();
    public DbSet<Insight> Insights => Set<Insight>();
    public DbSet<SyncQueueItem> SyncQueueItems => Set<SyncQueueItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Household>()
            .HasIndex(x => x.InviteCode)
            .IsUnique();

        modelBuilder.Entity<HouseholdMember>()
            .HasIndex(x => new { x.HouseholdId, x.UserProfileId })
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => new { x.CreditCardId, x.ReferenceMonth, x.ReferenceYear })
            .IsUnique();

        modelBuilder.Entity<SyncQueueItem>()
            .HasIndex(x => new { x.EntityName, x.EntityId });

        modelBuilder.Entity<Invoice>()
            .Ignore(x => x.ReferenceLabel);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
