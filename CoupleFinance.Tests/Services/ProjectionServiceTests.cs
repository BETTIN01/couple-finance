using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Application.Services;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using FluentAssertions;

namespace CoupleFinance.Tests.Services;

public class ProjectionServiceTests
{
    [Fact]
    public void BuildSummary_ShouldProjectGoalsUsingAvailableSavings()
    {
        var householdId = Guid.NewGuid();
        var transactions = new List<Transaction>
        {
            new() { HouseholdId = householdId, Kind = TransactionKind.Income, Amount = 8000m, OccurredOn = new DateTime(2026, 4, 3) },
            new() { HouseholdId = householdId, Kind = TransactionKind.Expense, Amount = 5000m, OccurredOn = new DateTime(2026, 4, 7) }
        };

        var goals = new List<Goal>
        {
            new() { HouseholdId = householdId, Name = "Viagem", TargetAmount = 12000m, CurrentAmount = 2000m, MonthlyContributionTarget = 1500m }
        };

        var service = new ProjectionService();
        var summary = service.BuildSummary(transactions, goals, [], new PeriodFilter(PeriodPreset.Month, new DateTime(2026, 4, 18), OwnershipFilter.All));

        summary.AvailableToSave.Should().Be(3000m);
        summary.Goals.Should().ContainSingle();
        summary.Goals[0].MonthsToTarget.Should().Be(7);
        summary.Recommendation.Should().NotBeNullOrWhiteSpace();
    }
}
