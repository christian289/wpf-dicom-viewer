using System.Text.Json;

namespace DicomViewer.DatasetTool;

public static class DatasetDefinition
{
    public const string Id = "vhp-female-ankle-v1";
    public const int FileCount = 150;
    public const string SourceUrl = "https://doi.org/10.7910/DVN/3JDZCT";
    public const string MetadataUrl = "https://dataverse.harvard.edu/api/datasets/7575015/versions/1.0";
    public const string SourceVersion = "1.0";
    public const string MetadataPolicy = "metadata-allowlist-v1";
    public const string LicenseId = "CC0-1.0";
    public const string LicenseUrl = "https://creativecommons.org/publicdomain/zero/1.0/";
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public sealed record DatasetManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string DatasetId { get; init; } = DatasetDefinition.Id;
    public string SourceUrl { get; init; } = DatasetDefinition.SourceUrl;
    public string SourceVersion { get; init; } = DatasetDefinition.SourceVersion;
    public string LicenseId { get; init; } = DatasetDefinition.LicenseId;
    public string LicenseUrl { get; init; } = DatasetDefinition.LicenseUrl;
    public bool RedistributionAllowed { get; init; } = true;
    public string MetadataPolicy { get; init; } = DatasetDefinition.MetadataPolicy;
    public string Writer { get; init; } = "fo-dicom 5.2.5";
    public string Attribution { get; init; } = "Courtesy of the U.S. National Library of Medicine";
    public string Classification { get; init; } = "Non-weight-bearing cadaver ankle CT; not WBCT or CBCT; non-diagnostic reference data.";
    public required string CollectedAtUtc { get; init; }
    public required List<DatasetFileEntry> Files { get; init; }
}

public sealed record DatasetFileEntry
{
    public required string Path { get; init; }
    public required long SourceFileId { get; init; }
    public required string DownloadUrl { get; init; }
    public required string OriginalFilename { get; init; }
    public required string SourceMd5 { get; init; }
    public required string SourceSha256 { get; init; }
    public required long SourceSize { get; init; }
    public required string Sha256 { get; init; }
    public required long Size { get; init; }
    public required string LfsOid { get; init; }
    public required string SourcePixelDataSha256 { get; init; }
    public required string PixelDataSha256 { get; init; }
    public required string SourceGeometrySha256 { get; init; }
    public required string GeometrySha256 { get; init; }
    public required double SlicePositionMm { get; init; }
}

public sealed record ImageFingerprint(string PixelDataSha256, string GeometrySha256, double SlicePositionMm);

public sealed record VerificationResult(int FileCount, long Bytes);