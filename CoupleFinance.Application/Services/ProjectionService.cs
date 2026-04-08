using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Application.Models.Planning;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Application.Services;

public sealed class ProjectionService : IProjectionService
{
    public ProjectionSummary BuildSummary(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<InvestmentAsset> investments,
        PeriodFilter filter)
    {
        var monthlyIncome = transactions
            .Where(x => x.Kind == TransactionKind.Income && x.OccurredOn >= filter.StartDate && x.OccurredOn <= filter.EndDate)
            .Sum(x => x.Amount);

        var monthlyExpenses = transactions
            .Where(x => x.Kind is TransactionKind.Expense or TransactionKind.CardInvoicePayment && x.OccurredOn >= filter.StartDate && x.OccurredOn <= filter.EndDate)
            .Sum(x => x.Amount);

        var availableToSave = monthlyIncome - monthlyExpenses;
        var savingsRate = monthlyIncome <= 0 ? 0 : Math.Round(availableToSave / monthlyIncome * 100m, 2);
        var totalInvested = investments.Sum(x => x.InvestedAmount);
        var totalCurrent = investments.Sum(x => x.CurrentValue);

        var goalProjections = goals
            .Select(goal =>
            {
                var remaining = Math.Max(goal.TargetAmount - goal.CurrentAmount, 0);
                var monthlyContribution = goal.MonthlyContributionTarget > 0 ? goal.MonthlyContributionTarget : Math.Max(availableToSave * 0.35m, 0);
                var monthsToTarget = monthlyContribution <= 0 ? 0 : (int)Math.Ceiling(remaining / monthlyContribution);
                var projectedDate = monthsToTarget <= 0 ? filter.AnchorDate : filter.AnchorDate.AddMonths(monthsToTarget);
                var message = remaining <= 0
                    ? "Meta concluída."
                    : monthlyContribution <= 0
                        ? "Defina um valor mensal para projetar a meta."
                        : $"Guardando R$ {monthlyContribution:N2}/mês, a meta chega em {monthsToTarget} meses.";

                return new GoalProjection(
                    goal.Id,
                    goal.Name,
                    goal.CurrentAmount,
                    goal.TargetAmount,
                    goal.ProgressPercentage,
                    monthlyContribution,
                    monthsToTarget,
                    projectedDate,
                    message);
            })
            .OrderByDescending(x => x.ProgressPercentage)
            .ToList();

        var recommendation = availableToSave switch
        {
            > 0 when savingsRate >= 20 => "Vocês estão com uma taxa de poupança saudável. Vale acelerar metas ou reforçar investimentos.",
            > 0 => "Existe espaço para poupar mais. Reduzir gastos variáveis pode aumentar o ritmo das metas.",
            _ => "As despesas do período superaram a receita. O foco agora é conter gastos recorrentes e reorganizar o fluxo."
        };

        return new ProjectionSummary(
            monthlyIncome,
            monthlyExpenses,
            savingsRate,
            availableToSave,
            totalInvested,
            totalCurrent,
            goalProjections,
            recommendation);
    }
}
