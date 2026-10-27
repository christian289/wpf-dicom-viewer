# 검증 기록

이 문서는 저장소의 주장과 실행 근거를 한 곳에 정리한다. 각 결과에는 **범위**(작업 트리인지, 특정 커밋인지, 격리 복사본인지)와 **상태**를 함께 적는다.

상태 값:

- **직접 실행**: 이 문서의 작업 중 명령을 실행하여 얻은 결과.
- **통합 빌드 보고**: 같은 저장소의 작업 트리에서 통합 과정에 실행되어 보고된 결과. 이 문서 작성자가 재실행하지 않았다.
- **과거 커밋**: 특정 커밋에서 실행한 기록. 현재 작업 트리나 최종 커밋의 증거가 아니다.
- **미실행**: 아직 실행하지 않았다.

## 1. 현재 상태

- 기준: 커밋 `aef1897` (G, `aef18975c226e82e5db43c7b240f8d8d19259d92`)이 canonical 동결 코드 SHA다. 이 증거 커밋(docs-only)은 source·build·data·rights 코드를 바꾸지 않는다.
- 최종 익명 클린 클론 검증: **실행 완료 (G)**. 5절 절차, 결과는 E19.
- 대화형 UI: 승인된 3개 시나리오가 각각 별도 실행에서 통과했다(E20). 세 시나리오를 한 번에 묶은 배치 실행은 없다. G 소스에서 1개 케이스를 fresh로 실행해 통과했다(E24, 번호·범위 안정성 검사).
- 일정: 후보 동결 기록 **2026-10-27** (코드 SHA는 G). 체크포인트 2026-10-29 이전에 기록한다. 게시 마감 **2026-10-30**.
- 진행: G 코드 커밋 → 공개 push → 익명 클린 클론 검증 → 이 증거 문서(E2, docs-only) 커밋과 tag. 이후 추가 변경은 별도 기록한다.

## 2. 증거 표

