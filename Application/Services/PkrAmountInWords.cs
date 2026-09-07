using System.Globalization;

namespace SaluExamPortal.Application.Services;

public static class PkrAmountInWords
{
    private static readonly string[] Units =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
        ["", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

    public static string ToEnglish(decimal amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)Math.Floor(amount);
        var paisa = (int)Math.Round((amount - rupees) * 100m, 0, MidpointRounding.AwayFromZero);
        if (paisa == 100)
        {
            rupees += 1;
            paisa = 0;
        }

        if (rupees == 0 && paisa == 0)
            return "Zero Rupees Only";

        var parts = new List<string>();
        if (rupees > 0)
            parts.Add($"{ConvertBelowThousandCrore(rupees)} {(rupees == 1 ? "Rupee" : "Rupees")}");
        else
            parts.Add("Zero Rupees");

        if (paisa > 0)
            parts.Add($"and {ConvertTwoDigit(paisa)} {(paisa == 1 ? "Paisa" : "Paisas")}");

        return string.Join(" ", parts) + " Only";
    }

    private static string ConvertBelowThousandCrore(long n)
    {
        if (n == 0) return "Zero";

        var crore = n / 10_000_000;
        n %= 10_000_000;
        var lakh = n / 100_000;
        n %= 100_000;
        var thousand = n / 1_000;
        n %= 1_000;

        var chunks = new List<string>();
        if (crore > 0) chunks.Add($"{ConvertBelowThousand((int)crore)} Crore");
        if (lakh > 0) chunks.Add($"{ConvertBelowThousand((int)lakh)} Lakh");
        if (thousand > 0) chunks.Add($"{ConvertBelowThousand((int)thousand)} Thousand");
        if (n > 0) chunks.Add(ConvertBelowThousand((int)n));
        return string.Join(" ", chunks);
    }

    private static string ConvertBelowThousand(int n)
    {
        if (n == 0) return "";
        if (n < 20) return Units[n];
        if (n < 100)
        {
            var ten = n / 10;
            var unit = n % 10;
            return unit == 0 ? Tens[ten] : $"{Tens[ten]} {Units[unit]}";
        }

        var hundred = n / 100;
        var rest = n % 100;
        return rest == 0 ? $"{Units[hundred]} Hundred" : $"{Units[hundred]} Hundred {ConvertBelowThousand(rest)}";
    }

    private static string ConvertTwoDigit(int n)
    {
        if (n < 20) return Units[n];
        return ConvertBelowThousand(n);
    }

    public static string FormatPkr(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture);
}
