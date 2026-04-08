using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;

namespace CoupleFinance.Infrastructure.Services;

public static class SeedDataFactory
{
    public static IReadOnlyList<Category> DefaultCategories(Guid householdId) =>
    [
        new Category { HouseholdId = householdId, Name = "Moradia", ColorHex = "#FF7C5DFA", IconKey = "HomeCity", Type = CategoryType.Expense, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Alimentação", ColorHex = "#FFF5A623", IconKey = "SilverwareForkKnife", Type = CategoryType.Expense, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Transporte", ColorHex = "#FF32C6C6", IconKey = "Car", Type = CategoryType.Expense, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Lazer", ColorHex = "#FFFF7A59", IconKey = "PartyPopper", Type = CategoryType.Expense, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Saúde", ColorHex = "#FFFC5C7D", IconKey = "HeartPulse", Type = CategoryType.Expense, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Salário", ColorHex = "#FF3DDC97", IconKey = "CashPlus", Type = CategoryType.Income, IsDefault = true },
        new Category { HouseholdId = householdId, Name = "Investimentos", ColorHex = "#FF5DADE2", IconKey = "ChartLine", Type = CategoryType.Both, IsDefault = true }
    ];
}
