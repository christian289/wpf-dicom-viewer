using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using FellowOakDicom;
using FellowOakDicom.IO.Buffer;

namespace DicomViewer.DatasetTool;

/// <summary>
/// Rebuilds a supported public CT slice as a new DICOM dataset from an allowlist of image attributes.
/// Nothing but the allowlisted image tags is copied; identity, UIDs and CT structure are generated.
/// Error messages are fixed strings and never contain source values.
/// </summary>
public static class DicomSanitizer
{
    private const int ImageSize = 512;
    private const int PixelDataLength = ImageSize * ImageSize * sizeof(short);
    private const int MaxDecimalStringLength = 16;
    private const double GeometryTolerance = 1e-3;
    private const string SyntheticPatientName = "Synthetic^Public";
    private const string SyntheticPatientId = DatasetDefinition.Id;
    private const string ParseFailure = "The DICOM dataset could not be parsed safely.";
    private const string OutsideAllowlist = "The sanitized DICOM dataset contains an attribute outside the allowlist.";
    private const string MissingAttribute = "The sanitized DICOM dataset is missing a required attribute.";
    private const string GeneratedMismatch = "The sanitized DICOM dataset contains unexpected generated metadata.";

    private static readonly string[] ImageTypeValues = ["DERIVED", "SECONDARY"];
    private static readonly string[] RescaleTypes = ["HU", "US", "OD"];
    private static readonly string[] VoiLutFunctions = ["LINEAR", "SIGMOID", "LINEAR_EXACT"];

    private static readonly Regex DecimalStringPattern = new(
        @"\A[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z",
        RegexOptions.CultureInvariant);

    private static readonly string StudyInstanceUid = DeriveUid($"{DatasetDefinition.Id}/study");
    private static readonly string SeriesInstanceUid = DeriveUid($"{DatasetDefinition.Id}/series");
    private static readonly string FrameOfReferenceUid = DeriveUid($"{DatasetDefinition.Id}/frame-of-reference");

    /// <summary>Every top-level tag a sanitized dataset may contain, with its exact VR code.</summary>
    private static readonly Dictionary<DicomTag, string> AttributeVrs = new()
    {
        // Generated: SOP common, patient, study, series, frame of reference, equipment, general image.
        [DicomTag.SOPClassUID] = "UI",
        [DicomTag.SOPInstanceUID] = "UI",
        [DicomTag.ImageType] = "CS",
        [DicomTag.StudyDate] = "DA",
        [DicomTag.StudyTime] = "TM",
        [DicomTag.AccessionNumber] = "SH",
        [DicomTag.Modality] = "CS",
        [DicomTag.Manufacturer] = "LO",
        [DicomTag.ReferringPhysicianName] = "PN",
        [DicomTag.PatientName] = "PN",
        [DicomTag.PatientID] = "LO",
        [DicomTag.PatientBirthDate] = "DA",
        [DicomTag.PatientSex] = "CS",
        [DicomTag.KVP] = "DS",
        [DicomTag.StudyInstanceUID] = "UI",
        [DicomTag.SeriesInstanceUID] = "UI",
        [DicomTag.StudyID] = "SH",
        [DicomTag.SeriesNumber] = "IS",
        [DicomTag.AcquisitionNumber] = "IS",
        [DicomTag.InstanceNumber] = "IS",
        [DicomTag.FrameOfReferenceUID] = "UI",
        [DicomTag.PositionReferenceIndicator] = "LO",

        // Copied from the source (SliceThickness is type 2: copied when present, otherwise empty).
        [DicomTag.SamplesPerPixel] = "US",
        [DicomTag.PhotometricInterpretation] = "CS",
        [DicomTag.Rows] = "US",
        [DicomTag.Columns] = "US",
        [DicomTag.BitsAllocated] = "US",
        [DicomTag.BitsStored] = "US",
        [DicomTag.HighBit] = "US",
        [DicomTag.PixelRepresentation] = "US",
        [DicomTag.PixelSpacing] = "DS",
        [DicomTag.ImageOrientationPatient] = "DS",
        [DicomTag.ImagePositionPatient] = "DS",
        [DicomTag.SliceThickness] = "DS",
        [DicomTag.PixelData] = "OW",

        // Copied only when present in the source.
        [DicomTag.SpacingBetweenSlices] = "DS",
        [DicomTag.RescaleSlope] = "DS",
        [DicomTag.RescaleIntercept] = "DS",
        [DicomTag.RescaleType] = "LO",
        [DicomTag.WindowCenter] = "DS",
        [DicomTag.WindowWidth] = "DS",
        [DicomTag.VOILUTFunction] = "CS",
        [DicomTag.PixelPaddingValue] = "SS",
        [DicomTag.PixelPaddingRangeLimit] = "SS",
    };

