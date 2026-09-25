using System.Globalization;

namespace GammaControl;

internal enum AppLanguage
{
    Korean,
    English
}

internal enum TextId
{
    WindowTitle,
    AlreadyRunning,
    UnexpectedError,
    AllDisplays,
    PrimaryMonitor,
    GammaUnsupported,
    GenericMonitor,
    EnumerationFailed,
    SelectedNotFound,
    SharedLutUseAll,
    SharedLutRolledBack,
    SharedLutRollbackFailed,
    DisplayDcFailed,
    RampReadFailed,
    DriverRejected,
    HeaderTitle,
    HeaderSubtitle,
    LanguageLabel,
    MonitorSection,
    MonitorAccessible,
    CheckingMonitors,
    GammaInputAccessible,
    GammaSliderAccessible,
    ScaleDark,
    ScaleNeutral,
    ScaleBright,
    Reapply,
    ResetSelected,
    ResetAll,
    RestoreOnExit,
    RestoreAccessible,
    StartWithWindows,
    StartAccessible,
    TargetProgram,
    TargetProgramAccessible,
    ChooseProgram,
    ConfineCursor,
    ConfineAccessible,
    ConfineRequiresProgram,
    ConfineFailed,
    ConfinePaused,
    ConfineEnabled,
    ConfineHotkeyUnavailable,
    ConfineReleased,
    ConfineDisabled,
    TrayReleaseCursor,
    Ready,
    Caveat,
    Shortcut,
    NoMonitorsFound,
    SavedReapplied,
    PreviewFound,
    MonitorUnsupported,
    SharedMonitorDetail,
    MonitorPosition,
    NoMonitorsConnected,
    AllMonitorsDetail,
    Mixed,
    GammaApplied,
    CurrentReapplied,
    SelectedResetSuccess,
    AllResetSuccess,
    SettingsSaveFailed,
    StartupUpdateFailed,
    RollbackSingleSuccess,
    RollbackSingleFailed,
    SomeUnsupported,
    ApplyFailedCount,
    AppliedOtherCount,
    UnverifiedCount,
    SuccessCount,
    TrayOpen,
    TrayExit,
    TrayTitle,
    TrayMessage,
    RestoreFailed,
    SettingsCorrupted,
    TabGamma,
    TabProgramGamma,
    TabMouse,
    TabSettings,
    ChooseProcessTitle,
    ProcessPickerHelp,
    NoProcessesFound,
    ProcessSelect,
    ProcessCancel,
    ProcessRefresh,
    ProgramGammaProcessAccessible,
    ProgramGammaValue,
    ProgramGammaEnable,
    ProgramGammaNote,
    ProgramGammaRequiresProcess,
    ProgramGammaApplyFailed,
    ProgramGammaRestoreFailed
}

