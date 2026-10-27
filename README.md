# wpf-dicom-viewer

WPF 기반 DICOM 뷰어 실험 저장소입니다. **제품이나 의료기기가 아니며**, 의료 영상 판독·진단·임상 용도로 사용하지 않습니다.

## 해결하려는 문제

- 공개 DICOM 자료를 **외부 서비스 호출 없이** 같은 입력으로 반복해 열 수 있어야 합니다. 기존 기본 경로는 TCIA 실시간 탐색과 선택한 시리즈 다운로드에 의존했습니다(소스 관찰이며, 모든 실행의 동작을 측정한 것은 아닙니다).
- 픽셀 간격(PixelSpacing)이 없는 영상에서 거리를 **임의의 1 mm**로 표시하면 잘못된 측정이 됩니다.
- 시리즈 순서를 파일명이나 InstanceNumber만으로 정하면 공간 위치와 어긋날 수 있습니다.
- 빠른 슬라이스 변경이나 취소 후에도 이전 작업의 결과가 화면에 게시되면 안 됩니다.

## 설계 결정과 구현 상태

| 영역 | 결정 | 상태 |
| --- | --- | --- |
| 자료 | 공개 발목 CT 150장(`vhp-female-ankle-v1`)을 Git LFS로 고정 | 검증됨 (아래 검증 기록) |
| 자료 취급 | 허용 목록 속성만 복사한 저장 사본과 SHA-256 매니페스트 | 검증됨. 비식별화 인증은 아님 |
| 픽셀 간격 | PixelSpacing(0028,0030)만 사용. 없으면 `Unknown`이고 px로 표시 | 소스 구현, 테스트 통과 |
| 시리즈 정렬 | IPP·IOP 투영으로 정렬, 누락·모호하면 상태로 표시 | 소스 구현, 테스트 통과 |
| 지원 범위 | 단일 프레임, 비압축, Little Endian, MONOCHROME2 | 소스 구현. 나머지 구문은 명시적 거부 경로 |
| Window/Level | VOI LINEAR 경계 구현, 결과 절삭 | 소스 구현, 테스트 통과 |
| 최신 요청 우선 | 세대(generation) 기반으로 낡은 결과를 게시하지 않음 | 소스 구현, 테스트 통과 |
| 계층 | Domain · Application · Infrastructure · ViewModels는 WPF에 의존하지 않음 | 소스 구조로 확인 |
| UI | WPF-UI 4.3.0 테마 리소스, 커스텀 컨트롤(`DicomViewer.UI`) | 시작 관찰(읽기 전용) 및 승인된 3개 UI 시나리오 자동 검증 통과(E20). 나머지 상호작용과 배치 실행은 미확인 |

항목별 테스트 매핑과 실행 기록은 [검증 기록](docs/VALIDATION.md)에 있습니다.

## 소유와 AI 보조 방식

- 소유자: 저장소 소유자 christian289. 범위 결정과 최종 승인을 맡았습니다.
- AI 보조: 코드·테스트·문서 초안 작성에 AI 도구를 사용했습니다. 세부 검토 절차와 사람이 직접 작성한 부분은 이 문서에서 구체적으로 주장하지 않습니다.
- 사람이 직접 작성한 코드의 비율이나 작업 시간 같은 **수치는 주장하지 않습니다.**

## 현재 검증된 범위

상세 근거와 명령은 [검증 기록](docs/VALIDATION.md)을 따릅니다. 아래는 실행이나 관찰로 확인된 항목입니다.

