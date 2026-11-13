using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Spreadsheet.Core.Model;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Spreadsheet.Core.Storage;

/// <summary>
/// Збереження таблиць на Google Drive у папку "Lab1Spreadsheets".
/// Авторизація OAuth 2.0 (desktop): потрібен файл credentials.json з Google Cloud Console.
/// </summary>
public class GoogleDriveStorage : ISpreadsheetStorage
{
    private const string FolderName = "Lab1Spreadsheets";
    private const string MimeType = "application/json";

    private readonly string _credentialsPath;
    private readonly string _tokenFolder;
    private DriveService? _service;
    private string? _folderId;

    public GoogleDriveStorage(string credentialsPath, string tokenFolder)
    {
        _credentialsPath = credentialsPath;
        _tokenFolder = tokenFolder;
    }

    public string DisplayName => "Google Drive";

    public bool HasCredentials => File.Exists(_credentialsPath);

    private async Task<DriveService> GetServiceAsync(CancellationToken ct)
    {
        if (_service is not null) return _service;
        if (!HasCredentials)
            throw new FileNotFoundException(
                $"Не знайдено файл OAuth-клієнта Google: {_credentialsPath}. Див. README, розділ \"Google Drive\".");

        await using var stream = File.OpenRead(_credentialsPath);
        var secrets = (await GoogleClientSecrets.FromStreamAsync(stream, ct)).Secrets;
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { DriveService.Scope.DriveFile },
            "user",
            ct,
            new FileDataStore(_tokenFolder, true));

        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Lab1 Spreadsheet"
        });
        return _service;
    }

    private async Task<string> GetFolderIdAsync(DriveService service, CancellationToken ct)
    {
        if (_folderId is not null) return _folderId;

        var list = service.Files.List();
        list.Q = $"mimeType='application/vnd.google-apps.folder' and name='{FolderName}' and trashed=false";
        list.Fields = "files(id,name)";
        var found = await list.ExecuteAsync(ct);
        if (found.Files.Count > 0)
            return _folderId = found.Files[0].Id;

        var create = service.Files.Create(new DriveFile
        {
            Name = FolderName,
            MimeType = "application/vnd.google-apps.folder"
        });
        create.Fields = "id";
        return _folderId = (await create.ExecuteAsync(ct)).Id;
    }

    public async Task<IReadOnlyList<StoredFileInfo>> ListAsync(CancellationToken ct = default)
    {
        var service = await GetServiceAsync(ct);
        var folderId = await GetFolderIdAsync(service, ct);
        var list = service.Files.List();
        list.Q = $"'{folderId}' in parents and trashed=false";
        list.Fields = "files(id,name,modifiedTimeDateTimeOffset)";
        list.OrderBy = "modifiedTime desc";
        var result = await list.ExecuteAsync(ct);
        return result.Files.Select(f => new StoredFileInfo(f.Id, f.Name, f.ModifiedTimeDateTimeOffset)).ToList();
    }

    public async Task<StoredFileInfo> SaveAsync(string name, SpreadsheetDocument document, string? existingId = null, CancellationToken ct = default)
    {
        var service = await GetServiceAsync(ct);
        var folderId = await GetFolderIdAsync(service, ct);
        var fileName = LocalFileStorage.EnsureExtension(name);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(SpreadsheetSerializer.Serialize(document)));

        if (existingId is not null)
        {
            var update = service.Files.Update(new DriveFile { Name = fileName }, existingId, content, MimeType);
            update.Fields = "id,name";
            await update.UploadAsync(ct);
            return new StoredFileInfo(update.ResponseBody.Id, update.ResponseBody.Name, DateTimeOffset.Now);
        }

        var upload = service.Files.Create(new DriveFile { Name = fileName, Parents = new[] { folderId } }, content, MimeType);
        upload.Fields = "id,name";
        var progress = await upload.UploadAsync(ct);
        if (progress.Exception is not null) throw progress.Exception;
        return new StoredFileInfo(upload.ResponseBody.Id, upload.ResponseBody.Name, DateTimeOffset.Now);
    }

    public async Task<SpreadsheetDocument> LoadAsync(string id, CancellationToken ct = default)
    {
        var service = await GetServiceAsync(ct);
        using var stream = new MemoryStream();
        await service.Files.Get(id).DownloadAsync(stream, ct);
        return SpreadsheetSerializer.Deserialize(Encoding.UTF8.GetString(stream.ToArray()));
    }
}
