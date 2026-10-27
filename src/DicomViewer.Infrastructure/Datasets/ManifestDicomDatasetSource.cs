using System.Text.Json;

namespace DicomViewer.Infrastructure.Datasets;

/// <summary>
/// 매니페스트로 고정한 로컬 DICOM 데이터셋을 연다. 같은 <see cref="DatasetVerifier"/>로 무결성(LFS 포인터·누락·손상)을 먼저 검증하고
/// 검증된 파일의 헤더만 한 번 읽는다. 원 출처에 접속하거나 합성 데이터로 대체하지 않는다.
/// Opens the manifest-pinned local DICOM dataset. It first runs the same <see cref="DatasetVerifier"/> integrity check (LFS pointer, missing,
/// corrupt) and then reads each verified file's header once. It never contacts the original source and never substitutes synthetic data.
/// </summary>
public sealed class ManifestDicomDatasetSource : IDicomDatasetSource
{
    private readonly IDicomImageService _imageService;
    private readonly string _manifestPath;
    private readonly int _expectedFileCount;

    public ManifestDicomDatasetSource(
        IDicomImageService imageService,
        string manifestPath,
        int expectedFileCount = DatasetDefinition.FileCount)
    {
        ArgumentNullException.ThrowIfNull(imageService);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedFileCount, 1);

        _imageService = imageService;
        _manifestPath = Path.GetFullPath(manifestPath);
        _expectedFileCount = expectedFileCount;
    }

    public async Task<DicomDatasetDto> OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_manifestPath))
        {
            // Explicit locate error: the packaged fixture is not next to the application. No source download, no empty success.
            throw new FileNotFoundException(
                $"고정 DICOM 데이터셋 매니페스트를 찾을 수 없습니다: {_manifestPath} / " +
                $"The fixed DICOM dataset manifest was not found at: {_manifestPath}",
                _manifestPath);
        }

        // Hashing and full DICOM verification are blocking work: run them off the caller's (UI) thread.
        await Task.Run(
            () => DatasetVerifier.Verify(_manifestPath, _expectedFileCount, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // The verifier proved the strict schema and safe relative paths; this read only needs the registered paths and attribution.
        var manifestBytes = await File.ReadAllBytesAsync(_manifestPath, cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<DatasetManifest>(manifestBytes, DatasetDefinition.JsonOptions)
                       ?? throw new InvalidDataException("Dataset manifest is not valid for the supported schema.");
        var root = Path.GetDirectoryName(_manifestPath)!;

        var groups = new Dictionary<string, List<DicomInstanceDto>>(StringComparer.Ordinal);
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var instance = await _imageService.ReadMetadataAsync(path, cancellationToken).ConfigureAwait(false);

            var key = instance.SeriesInstanceUid ?? string.Empty;
            if (!groups.TryGetValue(key, out var instances))
            {
                instances = [];
                groups.Add(key, instances);
            }

            instances.Add(instance);
        }

        // SeriesNumber and SeriesDescription are not part of the sanitized allowlist, so no values are invented for them:
        // the number is only the series ordinal within this dataset and the description stays unknown.
        var series = new List<DicomSeriesDto>(groups.Count);
        foreach (var (seriesUid, instances) in groups)
        {
            series.Add(new DicomSeriesDto(
                SeriesInstanceUid: seriesUid,
                SeriesDescription: null,
                SeriesNumber: series.Count + 1,
                Modality: DatasetDefinition.Modality,
                NumberOfInstances: instances.Count,
                Instances: instances));
        }

        return new DicomDatasetDto(manifest.DatasetId, manifest.Attribution, manifest.Classification, series);
    }
}