| # | 항목 | 범위 | 명령 또는 방법 | 결과 | 상태 |
| --- | --- | --- | --- | --- | --- |
| E1 | 고정 자료 LFS 객체 | 작업 트리 | `git lfs ls-files` | 150 | 직접 실행 |
| E2 | 자료 오프라인 검증 | 격리 복사본 (아래 주석) | `dotnet run --project tools/DicomViewer.DatasetTool -c Release --no-build --no-restore -- verify data/dicom/vhp-female-ankle-v1/manifest.json` | 150 파일, 78,793,078 bytes, 종료 코드 0 | 직접 실행 |
| E3 | WpfApp Release 빌드 | 격리 복사본 | `dotnet build src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release --no-restore` | 경고 0, 오류 0 | 직접 실행 |
| E4 | DatasetTool Release 빌드 | 격리 복사본 | 같은 방식 (`tools/DicomViewer.DatasetTool`) | 경고 0, 오류 0 | 직접 실행 |
| E5 | UiTests 컴파일 | 격리 복사본과 작업 트리 | `dotnet build src/DicomViewer.UiTests/DicomViewer.UiTests.csproj -c Release --no-restore` | 경고 0, 오류 0 | 직접 실행 |
| E6 | UiTests 게이트 | 격리 복사본 | `DICOM_VIEWER_RUN_UI_TESTS=0` 설정, `DICOM_VIEWER_APP_EXE` 미설정, `dotnet test ... --no-restore` | 실행 0, 건너뜀 3, 종료 코드 0. 앱 실행 없음 | 직접 실행 (게이트 검증이며 UI 통과가 아님) |
| E7 | 전체 솔루션 Release 빌드 | 작업 트리 (G 최종 소스) | `dotnet build src/DicomViewer.slnx -c Release` | 오류 0. 경고 집계 9 (OpenSilver 계열만: OpenSilverApp CS8604 1건, NU1507 3개 프로젝트, 요약에서 중복 집계됨). 앱·핵심 경고 0 | 직접 실행 |
| E8 | 전체 테스트 (단위·통합, UiTests 제외) | 작업 트리 (G 최종 소스) | `dotnet test src/DicomViewer.Tests/DicomViewer.Tests.csproj -c Release --no-build --no-restore` | 273 실행, 실패 0, 건너뜀 0, 전체 273, exit 0 | 직접 실행 |
| E9 | 포맷 검사 | 변경된 C# 파일 3개 (ViewModels 1, Tests 2) | `dotnet format <프로젝트> --verify-no-changes --no-restore --include <파일>` (프로젝트별 실행) | 두 프로젝트 모두 exit 0 (변경 필요 없음) | 직접 실행 |
| E10 | XAML 서식 | 과거 Generic.xaml 및 현재 변경된 XAML | 과거: `dotnet dnx -y XamlStyler.Console@3.2501.8 -- -f src/DicomViewer.UI/Themes/Generic.xaml -c Settings.XamlStyler --write-to-stdout`. 현재: 통합 측 검사(명령 옵션은 보고되지 않음) | 과거: stdout 미리보기 실행, 원본 미수정. 현재: 변경된 XAML PASS 보고(검사한 파일 목록에 한정) | 과거 커밋 / 통합 빌드 보고 |
| E11 | 앱 시작 (읽기 전용) | 작업 트리 빌드 | 앱 실행 후 관찰, 입력 없음 | 150 슬라이스 자료 표시, 192 DPI(200%)에서 시작, 테마 리소스 적용 | 통합 빌드 보고 |
| E12 | 앱 음성 시작 (읽기 전용) | 일회용 복사본 2개 | 복사본 1: `manifest.json` 없음. 복사본 2: `sanitized/0001.dcm`이 LFS 포인터 | 두 경우 모두 Error/Retry 안내, 프레임 0, 시리즈 0, 슬라이스 0, 비정상 종료 없음. 관찰 시점에 소유 프로세스의 TCP 연결 0 (살아 있는 상태에서 관찰). 이는 네트워크 없는 생명주기 증명이 아님 | 통합 빌드 보고 |
| E13 | 창 닫기 생명주기 | 작업 트리 빌드 | 정상 닫기 | 종료 코드 0 (닫기 중 재진입 수정 후) | 통합 빌드 보고 |
| E14 | 멀티프레임 로드·메타데이터 거부 | 단위 테스트 | `SyntheticDicomLoadTests` (합성 DICOM) | E8 안에 포함 | 통합 빌드 보고 |
| E15 | LFS 원격 복원 | 원격 커밋 `1506f84` | `GIT_LFS_SKIP_SMUDGE=1`로 clone, 빈 캐시 확인, `git lfs pull --include "data/dicom/vhp-female-ankle-v1/**"`, verify | 캐시 0 → 150개 수신 → verify 종료 코드 0 | 과거 커밋 |
| E16 | LFS 로컬 복원·음성 경로 | 로컬 커밋 `3dd5dc7` | 포인터만 있을 때, 객체 누락, 객체 손상 각각 verify | 종료 코드 1 (각각 "unresolved pointer", "missing", "SHA-256 differs"), 복원 후 종료 코드 0 | 과거 커밋 |
| E17 | 기준 자료 독립 확인 | 저장 사본 150개 | pydicom 3.0.1 파싱, 원본은 파싱하지 않음 | 150/150 통과 (전송 구문, CT SOP, 512×512, 16비트, 단일 프레임) | 과거 커밋 |
| E18 | 압축 구문 거부 | — | — | — | 미실행 |
| E19 | 익명 공개 클린 클론 최종 검증 | G `aef18975c226e82e5db43c7b240f8d8d19259d92`, 공개 HTTPS, 개인 설정·토큰 없음 | 5절 절차: 임시 TEMP clone, checkout 후 cwd·HEAD assert, 빈 LFS 캐시 0, 포인터 150개 manifest 대조, `lfs pull` 150, `lfs fsck`, 파일별 크기·SHA-256 대조, 공개 전용 NuGet 설정으로 restore(3개 프로젝트), build, DatasetTool verify, core test | 캐시 0 → 150. 포인터 150개 manifest 대조 통과, 전체 78,793,078 bytes, `lfs fsck` OK, dataset verify exit 0 (150 파일, 78793078 bytes), SDK 10.0.403, restore 3개 exit 0, DatasetTool·WpfApp·Tests build 경고 0 오류 0, core TRX 273 total / 273 passed / 0 failed / 0 notExecuted. 원본 로그 파일은 보존하지 않았고, 값은 실행 중 캡처한 stdout 기준이다 | 직접 실행 (G 기준 1회) |
| E20 | 대화형 UI, 승인된 3개 시나리오 (별도 실행) | 소유 PID 창, 작업 트리 Release 빌드 | 게이트 `DICOM_VIEWER_RUN_UI_TESTS=1`, 절대 exe 경로, `dotnet test --no-build --no-restore`, 케이스별 `--filter` | (a) 시작·첫 프레임·빠른 슬라이더(74→149→0→74, 표시 75 안정 1.5 s): **통과** (run2 및 단독 실행), 캡처 1장(임시 폴더, 검토 대기). (b) 변환·드래그·W/L 리셋·프리셋: **통과** (run2 한 번). (c) 측정: **통과** (단독 실행, 라벨 49.99 mm, 기대 50 mm, 허용 2/fit ≈ 1.18 mm, 정상 종료 코드 0). 주의: (a), (b)는 하네스의 최종 소유 프로세스 수정(Process 소유·종료 코드 읽기)보다 앞선 실행에서 통과했다. 최종 하네스로 (a), (b)를 다시 실행한 기록은 없다. 세 시나리오 배치 실행은 아님 | 직접 실행 (a·b는 이전 하네스, c는 최종 하네스) |
| E21 | 측정 케이스 수동 활성화 기록 | 소유 PID 창 | `DICOM_VIEWER_UI_MANUAL_FOREGROUND_WAIT_SECONDS`(기본 0, 최대 600) | 이력: 120 초 대기에서 사용자 활성화 없이 시간 초과(입력 없음, 정리 완료). 이후 600 초 대기에서 사용자가 소유 창을 활성화하여 측정 통과(E20 c). 전경 활성화는 사용자 클릭 필요. 자동 재시도 없음 | 직접 실행 (이력 포함) |
| E22 | 선택 해제 중 로드 회귀 (F 수정) | 작업 트리 (F 커밋 직전) | 신규 테스트 `Clearing_the_selection_during_a_pending_load_publishes_empty_and_a_late_result_stays_ignored`: 수정 전 실패(`Expected h.Vm.IsLoading to be False, but found True`), 최소 수정(`ClearSeries`에서 취소 시 `IsLoading = false`) 후 통과. ViewerViewModelTests 26/26 통과 | 전체 core 테스트 **268 통과, 실패 0, 건너뜀 0** (F 이전 통합 보고 267에 신규 1개 추가). 솔루션 Release 빌드: 오류 0, 경고 6(기존 OpenSilver). WpfApp Release 빌드: 오류 0, 경고 0. 실제 앱 읽기 전용 시작 smoke: 자체 실행 프로세스 1개, 입력 없음, SliceTotal 150, DatasetId `vhp-female-ankle-v1`, MeasurementUnit `Unit: mm`, WindowLevelReadout `W 400 / L 40`, SliceNumber 1, StatePanel 숨김, 창 닫기 후 종료 코드 0, 잔존 프로세스 없음 | 직접 실행 (F 커밋 대상 소스 기준) |
| E23 | 로드 중 W/L 교체 경합 (G 수정) | 작업 트리 (G 최종 소스) | 로드 중 렌더 취소 후 `Load.finally`가 `IsLoading=false`를 잘못 쓰던 경합. 수정: 로드가 자기 영상의 최신 렌더 완료까지 대기(epoch·`IsStale` 확인), `CancelRenders`에서 `_latestRender` 해제. 회귀 테스트 5개 의미 있는 항목 포함 | 수정 전 RED 1/31 실패, 수정 후 `ViewerViewModelTests` 31/31 × 3 통과. 전체 core 273 통과, 실패 0, 건너뜀 0. 소스 스모크(실제 fo-dicom, 고정 150 자료, 단일 UI 컨텍스트, 창 없음): 무작위 슬라이스·W/L 2×400 반복에서 stuck 0, notReady 0, finalWrong 0. 선택 해제 동기 호출 내 `PropertyChanged` 8건 일시 Ready 관찰(같은 UI 턴, 최종 Empty·loading false·frame none)은 위반 0으로 뭉개지 않고 별도 기록. 수정 전 비교(약 241–247 위반)는 이전 동등 헬퍼 버전이며 현재 코드의 주장이 아님 | 직접 실행 (소스·단위 수준; UI 창 실행 아님) |
| E24 | UI 1개 케이스 fresh 실행 (G) | G 클론 빌드의 UiTests, 절대 exe 경로 | `DICOM_VIEWER_RUN_UI_TESTS=1`, `DICOM_VIEWER_APP_EXE`에 클론 exe 절대 경로, 수동 포그라운드 대기 변수 미설정(기본 0), `dotnet test --no-build --no-restore --filter FullyQualifiedName~Startup_WaitsForFirstFrame_ThenRapidSliderSettlesOnLastRequest` | 1건 실행, 1 통과, 0 실패, 0 건너뜀, 약 5초, 실행 후 잔존 앱 프로세스 0. 입력 범위: 승인된 하네스가 소유 프로세스의 `SliceSlider`를 UIA RangeValue로 74 → 149 → 0 → 74 설정하고, 표시 75가 1.5초 안정적인지 관찰한 뒤 소유 창만 임시 폴더에 캡처한다. 이 케이스에는 마우스·키보드 입력이 없다(다른 두 케이스의 드래그·클릭 입력과 구분). 하네스 설계상 캡처 직전 소유 창 전경 PID 검사를 하며, 이 실행의 검사 통과 로그는 보존하지 않았다. 검증 범위: 마지막 요청 후 번호·범위 안정성. 최종 bitmap의 픽셀 동일성은 보증하지 않음 | 직접 실행 (G 기준 1건, 3시나리오 배치 아님) |
| E25 | G 클론 빌드의 읽기 전용 시작 (클론 밖 작업 디렉터리) | G 클론 빌드 WpfApp Release exe 절대 경로, 작업 디렉터리는 클론 밖 임시 폴더 | 앱을 직접 시작하고 UIA로 값만 읽는다(입력 주입 없음, 포그라운드 조작 없음). 종료는 `CloseMainWindow`(WM_CLOSE) | SliceTotal 150, DatasetId `vhp-female-ankle-v1`, SliceNumber 1, WindowLevelReadout `W 400 / L 40`, MeasurementUnit `Unit: mm`. StatePanel과 LoadingIndicator는 UIA 트리에 없어 숨김으로 판단. WM_CLOSE 요청 true, exit code 0, 잔존 pid 없음 | 직접 실행 (G, 입력 없음) |

