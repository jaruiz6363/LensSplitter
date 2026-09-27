using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;
using AberrationCalculator.Optimize.Evaluation;
using LensSplitter.Core.Lens;

namespace LensSplitter.Core.Splitting;

/// <summary>
/// How the element's power is shared and each half bent, the gap between them, and how much
/// length the split borrows from the air after the element (<see cref="Extra"/>), so the halves
/// can be thicker than half the element each.
/// </summary>
public sealed record SplitShape(double Ratio, double X1, double X2, double Gap, double Extra = 0.0)
{
    public override string ToString() =>
        FormattableString.Invariant($"ratio {Ratio:0.###}, X1 {X1:0.###}, X2 {X2:0.###}, gap {Gap:0.###}, extra {Extra:0.###}");
}

/// <summary>
/// A glass for each half, instead of the element's own in both: the catalog names and their
/// indices at the primary wavelength.
/// </summary>
public sealed record SplitGlasses(string A, double NA, string B, double NB);

/// <summary>What a split must leave room for, in lens units.</summary>
public sealed class SplitLimits
{
    /// <summary>Least centre thickness of either half.</summary>
    public double MinCentreThickness { get; set; } = 1.0;

    /// <summary>Least edge thickness of either half, and least edge clearance of an air space, at the clear aperture.</summary>
    public double MinEdgeThickness { get; set; } = 0.5;

    /// <summary>Least centre air gap between the halves.</summary>
    public double MinAirGap { get; set; } = 0.1;
}

/// <summary>A built split, and where its surfaces are.</summary>
public sealed class SplitLens
{
    public OpticalSystem System { get; init; } = null!;
    public SplitShape Shape { get; init; } = null!;

    /// <summary>Surface index of the first half's front face; the four split faces follow it.</summary>
    public int Front { get; init; }

    public double T1 { get; init; }
    public double Gap { get; init; }
    public double T2 { get; init; }

    /// <summary>The air space after the split, shortened by what the split added, so every surface after it stays where it was.</summary>
    public double AirAfter { get; init; }
}

