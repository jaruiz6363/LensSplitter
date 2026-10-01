using System;
using System.Collections.Generic;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// Recovers the seventh-order coefficients from exact ray traces, by inverting the transverse
/// aberration polynomial - and, through <see cref="InvertThirdAndFifth"/>, the third and fifth
/// orders the same way. An oracle that owes nothing to the scheme it checks.
///
/// <para><b>Why this and not a spot comparison.</b> A predicted RMS spot collapses eighteen
/// coefficients into one number, so agreement there is an aggregate over errors that may be
/// cancelling and cannot tell a more-correct set from a differently-wrong one. Inverting the
/// polynomial recovers each coefficient SEPARATELY, because every one has its own signature in
/// pupil radius, azimuth and field. Hopkins (JOSA 66, 405) did this against ACCOS V and found
/// agreement to roundoff, on aspheric systems, using Buchdahl's own polynomials.</para>
///
/// <para><b>How the orders are separated.</b> Scaling the pupil coordinate AND the field by a
/// common factor s makes every term of total degree d scale as s^d, so the transverse error
/// along a fixed ray shape is an odd polynomial in s: the paraxial part in s, third order in
/// s^3, fifth in s^5, seventh in s^7. Fitting that polynomial and reading its s^7 coefficient
/// isolates the seventh order exactly, with the ninth and beyond absorbed by the higher terms
/// of the fit rather than contaminating it. This is the same device as Hopkins's proximate
/// rays, done numerically instead of algebraically.</para>
///
/// <para>The scheme is not consulted anywhere in this file.</para>
/// </summary>
public static class CoefficientInversion
{
    /// <summary>One ray shape: a pupil point and a field, to be scaled together.</summary>
    public readonly record struct Shape(double Rho, double Theta, double H);

    /// <summary>What the inversion recovered, and how well the fit held.</summary>
    public sealed class Result
    {
        /// <summary>tau1..tau20 in the transverse convention, indexed 1..20.</summary>
        public double[] Tau { get; init; } = new double[21];

        /// <summary>Largest residual of the least-squares solve, relative to the data.</summary>
        public double Residual { get; init; }

        /// <summary>Shapes that were traced successfully and used.</summary>
        public int Used { get; init; }
    }

    /// <summary>
    /// A spread of ray shapes: several pupil radii and azimuths against several field
    /// fractions, plus the pure cases - zero field, which isolates the coefficient in rho^7,
    /// and zero pupil, which isolates the one in h^7 that no identity constrains.
    /// </summary>
    public static List<Shape> DefaultShapes()
    {
        var shapes = new List<Shape>();
        double[] rhos = { 0.35, 0.7, 1.0 };
        double[] thetas = { 0.0, Math.PI / 6, Math.PI / 4, Math.PI / 3, Math.PI / 2,
                            2 * Math.PI / 3, 5 * Math.PI / 6 };
        double[] hs = { 0.35, 0.7, 1.0 };

        foreach (double r in rhos)
            foreach (double t in thetas)
                foreach (double h in hs)
                    shapes.Add(new Shape(r, t, h));

        foreach (double r in rhos)
            foreach (double t in new[] { 0.0, Math.PI / 4, Math.PI / 2 })
                shapes.Add(new Shape(r, t, 0.0));          // pure aperture

        foreach (double h in hs)
            shapes.Add(new Shape(0.0, 0.0, h));            // pure field

        return shapes;
    }

    /// <summary>
    /// Traces the shapes at a ladder of scales, fits the odd polynomial in the scale, and
    /// solves for tau1..tau20.
    /// </summary>
    /// <param name="maxFieldDeg">
    /// The field the coefficients are normalised to - the same one
    /// <see cref="TertiaryCoefficients.Attach"/> uses, so that h is tan(field)/tan(maxField).
    /// </param>
    /// <summary>
    /// Where a ray of this shape lands. Supplying one replaces the real trace, which is how a
    /// SERIES trace is measured on exactly the same footing. The inversion returns coefficients
    /// in Buchdahl's basis whatever produced the landings, so two routes can be compared without
    /// anyone having to derive a change of basis between them and get it right - which is the
    /// step this project has twice lost days to.
    /// </summary>
    public delegate RealRayTrace.Landing RaySource(double fieldDeg, double py, double pz);