**주석 (E2, E3, E4, E5, E6):** 격리 복사본은 작업 트리의 `src`, `tools`, `data`와 빌드 설정 파일만 복사한 것이다. `.git`, `.claude`, `.serena`, `bin`, `obj`, 사용자 설정 파일과 비밀 파일은 제외했다. 복사본은 `.git` 내부 임시 폴더에 두었고 원본 `bin`·`obj`는 건드리지 않았다. 공개 NuGet 소스만 쓰는 `NuGet.Config`를 `--configfile`로 지정했고, `DOTNET_CLI_HOME`과 `NUGET_PACKAGES`는 복사본 안으로 격리했다. 토큰 환경 변수는 해제했다. 이것은 **클론이 아니며** 최종 커밋 증거가 아니다.

**주석 (E15, E16):** 과거 커밋의 증거다. 현재 작업 트리 또는 최종 커밋의 증명이 아니다.

**주석 (E7, E8, E9):** G 소스에서 이 세션이 직접 재실행했다. **주석 (E11, E12, E13):** 통합 빌드 측 보고 결과이며 G에서 재실행하지 않았다.

## 3. 자료 범위

- 출처: Harvard Dataverse DOI `10.7910/DVN/3JDZCT`, 버전 1.0. 라이선스 CC0-1.0, 출처 표시 "Courtesy of the U.S. National Library of Medicine".
- 내용: 체중부하가 아닌 사체 발목 CT. WBCT·CBCT·진단용이 아니다.
- 저장: 150개 파일, 합계 78,793,078 bytes, Git LFS.
- 원본 해시: 수집 도구가 원본을 메모리에서 읽어 PixelData·기하 해시를 저장 사본과 대조했다. 이 대조는 수집 시점에 한 번 수행되었다.
- `verify`의 범위: 원본 출처에 접속하지 않는다. 저장 사본의 SHA-256, 크기, 픽셀·기하 지문을 매니페스트와 대조한다. 원본 값을 다시 계산하지 않는다.
- pydicom 독립 확인은 저장 사본 150개에만 수행했다. 원본은 pydicom으로 파싱하지 않았다.

