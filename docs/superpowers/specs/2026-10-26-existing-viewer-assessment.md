# 기존 공개 DICOM 뷰어 조사와 고도화 규약

- 대상: [christian289/wpf-dicom-viewer](https://github.com/christian289/wpf-dicom-viewer).
- 조사 기준 리비전: `f42cafc058db35707d46b56613a01f82ac2684ce`. 아래 소스 관찰은 이 리비전에 한정하며 현재 코드 전체를 재평가한 결과가 아니다.
- 상태: 읽기 전용 소스 조사와 후속 고도화 요구사항. 앱 실행·버그 재현·빌드·테스트·성능 측정 결과가 아니다.
- 문서 인계일: 2026-10-27. 이 저장소에서 기존 뷰어를 개선하기 위한 자체완결 명세이며 다른 프로젝트의 설계·정책 파일을 요구하지 않는다.
- 이번 변경 범위(최초 문서 이전 단계의 역사 기록): 이 문서의 이전·공개 검토·규약 정리만 수행했다. 당시에는 코드 복제, 앱 기능 구현, 데이터 반입, Git LFS 설정, 패키지 추가, 설정 마이그레이션을 포함하지 않았다. 이후 단계의 현재 상태는 제7절을 따른다.
- **문서 이전 커밋 기준으로** 실시간 TCIA 코드 교체, 고정 DICOM 등록, WPF-UI 적용, NuGet·SDK·서식·솔루션 설정 마이그레이션은 수행하지 않았다. 설정 마이그레이션은 별도 커밋·검증으로 구분하며 앱 기능 구현은 후속 작업이다.

## 1. 조사 방법과 해석 범위

조사 당시 GitHub 저장소 메타데이터, 전체 재귀 트리와 아래 공개 소스 파일을 읽었다. 전체 트리는 `truncated=false`였다. 라이선스 메타데이터는 null이었고 LICENSE/LICENCE/COPYING 계열 파일은 트리에서 확인되지 않았다. 이는 코드·자산 복사나 재라이선스를 허용하는 근거가 아니다.

설정·개인 설정·기존 작업 하네스 원문은 반입하지 않는다. 공개 저장소라는 이유만으로 모든 자산의 재배포 권한을 인정하지 않는다. 문서에는 공개 근거와 뷰어에 필요한 정책만 두며 비공개 업무 식별자·작업 기록·환자 메타데이터 값은 포함하지 않는다.

## 2. 확인된 사실과 고도화 결정

| 관찰 파일·심볼 | 조사 리비전의 소스에서 확인한 사실 | 뷰어 고도화 결정 |
| --- | --- | --- |
| `Directory.Build.props` | 공통 타깃은 net10.0-windows이고 UseWPF가 켜져 있다 | Windows·WPF 의존성은 WPF 호스트·UI 경계에 한정하고 핵심·ViewModels의 독립 타깃을 유지한다 |
| `src/DicomViewer.ViewModels/DicomViewer.ViewModels.csproj` | net10.0·UseWPF=false, CommunityToolkit.Mvvm 8.4.0, Application 참조 | WPF 없는 ViewModels를 유지한다. 새 패키지는 실제 버전·API·라이선스를 확인하고 제6절의 중앙 설정으로 관리한다 |
| `src/DicomViewer.Infrastructure/DicomViewer.Infrastructure.csproj` | net10.0, fo-dicom 5.2.5와 Microsoft.Extensions.Http 10.0.1 참조 | DICOM·HTTP는 인프라 경계에 둔다. 조사 버전을 무조건 갱신하거나 다른 프로젝트에서 복사하지 않는다 |
| `FoDicomImageService.LoadPixelDataAsync`, `LoadPixelDataFromStreamAsync` | CancellationToken을 받지만 확인한 메서드 본문에서 사용하지 않는다 | 취소 확인 지점과 결과 게시 세대를 별도로 정의하고 취소·선택 변경 후 낡은 결과가 표시되지 않게 한다 |
| `FoDicomImageService.GetPixelSpacing` | PixelSpacing 누락 시 ImagerPixelSpacing을 시도하고 실패·예외 시 PixelSpacing.Default를 반환한다 | 환자 공간 간격의 누락·오류를 숨기지 않고 px로 표시한다. 다른 종류의 간격을 임의 대체하지 않는다 |
| `src/DicomViewer.Application/DTOs/PixelSpacing.cs` | Default는 1.0×1.0mm이며 거리 계산은 축별 간격을 사용한다 | 축별 계산은 유지하되 미확인 간격을 1mm로 만들지 않는다 |
| `SeriesNavigationService.SetSeries` | SliceLocation 또는 InstanceNumber로 정렬한다 | 위치·방향의 투영값으로 정렬하며 기하 부족·스택 모호함을 표시한다 |
| `FoDicomImageService.ApplyWindowLevel` | center±width/2 구간과 255/width 스케일을 사용한다 | 지원하는 VOI LINEAR의 c-0.5·w-1·width=1 경계를 DICOM 규격과 독립 기대값으로 검증한다 |
| `FoDicomImageService.ExtractPixelData` | GetFrame(0)으로 프레임을 얻는다 | 단일 프레임 지원을 명시하고 멀티프레임을 첫 프레임 성공처럼 처리하지 않는다 |
| `FoDicomImageService.ApplyWindowLevel` | ArrayPool 작업 버퍼를 정확한 길이의 새 배열로 복사한다 | 호출자 소유 출력·버퍼 재사용으로 불필요한 통짜 복사를 피하고 UI 복사는 별도로 계측한다 |
| `src/DicomViewer.Domain/ValueObjects/ImageTransform.cs` | 이동·확대·회전·반전 상태를 불변 record로 표현한다 | 원본 측정 좌표와 표시 변환을 분리하고 화면→영상 역변환·DPI 불변성을 검증한다 |
| `src/DicomViewer.Tests/Infrastructure/TciaServiceTests.cs` | 모의 HTTP 응답 기반 TCIA 서비스 검사가 있다 | 이를 영상 기하·수치·취소 경합·실제 UI 검증의 증거로 사용하지 않는다 |
| `src/DicomViewer.Infrastructure/Tcia/TciaService.cs` | collection/patient/study/series 조회와 getImage ZIP 다운로드·압축 해제를 수행한다 | 앱·회귀의 기본 경로는 최초 1회 수집 후 Git LFS에 고정한 공개 DICOM의 로컬 재사용으로 바꾼다 |

이 표는 사용자 환경에서 재현한 장애 목록이 아니다. 기존 코드 전체의 정확성·운영 안정성·개인 기여를 판정하지 않는다.

### 2.1 테스트 인벤토리의 한계

조사 트리에서 확인한 테스트 프로젝트는 `src/DicomViewer.Tests/DicomViewer.Tests.csproj`이며 `*Test.cs`/`*Tests.cs` 파일명으로 확인된 파일은 위 TCIA 테스트 한 개다. 이름 규칙 밖의 검증까지 없다고 단정하지 않는다.

해당 파일은 모의 HttpMessageHandler를 사용하여 응답 파싱·빈 응답·연결 성공/실패를 검사한다. 영상 좌표·픽셀 변환·취소 경합·네이티브 수명·실제 UI가 검증됐다는 근거가 아니다. 이번 문서 이전에서 기존 테스트를 실행하거나 수정하지 않았다.

## 3. 공개 DICOM 선정과 보존 정책

### 3.1 족부·발목 우선과 현재 대체재

목표는 WBCT에 가까운 족부·발목 영상의 표시·정렬·측정 회귀다. 공개 DICOM 여부와 재배포 권한을 함께 확인한 자료를 우선하며 **일반 CT를 실제 WBCT라고 표기하지 않는다.** 조사 범위에서 직접적인 공개 WBCT DICOM과 재배포 권한을 함께 확인하지 못했으며, 전 세계에 없다는 뜻은 아니다.

| 항목 | 고정할 공개 근거·선정 기준 |
| --- | --- |
| 현재 확인된 공개 대체재 | Harvard Dataverse의 Visible Human Project CT Datasets Male and Female 중 여성 발목 시리즈 |
| 식별자 | DOI `10.7910/DVN/3JDZCT`, dataset version `1.0` |
| 선택 파일 | `VHFCT1mm-Ankle (숫자).dcm` 150개. 확장자뿐 아니라 DICOM CT SOP Class도 검증 대상으로 삼는다 |
| 원본 페이지 | [Harvard Dataverse DOI](https://doi.org/10.7910/DVN/3JDZCT) |
| 버전·라이선스 근거 | [v1.0 공개 메타데이터](https://dataverse.harvard.edu/api/datasets/:persistentId/versions/1.0?persistentId=doi:10.7910/DVN/3JDZCT), `CC0-1.0` |
| 이용조건 | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/), [NLM 원 데이터 이용조건](https://www.nlm.nih.gov/databases/download/terms_and_conditions.html) |
| 출처 표시 | Courtesy of the U.S. National Library of Medicine |
| 해부학적 범위 | [University of Iowa 공식 안내](https://mri.medicine.uiowa.edu/equipment-information/scanner-images/visible-human-project-ct-datasets) |
| 한계 | **비체중부하 사체의 일반 CT이며 WBCT·체중부하 촬영·CBCT가 아니다** |
| 회귀 목적 | 족부·발목 주변 뼈 표시, 슬라이스 정렬, Window/Level, 거리 측정, 버퍼 수명 |

NLM이 이 프로젝트·결과를 보증한다는 뜻이 아니다. 고정 버전의 재현성을 위한 사용이며 최신·가장 정확한 원 자료를 반영한다고 주장하지 않는다. 자료의 라이선스·출처 고지는 신규 코드의 라이선스와 별도로 보존한다.

### 3.2 최초 수집·검토·Git LFS 고정

앱·회귀·시연은 **최초 1회 수집 → 출처·권한·메타데이터 검토 → Git LFS 커밋 → 고정 Git revision의 로컬 DICOM 재사용**을 따른다.

- 앱 실행·회귀 재실행 때마다 원 출처나 TCIA API를 호출하지 않는다. 기존 실시간 다운로드·캐시 삭제 흐름을 새 기본 경로로 이어받지 않는다.
- clone/checkout 뒤 `git lfs pull`은 선택한 revision의 등록 객체 복원이며 원 출처 재수집과 다르다. 객체가 이미 있으면 `git lfs checkout`으로 복원할 수 있다.
- DICOM은 일회성 캐시·빌드 생성물이 아니다. 정리 작업으로 삭제하거나 ignore하지 않는다. 원 출처 변경으로 기존 기준선을 자동 덮어쓰지 않는다.
- 새 자료·수정·기대값은 매니페스트와 같은 커밋에서 관리한다. 과거 바이너리 보존·저장/전송 쿼터를 고려하여 의미 없는 재저장과 전체 재생성을 피한다. 이력 재작성·LFS migrate는 이 계획에 포함하지 않는다.
- LFS는 암호화·개인정보 삭제·라이선스 허가를 대신하지 않는다. 미복원 포인터·객체 누락은 명시적 오류이며 빈 데이터 성공이나 몰래 원 출처 다운로드로 대체하지 않는다.
- 저장 위치는 `data/dicom/vhp-female-ankle-v1/`이다(`manifest.json`, `sanitized/0001.dcm`~`0150.dcm`, `NOTICE.md`). 수집과 오프라인 검증은 실행했으며, 깨끗한 clone 복원 증명은 기준 커밋 3dd5dc7에서 실행했다(결과는 도구 README에 기록).

### 3.3 메타데이터 최소화와 검증 경계

공개 다운로드와 CC0만으로 원본의 모든 태그를 다시 공개해도 안전하다고 판단하지 않는다. 실제 환자 자료·원본 환자 메타데이터 값·private tag·원본 자유 텍스트·원본 sequence·개인 정책은 반입하지 않는다.

- 원본 DICOM은 저장소 밖에 보관하지 않는다. 수집 도구가 원본을 메모리에서만 내려받아 사용하며 디스크·저장소·로그에 쓰지 않는다. 환자 필드 값은 로그·문서·테스트·커밋에 출력하지 않는다.
- 저장 사본은 빈 데이터셋에서 시작해 허용 영상 속성만 가져온다. 원본을 통째로 복사한 뒤 일부 식별 필드만 지우는 방식은 사용하지 않는다.
- 허용 속성은 Rows, Columns, BitsAllocated, BitsStored, HighBit, PixelRepresentation, SamplesPerPixel, PhotometricInterpretation, PixelSpacing, ImageOrientationPatient, ImagePositionPatient, SliceThickness, SpacingBetweenSlices, RescaleSlope, RescaleIntercept, RescaleType, WindowCenter, WindowWidth, VOILUTFunction, PixelPaddingValue, PixelPaddingRangeLimit, PixelData로 한정한다. 존재하는 속성만 복사한다.
- Study/Series/SOP/FrameOfReference UID는 공개 회귀 자료 키에서 결정적으로 새로 발급한다. 필수 식별 항목은 명백한 가상값을 사용하고 민감 메타데이터는 비운다. 필수 DICOM 구조·파일 메타데이터는 작성기가 생성하며 DERIVED/SECONDARY로 표시한다.
- PixelData 바이트와 기하를 보존한다. JPEG 임의 변환·픽셀 재인코딩을 하지 않는다. 변환 도구·버전·처리 정책을 기록하고 원본/저장본의 픽셀·기하 해시를 분리한다.
- 동일 라이브러리 왕복 확인과 다른 DICOM 파서의 독립 판독은 구분한다. 이 뷰어의 실제 판독·실행 전에는 호환성 통과라고 쓰지 않는다.
- 이 절차는 DICOM PS3.15 전체 비식별화 적합성 인증이 아니다. 픽셀 내 식별 표기·재식별 가능성을 별도 검토하고 새 자료를 자동 안전 판정하지 않는다. 실제 환자 자료 반입·클라우드 전송 기능을 이 계획의 전제로 삼지 않는다.

매니페스트에는 schemaVersion, datasetId, sourceUrl, sourceVersion, licenseId, licenseUrl, redistributionAllowed, files를 둔다. 파일별로 path, sourceFileId, downloadUrl, originalFilename, sourceMd5, sourceSha256, sourceSize, sha256, size, lfsOid, 원본/저장본 PixelData·기하 해시와 slicePositionMm를 기록한다. 원본 파일명도 공개·비식별 여부를 검토한 값만 허용한다. 기하 해시는 PixelSpacing·ImageOrientationPatient·ImagePositionPatient·Rows·Columns의 정규화 값을 대상으로 한다.

파일 path는 매니페스트 디렉터리 기준이며 외부·상위 경로를 허용하지 않는다. **저장 SHA-256 = LFS OID의 SHA-256 = 복원 payload의 SHA-256**이어야 한다. 원 출처 MD5는 원본 확인용이며 변환된 저장본과 비교하지 않는다. 해시·크기·포인터 검증만으로 DICOM 의미·비식별화·재배포 권한을 검증했다고 주장하지 않는다. 공개 clone의 자료 검증은 개인 정책에 의존하지 않아야 한다.

### 3.4 기준 자료의 한계와 제외 기준

선정 자료의 확인 프로필은 단일 CT 시리즈 150평면, Explicit VR Little Endian, CT Image Storage, 512×512, 16-bit, BitsStored=16, HighBit=15, MONOCHROME2다. 저장 사본의 헤더만 집계한 결과(환자 필드 제외)는 PixelSpacing (1.0,1.0)mm 150개, ImageOrientationPatient 항등 150개, SliceThickness 1.0 150개, ImagePositionPatient z 0.0~149.0mm 고유 150개(간격 1.0mm)였다. 독립 pydicom 검사는 저장 사본만 대상이다. 수집기는 원본을 메모리에서 fo-dicom으로 판독해 허용 목록 사본을 만들고 원본과 저장 사본의 픽셀·기하 일치를 확인했다. 이 확인은 판독기 간 호환성이나 임상 적합성을 증명하지 않는다.

파일명·InstanceNumber·공간 위치 순서가 같은 등방 기준선만으로 공간 정렬·비등방 측정의 정확성을 구분할 수 없다. 뒤섞인 파일명·InstanceNumber, 비등방 간격·회전·누락 태그·오류 경계는 별도 합성 DICOM으로 검증한다. 분할 정답이 검증된 자료가 아니므로 임상 정확도·Dice의 근거로 사용하지 않는다.

- [NLM VHP의 IDC DICOM 변환 배포](https://zenodo.org/records/12690050)는 별도 파일·권한·형식 검토 전 현재 기준선에 섞지 않는다.
- [발 CAD 모델](https://zenodo.org/records/5192130)은 CT 기반 설명만으로 원본 DICOM 제공을 확인한 것으로 보지 않는다.
- [OsiriX 샘플](https://www.osirix-viewer.com/resources/dicom-image-library/)은 조사에서 공개 Git LFS 재배포 금지 조건을 확인하여 제외했다.
- 실제 WBCT를 확보하면 출처·체중부하 상태·장비/자세 근거·권한·지원 형식을 별도 검토하고 새 datasetId·Git/LFS revision으로 추가한다. 발 사진·STL·NIfTI를 원본 DICOM이라고 쓰지 않는다.

## 4. 뷰어의 재사용·확장 경계

개선의 가치는 다른 이름의 동일 뷰어를 만드는 데 있지 않다. 미확인 단위 표시, DICOM 공간 정렬, LINEAR 경계, 취소·선택 경합을 명시적인 요구사항과 실행 증거로 개선한다.

- 기존 Domain·Application·Infrastructure·ViewModels·UI·WpfServices·WpfApp 책임 분리를 출발점으로 삼고 무관한 새 앱을 만들거나 코드를 복제하지 않는다.
- 향후 합성 촬영 결과나 저장 이력 재열기를 연결할 때도 같은 DICOM 로딩 경로와 고정 공개·합성 자료를 사용한다. 촬영 완료와 동기화 완료는 별도 상태이며 연관 영상·오류 이벤트는 같은 작업 식별자로 연결한다. 이 연계는 이번 문서 이전의 구현 범위가 아니다.
- TCIA 실시간 탐색, OpenSilver, 외부 PACS를 모두 고도화 범위로 끌어오지 않는다. 우선순위는 허용된 공개 발목 CT의 고정 기준선과 합성 경계 사례다.
- 네이티브·추론 확장을 별도로 진행한다면 공용 계약은 UI 없는 경계에 둔다. P/Invoke·DLL 로딩·ABI 변환은 Infrastructure와 구분한 Interop 책임으로 한정한다. LibraryImport·버퍼 소유권·해제·취소 계약과 단일 DLL 빌드 절차를 해당 구현 전에 명시한다.
- 추론 확장 시 .NET ONNX Runtime에서 CPU 기본·CUDA 선택·TensorRT EP 선택을 명시하고 C++은 전처리·후처리로 한정한다. 모델 출처·권한·학습/평가 분리·SHA-256을 고정하고 허용 ONNX만 LFS 관리한다. 환경 종속 엔진·SDK·캐시는 원본 자산과 구분한다. 공개 CT 적용은 비진단 실험이며 합성 held-out 정답 평가와 구분한다.
- 검증은 실제 소비자 동작에 집중한다. 기하·수치·상태 전이는 단위 검증, 도입한 실제 DLL·SQLite·루프백 HTTP는 해당 경계의 통합 검증, WPF 표면은 별도 UI 검증으로 구분한다. 사용하지 않는 의존성을 검증 명목으로 추가하지 않는다. xUnit 기반 앱 검증과 xUnit+FlaUI UIA3 UI 검증을 구분한다.

## 5. UI 규약: CustomControl 기본, WPF-UI 기본 테마

### 5.1 컨트롤과 업무 조합 View의 책임

**새 기본·재사용 컨트롤 단위는 CustomControl이 기본이다. UserControl은 여러 CustomControl·표준 컨트롤을 조합하여 하나의 비즈니스·업무 화면을 표현할 때만 사용한다.**

| 구분 | 책임·허용 범위 |
| --- | --- |
| 기존 WPF·WPF-UI 컨트롤 | 제공 기능을 직접 사용한다. 모양만 바꾸면 Style·ControlTemplate·DataTemplate을 우선하고 전부 재래핑하지 않는다 |
| 새 재사용 CustomControl | 재사용 동작·표현과 DependencyProperty·명령·템플릿 계약을 담당한다. 업무 화면의 데이터 조회·저장·탐색을 소유하지 않는다 |
| 업무 조합 View | 여러 컨트롤의 배치와 업무 단위 상호작용을 ViewModel에 연결한다. 이 경우에만 UserControl을 사용하며 범용 컨트롤의 대용품으로 만들지 않는다 |
| ViewModel·핵심 | 업무 상태·명령·계약을 담당하고 System.Windows·Wpf.Ui 타입과 리소스 탐색을 참조하지 않는다 |
| WPF 호스트·WPF 서비스 | 테마 자원과 UI 서비스의 실제 연결을 담당한다. UI 라이브러리를 핵심 계층에 전파하지 않는다 |

새 컨트롤마다 기존 컨트롤의 직접 사용·스타일 확장으로 충분하지 않은 이유를 기록한다. 기본 스타일은 `src/DicomViewer.UI/Themes/Generic.xaml`의 기존 병합 구조를 따르고 컨트롤별 자원과 업무 View를 구분한다. 기존 렌더링 요소의 기반형을 문서 규약만으로 일괄 교체하지 않는다. 직접 렌더링이 필요한 내부 요소와 템플릿 외부 계약은 별도로 판단한다.

### 5.2 WPF-UI 버전·리소스·API 확인

기본 테마는 **WPF-UI(Wpf.Ui)**다. 이는 후속 적용 결정이지 현재 앱에 적용 완료했다는 뜻이 아니다. 문서 인계 시점에 프로젝트의 PackageReference에는 WPF-UI가 없었다.

검토 기준 패키지는 **NuGet `WPF-UI` 4.3.0**, 어셈블리 `Wpf.Ui` 4.3.0.0, `net10.0-windows7.0` 자산이며 MIT 라이선스와 `WPF-UI.Abstractions` 의존성을 확인했다. 실제 도입 버전은 구현 시 다시 검증하고 제6절 방식으로 명시적으로 고정한다. 예제의 와일드카드 버전·오래된 리소스 경로를 그대로 복사하지 않는다.

- 패키지 타입 목록·동봉 XML과 공식 안내에서 리소스 진입점 `Wpf.Ui.Markup.ThemesDictionary`와 `ControlsDictionary`, `ThemesDictionary.Theme`를 확인했다. XAML 네임스페이스는 `http://schemas.lepo.co/wpfui/2022/xaml`이며 테마 사전 다음 컨트롤 사전을 병합한다. 구형 `Styles/Controls.xaml` 경로를 추정하여 하드코딩하지 않는다.
- 실제 어셈블리 검사에서 `Wpf.Ui.Controls.FluentWindow.ExtendsContentIntoTitleBar`·`WindowBackdropType`과 `Wpf.Ui.Controls.NavigationView`의 API를 확인했다. 예를 들어 `IsBackButtonVisible`은 WPF Visibility가 아니라 `NavigationViewBackButtonVisible` 타입이다. 컨트롤을 사용할 때 해당 버전의 실제 속성·enum·템플릿을 다시 확인한다. 이 확인만으로 새 탐색 화면 도입을 요구하지 않는다.
- `Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme()`와 `Apply(ApplicationTheme, WindowBackdropType, bool)`를 확인했다. 테마 적용은 WPF 경계에서 수행하며 핵심·ViewModel에서 직접 호출하지 않는다.
- 리소스 타입의 상세 어셈블리 검사에는 PresentationCore 의존성 해석 제한이 있어 동봉 XML·공식 API 문서로 보완했다. **실제 앱의 리소스 로딩·테마 전환·컨트롤 표시를 실행 검증한 것은 아니다.** 후속 구현에서 현재 버전에 맞는 리소스·컨트롤 API와 실제 시작 화면을 함께 검증한다.

## 6. 저장소 공통 설정 규약

### 6.1 NuGet: Directory.Packages.props CPM

**NuGet 패키지 버전은 루트 `Directory.Packages.props`의 Central Package Management(CPM)로 관리한다.** `ManagePackageVersionsCentrally=true`와 `PackageVersion`을 사용한다.

- 각 csproj의 필요한 PackageReference와 프로젝트·대상 프레임워크별 조건은 유지한다. 중복 Version과 VersionOverride는 제거하고 의도적인 버전 차이는 중앙 PackageVersion의 조건으로 표현한다.
- 테스트 SDK·러너·커버리지·모의 라이브러리도 포함한다. PrivateAssets·IncludeAssets·ExcludeAssets 등 비버전 메타데이터를 보존하고 테스트 패키지를 앱으로 전파하지 않는다.
- WPF-UI를 포함한 이후 패키지 추가·버전 변경도 이 규약을 따른다. 이번 설정 이전만을 이유로 기존 패키지 버전을 올리거나 WPF-UI를 설치하지 않는다.
- 별도 사용자 정의 targets 파일과 Import 브릿지 방식은 사용하지 않는다. 다른 중앙 관리 방식으로 조용히 대체하지 않는다.

### 6.2 빌드·SDK·솔루션

- 루트 `Directory.Build.props`는 공통 빌드 규약을 담당한다. 기존 프로젝트별 타깃·WPF 사용 여부·조건의 의도적인 차이를 보존하고 ViewModels·핵심에 WPF를 전파하지 않는다.
- 루트 `global.json`은 이 환경에 설치된 .NET SDK `10.0.403`으로 고정한다. `rollForward`는 `disable`, `allowPrerelease`는 `false`로 명시하여 실제 검증 SDK를 재현한다. SDK 고정이 restore/build 통과를 대신하지 않는다.
- 솔루션은 `.slnx`를 기본으로 한다. 기존 `.sln` 전환 시 프로젝트·구성 매핑을 보존하고 명령·문서·CI 참조를 함께 변경하며 불필요한 중복 `.sln`은 남기지 않는다.

### 6.3 XAML·EditorConfig와 실행 승인

- 루트 `Settings.XamlStyler`를 XAML 서식 기준으로 둔다.
- 루트 `.editorconfig`는 .NET 10 SDK 표준 템플릿을 출발점으로 사용하고 `root=true`로 적용한다. 들여쓰기·줄바꿈 등 XAML과 공유하는 규칙은 Settings.XamlStyler와 일치시킨다. 무관한 소스 전체 재서식은 설정 마이그레이션에 섞지 않는다.
- formatter 실행은 서식 검증이지 앱·UI 실행 검증이 아니다. 앱 실행·UI 표시·사용자 동작 검증 결과와 구분한다. **UI 자동조작은 실행 직전 사용자의 승인을 받아야 한다.**

### 6.4 마이그레이션 검증·커밋 경계

실제 설정 마이그레이션은 문서 이전·정상 푸시 결과를 먼저 전달한 뒤 **별도 커밋**으로 수행한다. 전체 프로젝트의 참조·조건·평가 버전과 솔루션 구성을 비교하고 restore/build로 실제 적용을 확인한다. 테스트·OpenSilver 등 기존 프로젝트를 묵시적으로 제외하거나 필요한 참조를 삭제하지 않는다. 이 단계에는 앱 기능 구현을 포함하지 않는다.

### 6.5 설정 마이그레이션의 실행 기록

문서 이전 후 별도 설정 변경에서 기존 13개 프로젝트의 PackageReference 26개·고유 버전 21개를 조사했다. 버전만 CPM으로 이동했으며 프로젝트 XML의 참조·조건·비버전 메타데이터와 평가된 타깃·WPF·공통 빌드 값은 보존했다. Directory.Build.props의 기존 공통 기본값은 그대로 유지했다. 솔루션은 이미 `src/DicomViewer.slnx`였으므로 형식 변환이나 중복 sln 생성은 하지 않았다.

- SDK 10.0.403에서 전체 slnx restore와 `build --no-restore`를 실행했다. 빌드는 오류 0개·경고 19개였다. NU1507 패키지 소스 매핑, NU1701 패키지 호환성, CS8603/CS8604 nullable, CS2002 중복 소스 경고를 숨기거나 관련 없는 코드 수정으로 처리하지 않았다.
- 기존 테스트 프로젝트를 `--no-build --no-restore`로 실행하여 8개 통과·실패 0개·건너뜀 0개를 확인했다. 공개 DICOM·영상 기하·UI 검증으로 확대 해석하지 않는다.
- XamlStyler.Console 3.2501.8을 실제 실행하여 Generic.xaml의 stdout 미리보기를 확인했다. 공백 4칸·CRLF와 XML 의미 보존, 원본 XAML 미변경을 확인했다. 무관한 소스 전체를 재서식하지 않았다.
- 전체 프로젝트의 CPM 자동 적용·중앙 버전·SDK 고정을 MSBuild 평가로 확인했다. 앱·UI 실행이나 UI 자동조작은 수행하지 않았다.

## 7. 후속 실행 순서와 완료 증거

공통 설정 단계와 2단계의 수집·오프라인 검증·저장 사본 독립 확인, 기준 커밋 3dd5dc7의 clone 복원·누락·손상 검증을 실행했다. 나머지 항목은 미실행 후속 작업이다. 각 단계의 변경·검증을 구분하고 문서 이전이나 formatter 성공을 앱 기능 구현 완료로 소급 해석하지 않는다.

1. **공통 설정 마이그레이션**
   - [x] 루트 Directory.Packages.props의 CPM·PackageVersion으로 버전을 중앙화하고 csproj의 Version/VersionOverride 중복을 제거했다. 프로젝트별 조건·테스트 패키지·비버전 메타데이터·기존 유효 버전을 보존했다.
   - [x] Directory.Build.props 공통 규약을 유지하고 global.json의 SDK 10.0.403·rollForward=disable·allowPrerelease=false를 적용했다.
   - [x] .NET 10 기본 .editorconfig와 Settings.XamlStyler의 XAML 서식 규약을 일치시켰다. formatter 검증과 앱/UI 실행은 구분하며 UI 자동조작은 직전 승인을 받는다.
   - [x] 기존 slnx와 명령·문서 참조를 유지했다. 전환할 sln이나 중복 sln은 없었다.
   - [x] 전체 영향 프로젝트의 restore/build와 실제 설정 적용을 검증했다. 문서 이전과 별도 설정 커밋으로 분리한다.
2. **공개 자료 기준선**
   - [x] 제3절의 출처·권한·메타데이터·픽셀/기하 보존을 검토하고 최초 수집한 공개 사본을 수집 도구와 오프라인 검증으로 확인했다. 기준 커밋은 3dd5dc7이다(원격 반영 여부는 실행 시점 보고에 따른다).
   - [x] 매니페스트·Git/LFS revision(3dd5dc7)·기대값을 고정하고 로컬 경로 clone에서 포인터 복원·해시·누락·손상 오류를 확인했다. 원 출처 연결 없이 로컬 LFS 캐시로 같은 자료를 복원했다. 원격 빈 LFS 캐시에서의 다운로드 검증은 이 로컬 증명에 포함되지 않으며 별도 실행 기록으로 남긴다.
3. **기하·픽셀·비동기 경합 고도화**
   - [ ] 단위 미확인 px 표시, 공간 정렬·모호함, 비등방 거리, 역변환·DPI, VOI LINEAR 경계, 단일/멀티프레임 지원 계약을 검증한다.
   - [ ] 빠른 시리즈·슬라이스 변경과 취소 후 낡은 결과 게시를 막는다. 버퍼 소유권·재사용·UI 복사 비용은 실제 경로에서 확인한다.
4. **UI와 기본 로딩 경로 적용**
   - [ ] 실제 도입 WPF-UI 버전·리소스·컨트롤 API를 재확인하고 제5절의 CustomControl/업무 View 책임을 구현 계획의 파일 단위에 표시한다. 표준 컨트롤을 일괄 래핑하지 않는다.
   - [ ] 고정 로컬 DICOM을 앱·회귀의 기본 입력으로 연결한다. 기존 실시간 TCIA 동작의 변경·제거 범위와 영향 호출자를 확인하고 매 실행 다운로드나 캐시 삭제를 기본 경로에 남기지 않는다.
   - [ ] 앱을 실행하여 네트워크 없이 자료 열기·슬라이스 탐색·W/L·측정·취소와 WPF-UI 테마·컨트롤 표시를 관찰한다. UI 자동조작은 직전 사용자 승인을 받고 UIA3 검증·실제 화면 증거·formatter 결과를 구분한다.
5. **확장 경계가 실제로 필요할 때**
   - [ ] 네이티브·이력·추론 구현은 제4절의 계약·지원 범위·검증 자료를 먼저 확정한 뒤 별도 작업으로 진행한다. 사용하지 않는 DLL·SQLite·HTTP·추론 의존성을 미리 추가하지 않는다.

최종 완료 근거에는 사용한 코드·자료 revision, 수행한 검증, 관찰한 결과와 미실행 항목을 구분한다. 기존 TCIA mock 테스트의 존재를 공개 자료 오프라인 회귀나 실제 UI 통과로 바꿔 쓰지 않는다.

## 8. 권리·공개·기여 표시

- 저장소와 계정명이 같다는 이유만으로 코드·자산 전체의 권리와 기여가 확인된 것으로 보지 않는다.
- 권한 확인 전에는 다른 코드·자산 복사, 과거 이력 이식, 기존 코드에 대한 새 라이선스 선언을 하지 않는다. 기존 소스를 참고한 개선과 외부 반입은 구분한다.
- 새 독립 코드의 라이선스는 공개 전에 정하고 외부 라이브러리·자료의 고지와 구분한다.
- 최종 설명에는 기존 프로젝트의 아이디어, 실제 반입한 코드가 있다면 그 범위·권리 근거, 새로 작성·검증한 내용을 구분한다.
- 회사 코드·고객 자료·비공개 업무 저장소/조직/계정 식별자·관련 경로·세션 원문·내부 성능 수치·개인 정책을 문서·새 파일·테스트·커밋에 포함하지 않는다.

## 9. 공개 근거 링크

### 조사 리비전

- [기준 리비전 트리](https://github.com/christian289/wpf-dicom-viewer/tree/f42cafc058db35707d46b56613a01f82ac2684ce)
- [공통 프로젝트 설정](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/Directory.Build.props)
- [영상 서비스](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Infrastructure/Dicom/FoDicomImageService.cs)
- [시리즈 탐색](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Application/Services/SeriesNavigationService.cs)
- [픽셀 간격](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Application/DTOs/PixelSpacing.cs)
- [영상 변환 상태](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Domain/ValueObjects/ImageTransform.cs)
- [TCIA 테스트](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Tests/Infrastructure/TciaServiceTests.cs)
- [기존 실시간 TCIA 구현](https://github.com/christian289/wpf-dicom-viewer/blob/f42cafc058db35707d46b56613a01f82ac2684ce/src/DicomViewer.Infrastructure/Tcia/TciaService.cs)

### 규격·UI·빌드

- [DICOM Image Plane 규격](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.7.6.2.html)
- [DICOM VOI LUT 규격](https://dicom.nema.org/medical/dicom/current/output/chtml/part03/sect_C.11.2.html)
- [WPF-UI 4.3.0 패키지](https://www.nuget.org/packages/WPF-UI/4.3.0)
- [WPF-UI 공식 리소스 설정](https://wpfui.lepo.co/documentation/getting-started.html)
- [ThemesDictionary API](https://wpfui.lepo.co/api/Wpf.Ui.Markup.ThemesDictionary.html)
- [ControlsDictionary API](https://wpfui.lepo.co/api/Wpf.Ui.Markup.ControlsDictionary.html)
- [NuGet CPM 공식 규약](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management)
- [MSBuild 공통 빌드 설정과 평가 순서](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)

공개 DICOM의 출처·버전·권한 링크와 필수 정책은 제3절에 포함했다. 존재하지 않는 별도 데이터 정책·Interop 계약·통합 설계 문서에 의존하지 않는다.
