namespace Spreadsheet.Core.Model;

/// <summary>Формат файлу таблиці (JSON): розмір і вирази непорожніх клітинок.</summary>
public class SpreadsheetDocument
{
    public string Format { get; set; } = "lab1-spreadsheet";
    public int Version { get; set; } = 1;
    public int Rows { get; set; }
    public int Columns { get; set; }
    public Dictionary<string, string> Cells { get; set; } = new();
}
