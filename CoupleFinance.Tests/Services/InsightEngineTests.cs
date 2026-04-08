using CoupleFinance.Application.Services;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using FluentAssertions;

namespace CoupleFinance.Tests.Services;

public class InsightEngineTests
{
    [Fact]
    public void GenerateInsights_ShouldFlagSpendingIncreaseAndHighCardUsage()
    {
        var householdId = Guid.NewGuid();
        var category = new Category { Id = Guid.NewGuid(), HouseholdId = householdId, Name = "Lazer" };
        var card = new CreditCard { Id = Guid.NewGuid(), HouseholdId = householdId, Name = "Nubank", LimitAmount = 2000m };
        var referenceDate = new DateTime(2026, 4, 20);

        var transactions = new List<Transaction>
        {
            new() { HouseholdId = householdId, Kind = TransactionKind.Expense, CategoryId = category.Id, Amount = 900m, OccurredOn = new DateTime(2026, 4, 15) },
            new() { HouseholdId = householdId, Kind = TransactionKind.Expense, CategoryId = category.Id, Amount = 300m, OccurredOn = new DateTime(2026, 3, 10) }
        };

        var invoices = new List<Invoice>
        {
            new() { HouseholdId = householdId, CreditCardId = card.Id, ReferenceMonth = 4, ReferenceYear = 2026, TotalAmount = 1700m }
        };

        var engine = new InsightEngine();
        var insights = engine.GenerateInsights(transactions, [category], [card], invoices, [], [], householdId, referenceDate);

        insights.Should().Contain(x => x.Title.Contains("Lazer"));
        insights.Should().Contain(x => x.Title.Contains("Nubank"));
    }
}
