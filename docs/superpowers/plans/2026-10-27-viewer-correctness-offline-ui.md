# 뷰어 정확성·오프라인·UI 구현 계획 (2026-10-27)

근거: `docs/superpowers/specs/2026-10-26-existing-viewer-assessment.md` 제2·3·4·5·7절. 이 문서는 외부 메시지나 다른 파일에 의존하지 않는다.

## 1. 범위와 소유권

| 영역 | 담당 | 비고 |
| --- | --- | --- |
| Domain: `ValueObjects/ImageTransform.cs`, `PixelData.cs`, `MeasurementLine.cs`, `Entities/DicomInstance.cs` | 핵심 작업자 | 역변환·뷰포트, 단위 미확인 측정, 픽셀 수 검사 |
| Application: `DTOs/PixelSpacing.cs`, `DicomInstanceDto.cs`, `LoadedDicomImage.cs`, `SeriesGeometryStatus.cs`, `Exceptions/UnsupportedDicomImageException.cs`, `Interfaces/IDicomImageService.cs`, `Interfaces/ISeriesNavigationService.cs`, `Services/SeriesOrdering.cs`, `Services/SeriesNavigationService.cs`, `Services/ThumbnailService.cs` | 핵심 작업자 | 계약과 호출자 이행 |
| Infrastructure: `Dicom/FoDicomImageService.cs`, `Dicom/DicomInstanceMapper.cs`, `Pacs/LocalFolderPacsService.cs`, `Pacs/OrthancPacsService.cs` | 핵심 작업자 | 판독·헤더 검증·PACS 매핑 |
| Infrastructure: `Datasets/`, `tools/DicomViewer.DatasetTool`, 기존 `Tests/Datasets/*`, test csproj, `slnx`, `Directory.Packages.props` | 표면 작업자 | 핵심 작업자는 수정하지 않는다 |
| ViewModels, UI, WpfServices, WpfApp | 표면 작업자 | 핵심 작업자는 수정하지 않는다 |
| Tests: `Tests/Domain/`, `Tests/Application/`, `Tests/Infrastructure/` 의 아래 신규 파일 | 핵심 작업자 | 공개 계약 소비자 관점 |

## 2. 공개 계약 (정확한 시그니처)

