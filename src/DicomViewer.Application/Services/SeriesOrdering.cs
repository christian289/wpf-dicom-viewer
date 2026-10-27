namespace DicomViewer.Application.Services;

/// <summary>
/// 시리즈 인스턴스의 공간 정렬과 기하 상태 판정 (순수 계산)
/// Spatial ordering and geometry status evaluation for series instances (pure computation)
/// </summary>
/// <remarks>
/// Spatial: 법선 n = row × column 에 대한 투영 dot(IPP, n) 오름차순.
/// 그 밖의 경우: InstanceNumber → SopInstanceUid → FilePath의 결정적 순서이며 물리 위치가 아니다.
/// </remarks>
internal static class SeriesOrdering
{
    // IOP 단위 벡터·직교성·동일성 허용 오차 / Tolerance for unit length, orthogonality, and equality of IOP
    private const double OrientationTolerance = 1e-3;

    // 위치 투영 중복 판정 허용 오차 (mm) / Tolerance for duplicate position projections (mm)
    private const double PositionTolerance = 1e-3;

    internal sealed record Result(SeriesGeometryStatus Status, IReadOnlyList<DicomInstanceDto> Instances);

    public static Result Order(IReadOnlyList<DicomInstanceDto> instances)
    {
        if (instances.Count == 0)
        {
            return new Result(SeriesGeometryStatus.Empty, []);
        }

        var identity = EvaluateIdentity(instances);
        if (identity is not null)
        {
            return new Result(identity.Value, FallbackOrder(instances));
        }

        if (instances.Any(i => i.ImagePositionPatient is null || i.ImageOrientationPatient is null))
        {
            return new Result(SeriesGeometryStatus.MissingGeometry, FallbackOrder(instances));
        }

        if (!TryOrderSpatially(instances, out var sorted))
        {
            return new Result(SeriesGeometryStatus.AmbiguousGeometry, FallbackOrder(instances));
        }

        return new Result(SeriesGeometryStatus.Spatial, sorted);
    }

    /// <summary>
    /// 시리즈·프레임 식별자 판정. 모두 없으면 MissingGeometry, 일부만 있거나 값이 다르면 Ambiguous, 모두 같으면 null(다음 단계 진행)
    /// Series/frame identity check. All absent => MissingGeometry; partly absent or differing => Ambiguous; all equal => null (continue)
    /// </summary>
    private static SeriesGeometryStatus? EvaluateIdentity(IReadOnlyList<DicomInstanceDto> instances)
    {
        var seriesStatus = EvaluateReferenceIdentity(instances.Select(i => i.SeriesInstanceUid).ToList());
        if (seriesStatus is not null)
        {
            return seriesStatus;
        }

        return EvaluateReferenceIdentity(instances.Select(i => i.FrameOfReferenceUid).ToList());
    }

    private static SeriesGeometryStatus? EvaluateReferenceIdentity(IReadOnlyList<string?> values)
    {
        var absent = values.Count(v => string.IsNullOrWhiteSpace(v));
        if (absent == values.Count)
        {
            return SeriesGeometryStatus.MissingGeometry;
        }

        if (absent > 0)
        {
            return SeriesGeometryStatus.AmbiguousGeometry;
        }

        var distinct = values.Distinct(StringComparer.Ordinal).Count();
        return distinct > 1 ? SeriesGeometryStatus.AmbiguousGeometry : null;
    }

