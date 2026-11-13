using Spreadsheet.Core.Model;
using Spreadsheet.Core.Storage;
using Spreadsheet.Core.Workbook;

namespace Spreadsheet.App;

/// <summary>
/// Головне вікно. Обробники подій лише передають дії робочій книзі
/// (<see cref="SpreadsheetWorkbook"/>) і оновлюють відображення.
/// </summary>
public partial class MainPage : ContentPage
{
    private const double CellWidth = 110;
    private const double CellHeight = 34;

    private readonly SpreadsheetWorkbook _workbook = new();
    private readonly LocalFileStorage _localStorage;
    private readonly GoogleDriveStorage _driveStorage;
    private Entry[,] _cellEntries = new Entry[0, 0];
    private (int Row, int Column) _selected = (0, 0);

    public MainPage()
    {
        InitializeComponent();

        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lab1Spreadsheets");
        _localStorage = new LocalFileStorage(appFolder);
        _driveStorage = new GoogleDriveStorage(
            Path.Combine(AppContext.BaseDirectory, "credentials.json"),
            Path.Combine(FileSystem.AppDataDirectory, "GoogleToken"));

        _workbook.TableReplaced += (_, _) => BuildGrid();
        _workbook.Changed += (_, _) => UpdateTitle();
        BuildGrid();
    }

    // ---------- Побудова і оновлення сітки ----------

    private void BuildGrid()
    {
        var table = _workbook.Table;
        CellsGrid.Children.Clear();
        CellsGrid.RowDefinitions.Clear();
        CellsGrid.ColumnDefinitions.Clear();

        CellsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        for (var c = 0; c < table.ColumnCount; c++)
            CellsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        for (var r = 0; r <= table.RowCount; r++)
            CellsGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

        CellsGrid.Add(HeaderLabel(string.Empty), 0, 0);
        for (var c = 0; c < table.ColumnCount; c++)
            CellsGrid.Add(HeaderLabel(CellAddress.ColumnName(c)), c + 1, 0);

        _cellEntries = new Entry[table.RowCount, table.ColumnCount];
        for (var r = 0; r < table.RowCount; r++)
        {
            CellsGrid.Add(HeaderLabel((r + 1).ToString()), 0, r + 1);
            for (var c = 0; c < table.ColumnCount; c++)
            {
                var entry = CreateCellEntry(r, c);
                _cellEntries[r, c] = entry;
                CellsGrid.Add(entry, c + 1, r + 1);
            }
        }

        _selected = (Math.Min(_selected.Row, table.RowCount - 1), Math.Min(_selected.Column, table.ColumnCount - 1));
        RefreshCells();
        UpdateTitle();
    }

    private static Label HeaderLabel(string text) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb("#E8EAED"),
        TextColor = Colors.Black,
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private Entry CreateCellEntry(int row, int column)
    {
        var entry = new Entry
        {
            BackgroundColor = Colors.White,
            TextColor = Colors.Black,
            FontSize = 13
        };
        entry.Focused += (_, _) => SelectCell(row, column);
        entry.Completed += (_, _) => CommitCellEntry(row, column, entry);
        entry.Unfocused += (_, _) => CommitCellEntry(row, column, entry);
        return entry;
    }

    private void RefreshCells()
    {
        var table = _workbook.Table;
        var expressionMode = _workbook.Mode == DisplayMode.Expression;
        for (var r = 0; r < table.RowCount; r++)
        {
            for (var c = 0; c < table.ColumnCount; c++)
            {
                var entry = _cellEntries[r, c];
                var cell = table.GetCell(r, c);
                entry.Text = _workbook.GetDisplayText(r, c);
                entry.IsReadOnly = !expressionMode;
                entry.TextColor = cell.Error is not null ? Colors.Firebrick : Colors.Black;
                entry.HorizontalTextAlignment = expressionMode || cell.Error is not null ? TextAlignment.Start : TextAlignment.End;
            }
        }
        ShowSelectedCell();
    }

    private void SelectCell(int row, int column)
    {
        _selected = (row, column);
        ShowSelectedCell();
    }

    private void ShowSelectedCell()
    {
        var cell = _workbook.Table.GetCell(_selected.Row, _selected.Column);
        SelectedCellLabel.Text = cell.Name;
        FormulaEntry.Text = cell.Expression;
        StatusLabel.Text = cell.Error is not null
            ? $"{cell.Name}: {cell.Error}"
            : $"{cell.Name} = {(cell.IsEmpty ? "порожня" : cell.DisplayValue)}   |   {_workbook.CurrentLocation ?? "файл не збережено"}";
    }

    private void CommitCellEntry(int row, int column, Entry entry)
    {
        if (_workbook.Mode != DisplayMode.Expression) return;
        ApplyExpression(row, column, entry.Text);
    }

    private void ApplyExpression(int row, int column, string? text)
    {
        _workbook.SetExpression(row, column, text ?? string.Empty);
        RefreshCells();
    }

    private void UpdateTitle()
    {
        Title = _workbook.Title;
        if (Window is not null)
            Window.Title = $"{_workbook.Title} — Електронна таблиця (ЛР1*)";
    }

    // ---------- Обробники команд ----------

    private void OnFormulaCompleted(object? sender, EventArgs e)
        => ApplyExpression(_selected.Row, _selected.Column, FormulaEntry.Text);