    public static Result? Invert(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                 double maxFieldDeg, IReadOnlyList<Shape>? shapes = null,
                                 RaySource? rays = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));

        RequireObjectAtInfinity(system);

        shapes ??= DefaultShapes();
        double hmax = Math.Tan(maxFieldDeg * Math.PI / 180.0);
        if (Math.Abs(hmax) < 1e-12) return null;

        // The scale ladder, and it matters more than anything else here. Measured against B7
        // on an axial fan - where the degree-seven part is that coefficient alone, and B7 is
        // known exactly from the FIFTHORD comparison - a ladder running out to s = 1 with
        // powers to s^11 recovers it only to 1.5 per cent, because the ninth order and above
        // are still large at full aperture and the fit cannot separate them from the seventh.
        // Twelve points out to six tenths, with powers to s^15 to absorb the tail, recovers it
        // to better than one part in a million.
        double[] scales = new double[12];
        for (int i = 0; i < 12; i++) scales[i] = 0.6 * (i + 1) / 12.0;
        int[] powers = { 1, 3, 5, 7, 9, 11, 13, 15 };

        var rows = new List<double[]>();
        var rhs = new List<double>();
        int used = 0;
        double frame = ImageFrameSign(system, paraxial);

        foreach (var sh in shapes)
        {
            var ey = new List<(double S, double V)>();
            var ez = new List<(double S, double V)>();
            bool ok = true;

            foreach (double s in scales)
            {
                double py = s * sh.Rho * Math.Cos(sh.Theta);
                double pz = s * sh.Rho * Math.Sin(sh.Theta);
                double fieldDeg = Math.Atan(s * sh.H * hmax) * 180.0 / Math.PI;

                var land = rays != null
                         ? rays(fieldDeg, py, pz)
                         : RealRayTrace.Trace(system, indices, paraxial, fieldDeg, py, pz);
                if (!land.Ok) { ok = false; break; }

                // Take the paraxial image height off before fitting. It is the whole of the
                // degree-one term and for an object at infinity it is exactly efl*tan(field).
                // Leaving it in is fatal: at seven tenths of the field it is 12.7 mm sitting on
                // a third-order term of 0.019, so the fit has to cancel three orders of
                // magnitude to reach the aberration and the residue swamps everything below.
                // That showed as a third-order ratio of 0.84 on tangential shapes while axial
                // and sagittal ones - which have no paraxial term - came out at 1.0000.
                double paraxialY = paraxial.Efl * Math.Tan(fieldDeg * Math.PI / 180.0);
                ey.Add((s, frame * (land.Y - paraxialY)));
                ez.Add((s, frame * land.Z));
            }
            if (!ok) continue;
            used++;

            double c7y = OddFit(ey, powers, 7);
            double c7z = OddFit(ez, powers, 7);

            var (ry, rz) = ModelRow(sh);

            if (AnyNonZero(ry)) { rows.Add(ry); rhs.Add(c7y); }
            if (AnyNonZero(rz)) { rows.Add(rz); rhs.Add(c7z); }
        }

        if (rows.Count < 20) return null;

        double[] x = LeastSquares(rows, rhs);
        var tau = new double[21];
        for (int k = 1; k <= 20; k++) tau[k] = x[k - 1];

        double worst = 0.0, scale = 1e-30;
        for (int i = 0; i < rows.Count; i++)
        {
            double pred = 0.0;
            for (int k = 0; k < 20; k++) pred += rows[i][k] * x[k];
            worst = Math.Max(worst, Math.Abs(pred - rhs[i]));
            scale = Math.Max(scale, Math.Abs(rhs[i]));
        }

        return new Result { Tau = tau, Residual = worst / scale, Used = used };
    }

    /// <summary>
    /// The sign that turns a landing, measured in the global frame, into the frame Buchdahl's
    /// coefficients are written in: -1 after an odd number of reflections, +1 otherwise.
    ///
    /// <para>His convention carries a reflection in the sign of the index, and the image-space
    /// transverse aberration is then measured along an axis the mirror has reversed. Measured,
    /// not assumed: on the parabola (<c>F4_parabolic_mirror</c>) every one of the seventeen
    /// third- and fifth-order coefficients recovered from correctly reflected rays is exactly
    /// the negative of Buchdahl's, the zeros included - a change of frame, not an error, which
    /// would not flip all seventeen together. The ray trace keeps the global frame, as
    /// OpticStudio and Optiland do. A multiplication by exactly one everywhere else, so no
    /// refracting design computes a different bit.</para>
    ///
    /// <para>Established on one reflection (the parabola and the spherical mirror) and on two
    /// (Thompson's telescope, built in <c>ThompsonTelescopeTests</c>), where the rule turns
    /// nothing and the rays agree with Buchdahl unturned on every order.</para>
    /// </summary>
    private static double ImageFrameSign(OpticalSystem system, ParaxialResult paraxial) =>
        paraxial.N[system.LastOpticalSurface()] < 0.0 ? -1.0 : 1.0;

    private static void RequireObjectAtInfinity(OpticalSystem system)
    {
        // OBJECT AT INFINITY ONLY, and the assumption is THIS class's rather than the ray
        // trace's. Two things are written for a collimated object: the field is taken as
        // tan(theta), and the paraxial part subtracted off before the fit is efl*tan(theta).
        // Neither means anything when the object is at a finite distance, and both would fail
        // quietly - the fit would still converge, on the wrong quantity.
        //
        // RealRayTrace used to refuse a finite conjugate itself, which happened to protect this
        // method. It no longer does: the trace is conjugate-agnostic and its aiming now handles
        // both, so the guard has to live where the assumption actually is.
        if (!double.IsInfinity(system.Surfaces[0].Thickness)
            && Math.Abs(system.Surfaces[0].Thickness) < 1e12)
            throw new NotSupportedException(
                "CoefficientInversion recovers the coefficients from rays at an object at "
              + "infinity only. It measures the field as tan(theta) and subtracts a paraxial "
              + "height of efl*tan(theta), neither of which holds at a finite conjugate. Use "
              + "ForbesCoefficients.Invert, whose series trace handles either.");
    }

    // ── Third and fifth order ────────────────────────────────────────────────────────────

    /// <summary>One ray to trace: a field angle in degrees and a fractional pupil point.</summary>
    public readonly record struct RayRequest(double FieldDeg, double Py, double Pz);

    /// <summary>
    /// Where each of a batch of rays lands, in the order asked. The batch form exists for a
    /// tracer on the far side of an interop boundary - Optiland, reached through Python - which
    /// pays per call rather than per ray: one call for the whole ladder instead of nine hundred.
    /// </summary>
    public delegate RealRayTrace.Landing[] BatchRaySource(IReadOnlyList<RayRequest> rays);

    /// <summary>The third- and fifth-order coefficients in Rimmer's names, as reported.</summary>
    public static readonly string[] ThirdOrderNames = { "B", "F", "C", "Pi", "E" };

    public static readonly string[] FifthOrderNames =
        { "B5", "F1", "F2", "M1", "M2", "M3", "N1", "N2", "N3", "C5", "Pi5", "E5" };

    /// <summary>What <see cref="InvertThirdAndFifth"/> recovered.</summary>
    public sealed class LowerOrders
    {
        /// <summary>B..E and B5..E5 filled in; everything else zero.</summary>
        public BuchdahlTerms Terms { get; init; } = new();

        /// <summary>Largest residual of each solve, relative to its data.</summary>
        public double ResidualThird { get; init; }
        public double ResidualFifth { get; init; }

        /// <summary>Shapes that were traced successfully and used.</summary>
        public int Used { get; init; }
    }

    /// <summary>
    /// The third and fifth orders from real rays, by the same device <see cref="Invert"/> uses
    /// for the seventh: scale pupil and field together, fit the odd polynomial in the scale, and
    /// read the s^3 and s^5 coefficients off each shape instead of the s^7.
    ///
    /// <para>The same ladder serves all three orders, and it serves the lower ones better: the
    /// terms they are separated from are higher in s, so the fit leans less on the tail.</para>
    ///
    /// <para>Nothing here depends on who traced the rays. With <paramref name="rays"/> omitted
    /// they come from <see cref="RealRayTrace"/>; supplied, they come from anywhere, and what
    /// comes back is still in Buchdahl's basis - which is how a second program's ray trace is
    /// checked against this program's coefficients without a change of basis in between.</para>
    /// </summary>
    public static LowerOrders? InvertThirdAndFifth(OpticalSystem system, double[] indices,
                                                   ParaxialResult paraxial, double maxFieldDeg,
                                                   IReadOnlyList<Shape>? shapes = null,
                                                   BatchRaySource? rays = null)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        RequireObjectAtInfinity(system);

        shapes ??= DefaultShapes();
        double hmax = Math.Tan(maxFieldDeg * Math.PI / 180.0);
        if (Math.Abs(hmax) < 1e-12) return null;

        // The ladder and the powers are Invert's own; see the note there on why.
        double[] scales = new double[12];
        for (int i = 0; i < 12; i++) scales[i] = 0.6 * (i + 1) / 12.0;
        int[] powers = { 1, 3, 5, 7, 9, 11, 13, 15 };

        var requests = new List<RayRequest>(shapes.Count * scales.Length);
        foreach (var sh in shapes)
            foreach (double s in scales)
                requests.Add(new RayRequest(Math.Atan(s * sh.H * hmax) * 180.0 / Math.PI,
                                            s * sh.Rho * Math.Cos(sh.Theta),
                                            s * sh.Rho * Math.Sin(sh.Theta)));

        RealRayTrace.Landing[] landings;
        if (rays != null)
        {
            landings = rays(requests);
            if (landings == null || landings.Length != requests.Count)
                throw new InvalidOperationException(
                    $"The ray source returned {landings?.Length ?? 0} landings for {requests.Count} rays.");
        }
        else
        {
            landings = new RealRayTrace.Landing[requests.Count];
            for (int i = 0; i < requests.Count; i++)
                landings[i] = RealRayTrace.Trace(system, indices, paraxial,
                                                 requests[i].FieldDeg, requests[i].Py, requests[i].Pz);
        }

        double frame = ImageFrameSign(system, paraxial);
        var rows3 = new List<double[]>(); var rhs3 = new List<double>();
        var rows5 = new List<double[]>(); var rhs5 = new List<double>();
        int used = 0;

        for (int k = 0; k < shapes.Count; k++)
        {
            var sh = shapes[k];
            var ey = new List<(double S, double V)>();
            var ez = new List<(double S, double V)>();
            bool ok = true;
            for (int i = 0; i < scales.Length; i++)
            {
                var req = requests[k * scales.Length + i];
                var land = landings[k * scales.Length + i];
                if (!land.Ok) { ok = false; break; }
                double paraxialY = paraxial.Efl * Math.Tan(req.FieldDeg * Math.PI / 180.0);
                ey.Add((scales[i], frame * (land.Y - paraxialY)));
                ez.Add((scales[i], frame * land.Z));
            }
            if (!ok) continue;
            used++;

            double[] cy = OddFitAll(ey, powers);
            double[] cz = OddFitAll(ez, powers);

            // powers[1] is s^3, powers[2] is s^5.
            AddRows(sh, ThirdOrderNames, 3, cy[1], cz[1], rows3, rhs3);
            AddRows(sh, FifthOrderNames, 5, cy[2], cz[2], rows5, rhs5);
        }

        if (rows3.Count < ThirdOrderNames.Length || rows5.Count < FifthOrderNames.Length) return null;

        var (x3, r3) = SolveWithResidual(rows3, rhs3);
        var (x5, r5) = SolveWithResidual(rows5, rhs5);

        var terms = new BuchdahlTerms();
        for (int j = 0; j < ThirdOrderNames.Length; j++) SetTerm(terms, ThirdOrderNames[j], x3[j]);
        for (int j = 0; j < FifthOrderNames.Length; j++) SetTerm(terms, FifthOrderNames[j], x5[j]);

        return new LowerOrders { Terms = terms, ResidualThird = r3, ResidualFifth = r5, Used = used };
    }

    /// <summary>
    /// What each named coefficient contributes to the degree-<paramref name="degree"/>
    /// transverse aberration at one shape, as a meridional and a sagittal row.
    /// </summary>
    private static void AddRows(Shape sh, string[] names, int degree, double cy, double cz,
                                List<double[]> rows, List<double> rhs)
    {
        var ry = new double[names.Length];
        var rz = new double[names.Length];
        for (int j = 0; j < names.Length; j++)
        {
            var unit = new BuchdahlTerms();
            SetTerm(unit, names[j], 1.0);
            var hi = Prms.Transverse(unit, sh.Rho, sh.Theta, sh.H, degree);
            var lo = Prms.Transverse(unit, sh.Rho, sh.Theta, sh.H, degree - 2);
            ry[j] = hi.Y - lo.Y;
            rz[j] = hi.Z - lo.Z;

            // Distortion, which Prms leaves out for the reason given in ModelRow: E enters the
            // meridional error as E h^3 and E5 as E5 h^5, with no pupil dependence.
            if (names[j] == "E" || names[j] == "E5") ry[j] += Math.Pow(sh.H, degree);
        }
        if (AnyNonZero(ry)) { rows.Add(ry); rhs.Add(cy); }
        if (AnyNonZero(rz)) { rows.Add(rz); rhs.Add(cz); }
    }

    private static (double[] X, double Residual) SolveWithResidual(List<double[]> rows, List<double> rhs)
    {
        double[] x = LeastSquares(rows, rhs);
        double worst = 0.0, scale = 1e-30;
        for (int i = 0; i < rows.Count; i++)
        {
            double pred = 0.0;
            for (int k = 0; k < x.Length; k++) pred += rows[i][k] * x[k];
            worst = Math.Max(worst, Math.Abs(pred - rhs[i]));
            scale = Math.Max(scale, Math.Abs(rhs[i]));
        }
        return (x, worst / scale);
    }

    private static void SetTerm(BuchdahlTerms t, string name, double value) =>
        typeof(BuchdahlTerms).GetField(name)!.SetValue(t, value);

    /// <summary>
    /// What each tau contributes to the degree-seven transverse aberration at one shape.
    ///
    /// <para>Exposed because it is the half of this class that is exact. Recovering the
    /// degree-seven part of a traced ray needs a ladder of scales and a fit; mapping it onto the
    /// twenty coefficients afterwards is a linear solve against this model and needs neither. A
    /// route that can write the degree-seven part down - a series trace can - should reuse the
    /// model and skip the fit, and then its coefficients are exact rather than resolved to
    /// whatever the ladder can separate.</para>
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Shape,
        (double[] Y, double[] Z)> ModelCache = new();

    public static (double[] Y, double[] Z) ModelRow(Shape sh)
    {
        // The model depends on the SHAPE alone - never on the lens - so it is built once per
        // shape and kept. It was most of the cost of an inversion: twenty evaluations of Prms
        // per shape, per call, recomputing the same constants for every design. Copies go out,
        // since the solver is free to do as it likes with what it is handed.
        var hit = ModelCache.GetOrAdd(sh, Build);
        return ((double[])hit.Y.Clone(), (double[])hit.Z.Clone());
    }

    private static (double[] Y, double[] Z) Build(Shape sh)
    {
        var ry = new double[20];
        var rz = new double[20];
        for (int k = 1; k <= 20; k++)
        {
            var unit = UnitTerms(k);
            var hi = Prms.Transverse(unit, sh.Rho, sh.Theta, sh.H, 7);
            var lo = Prms.Transverse(unit, sh.Rho, sh.Theta, sh.H, 5);
            ry[k - 1] = hi.Y - lo.Y;
            rz[k - 1] = hi.Z - lo.Z;
        }

        // Prms is Robb's analytic merit function - a SPOT-SIZE polynomial, and it says so in its
        // own documentation: distortion displaces the whole patch without changing its size, so
        // E, E5 and tau20 are deliberately absent from it. A traced ray does contain them, so the
        // missing column has to be supplied here, or the solve is asked to fit data the model
        // cannot express. At degree seven that is tau20 alone, and its form is forced rather than
        // chosen: aperture power zero, hence field power seven, and with no pupil vector to
        // reference there is no azimuth it could depend on.
        ry[19] += Math.Pow(sh.H, 7);
        return (ry, rz);
    }

    /// <summary>
    /// Solves for the twenty coefficients from the degree-seven transverse aberration supplied
    /// EXACTLY at each shape, rather than fitted off a ladder of traced rays.
    /// </summary>
    /// <summary>
    /// The model matrix and the solve it induces, for a fixed set of shapes.
    ///
    /// <para>The model never depends on the lens, so neither does the least-squares operator it
    /// defines: the solution is linear in the right-hand side, <c>x = M b</c> for one fixed
    /// <c>M</c>. Building <c>M</c> costs one solve per row and is done once; every design after
    /// that is a matrix-vector product. <c>M</c> is built by the SAME Householder routine the
    /// real-ray route uses, applied to unit right-hand sides, so this is a cached solve and not
    /// a different one - <c>ForbesCoefficientsTests</c> holds the two against each other.</para>
    /// </summary>
    private sealed class FixedModel
    {
        public readonly List<double[]> Rows = new();
        public readonly List<int> ShapeOfRow = new();
        public readonly List<bool> IsMeridional = new();
        private readonly double[][] _operator;

        public FixedModel(IReadOnlyList<Shape> shapes)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                var (ry, rz) = ModelRow(shapes[i]);
                if (AnyNonZero(ry)) { Rows.Add(ry); ShapeOfRow.Add(i); IsMeridional.Add(true); }
                if (AnyNonZero(rz)) { Rows.Add(rz); ShapeOfRow.Add(i); IsMeridional.Add(false); }
            }

            _operator = new double[Rows.Count][];
            var unit = new List<double>(new double[Rows.Count]);
            for (int i = 0; i < Rows.Count; i++)
            {
                unit[i] = 1.0;
                _operator[i] = LeastSquares(Rows, unit);
                unit[i] = 0.0;
            }
        }

        public double[] Solve(IReadOnlyList<double> rhs)
        {
            var x = new double[20];
            for (int i = 0; i < _operator.Length; i++)
            {
                double v = rhs[i];
                if (v == 0.0) continue;
                var column = _operator[i];
                for (int k = 0; k < 20; k++) x[k] += column[k] * v;
            }
            return x;
        }
    }

    private static readonly Lazy<FixedModel> DefaultModel =
        new(() => new FixedModel(DefaultShapes()));

    public static Result? SolveFromDegreeSeven(
        IReadOnlyList<Shape> shapes, Func<Shape, (double Y, double Z)> degreeSeven)
    {
        if (shapes == null) throw new ArgumentNullException(nameof(shapes));
        if (degreeSeven == null) throw new ArgumentNullException(nameof(degreeSeven));

        var model = DefaultModel.Value;
        if (shapes.Count != DefaultShapes().Count)
            throw new ArgumentException(
                "SolveFromDegreeSeven is set up for the default shapes; a different set would " +
                "need its own cached model rather than silently reusing this one.");

        var values = new (double Y, double Z)[shapes.Count];
        for (int i = 0; i < shapes.Count; i++) values[i] = degreeSeven(shapes[i]);

        var rows = model.Rows;
        var rhs = new List<double>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            var v = values[model.ShapeOfRow[i]];
            rhs.Add(model.IsMeridional[i] ? v.Y : v.Z);
        }
        int used = shapes.Count;

        if (rows.Count < 20) return null;

        double[] x = model.Solve(rhs);
        var tau = new double[21];
        for (int k = 1; k <= 20; k++) tau[k] = x[k - 1];

        double worst = 0.0, scale = 1e-30;
        for (int i = 0; i < rows.Count; i++)
        {
            double pred = 0.0;
            for (int k = 0; k < 20; k++) pred += rows[i][k] * x[k];
            worst = Math.Max(worst, Math.Abs(pred - rhs[i]));
            scale = Math.Max(scale, Math.Abs(rhs[i]));
        }

        return new Result { Tau = tau, Residual = worst / scale, Used = used };
    }

    private static bool AnyNonZero(double[] v)
    {
        foreach (double t in v) if (Math.Abs(t) > 1e-18) return true;
        return false;
    }

    /// <summary>A coefficient set with one tau set to unity and everything else zero.</summary>
    private static BuchdahlTerms UnitTerms(int k)
    {
        var t = new BuchdahlTerms();
        if (k == 1) t.B7 = 1.0;
        else typeof(BuchdahlTerms).GetField("Tau" + k)!.SetValue(t, 1.0);
        return t;
    }

    /// <summary>
    /// Fits v(s) = sum over the given odd powers and returns the coefficient of s^want. The
    /// higher powers are present to absorb the ninth order and beyond, not to be believed.
    /// </summary>
    private static double OddFit(List<(double S, double V)> data, int[] powers, int want)
    {
        double[] c = OddFitAll(data, powers);
        for (int j = 0; j < powers.Length; j++) if (powers[j] == want) return c[j];
        return 0.0;
    }

    /// <summary>Every coefficient of the same fit, in the order of <paramref name="powers"/>.</summary>
    private static double[] OddFitAll(List<(double S, double V)> data, int[] powers)
    {
        int m = powers.Length;
        var rows = new List<double[]>();
        var rhs = new List<double>();
        foreach (var (s, v) in data)
        {
            var r = new double[m];
            for (int j = 0; j < m; j++) r[j] = Math.Pow(s, powers[j]);
            rows.Add(r);
            rhs.Add(v);
        }
        return LeastSquares(rows, rhs);
    }

    /// <summary>
    /// Least squares by Householder QR, with the columns scaled to unit norm first.
    ///
    /// <para>Normal equations were tried and are not good enough here. The scale ladder makes
    /// a Vandermonde, whose condition number is already large, and forming A-transpose-A
    /// squares it - which showed up as a five per cent residual on a design whose coefficients
    /// are known exactly. QR works on the matrix itself and leaves the conditioning alone.</para>
    ///
    /// <para>Columns that are identically zero - a coefficient the polynomial in use does not
    /// represent - are dropped and returned as zero rather than making the system singular.
    /// </para>
    /// </summary>
    private static double[] LeastSquares(List<double[]> rows, List<double> rhs)
    {
        int m = rows.Count, n = rows[0].Length;

        var norm = new double[n];
        var live = new List<int>();
        for (int j = 0; j < n; j++)
        {
            double acc = 0.0;
            for (int i = 0; i < m; i++) acc += rows[i][j] * rows[i][j];
            norm[j] = Math.Sqrt(acc);
            if (norm[j] > 1e-14) live.Add(j);
        }
        int k = live.Count;
        if (k == 0) return new double[n];

        var a = new double[m, k];
        for (int i = 0; i < m; i++)
            for (int j = 0; j < k; j++)
                a[i, j] = rows[i][live[j]] / norm[live[j]];
        var y = new double[m];
        for (int i = 0; i < m; i++) y[i] = rhs[i];

        // Householder reflections, applied to the matrix and the right-hand side together.
        for (int c = 0; c < k; c++)
        {
            double nrm = 0.0;
            for (int i = c; i < m; i++) nrm += a[i, c] * a[i, c];
            nrm = Math.Sqrt(nrm);
            if (nrm < 1e-300) continue;
            if (a[c, c] > 0) nrm = -nrm;

            var v = new double[m];
            for (int i = c; i < m; i++) v[i] = a[i, c];
            v[c] -= nrm;

            double vv = 0.0;
            for (int i = c; i < m; i++) vv += v[i] * v[i];
            if (vv < 1e-300) continue;

            for (int j = c; j < k; j++)
            {
                double dot = 0.0;
                for (int i = c; i < m; i++) dot += v[i] * a[i, j];
                double f = 2.0 * dot / vv;
                for (int i = c; i < m; i++) a[i, j] -= f * v[i];
            }
            double dy = 0.0;
            for (int i = c; i < m; i++) dy += v[i] * y[i];
            double fy = 2.0 * dy / vv;
            for (int i = c; i < m; i++) y[i] -= fy * v[i];
        }

        // Back substitution on the upper triangle.
        var z = new double[k];
        for (int i = k - 1; i >= 0; i--)
        {
            double acc = y[i];
            for (int j = i + 1; j < k; j++) acc -= a[i, j] * z[j];
            z[i] = Math.Abs(a[i, i]) > 1e-300 ? acc / a[i, i] : 0.0;
        }

        var x = new double[n];
        for (int j = 0; j < k; j++) x[live[j]] = z[j] / norm[live[j]];
        return x;
    }
}
