using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using FellowOakDicom;

namespace DicomViewer.DatasetTool;

/// <summary>
/// Offline integrity and fixed-policy verification of a stored sanitized dataset.
/// It proves payload size/SHA-256/LFS OID agreement, restored (non-pointer) objects, registered-only DICOM content and
/// stored pixel/geometry fingerprints. It does not certify DICOM semantics, de-identification or redistribution rights.
/// Failures are <see cref="InvalidDataException"/> with fixed messages; parser/IO messages and paths are never forwarded.
/// </summary>
public static class DatasetVerifier
{
    private const int MaxManifestBytes = 8 * 1024 * 1024;
    private const long MaxFileBytes = 64L * 1024 * 1024;
    private const int MaxPathLength = 256;
    private const int MaxPathDepth = 8;
    private const int PointerProbeLength = 64;
    private const int PreambleLength = 132;

    // Schema v1 root fields. Every one must be present in the JSON: record defaults never stand in for absent content.
    private static readonly string[] RootFields =
    [
        "schemaVersion", "datasetId", "sourceUrl", "sourceVersion", "licenseId", "licenseUrl", "redistributionAllowed",
        "metadataPolicy", "writer", "attribution", "classification", "collectedAtUtc", "files"
    ];

    private static readonly HashSet<string> RootFieldSet = new(RootFields, StringComparer.Ordinal);

    // Allowlist per path segment: no dots, separators, drive/URI colons, spaces, '%' or '~', so rooted, UNC, URI, dot-segment,
    // backslash, trailing-dot/space, alternate-stream and short-name aliases cannot be expressed.
    private static readonly Regex DirectorySegment = new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex FileSegment = new(@"\A([0-9]{4})\.dcm\z", RegexOptions.CultureInvariant);
    private static readonly Regex SourceFileName = new(@"\AVHFCT1mm-Ankle \(([0-9]{1,9})\)\.dcm\z", RegexOptions.CultureInvariant);

    private static readonly string DownloadUrlPrefix =
        $"https://{new Uri(DatasetDefinition.MetadataUrl).Host}/api/access/datafile/";

    private static readonly byte[][] LfsPointerPrefixes =
    [
        "version https://git-lfs.github.com/spec/v1"u8.ToArray(),
        "version https://hawser.github.com/spec/v1"u8.ToArray()
    ];

