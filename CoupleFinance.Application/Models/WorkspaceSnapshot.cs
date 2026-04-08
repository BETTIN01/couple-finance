using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Application.Models.Planning;
using CoupleFinance.Application.Models.Sync;
using CoupleFinance.Domain.Entities;

namespace CoupleFinance.Application.Models;

public sealed record WorkspaceSnapshot(
    UserProfile CurrentUser,
    Household Household,
    UserProfile? Partner,
    DashboardSnapshot Dashboard,
    ProjectionSummary Projection,
    SyncHealth Sync,
    string InviteCode,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<BankAccount> Accounts,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<Transfer> Transfers,
    IReadOnlyList<CreditCard> CreditCards,
    IReadOnlyList<CardPurchase> Purchases,
    IReadOnlyList<Invoice> Invoices,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<InvestmentAsset> Investments,
    IReadOnlyList<Insight> Insights);
