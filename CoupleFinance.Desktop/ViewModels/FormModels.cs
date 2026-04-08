using CommunityToolkit.Mvvm.ComponentModel;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Desktop.ViewModels;

public partial class BankAccountFormModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string institution = string.Empty;
    [ObservableProperty] private AccountType type = AccountType.Checking;
    [ObservableProperty] private decimal currentBalance;
    [ObservableProperty] private string colorHex = "#FF3561F2";
}

public partial class TransactionFormModel : ObservableObject
{
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime occurredOn = DateTime.Today;
    [ObservableProperty] private Guid? categoryId;
    [ObservableProperty] private Guid? bankAccountId;
    [ObservableProperty] private TransactionKind kind = TransactionKind.Expense;
    [ObservableProperty] private EntryScope scope = EntryScope.Individual;
    [ObservableProperty] private string? notes;
}

public partial class TransferFormModel : ObservableObject
{
    [ObservableProperty] private Guid? fromBankAccountId;
    [ObservableProperty] private Guid? toBankAccountId;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime occurredOn = DateTime.Today;
    [ObservableProperty] private string description = "Transferência interna";
}

public partial class CreditCardFormModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string brand = "Visa";
    [ObservableProperty] private decimal limitAmount;
    [ObservableProperty] private int closingDay = 25;
    [ObservableProperty] private int dueDay = 5;
    [ObservableProperty] private string colorHex = "#FFF5A623";
}

public partial class CardPurchaseFormModel : ObservableObject
{
    [ObservableProperty] private Guid? creditCardId;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime purchaseDate = DateTime.Today;
    [ObservableProperty] private int installmentCount = 1;
    [ObservableProperty] private Guid? categoryId;
    [ObservableProperty] private EntryScope scope = EntryScope.Individual;
    [ObservableProperty] private string? notes;
}

public partial class InvoicePaymentFormModel : ObservableObject
{
    [ObservableProperty] private Guid? invoiceId;
    [ObservableProperty] private Guid? bankAccountId;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime paidOn = DateTime.Today;
}

public partial class GoalFormModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private GoalType goalType = GoalType.Travel;
    [ObservableProperty] private decimal targetAmount;
    [ObservableProperty] private decimal currentAmount;
    [ObservableProperty] private decimal monthlyContributionTarget;
    [ObservableProperty] private DateTime? targetDate = DateTime.Today.AddMonths(12);
    [ObservableProperty] private string? notes;
}

public partial class InvestmentFormModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string? ticker;
    [ObservableProperty] private string broker = string.Empty;
    [ObservableProperty] private InvestmentAssetType assetType = InvestmentAssetType.FixedIncome;
    [ObservableProperty] private decimal investedAmount;
    [ObservableProperty] private decimal currentValue;
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private EntryScope scope = EntryScope.Individual;
    [ObservableProperty] private DateTime updatedOn = DateTime.Today;
}

public partial class CategoryFormModel : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string iconKey = "Tag";
    [ObservableProperty] private string colorHex = "#FF7C5DFA";
    [ObservableProperty] private CategoryType type = CategoryType.Expense;
}
