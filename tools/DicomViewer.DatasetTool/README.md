# DicomViewer.DatasetTool

공개 DICOM 기준 데이터를 한 번 수집해 개인정보 최소화 사본으로 만들고, 그 사본을 매니페스트로 오프라인 검증하는 명령줄 도구입니다. 이 도구는 뷰어 앱의 일부가 아니라 저장소의 기준 데이터 도구입니다.

## 데이터셋

| 항목 | 값 |
| --- | --- |
| 데이터셋 ID | `vhp-female-ankle-v1` |
| 출처 | Harvard Dataverse, DOI `10.7910/DVN/3JDZCT`, 버전 `1.0` (숫자 데이터셋 ID `7575015`) |
| 선택 파일 | `VHFCT1mm-Ankle (1).dcm` ~ `(150).dcm`, 총 150개, 78,856,102 bytes |
| 라이선스 | CC0-1.0 (데이터셋 라이선스, 아래 참고) |
| 출처 표시 | Courtesy of the U.S. National Library of Medicine |
| 내용 | 체중부하가 아닌 사체 발목 CT. **WBCT, 체중부하 촬영, CBCT가 아니며 진단용이 아닙니다.** |

이 자료는 공개 Visible Human Project CT 시리즈입니다. NLM은 이 저장소나 결과를 보증하지 않습니다. 이 도구는 **고정된 버전 1.0**을 재현 가능하게 사용하기 위한 것이며, 해당 출처의 최신 또는 가장 정확한 데이터를 추적하거나 주장하지 않습니다. 버전 1.0이 최신인지 여부도 이 도구는 판단하지 않습니다.

## 명령

저장소 루트에서 실행합니다. 먼저 빌드합니다.

```powershell
# 클론별 1회: 전역 LFS 설정이 없는 클론에서 로컬 필터만 등록합니다.
git lfs install --local --skip-repo

# 처음 빌드: NuGet 패키지 복원을 위해 네트워크가 필요할 수 있습니다.
dotnet build tools/DicomViewer.DatasetTool/DicomViewer.DatasetTool.csproj -c Release

# 수집(일회성 기준 단계, 원 출처 접속): 대상 경로는 존재하지 않거나 비어 있어야 합니다.
dotnet run --project tools/DicomViewer.DatasetTool -c Release --no-build --no-restore -- collect data/dicom/vhp-female-ankle-v1

# 검증: 복원 없이 이미 빌드된 결과로 실행하며 네트워크에 접속하지 않습니다.
dotnet run --project tools/DicomViewer.DatasetTool -c Release --no-build --no-restore -- verify data/dicom/vhp-female-ankle-v1/manifest.json
```

종료 코드: `0` 성공, `1` 검증 또는 수집 실패, `2` 사용법 오류, `130` 취소(Ctrl+C).

`collect`는 대상 경로가 없거나 비어 있을 때만 시작합니다. 경로가 없으면 생성하고, 이미 파일이 있으면 거부합니다. 기존 기준 데이터를 덮어쓰거나 다시 내려받지 않습니다. 수집 전에 대상 디렉터리에 안내 파일이나 다른 파일을 두지 않습니다.

`collect`는 `manifest.json`과 `sanitized/0001.dcm` ~ `sanitized/0150.dcm`을 만듭니다.

## 수집 시 원본 처리

- 원본 DICOM은 **메모리에만** 내려받습니다. 디스크, 저장소, 로그에 쓰지 않습니다.
- 각 원본은 고정된 공개 크기와 MD5를 확인한 뒤 사용합니다.
- 저장 사본은 빈 데이터셋에서 시작하고 아래 **허용 목록 속성만** 복사합니다.
  Rows, Columns, BitsAllocated, BitsStored, HighBit, PixelRepresentation, SamplesPerPixel, PhotometricInterpretation, PixelSpacing, ImageOrientationPatient, ImagePositionPatient, SliceThickness, SpacingBetweenSlices, RescaleSlope, RescaleIntercept, RescaleType, WindowCenter, WindowWidth, VOILUTFunction, PixelPaddingValue, PixelPaddingRangeLimit, PixelData.
- 허용 목록에 있어도 원본에 없는 속성은 복사하지 않습니다. 다만 DICOM CT 구조를 완성하기 위해 생성기가 필요한 속성을 빈 값으로 추가할 수 있습니다. 예를 들어 SliceThickness가 원본에 없어도 빈 값으로 들어갈 수 있습니다. 원본 값을 임의로 만들어 넣지는 않습니다.
- UID는 데이터셋 키에서 결정적으로 생성합니다. 저장 파일은 DERIVED/SECONDARY로 표시합니다.
- 원본과 저장 사본의 픽셀 바이트와 기하 정보가 일치해야 합니다. 일치하지 않으면 수집을 실패시킵니다.
- 저장 파일의 SHA-256을 기록하며, 이는 해당 파일의 Git LFS 객체 ID(`sha256:<hash>`)와 같습니다.