    private void OnModeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        _workbook.Mode = ExpressionModeRadio.IsChecked ? DisplayMode.Expression : DisplayMode.Value;
        RefreshCells();
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        _workbook.New();
    }

    private void OnAddRowClicked(object? sender, EventArgs e) => RunSafe(() => { _workbook.AddRow(); BuildGrid(); });

    private void OnAddColumnClicked(object? sender, EventArgs e) => RunSafe(() => { _workbook.AddColumn(); BuildGrid(); });

    private async void OnRemoveRowClicked(object? sender, EventArgs e)
        => await RemoveAsync(_workbook.Table.GetDependentsOfLastRow(), "рядок", () => _workbook.RemoveLastRow());

    private async void OnRemoveColumnClicked(object? sender, EventArgs e)
        => await RemoveAsync(_workbook.Table.GetDependentsOfLastColumn(), "стовпець", () => _workbook.RemoveLastColumn());

    private async Task RemoveAsync(IReadOnlyList<string> dependents, string what, Action remove)
    {
        if (dependents.Count > 0)
        {
            var ok = await DisplayAlert("Попередження",
                $"На {what}, що видаляється, посилаються клітинки: {string.Join(", ", dependents)}.\n" +
                "Після видалення в них з'явиться помилка. Продовжити?", "Так", "Ні");
            if (!ok) return;
        }
        RunSafe(() => { remove(); BuildGrid(); });
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await RunSafeAsync(async () =>
        {
            if (!await _workbook.SaveAsync())
                await SaveAsAsync(_localStorage);
            else
                StatusLabel.Text = "Збережено.";
        });
    }

    private async void OnSaveAsLocalClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => SaveAsAsync(_localStorage));

    private async void OnSaveDriveClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => SaveAsAsync(_driveStorage));

    private async void OnOpenLocalClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => OpenFromAsync(_localStorage));

    private async void OnOpenDriveClicked(object? sender, EventArgs e)
        => await RunSafeAsync(() => OpenFromAsync(_driveStorage));

    private async Task SaveAsAsync(ISpreadsheetStorage storage)
    {
        var name = await DisplayPromptAsync("Збереження", $"Назва файлу ({storage.DisplayName}):", "Зберегти", "Скасувати",
            initialValue: "table");
        if (string.IsNullOrWhiteSpace(name)) return;
        StatusLabel.Text = $"Збереження ({storage.DisplayName})…";
        await _workbook.SaveAsAsync(storage, name);
        StatusLabel.Text = $"Збережено: {_workbook.Title} ({storage.DisplayName})";
    }

    private async Task OpenFromAsync(ISpreadsheetStorage storage)
    {
        if (!await ConfirmDiscardAsync()) return;
        StatusLabel.Text = $"Отримання списку файлів ({storage.DisplayName})…";
        var files = await storage.ListAsync();
        if (files.Count == 0)
        {
            await DisplayAlert("Відкриття", $"У сховищі \"{storage.DisplayName}\" немає збережених таблиць.", "OK");
            ShowSelectedCell();
            return;
        }

        var choice = await DisplayActionSheet($"Відкрити ({storage.DisplayName})", "Скасувати", null,
            files.Select(f => f.Name).ToArray());
        var file = files.FirstOrDefault(f => f.Name == choice);
        if (file is null) { ShowSelectedCell(); return; }

        await _workbook.OpenAsync(storage, file);
        StatusLabel.Text = $"Відкрито: {file.Name}";
    }

    private async void OnHelpClicked(object? sender, EventArgs e)
    {
        await DisplayAlert("Довідка",
            "Електронна таблиця. Лабораторна робота №1*, варіант 6.\n\n" +
            "Клітинка містить вираз з цілих чисел, круглих дужок, посилань на клітинки (A1, B12) та операцій:\n" +
            "  +  -  *  /   (бінарні)\n" +
            "  mod, div    (остача і цілочисельне ділення)\n" +
            "  ^           (піднесення у степінь)\n" +
            "  max(x, y), min(x, y)\n\n" +
            "Режим \"Вираз\" показує і дозволяє редагувати вирази, режим \"Значення\" показує результати. " +
            "Вираз вибраної клітинки завжди можна змінити в рядку формул.\n\n" +
            "Порожня клітинка вважається нулем. Помилки (синтаксис, ділення на нуль, циклічні посилання) " +
            "позначаються як #ПОМИЛКА, а пояснення видно в рядку стану.\n\n" +
            "Таблиці зберігаються у форматі JSON локально (Документи\\Lab1Spreadsheets) або на Google Drive " +
            "(папка Lab1Spreadsheets).", "OK");
    }

    private async void OnExitClicked(object? sender, EventArgs e)
    {
        if (await ConfirmExitAsync())
            Application.Current?.Quit();
    }

    // ---------- Допоміжне ----------

    public async Task<bool> ConfirmExitAsync()
    {
        var message = _workbook.HasUnsavedChanges
            ? "Є незбережені зміни. Ви дійсно хочете завершити роботу з програмою?"
            : "Ви дійсно хочете завершити роботу з програмою?";
        return await DisplayAlert("Вихід", message, "Так", "Ні");
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!_workbook.HasUnsavedChanges) return true;
        return await DisplayAlert("Незбережені зміни", "Поточну таблицю не збережено. Продовжити без збереження?", "Так", "Ні");
    }

    private async void RunSafe(Action action)
    {
        try { action(); }
        catch (Exception ex) { await DisplayAlert("Помилка", ex.Message, "OK"); }
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            ShowSelectedCell();
            await DisplayAlert("Помилка", ex.Message, "OK");
        }
    }
}
