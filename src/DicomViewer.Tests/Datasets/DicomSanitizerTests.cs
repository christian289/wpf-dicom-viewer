using System.Security.Cryptography;
using System.Text;

using DicomViewer.DatasetTool;

using FellowOakDicom;
using FellowOakDicom.IO.Buffer;

using FluentAssertions;

namespace DicomViewer.Tests.Datasets;

/// <summary>
/// Privacy and fidelity tests for <see cref="DicomSanitizer"/>.
/// Every metadata value used here is synthetic; pixel data is a deterministic byte pattern.
/// </summary>
public sealed class DicomSanitizerTests
{
    private const int PixelByteCount = 512 * 512 * sizeof(short);

    private sealed record SourceIdentity(
        string PatientName,
        string PatientId,
        string BirthDate,
        string StudyDate,
        string Institution,
        string Description,
        string Accession,
        string StudyUid,
        string SeriesUid,
        string SopUid,
        string FrameUid,
        string PrivateValue,
        string SequenceUid)
    {
        public IReadOnlyList<string> Markers =>
        [
            PatientName, PatientId, BirthDate, StudyDate, Institution, Description, Accession,
            StudyUid, SeriesUid, SopUid, FrameUid, PrivateValue, SequenceUid,
        ];
    }

    private static readonly SourceIdentity PrimaryIdentity = new(
        "Source^Person", "SRC-PATIENT-0001", "19710305", "20200102", "Source Institution Name",
        "Source Study Description", "ACC-SRC-1", "2.25.111111", "2.25.222222", "2.25.333333",
        "2.25.444444", "SRC-PRIVATE-VALUE", "2.25.555555");

    private static readonly SourceIdentity AlternateIdentity = new(
        "Other^Name", "OTHER-ID-77", "19850709", "20211231", "Other Institution",
        "Other Description", "ACC-OTHER-9", "2.25.999111", "2.25.999222", "2.25.999333",
        "2.25.999444", "OTHER-PRIVATE", "2.25.999555");

    private static readonly DicomTag[] CopiedTags =
    [
        DicomTag.Rows, DicomTag.Columns, DicomTag.BitsAllocated, DicomTag.BitsStored, DicomTag.HighBit,
        DicomTag.PixelRepresentation, DicomTag.SamplesPerPixel, DicomTag.PhotometricInterpretation,
        DicomTag.PixelSpacing, DicomTag.ImageOrientationPatient, DicomTag.ImagePositionPatient,
        DicomTag.SliceThickness, DicomTag.SpacingBetweenSlices, DicomTag.RescaleSlope,
        DicomTag.RescaleIntercept, DicomTag.RescaleType, DicomTag.WindowCenter, DicomTag.WindowWidth,
        DicomTag.VOILUTFunction, DicomTag.PixelPaddingValue, DicomTag.PixelPaddingRangeLimit,
        DicomTag.PixelData,
    ];

    private static readonly DicomTag[] GeneratedTags =
    [
        DicomTag.SOPClassUID, DicomTag.SOPInstanceUID, DicomTag.ImageType, DicomTag.StudyDate,
        DicomTag.StudyTime, DicomTag.AccessionNumber, DicomTag.Modality, DicomTag.Manufacturer,
        DicomTag.ReferringPhysicianName, DicomTag.PatientName, DicomTag.PatientID,
        DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.KVP, DicomTag.StudyInstanceUID,
        DicomTag.SeriesInstanceUID, DicomTag.StudyID, DicomTag.SeriesNumber,
        DicomTag.AcquisitionNumber, DicomTag.InstanceNumber, DicomTag.FrameOfReferenceUID,
        DicomTag.PositionReferenceIndicator,
    ];

    private static readonly DicomTag[] OptionalSourceTags =
    [
        DicomTag.SliceThickness, DicomTag.SpacingBetweenSlices, DicomTag.RescaleSlope,
        DicomTag.RescaleIntercept, DicomTag.RescaleType, DicomTag.WindowCenter, DicomTag.WindowWidth,
        DicomTag.VOILUTFunction, DicomTag.PixelPaddingValue, DicomTag.PixelPaddingRangeLimit,
    ];