    private static readonly HashSet<DicomTag> OptionalAttributes =
    [
        DicomTag.SpacingBetweenSlices,
        DicomTag.RescaleSlope,
        DicomTag.RescaleIntercept,
        DicomTag.RescaleType,
        DicomTag.WindowCenter,
        DicomTag.WindowWidth,
        DicomTag.VOILUTFunction,
        DicomTag.PixelPaddingValue,
        DicomTag.PixelPaddingRangeLimit,
    ];

    /// <summary>Generated type-2 attributes that are always written with an empty value.</summary>
    private static readonly DicomTag[] EmptyAttributes =
    [
        DicomTag.StudyDate,
        DicomTag.StudyTime,
        DicomTag.AccessionNumber,
        DicomTag.Manufacturer,
        DicomTag.ReferringPhysicianName,
        DicomTag.PatientBirthDate,
        DicomTag.PatientSex,
        DicomTag.KVP,
        DicomTag.StudyID,
        DicomTag.SeriesNumber,
        DicomTag.AcquisitionNumber,
        DicomTag.PositionReferenceIndicator,
    ];

    /// <summary>
    /// Builds a new dataset containing only allowlisted image attributes copied from <paramref name="source"/>
    /// plus generated CT structure. The source is not modified; the PixelData bytes are shared, not copied.
    /// </summary>
    /// <exception cref="InvalidDataException">The source is unsupported or unsafe.</exception>
    public static DicomDataset Sanitize(DicomDataset source, int instanceNumber)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireInstanceNumber(instanceNumber);