- 전체 테스트: 실제 실행 273개 통과, 실패 0, 건너뜀 0 (G 최종 소스, 이 저장소 Release `--no-build` 실행). 이전 F 기록 268개와 통합 빌드 보고 267개는 이력으로 남겨 둔다.
- 솔루션 Release 빌드: 오류 0. 경고는 기존 OpenSilver 계열 프로젝트에만 있으며(CS8604 1건, NU1507 3개 프로젝트, 요약 집계 9), 앱·핵심 프로젝트 경고는 0입니다.
- 공개 자료: 150개 파일, 저장 사본 합계 78,793,078 bytes. 로컬 clone과 원격 빈 캐시에서 LFS 복원과 매니페스트 검증을 실행했습니다.
- 자료 복원 실패 경로: 포인터만 있을 때, 객체가 없을 때, 객체가 손상되었을 때 검증이 실패하는 것을 로컬 clone에서 확인했습니다.
- 앱 시작 관찰: 192 DPI(200%) 환경에서 150 슬라이스 자료를 표시하며 읽기 전용으로 시작하는 것을 관찰했습니다(검증 기록 2절 E11). 게시(publish) 출력 검증은 아직 증거 표에 없습니다.
- 선택적 UI 검증 프로젝트(`src/DicomViewer.UiTests`): 컴파일 0 경고 0 오류. 게이트 미설정(또는 `0`)에서는 3개 모두 Skip으로 보고됩니다.
- 승인된 UI 검증 3개는 **서로 다른 실행**에서 각각 통과했습니다: 시작·첫 프레임·빠른 슬라이더(표시 75 안정), 변환·드래그·W/L 리셋·프리셋, 1.0 mm 간격 측정(50 mm 허용 오차 2/fit) 후 정상 종료(종료 코드 0). 시작과 변환은 최종 하네스의 프로세스 소유 수정 전 실행에서 통과했고, 측정은 최종 하네스에서 통과했습니다. 세 실행을 한 번에 묶은 배치 통과는 아직 없습니다.
- 측정 실행에서 관찰된 값: 측정 라벨 49.99 mm, 허용 범위 내. 창 DPI 192, 테스트 프로세스는 DPI 비인식(가상 좌표), 화면 1920×1080 기준. 전경 활성화는 사용자가 소유 창을 직접 활성화해야 했습니다(환경 제약).

### UI 상호작용과 종료 구분

- **UI 상호작용(클릭, 드래그, 슬라이더):** 승인된 범위의 3개 시나리오가 각각 별도 실행에서 통과했습니다. 키보드 입력은 보내지 않습니다. 슬라이더 값은 UIA RangeValue 패턴으로 설정합니다. 소유 창 전경 PID 검사는 마우스 클릭·드래그 직전과 소유 창 캡처 직전에 수행합니다. 전경 활성화에는 사용자의 직접 클릭이 필요했습니다.
- **앱 시작 관찰:** 읽기 전용으로 관찰했습니다(입력 없음). 음성 시작(매니페스트 없음, LFS 포인터) 점검은 오류 상태와 프레임 0을 보였고 비정상 종료는 없었습니다.
- **종료:** 정상 닫기 후 프로세스 종료 코드 0을 소유 프로세스에서 읽어 확인했습니다. 실행 후 잔존 앱 프로세스는 없었습니다.
- **실패와 수정 기록:** 이 측정 케이스의 첫 실패는 하네스의 정수 나눗셈과 종료 코드 읽기 방식의 문제였고 테스트 코드에서 수정했습니다. 이 첫 실패에서는 앱 결함이 확인되지 않았습니다. 앱 자체의 결함 수정(선택 해제 중 로드 상태, 로드 중 W/L 교체 경합)은 검증 기록 E22·E23에 따로 적었습니다. 어서션과 허용 오차는 바꾸지 않았습니다.

**현재 상태 (G `aef18975c226e82e5db43c7b240f8d8d19259d92` 기준):** 익명 공개 HTTPS 클린 클론 검증은 완료되었습니다(150개 자료 복원·검증, 273개 코어 테스트, 읽기 전용 시작 관찰). 세 시나리오를 한 번에 묶은 배치 실행은 아직 없습니다. 최종 UI 확인은 기존 시나리오 중 1개를 최신 소스에서 새로 실행한 것입니다. 모두 [검증 기록](docs/VALIDATION.md)에 기록되어 있습니다.

## 소스 실행 명령

필요 도구: Windows, .NET SDK **10.0.403** (`global.json`에서 `rollForward=disable`로 고정), Git, Git LFS.

