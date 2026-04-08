using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CoupleFinance.Application.Models;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Desktop.Presentation;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using MaterialDesignThemes.Wpf;

namespace CoupleFinance.Desktop.ViewModels;

public partial class ShellViewModel
{
    [ObservableProperty] private string transactionSearchText = string.Empty;
    [ObservableProperty] private string selectedTransactionTypeFilter = "Todas";

    public IReadOnlyList<string> TransactionTypeFilters { get; } =
    [
        "Todas",
        "Receitas",
        "Despesas",
        "Transferências",
        "Pagamentos",
        "Ajustes"
    ];

    public string SectionSubtitle => CurrentSection switch
    {
        NavigationSection.Dashboard => "Resumo do período com fluxo do casal, visão individual e prioridades em destaque.",
        NavigationSection.Transactions => "Registre entradas, despesas e transferências com clareza e encontre tudo sem esforço.",
        NavigationSection.Accounts => "Acompanhe saldos, movimentação e estrutura das contas que sustentam a rotina.",
        NavigationSection.Cards => "Gerencie limites, faturas e parcelamentos sem perder o impacto no orçamento.",
        NavigationSection.Planning => "Transforme metas em plano mensal com projeções, ritmo de aporte e conclusão estimada.",
        NavigationSection.Investments => "Consolide patrimônio, retorno e alocação de cada pessoa e do patrimônio conjunto.",
        NavigationSection.Insights => "Receba diagnósticos automáticos sobre comportamento de gasto, cartões e metas.",
        NavigationSection.Settings => "Ajuste sua experiência, acompanhe o status da conta e confira a saúde do aplicativo.",
        _ => "Visão completa para decidir melhor em casal."
    };

    public string HeaderFilterSummary =>
        $"{DisplayText.FromEnum(SelectedPeriod)} · {DisplayText.FromEnum(SelectedOwnership)}";

    public string HouseholdHeadline =>
        CurrentSnapshot switch
        {
            null => "Couple Finance",
            { Partner: not null } snapshot => $"{snapshot.CurrentUser.DisplayName} + {snapshot.Partner.DisplayName}",
            { Household.Name: var householdName } => householdName
        };

    public string HouseholdSubtitle =>
        CurrentSnapshot switch
        {
            null => string.Empty,
            { Partner: not null } => "Finanças individuais e conjuntas organizadas em um só lugar.",
            _ => "Convide seu parceiro para liberar a visão compartilhada e a divisão conjunta."
        };

    public bool HasTrendData =>
        CurrentSnapshot?.Dashboard.TrendPoints.Any(point => point.Income != 0 || point.Expense != 0 || point.Balance != 0) == true;

    public bool HasCategoryData =>
        CurrentSnapshot?.Dashboard.CategoryBreakdown.Any(slice => slice.Amount > 0) == true;

    public bool HasComparisonData =>
        CurrentSnapshot?.Dashboard.Comparisons.Any(bar => bar.Income > 0 || bar.Expense > 0) == true;

    public bool HasTransactions => FilteredTransactions.Count > 0;
    public bool HasAccounts => AccountOverviewItems.Count > 0;
    public bool HasCards => CardOverviewItems.Count > 0;
    public bool HasGoals => GoalOverviewItems.Count > 0;
    public bool HasInvestments => InvestmentOverviewItems.Count > 0;
    public bool HasInsights => InsightOverviewItems.Count > 0;

    public decimal TotalAccountBalance => CurrentSnapshot?.Accounts.Sum(account => account.CurrentBalance) ?? 0m;

    public decimal TotalOpenInvoices =>
        CurrentSnapshot?.Invoices
            .Where(invoice => invoice.Status != InvoiceStatus.Paid)
            .Sum(invoice => Math.Max(invoice.TotalAmount - invoice.PaidAmount, 0m)) ?? 0m;

    public decimal TotalCardLimit => CurrentSnapshot?.CreditCards.Sum(card => card.LimitAmount) ?? 0m;

    public double CardUsagePercent =>
        TotalCardLimit <= 0 ? 0 : Math.Clamp((double)(TotalOpenInvoices / TotalCardLimit * 100m), 0, 100);

    public decimal TotalInvestmentProfit =>
        CurrentSnapshot?.Investments.Sum(investment => investment.Profit) ?? 0m;