허용 목록은 메타데이터 축소일 뿐입니다. **PS3.15 전체 비식별화가 아니며**, 픽셀에 화면 글자나 식별 정보가 없다는 것을 확인하지 않습니다.

## 검증

`verify`는 `manifest.json`과 목록에 있는 파일을 읽고 다음을 확인합니다.

- 스키마 v1의 필수 필드가 모두 있고, 데이터셋 ID·출처·버전·라이선스·메타데이터 정책·분류 값이 고정 계약과 같은지.
- 각 항목의 상대 경로가 매니페스트 디렉터리 안에 있는지. 절대 경로, 상위 경로, 과도하게 깊은 경로는 허용하지 않습니다.
- 파일이 존재하고 크기 제한 안에 있으며 DICM 프리앰블을 갖고, 기록된 크기와 SHA-256과 일치하는지.
- 저장된 픽셀 지문과 기하 지문이 매니페스트와 일치하는지.

실패 메시지는 짧은 문장으로만 출력하며 원본 메타데이터 값을 포함하지 않습니다.

## 오류 상황

| 상황 | 동작 |
| --- | --- |
| Git LFS 포인터가 복원되지 않음 (파일 첫 줄이 `version https://git-lfs.github.com/spec/v1`) | 검증 실패. 빈 데이터로 취급하지 않습니다. |
| 매니페스트에 있는 파일이 없음 | 검증 실패. |
| 해시, 크기, 지문 불일치 | 검증 실패. |
| 매니페스트 경로가 데이터셋 디렉터리 밖을 가리킴 | 검증 실패. |
| `collect` 대상 경로에 파일이 이미 있음 | 수집을 시작하지 않고 거부합니다. |
| 공개 출처를 사용할 수 없거나 메타데이터가 달라짐 | 수집 실패. 다른 출처로 대체하지 않습니다. |

**자동 재다운로드는 없습니다.** `verify`는 네트워크에 접속하지 않습니다. 원본 출처에 접속하는 명령은 `collect` 하나뿐이며, 사용자가 명시적으로 실행할 때만 동작합니다.

## Git LFS 복원과 수집의 차이

- `git lfs checkout`은 **로컬 LFS 캐시에 이미 있는 객체**를 작업 트리에 복원합니다. 이 명령 자체는 원격에서 객체를 가져오지 않습니다.
- `git lfs pull`은 현재 체크아웃에 필요한 객체를 **원격 저장소에서 가져올 수 있습니다.** 원격 LFS 저장소 접근이 필요할 수 있습니다.
- `collect`는 공개 출처에서 내려받아 사본을 다시 만듭니다. 일반적인 사용에서 반복할 필요가 없는 일회성 기준 단계입니다.

Git LFS는 저장 방식일 뿐입니다. 개인정보 삭제, 검토, 라이선스 확인을 대신하지 않습니다.

## 라이선스 범위

- `tools/DicomViewer.DatasetTool/`의 **새** 코드는 MIT 라이선스입니다(이 디렉터리의 `LICENSE` 참고).
- 이 도구는 저장소 루트나 기존 코드의 라이선스를 바꾸지 않습니다.
- 데이터셋(`data/dicom/vhp-female-ankle-v1/`)은 CC0-1.0이며 위의 NLM 출처 표시를 유지합니다.

## 한계와 주장하지 않는 것

- PS3.15 비식별화 적합성, 임상 또는 진단 검증이 아닙니다.
- 같은 라이브러리로 왕복 저장해 통과해도 다른 DICOM 판독기와의 호환성을 증명하지 않습니다.
- 픽셀 안의 화면 글자나 식별 정보는 이 도구가 검사하지 않습니다.
- 이 데이터는 체중부하가 아닌 사체 CT이며 WBCT 또는 CBCT로 표기하지 않습니다.

## 검증 증거 (관찰 결과)

아래는 실제 실행 결과입니다. 커밋·클론 검증은 아직 수행하지 않았습니다.

