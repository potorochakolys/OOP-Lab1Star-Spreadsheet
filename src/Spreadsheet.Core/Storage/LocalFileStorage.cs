using Spreadsheet.Core.Model;

namespace Spreadsheet.Core.Storage;

/// <summary>Збереження таблиць у локальну папку.</summary>
public class LocalFileStorage : ISpreadsheetStorage
{
    private readonly string _folder;

    public LocalFileStorage(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public string DisplayName => "Локальний диск";

    public Task<IReadOnlyList<StoredFileInfo>> ListAsync(CancellationToken ct = default)
    {
        IReadOnlyList<StoredFileInfo> files = Directory
            .EnumerateFiles(_folder, "*" + SpreadsheetSerializer.Extension)
            .Select(p => new StoredFileInfo(p, Path.GetFileName(p), File.GetLastWriteTime(p)))
            .OrderByDescending(f => f.Modified)
            .ToList();
        return Task.FromResult(files);
    }

    public async Task<StoredFileInfo> SaveAsync(string name, SpreadsheetDocument document, string? existingId = null, CancellationToken ct = default)
    {
        var path = existingId ?? Path.Combine(_folder, EnsureExtension(name));
        await File.WriteAllTextAsync(path, SpreadsheetSerializer.Serialize(document), ct);
        return new StoredFileInfo(path, Path.GetFileName(path), DateTimeOffset.Now);
    }

    public async Task<SpreadsheetDocument> LoadAsync(string id, CancellationToken ct = default)
        => SpreadsheetSerializer.Deserialize(await File.ReadAllTextAsync(id, ct));

    internal static string EnsureExtension(string name)
    {
        var clean = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrEmpty(clean)) clean = "table";
        return clean.EndsWith(SpreadsheetSerializer.Extension, StringComparison.OrdinalIgnoreCase)
            ? clean
            : clean + SpreadsheetSerializer.Extension;
    }
}
