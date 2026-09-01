using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;

namespace GammaControl.Tests;

internal static class Program
{
    private static int _passed;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            TestGammaMath();
            TestLocalizationAndStartupCommand();
            TestSettingsRoundTripAndCorruption();
            TestDisplayEnumerationReadOnly();
            TestRuntimeLanguageSwitch();
            TestTrayLifecycle();

            if (args.Contains("--native-apply", StringComparer.OrdinalIgnoreCase))
            {
                TestNativeApplyAndRestore();
            }

            var registryArgument = Array.FindIndex(args, argument =>
                string.Equals(argument, "--registry-integration", StringComparison.OrdinalIgnoreCase));
            if (registryArgument >= 0)
            {
                if (registryArgument + 1 >= args.Length)
                {
                    throw new ArgumentException("--registry-integration requires the final executable path.");
                }

                TestRegistryIntegration(args[registryArgument + 1]);
            }

            Console.WriteLine($"PASS: {_passed} checks");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void TestLocalizationAndStartupCommand()
    {
        Assert(UiText.HasCompleteCatalog(), "Korean and English catalogs are complete");
        Assert(UiText.NormalizeLanguageCode("ko") == "ko", "Korean language code");
        Assert(UiText.NormalizeLanguageCode("EN") == "en", "English language code");
        Assert(
            new[] { "ko", "en" }.Contains(UiText.NormalizeLanguageCode("unsupported")),
            "Unsupported language falls back to Korean or English");

        UiText.CurrentLanguage = AppLanguage.Korean;
        Assert(UiText.Get(TextId.MonitorSection).Contains("모니터"), "Korean catalog lookup");
        UiText.CurrentLanguage = AppLanguage.English;
        Assert(UiText.Get(TextId.MonitorSection).Contains("monitor", StringComparison.OrdinalIgnoreCase),
            "English catalog lookup");

        var startupPath = @"C:\Program Files\HDobby Gamma\HDobbyGamma.exe";
        Assert(
            StartupManager.BuildCommand(startupPath) ==
            "\"C:\\Program Files\\HDobby Gamma\\HDobbyGamma.exe\" --startup",
            "Startup command quotes paths and starts hidden");
    }

    private static void TestGammaMath()
    {
        var identity = GammaRampBuilder.Build(1.0);
        Assert(identity.Values.Length == 768, "Ramp length");
        for (var index = 0; index < 256; index++)
        {
            Assert(identity.Values[index] == index * 257, $"Identity ramp at {index}");
            Assert(identity.Values[index] == identity.Values[index + 256], "RGB green equality");
            Assert(identity.Values[index] == identity.Values[index + 512], "RGB blue equality");
        }

        foreach (var gamma in new[] { 0.2, 0.5, 0.8, 1.2, 2.0, 3.0, 5.0 })
        {
            var ramp = GammaRampBuilder.Build(gamma);
            Assert(ramp.Values[0] == 0, $"Gamma {gamma} starts at zero");
            Assert(ramp.Values[255] == ushort.MaxValue, $"Gamma {gamma} ends at max");
            for (var index = 1; index < 256; index++)
            {
                Assert(ramp.Values[index] >= ramp.Values[index - 1], $"Gamma {gamma} monotonic");
            }
            for (var index = 0; index < 256; index++)
            {
                Assert(
                    Math.Abs(ramp.Values[index] - (index * 257)) <=
                    GammaRampBuilder.MaximumIdentityDeviation,
                    $"Gamma {gamma} stays inside the Windows safety envelope");
            }
        }

        Assert(GammaRampBuilder.Build(2.0).Values[128] > identity.Values[128], "Gamma > 1 brightens midtones");
        Assert(GammaRampBuilder.Build(0.5).Values[128] < identity.Values[128], "Gamma < 1 darkens midtones");
        AssertThrows(() => GammaRampBuilder.Build(0.19), "Gamma below minimum rejected");
        AssertThrows(() => GammaRampBuilder.Build(double.NaN), "Gamma NaN rejected");
        AssertThrows(() => GammaRampBuilder.Build(5.01), "Gamma above maximum rejected");

        Assert(GammaRampBuilder.ToSliderPosition(0.2) == 0, "Slider minimum mapping");
        Assert(GammaRampBuilder.ToSliderPosition(1.0) == 500, "Slider neutral is centered");
        Assert(GammaRampBuilder.ToSliderPosition(5.0) == 1000, "Slider maximum mapping");
        foreach (var gamma in new[] { 0.2, 0.35, 0.8, 1.0, 1.4, 3.2, 5.0 })
        {
            var roundTrip = GammaRampBuilder.FromSliderPosition(GammaRampBuilder.ToSliderPosition(gamma));
            Assert(Math.Abs(roundTrip - gamma) / gamma < 0.003, $"Slider round trip {gamma}");
        }
    }

