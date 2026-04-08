using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Domain.Entities;

namespace CoupleFinance.Application.Contracts;

public interface IDashboardComposer
{
    DashboardSnapshot Build(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Category> categories,
        IReadOnlyList<CreditCard> creditCards,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<InvestmentAsset> investments,
        PeriodFilter filter,
        Guid currentUserId);
}
