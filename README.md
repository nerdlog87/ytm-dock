# YTM Dock

**바탕화면 모서리에 시계 위젯처럼 떠 있는 유튜브 뮤직 미니 플레이어.** 브라우저 탭 없이, 로그인 때 자동으로 켜지고, 전역 핫키로 조작합니다.

*A tiny always-on-top YouTube Music mini player for Windows — no browser tabs, launches at login, global hotkeys. English notes are at the bottom.*

```
┌────────────────────────────────────────────────┐
│ [앨범아트] 곡 제목 (길면 흐름) ⏮ ▶ ⏭  🔊     │  440×64
│           아티스트 · 앨범                       │
└────────────────────────────────────────────────┘
```

> 상태: **v0.1.1** (2026-10) · Windows 10 21H2+ / 11 · 개발 과정은 [NERD LOG 개발노트](https://nerdlog.kr) 에 연재

## 기능

- 플레이어 바만 남긴 **미니 모드**(440×64) ↔ 더블클릭으로 **전체 모드**(유튜브 뮤직 원래 화면)
- 항상 위 · 드래그 이동 · 위치 기억 · **위치 고정**
- **트레이 상주** (작업 표시줄엔 안 나옴) — 닫기(Alt+F4)는 숨기기, 종료는 트레이 메뉴
- **로그인 시 자동 실행** (트레이 메뉴에서 토글)
- **전역 핫키** (다른 창에 있어도 동작, 설정 창에서 변경)

  | 동작 | 기본값 |
  |---|---|
  | ⏮ 이전 곡 | `Ctrl+Alt+Z` |
  | ⏭ 다음 곡 | `Ctrl+Alt+X` |
  | ⏯ 재생 / 정지 | `Ctrl+Alt+C` |
  | 👁 위젯 보이기 / 숨기기 | `Ctrl+Alt+M` |
  | 🔊 볼륨 +5 / −5 | `Ctrl+Alt+↑` / `Ctrl+Alt+↓` |

- **알람 자동 재생** — 정한 시각에 플레이리스트를 재생. 알람은 여러 개, 요일별로 등록 가능
  - PC 가 켜져 있으면 그 시각에 울리고, 꺼져 있거나 절전 중이었다면 켜거나 깨우는 순간 대신 울림(기본 60분 안)
  - 알람마다 하루 한 번만 · 이미 음악이 나오고 있으면 끊지 않음 · 잠금 화면에선 풀 때까지 미루기(선택)
- 볼륨 아이콘에 마우스를 올리면 슬라이더가 옆으로 쏙 나옴
- 긴 곡 제목은 자동으로 좌우 스크롤
- 상단에 얇은 **타임라인**(클릭·드래그로 탐색)
- **곡 바뀔 때 잠깐 보여주기** — 숨겨 둔 상태에서 곡이 바뀌면 4초간 스르륵 떠올랐다 사라짐 (포커스는 안 뺏음, 트레이에서 끄기 가능)
- 등장/퇴장 슬라이드 애니메이션, Windows 11 둥근 모서리
- 로그인은 한 번만 — 프로필이 `%AppData%\YtmDock\profile` 에 유지됨

## 설치

### 실행 파일로 (권장)

[Releases](https://github.com/nerdlog87/ytm-dock/releases) 에서 `YtmDock.exe` 를 받아 원하는 폴더에 두고 실행하세요.
`framework-dependent` 버전은 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 이 필요하고, `self-contained` 버전은 그냥 실행됩니다. WebView2 런타임은 Windows 11 에 기본 포함돼 있습니다(Windows 10 은 보통 Edge 와 함께 설치돼 있음).

처음 실행하면 창이 오른쪽 아래에 뜹니다. 더블클릭해서 전체 모드로 바꾼 뒤 구글 로그인을 하세요.

### 소스에서

```powershell
winget install Microsoft.DotNet.SDK.10
git clone https://github.com/nerdlog87/ytm-dock.git
cd ytm-dock\src\YtmDock
dotnet run
```

가장 쉬운 방법 — 저장소 폴더에서 **더블클릭**:

| 파일 | 하는 일 |
|---|---|
| `run.cmd` | 켜져 있는 YTM Dock 을 끄고 소스에서 바로 실행 (고친 코드 확인용) |
| `build.cmd` | exe 두 종류를 만들어 `publish\release` 에 모으고 폴더를 열어 줌 |

PowerShell 로 직접 하려면 (저장소 루트에서):

```powershell
powershell -ExecutionPolicy Bypass -File .\publish.ps1                 # 작은 exe (.NET 10 Desktop Runtime 필요)
powershell -ExecutionPolicy Bypass -File .\publish.ps1 -SelfContained  # 런타임 포함 exe
```

> 윈도우 기본 실행 정책이 `.ps1` 을 막기 때문에 `-ExecutionPolicy Bypass` 를 붙입니다(그 한 번만 우회, 시스템 설정은 그대로).

## 사용법

| 하고 싶은 것 | 방법 |
|---|---|
| 창 옮기기 | 플레이어 바의 빈 곳을 끌기 |
| 미니 ↔ 전체 | 빈 곳 더블클릭, 또는 오른쪽 아래 `⤢` 버튼, 전체 모드에서 `Esc` |
| 숨기기 / 다시 보기 | `Ctrl+Alt+M`, 또는 트레이 아이콘 더블클릭 |
| 핫키 바꾸기 | 트레이 우클릭 → **핫키 설정…** → 칸 클릭 후 조합 누르기 |
| 알람 자동 재생 | 트레이 → **알람 설정…** → **＋ 알람 추가** → 시각·요일·플레이리스트 주소(또는 **지금 열린 목록**) → 저장. **▶ 테스트** 로 미리 들어 보기 |
| 종료 | 트레이 우클릭 → **종료** |

## 설정 파일

`%AppData%\YtmDock\settings.json` (트레이 메뉴 **설정 파일 열기** 로 바로 열림)

```jsonc
{
  "window":  { "x": 1460, "y": 980, "locked": false },
  "startup": { "runAtLogin": true },
  "hotkeys": {
    "prev": "Ctrl+Alt+Z", "next": "Ctrl+Alt+X", "playPause": "Ctrl+Alt+C",
    "toggleVisible": "Ctrl+Alt+M",
    "volumeUp": "Ctrl+Alt+Up", "volumeDown": "Ctrl+Alt+Down", "volumeStep": 5
  },
  "alarms": {
    "enabled": true,
    "catchUpMinutes": 60,       // 놓친 알람을 대신 울려 주는 시간
    "skipIfPlaying": true,      // 이미 재생 중이면 끊지 않기
    "deferWhileLocked": false,  // 잠금 화면에선 풀 때까지 미루기
    "items": [
      { "id": "a1b2c3d4", "enabled": true, "time": "08:00",
        "days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
        "playlistUrl": "https://music.youtube.com/playlist?list=...",
        "lastFired": "2026-10-16" }   // 하루 한 번만 울리기 위한 기록 (자동)
    ]
  },
  "peek": { "enabled": true, "seconds": 4 },
  "startUrl": "https://music.youtube.com/",
  "cssOverride": null,   // 아래 참고
  "debug": false         // true 면 F12 개발자 도구 허용
}
```

핫키 이름은 WPF `Key` 열거형을 그대로 씁니다 — `Space`, `Up`, `F9`, `MediaPlayPause`, `MediaNextTrack`, `OemTilde` 등. 미디어 키를 등록하면 다른 음악/영상 앱은 그 키를 받지 못합니다.

## 유튜브 뮤직 UI 가 바뀌어 미니 모드가 깨졌을 때

이 앱은 공식 웹 플레이어(music.youtube.com)를 그대로 띄우고 **CSS 로 플레이어 바만 남기는** 방식입니다. 그래서 유튜브가 마크업을 바꾸면 미니 모드가 어긋날 수 있습니다. 이때는 코드를 고치지 않고 설정으로 덮어쓸 수 있습니다.

1. `settings.json` 에 `"debug": true` 를 넣고 재시작 → 미니 창에서 **F12** 로 개발자 도구를 엽니다.
2. [`src/YtmDock/Web/ytm-mini.css`](src/YtmDock/Web/ytm-mini.css) 를 복사해 셀렉터를 고칩니다. 파일 머리말에 2026-10 기준 DOM 구조와 "왜 이렇게 했는지"가 적혀 있습니다.
3. 고친 CSS 전체를 `"cssOverride": "..."` 에 한 줄 문자열로 넣습니다(줄바꿈은 `\n`).
4. 잘 되면 PR 로 보내 주세요. 🙏

알아두면 좋은 것들 (삽질 끝에 알아낸 것):

- `ytmusic-player-bar` 는 페이지에 **두 개** 있다. 진짜 하단 바는 `ytmusic-app-layout > ytmusic-player-bar`.
- 하단 바는 flex 가 아니라 **grid** 다.
- 창 폭이 좁으면 유튜브 뮤직이 JS 로 '컴팩트 바'(재생/다음만)로 바꿔 버린다. 그래서 미니 모드에서는 WebView2 를 창보다 넓은 **1400px** 로 두고(볼륨 슬라이더가 나타나는 최소 폭) CSS 로 440px 에 욱여넣는다.
- 볼륨 슬라이더 값은 실제 볼륨과 다르다(청감 곡선, 슬라이더 90 ≈ 볼륨 74). 핫키는 슬라이더 값을 바꾸고 `change` 를 쏜다.

## 변경 기록

- **v0.1.1** — '아침 자동 재생'(08:00 이전에 켜면 재생)을 **알람 자동 재생**으로 교체. 켜 둔 PC 에서도 정각에 울리고, 여러 개를 요일별로 등록. v0.1 설정은 같은 시각의 알람 하나로 자동 이전.
- **v0.1.0** — 첫 공개

## 한계 (v0.1)

- **투명도·블러·진짜 페이드가 안 된다.** WebView2 는 별도 HWND 이고 Chromium 이 DirectComposition 으로 직접 그리기 때문에, WPF `Opacity` 는 물론 Win32 레이어드 윈도우(`WS_EX_LAYERED` 알파)도 무시한다(실측). 그래서 등장/퇴장은 슬라이드 + 페이지 안 CSS 페이드로 대신했다. 둥근 모서리만 DWM(`DWMWA_WINDOW_CORNER_PREFERENCE`)으로 해결. 나머지는 v0.2 에서 WebView2 를 화면 밖에 숨기고 네이티브 WPF 창이 SMTC 로 정보를 받아 그리는 구조로 간다.
- 유튜브 뮤직 DOM 에 의존한다(위 참고).
- 알람은 YTM Dock 이 실행 중일 때만 울린다. PC 를 **절전에서 깨우는 기능은 없다**(작업 스케줄러의 '절전 모드 해제' 같은 권한이 필요해 넣지 않음). 그래서 놓친 알람은 켜거나 깨우는 순간 대신 울린다.
- 윈도우 11 '위젯 보드'(Win+W)에는 들어가지 않는다 — 그 보드는 정적 카드만 허용해 오디오 재생이 불가능.
- 미디어 키는 윈도우가 '가장 최근에 소리 낸 앱'에 보내므로, 다른 영상을 보고 나면 그쪽이 가져간다. 그래서 기본 핫키는 `Ctrl+Alt+…` 조합.

## 보안 · 개인정보

- exe 에는 **어떤 계정 정보도 들어 있지 않습니다.** 구글 로그인 쿠키는 각자 PC 의 `%AppData%\YtmDock\profile` 에만 저장됩니다(브라우저 프로필과 같은 성격 — 이 폴더는 남에게 보내지 마세요). 설정도 `%AppData%\YtmDock\settings.json` 에만 있습니다.
- 네트워크 통신은 WebView2 가 music.youtube.com 과 하는 것뿐이며, 이 앱은 자체 서버·통계·업데이트 확인이 없습니다.
- 관리자 권한을 요구하지 않습니다. 건드리는 곳은 `HKCU\...\Run`(로그인 시 실행, 사용자 범위), `%AppData%\YtmDock`, 전역 핫키 등록이 전부입니다.
- exe 는 **코드 서명이 없어서** 처음 실행 때 Windows SmartScreen 이 "PC 를 보호했습니다" 를 띄울 수 있습니다. `추가 정보 → 실행` 으로 진행하거나, 못 믿겠으면 소스에서 직접 빌드하세요(위 '소스에서').
- 소스는 전부 이 저장소에 있고, 외부 의존성은 Microsoft 의 `Microsoft.Web.WebView2` 패키지 하나입니다.

## 동작 원리 / 면책

YTM Dock 는 유튜브 뮤직을 수정하거나 우회하지 않습니다. 윈도우 내장 WebView2(Edge 엔진)로 공식 웹 플레이어를 그대로 표시하고, 사용자 스타일시트와 창 꾸밈만 제공합니다. 광고·프리미엄 정책은 웹과 동일하게 적용됩니다. 비공식 프로젝트이며 Google 과 무관합니다.

## License

MIT

---

## English

YTM Dock hosts the official YouTube Music web player in a frameless, always-on-top WebView2 window and hides everything except the player bar with injected CSS. It lives in the system tray, starts at login, remembers its position, and exposes global hotkeys (prev / next / play-pause / show-hide / volume ±) that you can rebind from the tray menu. Alarms (any number, per weekday) start a playlist at a set time; if the PC was off or asleep at that moment, the missed alarm fires when you boot or wake it (within 60 min by default), once per day.

Build: .NET 10 SDK → `dotnet run` in `src/YtmDock`, or `powershell -ExecutionPolicy Bypass -File .\publish.ps1 [-SelfContained]` for a single exe. Settings live in `%AppData%\YtmDock\settings.json`; if YouTube changes its markup, drop a fixed stylesheet into `cssOverride` (see the Korean section above for the DOM notes). Not affiliated with Google.
