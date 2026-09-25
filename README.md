# HDobby Gamma

Windows 11용 모니터 감마 조절 앱입니다. 기본 감마, 프로그램 감마, 마우스 가두기, 설정 메뉴를 탭으로 구분했습니다. [English](#english)

## 다운로드

[최신 GitHub 릴리스](https://github.com/Hangeulkim/HDobbyGamma/releases/latest)에서 `HDobbyGamma-*-win-x64.exe`를 받으세요. 별도 .NET 설치나 관리자 권한이 필요 없는 단일 실행 파일입니다. 코드 서명이 없어 Windows SmartScreen에서 처음 실행할 때 게시자 경고가 나올 수 있습니다.

## 사용법

1. **기본 감마** 탭에서 전체 모니터 또는 개별 모니터를 고르고 `0.20`~`5.00` 값을 조절합니다. `1.00`이 기본값입니다. 값은 저장되며 다음 실행 때 다시 적용됩니다.
2. **프로그램 감마** 탭에서 **프로세스 선택**을 눌러 현재 실행 중인 프로그램 창을 고릅니다. 감마 값을 입력하고 자동 적용을 켜세요. 그 창이 활성화되면 창이 위치한 모니터에 저장한 값이 적용되고, 다른 창으로 전환하거나 창을 최소화·종료하면 적용 직전 색상으로 돌아갑니다. 프로그램이 다시 실행되어 프로세스 ID가 바뀌어도 저장된 규칙을 사용합니다.
3. **마우스 가두기** 탭에서도 실행 중인 프로세스를 선택한 뒤 옵션을 켭니다. 선택한 창이 활성화된 동안에만 마우스를 창의 내부 영역에 가둡니다. 포커스를 잃거나 창이 최소화·종료되면 풀립니다. `Ctrl+Alt+L` 또는 트레이 메뉴는 **이번 활성화에서만** 마우스를 풀며 체크 상태와 저장된 규칙은 유지합니다. 알트탭으로 대상 창에 돌아오면 Windows의 실제 제한 상태를 확인해 다시 가두며 체크도 유지됩니다. 규칙을 완전히 끄려면 체크를 직접 해제하세요.
4. **설정** 탭에서 Windows 시작 시 자동 실행과 완전 종료 시 실행 전 색상 복원을 설정합니다. 자동 실행은 사용자 계정의 시작 프로그램으로 등록되며 트레이에서 조용히 시작합니다.

프로세스 목록에는 현재 창이 열린 프로그램이 표시됩니다. 목록에 없으면 프로그램을 먼저 실행하고 **새로고침**을 누르세요. 앱은 선택한 프로세스의 실행 경로를 내부 식별 정보로 저장하여 재실행 후 같은 프로그램을 찾습니다. 프로그램의 설치 위치가 바뀌면 다시 선택하세요. 기본 감마와 프로그램 감마는 별도로 저장되며, 마우스 대상도 따로 선택할 수 있습니다.

창을 닫거나 최소화하면 트레이에서 계속 실행됩니다. 트레이 아이콘을 두 번 클릭하면 창이 다시 열리고, 오른쪽 클릭 메뉴에서 마우스 가두기 해제 또는 완전 종료를 선택할 수 있습니다. 설정은 `%LOCALAPPDATA%\HDobbyGamma\settings.json`에 저장됩니다.

## 기술적 제한

프로그램 감마는 특정 창의 픽셀만 조절하는 기능이 아닙니다. Windows `SetDeviceGammaRamp`는 디스플레이 전체의 감마 램프를 바꾸므로 해당 창이 있는 **모니터 전체**의 색상이 활성화된 동안 바뀝니다. 드라이버가 여러 모니터의 감마 LUT를 공유하는 경우 개별 모니터 적용을 중단하고 변경을 되돌립니다. HDR, 야간 모드, 색상 보정 도구, 원격 데스크톱, 일부 전체 화면 게임 또는 드라이버는 감마 변경을 무시하거나 덮어쓸 수 있습니다. 다른 프로그램이 감마를 바꾼 것이 확인되면 HDobby Gamma는 그 값을 덮어쓰지 않습니다.

일부 드라이버는 극단 값 `0.20`, `5.00` 근처를 거부하거나 제한합니다. 앱은 변경 후 Windows에서 값을 다시 읽어 확인하며 확인되지 않은 변경은 성공으로 표시하지 않습니다. 모니터 연결·포트·드라이버가 바뀌면 저장된 모니터 식별 정보와 값을 확인하세요.

## 소스에서 빌드

.NET SDK `10.0.400` 또는 호환되는 `10.0.4xx`를 설치하고 PowerShell에서 실행하세요.

```powershell
.\build.ps1
```

완성된 단일 실행 파일은 `dist\HDobbyGamma.exe`에 생성됩니다.

---

## English

HDobby Gamma is a Windows 11 monitor gamma utility. Its tabs separate standard gamma, program gamma, mouse confinement, and settings.

### Download

Download `HDobbyGamma-*-win-x64.exe` from the [latest GitHub release](https://github.com/Hangeulkim/HDobbyGamma/releases/latest). It is a self-contained executable that needs neither a separate .NET installation nor administrator rights. It is not code-signed, so SmartScreen may show an unknown-publisher warning.

### Usage

1. In **Gamma**, select all monitors or one monitor and adjust the value from `0.20` to `5.00`. `1.00` is neutral. Values are saved and reapplied on the next launch.
2. In **Program gamma**, choose a **running process**, enter a saved gamma value, and enable automatic application. When its window is foreground, the value applies to the monitor containing that window. Its previous ramp is restored when focus moves away or the window is minimized or closed. The saved rule works after the process restarts with a new PID.
3. In **Mouse confinement**, choose a running process and enable the option. The cursor is confined to its active window's client area. Focus loss, minimize, and exit release it. Press `Ctrl+Alt+L` or use the tray menu to release it **for the current activation** while keeping the saved rule enabled. When you Alt+Tab back, the app checks the actual Windows cursor clip and reapplies confinement while the option stays checked. Uncheck the option to turn off the rule completely.
4. In **Settings**, configure start with Windows and restoration of pre-launch colors on full exit. Startup uses the current user's Run entry and starts in the tray.

The picker lists running programs with windows. Start the program and press **Refresh** if it is absent. HDobby Gamma stores the chosen process's image path internally so the rule survives a changed PID. Select it again if its installation path changes. The gamma and mouse process choices are independent.

Closing or minimizing the window keeps the app running in the tray. Double-click the tray icon to reopen it, or right-click for release and exit commands. Settings are stored in `%LOCALAPPDATA%\HDobbyGamma\settings.json`.

### Limitations

Program gamma changes the **entire monitor**, not only the selected window. Windows `SetDeviceGammaRamp` writes a display gamma ramp. Some drivers share a LUT between outputs; when HDobby Gamma detects that, it rolls back individual-display changes. HDR, Night light, calibration tools, Remote Desktop, some fullscreen games, and graphics drivers can ignore or replace the ramp. If another program takes over the ramp, HDobby Gamma leaves that program's setting intact.

The app reads the ramp back after applying it and reports unverified results. Drivers can reject or clamp values near `0.20` and `5.00`. Check saved monitor values after hardware, port, dock, or driver changes.

### Build

Install .NET SDK `10.0.400` or a compatible `10.0.4xx` patch and run `./build.ps1` in PowerShell. The self-contained executable is written to `dist\HDobbyGamma.exe`.