```csharp
// Application/DTOs/PixelSpacing.cs
public readonly record struct PixelSpacing(double X, double Y)   // X=열(수평) mm, Y=행(수직) mm
{
    public static PixelSpacing Unknown => default;
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && X > 0 && Y > 0;
    public PixelSpacingUnit Unit => IsValid ? PixelSpacingUnit.Millimeter : PixelSpacingUnit.Pixel;
    public double CalculateDistance(double pixelDeltaX, double pixelDeltaY); // 미확인: px 유클리드, 확인: 축별 mm
}
public enum PixelSpacingUnit { Pixel, Millimeter }

// Application/DTOs/LoadedDicomImage.cs
public sealed record LoadedDicomImage(PixelData Pixels, PixelSpacing Spacing, WindowLevel DefaultWindowLevel);

// Application/DTOs/DicomInstanceDto.cs
public sealed record DicomInstanceDto(
    string SopInstanceUid, int InstanceNumber, string FilePath, int Rows, int Columns,
    string? SeriesInstanceUid = null, string? FrameOfReferenceUid = null,
    double[]? ImagePositionPatient = null, double[]? ImageOrientationPatient = null);

// Application/DTOs/SeriesGeometryStatus.cs
public enum SeriesGeometryStatus { Empty, Spatial, MissingGeometry, AmbiguousGeometry }

// Application/Exceptions/UnsupportedDicomImageException.cs
public sealed class UnsupportedDicomImageException : Exception { public UnsupportedDicomImageException(string message); }

// Application/Interfaces/IDicomImageService.cs
Task<LoadedDicomImage> LoadImageAsync(string filePath, CancellationToken cancellationToken = default);
Task<LoadedDicomImage> LoadImageFromStreamAsync(Stream dicomStream, CancellationToken cancellationToken = default);
Task<DicomInstanceDto> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default);
void ApplyWindowLevel(PixelData pixelData, WindowLevel windowLevel, Span<byte> destination, CancellationToken cancellationToken = default);

// Application/Interfaces/ISeriesNavigationService.cs
// 기존 멤버 유지 + / existing members kept, plus:
SeriesGeometryStatus GeometryStatus { get; }

// Domain/ValueObjects/ImageTransform.cs
public readonly record struct ImageViewport(double ViewportWidthDip, double ViewportHeightDip, double ImageWidthPx, double ImageHeightPx)
{ public double FitScaleDipPerPixel { get; } }   // min(vw/iw, vh/ih); 양수 유한만 허용
// ImageTransform (기존 record에 추가 / added to existing record)
double DipPerImagePixel(ImageViewport viewport);
Point2D ImageToViewport(Point2D imagePixel, ImageViewport viewport);
Point2D ViewportToImage(Point2D viewportDip, ImageViewport viewport);                    // 범위 제한 없음
bool TryViewportToImage(Point2D viewportDip, ImageViewport viewport, out Point2D imagePixel); // [0,W]x[0,H]
Point2D ViewportToImageClamped(Point2D viewportDip, ImageViewport viewport);

// Domain/ValueObjects/MeasurementLine.cs
public sealed record MeasurementLine(Point2D StartPoint, Point2D EndPoint, double? LengthMm, string Label)
{ public double LengthPixels { get; } }   // LengthMm이 null이면 미확인 보정

// Domain/ValueObjects/PixelData.cs
public int PixelCount => checked(Width * Height);   // 오버플로는 예외
```

변환 순서: 플립(영상 중심 기준) → 확대(맞춤 배율 × ZoomFactor) → 회전(화면 y 아래 기준 시계 방향 양수) → 이동(뷰포트 중심 + Pan). 영상 원점은 좌상단이며 (W,H)는 우하단 모서리다.

## 3. 구현 규칙

### 3.1 간격
- `PixelSpacing`(0028,0030)의 두 값만 사용한다. `ImagerPixelSpacing` 대체와 1 mm 기본값은 없다.
- 누락, 개수 오류, 비유한, 비양수는 `Unknown`이다. 거리는 px로 계산하고 `Unit`으로 구분한다.

### 3.2 지원 프로필 (헤더 검증, 메타데이터와 로드가 같은 규칙)
- 거부(미지원, `UnsupportedDicomImageException`): 빅 엔디언, 압축(encapsulated) 구문, `NumberOfFrames != 1`, `SamplesPerPixel != 1`, `PhotometricInterpretation != MONOCHROME2`, `VOI LUT Sequence` 존재, `VOILUTFunction` 존재하고 `LINEAR`가 아님, `BitsAllocated`가 8/16이 아님, `BitsStored`가 1..BitsAllocated 밖, `HighBit != BitsStored - 1`, `PixelRepresentation > 1`, 바이트 수가 `Array.MaxLength` 초과.
- 손상(`InvalidDataException`, 고정 안전 메시지): 필수 속성 누락, `Rows` 또는 `Columns` 가 0, 기울기·절편 비유한 또는 기울기 0, 픽셀 데이터가 선언 크기보다 짧음, fo-dicom 파싱 실패.
- 순서: 헤더 검사가 끝나야 프레임 바이트를 꺼낸다. 크기 검사는 프레임 읽기 전에 한다.
- 비트 배치: 리틀 엔디언만 지원한다. 상위 비트에 저장된 레이아웃(`HighBit != BitsStored-1`)은 마스킹으로 추측하지 않고 거부한다.