    private static readonly Dictionary<string, Action<DicomDataset>> UnsupportedSources = new()
    {
        ["non-CT SOP class"] = d => d.AddOrUpdate(Ui(DicomTag.SOPClassUID, DicomUID.MRImageStorage.UID)),
        ["missing SOP class"] = d => d.Remove(DicomTag.SOPClassUID),
        ["multi-frame"] = d => d.AddOrUpdate(Is(DicomTag.NumberOfFrames, 2)),
        ["multi-frame with all frames in pixel data"] = d =>
        {
            d.AddOrUpdate(Is(DicomTag.NumberOfFrames, 2));
            d.AddOrUpdate(PixelData(CreatePixels(1, frames: 2)));
        },
        ["256 rows"] = d => d.AddOrUpdate(Us(DicomTag.Rows, 256)),
        ["256 columns"] = d => d.AddOrUpdate(Us(DicomTag.Columns, 256)),
        ["8-bit allocation"] = d =>
        {
            d.AddOrUpdate(Us(DicomTag.BitsAllocated, 8));
            d.AddOrUpdate(Us(DicomTag.BitsStored, 8));
            d.AddOrUpdate(Us(DicomTag.HighBit, 7));
        },
        ["12-bit storage"] = d =>
        {
            d.AddOrUpdate(Us(DicomTag.BitsStored, 12));
            d.AddOrUpdate(Us(DicomTag.HighBit, 11));
        },
        ["unsigned pixels"] = d => d.AddOrUpdate(Us(DicomTag.PixelRepresentation, 0)),
        ["missing pixel representation"] = d => d.Remove(DicomTag.PixelRepresentation),
        ["color samples"] = d =>
        {
            d.AddOrUpdate(Us(DicomTag.SamplesPerPixel, 3));
            d.AddOrUpdate(Cs(DicomTag.PhotometricInterpretation, "RGB"));
        },
        ["MONOCHROME1"] = d => d.AddOrUpdate(Cs(DicomTag.PhotometricInterpretation, "MONOCHROME1")),
        ["OB pixel data"] = d => d.AddOrUpdate(new DicomOtherByte(DicomTag.PixelData, new MemoryByteBuffer(CreatePixels(0)))),
        ["encapsulated pixel data"] = d => d.AddOrUpdate(new DicomOtherWordFragment(DicomTag.PixelData)),
        ["missing pixel data"] = d => d.Remove(DicomTag.PixelData),
        ["short pixel data"] = d => d.AddOrUpdate(PixelData(new byte[PixelByteCount - 2])),
        ["oversized pixel data"] = d => d.AddOrUpdate(PixelData(new byte[PixelByteCount + 2])),
        ["missing pixel spacing"] = d => d.Remove(DicomTag.PixelSpacing),
        ["zero pixel spacing"] = d => d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "0", "0.5")),
        ["negative pixel spacing"] = d => d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "-0.5", "0.5")),
        ["non-finite pixel spacing"] = d => d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "1e999", "1")),
        ["single-valued pixel spacing"] = d => AddWithoutValidation(d, Ds(DicomTag.PixelSpacing, "0.5")),
        ["pixel spacing with wrong VR"] = d => AddWithoutValidation(d, Lo(DicomTag.PixelSpacing, "0.5")),
        ["missing orientation"] = d => d.Remove(DicomTag.ImageOrientationPatient),
        ["non-unit orientation"] = d => d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "2", "0", "0", "0", "1", "0")),
        ["non-orthogonal orientation"] = d => d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "1", "0", "0", "1", "0", "0")),
        ["five-valued orientation"] = d => AddWithoutValidation(d, Ds(DicomTag.ImageOrientationPatient, "1", "0", "0", "0", "1")),
        ["missing position"] = d => d.Remove(DicomTag.ImagePositionPatient),
        ["two-valued position"] = d => AddWithoutValidation(d, Ds(DicomTag.ImagePositionPatient, "1", "2")),
        ["non-finite position"] = d => d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "1e999", "0", "0")),
        ["burned-in annotation"] = d => d.AddOrUpdate(Cs(DicomTag.BurnedInAnnotation, "YES")),
        ["multi-valued slice thickness"] = d => AddWithoutValidation(d, Ds(DicomTag.SliceThickness, "1", "2")),
        ["non-finite rescale slope"] = d => d.AddOrUpdate(Ds(DicomTag.RescaleSlope, "1e999")),
        ["free-text rescale type"] = d => d.AddOrUpdate(Lo(DicomTag.RescaleType, "Free text note")),
        ["rescale type with wrong VR"] = d => d.AddOrUpdate(Cs(DicomTag.RescaleType, "HU")),
        ["window center without width"] = d => d.Remove(DicomTag.WindowWidth),
        ["window width without center"] = d => d.Remove(DicomTag.WindowCenter),
        ["non-positive window width"] = d => d.AddOrUpdate(Ds(DicomTag.WindowWidth, "0", "1500")),
        ["mismatched window counts"] = d => d.AddOrUpdate(Ds(DicomTag.WindowWidth, "400")),
        ["unknown VOI LUT function"] = d => d.AddOrUpdate(Cs(DicomTag.VOILUTFunction, "CUSTOM")),
        ["padding range limit without value"] = d => d.Remove(DicomTag.PixelPaddingValue),
        ["unsigned padding value"] = d => d.AddOrUpdate(new DicomUnsignedShort(DicomTag.PixelPaddingValue, new ushort[] { 1 })),
        ["empty spacing between slices"] = d => d.AddOrUpdate(new DicomDecimalString(DicomTag.SpacingBetweenSlices, Array.Empty<string>())),
        ["empty rescale slope"] = d => d.AddOrUpdate(new DicomDecimalString(DicomTag.RescaleSlope, Array.Empty<string>())),
        ["empty rescale intercept"] = d => d.AddOrUpdate(new DicomDecimalString(DicomTag.RescaleIntercept, Array.Empty<string>())),
        ["empty rescale type"] = d => d.AddOrUpdate(Ascii(new DicomLongString(DicomTag.RescaleType, Array.Empty<string>()))),
        ["empty window center"] = d => d.AddOrUpdate(new DicomDecimalString(DicomTag.WindowCenter, Array.Empty<string>())),
        ["empty window width"] = d => d.AddOrUpdate(new DicomDecimalString(DicomTag.WindowWidth, Array.Empty<string>())),
        ["empty VOI LUT function"] = d => d.AddOrUpdate(new DicomCodeString(DicomTag.VOILUTFunction, Array.Empty<string>())),
        ["empty pixel padding value"] = d => d.AddOrUpdate(new DicomSignedShort(DicomTag.PixelPaddingValue, Array.Empty<short>())),
        ["empty pixel padding range limit"] = d => d.AddOrUpdate(new DicomSignedShort(DicomTag.PixelPaddingRangeLimit, Array.Empty<short>())),
        ["zero rescale slope"] = d => d.AddOrUpdate(Ds(DicomTag.RescaleSlope, "0")),
        ["burned-in annotation not declared NO"] = d => d.AddOrUpdate(Cs(DicomTag.BurnedInAnnotation, "UNKNOWN")),
        ["modality LUT sequence"] = d => d.AddOrUpdate(new DicomSequence(DicomTag.ModalityLUTSequence, Array.Empty<DicomDataset>())),
        ["VOI LUT sequence"] = d => d.AddOrUpdate(new DicomSequence(DicomTag.VOILUTSequence, Array.Empty<DicomDataset>())),
    };

    private static readonly Dictionary<string, Action<DicomDataset>> InvalidSanitizedDatasets = new()
    {
        ["extra institution name"] = d => d.AddOrUpdate(Lo(DicomTag.InstitutionName, "Extra Institution")),
        ["file meta attribute"] = d => d.AddOrUpdate(Ui(DicomTag.TransferSyntaxUID, DicomUID.ExplicitVRLittleEndian.UID)),
        ["private attribute"] = d => d.AddOrUpdate(Lo(new DicomTag(0x0009, 0x1001), "extra private value")),
        ["sequence"] = d => d.AddOrUpdate(new DicomSequence(
            DicomTag.ReferencedStudySequence,
            new[] { new DicomDataset(new DicomItem[] { Ui(DicomTag.ReferencedSOPInstanceUID, "2.25.1") }) })),
        ["empty sequence"] = d => d.AddOrUpdate(new DicomSequence(DicomTag.ReferencedStudySequence, Array.Empty<DicomDataset>())),
        ["source patient name"] = d => d.AddOrUpdate(Pn(DicomTag.PatientName, PrimaryIdentity.PatientName)),
        ["source patient id"] = d => d.AddOrUpdate(Lo(DicomTag.PatientID, PrimaryIdentity.PatientId)),
        ["birth date present"] = d => d.AddOrUpdate(Da(DicomTag.PatientBirthDate, PrimaryIdentity.BirthDate)),
        ["study date present"] = d => d.AddOrUpdate(Da(DicomTag.StudyDate, PrimaryIdentity.StudyDate)),
        ["accession number present"] = d => d.AddOrUpdate(Sh(DicomTag.AccessionNumber, PrimaryIdentity.Accession)),
        ["source study UID"] = d => d.AddOrUpdate(Ui(DicomTag.StudyInstanceUID, PrimaryIdentity.StudyUid)),
        ["source series UID"] = d => d.AddOrUpdate(Ui(DicomTag.SeriesInstanceUID, PrimaryIdentity.SeriesUid)),
        ["source frame of reference UID"] = d => d.AddOrUpdate(Ui(DicomTag.FrameOfReferenceUID, PrimaryIdentity.FrameUid)),
        ["source SOP instance UID"] = d => d.AddOrUpdate(Ui(DicomTag.SOPInstanceUID, PrimaryIdentity.SopUid)),
        ["study UID with wrong VR"] = d => d.AddOrUpdate(Lo(DicomTag.StudyInstanceUID, "2.25.1")),
        ["wrong instance number"] = d => d.AddOrUpdate(Is(DicomTag.InstanceNumber, 2)),
        ["original image type"] = d => d.AddOrUpdate(Cs(DicomTag.ImageType, "ORIGINAL", "PRIMARY")),
        ["wrong modality"] = d => d.AddOrUpdate(Cs(DicomTag.Modality, "MR")),
        ["wrong SOP class"] = d => d.AddOrUpdate(Ui(DicomTag.SOPClassUID, DicomUID.MRImageStorage.UID)),
        ["missing patient ID"] = d => d.Remove(DicomTag.PatientID),
        ["missing frame of reference UID"] = d => d.Remove(DicomTag.FrameOfReferenceUID),
        ["missing slice thickness"] = d => d.Remove(DicomTag.SliceThickness),
        ["missing pixel data"] = d => d.Remove(DicomTag.PixelData),
        ["short pixel data"] = d => d.AddOrUpdate(PixelData(new byte[PixelByteCount - 2])),
        ["non-unit orientation"] = d => d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "2", "0", "0", "0", "1", "0")),
        ["free-text rescale type"] = d => d.AddOrUpdate(Lo(DicomTag.RescaleType, "Free text note")),
        ["window center without width"] = d => d.Remove(DicomTag.WindowWidth),
    };

    public static IEnumerable<object[]> UnsupportedSourceCases =>
        UnsupportedSources.Keys.Select(static name => new object[] { name });

    public static IEnumerable<object[]> InvalidSanitizedCases =>
        InvalidSanitizedDatasets.Keys.Select(static name => new object[] { name });

    #region Sanitize: metadata stripping

    [Fact]
    public void Sanitize_ProducesOnlyAllowlistedAndGeneratedAttributes()
    {
        var source = CreateSource();

        var result = DicomSanitizer.Sanitize(source, 7);

        TagNames(result).Should().BeEquivalentTo(TagNames(CopiedTags.Concat(GeneratedTags)));
        result.Any(item => item.Tag.IsPrivate || item is DicomSequence).Should().BeFalse();
    }

    [Fact]
    public void Sanitize_RemovesEverySourceIdentifierValue()
    {
        var source = CreateSource();

        var result = DicomSanitizer.Sanitize(source, 7);

        var metadataText = string.Concat(result
            .OfType<DicomElement>()
            .Where(static element => element.ValueRepresentation.Code != "OW")
            .Select(static element => Encoding.ASCII.GetString(element.Buffer.Data)));
        foreach (var marker in PrimaryIdentity.Markers)
        {
            metadataText.Should().NotContain(marker);
        }
    }

    [Fact]
    public void Sanitize_WritesSyntheticIdentityIndependentOfSourceAndCtStructure()
    {
        var source = CreateSource(PrimaryIdentity);

        var result = DicomSanitizer.Sanitize(source, 7);
        var fromOtherSource = DicomSanitizer.Sanitize(CreateSource(AlternateIdentity), 7);

        foreach (var tag in new[] { DicomTag.PatientName, DicomTag.PatientID })
        {
            Text(result, tag).Should().NotBe(Text(source, tag));
            Text(fromOtherSource, tag).Should().Be(Text(result, tag), $"{tag} must not depend on the source");
        }

        Text(result, DicomTag.SOPClassUID).Should().Be(DicomUID.CTImageStorage.UID);
        Text(result, DicomTag.Modality).Should().Be("CT");
        result.GetValues<string>(DicomTag.ImageType).Should().Equal("DERIVED", "SECONDARY");
        result.GetSingleValue<int>(DicomTag.InstanceNumber).Should().Be(7);
        result.InternalTransferSyntax.UID.UID.Should().Be(DicomUID.ExplicitVRLittleEndian.UID);

        foreach (var tag in new[]
        {
            DicomTag.StudyDate, DicomTag.StudyTime, DicomTag.AccessionNumber, DicomTag.Manufacturer,
            DicomTag.ReferringPhysicianName, DicomTag.PatientBirthDate, DicomTag.PatientSex, DicomTag.KVP,
            DicomTag.StudyID, DicomTag.SeriesNumber, DicomTag.AcquisitionNumber,
            DicomTag.PositionReferenceIndicator,
        })
        {
            IsEmpty(result, tag).Should().BeTrue($"{tag} is a generated empty type-2 attribute");
        }
    }

    [Fact]
    public void Sanitize_OmitsAbsentOptionalAttributes_AndWritesEmptySliceThickness()
    {
        var source = CreateSource(customize: d => d.Remove(OptionalSourceTags));

        var result = DicomSanitizer.Sanitize(source, 1);

        var expected = CopiedTags.Except(OptionalSourceTags).Append(DicomTag.SliceThickness).Concat(GeneratedTags);
        TagNames(result).Should().BeEquivalentTo(TagNames(expected));
        IsEmpty(result, DicomTag.SliceThickness).Should().BeTrue();
        var validate = () => DicomSanitizer.ValidateSanitized(result, 1);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Sanitize_PreservesPresentButEmptySliceThickness()
    {
        var source = CreateSource(customize: d =>
            d.AddOrUpdate(new DicomDecimalString(DicomTag.SliceThickness, Array.Empty<string>())));

        var result = DicomSanitizer.Sanitize(source, 1);

        IsEmpty(result, DicomTag.SliceThickness).Should().BeTrue();
        var validate = () => DicomSanitizer.ValidateSanitized(result, 1);
        validate.Should().NotThrow();
    }

    [Fact]
    public void Sanitize_DoesNotMutateSource()
    {
        var source = CreateSource();
        var before = Snapshot(source);

        DicomSanitizer.Sanitize(source, 5);

        Snapshot(source).Should().Equal(before);
    }

    #endregion

    #region Sanitize: pixel and geometry fidelity

    [Fact]
    public void Sanitize_PreservesPixelBytesExactly()
    {
        var source = CreateSource();

        var result = DicomSanitizer.Sanitize(source, 3);

        var sourcePixels = PixelBytes(source);
        PixelBytes(result).Should().Equal(sourcePixels);
        sourcePixels[0..4].Should().Equal(0x00, 0x80, 0xFF, 0x7F);
    }

    [Fact]
    public void Sanitize_CopiesImageGeometryAndDisplayAttributesVerbatim()
    {
        var source = CreateSource();

        var result = DicomSanitizer.Sanitize(source, 3);

        foreach (var tag in new[]
        {
            DicomTag.PixelSpacing, DicomTag.ImageOrientationPatient, DicomTag.ImagePositionPatient,
            DicomTag.SliceThickness, DicomTag.SpacingBetweenSlices, DicomTag.RescaleSlope,
            DicomTag.RescaleIntercept, DicomTag.RescaleType, DicomTag.WindowCenter, DicomTag.WindowWidth,
            DicomTag.VOILUTFunction, DicomTag.PhotometricInterpretation,
        })
        {
            result.GetValues<string>(tag).Should().Equal(source.GetValues<string>(tag), $"{tag} is copied unchanged");
        }

        foreach (var tag in new[]
        {
            DicomTag.Rows, DicomTag.Columns, DicomTag.BitsAllocated, DicomTag.BitsStored, DicomTag.HighBit,
            DicomTag.PixelRepresentation, DicomTag.SamplesPerPixel,
        })
        {
            result.GetSingleValue<ushort>(tag).Should().Be(source.GetSingleValue<ushort>(tag), $"{tag} is copied unchanged");
        }

        result.GetSingleValue<short>(DicomTag.PixelPaddingValue).Should().Be(-2000);
        result.GetSingleValue<short>(DicomTag.PixelPaddingRangeLimit).Should().Be(-1500);
    }

    [Fact]
    public void Sanitize_KeepsFingerprintOfOriginalImage()
    {
        var source = CreateSource();

        var result = DicomSanitizer.Sanitize(source, 3);

        DicomSanitizer.Fingerprint(result).Should().Be(DicomSanitizer.Fingerprint(source));
    }

    [Fact]
    public void Sanitize_OutputSurvivesDicomFileRoundTrip()
    {
        var source = CreateSource();
        var sanitized = DicomSanitizer.Sanitize(source, 12);

        var reopened = RoundTrip(sanitized);

        var validate = () => DicomSanitizer.ValidateSanitized(reopened, 12);
        validate.Should().NotThrow();
        DicomSanitizer.Fingerprint(reopened).Should().Be(DicomSanitizer.Fingerprint(source));
        PixelBytes(reopened).Should().Equal(PixelBytes(source));
    }

    #endregion

    #region Sanitize: deterministic UIDs

    [Fact]
    public void Sanitize_UidsDoNotDependOnSourceIdentifiersOrPixels()
    {
        var primary = DicomSanitizer.Sanitize(CreateSource(PrimaryIdentity, pixelSeed: 3), 4);
        var alternate = DicomSanitizer.Sanitize(CreateSource(AlternateIdentity, pixelSeed: 9), 4);

        foreach (var tag in new[]
        {
            DicomTag.StudyInstanceUID, DicomTag.SeriesInstanceUID, DicomTag.FrameOfReferenceUID,
            DicomTag.SOPInstanceUID,
        })
        {
            Text(alternate, tag).Should().Be(Text(primary, tag), $"{tag} must not depend on the source");
        }

        DicomSanitizer.Fingerprint(primary).PixelDataSha256
            .Should().NotBe(DicomSanitizer.Fingerprint(alternate).PixelDataSha256);
    }

    [Fact]
    public void Sanitize_IsRepeatable()
    {
        var first = DicomSanitizer.Sanitize(CreateSource(), 6);
        var second = DicomSanitizer.Sanitize(CreateSource(), 6);

        Snapshot(second).Should().Equal(Snapshot(first));
    }

    [Fact]
    public void Sanitize_SharesStudySeriesAndFrameUidsAcrossInstancesButNotSopUids()
    {
        var first = DicomSanitizer.Sanitize(CreateSource(), 1);
        var second = DicomSanitizer.Sanitize(CreateSource(), 2);

        foreach (var tag in new[] { DicomTag.StudyInstanceUID, DicomTag.SeriesInstanceUID, DicomTag.FrameOfReferenceUID })
        {
            Text(second, tag).Should().Be(Text(first, tag));
        }

        Text(second, DicomTag.SOPInstanceUID).Should().NotBe(Text(first, DicomTag.SOPInstanceUID));
        first.GetSingleValue<int>(DicomTag.InstanceNumber).Should().Be(1);
        second.GetSingleValue<int>(DicomTag.InstanceNumber).Should().Be(2);
    }

    [Fact]
    public void Sanitize_GeneratesValidDistinctTwoTwentyFiveUids()
    {
        var result = DicomSanitizer.Sanitize(CreateSource(), 150);

        var uids = new[]
        {
            Text(result, DicomTag.StudyInstanceUID), Text(result, DicomTag.SeriesInstanceUID),
            Text(result, DicomTag.FrameOfReferenceUID), Text(result, DicomTag.SOPInstanceUID),
        };

        uids.Should().OnlyHaveUniqueItems();
        foreach (var uid in uids)
        {
            uid.Should().StartWith("2.25.");
            uid.Length.Should().BeLessThanOrEqualTo(64);
            DicomUID.IsValidUid(uid).Should().BeTrue();
        }
    }

    #endregion

    #region Sanitize: rejection

    [Theory]
    [MemberData(nameof(UnsupportedSourceCases))]
    public void Sanitize_RejectsUnsupportedSource_WithoutLeakingSourceValues(string caseName)
    {
        var source = CreateSource(customize: UnsupportedSources[caseName]);

        AssertRejectedWithoutLeaks(() => DicomSanitizer.Sanitize(source, 1));
    }

    [Fact]
    public void Sanitize_RejectsEveryTransferSyntaxExceptExplicitVrLittleEndian()
    {
        var syntaxes = new[]
        {
            DicomTransferSyntax.ImplicitVRLittleEndian,
            DicomTransferSyntax.ExplicitVRBigEndian,
            DicomTransferSyntax.DeflatedExplicitVRLittleEndian,
            DicomTransferSyntax.JPEG2000Lossless,
            DicomTransferSyntax.RLELossless,
        };

        foreach (var syntax in syntaxes)
        {
            var source = new DicomDataset(syntax);
            source.Add(CreateSource().ToArray());

            AssertRejectedWithoutLeaks(() => DicomSanitizer.Sanitize(source, 1));
            AssertRejectedWithoutLeaks(() => DicomSanitizer.Fingerprint(source));
        }
    }

    [Fact]
    public void Sanitize_ParserFailuresDoNotQuoteSourceValues()
    {
        const string leaked = "LEAKEDTEXT";
        var source = CreateSource(customize: d => AddWithoutValidation(d, new DicomDecimalString(
            DicomTag.PixelSpacing,
            new MemoryByteBuffer(Encoding.ASCII.GetBytes($"{leaked}\\0.5")))));

        var exception = AssertRejectedWithoutLeaks(() => DicomSanitizer.Sanitize(source, 1));

        exception.Message.Should().NotContain(leaked);
    }

    [Fact]
    public void Sanitize_ReplacesUnexpectedReaderExceptionsWithFixedMessage()
    {
        const string leaked = "LEAKED-PATH-OR-VALUE";
        var source = CreateSource(customize: d =>
            AddWithoutValidation(d, new DicomOtherWord(DicomTag.PixelData, new FailingBuffer(leaked))));

        var exception = AssertRejectedWithoutLeaks(() => DicomSanitizer.Sanitize(source, 1));

        exception.Message.Should().NotContain(leaked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Sanitize_RejectsOrdinalsBelowOne(int instanceNumber)
    {
        var source = CreateSource();

        var act = () => DicomSanitizer.Sanitize(source, instanceNumber);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Fingerprint

    [Fact]
    public void Fingerprint_HashesPixelBytesAsLowercaseSha256()
    {
        var source = CreateSource(pixelSeed: 5);

        var fingerprint = DicomSanitizer.Fingerprint(source);

        fingerprint.PixelDataSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(CreatePixels(5))));
        fingerprint.PixelDataSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        fingerprint.GeometrySha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Fingerprint_WorksOnOriginalDataWithoutSyntheticIdentity()
    {
        var source = CreateSource();

        var act = () => DicomSanitizer.Fingerprint(source);

        act.Should().NotThrow();
        Text(source, DicomTag.PatientName).Should().Be(PrimaryIdentity.PatientName);
    }

    [Fact]
    public void Fingerprint_IgnoresDecimalFormatting()
    {
        var plain = CreateSource(customize: d =>
        {
            d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "0.5", "0.5"));
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "10", "-2.5", "100"));
        });
        var formatted = CreateSource(customize: d =>
        {
            d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "0.50", "5e-1"));
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "1e1", "-2.50", "+100.0"));
        });

        DicomSanitizer.Fingerprint(formatted).Should().Be(DicomSanitizer.Fingerprint(plain));
    }

    [Fact]
    public void Fingerprint_NormalizesNegativeZero()
    {
        var positive = CreateSource(customize: d =>
        {
            d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "1", "0", "0", "0", "1", "0"));
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "0", "0", "0"));
        });
        var negative = CreateSource(customize: d =>
        {
            d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "1", "-0", "0", "0", "1", "-0"));
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "-0", "-0", "-0"));
        });

        var fingerprint = DicomSanitizer.Fingerprint(negative);

        fingerprint.Should().Be(DicomSanitizer.Fingerprint(positive));
        BitConverter.DoubleToInt64Bits(fingerprint.SlicePositionMm).Should().Be(0L);
    }

    [Fact]
    public void Fingerprint_GeometryHashChangesWithEachGeometryComponent()
    {
        var baseline = DicomSanitizer.Fingerprint(CreateSource()).GeometrySha256;

        var spacing = DicomSanitizer.Fingerprint(CreateSource(customize: d =>
            d.AddOrUpdate(Ds(DicomTag.PixelSpacing, "0.3125", "0.5")))).GeometrySha256;
        var orientation = DicomSanitizer.Fingerprint(CreateSource(customize: d =>
            d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, "1", "0", "0", "0", "1", "0")))).GeometrySha256;
        var position = DicomSanitizer.Fingerprint(CreateSource(customize: d =>
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, "-12.5", "3.25", "101.75")))).GeometrySha256;

        new[] { baseline, spacing, orientation, position }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Fingerprint_GeometryHashIgnoresMetadataAndPixels()
    {
        var primary = DicomSanitizer.Fingerprint(CreateSource(PrimaryIdentity, pixelSeed: 3));
        var alternate = DicomSanitizer.Fingerprint(CreateSource(AlternateIdentity, pixelSeed: 9));

        alternate.GeometrySha256.Should().Be(primary.GeometrySha256);
        alternate.SlicePositionMm.Should().Be(primary.SlicePositionMm);
        alternate.PixelDataSha256.Should().NotBe(primary.PixelDataSha256);
    }

    [Theory]
    [InlineData("1\\0\\0\\0\\1\\0", "1\\2\\10.5", 10.5)]
    [InlineData("1\\0\\0\\0\\0\\-1", "5\\-7.5\\3", -7.5)]
    [InlineData("1\\0\\0\\0\\0.8660254\\0.5", "3\\4\\5", 2.330127)]
    public void Fingerprint_SlicePositionIsPositionDottedWithSliceNormal(string orientation, string position, double expected)
    {
        var source = CreateSource(customize: d =>
        {
            d.AddOrUpdate(Ds(DicomTag.ImageOrientationPatient, orientation.Split('\\')));
            d.AddOrUpdate(Ds(DicomTag.ImagePositionPatient, position.Split('\\')));
        });

        DicomSanitizer.Fingerprint(source).SlicePositionMm.Should().BeApproximately(expected, 1e-9);
    }

    #endregion

    #region ValidateSanitized

    [Fact]
    public void ValidateSanitized_AcceptsSanitizeOutput()
    {
        var sanitized = DicomSanitizer.Sanitize(CreateSource(), 9);

        var act = () => DicomSanitizer.ValidateSanitized(sanitized, 9);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateSanitized_RejectsOriginalSourceDataset()
    {
        var source = CreateSource();

        AssertRejectedWithoutLeaks(() => DicomSanitizer.ValidateSanitized(source, 1));
    }

    [Fact]
    public void ValidateSanitized_RejectsOtherInstanceNumber()
    {
        var sanitized = DicomSanitizer.Sanitize(CreateSource(), 1);

        AssertRejectedWithoutLeaks(() => DicomSanitizer.ValidateSanitized(sanitized, 2));
    }

    [Theory]
    [MemberData(nameof(InvalidSanitizedCases))]
    public void ValidateSanitized_RejectsTamperedDataset_WithoutLeakingValues(string caseName)
    {
        var sanitized = DicomSanitizer.Sanitize(CreateSource(), 1);
        InvalidSanitizedDatasets[caseName](sanitized);

        AssertRejectedWithoutLeaks(() => DicomSanitizer.ValidateSanitized(sanitized, 1));
    }

    [Fact]
    public void ValidateSanitized_RejectsNonExplicitLittleEndianDataset()
    {
        var tampered = new DicomDataset(DicomTransferSyntax.ImplicitVRLittleEndian);
        tampered.Add(DicomSanitizer.Sanitize(CreateSource(), 1).ToArray());

        AssertRejectedWithoutLeaks(() => DicomSanitizer.ValidateSanitized(tampered, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ValidateSanitized_RejectsOrdinalsBelowOne(int instanceNumber)
    {
        var sanitized = DicomSanitizer.Sanitize(CreateSource(), 1);

        var act = () => DicomSanitizer.ValidateSanitized(sanitized, instanceNumber);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Helpers

    private static InvalidDataException AssertRejectedWithoutLeaks(Action act)
    {
        var exception = act.Should().Throw<InvalidDataException>().Which;

        exception.InnerException.Should().BeNull();
        foreach (var marker in PrimaryIdentity.Markers)
        {
            exception.Message.Should().NotContain(marker);
        }

        return exception;
    }

    private static DicomDataset CreateSource(
        SourceIdentity? identity = null,
        int pixelSeed = 3,
        Action<DicomDataset>? customize = null)
    {
        identity ??= PrimaryIdentity;

        var dataset = new DicomDataset(new DicomItem[]
        {
            Ui(DicomTag.SOPClassUID, DicomUID.CTImageStorage.UID),
            Ui(DicomTag.SOPInstanceUID, identity.SopUid),
            Ui(DicomTag.StudyInstanceUID, identity.StudyUid),
            Ui(DicomTag.SeriesInstanceUID, identity.SeriesUid),
            Ui(DicomTag.FrameOfReferenceUID, identity.FrameUid),
            Cs(DicomTag.ImageType, "ORIGINAL", "PRIMARY", "AXIAL"),
            Cs(DicomTag.Modality, "CT"),
            Pn(DicomTag.PatientName, identity.PatientName),
            Lo(DicomTag.PatientID, identity.PatientId),
            Da(DicomTag.PatientBirthDate, identity.BirthDate),
            Cs(DicomTag.PatientSex, "F"),
            Da(DicomTag.StudyDate, identity.StudyDate),
            Lo(DicomTag.InstitutionName, identity.Institution),
            Lo(DicomTag.StudyDescription, identity.Description),
            Sh(DicomTag.AccessionNumber, identity.Accession),
            Is(DicomTag.InstanceNumber, 99),
            Is(DicomTag.SeriesNumber, 5),
            Is(DicomTag.NumberOfFrames, 1),
            Cs(DicomTag.BurnedInAnnotation, "NO"),
            new DicomSequence(
                DicomTag.ReferencedStudySequence,
                new[] { new DicomDataset(new DicomItem[] { Ui(DicomTag.ReferencedSOPInstanceUID, identity.SequenceUid) }) }),
            Lo(new DicomTag(0x0009, 0x0010), "SOURCE CREATOR"),
            Lo(new DicomTag(0x0009, 0x1001), identity.PrivateValue),

            Us(DicomTag.SamplesPerPixel, 1),
            Cs(DicomTag.PhotometricInterpretation, "MONOCHROME2"),
            Us(DicomTag.Rows, 512),
            Us(DicomTag.Columns, 512),
            Us(DicomTag.BitsAllocated, 16),
            Us(DicomTag.BitsStored, 16),
            Us(DicomTag.HighBit, 15),
            Us(DicomTag.PixelRepresentation, 1),
            Ds(DicomTag.PixelSpacing, "0.3125", "0.3125"),
            Ds(DicomTag.ImageOrientationPatient, "0.8660254", "0.5", "0", "-0.5", "0.8660254", "0"),
            Ds(DicomTag.ImagePositionPatient, "-12.5", "3.25", "100.75"),
            Ds(DicomTag.SliceThickness, "0.625"),
            Ds(DicomTag.SpacingBetweenSlices, "0.5"),
            Ds(DicomTag.RescaleSlope, "1"),
            Ds(DicomTag.RescaleIntercept, "-1024"),
            Lo(DicomTag.RescaleType, "HU"),
            Ds(DicomTag.WindowCenter, "40", "400"),
            Ds(DicomTag.WindowWidth, "400", "1500"),
            Cs(DicomTag.VOILUTFunction, "LINEAR"),
            new DicomSignedShort(DicomTag.PixelPaddingValue, new short[] { -2000 }),
            new DicomSignedShort(DicomTag.PixelPaddingRangeLimit, new short[] { -1500 }),
            PixelData(CreatePixels(pixelSeed)),
        });

        customize?.Invoke(dataset);
        return dataset;
    }

    /// <summary>Deterministic pattern whose first four bytes are the int16 extremes -32768 and 32767.</summary>
    private static byte[] CreatePixels(int seed, int frames = 1)
    {
        var pixels = new byte[PixelByteCount * frames];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(((i * 31) + (seed * 17) + (i >> 8)) & 0xFF);
        }

        pixels[0] = 0x00;
        pixels[1] = 0x80;
        pixels[2] = 0xFF;
        pixels[3] = 0x7F;
        return pixels;
    }

    private static DicomDataset RoundTrip(DicomDataset dataset)
    {
        using var stream = new MemoryStream();
        new DicomFile(dataset).Save(stream);
        stream.Position = 0;
        return DicomFile.Open(stream, FileReadOption.ReadAll, 0).Dataset;
    }

    private static string Text(DicomDataset dataset, DicomTag tag) => dataset.GetSingleValue<string>(tag);

    private static byte[] PixelBytes(DicomDataset dataset) =>
        dataset.GetDicomItem<DicomElement>(DicomTag.PixelData).Buffer.Data;

    private static bool IsEmpty(DicomDataset dataset, DicomTag tag) =>
        dataset.GetDicomItem<DicomElement>(tag).Buffer.Size == 0;

    private static IEnumerable<string> TagNames(IEnumerable<DicomTag> tags) => tags.Select(static tag => tag.ToString());

    private static IEnumerable<string> TagNames(DicomDataset dataset) => TagNames(dataset.Select(static item => item.Tag));

    /// <summary>Tag, VR and a hash of the raw value for every top-level item.</summary>
    private static List<string> Snapshot(DicomDataset dataset) =>
        dataset.Select(static item => item is DicomElement element
                ? $"{item.Tag}|{item.ValueRepresentation.Code}|{Convert.ToHexString(SHA256.HashData(element.Buffer.Data))}"
                : $"{item.Tag}|{item.ValueRepresentation.Code}|non-element")
            .ToList();

    private static DicomUniqueIdentifier Ui(DicomTag tag, string value) => new(tag, new[] { value });

    private static DicomUnsignedShort Us(DicomTag tag, ushort value) => new(tag, new[] { value });

    private static DicomIntegerString Is(DicomTag tag, int value) => new(tag, new[] { value });

    private static DicomCodeString Cs(DicomTag tag, params string[] values) => new(tag, values);

    private static DicomDecimalString Ds(DicomTag tag, params string[] values) => new(tag, values);

    private static DicomLongString Lo(DicomTag tag, string value) => Ascii(new DicomLongString(tag, new[] { value }));

    private static DicomShortString Sh(DicomTag tag, string value) => Ascii(new DicomShortString(tag, new[] { value }));

    private static DicomPersonName Pn(DicomTag tag, string value) => Ascii(new DicomPersonName(tag, new[] { value }));

    private static DicomDate Da(DicomTag tag, string value) => new(tag, new[] { value });

    /// <summary>Elements created in memory have no target encodings, unlike elements produced by a file reader.</summary>
    private static T Ascii<T>(T element) where T : DicomStringElement
    {
        element.TargetEncodings = DicomEncoding.DefaultArray;
        return element;
    }

    private static DicomOtherWord PixelData(byte[] bytes) => new(DicomTag.PixelData, new MemoryByteBuffer(bytes));

    /// <summary>Adds an item that fo-dicom would refuse (wrong value multiplicity, invalid text), as real files can contain.</summary>
    private static void AddWithoutValidation(DicomDataset dataset, DicomItem item)
    {
#pragma warning disable CS0618 // AutoValidate is the only switch for per-dataset validation.
        var previous = dataset.AutoValidate;
        dataset.AutoValidate = false;
        dataset.AddOrUpdate(item);
        dataset.AutoValidate = previous;
#pragma warning restore CS0618
    }

    /// <summary>Pixel buffer whose every read fails with a message that must never surface.</summary>
    private sealed class FailingBuffer(string message) : IByteBuffer
    {
        public bool IsMemory => false;

        public long Size => PixelByteCount;

        public byte[] Data => throw new IOException(message);

        public void GetByteRange(long offset, int count, byte[] output) => throw new IOException(message);

        public void CopyToStream(Stream stream) => throw new IOException(message);

        public Task CopyToStreamAsync(Stream stream, CancellationToken cancellationToken) =>
            throw new IOException(message);
    }

    #endregion
}