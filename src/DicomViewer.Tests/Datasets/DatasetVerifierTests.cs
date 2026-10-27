using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using DicomViewer.DatasetTool;

using FellowOakDicom;
using FellowOakDicom.IO.Buffer;

using FluentAssertions;

namespace DicomViewer.Tests.Datasets;

/// <summary>
/// Consumer-visible behavior of the offline dataset verifier: a restored dataset passes, every unrestored,
/// altered, unregistered or unsafe state fails with a fixed message that never echoes manifest-provided text.
/// </summary>
public sealed class DatasetVerifierTests
{
    [Fact]
    public void Verify_ReturnsCountAndBytes_WhenRegisteredDatasetIsRestored()
    {
        using var fixture = new DatasetFixture(2);
        Directory.CreateDirectory(Path.Combine(fixture.Root, "notes"));
        File.WriteAllText(Path.Combine(fixture.Root, "README.txt"), "plain text is not DICOM");
        File.WriteAllText(Path.Combine(fixture.Root, "notes", "readme.md"), "plain text is not DICOM");

        var result = DatasetVerifier.Verify(fixture.ManifestPath, 2);

        result.FileCount.Should().Be(2);
        result.Bytes.Should().Be(fixture.Manifest.Files.Sum(file => file.Size));
    }

    [Fact]
    public void Verify_Throws_WhenManifestIsMissing()
    {
        using var fixture = new DatasetFixture(1);
        File.Delete(fixture.ManifestPath);

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenExpectedCountIsNotPositive()
    {
        using var fixture = new DatasetFixture(1);

        var verify = () => DatasetVerifier.Verify(fixture.ManifestPath, 0);

        verify.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Verify_Throws_WhenManifestFileCountDiffersFromExpectedCount()
    {
        using var fixture = new DatasetFixture(1);

        Fails(fixture, expectedCount: 2);
    }

    [Fact]
    public void Verify_Throws_WhenRegisteredFileIsMissing()
    {
        using var fixture = new DatasetFixture(2);
        File.Delete(fixture.FilePath(2));

        Fails(fixture, expectedCount: 2);
    }

    [Fact]
    public void Verify_Throws_WhenRegisteredFileIsUnresolvedLfsPointer()
    {
        using var fixture = new DatasetFixture(1);
        var entry = fixture.Manifest.Files[0];
        File.WriteAllText(fixture.FilePath(1),
            $"version https://git-lfs.github.com/spec/v1\noid sha256:{entry.Sha256}\nsize {entry.Size}\n", new UTF8Encoding(false));

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenManifestIsUnresolvedLfsPointer()
    {
        using var fixture = new DatasetFixture(1);
        File.WriteAllText(fixture.ManifestPath,
            $"version https://git-lfs.github.com/spec/v1\noid sha256:{new string('0', 64)}\nsize 1\n", new UTF8Encoding(false));

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenPayloadBytesAreCorruptedWithSameSize()
    {
        using var fixture = new DatasetFixture(1);
        var payload = File.ReadAllBytes(fixture.FilePath(1));
        payload[^1] ^= 0x01;
        File.WriteAllBytes(fixture.FilePath(1), payload);

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenPayloadSizeDiffersFromManifest()
    {
        using var fixture = new DatasetFixture(1);
        var payload = File.ReadAllBytes(fixture.FilePath(1));
        File.WriteAllBytes(fixture.FilePath(1), payload[..^10]);

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenManifestHashesMatchAlteredPayloadButPixelFingerprintDiffers()
    {
        using var fixture = new DatasetFixture(1);
        var payload = File.ReadAllBytes(fixture.FilePath(1));
        payload[^1] ^= 0x01;
        File.WriteAllBytes(fixture.FilePath(1), payload);
        var hash = Convert.ToHexStringLower(SHA256.HashData(payload));
        fixture.Rewrite(manifest => manifest with
        {
            Files = [manifest.Files[0] with { Sha256 = hash, LfsOid = $"sha256:{hash}" }]
        });

        Fails(fixture);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Verify_Throws_WhenManifestFingerprintDiffersFromStoredDicom(bool geometry)
    {
        using var fixture = new DatasetFixture(1);
        var other = new string('0', 64);
        fixture.Rewrite(manifest => manifest with
        {
            Files = [geometry
                ? manifest.Files[0] with { SourceGeometrySha256 = other }
                : manifest.Files[0] with { PixelDataSha256 = other }]
        });

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenLfsObjectIdDisagreesWithSha256()
    {
        using var fixture = new DatasetFixture(1);
        fixture.Rewrite(manifest => manifest with
        {
            Files = [manifest.Files[0] with { LfsOid = $"sha256:{new string('0', 64)}" }]
        });

        Fails(fixture);
    }

    [Theory]
    [InlineData("../0001.dcm")]
    [InlineData("sanitized/../0001.dcm")]
    [InlineData("./0001.dcm")]
    [InlineData("/0001.dcm")]
    [InlineData("C:/data/0001.dcm")]
    [InlineData("//server/share/0001.dcm")]
    [InlineData("https://example.test/0001.dcm")]
    [InlineData("sanitized\\0001.dcm")]
    [InlineData("sanitized//0001.dcm")]
    [InlineData("sanitized/0001.dcm/")]
    [InlineData("sanitized/0001.dcm ")]
    [InlineData("sanitized/0001.dcm:stream")]
    [InlineData("sanitized/0001.DCM")]
    [InlineData("sanitized/1.dcm")]
    [InlineData("sanitized/0000.dcm")]
    [InlineData("sanitized/0002.dcm")]
    [InlineData("")]
    public void Verify_Throws_WhenManifestPathIsNotANormalizedRegisteredRelativePath(string path)
    {
        using var fixture = new DatasetFixture(1);
        fixture.Rewrite(manifest => manifest with { Files = [manifest.Files[0] with { Path = path }] });

        var exception = Fails(fixture);

        if (path.Length > 0)
        {
            exception.Message.Should().NotContain(path);
        }
    }

    [Theory]
    [InlineData("sanitized/0001.dcm")]
    [InlineData("Sanitized/0001.dcm")]
    [InlineData("other/0001.dcm")]
    public void Verify_Throws_WhenManifestRegistersTheSameFileTwice(string secondPath)
    {
        using var fixture = new DatasetFixture(2);
        fixture.Rewrite(manifest => manifest with
        {
            Files = [manifest.Files[0], manifest.Files[1] with { Path = secondPath }]
        });

        Fails(fixture, expectedCount: 2);
    }

    [Theory]
    [InlineData("extra.dcm", false)]
    [InlineData("EXTRA.DICOM", false)]
    [InlineData("nested/deeper/other.DCM", false)]
    [InlineData("blob", true)]
    [InlineData("nested/blob.bin", true)]
    public void Verify_Throws_WhenDirectoryContainsUnregisteredDicom(string relativePath, bool hasDicomPreamble)
    {
        using var fixture = new DatasetFixture(1);
        var path = Path.Combine(fixture.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] content = hasDicomPreamble ? new byte[140] : Encoding.ASCII.GetBytes("not parsed as DICOM");
        if (hasDicomPreamble)
        {
            "DICM"u8.CopyTo(content.AsSpan(128));
        }

        File.WriteAllBytes(path, content);

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenAnyRequiredRootFieldIsAbsent()
    {
        using var fixture = new DatasetFixture(1);
        var fields = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath))!.AsObject().Select(property => property.Key).ToArray();

        fields.Should().NotBeEmpty();
        foreach (var field in fields)
        {
            fixture.EditJson(root => root.Remove(field).Should().BeTrue());
            Fails(fixture);
            fixture.Rewrite(manifest => manifest);
        }
    }

    [Theory]
    [InlineData("schemaVersion", 2)]
    [InlineData("datasetId", "other-dataset")]
    [InlineData("sourceUrl", "https://example.test/data")]
    [InlineData("sourceVersion", "2.0")]
    [InlineData("licenseId", "MIT")]
    [InlineData("licenseUrl", "https://example.test/license")]
    [InlineData("redistributionAllowed", false)]
    [InlineData("classification", "Weight-bearing CT")]
    [InlineData("collectedAtUtc", "2026-01-01T00:00:00+09:00")]
    public void Verify_Throws_WhenRootFieldIsNotTheSupportedValue(string field, object value)
    {
        using var fixture = new DatasetFixture(1);
        fixture.EditJson(root => root[field] = JsonSerializer.SerializeToNode(value));

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WhenRootFieldIsNull()
    {
        using var fixture = new DatasetFixture(1);
        fixture.EditJson(root => root["licenseId"] = null);

        Fails(fixture);
    }

    [Fact]
    public void Verify_Throws_WithoutEchoingUnexpectedRootField()
    {
        using var fixture = new DatasetFixture(1);
        fixture.EditJson(root => root["unreviewedFreeText"] = "value");

        var exception = Fails(fixture, expectedCount: 1);

        exception.Message.Should().NotContain("unreviewedFreeText");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Verify_Throws_WhenFileEntryLacksRequiredFieldOrAddsUnreviewedField(bool removeField)
    {
        using var fixture = new DatasetFixture(1);
        fixture.EditJson(root =>
        {
            var entry = root["files"]![0]!.AsObject();
            if (removeField)
            {
                entry.Remove("sha256").Should().BeTrue();
            }
            else
            {
                entry["unreviewedFreeText"] = "value";
            }
        });

        Fails(fixture);
    }

    // Directory links only (junction fallback on Windows needs no privilege, so these always run). Replacing the
    // manifest or a registered payload file by a file symbolic link is deliberately NOT tested here: creating one
    // requires privileges that cannot be assumed, and a test that silently returns when it cannot would prove nothing.
    [Theory]
    [InlineData("root")]
    [InlineData("directory")]
    public void Verify_Throws_WhenRootOrRegisteredDirectoryIsALink(string linked)
    {
        using var fixture = new DatasetFixture(1);
        var outside = Path.Combine(Path.GetTempPath(), $"dataset-verifier-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        var link = string.Empty;
        try
        {
            var manifestPath = fixture.ManifestPath;
            if (linked == "root")
            {
                link = Path.Combine(outside, "link");
                CreateDirectoryLink(link, fixture.Root);
                manifestPath = Path.Combine(link, "manifest.json");
            }
            else
            {
                link = Path.Combine(fixture.Root, "sanitized");
                Directory.Move(link, Path.Combine(outside, "sanitized"));
                CreateDirectoryLink(link, Path.Combine(outside, "sanitized"));
            }

            var verify = () => DatasetVerifier.Verify(manifestPath, 1);
            verify.Should().Throw<InvalidDataException>();
        }
        finally
        {
            // Delete the link itself, never the content behind it.
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(outside, recursive: true);
        }
    }

    private static void CreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && OperatingSystem.IsWindows())
        {
            using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            mklink!.WaitForExit();
            mklink.ExitCode.Should().Be(0, "a directory junction must be creatable for this test to be meaningful");
        }

        new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint).Should().BeTrue();
    }

    private static InvalidDataException Fails(DatasetFixture fixture, int expectedCount = 1)
    {
        var verify = () => DatasetVerifier.Verify(fixture.ManifestPath, expectedCount);
        return verify.Should().Throw<InvalidDataException>().Which;
    }

    /// <summary>
    /// Isolated temporary dataset: synthetic CT slices written through the sanitizer plus a manifest describing them.
    /// </summary>
    private sealed class DatasetFixture : IDisposable
    {
        private const int PixelByteCount = 512 * 512 * 2;
        private static readonly string DownloadHost = new Uri(DatasetDefinition.MetadataUrl).Host;

        public DatasetFixture(int count)
        {
            Root = Path.Combine(Path.GetTempPath(), $"dataset-verifier-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(Root, "sanitized"));
            Manifest = new DatasetManifest
            {
                CollectedAtUtc = "2026-01-01T00:00:00.0000000+00:00",
                Files = Enumerable.Range(1, count).Select(CreateFile).ToList()
            };
            Write(Manifest);
        }

        public string Root { get; }

        public string ManifestPath => Path.Combine(Root, "manifest.json");

        public DatasetManifest Manifest { get; private set; }

        public string FilePath(int ordinal) => Path.Combine(Root, "sanitized", $"{ordinal:D4}.dcm");

        public void Rewrite(Func<DatasetManifest, DatasetManifest> change)
        {
            Manifest = change(Manifest);
            Write(Manifest);
        }

        public void EditJson(Action<JsonObject> change)
        {
            var root = JsonNode.Parse(File.ReadAllText(ManifestPath))!.AsObject();
            change(root);
            File.WriteAllText(ManifestPath, root.ToJsonString());
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort temp cleanup; a leftover directory must not fail the test.
            }
        }

        private void Write(DatasetManifest manifest) =>
            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, DatasetDefinition.JsonOptions));

        private DatasetFileEntry CreateFile(int ordinal)
        {
            var source = CreateSource(ordinal);
            var sourceFingerprint = DicomSanitizer.Fingerprint(source);

            using var output = new MemoryStream();
            new DicomFile(DicomSanitizer.Sanitize(source, ordinal)).Save(output);
            var payload = output.ToArray();
            File.WriteAllBytes(FilePath(ordinal), payload);

            using var reopened = new MemoryStream(payload, writable: false);
            var storedFingerprint = DicomSanitizer.Fingerprint(DicomFile.Open(reopened, FileReadOption.ReadAll, 0).Dataset);
            var hash = Convert.ToHexStringLower(SHA256.HashData(payload));
            var sourceId = 1000L + ordinal;
            return new DatasetFileEntry
            {
                Path = $"sanitized/{ordinal:D4}.dcm",
                SourceFileId = sourceId,
                DownloadUrl = $"https://{DownloadHost}/api/access/datafile/{sourceId}",
                OriginalFilename = $"VHFCT1mm-Ankle ({ordinal}).dcm",
                SourceMd5 = new string('a', 32),
                SourceSha256 = new string('b', 64),
                SourceSize = PixelByteCount + 1024,
                Sha256 = hash,
                Size = payload.Length,
                LfsOid = $"sha256:{hash}",
                SourcePixelDataSha256 = sourceFingerprint.PixelDataSha256,
                PixelDataSha256 = storedFingerprint.PixelDataSha256,
                SourceGeometrySha256 = sourceFingerprint.GeometrySha256,
                GeometrySha256 = storedFingerprint.GeometrySha256,
                SlicePositionMm = storedFingerprint.SlicePositionMm
            };
        }

        private static DicomDataset CreateSource(int ordinal)
        {
            var pixels = new byte[PixelByteCount];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = (byte)(index * 31 + ordinal);
            }

            return new DicomDataset(new DicomItem[]
            {
                new DicomUniqueIdentifier(DicomTag.SOPClassUID, new[] { DicomUID.CTImageStorage }),
                new DicomUnsignedShort(DicomTag.Rows, new ushort[] { 512 }),
                new DicomUnsignedShort(DicomTag.Columns, new ushort[] { 512 }),
                new DicomUnsignedShort(DicomTag.BitsAllocated, new ushort[] { 16 }),
                new DicomUnsignedShort(DicomTag.BitsStored, new ushort[] { 16 }),
                new DicomUnsignedShort(DicomTag.HighBit, new ushort[] { 15 }),
                new DicomUnsignedShort(DicomTag.PixelRepresentation, new ushort[] { 1 }),
                new DicomUnsignedShort(DicomTag.SamplesPerPixel, new ushort[] { 1 }),
                new DicomCodeString(DicomTag.PhotometricInterpretation, new[] { "MONOCHROME2" }),
                new DicomDecimalString(DicomTag.PixelSpacing, new[] { 1m, 1m }),
                new DicomDecimalString(DicomTag.ImageOrientationPatient, new[] { 1m, 0m, 0m, 0m, 1m, 0m }),
                new DicomDecimalString(DicomTag.ImagePositionPatient, new[] { 0m, 0m, (decimal)ordinal }),
                new DicomDecimalString(DicomTag.SliceThickness, new[] { 1m }),
                new DicomDecimalString(DicomTag.RescaleSlope, new[] { 1m }),
                new DicomDecimalString(DicomTag.RescaleIntercept, new[] { 0m }),
                new DicomOtherWord(DicomTag.PixelData, new MemoryByteBuffer(pixels))
            });
        }
    }
}