```powershell
# 0) 저장소 복제 (처음부터 복사 실행 가능하도록)
git clone https://github.com/christian289/wpf-dicom-viewer.git
cd wpf-dicom-viewer

# 1) 자료 가져오기 (클론별 1회 필터 등록 후 LFS 객체 복원)
git lfs install --local --skip-repo
git lfs pull --include "data/dicom/vhp-female-ankle-v1/**"

# 2) 복원 (도구와 앱은 각각 복원해야 합니다. 앱 복원이 도구 프로젝트를 복원하지는 않습니다.)
dotnet restore tools/DicomViewer.DatasetTool/DicomViewer.DatasetTool.csproj
dotnet restore src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj

# 3) 오프라인 자료 검증 (복원된 결과를 사용하며 네트워크에 접속하지 않음)
dotnet build tools/DicomViewer.DatasetTool/DicomViewer.DatasetTool.csproj -c Release --no-restore
dotnet run --project tools/DicomViewer.DatasetTool -c Release --no-build --no-restore -- verify data/dicom/vhp-female-ankle-v1/manifest.json

# 4) 데스크톱 앱 빌드와 실행
dotnet build src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release --no-restore
dotnet run --project src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release --no-build
```

- 앱은 `AppContext.BaseDirectory` 기준 상대 경로로 자료를 엽니다. 임시 폴더를 작업 디렉터리로 둔 시작 점검에서 자료가 열리는 것을 확인했습니다(검증 기록 E11).
- 데스크톱 앱 경로는 OpenSilver(wasm) 워크로드가 필요하지 않습니다(프로젝트 구성 기준). 전체 솔루션 빌드(`src/DicomViewer.slnx`)에는 해당 프로젝트 때문에 추가 워크로드가 필요할 수 있습니다.
- LFS 객체가 복원되지 않았다면 포인터 파일이 복사되고, 앱은 명시적 오류를 표시합니다.
- NuGet 패키지 복원에는 네트워크가 필요합니다. 개인 설정, 인증 토큰, 개인 NuGet 소스는 필요하지 않습니다.

## 데모 절차 (소스 실행)

아래 절차는 소스에서 빌드한 Release 앱을 실행하는 방법입니다. 개인 설정이나 인증 정보는 필요하지 않습니다.

```powershell
dotnet build src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release
dotnet run --project src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release --no-build
```

1. 앱이 150 슬라이스 자료를 표시할 때까지 기다립니다(상태 메시지가 사라지고 Unit: mm).
2. 슬라이스 슬라이더를 75번째 위치(화면의 슬라이스 번호 표시 75)로 옮깁니다. 표시가 75인지 확인합니다.
3. 우측 패널의 Lung 프리셋을 선택합니다. 읽기 값이 `W 1500 / L -600`이어야 합니다.
4. Bone 프리셋을 선택합니다. 코드 기준 값은 `W 2000 / L 500`입니다. 이 값은 자동 검증되지 않았습니다.
5. Pan 도구로 영상을 드래그하고, Zoom 도구나 확대·축소 버튼을 사용합니다. 기본 보기로 되돌리려면 Reset view를 누릅니다.
6. 회전·좌우 반전 버튼을 사용하고 Reset view로 되돌립니다.
7. Measure 도구로 영상 위 두 점을 드래그하면 측정 목록에 mm 값이 추가됩니다. 알려진 1.0 mm 기준선에서 30×40 px 구간은 약 50 mm로 확인되었습니다(자동 검증 기준, 임상 정확도와 무관).
8. 창을 닫습니다. 정상 종료는 종료 코드 0으로 확인되었습니다.

이 데모는 연구·검증용 공개 자료에 대한 것이며 임상 판독, 진단, 치료 계획에 사용하지 않습니다.

## 테스트 실행 구분

- 기본 단위·통합 테스트(`src/DicomViewer.Tests`)는 UI 없이 실행합니다. 명령: `dotnet test src/DicomViewer.Tests/DicomViewer.Tests.csproj`
- 선택적 UI 검증(`src/DicomViewer.UiTests`)은 제품 기능이 아니며 기본 실행에 포함되지 않습니다. 두 환경 변수가 모두 설정되어야 실행되며, 설정하지 않으면 테스트는 통과가 아니라 **Skip**으로 보고됩니다.

