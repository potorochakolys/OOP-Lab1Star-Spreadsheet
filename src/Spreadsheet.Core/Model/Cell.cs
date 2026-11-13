namespace Spreadsheet.Core.Model;

/// <summary>Клітинка таблиці: вираз, обчислене значення або текст помилки.</summary>
public class Cell
{
    public Cell(CellAddress address)
    {
        Address = address;
    }

    public CellAddress Address { get; }

    public string Name => Address.Name;

    /// <summary>Вираз, введений користувачем (без знака "=").</summary>
    public string Expression { get; internal set; } = string.Empty;

    /// <summary>Обчислене значення (null, якщо клітинка порожня або містить помилку).</summary>
    public double? Value { get; internal set; }

    /// <summary>Текст помилки, якщо вираз не вдалося розібрати чи обчислити.</summary>
    public string? Error { get; internal set; }

    /// <summary>Імена клітинок, на які посилається вираз.</summary>
    public IReadOnlySet<string> References { get; internal set; } = new HashSet<string>();

    public bool IsEmpty => string.IsNullOrWhiteSpace(Expression);

    /// <summary>Текст для режиму "Значення".</summary>
    public string DisplayValue =>
        Error is not null ? "#ПОМИЛКА"
        : Value is null ? string.Empty
        : Value.Value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);
}