### 3.3 메타데이터 읽기 (픽셀 미로드)
- `FileStream`을 서비스가 열고 `DicomFile.OpenAsync(stream, FileReadOption.ReadLargeOnDemand, 0)`로 읽는다. 큰 태그(PixelData)는 지연 상태이므로 값을 꺼내지 않고 존재 여부만 검사한다.
- 같은 지원 프로필을 적용하고 통과하면 `DicomInstanceDto`를 반환한다. 스트림은 반환 전에 닫는다.
- 다중 프레임은 메타데이터 단계에서도 `UnsupportedDicomImageException`이다. 유효하지 않은 파일을 유효한 메타데이터로 반환하지 않는다.

### 3.4 Window/Level
- 기본 Window/Level: `WindowCenter`와 `WindowWidth` 둘 다 없을 때만 `WindowLevel.Default`(400, 40).
- 한쪽만 있거나, 개수가 다르거나, 비어 있거나, 폭이 1 미만이거나, 값이 비유한이면 `InvalidDataException`. 조용한 기본값으로 대체하지 않는다.
- 여러 쌍이 있으면 첫 번째 쌍을 기본값으로 쓴다.

### 3.5 VOI LINEAR (DICOM PS3.3 C.11.2.1.2.1)
- `x <= c-0.5-(w-1)/2` 이면 0, `x > c-0.5+(w-1)/2` 이면 255, 그 밖은 `((x-(c-0.5))/(w-1)+0.5)*255`.
- **양자화 결정(고정):** 연속값 y를 0 방향으로 절삭(truncate)하고 `[0,255]`로 고정한다. 규격은 반올림 규칙을 명시하지 않으므로 이 선택을 문서화하며, 테스트는 절삭 결과가 반올림과 다른 중간값(예: 128.14→128)으로 이 선택을 고정한다.
- 상수(`c-0.5`, 반폭, `w-1`, 하한·상한)는 `ApplyWindowLevel` 호출당 한 번만 계산한다. 마스크와 부호 비트도 루프 밖에서 계산한다.
- `w == 1`이면 하한과 상한이 같아 나눗셈 분기는 도달하지 않는다.

### 3.6 출력 버퍼와 취소
- 호출자 소유 `Span<byte>`에 직접 기록한다. 풀 대여 후 전체 복사를 하지 않는다.
- 취소 확인 간격은 **65536픽셀 블록**이다. 행(row) 단위가 아니다. 시작 전 취소된 토큰은 아무 것도 쓰기 전에 `OperationCanceledException`을 던진다.
- `destination.Length < 픽셀 수` 는 `ArgumentException`, `WindowWidth < 1` 또는 비유한은 `ArgumentOutOfRangeException`.
- `PixelData` 검증은 폭·높이가 양수이고 64비트 곱이 `Array.MaxLength` 이하이며 원시 버퍼가 충분한지 확인한다.

### 3.7 시리즈 정렬
- 기하 판정 순서: 시리즈·프레임 식별자 → IPP/IOP 누락 → IOP 값 → IPP 값 → 투영 중복.
- 시리즈 식별자 또는 프레임 식별자가 모두 없으면 `MissingGeometry`(공간 주장 없음). 일부만 있거나 값이 다르면 `AmbiguousGeometry`.
- IPP 또는 IOP가 없으면 `MissingGeometry`. 잘못된 IOP(단위·직교·길이), 값이 다른 IOP, 투영이 1e-3 mm 안에서 겹치면 `AmbiguousGeometry`.
- `Spatial`: 법선 `row × column`에 대한 `dot(IPP, n)` 오름차순, 동률은 InstanceNumber→SopInstanceUid→FilePath.
- 그 밖의 상태: InstanceNumber→SopInstanceUid→FilePath 결정적 순서이며 **물리 위치가 아니다.**

