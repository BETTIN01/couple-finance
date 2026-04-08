using CoupleFinance.Domain.Entities;

namespace CoupleFinance.Application.Contracts;

public interface IInsightEngine
{
    IReadOnlyList<Insight> GenerateInsights(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories,
        IReadOnlyList<CreditCard> creditCards,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<InvestmentAsset> investments,
        Guid householdId,
        DateTime referenceDate);
}
