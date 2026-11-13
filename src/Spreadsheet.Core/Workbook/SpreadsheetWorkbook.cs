using Spreadsheet.Core.Model;
using Spreadsheet.Core.Storage;

namespace Spreadsheet.Core.Workbook;

/// <summary>Режим відображення клітинок.</summary>
public enum DisplayMode
{
    Expression,
    Value
}

/// <summary>
/// Робоча книга: поточна таблиця, ім'я файлу, ознака незбережених змін
/// і операції з файлами. Інтерфейс користувача лише викликає ці методи,
/// тому логіка не "зашита" в обробники кнопок.
/// </summary>
public class SpreadsheetWorkbook
{
    private StoredFileInfo? _currentFile;
    private ISpreadsheetStorage? _currentStorage;

    public SpreadsheetWorkbook()
    {
        Table = CreateTable(new SpreadsheetTable());
    }

    public SpreadsheetTable Table { get; private set; }

    public DisplayMode Mode { get; set; } = DisplayMode.Value;

    public bool HasUnsavedChanges { get; private set; }

    public string Title => (_currentFile?.Name ?? "Нова таблиця") + (HasUnsavedChanges ? " *" : string.Empty);

    public string? CurrentLocation => _currentStorage?.DisplayName;

    public event EventHandler? TableReplaced;
    public event EventHandler? Changed;

    public string GetDisplayText(int row, int column)
    {
        var cell = Table.GetCell(row, column);
        return Mode == DisplayMode.Expression ? cell.Expression : cell.DisplayValue;
    }

    public void SetExpression(int row, int column, string expression)
    {
        var cell = Table.GetCell(row, column);
        if (cell.Expression == (expression ?? string.Empty).Trim()) return;
        Table.SetExpression(row, column, expression ?? string.Empty);
        MarkChanged();
    }

    public void AddRow() { Table.AddRow(); MarkChanged(); }
    public void AddColumn() { Table.AddColumn(); MarkChanged(); }

    public IReadOnlyList<string> RemoveLastRow()
    {
        var result = Table.RemoveLastRow();
        MarkChanged();
        return result;
    }

    public IReadOnlyList<string> RemoveLastColumn()
    {
        var result = Table.RemoveLastColumn();
        MarkChanged();
        return result;
    }

    public void New(int rows = 10, int columns = 6)
    {
        Table = CreateTable(new SpreadsheetTable(rows, columns));
        _currentFile = null;
        _currentStorage = null;
        HasUnsavedChanges = false;
        TableReplaced?.Invoke(this, EventArgs.Empty);
    }

    public async Task OpenAsync(ISpreadsheetStorage storage, StoredFileInfo file, CancellationToken ct = default)
    {
        var document = await storage.LoadAsync(file.Id, ct);
        Table = CreateTable(SpreadsheetTable.FromDocument(document));
        _currentFile = file;
        _currentStorage = storage;
        HasUnsavedChanges = false;
        TableReplaced?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Зберігає під новим ім'ям у вказане сховище.</summary>
    public async Task SaveAsAsync(ISpreadsheetStorage storage, string name, CancellationToken ct = default)
    {
        _currentFile = await storage.SaveAsync(name, Table.ToDocument(), null, ct);
        _currentStorage = storage;
        HasUnsavedChanges = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Перезаписує поточний файл. Повертає false, якщо файл ще не має імені.</summary>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (_currentFile is null || _currentStorage is null) return false;
        _currentFile = await _currentStorage.SaveAsync(_currentFile.Name, Table.ToDocument(), _currentFile.Id, ct);
        HasUnsavedChanges = false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void MarkChanged()
    {
        HasUnsavedChanges = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static SpreadsheetTable CreateTable(SpreadsheetTable table) => table;
}