    private static void TestSettingsRoundTripAndCorruption()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GammaControlTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AppSettings(directory);
            var initial = store.Load(out var initialWarning);
            Assert(initialWarning == SettingsLoadWarning.None, "First load has no warning");
            Assert(initial.RestoreOnExit, "Restore on exit defaults true");
            Assert(new[] { "ko", "en" }.Contains(initial.Language), "Default language is supported");
            Assert(!initial.StartWithWindows, "Start with Windows defaults false");

            initial.SelectedDevice = @"monitor:\\?\DISPLAY#MONITOR_B";
            initial.Language = "en";
            initial.StartWithWindows = true;
            initial.GammaByDevice[@"monitor:\\?\DISPLAY#MONITOR_A"] = 1.25;
            initial.GammaByDevice[@"monitor:\\?\DISPLAY#MONITOR_B"] = 0.80;
            store.Save(initial);

            var loaded = store.Load(out var loadWarning);
            Assert(loadWarning == SettingsLoadWarning.None, "Valid settings load");
            Assert(loaded.SelectedDevice == @"monitor:\\?\DISPLAY#MONITOR_B", "Selected monitor round trip");
            Assert(Math.Abs(loaded.GammaByDevice[@"monitor:\\?\DISPLAY#MONITOR_A"] - 1.25) < 0.0001,
                "Gamma round trip");
            Assert(loaded.Language == "en", "Language round trip");
            Assert(loaded.StartWithWindows, "Start with Windows round trip");
            Assert(loaded.SchemaVersion == 3, "Settings schema upgraded to version 3");

            File.WriteAllText(
                store.PathForDiagnostics,
                "{\"SchemaVersion\":1,\"RestoreOnExit\":false," +
                "\"SelectedDevice\":\"\\\\\\\\.\\\\DISPLAY1\"," +
                "\"GammaByDevice\":{\"\\\\\\\\.\\\\DISPLAY1\":1.4}}");
            var migrated = store.Load(out var migrationWarning);
            Assert(migrationWarning == SettingsLoadWarning.None, "Version 1 settings migrate without warning");
            Assert(migrated.SchemaVersion == 3, "Version 1 settings migrate to current schema");
            Assert(!migrated.RestoreOnExit, "Version 1 restore preference preserved");
            Assert(Math.Abs(migrated.GammaByDevice[@"\\.\DISPLAY1"] - 1.4) < 0.0001,
                "Version 1 gamma preserved");
            Assert(new[] { "ko", "en" }.Contains(migrated.Language), "Version 1 receives supported language");

            File.WriteAllText(store.PathForDiagnostics, "{broken json");
            var recovered = store.Load(out var corruptionWarning);
            Assert(corruptionWarning == SettingsLoadWarning.Corrupted, "Corruption warning");
            Assert(recovered.GammaByDevice.Count == 0, "Corrupt settings fall back safely");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestDisplayEnumerationReadOnly()
    {
        Assert(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigPathInfo>() == 72,
            "DISPLAYCONFIG_PATH_INFO interop size");
        Assert(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigModeInfo>() == 64,
            "DISPLAYCONFIG_MODE_INFO interop size");
        Assert(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>() == 84,
            "DISPLAYCONFIG_SOURCE_DEVICE_NAME interop size");
        Assert(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>() == 420,
            "DISPLAYCONFIG_TARGET_DEVICE_NAME interop size");
        using var service = new MonitorGammaService();
        var targets = service.Refresh();
        Assert(targets.Count > 0, "At least one display found");
        Assert(targets.Select(target => target.DeviceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == targets.Count,
            "Display identifiers are unique");
        Assert(targets.All(target => !string.IsNullOrWhiteSpace(target.SettingsKey)),
            "Every display has a persistent settings key");
        Assert(targets.Select(target => target.SettingsKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() == targets.Count,
            "Persistent settings keys are unique");

        foreach (var target in targets)
        {
            Console.WriteLine(
                $"DISPLAY: name={target.DeviceName}, settingsKey={target.SettingsKey}, " +
                $"gammaSupported={target.SupportsGamma}, readError={target.ReadError ?? "none"}");
            if (!target.SupportsGamma)
            {
                continue;
            }

            var ramp = service.ReadCurrentRamp(target.DeviceName, out var error);
            Assert(ramp != null && error == null, "Supported display ramp can be read");
            Assert(ramp!.Values.Length == GammaRamp.TotalLength, "Native ramp size");
        }
    }

    private static void TestTrayLifecycle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GammaControlTrayTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var form = new MainForm(
                       previewOnly: false,
                       settingsDirectory: directory,
                       manageStartup: false))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-10000, -10000);
                form.Opacity = 0;
                form.Show();
                Application.DoEvents();
                form.Close();
                Application.DoEvents();

