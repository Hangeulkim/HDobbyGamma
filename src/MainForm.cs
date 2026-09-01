using Microsoft.Win32;
using System.Drawing;
using System.Globalization;

namespace GammaControl;

internal sealed class MainForm : Form
{
    private sealed class AllDisplaysItem
    {
        internal AllDisplaysItem(int count)
        {
            Count = count;
        }

        internal int Count { get; }
        public override string ToString() => UiText.Format(TextId.AllDisplays, Count);
    }

    private sealed class LanguageItem
    {
        internal LanguageItem(AppLanguage language, string label)
        {
            Language = language;
            Label = label;
        }

        internal AppLanguage Language { get; }
        private string Label { get; }
        public override string ToString() => Label;
    }

    private readonly MonitorGammaService _gammaService;
    private readonly AppSettings _settingsStore;
    private readonly AppSettingsData _settings;
    private readonly bool _previewOnly;
    private readonly bool _startHidden;
    private readonly bool _manageStartup;
    private readonly StartupManager _startupManager;
    private readonly ComboBox _monitorCombo;
    private readonly ComboBox _languageCombo;
    private readonly TrackBar _gammaSlider;
    private readonly NumericUpDown _gammaNumber;
    private readonly Label _gammaValueLabel;
    private readonly Label _statusLabel;
    private readonly Label _monitorDetailLabel;
    private readonly CheckBox _restoreOnExitCheck;
    private readonly CheckBox _startWithWindowsCheck;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _trayOpenItem;
    private readonly ToolStripMenuItem _trayExitItem;
    private readonly System.Windows.Forms.Timer _applyTimer;
    private readonly System.Windows.Forms.Timer _saveTimer;
    private readonly System.Windows.Forms.Timer _displayRefreshTimer;
    private bool _suppressInput;
    private bool _restored;
    private bool _allowExit;
    private bool _trayNoticeShown;
    private bool _changingTrayVisibility;
    private double _pendingGamma = 1.0;
    private string? _pendingDeviceName;
    private bool _hasPendingApply;

    private Label _headerTitleLabel = null!;
    private Label _headerSubtitleLabel = null!;
    private Label _languageLabel = null!;
    private Label _monitorSectionLabel = null!;
    private Label _scaleDarkLabel = null!;
    private Label _scaleNeutralLabel = null!;
    private Label _scaleBrightLabel = null!;
    private Button _reapplyButton = null!;
    private Button _resetSelectedButton = null!;
    private Button _resetAllButton = null!;
    private Label _caveatLabel = null!;
    private Label _shortcutLabel = null!;

