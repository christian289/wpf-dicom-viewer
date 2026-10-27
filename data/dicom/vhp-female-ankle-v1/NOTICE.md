# 고지: vhp-female-ankle-v1 공개 DICOM 기준 자료

이 디렉터리는 공개 Visible Human Project CT 데이터 중 여성 발목 시리즈 150개 파일을 저장소용으로 최소화한 사본입니다.

## 출처와 버전

- 원본 DOI: [10.7910/DVN/3JDZCT](https://doi.org/10.7910/DVN/3JDZCT) (Harvard Dataverse)
- 고정 버전: **1.0** (숫자 데이터셋 ID 7575015). 공개 메타데이터: [v1.0 API](https://dataverse.harvard.edu/api/datasets/7575015/versions/1.0)
- 선택 파일: `VHFCT1mm-Ankle (1).dcm` ~ `(150).dcm`, 150개, 원본 공개 합계 78,856,102 bytes
- 라이선스: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) (`CC0-1.0`)
- NLM 이용 조건: [NLM 데이터베이스 이용 조건](https://www.nlm.nih.gov/databases/download/terms_and_conditions.html)

## 출처 표시와 보증 범위

- 필수 출처 표시: **Courtesy of the U.S. National Library of Medicine**
- U.S. National Library of Medicine은 이 저장소, 이 사본, 이 도구 또는 이 자료를 이용한 결과를 **보증하거나 추천하지 않습니다.**
- 이 사본은 **고정된 버전 1.0**을 재현 가능하게 쓰기 위한 것입니다. 출처의 최신 또는 가장 정확한 데이터를 추적하거나 반영한다고 주장하지 않습니다.

## 자료의 한계

- 체중부하가 아닌 **사체의 일반 CT**입니다. **WBCT, 체중부하 촬영, CBCT가 아닙니다.**
- **진단용이 아닙니다.** 임상 판단, 치료 계획, 환자 관리에 사용하지 않습니다.
- 분할 정답이나 측정 정답이 검증된 자료가 아닙니다.

## 원본 취급 정책

- 원본 DICOM은 수집 도구가 **메모리에서만** 내려받아 사용하며, 디스크·저장소·로그에 쓰지 않습니다.
- 저장 사본은 빈 데이터셋에서 시작해 허용 목록 속성만 복사합니다. 원본 메타데이터의 식별 값은 복사하지 않습니다.
- 생성된 UID, 합성 환자 이름·ID, DERIVED/SECONDARY 표시를 사용합니다.

## 검증 범위 (정확히 이 범위만 주장)

- **저장 파일 150개**를 독립 pydicom 3.0.1로 파싱했습니다. **원본은 pydicom으로 파싱하지 않았습니다.** 원본은 수집 도구가 fo-dicom으로 메모리에서 읽어 허용 목록 저장 사본을 만들었고, 수집 중 원본 MD5·크기와 원본→저장 픽셀·기하 일치를 확인했습니다.
- 저장 PixelData SHA-256과 기하 JSON SHA-256은 매니페스트의 저장 값과 원본 값(`sourcePixelDataSha256`·`sourceGeometrySha256`) 둘 다와 일치했습니다. 파일 SHA-256·크기·슬라이스 위치도 매니페스트와 일치했습니다. 원본 해시는 저장 사본과의 일치로 확인했으며, 원본을 다시 내려받아 재계산하지 않았습니다.
- 허용 목록 밖 최상위 태그, 사설 태그, 시퀀스, 생성 빈 속성 위반은 없었습니다.
- 픽셀은 150장 축소 대조표와 9장 원본 해상도 표본에서 화면 글자가 보이지 않았습니다. 이는 **표본 시각 검사**이며 픽셀 내 식별 정보가 없다는 보증이 아닙니다.
- 이 검증은 **DICOM PS3.15 비식별화 적합성**, 임상 정확도, 다른 판독기와의 호환성을 증명하지 않습니다.
