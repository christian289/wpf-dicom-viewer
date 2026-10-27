# wpf-dicom-viewer

- 의료 기기 업계를 잘 모르지만 5시간에 걸쳐 AI를 통해 의료 영상 Viewer를 개발.
- 잘 모르는 사람도 이제는 AI를 이용하여 어느 정도까지는 제품을 만들 수 있음을 시사.
- .claude 폴더 안의 claude skill을 사용하여 개발.

## 개발 환경 및 빌드

- 기준 SDK: .NET SDK **10.0.403**. `global.json`에 `rollForward=disable`, `allowPrerelease=false`를 명시하여 검증한 SDK로 고정합니다.
- Visual Studio에서 작업할 경우 .NET 10과 `.slnx`를 지원하는 Visual Studio 2026을 사용합니다.
- WPF 및 공통 계층의 기존 .NET 10 대상은 유지합니다. OpenSilver 실행 프로젝트는 호환성을 위해 .NET 9, 공유 라이브러리는 .NET Standard 2.0을 유지합니다.
- 이전 개발 환경의 워크로드 `10.0.101` 고정은 제거했습니다. 전체 솔루션 빌드에는 현재 SDK의 `wasm-tools-net9` 워크로드가 필요합니다.

저장소 루트에서 실행:

```powershell
dotnet workload restore src/DicomViewer.slnx
dotnet restore src/DicomViewer.slnx
dotnet build src/DicomViewer.slnx
dotnet test src/DicomViewer.Tests/DicomViewer.Tests.csproj
dotnet run --project src/DicomViewer.WpfApp/DicomViewer.WpfApp.csproj
```

SDK 설정 변경 후 이미 열린 Visual Studio에서는 솔루션을 닫았다가 다시 열어 프로젝트를 다시 로드합니다.

### 패키지·서식·고도화 규약

- NuGet 버전은 루트 `Directory.Packages.props`의 CPM(`ManagePackageVersionsCentrally`, `PackageVersion`)으로 관리합니다. csproj에는 필요한 PackageReference와 조건·비버전 메타데이터를 유지하고 Version/VersionOverride는 두지 않습니다.
- `Directory.Build.props`의 공통 빌드 기본값과 각 프로젝트의 타깃·WPF 사용 여부를 유지합니다. 패키지 설정 이전으로 기존 버전을 올리거나 참조를 추가하지 않습니다.
- 기본 솔루션은 기존 `src/DicomViewer.slnx`입니다. 중복 `.sln`은 사용하지 않습니다.
- 루트 `.editorconfig`는 .NET 10 SDK 기본 템플릿을 기반으로 합니다. XAML은 `Settings.XamlStyler`와 동일한 공백 4칸·CRLF 규약을 사용합니다. 전체 소스를 일괄 재서식하지 않습니다.
- 고도화의 UI 규약은 CustomControl 기본, 업무 화면 조합에만 UserControl 사용, 기존 컨트롤의 불필요한 재래핑 금지, WPF-UI 기본 테마입니다. 상세 공개 자료·설계·후속 작업은 [뷰어 고도화 명세](docs/superpowers/specs/2026-10-26-existing-viewer-assessment.md)를 따릅니다.
- 아래 사용법은 현재 실시간 TCIA 구현을 설명합니다. 고정 공개 DICOM 재사용과 WPF-UI 적용은 후속 구현이며 이번 설정 마이그레이션에서 변경하지 않았습니다.

XAML 서식 미리보기는 다음 명령으로 소스 변경 없이 확인할 수 있습니다.

```powershell
dotnet dnx -y XamlStyler.Console@3.2501.8 -- -f src/DicomViewer.UI/Themes/Generic.xaml -c Settings.XamlStyler --write-to-stdout
```

formatter 실행은 앱·UI 실행 검증이 아닙니다. UI 자동조작은 실행 직전 사용자 승인이 필요합니다.

## User Guide (사용법)

### Main Navigation (화면 전환)

상단 도구 모음의 2개 탭으로 화면을 전환합니다.

| Tab | View | Purpose |
|-----|------|---------|
| **TCIA Explorer** | TciaExplorerView | 공개 DICOM 데이터셋 탐색 및 다운로드 |
| **Viewer** | ViewerView | DICOM 이미지 표시 및 조작 |

### Opening DICOM Files (DICOM 파일 열기)

1. **TCIA Explorer** 탭에서 Collection → Patient → Study → Series 순으로 탐색
2. 원하는 Series 선택 후 **Download & Open** 버튼 클릭
3. 다운로드 완료 시 자동으로 **Viewer** 탭으로 전환되어 이미지 표시

### Why No "Study List" Tab? (왜 Study 탭이 없는가?)

일반적인 병원용 DICOM Viewer(PACS Workstation)에는 Study List가 필수입니다:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Study List    │ →  │  Series List    │ →  │     Viewer      │
│  (환자/검사 목록) │    │  (시리즈 목록)   │    │   (이미지 보기)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

