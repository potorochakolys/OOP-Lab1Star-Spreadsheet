using System.Text;
using System.Text.RegularExpressions;

namespace Spreadsheet.Core.Model;

/// <summary>Адреса клітинки: стовпець (з 0) і рядок (з 0). Текстовий вигляд: A1, B12, AA3.</summary>
public readonly partial record struct CellAddress(int Column, int Row)
{
    public string Name => ColumnName(Column) + (Row + 1);

    public override string ToString() => Name;

    /// <summary>Перетворює номер стовпця (з 0) на літерну назву: 0 → A, 25 → Z, 26 → AA.</summary>
    public static string ColumnName(int column)
    {
        if (column < 0) throw new ArgumentOutOfRangeException(nameof(column));
        var sb = new StringBuilder();
        var n = column + 1;
        while (n > 0)
        {
            var rem = (n - 1) % 26;
            sb.Insert(0, (char)('A' + rem));
            n = (n - 1) / 26;
        }
        return sb.ToString();
    }

    public static int ColumnIndex(string letters)
    {
        var result = 0;
        foreach (var ch in letters.ToUpperInvariant())
            result = result * 26 + (ch - 'A' + 1);
        return result - 1;
    }

    public static bool TryParse(string name, out CellAddress address)
    {
        var match = NamePattern().Match(name.Trim());
        if (!match.Success)
        {
            address = default;
            return false;
        }
        address = new CellAddress(ColumnIndex(match.Groups[1].Value), int.Parse(match.Groups[2].Value) - 1);
        return true;
    }

    [GeneratedRegex("^([A-Za-z]+)([1-9][0-9]*)$")]
    private static partial Regex NamePattern();
}
