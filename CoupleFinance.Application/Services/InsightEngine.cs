using CoupleFinance.Application.Contracts;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Application.Services;

public sealed class InsightEngine : IInsightEngine
{
    public IReadOnlyList<Insight> GenerateInsights(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories,
        IReadOnlyList<CreditCard> creditCards,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Goal> goals,
        IReadOnlyList<InvestmentAsset> investments,
        Guid householdId,
        DateTime referenceDate)
    {
        var insights = new List<Insight>();
        var thisMonthStart = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart.AddTicks(-1);

        var thisMonthExpenses = transactions
            .Where(x => x.Kind == TransactionKind.Expense && x.OccurredOn >= thisMonthStart && x.OccurredOn <= referenceDate)
            .ToList();

        var lastMonthExpenses = transactions
            .Where(x => x.Kind == TransactionKind.Expense && x.OccurredOn >= lastMonthStart && x.OccurredOn <= lastMonthEnd)
            .ToList();

        foreach (var categoryGroup in thisMonthExpenses.GroupBy(x => x.CategoryId))
        {
            if (!categoryGroup.Key.HasValue)
            {
                continue;
            }

            var currentValue = categoryGroup.Sum(x => x.Amount);
            var previousValue = lastMonthExpenses.Where(x => x.CategoryId == categoryGroup.Key).Sum(x => x.Amount);
            if (previousValue <= 0 || currentValue <= previousValue * 1.2m)
            {
                continue;
            }

            var category = categories.FirstOrDefault(x => x.Id == categoryGroup.Key.Value);
            var deltaPercentage = previousValue == 0 ? 100 : Math.Round((currentValue - previousValue) / previousValue * 100m, 2);
            insights.Add(new Insight
            {
                HouseholdId = householdId,
                Title = $"Gasto maior em {category?.Name ?? "categoria"}",
                Message = $"Seu gasto com {category?.Name?.ToLowerInvariant() ?? "essa categoria"} aumentou {deltaPercentage}% em relação ao mês anterior.",
                Severity = deltaPercentage > 35 ? InsightSeverity.Warning : InsightSeverity.Neutral,
                MetricDeltaPercentage = deltaPercentage
            });
        }

        foreach (var invoice in invoices.Where(x => x.ReferenceMonth == referenceDate.Month && x.ReferenceYear == referenceDate.Year))
        {
            var card = creditCards.FirstOrDefault(x => x.Id == invoice.CreditCardId);
            if (card is null || card.LimitAmount <= 0)
            {
                continue;
            }

            var usage = Math.Round(invoice.TotalAmount / card.LimitAmount * 100m, 2);
            if (usage < 70)
            {
                continue;
            }

            insights.Add(new Insight
            {
                HouseholdId = householdId,
                Title = $"Uso alto do cartão {card.Name}",
                Message = $"A fatura atual do cartão {card.Name} já consumiu {usage}% do limite disponível.",
                Severity = usage >= 90 ? InsightSeverity.Critical : InsightSeverity.Warning,
                MetricDeltaPercentage = usage
            });
        }

        foreach (var goal in goals)
        {
            if (goal.MonthlyContributionTarget <= 0)
            {
                continue;
            }

            var remaining = Math.Max(goal.TargetAmount - goal.CurrentAmount, 0);
            var months = (int)Math.Ceiling(remaining / goal.MonthlyContributionTarget);
            insights.Add(new Insight
            {
                HouseholdId = householdId,
                Title = $"Meta {goal.Name} em rota",
                Message = months <= 0
                    ? $"Vocês já bateram a meta {goal.Name}."
                    : $"Se mantiverem R$ {goal.MonthlyContributionTarget:N2} por mês, atingem {goal.Name} em cerca de {months} meses.",
                Severity = months <= 6 ? InsightSeverity.Positive : InsightSeverity.Neutral,
                MetricDeltaPercentage = goal.ProgressPercentage
            });
        }

        var totalInvested = investments.Sum(x => x.InvestedAmount);
        var totalCurrent = investments.Sum(x => x.CurrentValue);
        if (totalInvested > 0)
        {
            var returnPercentage = Math.Round((totalCurrent - totalInvested) / totalInvested * 100m, 2);
            insights.Add(new Insight
            {
                HouseholdId = householdId,
                Title = "Panorama da carteira",
                Message = returnPercentage >= 0
                    ? $"Sua carteira manual está rendendo {returnPercentage}% acima do valor investido."
                    : $"Sua carteira está {Math.Abs(returnPercentage):N2}% abaixo do valor investido. Vale revisar a alocação.",
                Severity = returnPercentage >= 0 ? InsightSeverity.Positive : InsightSeverity.Warning,
                MetricDeltaPercentage = returnPercentage
            });
        }

        return insights
            .OrderByDescending(x => x.Severity)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .ToList();
    }
}