## 4. UI 검증 범위 (설계와 승인된 실행 결과)

- 프로젝트: `src/DicomViewer.UiTests` (xUnit + FlaUI.UIA3 5.0.0, 루트 CPM). 기본 `dotnet test` 실행에 포함되지 않는다.
- 게이트: `DICOM_VIEWER_RUN_UI_TESTS=1`과 `DICOM_VIEWER_APP_EXE`(빌드된 `DicomViewer.WpfApp.exe` 경로)가 모두 있어야 실행된다. 없으면 Skip으로 보고한다.
- 프로세스: 테스트가 직접 시작한 앱 프로세스 하나만 다룬다. 다른 창과 데스크톱 전체 캡처는 하지 않는다. 작업 디렉터리는 임시 폴더다.
- 입력 가드: 마우스 클릭·드래그 직전에 소유 창을 전경으로 올리고 전경 PID가 소유 PID인지 확인한다(소유 창 캡처 직전에도 같은 확인). 아니면 입력하지 않고 실패한다. 좌표는 소유 창 경계 안이어야 한다. 드래그 버튼은 `finally`에서 해제한다. 키보드 입력은 보내지 않는다. 슬라이더는 UIA RangeValue 패턴으로 설정한다.
- 대기: 첫 프레임 게시(`MeasurementUnit` = `Unit: mm`, `StatePanel` 숨김, `LoadingIndicator` 완료, 슬라이스 총수 150)를 확인한 뒤에만 조작한다. 폴링 간격 100 ms, 명시적 마감.
- 테스트 3개:
  1. 시작과 빠른 슬라이더: 연속 요청 74 → 149 → 0 → 74 후 표시 75로 안정되는지, 소유 창 캡처를 임시 폴더에 저장.
  2. 버튼 경로: 회전·반전·초기화, 숫자 readout 파싱. 드래그 경로: 소유 `ViewerSurface` 중심에서 시작해 창 안에서만 팬(`ToolPan`), 줌(`ToolZoom`), W/L(`ToolWindowLevel`, 가로는 폭, 세로는 중심) 드래그. 값은 정확한 배율이 아니라 "변했고 범위 안"(줌 10–1000%)만 확인. 이후 `ResetView`, `ResetWindowLevel`, `PresetLung`으로 바인딩 상태가 기준값으로 돌아오는지 확인. 좌표는 DPI 비인식 테스트 프로세스의 논리(가상) 좌표이며 입력도 같은 공간을 쓴다. 입력 전 소유 창 전경 확인.
  3. 알려진 1.0 mm 간격 측정: 영상 좌표 (100,100)→(130,140), 30×40 px → 50 mm, 허용 오차 2/fit mm (논리 좌표 반올림). 삭제 후 창 닫기, 종료 코드 0.
