using DicomViewer.Application.DTOs;
using DicomViewer.Application.Services;

namespace DicomViewer.Tests.Application;

public class SeriesNavigationGeometryTests
{
    // 행 방향 (1,0,0), 열 방향 (0,0.6,0.8) → 법선 (0,-0.8,0.6)
    // Row (1,0,0), column (0,0.6,0.8) → normal (0,-0.8,0.6)
    private static readonly double[] Orientation = [1, 0, 0, 0, 0.6, 0.8];

    private const string Series = "1.2.3.100";
    private const string Frame = "1.2.3.200";

    [Fact]
    public void Oblique_series_is_ordered_by_projection_regardless_of_instance_number_or_file_name()
    {
        // 법선 방향 위치 k*2mm. InstanceNumber와 파일명은 일부러 뒤섞는다.
        // Position along the normal is k*2 mm. InstanceNumber and file names are deliberately shuffled.
        var instances = new[]
        {
            Slice(sliceIndex: 2, instanceNumber: 1, fileName: "a.dcm"),
            Slice(sliceIndex: 0, instanceNumber: 3, fileName: "z.dcm"),
            Slice(sliceIndex: 1, instanceNumber: 2, fileName: "m.dcm"),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.Spatial, navigation.GeometryStatus);
        Assert.Equal(["slice-0", "slice-1", "slice-2"], OrderedUids(navigation));
    }

    [Fact]
    public void Missing_position_reports_missing_geometry_and_uses_non_physical_fallback_order()
    {
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 2),
            new DicomInstanceDto("no-geometry", 1, "file-b.dcm", 4, 4, Series, Frame, null, Orientation),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.MissingGeometry, navigation.GeometryStatus);
        Assert.Equal(["no-geometry", "slice-0"], OrderedUids(navigation));
    }

    [Fact]
    public void Two_instances_at_the_same_projection_are_ambiguous()
    {
        var instances = new[]
        {
            Slice(sliceIndex: 1, instanceNumber: 1, uid: "dup-a"),
            Slice(sliceIndex: 1, instanceNumber: 2, uid: "dup-b"),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    [Fact]
    public void Mixed_known_and_absent_frame_of_reference_is_ambiguous_not_spatial()
    {
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1, frame: Frame),
            Slice(sliceIndex: 1, instanceNumber: 2, frame: null),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    [Fact]
    public void Differing_frame_of_reference_values_are_ambiguous()
    {
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1, frame: "frame-A"),
            Slice(sliceIndex: 1, instanceNumber: 2, frame: "frame-B"),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    [Fact]
    public void All_absent_frame_of_reference_reports_missing_geometry_not_spatial()
    {
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1, frame: null),
            Slice(sliceIndex: 1, instanceNumber: 2, frame: null),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.MissingGeometry, navigation.GeometryStatus);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Valid_first_instance_with_non_finite_orientation_on_a_later_instance_is_ambiguous(double bad)
    {
        // 첫 인스턴스는 유효하다. 두 번째의 방향 성분이 NaN/무한대여도 허용오차 비교만으로 통과해서는 안 된다.
        // The first instance is valid. A NaN/infinite component on the second must not slip through a tolerance comparison.
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1),
            WithOrientation(Slice(sliceIndex: 1, instanceNumber: 2), [1, 0, 0, 0, bad, 0.8]),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Valid_first_instance_with_non_finite_position_on_a_later_instance_is_ambiguous(double bad)
    {
        var instances = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1),
            WithPosition(Slice(sliceIndex: 1, instanceNumber: 2), [bad, 0, 0]),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(instances);

        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    [Theory]
    [InlineData(new double[] { 1, 0, 0, 0, 0.6 })]
    [InlineData(new double[] { 1, 0, 0, 0, 0.6, 0.8, 0 })]
    [InlineData(new double[] { 2, 0, 0, 0, 0.6, 0.8 })]
    [InlineData(new double[] { 1, 0, 0, 1, 0, 0 })]
    public void Malformed_non_unit_or_non_orthogonal_orientation_is_ambiguous_on_any_instance(double[] malformed)
    {
        var later = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1),
            WithOrientation(Slice(sliceIndex: 1, instanceNumber: 2), malformed),
        };
        var first = later.Reverse().ToArray();

        foreach (var instances in new[] { later, first })
        {
            var navigation = new SeriesNavigationService();
            navigation.SetSeries(instances);

            Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
        }
    }

    [Fact]
    public void Blank_or_whitespace_identifiers_count_as_missing()
    {
        var allBlank = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1, frame: "  "),
            Slice(sliceIndex: 1, instanceNumber: 2, frame: ""),
        };
        var mixed = new[]
        {
            Slice(sliceIndex: 0, instanceNumber: 1, frame: Frame),
            Slice(sliceIndex: 1, instanceNumber: 2, frame: "\t "),
        };

        var navigation = new SeriesNavigationService();
        navigation.SetSeries(allBlank);
        Assert.Equal(SeriesGeometryStatus.MissingGeometry, navigation.GeometryStatus);

        navigation.SetSeries(mixed);
        Assert.Equal(SeriesGeometryStatus.AmbiguousGeometry, navigation.GeometryStatus);
    }

    private static DicomInstanceDto WithOrientation(DicomInstanceDto instance, double[] orientation) =>
        instance with { ImageOrientationPatient = orientation };

    private static DicomInstanceDto WithPosition(DicomInstanceDto instance, double[] position) =>
        instance with { ImagePositionPatient = position };

    [Fact]
    public void Empty_series_reports_empty_status()
    {
        var navigation = new SeriesNavigationService();
        navigation.SetSeries([]);

        Assert.Equal(SeriesGeometryStatus.Empty, navigation.GeometryStatus);
        Assert.Null(navigation.GetCurrentInstance());
    }

    private static List<string> OrderedUids(SeriesNavigationService navigation)
    {
        var uids = new List<string>();
        navigation.NavigateFirst();
        for (var i = 0; i < navigation.TotalCount; i++)
        {
            uids.Add(navigation.GetCurrentInstance()!.SopInstanceUid);
            navigation.NavigateNext();
        }

        return uids;
    }

    private static DicomInstanceDto Slice(
        int sliceIndex,
        int instanceNumber,
        string? fileName = null,
        string? uid = null,
        string? frame = Frame)
    {
        // 법선 방향 위치 2mm × sliceIndex
        // Position 2 mm × sliceIndex along the normal
        var position = new double[] { 0, -0.8 * 2 * sliceIndex, 0.6 * 2 * sliceIndex };
        var sopUid = uid ?? $"slice-{sliceIndex}";
        return new DicomInstanceDto(
            sopUid,
            instanceNumber,
            fileName ?? $"{sopUid}.dcm",
            4,
            4,
            Series,
            frame,
            position,
            Orientation);
    }
}