        return Guard(() => Build(ReadRequired(source), ReadOptional(source), instanceNumber));
    }

    /// <summary>
    /// SHA-256 of the PixelData bytes, SHA-256 of the normalized geometry JSON and the slice position
    /// (ImagePositionPatient dot cross(row, column)). Works on original and sanitized datasets alike.
    /// </summary>
    /// <exception cref="InvalidDataException">The dataset is not a supported image.</exception>
    public static ImageFingerprint Fingerprint(DicomDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        return Guard(() =>
        {
            var image = ReadRequired(dataset);
            return new ImageFingerprint(Hex(SHA256.HashData(image.Pixels)), GeometryHash(image), SlicePosition(image));
        });
    }

    /// <summary>
    /// Verifies that <paramref name="dataset"/> contains exactly the allowlisted copied and generated
    /// attributes, with the fixed synthetic metadata and deterministic UIDs for <paramref name="instanceNumber"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The dataset is not a valid sanitized dataset.</exception>
    public static void ValidateSanitized(DicomDataset dataset, int instanceNumber)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        RequireInstanceNumber(instanceNumber);

        Guard(() =>
        {
            foreach (var item in dataset)
            {
                if (item is not DicomElement element
                    || !AttributeVrs.TryGetValue(item.Tag, out var vr)
                    || element.ValueRepresentation.Code != vr)
                {
                    throw new InvalidDataException(OutsideAllowlist);
                }
            }

            foreach (var tag in AttributeVrs.Keys)
            {
                if (!OptionalAttributes.Contains(tag) && !dataset.Contains(tag))
                {
                    throw new InvalidDataException(MissingAttribute);
                }
            }

            RequireGeneratedValues(dataset, instanceNumber);
            ReadRequired(dataset);
            ReadOptional(dataset);
        });
    }

    #region Source parsing

    private sealed record Decimals(string[] Text, double[] Values);

    private sealed record RequiredImage(byte[] Pixels, Decimals Spacing, Decimals Orientation, Decimals Position);

    private sealed record OptionalImage(
        Decimals? SliceThickness,
        Decimals? SpacingBetweenSlices,
        Decimals? RescaleSlope,
        Decimals? RescaleIntercept,
        string? RescaleType,
        Decimals? WindowCenter,
        Decimals? WindowWidth,
        string? VoiLutFunction,
        short? PixelPaddingValue,
        short? PixelPaddingRangeLimit);

    private static RequiredImage ReadRequired(DicomDataset dataset)
    {
        if (dataset.InternalTransferSyntax.UID.UID != DicomUID.ExplicitVRLittleEndian.UID)
        {
            throw Invalid("TransferSyntax");
        }

        if (ReadSingleText(dataset, DicomTag.SOPClassUID, "UI", "SOPClassUID") != DicomUID.CTImageStorage.UID)
        {
            throw Invalid("SOPClassUID");
        }

        var frames = Find(dataset, DicomTag.NumberOfFrames, "IS", "NumberOfFrames");
        if (frames is not null)
        {
            var frameCount = Texts(frames);
            if (frameCount.Length != 1
                || !int.TryParse(frameCount[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count)
                || count != 1)
            {
                throw Invalid("NumberOfFrames");
            }
        }

        var burnedIn = Find(dataset, DicomTag.BurnedInAnnotation, "CS", "BurnedInAnnotation");
        if (burnedIn is not null && Texts(burnedIn) is not ["NO"])
        {
            throw Invalid("BurnedInAnnotation");
        }

        // Sequences are never copied, so a source that relies on them for pixel interpretation is unsupported.
        if (dataset.Contains(DicomTag.ModalityLUTSequence) || dataset.Contains(DicomTag.VOILUTSequence))
        {
            throw Invalid("LUTSequence");
        }

        RequireUnsignedShort(dataset, DicomTag.SamplesPerPixel, 1, "SamplesPerPixel");
        RequireUnsignedShort(dataset, DicomTag.Rows, ImageSize, "Rows");
        RequireUnsignedShort(dataset, DicomTag.Columns, ImageSize, "Columns");
        RequireUnsignedShort(dataset, DicomTag.BitsAllocated, 16, "BitsAllocated");
        RequireUnsignedShort(dataset, DicomTag.BitsStored, 16, "BitsStored");
        RequireUnsignedShort(dataset, DicomTag.HighBit, 15, "HighBit");
        RequireUnsignedShort(dataset, DicomTag.PixelRepresentation, 1, "PixelRepresentation");

        if (ReadSingleText(dataset, DicomTag.PhotometricInterpretation, "CS", "PhotometricInterpretation") != "MONOCHROME2")
        {
            throw Invalid("PhotometricInterpretation");
        }

        var spacing = ReadDecimals(dataset, DicomTag.PixelSpacing, 2, "PixelSpacing") ?? throw Invalid("PixelSpacing");
        if (spacing.Values.Any(static value => value <= 0))
        {
            throw Invalid("PixelSpacing");
        }

        var orientation = ReadDecimals(dataset, DicomTag.ImageOrientationPatient, 6, "ImageOrientationPatient")
            ?? throw Invalid("ImageOrientationPatient");
        if (!IsProperOrientation(orientation.Values))
        {
            throw Invalid("ImageOrientationPatient");
        }

        var position = ReadDecimals(dataset, DicomTag.ImagePositionPatient, 3, "ImagePositionPatient")
            ?? throw Invalid("ImagePositionPatient");

        // Only native OW pixel data qualifies: this excludes encapsulated (fragmented) and OB pixel data.
        if (dataset.GetDicomItem<DicomItem>(DicomTag.PixelData) is not DicomOtherWord pixelData)
        {
            throw Invalid("PixelData");
        }

        var pixels = pixelData.Buffer.Data;
        if (pixels.Length != PixelDataLength)
        {
            throw Invalid("PixelData");
        }

        return new RequiredImage(pixels, spacing, orientation, position);
    }

    private static OptionalImage ReadOptional(DicomDataset dataset)
    {
        var windowCenter = ReadDecimals(dataset, DicomTag.WindowCenter, 0, "WindowCenter");
        var windowWidth = ReadDecimals(dataset, DicomTag.WindowWidth, 0, "WindowWidth");
        if ((windowCenter is null) != (windowWidth is null)
            || (windowCenter is not null
                && (windowCenter.Values.Length != windowWidth!.Values.Length
                    || windowWidth.Values.Any(static value => value <= 0))))
        {
            throw Invalid("WindowCenterWidth");
        }

        var paddingValue = ReadSignedShort(dataset, DicomTag.PixelPaddingValue, "PixelPaddingValue");
        var paddingLimit = ReadSignedShort(dataset, DicomTag.PixelPaddingRangeLimit, "PixelPaddingRangeLimit");
        if (paddingLimit is not null && paddingValue is null)
        {
            throw Invalid("PixelPaddingRangeLimit");
        }

        var rescaleSlope = ReadDecimals(dataset, DicomTag.RescaleSlope, 1, "RescaleSlope");
        if (rescaleSlope?.Values[0] == 0)
        {
            throw Invalid("RescaleSlope");
        }

        return new OptionalImage(
            ReadDecimals(dataset, DicomTag.SliceThickness, 1, "SliceThickness", allowEmpty: true),
            ReadDecimals(dataset, DicomTag.SpacingBetweenSlices, 1, "SpacingBetweenSlices"),
            rescaleSlope,
            ReadDecimals(dataset, DicomTag.RescaleIntercept, 1, "RescaleIntercept"),
            ReadCode(dataset, DicomTag.RescaleType, "LO", "RescaleType", RescaleTypes),
            windowCenter,
            windowWidth,
            ReadCode(dataset, DicomTag.VOILUTFunction, "CS", "VOILUTFunction", VoiLutFunctions),
            paddingValue,
            paddingLimit);
    }

    private static DicomElement? Find(DicomDataset dataset, DicomTag tag, string vr, string attribute)
    {
        var item = dataset.GetDicomItem<DicomItem>(tag);
        if (item is null)
        {
            return null;
        }

        if (item is not DicomElement element || element.ValueRepresentation.Code != vr)
        {
            throw Invalid(attribute);
        }

        return element;
    }

    /// <summary>String values decoded from the raw element bytes, without padding; empty array for an empty element.</summary>
    private static string[] Texts(DicomElement element)
    {
        var data = element.Buffer.Data;
        if (data.Length == 0)
        {
            return [];
        }

        return Encoding.ASCII.GetString(data).Split('\\').Select(static value => value.Trim(' ', '\0')).ToArray();
    }

    private static string? ReadSingleText(DicomDataset dataset, DicomTag tag, string vr, string attribute)
    {
        var element = Find(dataset, tag, vr, attribute);
        if (element is null)
        {
            return null;
        }

        var texts = Texts(element);
        return texts.Length == 1 ? texts[0] : throw Invalid(attribute);
    }

    private static string? ReadCode(DicomDataset dataset, DicomTag tag, string vr, string attribute, string[] allowed)
    {
        var element = Find(dataset, tag, vr, attribute);
        if (element is null)
        {
            return null;
        }

        var texts = Texts(element);

        return texts.Length == 1 && allowed.Contains(texts[0]) ? texts[0] : throw Invalid(attribute);
    }

    private static void RequireUnsignedShort(DicomDataset dataset, DicomTag tag, ushort expected, string attribute)
    {
        var element = Find(dataset, tag, "US", attribute);
        if (element is null || element.Count != 1 || element.Get<ushort>(0) != expected)
        {
            throw Invalid(attribute);
        }
    }

    private static short? ReadSignedShort(DicomDataset dataset, DicomTag tag, string attribute)
    {
        var element = Find(dataset, tag, "SS", attribute);
        if (element is null)
        {
            return null;
        }

        return element.Count == 1 ? element.Get<short>(0) : throw Invalid(attribute);
    }

    /// <summary>
    /// Reads finite DS values keeping their original text. Returns null when the element is absent, or empty and
    /// <paramref name="allowEmpty"/> is set; any other empty value is rejected rather than dropped.
    /// <paramref name="expectedCount"/> of 0 accepts any value multiplicity.
    /// </summary>
    private static Decimals? ReadDecimals(DicomDataset dataset, DicomTag tag, int expectedCount, string attribute, bool allowEmpty = false)
    {
        var element = Find(dataset, tag, "DS", attribute);
        if (element is null)
        {
            return null;
        }

        var texts = Texts(element);
        if (texts.Length == 0)
        {
            return allowEmpty ? null : throw Invalid(attribute);
        }

        if (expectedCount > 0 && texts.Length != expectedCount)
        {
            throw Invalid(attribute);
        }

        var values = new double[texts.Length];
        for (var i = 0; i < texts.Length; i++)
        {
            if (texts[i].Length > MaxDecimalStringLength
                || !DecimalStringPattern.IsMatch(texts[i])
                || !double.TryParse(texts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])
                || !double.IsFinite(values[i]))
            {
                throw Invalid(attribute);
            }
        }

        return new Decimals(texts, values);
    }

    /// <summary>Row and column direction cosines must be unit length and orthogonal.</summary>
    private static bool IsProperOrientation(double[] cosines)
    {
        var rowLength = Math.Sqrt((cosines[0] * cosines[0]) + (cosines[1] * cosines[1]) + (cosines[2] * cosines[2]));
        var columnLength = Math.Sqrt((cosines[3] * cosines[3]) + (cosines[4] * cosines[4]) + (cosines[5] * cosines[5]));
        var dot = (cosines[0] * cosines[3]) + (cosines[1] * cosines[4]) + (cosines[2] * cosines[5]);

        return Math.Abs(rowLength - 1) <= GeometryTolerance
            && Math.Abs(columnLength - 1) <= GeometryTolerance
            && Math.Abs(dot) <= GeometryTolerance;
    }

    #endregion

    #region Generated dataset

    private static DicomDataset Build(RequiredImage image, OptionalImage optional, int instanceNumber)
    {
        var items = new List<DicomItem>
        {
            new DicomUniqueIdentifier(DicomTag.SOPClassUID, new[] { DicomUID.CTImageStorage.UID }),
            new DicomUniqueIdentifier(DicomTag.SOPInstanceUID, new[] { SopInstanceUid(instanceNumber) }),
            new DicomUniqueIdentifier(DicomTag.StudyInstanceUID, new[] { StudyInstanceUid }),
            new DicomUniqueIdentifier(DicomTag.SeriesInstanceUID, new[] { SeriesInstanceUid }),
            new DicomUniqueIdentifier(DicomTag.FrameOfReferenceUID, new[] { FrameOfReferenceUid }),
            new DicomCodeString(DicomTag.ImageType, ImageTypeValues),
            new DicomCodeString(DicomTag.Modality, ["CT"]),
            new DicomPersonName(DicomTag.PatientName, [SyntheticPatientName]),
            new DicomLongString(DicomTag.PatientID, [SyntheticPatientId]),
            new DicomIntegerString(DicomTag.InstanceNumber, [instanceNumber]),

            new DicomUnsignedShort(DicomTag.SamplesPerPixel, [1]),
            new DicomCodeString(DicomTag.PhotometricInterpretation, ["MONOCHROME2"]),
            new DicomUnsignedShort(DicomTag.Rows, [ImageSize]),
            new DicomUnsignedShort(DicomTag.Columns, [ImageSize]),
            new DicomUnsignedShort(DicomTag.BitsAllocated, [16]),
            new DicomUnsignedShort(DicomTag.BitsStored, [16]),
            new DicomUnsignedShort(DicomTag.HighBit, [15]),
            new DicomUnsignedShort(DicomTag.PixelRepresentation, [1]),
            new DicomDecimalString(DicomTag.PixelSpacing, image.Spacing.Text),
            new DicomDecimalString(DicomTag.ImageOrientationPatient, image.Orientation.Text),
            new DicomDecimalString(DicomTag.ImagePositionPatient, image.Position.Text),
            new DicomOtherWord(DicomTag.PixelData, new MemoryByteBuffer(image.Pixels)),
        };

        foreach (var tag in EmptyAttributes)
        {
            items.Add(CreateEmpty(tag));
        }

        items.Add(optional.SliceThickness is { } thickness
            ? new DicomDecimalString(DicomTag.SliceThickness, thickness.Text)
            : CreateEmpty(DicomTag.SliceThickness));

        AddDecimals(items, DicomTag.SpacingBetweenSlices, optional.SpacingBetweenSlices);
        AddDecimals(items, DicomTag.RescaleSlope, optional.RescaleSlope);
        AddDecimals(items, DicomTag.RescaleIntercept, optional.RescaleIntercept);
        AddDecimals(items, DicomTag.WindowCenter, optional.WindowCenter);
        AddDecimals(items, DicomTag.WindowWidth, optional.WindowWidth);

        if (optional.RescaleType is not null)
        {
            items.Add(new DicomLongString(DicomTag.RescaleType, [optional.RescaleType]));
        }

        if (optional.VoiLutFunction is not null)
        {
            items.Add(new DicomCodeString(DicomTag.VOILUTFunction, [optional.VoiLutFunction]));
        }

        if (optional.PixelPaddingValue is { } paddingValue)
        {
            items.Add(new DicomSignedShort(DicomTag.PixelPaddingValue, [paddingValue]));
        }

        if (optional.PixelPaddingRangeLimit is { } paddingLimit)
        {
            items.Add(new DicomSignedShort(DicomTag.PixelPaddingRangeLimit, [paddingLimit]));
        }

        // Elements created in memory have no target encodings until a writer assigns them, which makes their
        // buffers unreadable (and the dataset unvalidatable) before saving; ASCII is the DICOM default.
        foreach (var element in items.OfType<DicomStringElement>())
        {
            element.TargetEncodings = DicomEncoding.DefaultArray;
        }

        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian);
        dataset.Add(items);
        return dataset;
    }

    private static void AddDecimals(List<DicomItem> items, DicomTag tag, Decimals? decimals)
    {
        if (decimals is not null)
        {
            items.Add(new DicomDecimalString(tag, decimals.Text));
        }
    }

    private static DicomElement CreateEmpty(DicomTag tag)
    {
        string[] none = [string.Empty];
        return AttributeVrs[tag] switch
        {
            "DA" => new DicomDate(tag, none),
            "TM" => new DicomTime(tag, none),
            "SH" => new DicomShortString(tag, none),
            "LO" => new DicomLongString(tag, none),
            "PN" => new DicomPersonName(tag, none),
            "CS" => new DicomCodeString(tag, none),
            "DS" => new DicomDecimalString(tag, none),
            "IS" => new DicomIntegerString(tag, none),
            _ => throw new InvalidOperationException("No empty element factory for the attribute."),
        };
    }

    private static void RequireGeneratedValues(DicomDataset dataset, int instanceNumber)
    {
        RequireValues(dataset, DicomTag.SOPClassUID, DicomUID.CTImageStorage.UID);
        RequireValues(dataset, DicomTag.SOPInstanceUID, SopInstanceUid(instanceNumber));
        RequireValues(dataset, DicomTag.StudyInstanceUID, StudyInstanceUid);
        RequireValues(dataset, DicomTag.SeriesInstanceUID, SeriesInstanceUid);
        RequireValues(dataset, DicomTag.FrameOfReferenceUID, FrameOfReferenceUid);
        RequireValues(dataset, DicomTag.ImageType, ImageTypeValues);
        RequireValues(dataset, DicomTag.Modality, "CT");
        RequireValues(dataset, DicomTag.PatientName, SyntheticPatientName);
        RequireValues(dataset, DicomTag.PatientID, SyntheticPatientId);
        RequireValues(dataset, DicomTag.InstanceNumber, instanceNumber.ToString(CultureInfo.InvariantCulture));

        foreach (var tag in EmptyAttributes)
        {
            if (Texts(dataset.GetDicomItem<DicomElement>(tag)).Length != 0)
            {
                throw new InvalidDataException(GeneratedMismatch);
            }
        }
    }

    private static void RequireValues(DicomDataset dataset, DicomTag tag, params string[] expected)
    {
        if (!Texts(dataset.GetDicomItem<DicomElement>(tag)).SequenceEqual(expected))
        {
            throw new InvalidDataException(GeneratedMismatch);
        }
    }

    private static string SopInstanceUid(int instanceNumber) =>
        DeriveUid($"{DatasetDefinition.Id}/sop/{instanceNumber.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>
    /// UUID-shaped (RFC 9562 version 8) 2.25 UID derived only from the dataset key, never from source identifiers.
    /// </summary>
    private static string DeriveUid(string key)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(key), hash);

        var uuid = hash[..16];
        uuid[6] = (byte)((uuid[6] & 0x0F) | 0x80);
        uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);

        return "2.25." + new BigInteger(uuid, isUnsigned: true, isBigEndian: true).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region Fingerprint

    private static string GeometryHash(RequiredImage image)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("rows", ImageSize);
            writer.WriteNumber("columns", ImageSize);
            WriteValues(writer, "pixelSpacing", image.Spacing.Values);
            WriteValues(writer, "imageOrientationPatient", image.Orientation.Values);
            WriteValues(writer, "imagePositionPatient", image.Position.Values);
            writer.WriteEndObject();
        }

        return Hex(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteValues(Utf8JsonWriter writer, string name, double[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteNumberValue(NormalizeZero(value));
        }

        writer.WriteEndArray();
    }

    /// <summary>Position dotted with the slice normal, cross(row, column).</summary>
    private static double SlicePosition(RequiredImage image)
    {
        var cosines = image.Orientation.Values;
        var position = image.Position.Values;

        var normalX = (cosines[1] * cosines[5]) - (cosines[2] * cosines[4]);
        var normalY = (cosines[2] * cosines[3]) - (cosines[0] * cosines[5]);
        var normalZ = (cosines[0] * cosines[4]) - (cosines[1] * cosines[3]);

        return NormalizeZero((position[0] * normalX) + (position[1] * normalY) + (position[2] * normalZ));
    }

    private static double NormalizeZero(double value) => value == 0 ? 0 : value;

    private static string Hex(byte[] hash) => Convert.ToHexStringLower(hash);

    #endregion

    #region Errors

    private static void RequireInstanceNumber(int instanceNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instanceNumber, 1);
    }

    private static InvalidDataException Invalid(string attribute) =>
        new($"Unsupported or invalid DICOM attribute: {attribute}.");

    /// <summary>
    /// fo-dicom and BCL parse errors can quote element values, so anything that is not already a fixed
    /// <see cref="InvalidDataException"/> is replaced by a fixed message without an inner exception.
    /// </summary>
    private static T Guard<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException(ParseFailure);
        }
    }

    private static void Guard(Action action) =>
        Guard<object?>(() =>
        {
            action();
            return null;
        });

    #endregion
}