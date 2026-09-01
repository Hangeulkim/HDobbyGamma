using Microsoft.Win32;

namespace GammaControl;

internal sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Product-specific ownership prevents disabling this app from deleting another program's entry.
    internal const string RunValueName = "HDobbyGamma";
    private readonly string _executablePath;

    internal StartupManager(string? executablePath = null)
    {
        _executablePath = Path.GetFullPath(
            executablePath ?? Environment.ProcessPath ?? Application.ExecutablePath);
    }

    internal static string BuildCommand(string executablePath) =>
        $"\"{Path.GetFullPath(executablePath)}\" --startup";

    internal void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true) ??
                        throw new InvalidOperationException("Could not open the current-user startup registry key.");

        if (!enabled)
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
            return;
        }

        var expected = BuildCommand(_executablePath);
        var current = key.GetValue(RunValueName) as string;
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
            key.SetValue(RunValueName, expected, RegistryValueKind.String);
        }
    }
}
