namespace DicomViewer.Infrastructure.Dicom;

/// <summary>
/// fo-dicom 기반 DICOM 이미지 서비스 구현
/// fo-dicom based DICOM image service implementation
/// </summary>
public sealed class FoDicomImageService : IDicomImageService
{
    public async Task<PixelData> LoadPixelDataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var dicomFile = await DicomFile.OpenAsync(filePath);
        return ExtractPixelData(dicomFile.Dataset);
    }

    public async Task<PixelData> LoadPixelDataFromStreamAsync(
        Stream dicomStream,
        CancellationToken cancellationToken = default)
    {
        var dicomFile = await DicomFile.OpenAsync(dicomStream);
        return ExtractPixelData(dicomFile.Dataset);
    }

    public WindowLevel GetDefaultWindowLevel(string filePath)
    {
        var dicomFile = DicomFile.Open(filePath);
        var dataset = dicomFile.Dataset;

        var windowWidth = dataset.GetSingleValueOrDefault(DicomTag.WindowWidth, 400.0);
        var windowCenter = dataset.GetSingleValueOrDefault(DicomTag.WindowCenter, 40.0);

        return new WindowLevel(windowWidth, windowCenter);
    }

    public byte[] ApplyWindowLevel(PixelData pixelData, WindowLevel windowLevel)
    {
        var width = pixelData.Width;
        var height = pixelData.Height;
        var output = new byte[width * height];

        var windowMin = windowLevel.WindowCenter - windowLevel.WindowWidth / 2.0;
        var windowMax = windowLevel.WindowCenter + windowLevel.WindowWidth / 2.0;
        var scale = 255.0 / windowLevel.WindowWidth;

        if (pixelData.BitsAllocated == 16)
        {
            ProcessPixels16Bit(pixelData, output, windowMin, windowMax, scale);
        }
        else
        {
            ProcessPixels8Bit(pixelData, output, windowMin, windowMax, scale);
        }

        return output;
    }

    private static PixelData ExtractPixelData(DicomDataset dataset)
    {
        var rows = dataset.GetSingleValueOrDefault(DicomTag.Rows, (ushort)0);
        var columns = dataset.GetSingleValueOrDefault(DicomTag.Columns, (ushort)0);
        var bitsAllocated = dataset.GetSingleValueOrDefault(DicomTag.BitsAllocated, (ushort)16);
        var bitsStored = dataset.GetSingleValueOrDefault(DicomTag.BitsStored, (ushort)12);
        var pixelRepresentation = dataset.GetSingleValueOrDefault(DicomTag.PixelRepresentation, (ushort)0);
        var rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
        var rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

        var pixelData = DicomPixelData.Create(dataset);
        var frameData = pixelData.GetFrame(0);
        var rawPixels = frameData.Data;

        return new PixelData(
            Width: columns,
            Height: rows,
            BitsAllocated: bitsAllocated,
            BitsStored: bitsStored,
            IsSigned: pixelRepresentation == 1,
            RescaleSlope: rescaleSlope,
            RescaleIntercept: rescaleIntercept,
            RawPixels: rawPixels);
    }

    private static void ProcessPixels16Bit(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;

        for (int i = 0; i < pixelCount; i++)
        {
            int rawValue;
            if (pixelData.IsSigned)
            {
                rawValue = BitConverter.ToInt16(rawPixels, i * 2);
            }
            else
            {
                rawValue = BitConverter.ToUInt16(rawPixels, i * 2);
            }

            // Rescale 적용
            // Apply rescale
            var hounsfield = rawValue * slope + intercept;

            // Window/Level 적용
            // Apply Window/Level
            double normalizedValue;
            if (hounsfield <= windowMin)
            {
                normalizedValue = 0;
            }
            else if (hounsfield >= windowMax)
            {
                normalizedValue = 255;
            }
            else
            {
                normalizedValue = (hounsfield - windowMin) * scale;
            }

            output[i] = (byte)Math.Clamp(normalizedValue, 0, 255);
        }
    }

    private static void ProcessPixels8Bit(
        PixelData pixelData,
        byte[] output,
        double windowMin,
        double windowMax,
        double scale)
    {
        var rawPixels = pixelData.RawPixels;
        var slope = pixelData.RescaleSlope;
        var intercept = pixelData.RescaleIntercept;
        var pixelCount = pixelData.PixelCount;

        for (int i = 0; i < pixelCount; i++)
        {
            var rawValue = rawPixels[i];
            var rescaled = rawValue * slope + intercept;

            double normalizedValue;
            if (rescaled <= windowMin)
            {
                normalizedValue = 0;
            }
            else if (rescaled >= windowMax)
            {
                normalizedValue = 255;
            }
            else
            {
                normalizedValue = (rescaled - windowMin) * scale;
            }

            output[i] = (byte)Math.Clamp(normalizedValue, 0, 255);
        }
    }
}
