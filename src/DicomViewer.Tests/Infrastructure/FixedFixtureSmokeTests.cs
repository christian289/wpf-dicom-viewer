using DicomViewer.Application.DTOs;
using DicomViewer.Domain.ValueObjects;
using DicomViewer.Infrastructure.Dicom;

namespace DicomViewer.Tests.Infrastructure;

/// <summary>
/// 고정 공개 기준선(data/dicom/vhp-female-ankle-v1)의 한 파일을 읽기 전용으로 확인한다.
/// 기대값은 설치된 pydicom 3.0.1 + numpy로 저장 사본에서 독립 계산했다(원본 재다운로드 없음).
/// Reads one file of the fixed public baseline read-only. Expected values were computed independently
/// from the stored copy with installed pydicom 3.0.1 and numpy (no original re-download).
/// </summary>
public class FixedFixtureSmokeTests
{
    private static readonly FoDicomImageService Service = new();

    [Fact]
    public async Task First_fixture_slice_matches_the_recorded_header_profile()
    {
        var path = FixturePath("0001.dcm");

        var loaded = await Service.LoadImageAsync(path);
        var metadata = await Service.ReadMetadataAsync(path);

        Assert.Equal(512, loaded.Pixels.Width);
        Assert.Equal(512, loaded.Pixels.Height);
        Assert.Equal(16, loaded.Pixels.BitsAllocated);
        Assert.Equal(16, loaded.Pixels.BitsStored);
        Assert.True(loaded.Pixels.IsSigned);
        Assert.Equal(1.0, loaded.Pixels.RescaleSlope);
        Assert.Equal(-1024.0, loaded.Pixels.RescaleIntercept);
        Assert.Equal(512 * 512 * 2, loaded.Pixels.RawPixels.Length);
        Assert.Equal(new PixelSpacing(1.0, 1.0), loaded.Spacing);
        Assert.Equal(WindowLevel.Default, loaded.DefaultWindowLevel);

        Assert.Equal(512, metadata.Rows);
        Assert.Equal(512, metadata.Columns);
        Assert.Equal(new[] { 0.0, 0.0, 0.0 }, metadata.ImagePositionPatient);
        Assert.Equal(new[] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0 }, metadata.ImageOrientationPatient);
    }

    [Fact]
    public async Task Windowed_samples_match_independently_computed_values()
    {
        var loaded = await Service.LoadImageAsync(FixturePath("0001.dcm"));
        var destination = new byte[loaded.Pixels.PixelCount];

        Service.ApplyWindowLevel(loaded.Pixels, loaded.DefaultWindowLevel, destination);

        // 저장 값(int16)과 pydicom 기대 출력 / stored int16 values and pydicom-expected outputs
        // index 0: 저장 0 (HU -1024) → 0 ; index 131328: 저장 53 → 0
        // index 204085: 저장 1080 (HU 56) → 138 ; index 204086: 저장 1139 (HU 115, 최댓값) → 175
        Assert.Equal(0, destination[0]);
        Assert.Equal(0, destination[131328]);
        Assert.Equal(138, destination[204085]);
        Assert.Equal(175, destination[204086]);

        Assert.Equal(0, StoredValue(loaded.Pixels, 0));
        Assert.Equal(53, StoredValue(loaded.Pixels, 131328));
        Assert.Equal(1080, StoredValue(loaded.Pixels, 204085));
        Assert.Equal(1139, StoredValue(loaded.Pixels, 204086));
    }

    private static int StoredValue(PixelData pixels, int index) =>
        pixels.RawPixels[index * 2] | (pixels.RawPixels[index * 2 + 1] << 8);

    private static string FixturePath(string fileName)
    {
        // 테스트 실행 위치에서 상위로 올라가며 고정 자료 디렉터리를 찾는다. 없으면 실패한다(건너뛰지 않음).
        // Walk up from the test output directory to find the fixed fixture directory. Missing fixture fails the test; it is never skipped.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "dicom", "vhp-female-ankle-v1", "sanitized", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Fixed fixture not found; restore it with Git LFS.", fileName);
    }
}