- 측정 기준 좌표: `ViewerSurface` 경계가 뷰포트와 같아야 한다. 원점은 중앙 정렬 기준이며 Pan 0,0을 확인한 뒤에만 측정한다.
- 증거 캡처: 소유 창 영역만, 임시 폴더, 저장소 밖. 공유 전에 사람이 검토한다.
- 현재 상태: 승인된 세 시나리오가 별도 실행에서 통과했다(E20 a·b·c). 세 시나리오를 한 번에 묶은 배치 실행은 없다. 측정 값은 49.99 mm, 기대 50 mm, 허용 2/fit mm(약 1.18 mm). 좌표는 DPI 비인식 테스트 프로세스의 논리(가상) 좌표이며, 물리 픽셀 기준이 아니다.
- UI 단위·ViewModel로 옮긴 항목: 미확인 간격의 px 거리, 측정 mm 계산, 최신 요청 우선(세대 번호), 폭 1 W/L 경계, VOI LINEAR 수치, 지원하지 않는 메타데이터. 기본 150 자료 UI 실행으로는 재현되지 않는다.

## 5. 최종 익명 클린 클론 절차 (G에서 실행 완료, 결과는 E19)

목적: 최종 커밋을 공개 HTTPS로 받아, 개인 설정·인증 없이 자료 150개와 빌드가 재현되는지 확인한다. 순서가 중요하다. clone과 checkout 중에는 LFS 스모지를 끄고 빈 캐시를 증명한 뒤 pull한다.

