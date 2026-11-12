using System.Globalization;
using System.Text.RegularExpressions;

namespace ShuatiKing.Models;

public static class NumericAnswer
{
    public static bool TryParse(string? text, out decimal value)
    {
        var normalized = text?.Replace('−', '-');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
        return value != 0 || !normalized!.TakeWhile(character => character is not ('e' or 'E')).Any(character => character is >= '1' and <= '9');
    }
    public static string Format(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);

    public static bool TryParseOriginal(string text, out decimal value)
    {
        if (TryParse(text, out value)) return true;
        // 只从明确的单个数值及单位中提取结果。公式和多值答案留给人工确认。
        var match = Regex.Match(text.Trim().Replace('−', '-'), @"^(?<number>[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\s*(?![eE]$)[\p{L}%°℃][\p{L}%°℃/·²³]*$");
        return match.Success && TryParse(match.Groups["number"].Value, out value);
    }

    public static bool Matches(string? text, decimal? reference, decimal tolerance)
    {
        if (reference is not { } expected || tolerance < 0 || !TryParse(text, out var actual)) return false;
        try { return Math.Abs(actual - expected) <= tolerance; }
        catch (OverflowException) { return false; }
    }
}
