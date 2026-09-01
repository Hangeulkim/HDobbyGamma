# HDobby Gamma

A small, portable Windows 11 gamma utility with per-monitor control, by HDobby (Hangeulkim).

[English](#english) · [한국어](#한국어)

## English

### Download

Download the latest `HDobbyGamma-*-win-x64.exe` from
[GitHub Releases](https://github.com/Hangeulkim/HDobbyGamma/releases/latest).
It is one self-contained executable: no separate .NET installation or administrator permission is required.
The release is not code-signed, so Windows SmartScreen may show an unknown-publisher warning on first launch.

### Features

- xgamma-style `0.20`–`5.00` range; `1.00` is neutral
- all-monitor or individual-monitor selection
- stable physical-monitor identifiers instead of relying only on changing `DISPLAY1/2` numbers
- logarithmic slider with `1.00` at the exact center and direct numeric input
- Korean and English interface with an in-app language selector
- remembers each monitor's last value and reapplies it on the next launch
- optional start with Windows; startup launches quietly in the system tray
- closing or minimizing the window keeps the app running in the tray
- double-click the tray icon to reopen, or use `Open` and `Exit completely`
- native read-back verification after gamma changes
- automatic rollback and warning if a driver exposes one shared LUT for multiple displays
- optional restoration of pre-launch colors when fully exiting

### Usage

1. Select `All monitors` or one display.
2. Move the slider or enter a value directly.
3. Enable `Start with Windows` if you want the saved values reapplied automatically.
4. Double-click the tray icon to reopen the window, or right-click it for `Open` and `Exit completely`.

Settings are stored in `%LOCALAPPDATA%\HDobbyGamma\settings.json`.

### Important limitations

HDobby Gamma uses the legacy Windows `GetDeviceGammaRamp` and `SetDeviceGammaRamp` APIs.
HDR, Night light, ICC/color-calibration tools, graphics drivers, Remote Desktop, and some
exclusive-fullscreen games may override or ignore these gamma ramps. Some drivers expose one shared
hardware lookup table for multiple outputs, so truly independent control is not possible on every GPU.
HDobby Gamma verifies the value read back from Windows and rolls back detected linked-display changes.
The generated curves stay inside Windows' documented safety envelope. A driver may still reject or
clamp extreme values near `0.20` or `5.00`; the app reports an unverified result instead of claiming success.

A monitor's device-interface identity is more stable than `DISPLAY1/2`, but Windows or a driver may
still assign a new identity after major hardware, port, dock, or driver changes. Review saved values
after changing display hardware. Hardware gamma is normally applied after desktop capture, so ordinary
screenshots do not record the real on-monitor brightness change.

### Build from source

Install .NET SDK `10.0.400` or a compatible `10.0.4xx` patch, then run:

```powershell
.\build.ps1
```

The self-contained executable is written to `dist\HDobbyGamma.exe`.

---

## 한국어

Windows 11에서 전체 화면 또는 모니터별 감마를 조절하는 HDobby(Hangeulkim)의 휴대용 프로그램입니다.

### 다운로드

[GitHub Releases](https://github.com/Hangeulkim/HDobbyGamma/releases/latest)에서 최신
`HDobbyGamma-*-win-x64.exe`를 받으면 됩니다. 단일 실행 파일이며 별도의 .NET 설치나
관리자 권한이 필요하지 않습니다.
코드 서명이 없는 배포본이므로 처음 실행할 때 Windows SmartScreen에 알 수 없는 게시자 경고가 표시될 수 있습니다.

### 기능

- xgamma 방식의 `0.20`~`5.00` 범위, `1.00`은 기본값
- 전체 모니터 또는 개별 모니터 선택
- 바뀔 수 있는 `DISPLAY1/2` 번호 대신 물리 모니터 식별자로 설정 저장
- 중앙이 정확히 `1.00`인 로그형 슬라이더와 숫자 직접 입력
- 프로그램 안에서 전환하는 한국어·영어 UI
- 모니터별 마지막 값을 기억하고 다음 실행 시 다시 적용
- Windows 시작 시 자동 실행 옵션, 자동 실행 시 트레이에서 조용히 시작
- 창의 `X` 또는 최소화 버튼을 눌러도 종료하지 않고 트레이에서 계속 실행
- 트레이 아이콘 더블클릭으로 창 열기, 우클릭 메뉴의 `열기`와 `완전히 종료`
- 감마 변경 후 Windows에서 실제 값을 다시 읽어 확인
- 여러 모니터가 하나의 LUT를 공유하면 감지된 변경을 자동 원상복구하고 경고
- 완전히 종료할 때 프로그램 실행 전 색상으로 복원하는 옵션

### 사용법

1. `모든 모니터` 또는 원하는 화면 하나를 선택합니다.
2. 슬라이더를 움직이거나 숫자를 직접 입력합니다.
3. 다음 부팅에도 자동 적용하려면 `Windows 시작 시 자동 실행`을 켭니다.
4. 창을 다시 열려면 트레이 아이콘을 더블클릭하고, `열기` 또는 `완전히 종료`는 우클릭 메뉴를 사용합니다.

설정은 `%LOCALAPPDATA%\HDobbyGamma\settings.json`에 저장됩니다.

### 알아둘 점

HDobby Gamma는 Windows의 `GetDeviceGammaRamp`와 `SetDeviceGammaRamp`를 사용합니다.
HDR, 야간 모드, ICC/색상 보정 프로그램, 그래픽 드라이버, 원격 데스크톱, 일부 독점
전체화면 게임은 감마 설정을 덮어쓰거나 무시할 수 있습니다. 그래픽 드라이버가 여러
출력에 하나의 하드웨어 LUT를 공유하면 완전한 개별 조절이 불가능할 수 있습니다.
HDobby Gamma는 Windows에서 다시 읽은 결과를 확인하고, 감지된 공유 화면 변경은 되돌립니다.
생성 곡선은 Windows가 문서화한 안전 범위 안으로 제한합니다. 그래도 `0.20`이나 `5.00`에
가까운 극단값은 드라이버에서 거부되거나 제한될 수 있으며, 이 경우 프로그램은 성공 대신
확인 실패를 표시합니다.

물리 모니터 식별자는 `DISPLAY1/2`보다 안정적이지만, 큰 하드웨어·포트·도킹·드라이버
변경 뒤에는 Windows가 새 식별자를 줄 수 있습니다. 화면 구성을 바꾼 뒤 저장값을 확인하세요.
하드웨어 감마는 일반적으로 화면 캡처 이후 단계에 적용되므로 일반 스크린샷에는 실제
모니터 밝기 변화가 기록되지 않습니다.
