using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoupleFinance.Application.Contracts;
using CoupleFinance.Application.Models;
using CoupleFinance.Application.Models.Auth;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Desktop.Configuration;
using CoupleFinance.Desktop.Services;
using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace CoupleFinance.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IFinanceWorkspaceService financeWorkspaceService;
    private readonly ISyncCoordinator syncCoordinator;
    private readonly IOptionsMonitor<SyncAutomationOptions> syncOptionsMonitor;
    private readonly DispatcherTimer _syncTimer = new();
    private AuthSession? _session;
    private bool _isSyncing;

    public ShellViewModel(
        IFinanceWorkspaceService financeWorkspaceService,
        ISyncCoordinator syncCoordinator,
        AppUpdateService updater,
        IOptionsMonitor<SyncAutomationOptions> syncOptionsMonitor)
    {
        this.financeWorkspaceService = financeWorkspaceService;
        this.syncCoordinator = syncCoordinator;
        this.syncOptionsMonitor = syncOptionsMonitor;
        Updater = updater;
    }

    public event Func<Task>? SignedOut;

    public BankAccountFormModel BankAccountForm { get; } = new();
    public TransactionFormModel TransactionForm { get; } = new();
    public TransferFormModel TransferForm { get; } = new();
    public CreditCardFormModel CreditCardForm { get; } = new();
    public CardPurchaseFormModel CardPurchaseForm { get; } = new();
    public InvoicePaymentFormModel InvoicePaymentForm { get; } = new();
    public GoalFormModel GoalForm { get; } = new();
    public InvestmentFormModel InvestmentForm { get; } = new();
    public CategoryFormModel CategoryForm { get; } = new();
    public AppUpdateService Updater { get; }

    public IReadOnlyList<PeriodPreset> PeriodPresets { get; } = Enum.GetValues<PeriodPreset>();
    public IReadOnlyList<OwnershipFilter> OwnershipFilters { get; } = Enum.GetValues<OwnershipFilter>();
    public IReadOnlyList<AccountType> AccountTypes { get; } = Enum.GetValues<AccountType>();
    public IReadOnlyList<TransactionKind> TransactionKinds { get; } = [TransactionKind.Income, TransactionKind.Expense, TransactionKind.Adjustment];
    public IReadOnlyList<EntryScope> EntryScopes { get; } = Enum.GetValues<EntryScope>();
    public IReadOnlyList<GoalType> GoalTypes { get; } = Enum.GetValues<GoalType>();
    public IReadOnlyList<InvestmentAssetType> AssetTypes { get; } = Enum.GetValues<InvestmentAssetType>();
    public IReadOnlyList<CategoryType> CategoryTypes { get; } = Enum.GetValues<CategoryType>();

    [ObservableProperty] private WorkspaceSnapshot? currentSnapshot;
    [ObservableProperty] private NavigationSection currentSection = NavigationSection.Dashboard;
    [ObservableProperty] private PeriodPreset selectedPeriod = PeriodPreset.Month;
    [ObservableProperty] private OwnershipFilter selectedOwnership = OwnershipFilter.All;
    [ObservableProperty] private DateTime anchorDate = DateTime.Today;
    [ObservableProperty] private string statusMessage = "Tudo pronto para organizar o mês.";
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private ISeries[] trendSeries = [];
    [ObservableProperty] private ISeries[] categorySeries = [];
    [ObservableProperty] private ISeries[] comparisonSeries = [];
    [ObservableProperty] private Axis[] trendXAxes = [];
    [ObservableProperty] private Axis[] trendYAxes = [];
    [ObservableProperty] private Axis[] comparisonXAxes = [];
    [ObservableProperty] private Axis[] comparisonYAxes = [];

    public string SectionTitle => CurrentSection switch
    {
        NavigationSection.Transactions => "Movimentações",
        NavigationSection.Accounts => "Contas e categorias",
        NavigationSection.Cards => "Cartões e faturas",
        NavigationSection.Planning => "Planejamento",
        NavigationSection.Investments => "Investimentos",
        NavigationSection.Insights => "Insights inteligentes",
        NavigationSection.Settings => "Configurações",
        _ => "Dashboard"
    };

    public string PeriodLabel => SelectedPeriod switch
    {
        PeriodPreset.Day => AnchorDate.ToString("dd 'de' MMMM"),
        PeriodPreset.Year => AnchorDate.ToString("yyyy"),
        _ => AnchorDate.ToString("MMMM 'de' yyyy")
    };

    public IRelayCommand<string> NavigateCommand => new RelayCommand<string>(Navigate);
    public IRelayCommand PreviousPeriodCommand => new RelayCommand(GoToPreviousPeriod);
    public IRelayCommand NextPeriodCommand => new RelayCommand(GoToNextPeriod);
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);
    public IAsyncRelayCommand SyncNowCommand => new AsyncRelayCommand(() => SyncNowAsync(true));
    public IAsyncRelayCommand SignOutCommand => new AsyncRelayCommand(SignOutAsync);
    public IAsyncRelayCommand SaveBankAccountCommand => new AsyncRelayCommand(SaveBankAccountAsync);
    public IAsyncRelayCommand SaveTransactionCommand => new AsyncRelayCommand(SaveTransactionAsync);
    public IAsyncRelayCommand SaveTransferCommand => new AsyncRelayCommand(SaveTransferAsync);
    public IAsyncRelayCommand SaveCreditCardCommand => new AsyncRelayCommand(SaveCreditCardAsync);
    public IAsyncRelayCommand SaveCardPurchaseCommand => new AsyncRelayCommand(SaveCardPurchaseAsync);
    public IAsyncRelayCommand PayInvoiceCommand => new AsyncRelayCommand(PayInvoiceAsync);
    public IAsyncRelayCommand SaveGoalCommand => new AsyncRelayCommand(SaveGoalAsync);
    public IAsyncRelayCommand SaveInvestmentCommand => new AsyncRelayCommand(SaveInvestmentAsync);
    public IAsyncRelayCommand SaveCategoryCommand => new AsyncRelayCommand(SaveCategoryAsync);
    public IAsyncRelayCommand RefreshInsightsCommand => new AsyncRelayCommand(RefreshInsightsAsync);
    public IAsyncRelayCommand CheckForUpdatesCommand => new AsyncRelayCommand(CheckForUpdatesAsync);
    public IAsyncRelayCommand InstallUpdateCommand => new AsyncRelayCommand(InstallUpdateAsync);

    private SyncAutomationOptions CurrentSyncOptions => syncOptionsMonitor.CurrentValue;

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        ConfigureSyncTimer();

        await financeWorkspaceService.InitializeAsync(session);

        if (CurrentSyncOptions.Enabled && CurrentSyncOptions.SyncOnStartup)
        {
            await SyncNowAsync(false);
        }
        else
        {
            await RefreshAsync();
        }

        if (CurrentSyncOptions.Enabled)
        {
            _syncTimer.Start();
        }
    }

    partial void OnCurrentSectionChanged(NavigationSection value)
    {
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionSubtitle));
    }

    partial void OnSelectedPeriodChanged(PeriodPreset value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        OnPropertyChanged(nameof(HeaderFilterSummary));
        _ = RefreshAsync();
    }

    partial void OnSelectedOwnershipChanged(OwnershipFilter value)
    {
        OnPropertyChanged(nameof(HeaderFilterSummary));
        _ = RefreshAsync();
    }

    partial void OnAnchorDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(PeriodLabel));
        _ = RefreshAsync();
    }

    private void ConfigureSyncTimer()
    {
        _syncTimer.Stop();
        _syncTimer.Tick -= HandleSyncTick;

        if (!CurrentSyncOptions.Enabled)
        {
            return;
        }

        _syncTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, CurrentSyncOptions.IntervalSeconds));
        _syncTimer.Tick += HandleSyncTick;
    }

    private async void HandleSyncTick(object? sender, EventArgs e) => await SyncNowAsync(false);

    private async Task RefreshAsync()
    {
        if (_session is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            CurrentSnapshot = await financeWorkspaceService.GetWorkspaceSnapshotAsync(_session, CreateFilter());
            BuildCharts();
            EnsureDefaultSelections();
            StatusMessage = Updater.IsUpdateAvailable
                ? Updater.StatusText
                : CurrentSnapshot.Sync.IsConfigured
                    ? CurrentSnapshot.Sync.StatusText
                    : "Modo local ativo. Seus lançamentos continuam salvos nesta máquina mesmo sem internet.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncNowAsync(bool surfaceMessage)
    {
        if (_session is null || _isSyncing || !CurrentSyncOptions.Enabled)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            var result = await syncCoordinator.SyncAsync(_session);
            var shouldRefresh = CurrentSnapshot is null ||
                                surfaceMessage ||
                                (CurrentSyncOptions.RefreshAfterAutomaticSync &&
                                 (result.DownloadedCount > 0 || result.UploadedCount > 0));

            if (shouldRefresh)
            {
                await RefreshAsync();
            }

            if (surfaceMessage)
            {
                StatusMessage = result.Succeeded
                    ? $"Sincronização concluída: {result.UploadedCount} enviado(s) e {result.DownloadedCount} recebido(s)."
                    : $"Sincronização não concluída: {result.ErrorMessage}";
                return;
            }

            if (!result.Succeeded && !Updater.IsUpdateAvailable && CurrentSnapshot?.Sync.IsConfigured == true)
            {
                StatusMessage = $"Sincronização automática em espera: {result.ErrorMessage}";
            }
            else if (!shouldRefresh && !Updater.IsUpdateAvailable && CurrentSnapshot?.Sync.IsConfigured == true)
            {
                StatusMessage = "Sincronização automática ativa em segundo plano.";
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void TriggerAutomaticSyncAfterLocalChange()
    {
        if (_session is null || !CurrentSyncOptions.Enabled || !CurrentSyncOptions.SyncAfterLocalChanges)
        {
            return;
        }

        _ = TriggerAutomaticSyncAfterLocalChangeAsync();
    }

    private async Task TriggerAutomaticSyncAfterLocalChangeAsync()
    {
        try
        {
            await SyncNowAsync(false);
        }
        catch (Exception ex)
        {
            if (!Updater.IsUpdateAvailable)
            {
                StatusMessage = $"Sincronização automática em espera: {ex.Message}";
            }
        }
    }

    private async Task SaveBankAccountAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveBankAccountAsync(
            _session,
            new BankAccountInput(null, BankAccountForm.Name, BankAccountForm.Institution, BankAccountForm.Type, BankAccountForm.CurrentBalance, BankAccountForm.ColorHex));

        BankAccountForm.Name = string.Empty;
        BankAccountForm.Institution = string.Empty;
        BankAccountForm.CurrentBalance = 0;
        await RefreshAsync();
        StatusMessage = "Conta salva com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveTransactionAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveTransactionAsync(
            _session,
            new TransactionInput(null, TransactionForm.Description, TransactionForm.Amount, TransactionForm.OccurredOn, TransactionForm.CategoryId, TransactionForm.BankAccountId, TransactionForm.Kind, TransactionForm.Scope, TransactionForm.Notes));

        TransactionForm.Description = string.Empty;
        TransactionForm.Amount = 0;
        TransactionForm.Notes = string.Empty;
        await RefreshAsync();
        StatusMessage = "Movimentação registrada com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveTransferAsync()
    {
        if (_session is null || !TransferForm.FromBankAccountId.HasValue || !TransferForm.ToBankAccountId.HasValue)
        {
            return;
        }

        await financeWorkspaceService.SaveTransferAsync(
            _session,
            new TransferInput(null, TransferForm.FromBankAccountId.Value, TransferForm.ToBankAccountId.Value, TransferForm.Amount, TransferForm.OccurredOn, TransferForm.Description));

        TransferForm.Amount = 0;
        TransferForm.Description = "Transferência interna";
        await RefreshAsync();
        StatusMessage = "Transferência registrada com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveCreditCardAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveCreditCardAsync(
            _session,
            new CreditCardInput(null, CreditCardForm.Name, CreditCardForm.Brand, CreditCardForm.LimitAmount, CreditCardForm.ClosingDay, CreditCardForm.DueDay, CreditCardForm.ColorHex));

        CreditCardForm.Name = string.Empty;
        CreditCardForm.LimitAmount = 0;
        await RefreshAsync();
        StatusMessage = "Cartão salvo com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveCardPurchaseAsync()
    {
        if (_session is null || !CardPurchaseForm.CreditCardId.HasValue)
        {
            return;
        }

        await financeWorkspaceService.SaveCardPurchaseAsync(
            _session,
            new CardPurchaseInput(null, CardPurchaseForm.CreditCardId.Value, CardPurchaseForm.Description, CardPurchaseForm.Amount, CardPurchaseForm.PurchaseDate, CardPurchaseForm.InstallmentCount, CardPurchaseForm.CategoryId, CardPurchaseForm.Scope, CardPurchaseForm.Notes));

        CardPurchaseForm.Description = string.Empty;
        CardPurchaseForm.Amount = 0;
        CardPurchaseForm.Notes = string.Empty;
        CardPurchaseForm.InstallmentCount = 1;
        await RefreshAsync();
        StatusMessage = "Compra adicionada ao cartão.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task PayInvoiceAsync()
    {
        if (_session is null || !InvoicePaymentForm.InvoiceId.HasValue || !InvoicePaymentForm.BankAccountId.HasValue)
        {
            return;
        }

        await financeWorkspaceService.PayInvoiceAsync(
            _session,
            new InvoicePaymentInput(InvoicePaymentForm.InvoiceId.Value, InvoicePaymentForm.BankAccountId.Value, InvoicePaymentForm.Amount, InvoicePaymentForm.PaidOn));

        InvoicePaymentForm.Amount = 0;
        await RefreshAsync();
        StatusMessage = "Pagamento da fatura registrado.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveGoalAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveGoalAsync(
            _session,
            new GoalInput(null, GoalForm.Name, GoalForm.GoalType, GoalForm.TargetAmount, GoalForm.CurrentAmount, GoalForm.MonthlyContributionTarget, GoalForm.TargetDate, GoalForm.Notes));

        GoalForm.Name = string.Empty;
        GoalForm.TargetAmount = 0;
        GoalForm.CurrentAmount = 0;
        GoalForm.MonthlyContributionTarget = 0;
        await RefreshAsync();
        StatusMessage = "Meta salva com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveInvestmentAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveInvestmentAssetAsync(
            _session,
            new InvestmentAssetInput(null, InvestmentForm.Name, InvestmentForm.Ticker, InvestmentForm.Broker, InvestmentForm.AssetType, InvestmentForm.InvestedAmount, InvestmentForm.CurrentValue, InvestmentForm.Quantity, InvestmentForm.Scope, InvestmentForm.UpdatedOn));

        InvestmentForm.Name = string.Empty;
        InvestmentForm.Ticker = string.Empty;
        InvestmentForm.Broker = string.Empty;
        InvestmentForm.InvestedAmount = 0;
        InvestmentForm.CurrentValue = 0;
        await RefreshAsync();
        StatusMessage = "Investimento salvo com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task SaveCategoryAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.SaveCategoryAsync(
            _session,
            new CategoryInput(null, CategoryForm.Name, CategoryForm.IconKey, CategoryForm.ColorHex, CategoryForm.Type));

        CategoryForm.Name = string.Empty;
        await RefreshAsync();
        StatusMessage = "Categoria criada com sucesso.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task RefreshInsightsAsync()
    {
        if (_session is null)
        {
            return;
        }

        await financeWorkspaceService.RefreshInsightsAsync(_session, AnchorDate);
        await RefreshAsync();
        StatusMessage = "Insights atualizados com base nos lançamentos mais recentes.";
        TriggerAutomaticSyncAfterLocalChange();
    }

    private async Task CheckForUpdatesAsync()
    {
        var updateAvailable = await Updater.CheckForUpdatesAsync();
        StatusMessage = Updater.StatusText;

        if (!updateAvailable)
        {
            MessageBox.Show(
                Updater.StatusText,
                "Atualização",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var prepared = await Updater.PrepareUpdateAsync();
        StatusMessage = Updater.StatusText;

        if (!prepared)
        {
            MessageBox.Show(
                Updater.StatusText,
                "Atualizacao",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await PromptToApplyPreparedUpdateAsync();
    }

    private Task InstallUpdateAsync() => InstallUpdateCoreAsync();

    private async Task InstallUpdateCoreAsync()
    {
        if (!Updater.IsUpdateAvailable)
        {
            MessageBox.Show(
                "Ainda não existe uma nova versão pronta para instalar.",
                "Atualização",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!Updater.IsUpdateReadyToApply)
        {
            var prepared = await Updater.PrepareUpdateAsync();
            StatusMessage = Updater.StatusText;

            if (!prepared)
            {
                MessageBox.Show(
                    Updater.StatusText,
                    "Atualizacao",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var started = await PromptToApplyPreparedUpdateAsync();
        StatusMessage = Updater.StatusText;

        if (!started)
        {
            MessageBox.Show(
                Updater.StatusText,
                "Atualização",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task<bool> PromptToApplyPreparedUpdateAsync()
    {
        if (!Updater.IsUpdateReadyToApply)
        {
            return false;
        }

        var result = MessageBox.Show(
            $"A atualizacao {Updater.PreparedVersion} ja foi baixada. Podemos reiniciar o aplicativo agora para aplicar a nova versao?",
            "Atualizacao pronta",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            StatusMessage = $"Atualizacao {Updater.PreparedVersion} baixada. Podemos reiniciar o app quando voce quiser aplicar.";
            return true;
        }

        return await Updater.ApplyPreparedUpdateAsync();
    }

    private async Task SignOutAsync()
    {
        _syncTimer.Stop();
        if (SignedOut is not null)
        {
            await SignedOut.Invoke();
        }
    }

    private void Navigate(string? sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return;
        }

        CurrentSection = Enum.Parse<NavigationSection>(sectionName, true);
    }

    private void GoToPreviousPeriod()
    {
        AnchorDate = SelectedPeriod switch
        {
            PeriodPreset.Day => AnchorDate.AddDays(-1),
            PeriodPreset.Year => AnchorDate.AddYears(-1),
            _ => AnchorDate.AddMonths(-1)
        };
    }

    private void GoToNextPeriod()
    {
        AnchorDate = SelectedPeriod switch
        {
            PeriodPreset.Day => AnchorDate.AddDays(1),
            PeriodPreset.Year => AnchorDate.AddYears(1),
            _ => AnchorDate.AddMonths(1)
        };
    }

    private PeriodFilter CreateFilter() => new(SelectedPeriod, AnchorDate, SelectedOwnership);

    public void Dispose() => _syncTimer.Stop();

    private void EnsureDefaultSelections()
    {
        if (CurrentSnapshot is null)
        {
            return;
        }

        TransactionForm.CategoryId ??= CurrentSnapshot.Categories.FirstOrDefault()?.Id;
        TransactionForm.BankAccountId ??= CurrentSnapshot.Accounts.FirstOrDefault()?.Id;
        TransferForm.FromBankAccountId ??= CurrentSnapshot.Accounts.FirstOrDefault()?.Id;
        TransferForm.ToBankAccountId ??= CurrentSnapshot.Accounts.Skip(1).FirstOrDefault()?.Id ?? CurrentSnapshot.Accounts.FirstOrDefault()?.Id;
        CardPurchaseForm.CreditCardId ??= CurrentSnapshot.CreditCards.FirstOrDefault()?.Id;
        CardPurchaseForm.CategoryId ??= CurrentSnapshot.Categories.FirstOrDefault()?.Id;
        InvoicePaymentForm.InvoiceId ??= CurrentSnapshot.Invoices.FirstOrDefault(x => x.Status != InvoiceStatus.Paid)?.Id;
        InvoicePaymentForm.BankAccountId ??= CurrentSnapshot.Accounts.FirstOrDefault()?.Id;
    }

    private void BuildCharts()
    {
        if (CurrentSnapshot is null)
        {
            TrendSeries = [];
            CategorySeries = [];
            ComparisonSeries = [];
            return;
        }

        var trend = CurrentSnapshot.Dashboard.TrendPoints;
        TrendSeries =
        [
            new LineSeries<double>
            {
                Values = trend.Select(x => (double)x.Income).ToArray(),
                Name = "Receitas",
                Stroke = new SolidColorPaint(SKColor.Parse("488399"), 3),
                Fill = null,
                GeometrySize = 8
            },
            new LineSeries<double>
            {
                Values = trend.Select(x => (double)x.Expense).ToArray(),
                Name = "Despesas",
                Stroke = new SolidColorPaint(SKColor.Parse("EA9393"), 3),
                Fill = null,
                GeometrySize = 8
            },
            new LineSeries<double>
            {
                Values = trend.Select(x => (double)x.Balance).ToArray(),
                Name = "Saldo",
                Stroke = new SolidColorPaint(SKColor.Parse("F4D38A"), 3),
                Fill = null,
                GeometrySize = 7,
                LineSmoothness = 0.65
            }
        ];

        TrendXAxes =
        [
            new Axis
            {
                Labels = trend.Select(x => x.Label).ToArray(),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("D8D8D8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("4A6472"))
            }
        ];

        TrendYAxes =
        [
            new Axis
            {
                Labeler = value => $"R$ {value:N0}",
                LabelsPaint = new SolidColorPaint(SKColor.Parse("D8D8D8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("4A6472"))
            }
        ];

        CategorySeries = CurrentSnapshot.Dashboard.CategoryBreakdown
            .Where(slice => slice.Amount > 0)
            .Select(slice => new PieSeries<double>
            {
                Values = [(double)slice.Amount],
                Name = slice.Name,
                Fill = new SolidColorPaint(ParseColor(slice.ColorHex)),
                Stroke = new SolidColorPaint(SKColor.Parse("0E1730"), 2)
            })
            .Cast<ISeries>()
            .ToArray();

        var comparisons = CurrentSnapshot.Dashboard.Comparisons;
        ComparisonSeries =
        [
            new ColumnSeries<double>
            {
                Values = comparisons.Select(x => (double)x.Income).ToArray(),
                Name = "Receitas",
                Fill = new SolidColorPaint(SKColor.Parse("488399"))
            },
            new ColumnSeries<double>
            {
                Values = comparisons.Select(x => (double)x.Expense).ToArray(),
                Name = "Despesas",
                Fill = new SolidColorPaint(SKColor.Parse("EA9393"))
            }
        ];

        ComparisonXAxes =
        [
            new Axis
            {
                Labels = comparisons.Select(x => x.Label).ToArray(),
                LabelsPaint = new SolidColorPaint(SKColor.Parse("D8D8D8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("4A6472"))
            }
        ];

        ComparisonYAxes =
        [
            new Axis
            {
                Labeler = value => $"R$ {value:N0}",
                LabelsPaint = new SolidColorPaint(SKColor.Parse("D8D8D8")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("4A6472"))
            }
        ];
    }

    private static SKColor ParseColor(string hex)
    {
        var normalized = hex.Replace("#", string.Empty);
        return SKColor.Parse(normalized);
    }
}