    public decimal TotalInvestedAmount =>
        CurrentSnapshot?.Investments.Sum(investment => investment.InvestedAmount) ?? 0m;

    public decimal InvestmentReturnPercentage =>
        TotalInvestedAmount <= 0 ? 0 : Math.Round(TotalInvestmentProfit / TotalInvestedAmount * 100m, 2);

    public IReadOnlyList<ContributionCardItem> ContributionCards
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var incomes = GetTransactionsForSelectedPeriod()
                .Where(transaction => transaction.Kind == TransactionKind.Income)
                .ToList();

            var mine = incomes
                .Where(transaction => transaction.Scope == EntryScope.Individual && transaction.OwnerUserId == CurrentSnapshot.CurrentUser.Id)
                .Sum(transaction => transaction.Amount);

            var partner = incomes
                .Where(transaction =>
                    transaction.Scope == EntryScope.Individual &&
                    CurrentSnapshot.Partner is not null &&
                    transaction.OwnerUserId == CurrentSnapshot.Partner.Id)
                .Sum(transaction => transaction.Amount);

            var joint = incomes
                .Where(transaction => transaction.Scope == EntryScope.Joint)
                .Sum(transaction => transaction.Amount);

            return
            [
                new ContributionCardItem(
                    "Sua renda",
                    "Entradas individuais do período",
                    mine,
                    mine > 0 ? "Base da sua contribuição pessoal." : "Ainda não há receitas individuais suas neste período.",
                    CreateBrush("#FF488399"),
                    PackIconKind.AccountCircleOutline),
                new ContributionCardItem(
                    CurrentSnapshot.Partner is null ? "Convide seu par" : "Renda do parceiro",
                    CurrentSnapshot.Partner is null ? "Dividam metas e despesas no mesmo painel" : "Entradas individuais da outra pessoa",
                    partner,
                    CurrentSnapshot.Partner is null ? $"Use o código {CurrentSnapshot.InviteCode} para conectar." : partner > 0 ? "Mostra quanto a outra pessoa trouxe para o período." : "Ainda não há receitas individuais do parceiro neste período.",
                    CreateBrush("#FFF1BEBE"),
                    PackIconKind.HeartOutline),
                new ContributionCardItem(
                    "Recursos conjuntos",
                    "Receitas registradas como compartilhadas",
                    joint,
                    joint > 0 ? "Ajuda a medir o caixa realmente do casal." : "Use o escopo conjunto para separar o que pertence aos dois.",
                    CreateBrush("#FFF4D38A"),
                    PackIconKind.HandshakeOutline)
            ];
        }
    }

    public IReadOnlyList<TransactionListItem> RecentTransactions => FilteredTransactions.Take(6).ToArray();

    public IReadOnlyList<TransactionListItem> FilteredTransactions
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var categoryLookup = CurrentSnapshot.Categories.ToDictionary(category => category.Id, category => category.Name);
            var accountLookup = CurrentSnapshot.Accounts.ToDictionary(account => account.Id, account => account.Name);

            var query = GetTransactionsForSelectedPeriod()
                .Where(MatchesTransactionSearch)
                .Where(MatchesTransactionTypeFilter)
                .Select(transaction => CreateTransactionListItem(transaction, categoryLookup, accountLookup));

            return query.ToArray();
        }
    }

    public IReadOnlyList<AccountOverviewItem> AccountOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var transactionsByAccount = GetTransactionsForSelectedPeriod()
                .Where(transaction => transaction.BankAccountId.HasValue)
                .GroupBy(transaction => transaction.BankAccountId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            return CurrentSnapshot.Accounts
                .Select(account =>
                {
                    transactionsByAccount.TryGetValue(account.Id, out var movements);
                    movements ??= [];
                    var inflows = movements.Where(transaction => transaction.GetSignedAmount() > 0).Sum(transaction => transaction.GetSignedAmount());
                    var outflows = movements.Where(transaction => transaction.GetSignedAmount() < 0).Sum(transaction => Math.Abs(transaction.GetSignedAmount()));

                    return new AccountOverviewItem(
                        account.Id,
                        account.Name,
                        account.Institution,
                        DisplayText.FromEnum(account.Type),
                        account.CurrentBalance,
                        inflows,
                        outflows,
                        movements.Count,
                        CreateBrush(account.ColorHex, "#FF488399"));
                })
                .OrderByDescending(item => item.Balance)
                .ToArray();
        }
    }

    public IReadOnlyList<CardOverviewItem> CardOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var purchaseCounts = GetPurchasesForSelectedPeriod()
                .GroupBy(purchase => purchase.CreditCardId)
                .ToDictionary(group => group.Key, group => group.Count());

            return CurrentSnapshot.CreditCards
                .Select(card =>
                {
                    var openAmount = CurrentSnapshot.Invoices
                        .Where(invoice => invoice.CreditCardId == card.Id && invoice.Status != InvoiceStatus.Paid)
                        .Sum(invoice => Math.Max(invoice.TotalAmount - invoice.PaidAmount, 0m));
                    var dueDate = CurrentSnapshot.Invoices
                        .Where(invoice => invoice.CreditCardId == card.Id && invoice.Status != InvoiceStatus.Paid)
                        .OrderBy(invoice => invoice.DueDate)
                        .Select(invoice => (DateTime?)invoice.DueDate)
                        .FirstOrDefault();
                    purchaseCounts.TryGetValue(card.Id, out var purchaseCount);

                    return new CardOverviewItem(
                        card.Id,
                        card.Name,
                        card.Brand,
                        card.LimitAmount,
                        openAmount,
                        card.LimitAmount - openAmount,
                        card.LimitAmount <= 0 ? 0 : Math.Clamp((double)(openAmount / card.LimitAmount * 100m), 0, 100),
                        dueDate.HasValue ? $"Próximo vencimento em {dueDate:dd/MM}" : "Sem fatura aberta",
                        purchaseCount,
                        CreateBrush(card.ColorHex, "#FFF4D38A"));
                })
                .OrderByDescending(item => item.OpenAmount)
                .ToArray();
        }
    }

    public IReadOnlyList<InvoiceOverviewItem> InvoiceOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var cardLookup = CurrentSnapshot.CreditCards.ToDictionary(card => card.Id, card => card.Name);

            return CurrentSnapshot.Invoices
                .OrderBy(invoice => invoice.Status == InvoiceStatus.Paid)
                .ThenBy(invoice => invoice.DueDate)
                .Take(8)
                .Select(invoice =>
                {
                    var brush = invoice.Status switch
                    {
                        InvoiceStatus.Paid => CreateBrush("#FF488399"),
                        InvoiceStatus.Overdue => CreateBrush("#FFEA9393"),
                        _ => CreateBrush("#FFF4D38A")
                    };

                    return new InvoiceOverviewItem(
                        invoice.Id,
                        cardLookup.GetValueOrDefault(invoice.CreditCardId, "Cartão"),
                        invoice.ReferenceLabel,
                        DisplayText.FromEnum(invoice.Status),
                        invoice.TotalAmount,
                        Math.Max(invoice.TotalAmount - invoice.PaidAmount, 0m),
                        invoice.DueDate,
                        brush);
                })
                .ToArray();
        }
    }

    public IReadOnlyList<GoalOverviewItem> GoalOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var goalsById = CurrentSnapshot.Goals.ToDictionary(goal => goal.Id);

            return CurrentSnapshot.Projection.Goals
                .Select(goal =>
                {
                    goalsById.TryGetValue(goal.GoalId, out var sourceGoal);
                    var accent = goal.ProgressPercentage switch
                    {
                        >= 80m => CreateBrush("#FF488399"),
                        >= 40m => CreateBrush("#FFF4D38A"),
                        _ => CreateBrush("#FFF1BEBE")
                    };

                    return new GoalOverviewItem(
                        goal.GoalId,
                        goal.GoalName,
                        sourceGoal is null ? "Meta financeira" : DisplayText.FromEnum(sourceGoal.GoalType),
                        goal.CurrentAmount,
                        goal.TargetAmount,
                        Math.Max(goal.TargetAmount - goal.CurrentAmount, 0m),
                        goal.SuggestedMonthlyContribution,
                        goal.MonthsToTarget,
                        goal.ProjectedCompletionDate,
                        (double)Math.Clamp(goal.ProgressPercentage, 0m, 100m),
                        goal.Message,
                        accent);
                })
                .OrderByDescending(goal => goal.ProgressPercent)
                .ToArray();
        }
    }

    public IReadOnlyList<InvestmentOverviewItem> InvestmentOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            return GetInvestmentsForCurrentSelection()
                .Select(investment =>
                    new InvestmentOverviewItem(
                        investment.Id,
                        investment.Name,
                        string.IsNullOrWhiteSpace(investment.Ticker) ? "Sem ticker" : investment.Ticker!.ToUpperInvariant(),
                        ResolveOwnerLabel(investment.OwnerUserId, investment.Scope),
                        DisplayText.FromEnum(investment.Scope),
                        investment.InvestedAmount,
                        investment.CurrentValue,
                        investment.Profit,
                        investment.ProfitPercentage,
                        investment.Profit >= 0 ? CreateBrush("#FF488399") : CreateBrush("#FFEA9393")))
                .OrderByDescending(item => item.CurrentValue)
                .ToArray();
        }
    }

    public IReadOnlyList<InvestmentAllocationItem> InvestmentAllocationItems
    {
        get
        {
            var items = InvestmentOverviewItems;
            if (items.Count == 0)
            {
                return [];
            }

            var total = items.Sum(item => item.CurrentValue);
            if (total <= 0)
            {
                return [];
            }

            var sourceLookup = CurrentSnapshot?.Investments.ToDictionary(investment => investment.Id);
            return items
                .GroupBy(item =>
                {
                    if (sourceLookup is not null &&
                        sourceLookup.TryGetValue(item.Id, out var source))
                    {
                        return DisplayText.FromEnum(source.AssetType);
                    }

                    return "Outros ativos";
                })
                .Select(group =>
                {
                    var currentValue = group.Sum(item => item.CurrentValue);
                    return new InvestmentAllocationItem(
                        group.Key,
                        currentValue,
                        Math.Round((double)(currentValue / total * 100m), 1),
                        group.First().AccentBrush);
                })
                .OrderByDescending(item => item.CurrentValue)
                .ToArray();
        }
    }

    public IReadOnlyList<InsightOverviewItem> InsightOverviewItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            return CurrentSnapshot.Insights
                .OrderByDescending(insight => insight.CreatedAtUtc)
                .Take(6)
                .Select(insight =>
                {
                    var (brush, icon) = insight.Severity switch
                    {
                        InsightSeverity.Positive => (CreateBrush("#FF488399"), PackIconKind.TrendingUp),
                        InsightSeverity.Warning => (CreateBrush("#FFF4D38A"), PackIconKind.AlertOutline),
                        InsightSeverity.Critical => (CreateBrush("#FFEA9393"), PackIconKind.AlertCircleOutline),
                        _ => (CreateBrush("#FFF1BEBE"), PackIconKind.LightbulbAutoOutline)
                    };

                    var supportingText = insight.MetricDeltaPercentage == 0
                        ? $"Gerado em {insight.CreatedAtUtc.ToLocalTime():dd/MM 'às' HH:mm}"
                        : $"Variação monitorada: {insight.MetricDeltaPercentage:N1}%";

                    return new InsightOverviewItem(
                        insight.Id,
                        insight.Title,
                        insight.Message,
                        DisplayText.FromEnum(insight.Severity),
                        supportingText,
                        brush,
                        icon);
                })
                .ToArray();
        }
    }

    public IReadOnlyList<CategorySpendItem> CategorySpendItems
    {
        get
        {
            if (CurrentSnapshot is null)
            {
                return [];
            }

            var totalExpenses = CurrentSnapshot.Dashboard.TotalExpenses;
            return CurrentSnapshot.Dashboard.CategoryBreakdown
                .OrderByDescending(slice => slice.Amount)
                .Take(6)
                .Select(slice =>
                    new CategorySpendItem(
                        slice.Name,
                        slice.Amount,
                        totalExpenses <= 0 ? 0 : Math.Round((double)(slice.Amount / totalExpenses * 100m), 1),
                        CreateBrush(slice.ColorHex, "#FF488399")))
                .ToArray();
        }
    }

    partial void OnCurrentSnapshotChanged(WorkspaceSnapshot? value) => NotifyUiStateChanged();

    partial void OnTransactionSearchTextChanged(string value) => NotifyUiStateChanged();

    partial void OnSelectedTransactionTypeFilterChanged(string value) => NotifyUiStateChanged();

    private void NotifyUiStateChanged()
    {
        OnPropertyChanged(nameof(SectionSubtitle));
        OnPropertyChanged(nameof(HeaderFilterSummary));
        OnPropertyChanged(nameof(HouseholdHeadline));
        OnPropertyChanged(nameof(HouseholdSubtitle));
        OnPropertyChanged(nameof(HasTrendData));
        OnPropertyChanged(nameof(HasCategoryData));
        OnPropertyChanged(nameof(HasComparisonData));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(HasGoals));
        OnPropertyChanged(nameof(HasInvestments));
        OnPropertyChanged(nameof(HasInsights));
        OnPropertyChanged(nameof(TotalAccountBalance));
        OnPropertyChanged(nameof(TotalOpenInvoices));
        OnPropertyChanged(nameof(TotalCardLimit));
        OnPropertyChanged(nameof(CardUsagePercent));
        OnPropertyChanged(nameof(TotalInvestmentProfit));
        OnPropertyChanged(nameof(TotalInvestedAmount));
        OnPropertyChanged(nameof(InvestmentReturnPercentage));
        OnPropertyChanged(nameof(ContributionCards));
        OnPropertyChanged(nameof(RecentTransactions));
        OnPropertyChanged(nameof(FilteredTransactions));
        OnPropertyChanged(nameof(AccountOverviewItems));
        OnPropertyChanged(nameof(CardOverviewItems));
        OnPropertyChanged(nameof(InvoiceOverviewItems));
        OnPropertyChanged(nameof(GoalOverviewItems));
        OnPropertyChanged(nameof(InvestmentOverviewItems));
        OnPropertyChanged(nameof(InvestmentAllocationItems));
        OnPropertyChanged(nameof(InsightOverviewItems));
        OnPropertyChanged(nameof(CategorySpendItems));
    }

    private IEnumerable<Transaction> GetTransactionsForSelectedPeriod()
    {
        if (CurrentSnapshot is null)
        {
            return [];
        }

        var (start, end) = GetPeriodBounds();
        return CurrentSnapshot.Transactions.Where(transaction =>
            transaction.OccurredOn.Date >= start &&
            transaction.OccurredOn.Date <= end &&
            MatchesOwnership(transaction.OwnerUserId, transaction.Scope));
    }

    private IEnumerable<CardPurchase> GetPurchasesForSelectedPeriod()
    {
        if (CurrentSnapshot is null)
        {
            return [];
        }

        var (start, end) = GetPeriodBounds();
        return CurrentSnapshot.Purchases.Where(purchase =>
            purchase.PurchaseDate.Date >= start &&
            purchase.PurchaseDate.Date <= end &&
            MatchesOwnership(purchase.OwnerUserId, purchase.Scope));
    }

    private IEnumerable<InvestmentAsset> GetInvestmentsForCurrentSelection()
    {
        if (CurrentSnapshot is null)
        {
            return [];
        }

        return CurrentSnapshot.Investments.Where(investment => MatchesOwnership(investment.OwnerUserId, investment.Scope));
    }

    private bool MatchesTransactionSearch(Transaction transaction)
    {
        if (string.IsNullOrWhiteSpace(TransactionSearchText) || CurrentSnapshot is null)
        {
            return true;
        }

        var categoryLookup = CurrentSnapshot.Categories.ToDictionary(category => category.Id, category => category.Name);
        var accountLookup = CurrentSnapshot.Accounts.ToDictionary(account => account.Id, account => account.Name);
        var search = TransactionSearchText.Trim();
        var ownerLabel = ResolveOwnerLabel(transaction.OwnerUserId, transaction.Scope);
        var account = transaction.BankAccountId.HasValue && accountLookup.TryGetValue(transaction.BankAccountId.Value, out var accountName)
            ? accountName
            : string.Empty;
        var category = transaction.CategoryId.HasValue && categoryLookup.TryGetValue(transaction.CategoryId.Value, out var categoryName)
            ? categoryName
            : string.Empty;

        return ContainsIgnoreCase(transaction.Description, search) ||
               ContainsIgnoreCase(transaction.Notes, search) ||
               ContainsIgnoreCase(account, search) ||
               ContainsIgnoreCase(category, search) ||
               ContainsIgnoreCase(ownerLabel, search) ||
               ContainsIgnoreCase(DisplayText.FromEnum(transaction.Kind), search);
    }

    private bool MatchesTransactionTypeFilter(Transaction transaction) => SelectedTransactionTypeFilter switch
    {
        "Receitas" => transaction.Kind == TransactionKind.Income,
        "Despesas" => transaction.Kind == TransactionKind.Expense,
        "Transferências" => transaction.Kind is TransactionKind.TransferIn or TransactionKind.TransferOut,
        "Pagamentos" => transaction.Kind == TransactionKind.CardInvoicePayment,
        "Ajustes" => transaction.Kind == TransactionKind.Adjustment,
        _ => true
    };

    private TransactionListItem CreateTransactionListItem(
        Transaction transaction,
        IReadOnlyDictionary<Guid, string> categoryLookup,
        IReadOnlyDictionary<Guid, string> accountLookup)
    {
        var amount = transaction.GetSignedAmount();
        var tone = transaction.Kind switch
        {
            TransactionKind.Income => CreateBrush("#FF488399"),
            TransactionKind.Expense => CreateBrush("#FFEA9393"),
            TransactionKind.CardInvoicePayment => CreateBrush("#FFF4D38A"),
            _ => CreateBrush("#FFF1BEBE")
        };

        var icon = transaction.Kind switch
        {
            TransactionKind.Income => PackIconKind.TrendingUp,
            TransactionKind.Expense => PackIconKind.TrendingDown,
            TransactionKind.CardInvoicePayment => PackIconKind.CreditCardOutline,
            TransactionKind.TransferIn or TransactionKind.TransferOut => PackIconKind.SwapHorizontalBold,
            _ => PackIconKind.Tune
        };

        var account = transaction.BankAccountId.HasValue && accountLookup.TryGetValue(transaction.BankAccountId.Value, out var accountName)
            ? accountName
            : "Sem conta";
        var category = transaction.CategoryId.HasValue && categoryLookup.TryGetValue(transaction.CategoryId.Value, out var categoryName)
            ? categoryName
            : "Sem categoria";
        var owner = ResolveOwnerLabel(transaction.OwnerUserId, transaction.Scope);

        return new TransactionListItem(
            transaction.Id,
            transaction.OccurredOn,
            transaction.Description,
            DisplayText.FromEnum(transaction.Kind),
            DisplayText.FromEnum(transaction.Scope),
            owner,
            account,
            category,
            string.IsNullOrWhiteSpace(transaction.Notes)
                ? $"{account} · {category}"
                : transaction.Notes!,
            amount,
            tone,
            icon);
    }

    private string ResolveOwnerLabel(Guid ownerId, EntryScope scope)
    {
        if (CurrentSnapshot is null)
        {
            return string.Empty;
        }

        return DisplayText.OwnershipLabel(
            ownerId,
            CurrentSnapshot.CurrentUser.Id,
            CurrentSnapshot.CurrentUser.DisplayName,
            CurrentSnapshot.Partner?.Id,
            CurrentSnapshot.Partner?.DisplayName,
            scope);
    }

    private bool MatchesOwnership(Guid ownerId, EntryScope scope) => SelectedOwnership switch
    {
        OwnershipFilter.Mine => scope == EntryScope.Individual && CurrentSnapshot is not null && ownerId == CurrentSnapshot.CurrentUser.Id,
        OwnershipFilter.Partner => scope == EntryScope.Individual && CurrentSnapshot?.Partner is not null && ownerId == CurrentSnapshot.Partner.Id,
        OwnershipFilter.Joint => scope == EntryScope.Joint,
        _ => true
    };

    private (DateTime Start, DateTime End) GetPeriodBounds()
    {
        var anchor = AnchorDate.Date;
        return SelectedPeriod switch
        {
            PeriodPreset.Day => (anchor, anchor),
            PeriodPreset.Year => (new DateTime(anchor.Year, 1, 1), new DateTime(anchor.Year, 12, 31)),
            _ => (new DateTime(anchor.Year, anchor.Month, 1), new DateTime(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month)))
        };
    }

    private static bool ContainsIgnoreCase(string? source, string value) =>
        !string.IsNullOrWhiteSpace(source) &&
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush CreateBrush(string hex, string fallbackHex = "#FF488399")
    {
        var normalized = string.IsNullOrWhiteSpace(hex) ? fallbackHex : hex;

        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalized));
        }
        catch
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        }
    }
}