    private static IReadOnlyList<DicomInstanceDto> FallbackOrder(IReadOnlyList<DicomInstanceDto> instances) =>
        instances
            .OrderBy(i => i.InstanceNumber)
            .ThenBy(i => i.SopInstanceUid, StringComparer.Ordinal)
            .ThenBy(i => i.FilePath, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// 모든 인스턴스의 방향이 유효(유한·단위·직교)하고 첫 인스턴스와 같으며 투영이 유한하고 서로 구별될 때만 한 번의 정렬로 물리 순서를 만든다.
    /// Builds the physical order with a single projection pass and one sort, only when every orientation is valid (finite, unit, orthogonal),
    /// equals the first one, and every projection is finite and distinct.
    /// </summary>
    private static bool TryOrderSpatially(IReadOnlyList<DicomInstanceDto> instances, out List<DicomInstanceDto> ordered)
    {
        ordered = [];

        var reference = instances[0].ImageOrientationPatient!;
        if (!IsValidOrientation(reference))
        {
            return false;
        }

        // 법선 = row × column (첫 인스턴스 기준, 성분 직접 계산)
        // Normal = row × column of the first instance (components computed directly)
        var normalX = reference[1] * reference[5] - reference[2] * reference[4];
        var normalY = reference[2] * reference[3] - reference[0] * reference[5];
        var normalZ = reference[0] * reference[4] - reference[1] * reference[3];

        var projected = new (DicomInstanceDto Instance, double Projection)[instances.Count];
        for (var i = 0; i < projected.Length; i++)
        {
            var instance = instances[i];
            var orientation = instance.ImageOrientationPatient!;
            var position = instance.ImagePositionPatient!;
            if (position.Length != 3 || !IsValidOrientation(orientation) || !SameOrientation(reference, orientation))
            {
                return false;
            }

            // NaN·무한대 성분은 투영도 비유한으로 만든다(0 × ∞ = NaN 포함).
            // A NaN or infinite component makes the projection non-finite as well (0 × ∞ is NaN too).
            var projection = position[0] * normalX + position[1] * normalY + position[2] * normalZ;
            if (!double.IsFinite(projection))
            {
                return false;
            }

            projected[i] = (instance, projection);
        }

        Array.Sort(projected, CompareProjected);

        for (var k = 1; k < projected.Length; k++)
        {
            if (projected[k].Projection - projected[k - 1].Projection < PositionTolerance)
            {
                return false;
            }
        }

        ordered = new List<DicomInstanceDto>(projected.Length);
        foreach (var (instance, _) in projected)
        {
            ordered.Add(instance);
        }

        return true;
    }

    // 전순서 비교: 투영 → InstanceNumber → SopInstanceUid → FilePath (불안정 정렬이어도 결과가 결정적이다)
    // Total order: projection, InstanceNumber, SopInstanceUid, FilePath (deterministic even with an unstable sort)
    private static int CompareProjected(
        (DicomInstanceDto Instance, double Projection) left,
        (DicomInstanceDto Instance, double Projection) right)
    {
        var byProjection = left.Projection.CompareTo(right.Projection);
        if (byProjection != 0)
        {
            return byProjection;
        }

        var byNumber = left.Instance.InstanceNumber.CompareTo(right.Instance.InstanceNumber);
        if (byNumber != 0)
        {
            return byNumber;
        }

        var byUid = string.CompareOrdinal(left.Instance.SopInstanceUid, right.Instance.SopInstanceUid);
        return byUid != 0 ? byUid : string.CompareOrdinal(left.Instance.FilePath, right.Instance.FilePath);
    }

    // 모든 비교는 NaN이 실패하도록 작성한다 (NaN < 허용오차 는 false).
    // Every comparison is written so that NaN fails (NaN < tolerance is false).
    private static bool IsValidOrientation(double[] iop)
    {
        if (iop.Length != 6)
        {
            return false;
        }

        var rowLengthSquared = iop[0] * iop[0] + iop[1] * iop[1] + iop[2] * iop[2];
        var columnLengthSquared = iop[3] * iop[3] + iop[4] * iop[4] + iop[5] * iop[5];
        var rowDotColumn = iop[0] * iop[3] + iop[1] * iop[4] + iop[2] * iop[5];

        return Math.Abs(rowLengthSquared - 1.0) < OrientationTolerance &&
               Math.Abs(columnLengthSquared - 1.0) < OrientationTolerance &&
               Math.Abs(rowDotColumn) < OrientationTolerance;
    }

    private static bool SameOrientation(double[] reference, double[] candidate)
    {
        for (var k = 0; k < 6; k++)
        {
            if (!(Math.Abs(reference[k] - candidate[k]) < OrientationTolerance))
            {
                return false;
            }
        }

        return true;
    }
}