- `collect`(Release, `--no-build --no-restore`): 150/150 수집, 종료 코드 0. 저장 사본 합계 78,793,078 bytes (원본 공개 합계 78,856,102 bytes와 다름: 저장 방식 차이이며 원본 크기 검사는 수집 중 메모리에서 수행).
- `verify manifest.json`(오프라인): 150개 파일, 78,793,078 bytes, 종료 코드 0.
- 독립 pydicom 3.0.1 검사(저장 파일 150개만 파싱, 원본은 파싱하지 않음): 150/150 통과, 종료 코드 0, 실패 0.
  - 전송 구문 Explicit VR Little Endian 150, SOP Class CT Image Storage, 512×512, 16비트, HighBit 15, MONOCHROME2, NumberOfFrames 없음(단일 프레임 가정).
  - 저장 PixelData SHA-256은 매니페스트의 `pixelDataSha256`(저장)과 `sourcePixelDataSha256`(원본) 둘 다와 일치합니다. 기하 JSON SHA-256은 `geometrySha256`과 `sourceGeometrySha256` 둘 다와 일치합니다. 원본 값은 원본을 다시 읽어 계산한 것이 아니라 저장 사본과의 일치로 확인한 것입니다.
  - `sha256`·`lfsOid`·크기 일치, `slicePositionMm` 최대 차이 0.0 mm.
  - 허용 목록 밖 최상위 태그 0, 사설 태그 0, 시퀀스 0, 생성 빈 속성 값 위반 0, VR 불일치 0. 환자 값은 출력하지 않음.
  - 음성 대조(실행함, 모두 종료 코드 1과 실패 보고): 저장 PixelData 해시 변경, 원본 PixelData 해시 변경, 원본 기하 해시 변경, 허용 목록 밖 태그 추가(매니페스트 해시를 갱신해 태그 검사만 실패하도록 함). 사설 태그·시퀀스·VR 불일치·생성 빈 속성 값 대조는 실행하지 않았습니다.
- 픽셀 시각 검사: 150장 전체를 축소 대조표로, 9장을 원본 해상도(512px)로 확인했고 화면 글자는 보이지 않았습니다. 이는 표본 시각 검사이며 픽셀 내 식별 정보가 없다는 보증이 아닙니다.
- 깨끗한 clone 증명(기준 커밋 `3dd5dc7`, 로컬 경로에서 `.git/` 임시 clone, 원격 push 이전):
  - `GIT_LFS_SKIP_SMUDGE=1`로 clone하면 150개 모두 LFS 포인터이며, verify는 `Dataset file 0001 is an unresolved Git LFS pointer` 메시지와 종료 코드 1을 냈습니다.
  - 복원: 캐시 객체 150개를 clone의 `.git/lfs/objects`로 복사한 뒤 `git lfs checkout`을 실행했습니다(150/150, 로컬 캐시만 사용). 이어서 verify를 실행해 종료 코드 0을 얻었습니다. 프록시 환경변수(`HTTP_PROXY`·`HTTPS_PROXY`·`ALL_PROXY`=`http://127.0.0.1:9`)는 verify 실행에만 설정했습니다. 이는 프록시 환경변수 차단일 뿐 OS 방화벽 격리가 아닙니다.
  - 복사 전 `git lfs checkout`은 객체가 없을 때 `Skipped checkout ... content not local. Use fetch to download.`를 출력했고 포인터를 남겼습니다. 이 명령은 다운로드하지 않았습니다.
  - 누락, origin이 도달 가능한 경우: 객체 1개를 `mv`로 치우고 파일을 `rm`한 뒤 `git checkout -- <path>`를 실행했고 종료 코드 0, verify 종료 코드 0이었습니다. 이 실행의 출력에는 다운로드 진행 줄이 없었으므로, 파일이 origin에서 내려받아졌는지는 출력으로 확인하지 않았습니다. 같은 명령이 origin이 도달 불가할 때 다운로드를 시도했다는 점[아래 항목]을 근거로 한 추정입니다.
  - 누락, origin이 도달 불가한 경우: clone의 origin을 `Z:/does-not-exist/repo`로 바꾼 뒤 `git checkout -- <path>`를 실행했습니다. 출력은 `Downloading ...` 후 `Smudge error`/`batch request` 오류였고, 종료 코드 128이었으며 파일은 생성되지 않았습니다. verify는 `Dataset file 0042 is missing` 메시지와 종료 코드 1이었습니다. origin과 객체를 복구한 뒤 `git checkout -- <path>`와 verify는 종료 코드 0이었습니다.
  - 손상: 캐시 객체 1개를 같은 길이의 0 바이트로 바꾼 뒤 파일을 `rm`하고 `git checkout -- <path>`를 실행했습니다. 종료 코드 0, git-lfs 오류 출력 없음. verify는 `SHA-256 differs` 메시지와 종료 코드 1을 냈습니다. 객체를 복구한 뒤 같은 checkout과 verify는 종료 코드 0이었습니다. git-lfs가 로컬 객체 해시를 검사했는지는 이 실행에서 확인하지 않았습니다.
  - 정상 상태 복구 후 프록시 차단 상태에서 verify 종료 코드 0, `git lfs fsck --objects --pointers` 통과, 작업 트리 변경 0입니다.
- 아직 실행하지 않음: 원격 push와 push 후 원격 clone 검증, 사설 태그·시퀀스·VR 불일치·생성 빈 속성 값 음성 대조, 증거 문서 커밋의 push.

이 증거는 PS3.15 적합성, 임상 정확도, 다른 판독기와의 호환성을 증명하지 않습니다.
