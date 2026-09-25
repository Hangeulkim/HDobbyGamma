using System.Drawing.Imaging;

namespace GammaControl;

internal static class Program
{
    private sealed record LaunchOptions(string? RenderPath, string? Language, bool StartHidden, int RenderTab);

    private static MainForm? _mainForm;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var options = ParseOptions(args);

        if (options.RenderPath != null)
        {
            return RenderPreview(options.RenderPath, options.Language, options.RenderTab);
        }

        var earlySettings = new AppSettings().Load(out _);
        UiText.CurrentLanguage = UiText.ParseLanguage(options.Language ?? earlySettings.Language);

        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: @"Local\HDobbyGamma.SingleInstance",
            createdNew: out var isFirstInstance);
        var ownsMutex = isFirstInstance;
        if (!ownsMutex)
        {
            try
            {
                ownsMutex = singleInstance.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous process died between opening the mutex and releasing it.
                // WaitOne grants ownership in this case, so continue as the replacement.
                ownsMutex = true;
            }
        }

        using var showWindowSignal = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: @"Local\HDobbyGamma.ShowWindow");
        using var showWindowAcknowledged = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: @"Local\HDobbyGamma.ShowWindowAck");
        if (!ownsMutex)
        {
            // A duplicate auto-start must not unexpectedly pop up an already-running window.
            if (!options.StartHidden)
            {
                showWindowAcknowledged.Reset();
                showWindowSignal.Set();
                if (!showWindowAcknowledged.WaitOne(TimeSpan.FromSeconds(5)))
                {
                    MessageBox.Show(
                        UiText.Get(TextId.AlreadyRunning),
                        UiText.Get(TextId.WindowTitle),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            return 0;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, exceptionArgs) =>
        {
            _mainForm?.RestoreIfRequested();
            MessageBox.Show(
                UiText.Format(TextId.UnexpectedError, exceptionArgs.Exception.Message),
                UiText.Get(TextId.WindowTitle),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Application.Exit();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _mainForm?.RestoreIfRequested();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => _mainForm?.RestoreIfRequested();

        _mainForm = new MainForm(
            languageOverride: options.Language,
            startHidden: options.StartHidden);
        _ = _mainForm.Handle;
        var uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        var showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
            showWindowSignal,
            (_, _) =>
            {
                var form = _mainForm;
                if (form == null || form.IsDisposed)
                {
                    return;
                }

                try
                {
                    uiContext.Post(_ =>
                    {
                        var liveForm = _mainForm;
                        if (liveForm == null || liveForm.IsDisposed)
                        {
                            return;
                        }

                        liveForm.RestoreFromTray();
                        showWindowAcknowledged.Set();
                    }, null);
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                    System.ComponentModel.InvalidAsynchronousStateException)
                {
                    // The UI message loop closed while the cross-instance request was being delivered.
                }
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        try
        {
            Application.Run(_mainForm);
            return 0;
        }
        finally
        {
            showWindowRegistration.Unregister(null);
            if (ownsMutex)
            {
                singleInstance.ReleaseMutex();
            }
        }
    }

    private static LaunchOptions ParseOptions(IReadOnlyList<string> args)
    {
        string? renderPath = null;
        string? language = null;
        var startHidden = false;
        var renderTab = 0;

        for (var index = 0; index < args.Count; index++)
        {
            if (string.Equals(args[index], "--render", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                renderPath = args[++index];
            }
            else if (string.Equals(args[index], "--language", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                language = UiText.NormalizeLanguageCode(args[++index]);
            }
            else if (string.Equals(args[index], "--startup", StringComparison.OrdinalIgnoreCase))
            {
                startHidden = true;
            }
            else if (string.Equals(args[index], "--tab", StringComparison.OrdinalIgnoreCase) &&
                     index + 1 < args.Count && int.TryParse(args[index + 1], out var requestedTab))
            {
                renderTab = requestedTab;
                index++;
            }
        }

        return new LaunchOptions(renderPath, language, startHidden, renderTab);
    }

    private static int RenderPreview(string outputPath, string? language, int tab)
    {
        var settingsDirectory = Path.Combine(
            Path.GetTempPath(),
            "HDobbyGamma-Preview-" + UiText.NormalizeLanguageCode(language));
        using var form = new MainForm(
            previewOnly: true,
            settingsDirectory,
            languageOverride: language);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = Point.Empty;
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        form.Show();
        form.SelectPreviewTab(tab);
        Application.DoEvents();
        form.PerformLayout();
        Application.DoEvents();

        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, ImageFormat.Png);
        form.Close();
        return 0;
    }
}
