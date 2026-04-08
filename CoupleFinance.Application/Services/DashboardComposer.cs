using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Application.Services;

public sealed class DashboardComposer : IDashboardComposer
{
    public DashboardSnapshot Build(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories,
        IReadOnlyList<CreditCard> creditCards,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<InvestmentAsset> investments,
        PeriodFilter filter,
        Guid currentUserId)
    {
        var filtered = ApplyOwnershipFilter(transactions, filter.Ownership, currentUserId)
            .Where(x => x.OccurredOn >= filter.StartDate && x.OccurredOn <= filter.EndDate)
            .ToList();

        var totalIncome = filtered
            .Where(x => x.Kind == TransactionKind.Income)
            .Sum(x => x.Amount);

        var totalExpenses = filtered
            .Where(x => x.Kind is TransactionKind.Expense or TransactionKind.CardInvoicePayment)
            .Sum(x => x.Amount);

        var currentBalance = totalIncome - totalExpenses + investments.Sum(x => x.CurrentValue);
        var cardUsage = invoices.Where(x => x.ReferenceMonth == filter.AnchorDate.Month && x.ReferenceYear == filter.AnchorDate.Year).Sum(x => x.TotalAmount);

        var trendPoints = BuildTrendPoints(filtered, filter);
        var categoryBreakdown = filtered
            .Where(x => x.Kind == TransactionKind.Expense && x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(group =>
            {
                var category = categories.FirstOrDefault(x => x.Id == group.Key);
                return new CategoryBreakdown(
                    category?.Name ?? "Sem categoria",
                    group.Sum(x => x.Amount),
                    category?.ColorHex ?? "#FFB45E");
            })
            .OrderByDescending(x => x.Amount)
            .Take(6)
            .ToList();

        var comparisons = trendPoints
            .TakeLast(6)
            .Select(x => new ComparisonBar(x.Label, x.Income, x.Expense))
            .ToList();

        return new DashboardSnapshot(totalIncome, totalExpenses, currentBalance, cardUsage, trendPoints, categoryBreakdown, comparisons);
    }

    private static List<Transaction> ApplyOwnershipFilter(IEnumerable<Transaction> transactions, OwnershipFilter ownership, Guid currentUserId)
    {
        return ownership switch
        {
            OwnershipFilter.Mine => transactions.Where(x => x.OwnerUserId == currentUserId && x.Scope == EntryScope.Individual).ToList(),
            OwnershipFilter.Partner => transactions.Where(x => x.OwnerUserId != currentUserId && x.Scope == EntryScope.Individual).ToList(),
            OwnershipFilter.Joint => transactions.Where(x => x.Scope == EntryScope.Joint).ToList(),
            _ => transactions.ToList()
        };
    }

    private static IReadOnlyList<TrendPoint> BuildTrendPoints(IReadOnlyList<Transaction> transactions, PeriodFilter filter)
    {
        return filter.Preset switch
        {
            PeriodPreset.Day => transactions
                .GroupBy(x => x.OccurredOn.Hour)
                .OrderBy(x => x.Key)
                .Select(group => BuildPoint($"{group.Key:00}h", group))
                .ToList(),
            PeriodPreset.Year => transactions
                .GroupBy(x => x.OccurredOn.Month)
                .OrderBy(x => x.Key)
                .Select(group => BuildPoint($"{group.Key:00}", group))
                .ToList(),
            _ => transactions
                .GroupBy(x => x.OccurredOn.Day)
                .OrderBy(x => x.Key)
                .Select(group => BuildPoint($"{group.Key:00}", group))
                .ToList()
        };
    }

    private static TrendPoint BuildPoint(string label, IGrouping<int, Transaction> group)
    {
        var income = group.Where(x => x.Kind == TransactionKind.Income).Sum(x => x.Amount);
        var expense = group.Where(x => x.Kind is TransactionKind.Expense or TransactionKind.CardInvoicePayment).Sum(x => x.Amount);
        return new TrendPoint(label, income, expense, income - expense);
    }
}