                Assert(!form.IsDisposed, "Window close keeps the application running");
                Assert(!form.Visible && !form.ShowInTaskbar, "Window close hides the application in the tray");

                form.TrayIconOnDoubleClick(null, EventArgs.Empty);
                Application.DoEvents();
                Assert(form.Visible && form.ShowInTaskbar, "Tray double click restores the window");
                Assert(IsWindowVisibleOnScreen(form.Bounds),
                    "Tray double click restores the window inside a visible work area");

                form.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                Assert(!form.Visible && !form.ShowInTaskbar, "Minimize button hides the application in the tray");

                form.RestoreFromTray();
                Application.DoEvents();
                Assert(form.Visible && form.ShowInTaskbar, "Tray Open menu action restores the window");
                Assert(IsWindowVisibleOnScreen(form.Bounds),
                    "Tray Open menu restores the window inside a visible work area");

                form.ExitCompletely();
                Application.DoEvents();
                Assert(form.IsDisposed, "Tray exit completely closes the application");
            }

            using (var startupForm = new MainForm(
                       previewOnly: false,
                       settingsDirectory: directory,
                       startHidden: true,
                       manageStartup: false))
            {
                startupForm.StartPosition = FormStartPosition.Manual;
                startupForm.Location = new Point(-10000, -10000);
                startupForm.Show();
                Application.DoEvents();
                Application.DoEvents();

                Assert(!startupForm.Visible && !startupForm.ShowInTaskbar,
                    "Windows startup launch begins hidden in the tray");

                startupForm.TrayIconOnDoubleClick(null, EventArgs.Empty);
                Application.DoEvents();
                Assert(startupForm.Visible && startupForm.ShowInTaskbar &&
                       startupForm.WindowState == FormWindowState.Normal,
                    "Windows startup tray instance restores without recursive resize");
                Assert(IsWindowVisibleOnScreen(startupForm.Bounds),
                    "Windows startup tray instance restores inside a visible work area");

                startupForm.ExitCompletely();
                Application.DoEvents();
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static bool IsWindowVisibleOnScreen(Rectangle bounds)
    {
        return Screen.AllScreens.Any(screen =>
        {
            var visibleBounds = Rectangle.Intersect(screen.WorkingArea, bounds);
            return visibleBounds.Width >= 80 && visibleBounds.Height >= 48;
        });
    }

    private static void TestRuntimeLanguageSwitch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GammaControlLanguageTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var form = new MainForm(
                previewOnly: true,
                settingsDirectory: directory,
                languageOverride: "ko");
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.Opacity = 0;
            form.Show();
            Application.DoEvents();

            var controls = EnumerateControls(form).ToArray();
            Assert(controls.OfType<Label>().Any(label => label.Text == "적용할 모니터"),
                "Korean UI is rendered");
            var languageCombo = controls.OfType<ComboBox>().Single(combo =>
                combo.Items.Count == 2 &&
                combo.Items.Cast<object>().Any(item => item.ToString() == "한국어") &&
                combo.Items.Cast<object>().Any(item => item.ToString() == "English"));
            var monitorCombo = controls.OfType<ComboBox>().Single(combo => !ReferenceEquals(combo, languageCombo));
            var gammaBefore = controls.OfType<NumericUpDown>().Single().Value;
            var monitorBefore = monitorCombo.SelectedIndex;
            Assert(monitorCombo.SelectedItem?.ToString()?.StartsWith("모든 모니터", StringComparison.Ordinal) == true,
                "Monitor combo starts in Korean");

            languageCombo.SelectedIndex = 1;
            Application.DoEvents();

            Assert(EnumerateControls(form).OfType<Label>().Any(label => label.Text == "Monitor to adjust"),
                "English UI is rendered after switching language");
            Assert(controls.OfType<NumericUpDown>().Single().Value == gammaBefore,
                "Language switch preserves gamma value");
            Assert(monitorCombo.SelectedIndex == monitorBefore,
                "Language switch preserves monitor selection");
            Assert(monitorCombo.SelectedItem?.ToString()?.StartsWith("All monitors", StringComparison.Ordinal) == true,
                "Monitor combo switches to English");

            form.Close();
            Application.DoEvents();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in EnumerateControls(child))
            {
                yield return descendant;
            }
        }
    }

    private static void TestNativeApplyAndRestore()
    {
        using var service = new MonitorGammaService();
        var targets = service.Refresh().Where(item => item.SupportsGamma).ToArray();
        if (targets.Length == 0)
        {
            Console.WriteLine("SKIP: no display exposes a readable gamma ramp");
            return;
        }

        var baselines = targets.ToDictionary(
            target => target.DeviceName,
            target => service.ReadCurrentRamp(target.DeviceName, out _)!,
            StringComparer.OrdinalIgnoreCase);
        for (var targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            var target = targets[targetIndex];
            var original = service.ReadCurrentRamp(target.DeviceName, out var readError);
            Assert(original != null && readError == null, "Capture native baseline");
            if (service.IsLinked(target.DeviceName))
            {
                Console.WriteLine($"NATIVE: target={target.DeviceName}, already identified as shared-LUT; individual write skipped");
                continue;
            }

            var desiredGamma = new[] { 0.90, 1.10 }
                .OrderByDescending(gamma => original!.MaxDifference(GammaRampBuilder.Build(gamma)))
                .First();
            var desiredRamp = GammaRampBuilder.Build(desiredGamma);
            var ownsRamp = false;

            try
            {
                var apply = service.ApplyGamma(target.DeviceName, desiredGamma);
                Assert(apply.FailedDevices.Count == 0, "Native SetDeviceGammaRamp succeeds");
                if (apply.RolledBack)
                {
                    Assert(apply.UnexpectedlyChangedDevices.Count > 0,
                        "Shared LUT reports the other affected displays");
                    Assert(apply.RollbackFailedDevices.Count == 0, "Shared LUT rollback verifies");
                    foreach (var baseline in baselines)
                    {
                        var afterRollback = service.ReadCurrentRamp(baseline.Key, out _);
                        Assert(afterRollback != null &&
                               baseline.Value.MaxDifference(afterRollback) <= MonitorGammaService.ReadbackTolerance,
                            "Shared LUT rollback restores every affected baseline");
                    }
                }
                else
                {
                    ownsRamp = true;
                    Assert(apply.UnverifiedDevices.Count == 0, "Native gamma read-back verifies");
                    Assert(apply.ChangedDevices.Count == 1, "Only the requested display is owned");
                    var appliedRamp = service.ReadCurrentRamp(target.DeviceName, out _);
                    Assert(appliedRamp != null &&
                           desiredRamp.MaxDifference(appliedRamp) <= MonitorGammaService.ReadbackTolerance,
                        "Native read-back is close to the requested ramp");
                    foreach (var other in baselines.Where(item =>
                                 !string.Equals(item.Key, target.DeviceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var otherAfter = service.ReadCurrentRamp(other.Key, out _);
                        Assert(otherAfter != null &&
                               other.Value.MaxDifference(otherAfter) <= MonitorGammaService.LinkedMonitorTolerance,
                            "An independent write leaves every other display unchanged");
                    }
                }
                Console.WriteLine(
                    $"NATIVE: target={target.DeviceName}, linkedDisplays={apply.UnexpectedlyChangedDevices.Count}, " +
                    $"verifiedDisplays={apply.ChangedDevices.Count - apply.UnverifiedDevices.Count}");
            }
            finally
            {
                var restored = service.RestoreOwnedRamps();
                Assert(restored == (ownsRamp ? 1 : 0), "Native ownership restoration count");
                var after = service.ReadCurrentRamp(target.DeviceName, out _);
                Assert(after != null && original!.MaxDifference(after) <= MonitorGammaService.ReadbackTolerance,
                    "Restored ramp read-back");
            }
        }

        var independentTargets = targets.Where(target => !service.IsLinked(target.DeviceName)).ToArray();
        if (independentTargets.Length == 0)
        {
            Console.WriteLine("SKIP: distinct saved gamma reapply is unavailable on a shared-LUT topology");
            return;
        }

        var startupBaselines = independentTargets.ToDictionary(
            target => target.DeviceName,
            target => service.ReadCurrentRamp(target.DeviceName, out _)!,
            StringComparer.OrdinalIgnoreCase);
        try
        {
            var savedGammas = independentTargets.Select((target, index) => new
                {
                    target.SettingsKey,
                    Gamma = index % 2 == 0 ? 0.90 : 1.10
                })
                .ToDictionary(
                item => item.SettingsKey,
                item => item.Gamma,
                StringComparer.OrdinalIgnoreCase);
            var reapplied = service.ReapplySavedGammas(savedGammas);
            Assert(reapplied.FailedDevices.Count == 0, "Saved startup gamma reapply succeeds");
            Assert(reapplied.UnverifiedDevices.Count == 0, "Saved startup gamma read-back verifies");
            Assert(reapplied.ChangedDevices.Count == independentTargets.Length,
                "Saved startup gamma reaches every independent display");
            foreach (var target in independentTargets)
            {
                var readback = service.ReadCurrentRamp(target.DeviceName, out _);
                Assert(readback != null &&
                       GammaRampBuilder.Build(savedGammas[target.SettingsKey]).MaxDifference(readback) <=
                       MonitorGammaService.ReadbackTolerance,
                    "Each display receives its own saved startup value");
            }
        }
        finally
        {
            var restored = service.RestoreOwnedRamps();
            Assert(restored == independentTargets.Length, "Startup reapply baselines restored");
            foreach (var target in independentTargets)
            {
                var after = service.ReadCurrentRamp(target.DeviceName, out _);
                Assert(after != null &&
                       startupBaselines[target.DeviceName].MaxDifference(after) <=
                       MonitorGammaService.ReadbackTolerance,
                    "Startup reapply restoration read-back");
            }
        }
    }

    private static void TestRegistryIntegration(string executablePath)
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        var markerName = "HDobbyGamma.Tests." + Guid.NewGuid().ToString("N");
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true) ??
                        throw new InvalidOperationException("Could not open the current-user Run key.");
        var names = key.GetValueNames();
        var hadOriginal = names.Contains(StartupManager.RunValueName, StringComparer.OrdinalIgnoreCase);
        var original = hadOriginal ? key.GetValue(StartupManager.RunValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) : null;
        var originalKind = hadOriginal ? key.GetValueKind(StartupManager.RunValueName) : RegistryValueKind.String;

        try
        {
            key.SetValue(markerName, "preserve-me", RegistryValueKind.String);
            var manager = new StartupManager(executablePath);
            manager.SetEnabled(true);
            Assert(
                string.Equals(
                    key.GetValue(StartupManager.RunValueName) as string,
                    StartupManager.BuildCommand(executablePath),
                    StringComparison.Ordinal),
                "Startup registry command uses the final executable");
            Assert(string.Equals(key.GetValue(markerName) as string, "preserve-me", StringComparison.Ordinal),
                "Enabling startup preserves unrelated Run values");

            manager.SetEnabled(false);
            Assert(!key.GetValueNames().Contains(StartupManager.RunValueName, StringComparer.OrdinalIgnoreCase),
                "Disabling startup removes only the app-owned value");
            Assert(string.Equals(key.GetValue(markerName) as string, "preserve-me", StringComparison.Ordinal),
                "Disabling startup preserves unrelated Run values");
        }
        finally
        {
            key.DeleteValue(markerName, throwOnMissingValue: false);
            if (hadOriginal)
            {
                key.SetValue(StartupManager.RunValueName, original!, originalKind);
            }
            else
            {
                key.DeleteValue(StartupManager.RunValueName, throwOnMissingValue: false);
            }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Assertion failed: " + name);
        }

        _passed++;
    }

    private static void AssertThrows(Action action, string name)
    {
        try
        {
            action();
        }
        catch (ArgumentOutOfRangeException)
        {
            _passed++;
            return;
        }

        throw new InvalidOperationException("Expected ArgumentOutOfRangeException: " + name);
    }
}