```powershell
# 0) 동결 SHA는 40자 전체 해시여야 한다. 아직 없으면 이 절차를 시작하지 않는다.
if ($env:FROZEN_SHA -notmatch '^[0-9a-f]{40}$') { throw 'Set FROZEN_SHA to the frozen 40-character commit SHA first.' }

# 1) 격리: 임시 전역 설정, 상속 설정·토큰·헤더·헬퍼 제거
$work = Join-Path $env:TEMP ("dicom-clean-" + [guid]::NewGuid())
New-Item -ItemType Directory $work | Out-Null
New-Item -ItemType File "$work\empty.gitconfig" | Out-Null
$env:GIT_CONFIG_GLOBAL = "$work\empty.gitconfig"
$env:GIT_CONFIG_NOSYSTEM = "1"
Remove-Item Env:GIT_CONFIG_COUNT, Env:GIT_CONFIG_KEY_0, Env:GIT_CONFIG_VALUE_0, Env:GIT_ASKPASS, Env:SSH_ASKPASS -ErrorAction SilentlyContinue
$env:GIT_TERMINAL_PROMPT = "0"
Remove-Item Env:GH_TOKEN, Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
$env:DOTNET_CLI_HOME = "$work\dotnet-home"
$env:NUGET_PACKAGES = "$work\nuget-packages"
# 인증 차단 래퍼: 추가 헤더와 자격 증명 헬퍼를 명령 단위로 비운다
# Auth-blocking wrapper: clears extra headers and credential helpers per command
function Git-Clean { & git -c http.extraHeader= -c credential.helper= @args }

# 2) 공개 HTTPS clone, 스모지 OFF, 동결 SHA로 checkout (LFS 객체를 받지 않음)
$env:GIT_LFS_SKIP_SMUDGE = "1"
Git-Clean clone https://github.com/christian289/wpf-dicom-viewer.git "$work\repo"
Set-Location "$work\repo"
Git-Clean checkout $env:FROZEN_SHA
git rev-parse HEAD                                   # 동결 SHA와 같아야 한다

# 3) 빈 캐시 증명: 캐시 객체 0개, 첫 파일이 포인터
(Get-ChildItem .git\lfs\objects -Recurse -File -ErrorAction SilentlyContinue | Measure-Object).Count   # 0
Get-Content data\dicom\vhp-female-ankle-v1\sanitized\0001.dcm -TotalCount 1                       # version https://git-lfs.github.com/spec/v1

# 4) 스모지 해제 후 150개 전체 pull
Remove-Item Env:GIT_LFS_SKIP_SMUDGE
Git-Clean lfs install --local --skip-repo
Git-Clean lfs pull --include "data/dicom/vhp-female-ankle-v1/**"
Git-Clean lfs fsck --objects --pointers
(git lfs ls-files | Measure-Object).Count            # 150

# 5) 공개 nuget.org만 쓰는 설정으로 도구·앱·테스트 프로젝트를 각각 명시적으로 복원
@"
<configuration>
  <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
</configuration>
"@ | Set-Content "$work\NuGet.Config"
dotnet restore tools/DicomViewer.DatasetTool/DicomViewer.DatasetTool.csproj --configfile "$work\NuGet.Config"
dotnet restore src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj --configfile "$work\NuGet.Config"
dotnet restore src/DicomViewer.Tests/DicomViewer.Tests.csproj --configfile "$work\NuGet.Config"

# 6) 빌드와 자료 검증 (복원 완료, --no-restore)
dotnet build tools/DicomViewer.DatasetTool/DicomViewer.DatasetTool.csproj -c Release --no-restore
dotnet run --project tools/DicomViewer.DatasetTool -c Release --no-build --no-restore -- verify data/dicom/vhp-female-ankle-v1/manifest.json
dotnet build src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release --no-restore

# 7) 핵심 테스트: 위에서 복원한 같은 설정 결과를 사용하고 --no-restore로 실행 (UI 게이트 미설정)
dotnet build src/DicomViewer.Tests/DicomViewer.Tests.csproj -c Release --no-restore
dotnet test src/DicomViewer.Tests/DicomViewer.Tests.csproj -c Release --no-build --no-restore
```

