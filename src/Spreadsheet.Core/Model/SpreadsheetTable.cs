using Spreadsheet.Core.Evaluation;

namespace Spreadsheet.Core.Model;

/// <summary>
/// Електронна таблиця: зберігає клітинки, перераховує значення з урахуванням
/// залежностей і виявляє циклічні посилання.
/// </summary>
public class SpreadsheetTable
{
    public const int MinSize = 1;
    public const int MaxSize = 200;

    private readonly Dictionary<CellAddress, Cell> _cells = new();

    public SpreadsheetTable(int rows = 10, int columns = 6)
    {
        if (rows < MinSize || columns < MinSize)
            throw new ArgumentException("Таблиця повинна мати хоча б один рядок і стовпець");
        RowCount = rows;
        ColumnCount = columns;
    }

    public int RowCount { get; private set; }
    public int ColumnCount { get; private set; }

    /// <summary>Подія після перерахунку таблиці.</summary>
    public event EventHandler? Recalculated;

    public Cell GetCell(int row, int column) => GetCell(new CellAddress(column, row));

    public Cell GetCell(CellAddress address)
    {
        EnsureInside(address);
        if (!_cells.TryGetValue(address, out var cell))
        {
            cell = new Cell(address);
            _cells[address] = cell;
        }
        return cell;
    }

    private void EnsureInside(CellAddress address)
    {
        if (!IsInside(address))
            throw new ArgumentOutOfRangeException(nameof(address), $"Клітинка {address.Name} поза межами таблиці");
    }

    public Cell? FindCell(string name)
        => CellAddress.TryParse(name, out var address) && IsInside(address) ? GetCell(address) : null;

    public bool IsInside(CellAddress address)
        => address.Row >= 0 && address.Row < RowCount && address.Column >= 0 && address.Column < ColumnCount;

    /// <summary>Записує вираз у клітинку і перераховує таблицю.</summary>
    public void SetExpression(string cellName, string expression)
    {
        var cell = FindCell(cellName) ?? throw new ArgumentException($"Клітинки {cellName} не існує");
        SetExpression(cell, expression);
    }

    public void SetExpression(int row, int column, string expression)
        => SetExpression(GetCell(row, column), expression);

    private void SetExpression(Cell cell, string expression)
    {
        var text = (expression ?? string.Empty).Trim();
        if (text.StartsWith('=')) text = text[1..].Trim();
        cell.Expression = text;
        Recalculate();
    }

    public void AddRow() => ChangeSize(RowCount + 1, ColumnCount);
    public void AddColumn() => ChangeSize(RowCount, ColumnCount + 1);

    /// <summary>Видаляє останній рядок. Повертає список клітинок, які посилалися на видалені.</summary>
    public IReadOnlyList<string> RemoveLastRow()
    {
        if (RowCount <= MinSize) throw new InvalidOperationException("Не можна видалити єдиний рядок");
        return RemoveWhere(a => a.Row == RowCount - 1, () => RowCount--);
    }

    public IReadOnlyList<string> RemoveLastColumn()
    {
        if (ColumnCount <= MinSize) throw new InvalidOperationException("Не можна видалити єдиний стовпець");
        return RemoveWhere(a => a.Column == ColumnCount - 1, () => ColumnCount--);
    }

    /// <summary>Клітинки, які посилаються на рядок/стовпець, що буде видалено.</summary>
    public IReadOnlyList<string> GetDependentsOfLastRow()
        => DependentsOf(a => a.Row == RowCount - 1);

    public IReadOnlyList<string> GetDependentsOfLastColumn()
        => DependentsOf(a => a.Column == ColumnCount - 1);

    private IReadOnlyList<string> DependentsOf(Func<CellAddress, bool> removed)
    {
        var removedNames = AllAddresses().Where(removed).Select(a => a.Name).ToHashSet();
        return _cells.Values
            .Where(c => !removed(c.Address) && c.References.Any(removedNames.Contains))
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToList();
    }

    private IReadOnlyList<string> RemoveWhere(Func<CellAddress, bool> removed, Action shrink)
    {
        var dependents = DependentsOf(removed);
        foreach (var key in _cells.Keys.Where(removed).ToList())
            _cells.Remove(key);
        shrink();
        Recalculate();
        return dependents;
    }

