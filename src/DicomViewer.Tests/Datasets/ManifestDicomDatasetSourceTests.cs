using DicomViewer.Application.DTOs;
using DicomViewer.Application.Services;
using DicomViewer.Infrastructure.Datasets;
using DicomViewer.Infrastructure.Dicom;

using FluentAssertions;

namespace DicomViewer.Tests.Datasets;

/// <summary>
/// 앱의 기본 입력(고정 로컬 데이터셋)이 읽기 전용으로 열리고, 포인터 미복원·누락·손상은 명시적 오류가 되는지 확인한다.
/// 원 출처 다운로드·합성 대체는 없다. 고정 자료가 없으면 건너뛰지 않고 실패한다.
/// Verifies that the application's default input (the fixed local dataset) opens read-only, and that unrestored LFS pointers, missing files and
/// corrupt payloads become explicit errors. There is no source download and no synthetic substitute. A missing fixture fails, it is never skipped.
/// </summary>
public sealed class ManifestDicomDatasetSourceTests : IDisposable
{
    private readonly List<string> _temporaryDirectories = [];

    [Fact]
    public async Task Fixed_dataset_opens_as_one_spatial_series_with_header_values()
    {
        var source = CreateSource(FixtureManifestPath());

        var dataset = await source.OpenAsync();

        dataset.DatasetId.Should().Be(DatasetDefinition.Id);
        dataset.Attribution.Should().Contain("National Library of Medicine");
        dataset.Classification.Should().Contain("not WBCT");

        var series = dataset.Series.Should().ContainSingle().Subject;
        series.Modality.Should().Be("CT");
        series.NumberOfInstances.Should().Be(DatasetDefinition.FileCount);
        series.Instances.Should().HaveCount(DatasetDefinition.FileCount);
        series.SeriesInstanceUid.Should().NotBeNullOrWhiteSpace();
        series.Instances.Should().OnlyContain(i =>
            i.SeriesInstanceUid == series.SeriesInstanceUid &&
            i.Rows == 512 &&
            i.Columns == 512 &&
            i.ImagePositionPatient != null &&
            i.ImageOrientationPatient != null &&
            File.Exists(i.FilePath));

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(series.Instances);

        navigation.GeometryStatus.Should().Be(SeriesGeometryStatus.Spatial);
        var positions = new List<double>();
        for (var index = 0; index < navigation.TotalCount; index++)
        {
            positions.Add(navigation.NavigateToIndex(index)!.ImagePositionPatient![2]);
        }

        positions.Should().BeInAscendingOrder();
        positions.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task A_cancelled_open_throws_cancellation_and_reads_nothing()
    {
        var source = CreateSource(FixtureManifestPath());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var open = () => source.OpenAsync(cancellation.Token);

        await open.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Missing_manifest_is_an_explicit_locate_error_naming_the_expected_path()
    {
        var directory = NewDirectory();
        var expected = Path.Combine(directory, "data", "dicom", "vhp-female-ankle-v1", "manifest.json");
        var source = CreateSource(expected);

        var open = () => source.OpenAsync();

        var error = (await open.Should().ThrowAsync<FileNotFoundException>()).Which;
        error.Message.Should().Contain(expected);
    }

    [Fact]
    public async Task Unrestored_git_lfs_pointer_is_an_explicit_error()
    {
        var manifest = NewDatasetDirectoryWithManifest();
        var first = Path.Combine(Path.GetDirectoryName(manifest)!, "sanitized", "0001.dcm");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        await File.WriteAllTextAsync(
            first,
            "version https://git-lfs.github.com/spec/v1\noid sha256:d504e62744fb2e090f78442df534157b93056bfedf20796fdf941b0bd9a47ce7\nsize 525286\n");

        var open = () => CreateSource(manifest).OpenAsync();

        var error = (await open.Should().ThrowAsync<InvalidDataException>()).Which;
        error.Message.Should().Contain("Git LFS pointer");
    }

    [Fact]
    public async Task Registered_file_that_is_missing_is_an_explicit_error()
    {
        var manifest = NewDatasetDirectoryWithManifest();

        var open = () => CreateSource(manifest).OpenAsync();

        var error = (await open.Should().ThrowAsync<InvalidDataException>()).Which;
        error.Message.Should().Contain("missing");
    }

    [Fact]
    public async Task Corrupt_payload_is_an_explicit_error_and_is_never_loaded()
    {
        var manifest = NewDatasetDirectoryWithManifest();
        var target = Path.Combine(Path.GetDirectoryName(manifest)!, "sanitized", "0001.dcm");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var payload = await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(FixtureManifestPath())!, "sanitized", "0001.dcm"));
        payload[^1] ^= 0xFF;
        await File.WriteAllBytesAsync(target, payload);

        var open = () => CreateSource(manifest).OpenAsync();

        var error = (await open.Should().ThrowAsync<InvalidDataException>()).Which;
        error.Message.Should().Contain("SHA-256");
    }

    public void Dispose()
    {
        foreach (var directory in _temporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static ManifestDicomDatasetSource CreateSource(string manifestPath) =>
        new(new FoDicomImageService(), manifestPath);

    private string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dicomviewer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);
        return directory;
    }

    /// <summary>고정 매니페스트만 복사한 빈 데이터셋 디렉터리 (영상 파일은 테스트가 필요한 것만 만든다)</summary>
    private string NewDatasetDirectoryWithManifest()
    {
        var directory = NewDirectory();
        var manifest = Path.Combine(directory, "manifest.json");
        File.Copy(FixtureManifestPath(), manifest);
        return manifest;
    }

    private static string FixtureManifestPath()
    {
        // 테스트 출력 위치에서 상위로 올라가며 고정 자료를 찾는다. 없으면 실패한다(건너뛰지 않음).
        // Walk up from the test output directory to find the fixed fixture. A missing fixture fails the test; it is never skipped.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "dicom", "vhp-female-ankle-v1", "manifest.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Fixed fixture not found; restore it with Git LFS.", "manifest.json");
    }
}