namespace DicomViewer.OpenSilverServices.Models;

/// <summary>
/// Window/Level 값 (DICOM 이미지 밝기/대비 조절)
/// Window/Level values (DICOM image brightness/contrast adjustment)
/// </summary>
public sealed class WindowLevel
{
    public double WindowWidth { get; }
    public double WindowCenter { get; }

    public WindowLevel(double windowWidth, double windowCenter)
    {
        WindowWidth = windowWidth;
        WindowCenter = windowCenter;
    }

    // 프리셋 값들
    // Preset values
    public static WindowLevel CtAbdomen { get; } = new(400, 40);
    public static WindowLevel CtLung { get; } = new(1500, -600);
    public static WindowLevel CtBone { get; } = new(2000, 500);
    public static WindowLevel CtBrain { get; } = new(80, 40);
    public static WindowLevel CtLiver { get; } = new(150, 30);
    public static WindowLevel CtMediastinum { get; } = new(350, 50);
    public static WindowLevel MrDefault { get; } = new(800, 400);
    public static WindowLevel MrT1 { get; } = new(600, 300);
    public static WindowLevel MrT2 { get; } = new(1000, 500);
    public static WindowLevel Default { get; } = new(400, 40);
}