    internal MainForm(
        bool previewOnly = false,
        string? settingsDirectory = null,
        string? languageOverride = null,
        bool startHidden = false,
        bool manageStartup = true)
    {
        _previewOnly = previewOnly;
        _startHidden = startHidden && !previewOnly;
        _manageStartup = manageStartup && !previewOnly;
        _gammaService = new MonitorGammaService();
        _settingsStore = new AppSettings(settingsDirectory);
        _settings = _settingsStore.Load(out var settingsWarning);
        if (!string.IsNullOrWhiteSpace(languageOverride))
        {
            _settings.Language = UiText.NormalizeLanguageCode(languageOverride);
        }
        UiText.CurrentLanguage = UiText.ParseLanguage(_settings.Language);
        _startupManager = new StartupManager();

        Text = UiText.Get(TextId.WindowTitle);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 620);
        ClientSize = new Size(720, 650);
        BackColor = Color.FromArgb(245, 247, 250);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        KeyPreview = true;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("HDobbyGamma.Icon"))
        {
            if (iconStream != null)
            {
                using var embeddedIcon = new Icon(iconStream);
                Icon = (Icon)embeddedIcon.Clone();
            }
        }
        if (_startHidden)
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 20, 24, 18),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _languageCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 112,
            Height = 32,
            Margin = new Padding(0, 3, 0, 0)
        };
        _languageCombo.Items.Add(new LanguageItem(AppLanguage.Korean, "한국어"));
        _languageCombo.Items.Add(new LanguageItem(AppLanguage.English, "English"));
        _languageCombo.SelectedIndex = UiText.CurrentLanguage == AppLanguage.Korean ? 0 : 1;
        _languageCombo.SelectedIndexChanged += LanguageComboOnSelectedIndexChanged;

        var header = BuildHeader();
        root.Controls.Add(header, 0, 0);

        var card = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(22, 18, 22, 18),
            Margin = new Padding(0, 18, 0, 12),
            BackColor = Color.White
        };
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(card, 0, 1);

        _monitorSectionLabel = NewSectionLabel(UiText.Get(TextId.MonitorSection));
        card.Controls.Add(_monitorSectionLabel, 0, 0);
        _monitorCombo = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 34,
            Margin = new Padding(0, 7, 0, 0),
            AccessibleName = UiText.Get(TextId.MonitorAccessible)
        };
        _monitorCombo.SelectedIndexChanged += MonitorComboOnSelectedIndexChanged;
        card.Controls.Add(_monitorCombo, 0, 1);

        _monitorDetailLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(102, 112, 133),
            Margin = new Padding(1, 5, 0, 12),
            Text = UiText.Get(TextId.CheckingMonitors)
        };
        card.Controls.Add(_monitorDetailLabel, 0, 2);

        var valueRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 1, 0, 0)
        };
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        valueRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        _gammaValueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "1.00×",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 22F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(32, 39, 55)
        };
        _gammaNumber = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = (decimal)GammaRampBuilder.MinimumGamma,
            Maximum = (decimal)GammaRampBuilder.MaximumGamma,
            Increment = 0.05M,
            Value = 1.00M,
            Dock = DockStyle.Fill,
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point),
            AccessibleName = UiText.Get(TextId.GammaInputAccessible)
        };
        _gammaNumber.ValueChanged += GammaNumberOnValueChanged;
        valueRow.Controls.Add(_gammaValueLabel, 0, 0);
        valueRow.Controls.Add(_gammaNumber, 1, 0);
        card.Controls.Add(valueRow, 0, 3);

        _gammaSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 1000,
            TickFrequency = 100,
            SmallChange = 5,
            LargeChange = 25,
            Value = 500,
            Margin = new Padding(0, 0, 0, 2),
            AccessibleName = UiText.Get(TextId.GammaSliderAccessible)
        };
        _gammaSlider.ValueChanged += GammaSliderOnValueChanged;
        card.Controls.Add(_gammaSlider, 0, 4);

        var scaleRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true };
        scaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        scaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        scaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        _scaleDarkLabel = NewScaleLabel(UiText.Get(TextId.ScaleDark), ContentAlignment.MiddleLeft);
        _scaleNeutralLabel = NewScaleLabel(UiText.Get(TextId.ScaleNeutral), ContentAlignment.MiddleCenter);
        _scaleBrightLabel = NewScaleLabel(UiText.Get(TextId.ScaleBright), ContentAlignment.MiddleRight);
        scaleRow.Controls.Add(_scaleDarkLabel, 0, 0);
        scaleRow.Controls.Add(_scaleNeutralLabel, 1, 0);
        scaleRow.Controls.Add(_scaleBrightLabel, 2, 0);
        card.Controls.Add(scaleRow, 0, 5);

        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 15, 0, 8)
        };
        _reapplyButton = NewButton(UiText.Get(TextId.Reapply), ReapplyButtonOnClick, true);
        _resetSelectedButton = NewButton(UiText.Get(TextId.ResetSelected), ResetSelectedButtonOnClick, false);
        _resetAllButton = NewButton(UiText.Get(TextId.ResetAll), ResetAllButtonOnClick, false);
        buttonRow.Controls.Add(_reapplyButton);
        buttonRow.Controls.Add(_resetSelectedButton);
        buttonRow.Controls.Add(_resetAllButton);
        card.Controls.Add(buttonRow, 0, 6);

        _restoreOnExitCheck = new CheckBox
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 26,
            Text = UiText.Get(TextId.RestoreOnExit),
            Checked = _settings.RestoreOnExit,
            Margin = new Padding(1, 5, 0, 7),
            AccessibleName = UiText.Get(TextId.RestoreAccessible)
        };
        _restoreOnExitCheck.CheckedChanged += RestoreOnExitCheckOnCheckedChanged;
        card.Controls.Add(_restoreOnExitCheck, 0, 7);

        _startWithWindowsCheck = new CheckBox
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 26,
            Text = UiText.Get(TextId.StartWithWindows),
            Checked = _settings.StartWithWindows,
            Margin = new Padding(1, 1, 0, 8),
            AccessibleName = UiText.Get(TextId.StartAccessible)
        };
        _startWithWindowsCheck.CheckedChanged += StartWithWindowsCheckOnCheckedChanged;
        card.Controls.Add(_startWithWindowsCheck, 0, 8);

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            MinimumSize = new Size(0, 58),
            MaximumSize = new Size(620, 0),
            Padding = new Padding(12, 9, 12, 9),
            BackColor = Color.FromArgb(237, 244, 255),
            ForeColor = Color.FromArgb(34, 79, 145),
            Text = settingsWarning == SettingsLoadWarning.Corrupted
                ? UiText.Get(TextId.SettingsCorrupted)
                : UiText.Get(TextId.Ready),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(_statusLabel, 0, 9);

        _caveatLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(620, 0),
            Text = UiText.Get(TextId.Caveat),
            ForeColor = Color.FromArgb(103, 112, 128),
            Margin = new Padding(2, 0, 0, 4)
        };
        root.Controls.Add(_caveatLabel, 0, 2);

        _shortcutLabel = new Label
        {
            AutoSize = true,
            Text = UiText.Get(TextId.Shortcut),
            ForeColor = Color.FromArgb(125, 132, 146),
            Margin = new Padding(2, 0, 0, 0)
        };
        root.Controls.Add(_shortcutLabel, 0, 3);

        _trayMenu = new ContextMenuStrip();
        _trayOpenItem = new ToolStripMenuItem(UiText.Get(TextId.TrayOpen), null, (_, _) => RestoreFromTray());
        _trayExitItem = new ToolStripMenuItem(UiText.Get(TextId.TrayExit), null, (_, _) => ExitCompletely());
        _trayMenu.Items.Add(_trayOpenItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_trayExitItem);
        _notifyIcon = new NotifyIcon
        {
            Icon = Icon == null ? SystemIcons.Application : (Icon)Icon.Clone(),
            Text = UiText.Get(TextId.WindowTitle),
            ContextMenuStrip = _trayMenu,
            Visible = !previewOnly
        };
        _notifyIcon.DoubleClick += TrayIconOnDoubleClick;

        _applyTimer = new System.Windows.Forms.Timer { Interval = 90 };
        _applyTimer.Tick += ApplyTimerOnTick;
        _saveTimer = new System.Windows.Forms.Timer { Interval = 350 };
        _saveTimer.Tick += SaveTimerOnTick;
        _displayRefreshTimer = new System.Windows.Forms.Timer { Interval = 850 };
        _displayRefreshTimer.Tick += DisplayRefreshTimerOnTick;

        FormClosing += MainFormOnFormClosing;
        Resize += MainFormOnResize;
        KeyDown += MainFormOnKeyDown;
        Shown += MainFormOnShown;
        SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += SystemEventsOnPowerModeChanged;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = false,
            Height = 78
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = false,
            Margin = new Padding(0)
        };
        titlePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _headerTitleLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = UiText.Get(TextId.HeaderTitle),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(28, 34, 48),
            Margin = new Padding(0)
        };
        _headerSubtitleLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(510, 0),
            Text = UiText.Get(TextId.HeaderSubtitle),
            ForeColor = Color.FromArgb(96, 106, 124),
            Margin = new Padding(2, 4, 0, 0)
        };
        titlePanel.Controls.Add(_headerTitleLabel, 0, 0);
        titlePanel.Controls.Add(_headerSubtitleLabel, 0, 1);
        panel.Controls.Add(titlePanel, 0, 0);

        var languagePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            Margin = new Padding(13, 1, 0, 0)
        };
        _languageLabel = new Label
        {
            AutoSize = true,
            Text = UiText.Get(TextId.LanguageLabel),
            ForeColor = Color.FromArgb(96, 106, 124),
            Margin = new Padding(1, 0, 0, 2)
        };
        languagePanel.Controls.Add(_languageLabel, 0, 0);
        languagePanel.Controls.Add(_languageCombo, 0, 1);
        panel.Controls.Add(languagePanel, 1, 0);
        return panel;
    }

    private static Label NewSectionLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point),
        ForeColor = Color.FromArgb(52, 60, 75),
        Margin = new Padding(1, 0, 0, 0)
    };

    private static Label NewScaleLabel(string text, ContentAlignment alignment) => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        Height = 21,
        Text = text,
        TextAlign = alignment,
        ForeColor = Color.FromArgb(125, 132, 146),
        Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point)
    };

    private static Button NewButton(string text, EventHandler handler, bool primary)
    {
        var button = new Button
        {
            AutoSize = true,
            Height = 34,
            Padding = new Padding(10, 3, 10, 3),
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(49, 101, 218) : Color.White,
            ForeColor = primary ? Color.White : Color.FromArgb(55, 65, 81),
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = primary
            ? Color.FromArgb(49, 101, 218)
            : Color.FromArgb(199, 205, 216);
        button.Click += handler;
        return button;
    }

    private void MainFormOnShown(object? sender, EventArgs e)
    {
        RefreshMonitors(reapply: !_previewOnly);
        if (_manageStartup)
        {
            try
            {
                // Reconcile both enabled and disabled states so a stale Run entry cannot survive.
                _startupManager.SetEnabled(_settings.StartWithWindows);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or IOException or InvalidOperationException or
                System.Security.SecurityException)
            {
                ShowStatus(UiText.Format(TextId.StartupUpdateFailed, exception.Message), StatusKind.Warning);
            }
        }

        if (_startHidden)
        {
            BeginInvoke(new Action(() => MinimizeToTray(showNotification: false)));
        }
    }

    private void LanguageComboOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressInput || _languageCombo.SelectedItem is not LanguageItem languageItem)
        {
            return;
        }

        UiText.CurrentLanguage = languageItem.Language;
        _settings.Language = UiText.ToCode(languageItem.Language);
        ApplyLanguage();
        ScheduleSave();
    }

    private void ApplyLanguage()
    {
        Text = UiText.Get(TextId.WindowTitle);
        _headerTitleLabel.Text = UiText.Get(TextId.HeaderTitle);
        _headerSubtitleLabel.Text = UiText.Get(TextId.HeaderSubtitle);
        _languageLabel.Text = UiText.Get(TextId.LanguageLabel);
        _monitorSectionLabel.Text = UiText.Get(TextId.MonitorSection);
        _monitorCombo.AccessibleName = UiText.Get(TextId.MonitorAccessible);
        _gammaNumber.AccessibleName = UiText.Get(TextId.GammaInputAccessible);
        _gammaSlider.AccessibleName = UiText.Get(TextId.GammaSliderAccessible);
        _scaleDarkLabel.Text = UiText.Get(TextId.ScaleDark);
        _scaleNeutralLabel.Text = UiText.Get(TextId.ScaleNeutral);
        _scaleBrightLabel.Text = UiText.Get(TextId.ScaleBright);
        _reapplyButton.Text = UiText.Get(TextId.Reapply);
        _resetSelectedButton.Text = UiText.Get(TextId.ResetSelected);
        _resetAllButton.Text = UiText.Get(TextId.ResetAll);
        _restoreOnExitCheck.Text = UiText.Get(TextId.RestoreOnExit);
        _restoreOnExitCheck.AccessibleName = UiText.Get(TextId.RestoreAccessible);
        _startWithWindowsCheck.Text = UiText.Get(TextId.StartWithWindows);
        _startWithWindowsCheck.AccessibleName = UiText.Get(TextId.StartAccessible);
        _caveatLabel.Text = UiText.Get(TextId.Caveat);
        _shortcutLabel.Text = UiText.Get(TextId.Shortcut);
        _trayOpenItem.Text = UiText.Get(TextId.TrayOpen);
        _trayExitItem.Text = UiText.Get(TextId.TrayExit);
        _notifyIcon.Text = UiText.Get(TextId.WindowTitle);
        RebuildMonitorCombo(_gammaService.Targets, _settings.SelectedDevice);
        UpdateEditorForSelection();
        ShowStatus(UiText.Get(TextId.Ready), StatusKind.Info);
    }

    private void RefreshMonitors(bool reapply)
    {
        try
        {
            var targets = _gammaService.Refresh();
            var migratedSettings = MigrateLegacyDisplayKeys(targets);
            foreach (var target in targets)
            {
                target.CurrentGamma = _settings.GammaByDevice.TryGetValue(target.SettingsKey, out var savedGamma)
                    ? savedGamma
                    : 1.0;
            }
            RebuildMonitorCombo(targets, _settings.SelectedDevice);
            UpdateEditorForSelection();

            if (targets.Count == 0)
            {
                ShowStatus(UiText.Get(TextId.NoMonitorsFound), StatusKind.Error);
                SetEditorEnabled(false);
                return;
            }

            SetEditorEnabled(targets.Any(target => target.SupportsGamma));
            if (reapply)
            {
                var result = _gammaService.ReapplySavedGammas(_settings.GammaByDevice);
                ShowApplyResult(result, UiText.Get(TextId.SavedReapplied));
            }
            else
            {
                ShowStatus(UiText.Format(TextId.PreviewFound, targets.Count), StatusKind.Info);
            }

            if (migratedSettings && !_previewOnly)
            {
                ScheduleSave();
            }
        }
        catch (Exception exception)
        {
            _suppressInput = false;
            SetEditorEnabled(false);
            ShowStatus(exception.Message, StatusKind.Error);
        }
    }

    private void RebuildMonitorCombo(IReadOnlyList<DisplayTarget> targets, string selectedDevice)
    {
        _suppressInput = true;
        try
        {
            _monitorCombo.BeginUpdate();
            _monitorCombo.Items.Clear();
            _monitorCombo.Items.Add(new AllDisplaysItem(targets.Count));
            foreach (var target in targets)
            {
                _monitorCombo.Items.Add(target);
            }

            var selectedIndex = 0;
            if (!string.Equals(selectedDevice, AppSettings.AllDisplaysKey, StringComparison.Ordinal))
            {
                for (var index = 1; index < _monitorCombo.Items.Count; index++)
                {
                    if (_monitorCombo.Items[index] is DisplayTarget target &&
                        string.Equals(target.SettingsKey, selectedDevice, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            _monitorCombo.SelectedIndex = selectedIndex;
        }
        finally
        {
            _monitorCombo.EndUpdate();
            _suppressInput = false;
        }
    }

    private void MonitorComboOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressInput || _monitorCombo.SelectedItem == null)
        {
            return;
        }

        ApplyPendingNow();

        _settings.SelectedDevice = SelectedTarget?.SettingsKey ?? AppSettings.AllDisplaysKey;
        UpdateEditorForSelection();
        ScheduleSave();
    }

    private DisplayTarget? SelectedTarget => _monitorCombo.SelectedItem as DisplayTarget;
    private string? SelectedDeviceName => SelectedTarget?.DeviceName;

    private void UpdateEditorForSelection()
    {
        if (_monitorCombo.SelectedItem is DisplayTarget target)
        {
            var linked = _gammaService.IsLinked(target.DeviceName);
            _monitorDetailLabel.Text = !target.SupportsGamma
                ? UiText.Get(TextId.MonitorUnsupported)
                : linked
                    ? UiText.Get(TextId.SharedMonitorDetail)
                    : UiText.Format(
                        TextId.MonitorPosition,
                        target.Bounds.Width,
                        target.Bounds.Height,
                        target.Bounds.X,
                        target.Bounds.Y);
            SetEditorGamma(GetSavedGamma(target.SettingsKey), mixed: false);
            SetEditorEnabled(target.SupportsGamma && !linked);
            return;
        }

        var targets = _gammaService.Targets;
        _monitorDetailLabel.Text = targets.Count == 0
            ? UiText.Get(TextId.NoMonitorsConnected)
            : UiText.Get(TextId.AllMonitorsDetail);
        var gammas = targets.Select(target => GetSavedGamma(target.SettingsKey)).ToArray();
        var mixed = gammas.Length > 1 && gammas.Any(gamma => Math.Abs(gamma - gammas[0]) > 0.001);
        var displayGamma = gammas.Length == 0 ? 1.0 : gammas.Average();
        SetEditorGamma(displayGamma, mixed);
        SetEditorEnabled(targets.Any(target => target.SupportsGamma));
    }

    private void SetEditorGamma(double gamma, bool mixed)
    {
        gamma = Math.Clamp(gamma, GammaRampBuilder.MinimumGamma, GammaRampBuilder.MaximumGamma);
        _suppressInput = true;
        _gammaSlider.Value = GammaRampBuilder.ToSliderPosition(gamma, _gammaSlider.Maximum);
        _gammaNumber.Value = Math.Clamp((decimal)gamma, _gammaNumber.Minimum, _gammaNumber.Maximum);
        _gammaValueLabel.Text = mixed
            ? UiText.Get(TextId.Mixed)
            : gamma.ToString("F2", CultureInfo.InvariantCulture) + "×";
        _suppressInput = false;
    }

    private void GammaSliderOnValueChanged(object? sender, EventArgs e)
    {
        if (_suppressInput)
        {
            return;
        }

        SetPendingGamma(GammaRampBuilder.FromSliderPosition(_gammaSlider.Value, _gammaSlider.Maximum), updateSlider: false);
    }

    private void GammaNumberOnValueChanged(object? sender, EventArgs e)
    {
        if (_suppressInput)
        {
            return;
        }

        SetPendingGamma((double)_gammaNumber.Value, updateSlider: true);
    }

    private void SetPendingGamma(double gamma, bool updateSlider)
    {
        gamma = Math.Clamp(gamma, GammaRampBuilder.MinimumGamma, GammaRampBuilder.MaximumGamma);
        gamma = Math.Round(gamma, 2, MidpointRounding.AwayFromZero);
        _suppressInput = true;
        if (updateSlider)
        {
            _gammaSlider.Value = GammaRampBuilder.ToSliderPosition(gamma, _gammaSlider.Maximum);
        }
        else
        {
            _gammaNumber.Value = Math.Clamp((decimal)gamma, _gammaNumber.Minimum, _gammaNumber.Maximum);
        }
        _gammaValueLabel.Text = gamma.ToString("F2", CultureInfo.InvariantCulture) + "×";
        _suppressInput = false;

        _pendingGamma = gamma;
        _pendingDeviceName = SelectedDeviceName;
        _hasPendingApply = true;
        StoreGammaForSelection(gamma);
        ScheduleApply();
        ScheduleSave();
    }

    private void StoreGammaForSelection(double gamma)
    {
        if (SelectedTarget is { } selectedTarget)
        {
            _settings.GammaByDevice[selectedTarget.SettingsKey] = gamma;
            return;
        }

        foreach (var target in _gammaService.Targets)
        {
            _settings.GammaByDevice[target.SettingsKey] = gamma;
        }
    }

    private void ScheduleApply()
    {
        if (_previewOnly)
        {
            return;
        }

        _applyTimer.Stop();
        _applyTimer.Start();
    }

    private void ApplyTimerOnTick(object? sender, EventArgs e)
    {
        _applyTimer.Stop();
        ApplyPendingNow();
    }

    private void ApplyPendingNow()
    {
        if (!_hasPendingApply)
        {
            return;
        }

        var deviceName = _pendingDeviceName;
        var gamma = _pendingGamma;
        _hasPendingApply = false;
        var result = _gammaService.ApplyGamma(deviceName, gamma);
        ShowApplyResult(
            result,
            UiText.Format(TextId.GammaApplied, gamma.ToString("F2", CultureInfo.InvariantCulture)));
    }

    private void ReapplyButtonOnClick(object? sender, EventArgs e)
    {
        CancelPendingApply();
        ApplyResult result;
        if (SelectedTarget is { } selectedTarget)
        {
            result = _gammaService.ApplyGamma(
                selectedTarget.DeviceName,
                GetSavedGamma(selectedTarget.SettingsKey));
        }
        else
        {
            result = _settings.GammaByDevice.Count == 0
                ? _gammaService.ApplyGamma(null, GetEditorGamma())
                : _gammaService.ReapplySavedGammas(_settings.GammaByDevice);
        }
        ShowApplyResult(result, UiText.Get(TextId.CurrentReapplied));
    }

    private void ResetSelectedButtonOnClick(object? sender, EventArgs e)
    {
        SetPendingGamma(1.0, updateSlider: true);
        if (!_previewOnly)
        {
            CancelPendingApply();
            ShowApplyResult(
                _gammaService.ApplyGamma(SelectedDeviceName, 1.0),
                UiText.Get(TextId.SelectedResetSuccess));
        }
    }

    private void ResetAllButtonOnClick(object? sender, EventArgs e)
    {
        CancelPendingApply();
        foreach (var target in _gammaService.Targets)
        {
            _settings.GammaByDevice[target.SettingsKey] = 1.0;
        }
        SetEditorGamma(1.0, mixed: false);
        ScheduleSave();
        if (!_previewOnly)
        {
            ShowApplyResult(
                _gammaService.ApplyGamma(null, 1.0),
                UiText.Get(TextId.AllResetSuccess));
        }
    }

    private void RestoreOnExitCheckOnCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressInput)
        {
            return;
        }

        _settings.RestoreOnExit = _restoreOnExitCheck.Checked;
        ScheduleSave();
    }

    private void StartWithWindowsCheckOnCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressInput || !_manageStartup)
        {
            return;
        }

        var requested = _startWithWindowsCheck.Checked;
        var previous = _settings.StartWithWindows;
        try
        {
            _startupManager.SetEnabled(requested);
            _settings.StartWithWindows = requested;
            _saveTimer.Stop();
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or IOException or InvalidOperationException or
            System.Security.SecurityException)
        {
            try
            {
                _startupManager.SetEnabled(previous);
            }
            catch
            {
                // Keep the original error visible; the next launch reconciles from the saved setting.
            }
            _settings.StartWithWindows = previous;
            _suppressInput = true;
            _startWithWindowsCheck.Checked = previous;
            _suppressInput = false;
            ShowStatus(UiText.Format(TextId.StartupUpdateFailed, exception.Message), StatusKind.Warning);
        }
    }

    private void MainFormOnResize(object? sender, EventArgs e)
    {
        if (!_previewOnly && !_changingTrayVisibility && WindowState == FormWindowState.Minimized)
        {
            MinimizeToTray(showNotification: true);
        }
    }

    private void MinimizeToTray(bool showNotification)
    {
        if (_previewOnly || IsDisposed)
        {
            return;
        }

        if (_changingTrayVisibility)
        {
            return;
        }

        _changingTrayVisibility = true;
        try
        {
            Hide();
            ShowInTaskbar = false;
            Opacity = 1;
        }
        finally
        {
            _changingTrayVisibility = false;
        }
        if (showNotification && !_trayNoticeShown)
        {
            _trayNoticeShown = true;
            _notifyIcon.ShowBalloonTip(
                2500,
                UiText.Get(TextId.TrayTitle),
                UiText.Get(TextId.TrayMessage),
                ToolTipIcon.Info);
        }
    }

    internal void RestoreFromTray()
    {
        if (IsDisposed)
        {
            return;
        }

        if (_changingTrayVisibility)
        {
            return;
        }

        _changingTrayVisibility = true;
        try
        {
            Opacity = 1;
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            EnsureWindowIsVisible();
            BringToFront();
            Activate();

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || !Visible)
                {
                    return;
                }

                EnsureWindowIsVisible();
                BringToFront();
                Activate();
            }));
        }
        finally
        {
            _changingTrayVisibility = false;
        }
    }

    internal void TrayIconOnDoubleClick(object? sender, EventArgs e)
    {
        RestoreFromTray();
    }

    private void EnsureWindowIsVisible()
    {
        const int minimumVisibleWidth = 80;
        const int minimumVisibleHeight = 48;
        var currentBounds = Bounds;
        var isVisibleOnAnyScreen = Screen.AllScreens.Any(screen =>
        {
            var visibleBounds = Rectangle.Intersect(screen.WorkingArea, currentBounds);
            return visibleBounds.Width >= minimumVisibleWidth &&
                   visibleBounds.Height >= minimumVisibleHeight;
        });
        if (isVisibleOnAnyScreen)
        {
            return;
        }

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var width = Math.Min(Math.Max(Width, MinimumSize.Width), workingArea.Width);
        var height = Math.Min(Math.Max(Height, MinimumSize.Height), workingArea.Height);
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(
            workingArea.Left + (workingArea.Width - width) / 2,
            workingArea.Top + (workingArea.Height - height) / 2,
            width,
            height);
    }

    internal void ExitCompletely()
    {
        _allowExit = true;
        Close();
    }

    private void MainFormOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.Shift && e.KeyCode == Keys.D0)
        {
            ResetAllButtonOnClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.D0)
        {
            ResetSelectedButtonOnClick(sender, EventArgs.Empty);
            e.SuppressKeyPress = true;
        }
    }

    private void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        ScheduleDisplayRefresh();
    }

    private void SystemEventsOnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            ScheduleDisplayRefresh();
        }
    }

    private void ScheduleDisplayRefresh()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(new Action(() =>
            {
                _displayRefreshTimer.Stop();
                _displayRefreshTimer.Start();
            }));
        }
        catch (InvalidOperationException) when (IsDisposed || Disposing || !IsHandleCreated)
        {
            // The form can finish disposing between the state check and BeginInvoke.
        }
    }

    private void DisplayRefreshTimerOnTick(object? sender, EventArgs e)
    {
        _displayRefreshTimer.Stop();
        RefreshMonitors(reapply: !_previewOnly);
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveTimerOnTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        TrySaveSettings();
    }

    private void TrySaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowStatus(UiText.Format(TextId.SettingsSaveFailed, exception.Message), StatusKind.Warning);
        }
    }

    private void MainFormOnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_previewOnly && !_allowExit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray(showNotification: true);
            return;
        }

        CancelPendingApply();
        _saveTimer.Stop();
        _displayRefreshTimer.Stop();
        TrySaveSettings();
        RestoreIfRequested();
        if (_settings.RestoreOnExit && _gammaService.HasOwnedRampsOnConnectedTargets &&
            e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _allowExit = false;
            RestoreFromTray();
            var message = UiText.Get(TextId.RestoreFailed);
            ShowStatus(message, StatusKind.Error);
            MessageBox.Show(
                this,
                message,
                UiText.Get(TextId.WindowTitle),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= SystemEventsOnPowerModeChanged;
    }

    internal int RestoreIfRequested()
    {
        if (_restored || _previewOnly || !_settings.RestoreOnExit)
        {
            return 0;
        }

        var restored = _gammaService.RestoreOwnedRamps();
        _restored = !_gammaService.HasOwnedRamps;
        return restored;
    }

    private bool MigrateLegacyDisplayKeys(IReadOnlyList<DisplayTarget> targets)
    {
        var changed = false;
        foreach (var target in targets)
        {
            if (_settings.GammaByDevice.TryGetValue(target.DeviceName, out var legacyGamma))
            {
                if (!_settings.GammaByDevice.ContainsKey(target.SettingsKey))
                {
                    _settings.GammaByDevice[target.SettingsKey] = legacyGamma;
                }

                _settings.GammaByDevice.Remove(target.DeviceName);
                changed = true;
            }

            if (string.Equals(_settings.SelectedDevice, target.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                _settings.SelectedDevice = target.SettingsKey;
                changed = true;
            }
        }

        return changed;
    }

    private double GetSavedGamma(string settingsKey)
    {
        return _settings.GammaByDevice.TryGetValue(settingsKey, out var gamma)
            ? Math.Clamp(gamma, GammaRampBuilder.MinimumGamma, GammaRampBuilder.MaximumGamma)
            : 1.0;
    }

    private double GetEditorGamma() => (double)_gammaNumber.Value;

    private void SetEditorEnabled(bool enabled)
    {
        _gammaSlider.Enabled = enabled;
        _gammaNumber.Enabled = enabled;
    }

    private enum StatusKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    private void ShowApplyResult(ApplyResult result, string successMessage)
    {
        if (result.RolledBack)
        {
            var rolledBackTarget = _gammaService.Targets.FirstOrDefault(target =>
                string.Equals(target.DeviceName, result.RequestedDeviceName, StringComparison.OrdinalIgnoreCase));
            if (rolledBackTarget != null && result.RollbackFailedDevices.Count == 0)
            {
                _settings.GammaByDevice[rolledBackTarget.SettingsKey] = rolledBackTarget.CurrentGamma;
            }
            UpdateEditorForSelection();
            ScheduleSave();
            var rollbackText = result.RollbackFailedDevices.Count == 0
                ? UiText.Get(TextId.RollbackSingleSuccess)
                : UiText.Get(TextId.RollbackSingleFailed);
            ShowStatus(rollbackText, StatusKind.Warning);
            return;
        }

        if (result.FailedDevices.Count > 0)
        {
            var detail = result.ErrorMessage ?? UiText.Get(TextId.SomeUnsupported);
            ShowStatus(
                UiText.Format(TextId.ApplyFailedCount, result.FailedDevices.Count, detail),
                StatusKind.Error);
            return;
        }

        if (result.UnexpectedlyChangedDevices.Count > 0)
        {
            ShowStatus(
                UiText.Format(TextId.AppliedOtherCount, result.UnexpectedlyChangedDevices.Count),
                StatusKind.Warning);
            return;
        }

        if (result.UnverifiedDevices.Count > 0)
        {
            ShowStatus(
                UiText.Format(TextId.UnverifiedCount, result.UnverifiedDevices.Count),
                StatusKind.Warning);
            return;
        }

        ShowStatus(
            UiText.Format(TextId.SuccessCount, successMessage, result.ChangedDevices.Count),
            StatusKind.Success);
    }

    private void CancelPendingApply()
    {
        _applyTimer.Stop();
        _hasPendingApply = false;
        _pendingDeviceName = null;
    }

    private void ShowStatus(string text, StatusKind kind)
    {
        _statusLabel.Text = text;
        (_statusLabel.BackColor, _statusLabel.ForeColor) = kind switch
        {
            StatusKind.Success => (Color.FromArgb(232, 247, 238), Color.FromArgb(34, 111, 68)),
            StatusKind.Warning => (Color.FromArgb(255, 246, 224), Color.FromArgb(137, 91, 10)),
            StatusKind.Error => (Color.FromArgb(254, 236, 236), Color.FromArgb(158, 48, 48)),
            _ => (Color.FromArgb(237, 244, 255), Color.FromArgb(34, 79, 145))
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;
            SystemEvents.PowerModeChanged -= SystemEventsOnPowerModeChanged;
            RestoreIfRequested();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _applyTimer.Dispose();
            _saveTimer.Dispose();
            _displayRefreshTimer.Dispose();
            _gammaService.Dispose();
        }

        base.Dispose(disposing);
    }
}