```powershell
dotnet build src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj -c Release
$env:DICOM_VIEWER_RUN_UI_TESTS = "1"
$env:DICOM_VIEWER_APP_EXE = (Resolve-Path '.\src\DicomViewer.WpfApp\bin\Release\net10.0-windows\DicomViewer.WpfApp.exe').Path
# 선택: 소유 새 뷰어 창을 사용자가 직접 전경으로 클릭할 때까지 기다리는 시간(초, 최대 600). 기본값 0(기다리지 않음).
$env:DICOM_VIEWER_UI_MANUAL_FOREGROUND_WAIT_SECONDS = "180"
dotnet test src/DicomViewer.UiTests/DicomViewer.UiTests.csproj -c Release
```

- 테스트는 자기가 시작한 앱 프로세스 하나만 조작하고, 다른 창이나 데스크톱 전체를 다루지 않습니다.
- 마우스·클릭 입력이 발생하므로 실행 전에 사용자의 승인을 받는 것을 전제로 합니다.
- 수동 활성화 옵션(`DICOM_VIEWER_UI_MANUAL_FOREGROUND_WAIT_SECONDS`)은 선택 사항입니다. 테스트가 시작한 새 뷰어 창이 전경이 될 때까지 기다리며, 전경 활성화는 **사용자가 그 창을 직접 클릭**해야 합니다. 기다리는 동안 테스트는 입력을 보내지 않습니다. 전역 전경 잠금 설정 변경, 다른 창 활성화, 키 입력으로 전경을 빼앗는 우회는 구현하지 않았습니다.

## 아키텍처

| 프로젝트 | 책임 |
| --- | --- |
| `DicomViewer.Domain` | 엔터티, 값 객체(변환, 측정, 픽셀 데이터, 창 레벨) |
| `DicomViewer.Application` | 계약(인터페이스), DTO, 시리즈 탐색과 정렬 |
| `DicomViewer.Infrastructure` | fo-dicom 판독, PACS 매핑, 자료 수집·검증(`Datasets/`) |
| `DicomViewer.ViewModels` | MVVM 뷰모델(WPF 비의존) |
| `DicomViewer.UI` | 커스텀 컨트롤과 테마 리소스 |
| `DicomViewer.WpfApp` | 진입점, DI 구성, WPF-UI 리소스 연결, 뷰 |
| `DicomViewer.Tests` | 단위·통합 테스트 |
| `DicomViewer.UiTests` | 선택적 UI 검증(옵트인, 제품 아님) |
| `tools/DicomViewer.DatasetTool` | 기준 자료 수집·검증 CLI |

과거에 있던 `DicomViewer.WpfServices` 프로젝트는 사용되지 않는 것으로 확인되어 제거했습니다(의존 프로젝트가 없고 인터페이스와 미사용 패키지 버전을 정리).

## 설정

`src/DicomViewer.WpfApp/appsettings.json`의 현재 내용은 다음과 같습니다.

```json
{
  "Dataset": {
    "ManifestPath": "data/dicom/vhp-female-ankle-v1/manifest.json"
  }
}
```

현재 파일에는 PACS 설정 항목이 없습니다. 앱은 고정 로컬 자료만 엽니다.

## 사용법

| 도구 | 동작 | 검증 상태 |
| --- | --- | --- |
| 슬라이스 | 슬라이더, 번호 표시(1-based). 빠른 연속 요청은 마지막 요청으로 정착 | 자동 UI 검증 (E20 a) |
| Window/Level | 드래그로 폭·중심 변경, 프리셋 Lung(1500/−600), 기본값 복원 | 드래그와 리셋·Lung 자동 검증 (E20 b). Bone(2000/500)과 Abdomen·Brain은 자동 검증 대상 아님 |
| Pan | 드래그로 이동 | 드래그 후 읽기 값 변화 자동 검증 (E20 b) |
| Zoom | 드래그 또는 확대·축소 버튼 | 드래그 후 범위 내 변화 자동 검증 (E20 b). 버튼 경로는 자동 검증 대상 아님 |
| 회전·좌우 반전·초기화 | 버튼 | 자동 검증 (E20 b) |
| Measure | 두 점 거리. 간격 확인 시 mm, 미확인 시 px | 1.0 mm 기준선 측정 자동 검증 (E20 c). 미확인 간격의 px 표시는 자동 UI 검증 대상이 아님 |