internal static class UiText
{
    private static readonly IReadOnlyDictionary<TextId, (string Korean, string English)> Catalog =
        new Dictionary<TextId, (string, string)>
        {
            [TextId.WindowTitle] = ("HDobby Gamma", "HDobby Gamma"),
            [TextId.AlreadyRunning] = ("HDobby Gamma가 이미 실행 중입니다. 트레이 아이콘을 확인하세요.", "HDobby Gamma is already running. Check the tray icon."),
            [TextId.UnexpectedError] = ("예상하지 못한 오류가 발생했습니다. 가능한 경우 실행 전 감마로 복원했습니다.\n\n{0}", "An unexpected error occurred. The pre-launch gamma was restored where possible.\n\n{0}"),
            [TextId.AllDisplays] = ("모든 모니터 ({0})", "All monitors ({0})"),
            [TextId.PrimaryMonitor] = ("주 모니터", "Primary"),
            [TextId.GammaUnsupported] = ("감마 미지원", "Gamma unsupported"),
            [TextId.GenericMonitor] = ("일반 모니터", "Generic monitor"),
            [TextId.EnumerationFailed] = ("Windows에서 모니터 목록을 가져오지 못했습니다.", "Windows could not enumerate the monitors."),
            [TextId.SelectedNotFound] = ("선택한 모니터를 찾을 수 없습니다.", "The selected monitor could not be found."),
            [TextId.SharedLutUseAll] = ("이 출력은 다른 모니터와 감마 LUT를 공유합니다. '모든 모니터'를 사용하세요.", "This output shares a gamma LUT with another monitor. Use 'All monitors'."),
            [TextId.SharedLutRolledBack] = ("드라이버의 공유 감마 LUT를 감지해 변경을 즉시 되돌렸습니다.", "A shared driver gamma LUT was detected, so the change was rolled back immediately."),
            [TextId.SharedLutRollbackFailed] = ("공유 감마 LUT를 감지했지만 일부 화면을 자동 복원하지 못했습니다.", "A shared gamma LUT was detected, but some displays could not be restored automatically."),
            [TextId.DisplayDcFailed] = ("{0}: 디스플레이 DC를 만들 수 없습니다.", "{0}: Could not create a display DC."),
            [TextId.RampReadFailed] = ("{0}: 현재 감마 램프를 읽을 수 없습니다.", "{0}: Could not read the current gamma ramp."),
            [TextId.DriverRejected] = ("{0}: 그래픽 드라이버가 감마 변경을 거부했습니다.", "{0}: The graphics driver rejected the gamma change."),
            [TextId.HeaderTitle] = ("HDobby Gamma", "HDobby Gamma"),
            [TextId.HeaderSubtitle] = ("모든 화면을 함께 조절하거나 모니터마다 다른 값을 적용할 수 있습니다.", "Adjust every display together or use a different value for each monitor."),
            [TextId.LanguageLabel] = ("언어", "Language"),
            [TextId.MonitorSection] = ("적용할 모니터", "Monitor to adjust"),
            [TextId.MonitorAccessible] = ("적용할 모니터", "Monitor to adjust"),
            [TextId.CheckingMonitors] = ("모니터를 확인하는 중입니다.", "Checking connected monitors."),
            [TextId.GammaInputAccessible] = ("감마값 직접 입력", "Direct gamma input"),
            [TextId.GammaSliderAccessible] = ("감마 슬라이더", "Gamma slider"),
            [TextId.ScaleDark] = ("0.20 · 매우 어둡게", "0.20 · Very dark"),
            [TextId.ScaleNeutral] = ("1.00 · 기본", "1.00 · Neutral"),
            [TextId.ScaleBright] = ("5.00 · 매우 밝게", "5.00 · Very bright"),
            [TextId.Reapply] = ("현재값 다시 적용", "Reapply current value"),
            [TextId.ResetSelected] = ("선택 1.00", "Selected 1.00"),
            [TextId.ResetAll] = ("전체 1.00", "All 1.00"),
            [TextId.RestoreOnExit] = ("완전히 종료할 때 실행 전 색상으로 복원 (권장)", "Restore pre-launch colors when fully exiting (recommended)"),
            [TextId.RestoreAccessible] = ("완전히 종료할 때 원래 감마 복원", "Restore original gamma on full exit"),
            [TextId.StartWithWindows] = ("Windows 시작 시 자동 실행 (트레이에서 시작)", "Start with Windows (start in tray)"),
            [TextId.StartAccessible] = ("Windows 시작 시 HDobby Gamma 자동 실행", "Start HDobby Gamma with Windows"),
            [TextId.TargetProgram] = ("대상 프로그램", "Target program"),
            [TextId.TargetProgramAccessible] = ("마우스를 가둘 프로세스", "Process whose window will confine the mouse"),
            [TextId.ChooseProgram] = ("프로세스 선택", "Select process"),
            [TextId.ConfineCursor] = ("대상 프로그램 사용 중 마우스를 창 안에 가두기", "Confine mouse to target window while active"),
            [TextId.ConfineAccessible] = ("대상 프로그램 창이 활성화된 동안 마우스 가두기", "Confine the mouse while the target program window is active"),
            [TextId.ConfineRequiresProgram] = ("먼저 실행 중인 프로세스를 선택하세요.", "Select a running process first."),
            [TextId.ConfineFailed] = ("마우스를 가두지 못했습니다: {0}", "Could not confine the mouse: {0}"),
            [TextId.ConfinePaused] = ("마우스 제한 오류가 반복되어 재시도를 멈췄습니다. 체크는 유지됩니다. Ctrl+Alt+L로 다시 시도하세요.", "Mouse confinement is paused after repeated errors. The rule stays checked. Press Ctrl+Alt+L to retry."),
            [TextId.ConfineEnabled] = ("대상 프로그램 창이 활성화되면 마우스를 가둡니다. Ctrl+Alt+L은 이번 활성화에서만 해제합니다.", "The mouse will be confined while the target window is active. Ctrl+Alt+L releases it for this activation."),
            [TextId.ConfineHotkeyUnavailable] = ("해제 단축키 Ctrl+Alt+L을 등록할 수 없어 마우스 가두기를 켜지 않았습니다.", "Could not register the Ctrl+Alt+L release shortcut, so mouse confinement was not enabled."),
            [TextId.ConfineReleased] = ("이번 활성화에서 마우스를 풀었습니다. 다음에 대상 창을 다시 활성화하면 가두기가 적용됩니다.", "Mouse released for this activation. Confinement resumes the next time the target window becomes active."),
            [TextId.ConfineDisabled] = ("마우스 가두기 규칙을 껐습니다.", "Mouse confinement rule disabled."),
            [TextId.TrayReleaseCursor] = ("이번 활성화에서 마우스 해제", "Release mouse for this activation"),
            [TextId.Ready] = ("준비 중…", "Getting ready…"),
            [TextId.Caveat] = ("HDR, 야간 모드, 색상 보정 프로그램 또는 일부 전체화면 게임은 Windows 감마 램프를 덮어쓸 수 있습니다.", "HDR, Night light, color calibration tools, or some fullscreen games may override the Windows gamma ramp."),
            [TextId.Shortcut] = ("단축키  Ctrl+0: 선택 초기화   Ctrl+Shift+0: 전체 초기화   Ctrl+Alt+L: 마우스 해제", "Shortcuts  Ctrl+0: reset selected   Ctrl+Shift+0: reset all   Ctrl+Alt+L: release mouse"),
            [TextId.NoMonitorsFound] = ("연결된 모니터를 찾을 수 없습니다.", "No connected monitors were found."),
            [TextId.SavedReapplied] = ("저장된 감마를 다시 적용했습니다.", "Reapplied the saved gamma values."),
            [TextId.PreviewFound] = ("모니터 {0}대를 찾았습니다. 미리보기에서는 화면을 변경하지 않습니다.", "Displays found: {0}. Preview mode does not change the screen."),
            [TextId.MonitorUnsupported] = ("이 모니터는 감마 램프를 지원하지 않습니다.", "This monitor does not support gamma ramps."),
            [TextId.SharedMonitorDetail] = ("그래픽 드라이버가 다른 모니터와 감마를 공유합니다. '모든 모니터'를 사용하세요.", "The graphics driver shares gamma with another monitor. Use 'All monitors'."),
            [TextId.MonitorPosition] = ("{0}×{1} · 위치 {2}, {3}", "{0}×{1} · Position {2}, {3}"),
            [TextId.NoMonitorsConnected] = ("연결된 모니터 없음", "No connected monitors"),
            [TextId.AllMonitorsDetail] = ("조절하면 지원되는 모든 모니터에 같은 값이 적용됩니다.", "The same value will be applied to every supported monitor."),
            [TextId.Mixed] = ("혼합", "Mixed"),
            [TextId.GammaApplied] = ("감마 {0}를 적용했습니다.", "Applied gamma {0}."),
            [TextId.CurrentReapplied] = ("현재값을 다시 적용했습니다.", "Reapplied the current value."),
            [TextId.SelectedResetSuccess] = ("선택 항목을 1.00으로 초기화했습니다.", "Reset the selected item to 1.00."),
            [TextId.AllResetSuccess] = ("모든 모니터를 1.00으로 초기화했습니다.", "Reset every monitor to 1.00."),
            [TextId.SettingsSaveFailed] = ("설정을 저장하지 못했습니다: {0}", "Could not save settings: {0}"),
            [TextId.StartupUpdateFailed] = ("자동 실행 설정을 변경하지 못했습니다: {0}", "Could not update the startup setting: {0}"),
            [TextId.RollbackSingleSuccess] = ("다른 모니터도 함께 바뀌어 즉시 원상 복구했습니다. 이 출력은 '모든 모니터'로만 조절할 수 있습니다.", "Another monitor changed too, so the change was rolled back. This output can only be adjusted with 'All monitors'."),
            [TextId.RollbackSingleFailed] = ("다른 모니터도 함께 바뀌었고 일부 자동 복원이 확인되지 않았습니다. 전체 1.00을 눌러 복구하세요.", "Another monitor changed too and some automatic restoration could not be verified. Use 'All 1.00' to recover."),
            [TextId.SomeUnsupported] = ("일부 모니터가 감마 변경을 지원하지 않습니다.", "Some monitors do not support gamma changes."),
            [TextId.ApplyFailedCount] = ("적용 실패 {0}대 · {1}", "Failed displays: {0} · {1}"),
            [TextId.AppliedOtherCount] = ("적용됨 · 드라이버가 다른 모니터 {0}대도 함께 변경했습니다.", "Applied · The driver also changed {0} other display(s)."),
            [TextId.UnverifiedCount] = ("요청은 전송됐지만 {0}대에서 결과를 확인하지 못했습니다. HDR/드라이버 설정을 확인하세요.", "The request was sent, but the result could not be verified on {0} display(s). Check HDR and driver settings."),
            [TextId.SuccessCount] = ("{0} · {1}대 확인", "{0} · Verified displays: {1}"),
            [TextId.TrayOpen] = ("열기", "Open"),
            [TextId.TrayExit] = ("완전히 종료", "Exit completely"),
            [TextId.TrayTitle] = ("HDobby Gamma는 계속 실행 중입니다", "HDobby Gamma is still running"),
            [TextId.TrayMessage] = ("창을 닫아도 트레이에서 감마를 유지합니다. 완전히 종료하려면 트레이 메뉴를 사용하세요.", "Closing the window keeps gamma active in the tray. Use the tray menu to exit completely."),
            [TextId.RestoreFailed] = ("일부 모니터를 실행 전 색상으로 복원하지 못해 종료를 중단했습니다. 화면 연결을 확인하거나 '전체 1.00'을 누른 뒤 다시 종료하세요.", "Exit was cancelled because some monitors could not be restored to their pre-launch colors. Check the display connection or use 'All 1.00', then try again."),
            [TextId.SettingsCorrupted] = ("설정 파일이 손상되어 기본값으로 시작합니다.", "The settings file is damaged, so defaults were loaded."),
            [TextId.TabGamma] = ("기본 감마", "Gamma"),
            [TextId.TabProgramGamma] = ("프로그램 감마", "Program gamma"),
            [TextId.TabMouse] = ("마우스 가두기", "Mouse confinement"),
            [TextId.TabSettings] = ("설정", "Settings"),
            [TextId.ChooseProcessTitle] = ("실행 중인 프로세스 선택", "Select a running process"),
            [TextId.ProcessPickerHelp] = ("목록에서 실행 중인 프로그램 창을 선택하세요. 선택한 프로그램은 나중에 다시 실행해도 인식합니다.",
                "Select a running program window. The saved rule also works after the program restarts."),
            [TextId.NoProcessesFound] = ("선택 가능한 프로그램 창이 없습니다. 프로그램을 실행한 뒤 새로고침하세요.",
                "No selectable program windows. Start the program, then refresh."),
            [TextId.ProcessSelect] = ("선택", "Select"),
            [TextId.ProcessCancel] = ("취소", "Cancel"),
            [TextId.ProcessRefresh] = ("새로고침", "Refresh"),
            [TextId.ProgramGammaProcessAccessible] = ("감마를 적용할 프로세스", "Process for gamma rule"),
            [TextId.ProgramGammaValue] = ("저장할 감마 값", "Saved gamma value"),
            [TextId.ProgramGammaEnable] = ("선택한 프로그램 창이 활성화되면 감마 적용", "Apply gamma while the selected program window is active"),
            [TextId.ProgramGammaNote] = ("감마는 해당 창이 있는 모니터 전체에 적용됩니다. 다른 창으로 전환하면 직전 색상으로 복원합니다.",
                "Gamma affects the entire monitor containing this window. The previous colors return when focus changes."),
            [TextId.ProgramGammaRequiresProcess] = ("먼저 실행 중인 프로세스를 선택하세요.", "Select a running process first."),
            [TextId.ProgramGammaApplyFailed] = ("프로그램 감마를 적용하거나 확인하지 못했습니다.", "Could not apply or verify program gamma."),
            [TextId.ProgramGammaRestoreFailed] = ("프로그램 감마 적용 전 색상으로 복원하지 못했습니다.", "Could not restore the colors from before program gamma.")
        };