/// <summary>
/// Builds the four surfaces that replace one element.
///
/// <list type="bullet">
/// <item>The first half's front face is the element's front face, and the second half's rear
/// face its rear face: each keeps its conic and aspheric terms, its type, its stop and its
/// aperture. Only its curvature changes, set so that its VERTEX curvature - with an r^2 term,
/// which is a curvature change, counted - is the one the bending asks for.</item>
/// <item>The two new inner faces are spheres, the gap between them air.</item>
/// <item>The glass is the element's, in both halves.</item>
/// <item>Each half's thickness is what its edge needs at the clear aperture, measured with the
/// real sag of each face, figuring included - at least <see cref="SplitLimits.MinCentreThickness"/>
/// at the centre, and otherwise an even share of the element's thickness plus whatever length
/// the split borrows (<see cref="SplitShape.Extra"/>).</item>
/// <item>The air space after the element is shortened by whatever the split adds, so every later
/// surface stays where it was. If that space cannot give it without the rear face meeting the next
/// surface, the split does not fit and is refused.</item>
/// </list>
///
/// <para>The curvatures come from the thin-lens relations for a given power and shape factor
/// X = (c1 + c2)/(c1 - c2) in the media on either side. They are a starting point: the optimiser
/// that follows moves every one of the four freely.</para>
/// </summary>
public static class SplitGeometry
{
    /// <summary>
    /// The split, or null when it does not fit.
    /// </summary>
    /// <param name="n">Index after each surface of the ORIGINAL lens at the primary wavelength.</param>
    /// <param name="elementPower">The element's power, which the two halves share.</param>
    /// <param name="clear">The clear semi-diameter every split face must pass.</param>
    /// <param name="thickness">Thicknesses to use instead of deriving them (t1, gap, t2), for
    /// holding them fixed while the power is adjusted.</param>
    public static SplitLens? Build(OpticalSystem system, LensElement e, double[] n, double elementPower,
                                   double clear, SplitShape shape, SplitLimits limits,
                                   (double T1, double Gap, double T2)? thickness = null, SplitGlasses? glasses = null)
    {
        double n0 = Math.Abs(n[e.Front - 1]), n2 = Math.Abs(n[e.Rear]);
        double ngA = glasses?.NA ?? Math.Abs(n[e.Front]), ngB = glasses?.NB ?? Math.Abs(n[e.Front]);
        const double nAir = 1.0;
        double phiA = shape.Ratio * elementPower, phiB = (1.0 - shape.Ratio) * elementPower;

        // Thin-lens curvatures for a power and shape factor between media: phi = (ng - nIn) c1 +
        // (nOut - ng) c2, with c1 = (X + 1) d / 2 and c2 = (X - 1) d / 2.
        static (double C1, double C2)? Curvatures(double phi, double x, double nIn, double ng, double nOut)
        {
            double den = (ng - nIn) * (x + 1.0) + (nOut - ng) * (x - 1.0);
            if (Math.Abs(den) < 1e-12) return null;
            double d = 2.0 * phi / den;
            return ((x + 1.0) * d / 2.0, (x - 1.0) * d / 2.0);
        }
        var a = Curvatures(phiA, shape.X1, n0, ngA, nAir);
        var b = Curvatures(phiB, shape.X2, nAir, ngB, n2);
        if (a == null || b == null) return null;

        var split = DesignCopy.Deep(system);
        var front = split.Surfaces[e.Front];
        var rear = split.Surfaces[e.Rear];
        double originalT = system.Surfaces[e.Front].Thickness;
        double originalAir = system.Surfaces[e.Rear].Thickness;

        // The outer faces: the element's own, with the vertex curvature the bending asks for.
        front.Curvature = a.Value.C1 - 2.0 * R2(front);
        rear.Curvature = b.Value.C2 - 2.0 * R2(rear);

        var inner1 = NewFace(a.Value.C2, glassFrom: null);     // air after it: the gap
        var inner2 = NewFace(b.Value.C1, glassFrom: front);    // the element's glass after it
        if (glasses != null)
        {
            foreach (var s in new[] { front, inner2 })
            {
                s.ModelIndexEnabled = false;
                s.CatalogName = null;
            }
            front.Material = glasses.A;
            inner2.Material = glasses.B;
        }
        if (front.SemiDiameterMode == SemiDiameterMode.Fixed || rear.SemiDiameterMode == SemiDiameterMode.Fixed)
        {
            // A fixed aperture on the element stays on its faces, and the new faces take the clear
            // aperture the element had to pass.
            inner1.SemiDiameterMode = inner2.SemiDiameterMode = SemiDiameterMode.Fixed;
        }
        inner1.SemiDiameter = inner2.SemiDiameter = clear;

        double t1, gap, t2;
        if (thickness is { } fixedT)
            (t1, gap, t2) = fixedT;
        else
        {
            double sf = front.Sag(clear), s1 = inner1.Sag(clear), s2 = inner2.Sag(clear), sr = rear.Sag(clear);
            if (double.IsNaN(sf) || double.IsNaN(s1) || double.IsNaN(s2) || double.IsNaN(sr)) return null;
            double t1Min = Math.Max(limits.MinCentreThickness, limits.MinEdgeThickness + sf - s1);
            double t2Min = Math.Max(limits.MinCentreThickness, limits.MinEdgeThickness + s2 - sr);
            double gapMin = Math.Max(limits.MinAirGap, limits.MinEdgeThickness + s1 - s2);
            gap = Math.Max(shape.Gap, gapMin);
            double glass = originalT + shape.Extra - gap;
            t1 = Math.Max(t1Min, glass / 2.0);
            t2 = Math.Max(t2Min, glass - t1);
        }

        double airAfter = originalAir + originalT - (t1 + gap + t2);
        if (!double.IsInfinity(originalAir))
        {
            // The rear face must still clear whatever follows it.
            int next = e.Rear + 1;
            double clearance = airAfter;
            if (next < system.Surfaces.Count - 1)
            {
                double h = Math.Min(clear, system.Surfaces[next].SemiDiameter > 0 ? system.Surfaces[next].SemiDiameter : clear);
                double sNext = system.Surfaces[next].Sag(h), sRear = rear.Sag(h);
                if (!double.IsNaN(sNext) && !double.IsNaN(sRear)) clearance = airAfter + sNext - sRear;
            }
            if (airAfter <= 0.0 || (next < system.Surfaces.Count - 1 && clearance < limits.MinEdgeThickness)) return null;
        }

        front.Thickness = t1;
        inner1.Thickness = gap;
        inner2.Thickness = t2;
        rear.Thickness = airAfter;
        split.Surfaces.Insert(e.Front + 1, inner1);
        split.Surfaces.Insert(e.Front + 2, inner2);
        for (int i = 0; i < split.Surfaces.Count; i++) split.Surfaces[i].Index = i;

        // Pickups beyond the element move with their surfaces; one on the element's own faces no
        // longer means anything and is dropped.
        split.Pickups = split.Pickups
            .Where(p => !IsOn(p.TargetSurfaceIndex, e) && !IsOn(p.SourceSurfaceIndex, e))
            .Select(p => { p.TargetSurfaceIndex = Shift(p.TargetSurfaceIndex, e); p.SourceSurfaceIndex = Shift(p.SourceSurfaceIndex, e); return p; })
            .ToList();

        return new SplitLens { System = split, Shape = shape, Front = e.Front, T1 = t1, Gap = gap, T2 = t2, AirAfter = airAfter };
    }