통과 기준:

- 3단계: 캐시 객체 0개, 첫 파일이 포인터 한 줄.
- 5단계 뒤: 파일 150개 모두 실제 내용(포인터 아님), `git lfs fsck` 통과, LFS 목록 150개.
- 6단계: verify 종료 코드 0, 150 파일, 78,793,078 bytes. 빌드 경고와 오류 0.
- 7단계: G 기준 core TRX 273 total, 273 passed, 0 failed, 0 notExecuted (E19).

UI 실행은 이 절차에 포함하지 않는다. 별도 승인 후 4절 게이트로 실행한다.

## 6. 한계

- 의료기기 제품, 인증, 임상 정확도, 진단 용도가 아니다.
- DICOM PS3.15 비식별화 적합성을 주장하지 않는다. 허용 목록 축소와 표본 시각 검사만 수행했다.
- 다른 DICOM 판독기와의 호환성은 확인하지 않았다.
- 압축 구문 거부 경로는 테스트하지 않았다. 멀티프레임 로드와 메타데이터 거부는 합성 DICOM 테스트로 확인했다.
- PixelPaddingValue 처리는 범위 밖이다.
- Orthanc 서버의 실제 응답 태그는 확인하지 않았다.
- 성능 수치는 측정하지 않았다. 성능 향상을 주장하지 않는다.
- 작업 트리 결과(E7–E9)는 G 소스에서 재실행했고, E11–E13은 통합 빌드 측 보고다.
- 죽은 프록시 환경 변수는 원격 접속 차단이 아니다(OS 방화벽 격리가 아님). 음성 시작 점검(E12)은 "네트워크 없이 열림"을 증명하지 않는다.
- 항목별 테스트 이름(픽셀 간격, 정렬, VOI LINEAR, 세대 번호)은 이 문서에 다시 열거하지 않았다. G 기준 core 273개(E8)의 실행 집합에 포함된다.
- 사람이 직접 작성한 코드 비율이나 작업 시간 같은 수치는 주장하지 않는다.
- UI 검증 제약: 테스트 프로세스는 DPI 비인식이므로 좌표는 가상(논리) 공간이며 입력도 같은 공간을 쓴다. 측정 기대값은 이 공간에서 계산한다(물리 픽셀 기준 아님).
- UI 검증 제약: 전경 활성화는 Windows 전경 잠금 규칙의 제약을 받으며, 실행마다 사용자가 소유 창을 직접 활성화해야 할 수 있다. 전역 설정 변경, 다른 창 활성화, 키 입력에 의한 전경 우회는 하지 않았다.
- UI 검증 제약: 측정 케이스는 한 번의 소유 실행에서만 통과했다. 세 시나리오 배치 실행, 반복 실행의 안정성, 네트워크 차단 상태에서의 UI 실행은 확인하지 않았다.
- UI 검증 제약: Bone 프리셋(2000/500)은 자동 읽기 값 검증 대상이 아니다. 자동 검증으로 통과한 프리셋은 Lung(1500/-600)뿐이다.
- UI 검증 제약: 측정 선 자체는 UIA로 읽히지 않으며, 측정 값은 목록 항목 이름으로만 확인한다.
- 원본 DICOM 수집(`collect`)은 이 문서의 어떤 절차에도 포함되지 않는다. 데모와 클린 클론 검증은 고정된 저장 사본과 매니페스트만 사용한다.

