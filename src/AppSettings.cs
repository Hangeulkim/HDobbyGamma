using System.Text.Json;

namespace GammaControl;

internal sealed class AppSettingsData
{
    public int SchemaVersion { get; set; } = 4;
    public string Language { get; set; } = UiText.DefaultLanguageCode;
    public bool RestoreOnExit { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ConfineCursor { get; set; }
    public string TargetExecutablePath { get; set; } = string.Empty;
    public string SelectedDevice { get; set; } = AppSettings.AllDisplaysKey;
    public Dictionary<string, double> GammaByDevice { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal enum SettingsLoadWarning
{
    None,
    Corrupted
}

internal sealed class AppSettings
{
    internal const string AllDisplaysKey = "__ALL_DISPLAYS__";
    private readonly string _directory;
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    internal AppSettings(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HDobbyGamma");
        _path = Path.Combine(_directory, "settings.json");
    }

    internal string PathForDiagnostics => _path;

    internal AppSettingsData Load(out SettingsLoadWarning warning)
    {
        warning = SettingsLoadWarning.None;
        if (!File.Exists(_path))
        {
            return new AppSettingsData();
        }

        try
        {
            var data = JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(_path), _jsonOptions) ??
                       new AppSettingsData();
            Normalize(data);
            return data;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = SettingsLoadWarning.Corrupted;
            return new AppSettingsData();
        }
    }

    internal void Save(AppSettingsData data)
    {
        Normalize(data);
        Directory.CreateDirectory(_directory);
        var tempPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(tempPath, json);

        try
        {
            File.Move(tempPath, _path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void Normalize(AppSettingsData data)
    {
        data.SchemaVersion = 4;
        data.Language = UiText.NormalizeLanguageCode(data.Language);
        data.SelectedDevice = string.IsNullOrWhiteSpace(data.SelectedDevice)
            ? AllDisplaysKey
            : data.SelectedDevice;
        data.TargetExecutablePath ??= string.Empty;
        data.GammaByDevice ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var cleaned = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in data.GammaByDevice)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || double.IsNaN(item.Value) || double.IsInfinity(item.Value))
            {
                continue;
            }

            cleaned[item.Key] = Math.Clamp(
                item.Value,
                GammaRampBuilder.MinimumGamma,
                GammaRampBuilder.MaximumGamma);
        }

        data.GammaByDevice = cleaned;
    }
}