    private static double R2(Surface s) => s.AsphericCoefficients.Length > 0 ? s.AsphericCoefficients[0] : 0.0;
    private static bool IsOn(int surface, LensElement e) => surface == e.Front || surface == e.Rear;
    private static int Shift(int surface, LensElement e) => surface > e.Rear ? surface + 2 : surface;

    // A new spherical face, with air after it, or the glass that follows glassFrom.
    private static Surface NewFace(double curvature, Surface? glassFrom) => new()
    {
        Type = SurfaceType.Standard,
        Curvature = curvature,
        Material = glassFrom?.Material,
        CatalogName = glassFrom?.CatalogName,
        ModelIndexEnabled = glassFrom?.ModelIndexEnabled ?? false,
        ModelNd = glassFrom?.ModelNd ?? 0.0,
        ModelVd = glassFrom?.ModelVd ?? 0.0,
        ModelDPgF = glassFrom?.ModelDPgF ?? 0.0,
        SemiDiameterMode = SemiDiameterMode.Auto,
    };

    /// <summary>
    /// The index after each surface of a split lens, from the original's: the glass for both
    /// halves, air in the gap, everything else shifted two places.
    /// </summary>
    public static double[] SplitIndices(double[] n, LensElement e, SplitGlasses? glasses = null)
    {
        var list = n.ToList();
        if (glasses != null) list[e.Front] = glasses.NA;
        list.Insert(e.Front + 1, 1.0);
        list.Insert(e.Front + 2, glasses?.NB ?? n[e.Front]);
        return list.ToArray();
    }

    /// <summary>
    /// Scales the two halves' power together until the lens's focal length is
    /// <paramref name="targetEfl"/>, thicknesses held. Secant iteration on the scale; the focal
    /// length is smooth and monotonic in it near 1.
    /// </summary>
    public static SplitLens? HoldFocalLength(OpticalSystem system, LensElement e, double[] n, double elementPower,
                                             double clear, SplitShape shape, SplitLimits limits, double targetEfl,
                                             SplitGlasses? glasses = null)
    {
        var first = Build(system, e, n, elementPower, clear, shape, limits, null, glasses);
        if (first == null) return null;
        var fixedT = (first.T1, first.Gap, first.T2);
        var ns = SplitIndices(n, e, glasses);

        double Efl(SplitLens s) => ParaxialTrace.Trace(s.System, ns, 0.0).Efl;
        double x0 = 1.0, f0 = Efl(first) - targetEfl;
        if (!double.IsFinite(f0)) return null;
        if (Math.Abs(f0) <= 1e-12 * Math.Abs(targetEfl)) return first;

        double x1 = 1.001;
        var s1 = Build(system, e, n, elementPower * x1, clear, shape, limits, fixedT, glasses);
        if (s1 == null) return null;
        double f1 = Efl(s1) - targetEfl;
        SplitLens best = Math.Abs(f1) < Math.Abs(f0) ? s1 : first;
        for (int k = 0; k < 40 && double.IsFinite(f1); k++)
        {
            if (Math.Abs(f1 - f0) < 1e-300) break;
            double x2 = x1 - f1 * (x1 - x0) / (f1 - f0);
            if (!double.IsFinite(x2) || x2 <= 0.05 || x2 > 20.0) break;
            var s2 = Build(system, e, n, elementPower * x2, clear, shape, limits, fixedT, glasses);
            if (s2 == null) break;
            double f2 = Efl(s2) - targetEfl;
            (x0, f0, x1, f1) = (x1, f1, x2, f2);
            if (Math.Abs(f2) < Math.Abs(Efl(best) - targetEfl)) best = s2;
            if (Math.Abs(f2) <= 1e-12 * Math.Abs(targetEfl)) break;
        }
        return Math.Abs(Efl(best) - targetEfl) <= 1e-9 * Math.Abs(targetEfl) ? best : null;
    }
}
