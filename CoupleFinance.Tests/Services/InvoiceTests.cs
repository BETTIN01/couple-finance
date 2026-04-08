using CoupleFinance.Domain.Entities;
using CoupleFinance.Domain.Enums;
using FluentAssertions;

namespace CoupleFinance.Tests.Services;

public class InvoiceTests
{
    [Fact]
    public void RegisterPayment_ShouldMarkInvoiceAsPaid_WhenAmountMatchesTotal()
    {
        var invoice = new Invoice
        {
            HouseholdId = Guid.NewGuid(),
            CreditCardId = Guid.NewGuid(),
            ReferenceMonth = 4,
            ReferenceYear = 2026,
            TotalAmount = 1500m,
            PaidAmount = 0m,
            Status = InvoiceStatus.Pending
        };

        invoice.RegisterPayment(1500m, DateTime.UtcNow);

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAmount.Should().Be(1500m);
        invoice.PaidAtUtc.Should().NotBeNull();
    }
}
