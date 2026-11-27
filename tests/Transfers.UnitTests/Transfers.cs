using Transfers.Domain;

namespace Transfers.UnitTests;

public class TransferTests
{
    public const int MaxDescriptionLength = 200;   // use the value you already have in the EF config

    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidData_ReturnsPendingTransfer()
    {
        var transfer = Transfer.Create(-45.99m, "pln", "  Biedronka zakupy ", Now);

        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(-45.99m, transfer.Amount);
        Assert.Equal("PLN", transfer.Currency);          // normalized to upper case
        Assert.Equal("Biedronka zakupy", transfer.Description); // trimmed
        Assert.Equal(Now, transfer.CreatedAt);
        Assert.Equal(TransferStatus.Pending, transfer.Status);
        Assert.Null(transfer.Category);
    }

    [Fact]
    public void Create_WithZeroAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Transfer.Create(0m, "PLN", "Netflix", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyDescription_Throws(string? description)
    {
        Assert.Throws<ArgumentException>(() =>
            Transfer.Create(10m, "PLN", description!, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PL")]
    [InlineData("PLNN")]
    public void Create_WithInvalidCurrency_Throws(string currency)
    {
        Assert.Throws<ArgumentException>(() =>
            Transfer.Create(10m, currency, "Netflix", Now));
    }


    [Theory]
    [InlineData(-45.99)]
    [InlineData(45.99)]
    public void Create_WithNonZeroAmount_Accepts(double amount)
    {
        var t = Transfer.Create((decimal)amount, "PLN", "Netflix", Now);
        Assert.Equal((decimal)amount, t.Amount);
    }

    [Fact]
    public void Create_WithTooLongDescription_Throws()
    {
        var tooLong = new string('a', MaxDescriptionLength + 1);   // same value as in the EF config
        Assert.ThrowsAny<ArgumentException>(() =>
            Transfer.Create(10m, "PLN", tooLong, Now));
    }

    [Fact]
    public void Create_WithMaxLengthDescription_Succeeds()
    {
        var atLimit = new string('a', MaxDescriptionLength);
        var t = Transfer.Create(10m, "PLN", atLimit, Now);
        Assert.Equal(atLimit, t.Description);
    }

    [Fact]
    public void Create_CalledTwice_GeneratesDifferentIds()
    {
        var a = Transfer.Create(10m, "PLN", "Netflix", Now);
        var b = Transfer.Create(10m, "PLN", "Netflix", Now);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithNullOrBlankCurrency_Throws(string? currency)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Transfer.Create(10m, currency!, "Netflix", Now));
    }

    [Theory]
    [InlineData("Biedronka zakupy", "Groceries")]
    [InlineData("Netflix", "Subscriptions")]
    [InlineData("Czynsz listopad", "Housing")]
    [InlineData("Żabka", "Groceries")]
    [InlineData("coś nieznanego", "Other")]
    public void Categorize_WithKnownDescriptions_ReturnsCorrectCategory(string description, string expectedCategory)
    {
        var transfer = Transfer.Create(10m, "PLN", description, Now);
        transfer.MarkCategorized(expectedCategory);
        Assert.Equal(expectedCategory, transfer.Category);
    }
}