    private void ChangeSize(int rows, int columns)
    {
        if (rows > MaxSize || columns > MaxSize)
            throw new InvalidOperationException($"Максимальний розмір таблиці {MaxSize}x{MaxSize}");
        RowCount = rows;
        ColumnCount = columns;
        Recalculate();
    }

    private IEnumerable<CellAddress> AllAddresses()
    {
        for (var r = 0; r < RowCount; r++)
            for (var c = 0; c < ColumnCount; c++)
                yield return new CellAddress(c, r);
    }

    /// <summary>Перераховує всі непорожні клітинки. Цикли та помилки позначаються в клітинках.</summary>
    public void Recalculate()
    {
        // 1. Синтаксичний аналіз і збір посилань
        foreach (var cell in _cells.Values)
        {
            cell.Value = null;
            cell.Error = null;
            cell.References = new HashSet<string>();
            if (cell.IsEmpty) continue;
            try
            {
                cell.References = ExpressionEvaluator.GetReferences(cell.Expression);
            }
            catch (SyntaxException ex)
            {
                cell.Error = ex.Message;
            }
        }

        // 2. Обчислення з пошуком у глибину (стан "в обробці" означає цикл)
        var state = new Dictionary<CellAddress, VisitState>();
        foreach (var cell in _cells.Values.ToList())
        {
            try
            {
                EvaluateCell(cell, state);
            }
            catch (EvaluationException)
            {
                // помилку вже записано у відповідну клітинку
            }
        }

        Recalculated?.Invoke(this, EventArgs.Empty);
    }

    private enum VisitState { InProgress, Done }

    private double EvaluateCell(Cell cell, Dictionary<CellAddress, VisitState> state)
    {
        if (state.TryGetValue(cell.Address, out var s))
        {
            if (s == VisitState.InProgress)
                throw new EvaluationException($"{CycleMessagePrefix} через {cell.Name}");
            return cell.Error is null ? cell.Value ?? 0 : throw new EvaluationException($"Помилка в клітинці {cell.Name}");
        }

        if (cell.IsEmpty)
        {
            state[cell.Address] = VisitState.Done;
            return 0;
        }

        if (cell.Error is not null)
        {
            state[cell.Address] = VisitState.Done;
            throw new EvaluationException($"Помилка в клітинці {cell.Name}");
        }

        state[cell.Address] = VisitState.InProgress;
        try
        {
            cell.Value = ExpressionEvaluator.Evaluate(cell.Expression, name =>
            {
                var referenced = FindCell(name)
                    ?? throw new EvaluationException($"Посилання на неіснуючу клітинку {name}");
                return EvaluateCell(referenced, state);
            });
            state[cell.Address] = VisitState.Done;
            return cell.Value.Value;
        }
        catch (Exception ex) when (ex is EvaluationException or SyntaxException)
        {
            cell.Value = null;
            cell.Error = ex.Message;
            state[cell.Address] = VisitState.Done;
            // Повідомлення про цикл передаємо далі без змін, щоб усі клітинки циклу були позначені
            throw ex.Message.StartsWith(CycleMessagePrefix)
                ? new EvaluationException(ex.Message)
                : new EvaluationException($"Помилка в клітинці {cell.Name}");
        }
    }

    public const string CycleMessagePrefix = "Циклічне посилання";

    /// <summary>Знімок вмісту таблиці для збереження.</summary>
    public SpreadsheetDocument ToDocument() => new()
    {
        Rows = RowCount,
        Columns = ColumnCount,
        Cells = _cells.Values
            .Where(c => !c.IsEmpty)
            .OrderBy(c => c.Address.Row).ThenBy(c => c.Address.Column)
            .ToDictionary(c => c.Name, c => c.Expression)
    };

    public static SpreadsheetTable FromDocument(SpreadsheetDocument document)
    {
        var rows = Math.Clamp(document.Rows, MinSize, MaxSize);
        var columns = Math.Clamp(document.Columns, MinSize, MaxSize);
        var table = new SpreadsheetTable(rows, columns);
        foreach (var (name, expression) in document.Cells)
        {
            if (CellAddress.TryParse(name, out var address) && table.IsInside(address))
                table.GetCell(address).Expression = expression;
        }
        table.Recalculate();
        return table;
    }
}
