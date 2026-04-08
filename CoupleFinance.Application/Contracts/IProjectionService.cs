using CoupleFinance.Application.Models.Planning;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Domain.Entities;

namespace CoupleFinance.Application.Contracts;

public interface IProjectionService
{
    ProjectionSummary BuildSummary(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<InvestmentAsset> investments,
        PeriodFilter filter);
}