    internal static AppLanguage CurrentLanguage { get; set; } = ParseLanguage(DefaultLanguageCode);

    internal static string DefaultLanguageCode =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ko", StringComparison.OrdinalIgnoreCase)
            ? "ko"
            : "en";

    internal static string NormalizeLanguageCode(string? code) =>
        string.Equals(code, "ko", StringComparison.OrdinalIgnoreCase) ? "ko" :
        string.Equals(code, "en", StringComparison.OrdinalIgnoreCase) ? "en" :
        DefaultLanguageCode;

    internal static AppLanguage ParseLanguage(string? code) =>
        string.Equals(NormalizeLanguageCode(code), "ko", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Korean
            : AppLanguage.English;

    internal static string ToCode(AppLanguage language) =>
        language == AppLanguage.Korean ? "ko" : "en";

    internal static string Get(TextId id)
    {
        var item = Catalog[id];
        return CurrentLanguage == AppLanguage.Korean ? item.Korean : item.English;
    }

    internal static string Format(TextId id, params object[] arguments) =>
        string.Format(CultureInfo.InvariantCulture, Get(id), arguments);

    internal static bool HasCompleteCatalog() =>
        Enum.GetValues<TextId>().All(id =>
            Catalog.TryGetValue(id, out var value) &&
            !string.IsNullOrWhiteSpace(value.Korean) &&
            !string.IsNullOrWhiteSpace(value.English));
}