자동 UI 검증은 승인된 세 시나리오(E20)에 한정합니다. 이 목록의 나머지 동작과 여러 시나리오를 묶은 실행은 확인하지 않았습니다.

## 한계

- 의료기기 제품이 아니며 인증, 임상 검증, 진단 용도가 없습니다.
- 공개 자료는 **체중부하가 아닌 사체 일반 CT**입니다. WBCT, 체중부하 촬영, CBCT가 아닙니다.
- DICOM PS3.15 비식별화 적합성을 주장하지 않습니다. 허용 목록 축소와 표본 시각 검사만 수행했습니다.
- 멀티프레임 로드와 메타데이터 거부는 합성 DICOM 테스트(`SyntheticDicomLoadTests`)로 확인했습니다. 압축 구문 거부는 아직 테스트하지 않았습니다.
- PixelPaddingValue 처리는 이번 범위에 없습니다.
- 성능 수치는 측정하지 않았습니다. 성능 향상은 주장하지 않습니다.
- 다른 DICOM 판독기와의 호환성은 확인하지 않았습니다.
- 기존 TCIA 테스트는 모의 HTTP 응답 기반이며 영상 검증이나 UI 검증의 근거가 아닙니다.
- Orthanc 서버의 실제 응답 태그는 확인하지 않았습니다.
- 대화형 UI 검증은 승인된 3개 시나리오만 다룹니다(시작·슬라이더, 변환·드래그·W/L, 측정·종료). 그 밖의 조작과 전체 배치 실행은 검증하지 않았습니다.

## 라이선스와 출처

- 루트 저장소에서 LICENSE 파일을 확인하지 못했습니다. 이는 루트 코드에 대한 재사용 허가가 **검증되지 않았다**는 뜻이며, 법적 결론(권리 없음)이 아닙니다.
- MIT 적용 범위: `tools/DicomViewer.DatasetTool/`의 새 코드와 `src/DicomViewer.Infrastructure/Datasets/`의 세 파일(`DatasetManifest.cs`, `DatasetVerifier.cs`, `DicomSanitizer.cs`)만입니다. 같은 폴더의 `LICENSE` 파일이 이 세 파일에 적용됩니다. `ManifestDicomDatasetSource.cs`와 기존 루트 코드에는 이 허가를 부여하지 않았습니다.
- 데이터 `data/dicom/vhp-female-ankle-v1/`는 CC0-1.0이며, 출처 표시 "Courtesy of the U.S. National Library of Medicine"을 유지합니다. 자세한 내용은 [NOTICE](data/dicom/vhp-female-ankle-v1/NOTICE.md)를 따릅니다.
- 자료 출처: [Harvard Dataverse DOI 10.7910/DVN/3JDZCT](https://doi.org/10.7910/DVN/3JDZCT), 버전 1.0.
- 제3자 구성요소: WPF-UI 4.3.0([NuGet 공개 패키지](https://www.nuget.org/packages/WPF-UI/4.3.0))을 사용합니다. 패키지의 `LICENSE.md`(MIT, "Copyright (c) 2021-2025 Leszek Pomianowski and WPF UI Contributors")를 확인했습니다. 그 밖의 NuGet 패키지는 각자의 라이선스를 따릅니다. 이 고지는 WPF-UI의 것이며 이 저장소의 루트 라이선스를 정하지 않습니다.

## 관련 문서

- [검증 기록](docs/VALIDATION.md)
- [기존 뷰어 조사와 고도화 규약](docs/superpowers/specs/2026-10-26-existing-viewer-assessment.md)
- [구현 계획](docs/superpowers/plans/2026-10-27-viewer-correctness-offline-ui.md)
- [데이터셋 도구](tools/DicomViewer.DatasetTool/README.md)

## 이전 프로토타입 (역사적 맥락)

이 저장소는 처음에 AI 보조 도구로 의료 영상 뷰어를 얼마나 빠르게 만들 수 있는지 보려는 **프로토타입**으로 시작했습니다. 작업 기간은 약 5시간이라고 기록되어 있습니다. 당시 결과는 데모 수준이었고, 위의 정확성·재현성 목표와 검증 범위는 이후 작업에서 정리한 것입니다. 이 섹션은 역사적 맥락이며 현재 기능 주장의 근거가 아닙니다.
