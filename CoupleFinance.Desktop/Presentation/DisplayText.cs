using System.Text.RegularExpressions;
using CoupleFinance.Application.Models.Dashboard;
using CoupleFinance.Desktop.ViewModels;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Desktop.Presentation;

public static partial class DisplayText
{
    public static string FromEnum(object? value) => value switch
    {
        null => string.Empty,
        AccountType.Checking => "Conta corrente",
        AccountType.Savings => "Poupança",
        AccountType.Cash => "Dinheiro",
        AccountType.DigitalWallet => "Carteira digital",
        AccountType.Investment => "Conta de investimentos",
        CategoryType.Income => "Receita",
        CategoryType.Expense => "Despesa",
        EntryScope.Individual => "Individual",
        EntryScope.Joint => "Conjunto",
        GoalType.EmergencyFund => "Reserva de emergência",
        GoalType.Travel => "Viagem",
        GoalType.Vehicle => "Veículo",
        GoalType.RealEstate => "Imóvel",
        GoalType.Lifestyle => "Qualidade de vida",
        GoalType.Other => "Outro objetivo",
        InsightSeverity.Positive => "Oportunidade",
        InsightSeverity.Neutral => "Acompanhamento",
        InsightSeverity.Warning => "Atenção",
        InsightSeverity.Critical => "Prioridade",
        InvestmentAssetType.FixedIncome => "Renda fixa",
        InvestmentAssetType.Stock => "Ações",
        InvestmentAssetType.Fund => "Fundos",
        InvestmentAssetType.Crypto => "Cripto",
        InvestmentAssetType.CashReserve => "Reserva em caixa",
        InvestmentAssetType.Other => "Outro ativo",
        InvoiceStatus.Pending => "Pendente",
        InvoiceStatus.Paid => "Paga",
        InvoiceStatus.Overdue => "Em atraso",
        OwnershipFilter.All => "Visão completa",
        OwnershipFilter.Mine => "Somente o meu",
        OwnershipFilter.Partner => "Somente o parceiro",
        OwnershipFilter.Joint => "Somente o conjunto",
        PeriodPreset.Day => "Dia",
        PeriodPreset.Month => "Mês",
        PeriodPreset.Year => "Ano",
        TransactionKind.Income => "Receita",
        TransactionKind.Expense => "Despesa",
        TransactionKind.TransferOut => "Transferência enviada",
        TransactionKind.TransferIn => "Transferência recebida",
        TransactionKind.CardInvoicePayment => "Pagamento de fatura",
        TransactionKind.Adjustment => "Ajuste",
        NavigationSection.Dashboard => "Dashboard",
        NavigationSection.Transactions => "Movimentações",
        NavigationSection.Accounts => "Contas",
        NavigationSection.Cards => "Cartões",
        NavigationSection.Planning => "Planejamento",
        NavigationSection.Investments => "Investimentos",
        NavigationSection.Insights => "Insights",
        NavigationSection.Settings => "Configurações",
        _ => SplitPascalCase(value.ToString() ?? string.Empty)
    };

    public static string OwnershipLabel(Guid ownerId, Guid currentUserId, string currentUserName, Guid? partnerId, string? partnerName, EntryScope scope)
    {
        if (scope == EntryScope.Joint)
        {
            return "Conjunto";
        }

        if (ownerId == currentUserId)
        {
            return "Você";
        }

        if (partnerId.HasValue && ownerId == partnerId.Value)
        {
            return string.IsNullOrWhiteSpace(partnerName) ? "Parceiro(a)" : partnerName;
        }

        return currentUserName;
    }

    public static string SplitPascalCase(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : WordBoundaryRegex().Replace(value, " $1").Trim();

    [GeneratedRegex("(\\B[A-Z])")]
    private static partial Regex WordBoundaryRegex();
}