### 3.8 PACS 매핑
- 로컬 폴더: 공통 `DicomInstanceMapper`로 헤더를 DTO로 바꾸고 결정적 순서를 쓴다.
- Orthanc: `MainDicomTags`의 `SeriesInstanceUID`, `FrameOfReferenceUID`, `ImagePositionPatient`, `ImageOrientationPatient`를 읽는다. 백슬래시로 구분된 문자열을 `double[]`로 바꾸며, 파싱 실패는 빈 배열(잘못된 기하)이다. 태그가 없으면 `null`(누락)이다.
- **상태:** 매핑은 구현되어 있다. 실제 Orthanc 서버에서 해당 태그가 반환되는지는 확인하지 않았다.

### 3.9 썸네일
- `ThumbnailService`는 `LoadImageAsync`로 로드하고 호출자 소유 버퍼에 `ApplyWindowLevel`을 호출한다.

## 4. 테스트 (신규 파일만)

모두 공개 계약을 소비하는 관점이며 구현 리터럴, 문구, 소스 텍스트, 배선은 검사하지 않는다.

| 파일 | 검증 내용 |
| --- | --- |
| `Tests/Domain/ImageViewportTransformTests.cs` | 왕복, 범위 밖 판정과 클램프, 회전·플립·이동 순서, DPI 배율에 대한 px 거리 불변성, 잘못된 뷰포트·확대율 예외 |
| `Tests/Application/PixelSpacingTests.cs` | 미확인 간격의 px 거리, 무효 축, 비등방 mm 거리 |
| `Tests/Application/SeriesNavigationGeometryTests.cs` | 사선 방향 투영 정렬(InstanceNumber·파일명 뒤섞음), 누락 기하, 투영 중복, 프레임 식별자 혼합·불일치·전체 누락, 빈 시리즈 |
| `Tests/Infrastructure/VoiLinearWindowingTests.cs` | 하한·상한 경계, w=1 임계값, 부호 확장(BitsStored 기준), rescale 순서, 절삭 방향, 8비트, 짧은 출력 버퍼, 폭 1 미만, 0 폭, 64비트 크기 초과, 사전 취소 시 무기록 |
| `Tests/Infrastructure/SyntheticDicomLoadTests.cs` | 다중 프레임(로드·메타데이터), HighBit 불일치, 0 행, 배열 한계 초과(헤더 단계 거부), WindowCenter만 있음, 개수 불일치, 0 간격의 Unknown, 비등방 간격 축, 메타데이터 읽기 |
| `Tests/Infrastructure/FixedFixtureSmokeTests.cs` | 고정 기준선 `0001.dcm`의 헤더 사실, 기본 Window/Level, 저장 int16 네 개 값과 pydicom으로 독립 계산한 출력 네 개의 정확한 일치 |

고정 자료 테스트는 파일이 없으면 실패한다. 건너뛰지 않는다.

## 5. 제약

- 초기 문서 이전 단계의 제약: 커밋·푸시·인증 변경을 하지 않았다. 기존 미커밋 변경은 사용자의 것이므로 덮어쓰지 않는다.
- 핵심 단계(Domain·Application·Infrastructure·테스트)에서는 새 패키지를 추가하지 않았다. 이후 UI 검증 단계에서는 FlaUI.UIA3 5.0.0을 루트 `Directory.Packages.props`의 CPM으로만 추가했다(`tools`·`src` 프로젝트의 버전 직접 지정 없음). fo-dicom 5.2.5, .NET 10.0.403은 유지한다.
- 핵심과 ViewModels는 WPF에 의존하지 않는다.
- 호환 shim, 옛 API 별칭, 미구현 스텁은 금지한다.
- 전체 솔루션 빌드는 표면 작업자 준비 후에만 한다. 그 전에는 핵심 프로젝트와 격리된 테스트 프로젝트만 빌드한다.

## 6. 진행 상태와 동결 체크포인트 (관찰된 결과만)

### 6.1 일정 (사용자 지정)
- 후보 동결 기록: 2026-10-27, 코드 SHA `aef18975c226e82e5db43c7b240f8d8d19259d92`(G). 체크포인트 2026-10-29 이전에 기록. 게시 마감: 2026-10-30.
- 기능 축소나 신규 기능 추가는 하지 않는다. 남은 작업은 정확성 보완, 검토, 릴리스 검증이다.