## 7. 라이선스

- 루트 저장소에는 LICENSE 파일이 없다. 사용자가 승인한 MIT 적용 범위는 이번에 새로 만들었거나 옮긴 구성요소(`tools/DicomViewer.DatasetTool/`의 새 코드와 `src/DicomViewer.Infrastructure/Datasets/`의 세 파일)뿐이다. 기존 루트 코드에 대한 재사용 허가는 **검증되지 않았다.** 이 작업은 루트 코드에 라이선스를 부여하지 않는다.
- MIT 적용 범위는 `tools/DicomViewer.DatasetTool/`의 새 코드와 `src/DicomViewer.Infrastructure/Datasets/`의 세 파일(`DatasetManifest.cs`, `DatasetVerifier.cs`, `DicomSanitizer.cs`)뿐이다. 같은 폴더의 `LICENSE`가 이 세 파일에 적용된다. `ManifestDicomDatasetSource.cs`와 기존 루트 코드에는 승인 없이 허가를 부여하지 않는다.
- 데이터는 CC0-1.0이며 NLM 출처 표시를 유지한다.
- WPF-UI 4.3.0: 패키지 메타데이터(nuspec)에 MIT가 명시되어 있고, 패키지 안 `LICENSE.md`(약 1.1 KB)를 HandMirror `get_nuget_license_info`로 실제 패키지에서 확인했다. 저작권 고지는 "Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors. https://lepo.co/"이며 표준 MIT 전문이 뒤따른다. 이 고지는 제3자 구성요소의 것이다. 이 저장소의 루트 라이선스 결정과 별개이며, 이 문서는 루트 코드에 새 라이선스를 주장하지 않는다.
- 그 밖의 NuGet 패키지는 각자의 라이선스를 따른다.

## 8. 동결 체크리스트

- [x] UI 실행 승인과 범위: 승인된 3개 시나리오(E20)만 실행됨
- [x] 승인된 UI 시나리오 실행, 결과를 E20·E21에 관찰된 값으로만 기록
- [x] 커밋 (G `aef1897`, 노리플라이 작성자·커밋터)
- [x] 푸시 (G `aef1897` 공개 main 반영, 개인 인증 명령 범위 push)
- [x] 동결 SHA(40자) 기록: `aef18975c226e82e5db43c7b240f8d8d19259d92` (G)
- [x] 5절 익명 클린 클론 절차 통과 (E19)
- [x] E7–E9는 G에서 재실행. E11–E13은 통합 빌드 측 보고이며 G 재실행 없음(표시)
- [x] WPF-UI 4.3.0 제3자 고지 정리: 패키지 `LICENSE.md`(MIT) 확인 및 §7에 고지 연결. 그 밖의 NuGet 패키지 전수 라이선스 검토는 이 항목의 범위가 아니다. 루트 라이선스 결정은 현재 푸시의 조건이 아니다.
- [x] 후보 동결 기록 2026-10-27 (G). 체크포인트 2026-10-29 이전, 게시 마감 2026-10-30 (미완료 시 마감 전 재확인)