**PACS(Picture Archiving and Communication System)**는 병원 내 수천~수만 건의 영상 검사를 관리하며, 의료진이 환자 ID/이름/검사일로 검색하여 필요한 Study를 찾습니다.

그러나 이 프로젝트는 **포트폴리오 데모 목적**으로, TCIA(The Cancer Imaging Archive) 공개 데이터셋을 탐색하고 보는 것이 주 용도입니다. TCIA Explorer가 이미 Collection → Patient → Study → Series 계층 탐색을 제공하므로, 별도의 Study List 탭은 기능 중복이 되어 제거했습니다.

### Image Manipulation Tools (이미지 조작 도구)

우측 도구 패널에서 도구 선택 후 이미지 위에서 마우스 드래그합니다.

| Tool | Mouse Action | Effect |
|------|--------------|--------|
| **Pan** | 드래그 | 이미지 이동 |
| **Zoom** | 상하 드래그 | 확대/축소 (0.1x ~ 10x) |
| **Window/Level** | 좌우 드래그: Width, 상하 드래그: Center | 밝기/대비 조정 |
| **Measure** | 드래그 | 두 점 사이 거리 측정 (mm) |

### Quick Transform Buttons (빠른 변환 버튼)

| Button | Action |
|--------|--------|
| **Rotate Left/Right** | 90° 회전 |
| **Flip H/V** | 수평/수직 반전 |
| **Zoom In/Out** | 1.2배 확대/축소 |
| **Reset** | 모든 변환 초기화 |

### Slice Navigation (슬라이스 네비게이션)

| Method | Action |
|--------|--------|
| **마우스 휠** | 위로: 이전 슬라이스, 아래로: 다음 슬라이스 |
| **하단 슬라이더** | 드래그하여 슬라이스 이동 |
| **Series 클릭** | 좌측 패널에서 다른 Series 선택 |

### Window/Level Presets (W/L 프리셋)

우측 패널 하단에서 CT 부위별 프리셋을 선택할 수 있습니다.

| Preset | Window | Level | Use Case |
|--------|--------|-------|----------|
| Default | 400 | 40 | 일반 |
| Abdomen | 400 | 40 | 복부 CT |
| Lung | 1500 | -600 | 폐 CT |
| Bone | 2000 | 400 | 골격 CT |
| Brain | 80 | 40 | 뇌 CT/MR |

### Histogram (히스토그램)

- 히스토그램 토글 버튼으로 표시/숨김
- 좌하단에 200x120 크기로 표시
- 노란색 영역: 현재 Window/Level 범위
- 밝기 분포 확인 및 W/L 조정 시 참고

### Configuration (설정)

`appsettings.json` 파일에서 PACS 데이터 소스를 설정합니다.

```json
{
  "Pacs": {
    "Type": "LocalFolder",
    "LocalPath": "C:\\DICOM",
    "OrthancUrl": "http://localhost:8042"
  }
}
```

| Setting | Values | Description |
|---------|--------|-------------|
| `Type` | `LocalFolder` / `Orthanc` | PACS 데이터 소스 |
| `LocalPath` | 폴더 경로 | 로컬 DICOM 폴더 (Type=LocalFolder) |
| `OrthancUrl` | HTTP URL | Orthanc 서버 주소 (Type=Orthanc) |

### TCIA Explorer (공개 데이터셋 다운로드)

1. **TCIA Explorer** 탭 선택
2. Collection → Patient → Study → Series 순으로 탐색
3. **Download & Open** 버튼으로 다운로드 및 자동 로드
4. 캐시 위치: `%LocalAppData%\DicomViewer\TciaCache\`

### Non-Image Modality Filtering (비이미지 Modality 필터링)

TCIA Explorer는 이미지 픽셀 데이터가 없는 Modality를 Series 목록에서 자동으로 필터링합니다.

| Modality | 설명 | 필터링 이유 |
|----------|------|-------------|
| `RTSTRUCT` | Radiotherapy Structure Set | ROI 윤곽선 데이터만 포함 (방사선 치료 계획용) |
| `RTPLAN` | Radiotherapy Plan | 방사선 치료 계획 메타데이터 |
| `RTDOSE` | Radiotherapy Dose | 방사선 선량 분포 데이터 |
| `SR` | Structured Report | 텍스트 기반 구조화된 보고서 |
| `PR` | Presentation State | 이미지 표시 설정 정보 |
| `SEG` | Segmentation | 분할 마스크 데이터 |
| `KO` | Key Object Selection | 키 이미지 참조 목록 |
| `REG` | Registration | 이미지 정합 정보 |
| `FID` | Fiducials | 기준점 좌표 데이터 |

이러한 Modality들은 일반 이미지 뷰어에서 렌더링할 수 없으며, 전문 방사선 치료 계획 소프트웨어에서 사용됩니다. 필터링된 Series가 있으면 상태 메시지에 표시됩니다:

```
2개의 Series를 찾았습니다. (4개 비이미지 제외)
Found 2 series. (4 non-image excluded)
```
