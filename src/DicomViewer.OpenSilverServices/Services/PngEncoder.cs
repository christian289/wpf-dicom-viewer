using System.IO.Compression;

namespace DicomViewer.OpenSilverServices.Services;

/// <summary>
/// 순수 C# PNG 인코더 (외부 의존성 없음)
/// Pure C# PNG encoder (no external dependencies)
/// </summary>
public static class PngEncoder
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// 8비트 그레이스케일 데이터를 PNG로 인코딩
    /// Encode 8-bit grayscale data to PNG
    /// </summary>
    public static byte[] EncodeGrayscale(byte[] pixels, int width, int height)
    {
        using var output = new MemoryStream();

        // PNG 시그니처 작성
        // Write PNG signature
        output.Write(PngSignature, 0, PngSignature.Length);

        // IHDR 청크 작성
        // Write IHDR chunk
        WriteIhdrChunk(output, width, height);

        // IDAT 청크 작성 (픽셀 데이터)
        // Write IDAT chunk (pixel data)
        WriteIdatChunk(output, pixels, width, height);

        // IEND 청크 작성
        // Write IEND chunk
        WriteIendChunk(output);

        return output.ToArray();
    }

    private static void WriteIhdrChunk(Stream output, int width, int height)
    {
        using var chunkData = new MemoryStream();
        using var writer = new BinaryWriter(chunkData);

        // Width (4 bytes, big-endian)
        writer.Write(ToBigEndian(width));
        // Height (4 bytes, big-endian)
        writer.Write(ToBigEndian(height));
        // Bit depth (1 byte) - 8 for grayscale
        writer.Write((byte)8);
        // Color type (1 byte) - 0 for grayscale
        writer.Write((byte)0);
        // Compression method (1 byte) - 0 for deflate
        writer.Write((byte)0);
        // Filter method (1 byte) - 0 for adaptive
        writer.Write((byte)0);
        // Interlace method (1 byte) - 0 for no interlace
        writer.Write((byte)0);

        WriteChunk(output, "IHDR", chunkData.ToArray());
    }

    private static void WriteIdatChunk(Stream output, byte[] pixels, int width, int height)
    {
        // 필터링된 스캔라인 생성 (각 라인 앞에 필터 타입 0 추가)
        // Create filtered scanlines (add filter type 0 before each line)
        var rawData = new byte[height * (width + 1)];
        var srcIndex = 0;
        var dstIndex = 0;

        for (int y = 0; y < height; y++)
        {
            // 필터 타입 0 (None)
            // Filter type 0 (None)
            rawData[dstIndex++] = 0;

            // 스캔라인 복사
            // Copy scanline
            Array.Copy(pixels, srcIndex, rawData, dstIndex, width);
            srcIndex += width;
            dstIndex += width;
        }

        // Deflate 압축
        // Deflate compression
        using var compressedStream = new MemoryStream();

        // zlib 헤더 (CMF, FLG)
        // zlib header (CMF, FLG)
        compressedStream.WriteByte(0x78); // CMF: deflate, 32K window
        compressedStream.WriteByte(0x9C); // FLG: default compression

        using (var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflateStream.Write(rawData, 0, rawData.Length);
        }

        // Adler-32 체크섬 추가
        // Add Adler-32 checksum
        var adler = ComputeAdler32(rawData);
        compressedStream.Write(ToBigEndian((int)adler), 0, 4);

        WriteChunk(output, "IDAT", compressedStream.ToArray());
    }

    private static void WriteIendChunk(Stream output)
    {
        WriteChunk(output, "IEND", Array.Empty<byte>());
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);

        // Length (4 bytes, big-endian)
        output.Write(ToBigEndian(data.Length), 0, 4);

        // Type (4 bytes)
        output.Write(typeBytes, 0, 4);

        // Data
        output.Write(data, 0, data.Length);

        // CRC (4 bytes)
        var crcData = new byte[4 + data.Length];
        Array.Copy(typeBytes, 0, crcData, 0, 4);
        Array.Copy(data, 0, crcData, 4, data.Length);
        var crc = ComputeCrc32(crcData);
        output.Write(ToBigEndian((int)crc), 0, 4);
    }

    private static byte[] ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return bytes;
    }

    private static uint ComputeAdler32(byte[] data)
    {
        const uint MOD_ADLER = 65521;
        uint a = 1, b = 0;

        foreach (byte by in data)
        {
            a = (a + by) % MOD_ADLER;
            b = (b + a) % MOD_ADLER;
        }

        return (b << 16) | a;
    }

    private static uint ComputeCrc32(byte[] data)
    {
        // CRC-32 테이블 (PNG 표준)
        // CRC-32 table (PNG standard)
        uint[] crcTable = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                if ((c & 1) != 0)
                    c = 0xEDB88320 ^ (c >> 1);
                else
                    c >>= 1;
            }
            crcTable[n] = c;
        }

        uint crc = 0xFFFFFFFF;
        foreach (byte by in data)
        {
            crc = crcTable[(crc ^ by) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }
}
