namespace DicomViewer.Domain.ValueObjects;

/// <summary>
/// Window/Level 설정을 나타내는 불변 레코드
/// Immutable record representing Window/Level settings
/// </summary>
public sealed record WindowLevel(double WindowWidth, double WindowCenter)
{
    // CT 프리셋
    // CT presets
    public static WindowLevel CtAbdomen => new(400, 40);
    public static WindowLevel CtLung => new(1500, -600);
    public static WindowLevel CtBone => new(2000, 500);
    public static WindowLevel CtBrain => new(80, 40);
    public static WindowLevel CtLiver => new(150, 30);
    public static WindowLevel CtMediastinum => new(350, 50);

    // MR 프리셋
    // MR presets
    public static WindowLevel MrDefault => new(800, 400);
    public static WindowLevel MrT1 => new(600, 300);
    public static WindowLevel MrT2 => new(1000, 500);

    // 기본값
    // Default
    public static WindowLevel Default => new(400, 40);
}