    private static readonly JsonSerializerOptions ManifestJson = new(DatasetDefinition.JsonOptions)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
        WriteIndented = false
    };

    public static VerificationResult Verify(string manifestPath, int expectedCount = DatasetDefinition.FileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentOutOfRangeException.ThrowIfLessThan(expectedCount, 1);

        var manifestFullPath = Path.GetFullPath(manifestPath);
        var root = Path.GetDirectoryName(manifestFullPath);
        if (string.IsNullOrEmpty(root))
        {
            throw Fail("Dataset manifest must be located inside a dataset directory.");
        }

        // The scan below only sees children; the root and the manifest itself must not redirect outside the dataset either.
        RejectLink(new DirectoryInfo(root));
        RejectLink(new FileInfo(manifestFullPath));
        var manifest = ReadManifest(manifestFullPath);
        RequireSupportedHeader(manifest, expectedCount);
        var files = RegisterFiles(manifest, expectedCount, root);
        RejectUnregisteredFiles(root, files);

        long bytes = 0;
        foreach (var file in files)
        {
            VerifyFile(file);
            bytes += file.Entry.Size;
        }

        return new VerificationResult(files.Length, bytes);
    }

    private static InvalidDataException Fail(string message) => new(message);

    private static void RejectLink(FileSystemInfo entry)
    {
        if (entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw Fail("Dataset directory contains a symbolic link or reparse point, which is never followed.");
        }
    }

    private static DatasetManifest ReadManifest(string manifestFullPath)
    {
        byte[] bytes;
        try
        {
            var info = new FileInfo(manifestFullPath);
            if (!info.Exists)
            {
                throw Fail("Dataset manifest is missing.");
            }

            if (info.Length > MaxManifestBytes)
            {
                throw Fail("Dataset manifest is larger than the supported maximum.");
            }

            bytes = File.ReadAllBytes(manifestFullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Fail("Dataset manifest could not be read.");
        }

        if (IsLfsPointer(bytes))
        {
            throw Fail("Dataset manifest is an unresolved Git LFS pointer; restore the object before verifying.");
        }

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            RequireRootFields(document.RootElement);
            return document.RootElement.Deserialize<DatasetManifest>(ManifestJson)
                   ?? throw Fail("Dataset manifest is not valid for the supported schema.");
        }
        catch (JsonException)
        {
            throw Fail("Dataset manifest is not valid for the supported schema.");
        }
    }

    private static void RequireRootFields(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Fail("Dataset manifest root must be a JSON object.");
        }

        var seen = new HashSet<string>(RootFields.Length, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!RootFieldSet.Contains(property.Name))
            {
                throw Fail("Dataset manifest contains an unsupported root field.");
            }

            if (!seen.Add(property.Name))
            {
                throw Fail("Dataset manifest repeats a root field.");
            }

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                throw Fail($"Dataset manifest root field '{property.Name}' must not be null.");
            }
        }

        foreach (var field in RootFields)
        {
            if (!seen.Contains(field))
            {
                throw Fail($"Dataset manifest is missing required root field '{field}'.");
            }
        }
    }

    private static void RequireSupportedHeader(DatasetManifest manifest, int expectedCount)
    {
        // Defaults are read only to obtain the fixed contract text; the JSON itself was already required to carry every field.
        var contract = new DatasetManifest { CollectedAtUtc = string.Empty, Files = [] };

        if (manifest.SchemaVersion != contract.SchemaVersion)
        {
            throw Fail("Dataset manifest field 'schemaVersion' is not supported.");
        }

        RequireEqual(manifest.DatasetId, DatasetDefinition.Id, "datasetId");
        RequireEqual(manifest.SourceUrl, DatasetDefinition.SourceUrl, "sourceUrl");
        RequireEqual(manifest.SourceVersion, DatasetDefinition.SourceVersion, "sourceVersion");
        RequireEqual(manifest.LicenseId, DatasetDefinition.LicenseId, "licenseId");
        RequireEqual(manifest.LicenseUrl, DatasetDefinition.LicenseUrl, "licenseUrl");
        if (!manifest.RedistributionAllowed)
        {
            throw Fail("Dataset manifest field 'redistributionAllowed' is not supported.");
        }

        RequireEqual(manifest.MetadataPolicy, DatasetDefinition.MetadataPolicy, "metadataPolicy");
        RequireEqual(manifest.Attribution, contract.Attribution, "attribution");
        RequireEqual(manifest.Classification, contract.Classification, "classification");
        if (string.IsNullOrWhiteSpace(manifest.Writer) || manifest.Writer.Length > 64)
        {
            throw Fail("Dataset manifest field 'writer' is not supported.");
        }

        if (!IsUtcTimestamp(manifest.CollectedAtUtc))
        {
            throw Fail("Dataset manifest field 'collectedAtUtc' is not a UTC timestamp.");
        }

        if (manifest.Files is null || manifest.Files.Count != expectedCount)
        {
            throw Fail("Dataset manifest file count does not match the expected file count.");
        }
    }

    private static void RequireEqual(string? actual, string expected, string field)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw Fail($"Dataset manifest field '{field}' is not supported.");
        }
    }

    // The collector writes DateTimeOffset "O" text ("+00:00"); an explicit "Z" is the equivalent ISO form. Zone-less or non-UTC text is refused.
    private static bool IsUtcTimestamp(string? value) =>
        value is { Length: > 0 and <= 64 } && (value.EndsWith('Z') || value.EndsWith("+00:00", StringComparison.Ordinal))
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
        && parsed.Offset == TimeSpan.Zero;

    private static RegisteredFile[] RegisterFiles(DatasetManifest manifest, int expectedCount, string root)
    {
        var ordered = new RegisteredFile[expectedCount];
        var sourceNumbers = new int[expectedCount];
        var paths = new HashSet<string>(expectedCount, StringComparer.OrdinalIgnoreCase);
        var sourceIds = new HashSet<long>(expectedCount);

        for (var index = 0; index < manifest.Files.Count; index++)
        {
            var entry = manifest.Files[index] ?? throw Fail($"Dataset manifest entry {index + 1} is empty.");
            if (!TryParsePath(entry.Path, expectedCount, out var ordinal, out var segments))
            {
                throw Fail($"Dataset manifest entry {index + 1} has an unsupported file path.");
            }

            if (!paths.Add(string.Join('/', segments)))
            {
                throw Fail($"Dataset manifest entry {index + 1} duplicates another file path.");
            }

            if (ordered[ordinal - 1] is not null)
            {
                throw Fail($"Dataset manifest entry {index + 1} duplicates another file ordinal.");
            }

            var sourceNumber = RequireEntryMetadata(entry, index + 1);
            if (!sourceIds.Add(entry.SourceFileId))
            {
                throw Fail($"Dataset manifest entry {index + 1} duplicates another source file id.");
            }

            ordered[ordinal - 1] = new RegisteredFile(entry, ordinal, Path.Combine(root, string.Join(Path.DirectorySeparatorChar, segments)));
            sourceNumbers[ordinal - 1] = sourceNumber;
        }

        for (var index = 1; index < sourceNumbers.Length; index++)
        {
            if (sourceNumbers[index] <= sourceNumbers[index - 1])
            {
                throw Fail("Dataset manifest source file order does not match the stored file ordinals.");
            }
        }

        return ordered;
    }

    private static bool TryParsePath(string? path, int expectedCount, out int ordinal, out string[] segments)
    {
        ordinal = 0;
        segments = [];
        if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength)
        {
            return false;
        }

        var parts = path.Split('/');
        if (parts.Length > MaxPathDepth)
        {
            return false;
        }

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (!DirectorySegment.IsMatch(parts[index]))
            {
                return false;
            }
        }

        var match = FileSegment.Match(parts[^1]);
        if (!match.Success)
        {
            return false;
        }

        var parsed = int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        if (parsed < 1 || parsed > expectedCount)
        {
            return false;
        }

        ordinal = parsed;
        segments = parts;
        return true;
    }

    private static int RequireEntryMetadata(DatasetFileEntry entry, int position)
    {
        if (entry.SourceFileId <= 0
            || !string.Equals(entry.DownloadUrl, DownloadUrlPrefix + entry.SourceFileId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw Fail($"Dataset manifest entry {position} has an unsupported public source reference.");
        }

        var name = SourceFileName.Match(entry.OriginalFilename ?? string.Empty);
        if (!name.Success)
        {
            throw Fail($"Dataset manifest entry {position} has an unsupported original file name.");
        }

        if (!IsLowerHex(entry.SourceMd5, 32) || !IsLowerHex(entry.SourceSha256, 64) || entry.SourceSize <= 0)
        {
            throw Fail($"Dataset manifest entry {position} has malformed source checksum or size metadata.");
        }

        if (!IsLowerHex(entry.Sha256, 64) || entry.Size <= 0 || entry.Size > MaxFileBytes)
        {
            throw Fail($"Dataset manifest entry {position} has malformed stored checksum or size metadata.");
        }

        if (!string.Equals(entry.LfsOid, "sha256:" + entry.Sha256, StringComparison.Ordinal))
        {
            throw Fail($"Dataset manifest entry {position} LFS object id does not match its SHA-256.");
        }

        if (!IsLowerHex(entry.SourcePixelDataSha256, 64) || !IsLowerHex(entry.PixelDataSha256, 64)
            || !IsLowerHex(entry.SourceGeometrySha256, 64) || !IsLowerHex(entry.GeometrySha256, 64)
            || !double.IsFinite(entry.SlicePositionMm))
        {
            throw Fail($"Dataset manifest entry {position} has malformed pixel or geometry fingerprint metadata.");
        }

        return int.Parse(name.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static bool IsLowerHex(string? value, int length)
    {
        if (value is null || value.Length != length)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLfsPointer(ReadOnlySpan<byte> bytes)
    {
        foreach (var prefix in LfsPointerPrefixes)
        {
            if (bytes.StartsWith(prefix.AsSpan()))
            {
                return true;
            }
        }

        return false;
    }

    private static void RejectUnregisteredFiles(string root, RegisteredFile[] files)
    {
        // Ordinal comparison on purpose: a differently-cased on-disk alias of a registered name is not the registered file.
        var registered = new HashSet<string>(files.Length, StringComparer.Ordinal);
        foreach (var file in files)
        {
            registered.Add(file.FullPath);
        }

        // Hidden/system entries are included; links are refused instead of followed, so nothing outside the directory is reached.
        var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false, RecurseSubdirectories = false };
        var pending = new Stack<string>();
        pending.Push(root);
        try
        {
            while (pending.TryPop(out var directory))
            {
                foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options))
                {
                    RejectLink(entry);

                    if (entry is DirectoryInfo)
                    {
                        pending.Push(entry.FullName);
                    }
                    else if (!registered.Contains(entry.FullName) && (HasDicomExtension(entry.Name) || HasDicomPreamble(entry.FullName)))
                    {
                        throw Fail("Dataset directory contains a DICOM file that is not registered in the manifest.");
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Fail("Dataset directory could not be inspected.");
        }
    }

    private static bool HasDicomExtension(string name)
    {
        var extension = Path.GetExtension(name);
        return extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDicomPreamble(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1);
        Span<byte> buffer = stackalloc byte[PreambleLength];
        return stream.ReadAtLeast(buffer, PreambleLength, throwOnEndOfStream: false) == PreambleLength
               && buffer[128..].SequenceEqual("DICM"u8);
    }

    private static void VerifyFile(RegisteredFile file)
    {
        var label = $"file {file.Ordinal:D4}";
        var entry = file.Entry;
        if (!new FileInfo(file.FullPath).Exists)
        {
            throw Fail($"Dataset {label} is missing; restore the registered object before verifying.");
        }

        try
        {
            using var stream = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            if (IsLfsPointer(ReadProbe(stream)))
            {
                throw Fail($"Dataset {label} is an unresolved Git LFS pointer; restore the object before verifying.");
            }

            if (stream.Length != entry.Size)
            {
                throw Fail($"Dataset {label} size differs from the manifest.");
            }

            var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
            {
                throw Fail($"Dataset {label} SHA-256 differs from the manifest and LFS object id.");
            }

            stream.Position = 0;
            VerifyDicom(stream, file, label);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Fail($"Dataset {label} could not be read.");
        }
    }

    private static byte[] ReadProbe(FileStream stream)
    {
        var probe = new byte[PointerProbeLength];
        var read = stream.ReadAtLeast(probe, PointerProbeLength, throwOnEndOfStream: false);
        stream.Position = 0;
        return read == probe.Length ? probe : probe[..read];
    }

    private static void VerifyDicom(Stream stream, RegisteredFile file, string label)
    {
        DicomFile dicom;
        try
        {
            dicom = DicomFile.Open(stream, FileReadOption.ReadAll, 0);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Parser messages can embed tag content; never forward them.
            throw Fail($"Dataset {label} is not a readable complete DICOM Part 10 file.");
        }

        if (dicom.Format != DicomFileFormat.DICOM3 || dicom.IsPartial)
        {
            throw Fail($"Dataset {label} is not a readable complete DICOM Part 10 file.");
        }

        RequireSafeFileMeta(dicom, label);

        ImageFingerprint actual;
        try
        {
            DicomSanitizer.ValidateSanitized(dicom.Dataset, file.Ordinal);
            actual = DicomSanitizer.Fingerprint(dicom.Dataset);
        }
        catch (InvalidDataException ex)
        {
            // Sanitizer messages are fixed and non-sensitive by contract.
            throw Fail($"Dataset {label} violates the sanitized metadata policy: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw Fail($"Dataset {label} could not be validated against the sanitized metadata policy.");
        }

        var entry = file.Entry;
        if (!string.Equals(entry.PixelDataSha256, actual.PixelDataSha256, StringComparison.Ordinal)
            || !string.Equals(entry.SourcePixelDataSha256, actual.PixelDataSha256, StringComparison.Ordinal))
        {
            throw Fail($"Dataset {label} pixel data fingerprint differs from the manifest or the original source.");
        }

        if (!string.Equals(entry.GeometrySha256, actual.GeometrySha256, StringComparison.Ordinal)
            || !string.Equals(entry.SourceGeometrySha256, actual.GeometrySha256, StringComparison.Ordinal))
        {
            throw Fail($"Dataset {label} geometry fingerprint differs from the manifest or the original source.");
        }

        if (entry.SlicePositionMm != actual.SlicePositionMm)
        {
            throw Fail($"Dataset {label} slice position differs from the manifest.");
        }
    }

    private static void RequireSafeFileMeta(DicomFile dicom, string label)
    {
        try
        {
            var meta = dicom.FileMetaInfo;
            foreach (var item in meta)
            {
                // Group length, version, SOP class/instance, transfer syntax, implementation class/version only.
                if (item.Tag.Group != 0x0002
                    || item.Tag.Element is not (0x0000 or 0x0001 or 0x0002 or 0x0003 or 0x0010 or 0x0012 or 0x0013))
                {
                    throw Fail($"Dataset {label} file meta information contains an unsupported element.");
                }
            }

            var dataset = dicom.Dataset;
            var consistent =
                string.Equals(meta.MediaStorageSOPClassUID?.UID, dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, string.Empty), StringComparison.Ordinal)
                && string.Equals(meta.MediaStorageSOPInstanceUID?.UID, dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, string.Empty), StringComparison.Ordinal)
                && string.Equals(meta.TransferSyntax.UID.UID, DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID, StringComparison.Ordinal);
            if (!consistent)
            {
                throw Fail($"Dataset {label} file meta information disagrees with the dataset or transfer syntax.");
            }
        }
        catch (Exception ex) when (ex is not (InvalidDataException or OutOfMemoryException))
        {
            throw Fail($"Dataset {label} file meta information could not be validated.");
        }
    }

    private sealed record RegisteredFile(DatasetFileEntry Entry, int Ordinal, string FullPath);
}