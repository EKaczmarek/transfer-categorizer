using System.Globalization;
using System.Text;

using Transfers.Application;

public sealed class RuleBasedCategorizer : ITransferCategorizer
{
    private static readonly (string Keyword, string Category)[] Rules =
    [
        ("biedronka", "Groceries"), ("lidl", "Groceries"), ("zabka", "Groceries"),
        ("netflix", "Subscriptions"), ("spotify", "Subscriptions"),
        ("czynsz", "Housing")
    ];

    public string Categorize(string description)
    {
        var text = Normalize(description);
        foreach (var (keyword, category) in Rules)
            if (text.Contains(keyword)) return category;
        return "Other";
    }

    private static string Normalize(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Replace('ł', 'l');   // ł nie rozkłada się przy FormD
    }
}