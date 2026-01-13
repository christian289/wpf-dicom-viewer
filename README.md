# wpf-dicom-viewer

- 의료 기기 업계를 잘 모르지만 5시간에 걸쳐 AI를 통해 의료 영상 Viewer를 개발.
- 잘 모르는 사람도 이제는 AI를 이용하여 어느 정도까지는 제품을 만들 수 있음을 시사.
- .claude 폴더 안의 claude skill을 사용하여 개발.

## User Guide (사용법)

### Main Navigation (화면 전환)

상단 도구 모음의 3개 탭으로 화면을 전환합니다.

| Tab | View | Purpose |
|-----|------|---------|
| **Study List** | StudyListView | DICOM 폴더 열기 및 Study 검색 |
| **Viewer** | ViewerView | DICOM 이미지 표시 및 조작 |
| **TCIA Explorer** | TciaExplorerView | 공개 DICOM 데이터셋 다운로드 |

### Opening DICOM Files (DICOM 파일 열기)

1. **Study List** 탭에서 **Open Folder** 버튼 클릭
2. DICOM 파일이 있는 폴더 선택
3. 자동으로 Study/Series 스캔 및 목록 표시
4. Study 더블클릭 → Viewer로 이동

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
