using CoupleFinance.Domain.Common;
using CoupleFinance.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoupleFinance.Domain.Entities;

public sealed class InvestmentAsset : SyncEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Ticker { get; set; }
    public string Broker { get; set; } = string.Empty;
    public InvestmentAssetType AssetType { get; set; } = InvestmentAssetType.FixedIncome;
    public decimal InvestedAmount { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal CurrentQuantity { get; set; }
    public Guid OwnerUserId { get; set; }
    public EntryScope Scope { get; set; } = EntryScope.Individual;
    public DateTime UpdatedOn { get; set; } = DateTime.Today;

    [NotMapped]
    public decimal Profit => CurrentValue - InvestedAmount;

    [NotMapped]
    public decimal ProfitPercentage => InvestedAmount <= 0 ? 0 : Math.Round((CurrentValue - InvestedAmount) / InvestedAmount * 100m, 2);
}
