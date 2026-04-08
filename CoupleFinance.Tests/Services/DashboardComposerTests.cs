using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Application.Services;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using FluentAssertions;

namespace CoupleFinance.Tests.Services;

public class DashboardComposerTests
{
    [Fact]
    public void Build_ShouldAggregateIncomeExpenseAndCategories_ForCurrentPeriod()
    {
        var userId = Guid.NewGuid();
        var householdId = Guid.NewGuid();
        var salaryCategory = new Category { Id = Guid.NewGuid(), HouseholdId = householdId, Name = "Salário", Type = CategoryType.Income, ColorHex = "#00FF00" };
        var foodCategory = new Category { Id = Guid.NewGuid(), HouseholdId = householdId, Name = "Alimentação", Type = CategoryType.Expense, ColorHex = "#FF9900" };

        var transactions = new List<Transaction>
        {
            new() { HouseholdId = householdId, OwnerUserId = userId, Kind = TransactionKind.Income, Amount = 5000m, OccurredOn = new DateTime(2026, 4, 5), CategoryId = salaryCategory.Id, Scope = EntryScope.Individual },
            new() { HouseholdId = householdId, OwnerUserId = userId, Kind = TransactionKind.Expense, Amount = 1200m, OccurredOn = new DateTime(2026, 4, 8), CategoryId = foodCategory.Id, Scope = EntryScope.Joint },
            new() { HouseholdId = householdId, OwnerUserId = userId, Kind = TransactionKind.Expense, Amount = 200m, OccurredOn = new DateTime(2026, 3, 8), CategoryId = foodCategory.Id, Scope = EntryScope.Joint }
        };

        var composer = new DashboardComposer();
        var snapshot = composer.Build(
            transactions,
            [salaryCategory, foodCategory],
            [],
            [],
            [],
            new PeriodFilter(PeriodPreset.Month, new DateTime(2026, 4, 10), OwnershipFilter.All),
            userId);

        snapshot.TotalIncome.Should().Be(5000m);
        snapshot.TotalExpenses.Should().Be(1200m);
        snapshot.CategoryBreakdown.Should().ContainSingle(x => x.Name == "Alimentação" && x.Amount == 1200m);
    }
}
