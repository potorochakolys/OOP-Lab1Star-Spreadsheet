using System.Text.Json;
using Spreadsheet.Core.Model;

namespace Spreadsheet.Core.Storage;

/// <summary>Інформація про збережену таблицю (локальний файл або файл на Google Drive).</summary>
public record StoredFileInfo(string Id, string Name, DateTimeOffset? Modified);

/// <summary>Сховище таблиць. Реалізації: локальні файли та Google Drive.</summary>
public interface ISpreadsheetStorage
{
    string DisplayName { get; }
    Task<IReadOnlyList<StoredFileInfo>> ListAsync(CancellationToken ct = default);
    Task<StoredFileInfo> SaveAsync(string name, SpreadsheetDocument document, string? existingId = null, CancellationToken ct = default);
    Task<SpreadsheetDocument> LoadAsync(string id, CancellationToken ct = default);
}

public static class SpreadsheetSerializer
{
    public const string Extension = ".sheet.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(SpreadsheetDocument document) => JsonSerializer.Serialize(document, Options);

    public static SpreadsheetDocument Deserialize(string json)
    {
        var doc = JsonSerializer.Deserialize<SpreadsheetDocument>(json, Options)
                  ?? throw new InvalidDataException("Порожній файл таблиці");
        if (doc.Format != "lab1-spreadsheet")
            throw new InvalidDataException("Файл не є таблицею цієї програми");
        return doc;
    }
}