### 6.2 핵심 작업 상태 (관찰)
1. 계약 확정: 완료.
2. Domain·Application·Infrastructure 구현: 완료. 3.2–3.8의 규칙이 코드에 있다.
3. 격리 빌드(Domain·Application·Infrastructure, `--no-restore`): 0 오류, 0 경고 (관찰됨).
4. 격리 핵심 테스트: 45개 중 45개 통과 (관찰됨). 테스트 하네스는 `.git` 아래 임시 프로젝트였고 실행 후 삭제했다.
5. 정확성 보완 점검: HighBit, 크기 한계, 지연 메타데이터 읽기, 창 쌍 검증, VOI 상수 호이스팅, pydicom 기대값 — 위 45개에 포함, 통과 (관찰됨).
6. 전체 솔루션 빌드, ViewModels·UI 통합: G 최종 소스의 Release 빌드에서 오류 0, 경고는 OpenSilver 계열에만 있음(집계 9), 앱·핵심 경고 0 (실제 출력 확인, E7).
7. 최종 익명 클린 클론과 150개 전체 LFS 복원 검증: G에서 완료. 빈 캐시 0 → 원격 pull 150, 전체 manifest 대조, 자료 크기 78,793,078 bytes, dataset verify exit 0, 3개 restore·build 경고 0·오류 0, core 273/0/0. 기록은 VALIDATION E19.
8. 실제 UI 검증: 승인된 3개 시나리오는 별도 실행에서 각각 통과했다(E20). G 소스에서 그중 1개 케이스를 새로 실행해 통과했다(E24, 번호·범위 안정성 검사). 세 시나리오 배치 실행과 네트워크 차단 증명은 아직 없다.

### 6.3 다음 단계
- 검토 → 독립 릴리스 검증. 릴리스 검증은 통합 스냅샷이 준비된 뒤에만 실행한다.
- README 지원 서술은 문제, 설계, 소유자 역할과 AI 보조, 검증, 한계 순서로 쓰며, 의료 인증·임상 성능·성능 수치 주장은 넣지 않는다.

### 6.4 관찰되지 않은 것
- 이전 바이너리 기반 before/after 비교: 불가 (`bin/` 출력이 기준 이전 빌드가 아니다). 기준 비교는 `7d0d080` 소스 공식에 근거하며 실행 증거가 아니다.
- 압축 구문 거부: 합성 파일로 검증하지 않았다. 코드 경로만 있다.
- Orthanc 실제 응답: 미확인.
- 앱 실행: 시작 관찰(읽기 전용)과 승인된 3개 UI 시나리오 별도 실행이 있다. 네트워크 차단 상태의 UI 실행, 배치 실행, 테마 전환은 실행하지 않았다.

## 7. UI 게이트

- **UI 입력 자동화**(클릭, 키 입력, 포인터 조작, 자동 테스트 주입)는 실행 직전 사용자의 즉시 승인을 받아야 한다. 승인은 해당 조작 범위에 한정한다.
- **앱 실행과 읽기 전용 관찰**(창 상태 확인, 읽기 전용 캡처)은 입력 자동화가 아니므로 위 승인 대상이 아니다. 단, 입력을 주입하지 않아야 하며 결과는 실제 관찰한 것만 보고한다.
- 사용자의 "continue"는 설계 승인이다. 입력 자동화의 실행 승인으로 해석하지 않는다.
- UI 검증 항목은 실행하지 않았거나 승인이 없으면 "미실행"으로 보고한다.

## 8. 미결 사항

- 압축 구문과 Orthanc 실제 태그 가용성은 실제 자료 또는 서버가 있어야 검증할 수 있다.
- 픽셀 패딩(PixelPaddingValue) 처리는 이번 범위에 없다.
