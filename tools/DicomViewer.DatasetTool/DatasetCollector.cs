using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using DicomViewer.Infrastructure.Datasets;

using FellowOakDicom;

namespace DicomViewer.DatasetTool;

public static class DatasetCollector
{
    private static readonly Regex SourceName = new(@"^VHFCT1mm-Ankle \((\d+)\)\.dcm$", RegexOptions.CultureInvariant);

    public static async Task<VerificationResult> CollectAsync(string directory, CancellationToken cancellationToken)
    {
        directory = Path.GetFullPath(directory);
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new InvalidDataException("Collection requires an empty destination; existing fixtures are never overwritten or downloaded again.");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DicomViewer.DatasetTool", "1.0"));
        using var metadataStream = await client.GetStreamAsync(DatasetDefinition.MetadataUrl, cancellationToken);
        using var metadata = await JsonDocument.ParseAsync(metadataStream, cancellationToken: cancellationToken);
        var data = metadata.RootElement.GetProperty("data");
        if (data.GetProperty("datasetPersistentId").GetString() != "doi:10.7910/DVN/3JDZCT"
            || data.GetProperty("versionNumber").GetInt32() != 1
            || data.GetProperty("versionMinorNumber").GetInt32() != 0
            || data.GetProperty("license").GetProperty("rightsIdentifier").GetString() != DatasetDefinition.LicenseId)
        {
            throw new InvalidDataException("Public source identity, fixed version or redistribution license does not match the reviewed baseline.");
        }

        var sources = new List<SourceFile>();
        foreach (var file in data.GetProperty("files").EnumerateArray())
        {
            var source = file.GetProperty("dataFile");
            var name = source.GetProperty("filename").GetString() ?? "";
            var match = SourceName.Match(name);
            if (!match.Success)
            {
                continue;
            }

            if (file.GetProperty("restricted").GetBoolean()
                || source.GetProperty("contentType").GetString() != "application/dicom")
            {
                throw new InvalidDataException("Selected source is restricted or is not declared DICOM.");
            }

            sources.Add(new SourceFile(source.GetProperty("id").GetInt64(), name,
                int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                source.GetProperty("filesize").GetInt64(), source.GetProperty("md5").GetString() ?? ""));
        }

        sources.Sort((left, right) => left.Number.CompareTo(right.Number));
        if (sources.Count != DatasetDefinition.FileCount || sources.Select(source => source.Id).Distinct().Count() != sources.Count
            || sources.Select(source => source.Number).Distinct().Count() != sources.Count)
        {
            throw new InvalidDataException("Fixed source must contain exactly 150 distinct female ankle CT files.");
        }

        Directory.CreateDirectory(Path.Combine(directory, "sanitized"));
        var entries = new DatasetFileEntry[sources.Count];
        var completed = 0;
        async ValueTask CollectOneAsync(int index, CancellationToken token)
        {
            var source = sources[index];
            var url = $"https://dataverse.harvard.edu/api/access/datafile/{source.Id}";
            var original = await client.GetByteArrayAsync(url, token);
            if (original.LongLength != source.Size
                || !string.Equals(Convert.ToHexStringLower(MD5.HashData(original)), source.Md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Downloaded original does not match the fixed public source size/checksum.");
            }

            using var originalStream = new MemoryStream(original, writable: false);
            var originalFile = await DicomFile.OpenAsync(originalStream, FileReadOption.ReadAll);
            var sourceFingerprint = DicomSanitizer.Fingerprint(originalFile.Dataset);
            var sanitized = DicomSanitizer.Sanitize(originalFile.Dataset, index + 1);
            var relativePath = $"sanitized/{index + 1:D4}.dcm";
            var output = Path.Combine(directory, relativePath);
            await new DicomFile(sanitized).SaveAsync(output);
            var stored = await DicomFile.OpenAsync(output, FileReadOption.ReadAll);
            DicomSanitizer.ValidateSanitized(stored.Dataset, index + 1);
            var storedFingerprint = DicomSanitizer.Fingerprint(stored.Dataset);
            if (sourceFingerprint != storedFingerprint)
            {
                throw new InvalidDataException("Stored DICOM changed the original pixel bytes or geometry.");
            }

            await using var payload = File.OpenRead(output);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(payload, token));
            entries[index] = new DatasetFileEntry
            {
                Path = relativePath,
                SourceFileId = source.Id,
                DownloadUrl = url,
                OriginalFilename = source.Name,
                SourceMd5 = source.Md5.ToLowerInvariant(),
                SourceSha256 = Convert.ToHexStringLower(SHA256.HashData(original)),
                SourceSize = source.Size,
                Sha256 = hash,
                Size = payload.Length,
                LfsOid = $"sha256:{hash}",
                SourcePixelDataSha256 = sourceFingerprint.PixelDataSha256,
                PixelDataSha256 = storedFingerprint.PixelDataSha256,
                SourceGeometrySha256 = sourceFingerprint.GeometrySha256,
                GeometrySha256 = storedFingerprint.GeometrySha256,
                SlicePositionMm = storedFingerprint.SlicePositionMm
            };
            var progress = Interlocked.Increment(ref completed);
            if (progress % 25 == 0)
            {
                Console.WriteLine($"Collected {progress}/{DatasetDefinition.FileCount} public CT files.");
            }
        }

        await Parallel.ForEachAsync(Enumerable.Range(0, sources.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (index, token) =>
            {
                try
                {
                    await CollectOneAsync(index, token);
                }
                catch (InvalidDataException exception)
                {
                    // Source numbers are public file names; the message is fixed text without source values.
                    throw new InvalidDataException($"Source file {sources[index].Number}: {exception.Message}");
                }
            });

        var manifest = new DatasetManifest
        {
            CollectedAtUtc = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            Files = entries.ToList()
        };
        var manifestPath = Path.Combine(directory, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, DatasetDefinition.JsonOptions) + "\n", cancellationToken);
        return DatasetVerifier.Verify(manifestPath);
    }

    private sealed record SourceFile(long Id, string Name, int Number, long Size, string Md5);
}