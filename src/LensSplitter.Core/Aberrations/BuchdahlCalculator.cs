using LensSplitter.Core.Models;
using LensSplitter.Core.Paraxial;

namespace LensSplitter.Core.Aberrations;

/// <summary>
/// Per-surface Buchdahl 5th-order contribution data.
/// Primary: σ-dependent 5th-order (ti[46..51]).
/// Secondary Intrinsic: surface's own 5th-order from geometry (ti[5]*ti[83..88]).
/// Secondary Induced: cross-surface interactions via prefix sums.
/// All secondary values are effective (unbarred + P*barred).
/// </summary>
public class BuchdahlSurfaceContribution
{
    public int SurfaceIndex { get; set; }

    // Primary contributions
    public double Ap { get; set; }
    public double Bp { get; set; }
    public double Cp { get; set; }
    public double Aq { get; set; }
    public double Bq { get; set; }
    public double Cq { get; set; }

    // Secondary intrinsic (effective = unbarred + P*barred)
    public double IntrinsicAp { get; set; }
    public double IntrinsicBp { get; set; }
    public double IntrinsicCp { get; set; }
    public double IntrinsicAq { get; set; }
    public double IntrinsicBq { get; set; }
    public double IntrinsicCq { get; set; }

    // Secondary induced (effective = unbarred + P*barred)
    public double InducedAp { get; set; }
    public double InducedBp { get; set; }
    public double InducedCp { get; set; }
    public double InducedAq { get; set; }
    public double InducedBq { get; set; }
    public double InducedCq { get; set; }
}

/// <summary>
/// Buchdahl 5th-order aberration coefficients.
/// 5th-order transverse ray displacement at image:
///   delta_xi = ap*rho^5 + bp*rho^3*eta^2 + cp*rho*eta^4     (sagittal)
///   delta_eta = aq*rho^4*eta + bq*rho^2*eta^3 + cq*eta^5      (tangential)
/// where rho = normalized aperture, eta = normalized field.
/// </summary>
public class BuchdahlResult
{
    // Primary displacement coefficients (p-ray, sagittal)
    public double Ap { get; set; }
    public double Bp { get; set; }
    public double Cp { get; set; }

    // Primary displacement coefficients (q-ray, tangential)
    public double Aq { get; set; }
    public double Bq { get; set; }
    public double Cq { get; set; }

    // Bar displacement coefficients
    public double ApBar { get; set; }
    public double BpBar { get; set; }
    public double CpBar { get; set; }
    public double AqBar { get; set; }
    public double BqBar { get; set; }
    public double CqBar { get; set; }

    // Secondary aberration sums
    public double S1p { get; set; }
    public double S2p { get; set; }
    public double S3p { get; set; }
    public double S4p { get; set; }
    public double S5p { get; set; }
    public double S6p { get; set; }

    // Secondary bar sums
    public double S1pBar { get; set; }
    public double S2pBar { get; set; }
    public double S3pBar { get; set; }
    public double S4pBar { get; set; }
    public double S5pBar { get; set; }
    public double S6pBar { get; set; }

    // Stop eccentricity parameter
    public double P { get; set; }

    // EFL used for normalization (needed to scale Buchdahl coefficients to Seidel scale)
    public double Efl { get; set; }

    // Per-surface primary contributions
    public List<BuchdahlSurfaceContribution> SurfaceContributions { get; set; } = new();

    /// <summary>
    /// Effective coefficient: ap + P * ap_bar
    /// </summary>
    public double ApEffective => Ap + P * ApBar;

    /// <summary>
    /// Effective coefficient: bp + P * bp_bar
    /// </summary>
    public double BpEffective => Bp + P * BpBar;

    /// <summary>
    /// Effective coefficient: cp + P * cp_bar
    /// </summary>
    public double CpEffective => Cp + P * CpBar;

    /// <summary>
    /// Effective coefficient: aq + P * aq_bar
    /// </summary>
    public double AqEffective => Aq + P * AqBar;

    /// <summary>
    /// Effective coefficient: bq + P * bq_bar
    /// </summary>
    public double BqEffective => Bq + P * BqBar;

    /// <summary>
    /// Effective coefficient: cq + P * cq_bar
    /// </summary>
    public double CqEffective => Cq + P * CqBar;

    /// <summary>
    /// Total 5th-order aberration magnitude (RSS of effective coefficients).
    /// Uses stop-dependent effective coefficients:
    ///   ap_eff = ap + p*ap_bar, etc.
    /// </summary>
    public double TotalMagnitude
    {
        get
        {
            double apEff = ApEffective;
            double bpEff = BpEffective;
            double cpEff = CpEffective;
            double aqEff = AqEffective;
            double bqEff = BqEffective;
            double cqEff = CqEffective;
            return Math.Sqrt(
                apEff * apEff + bpEff * bpEff + cpEff * cpEff +
                aqEff * aqEff + bqEff * bqEff + cqEff * cqEff);
        }
    }

    public override string ToString()
    {
        return $"Ap={Ap:E3}, Bp={Bp:E3}, Cp={Cp:E3}, Aq={Aq:E3}, Bq={Bq:E3}, Cq={Cq:E3}, P={P:F4}, Mag={TotalMagnitude:E3}";
    }
}

/// <summary>
/// Calculates Buchdahl 5th-order aberration coefficients for an optical system.
/// Translated from C++ compute_buchdahl_coefficients (BuchdahlCoefficients.cpp).
///
/// The calculator performs its own internal paraxial trace using EFL-normalized
/// coordinates, computing canonical p-ray (y=1, v=0) and q-ray (y=0, v=1) traces
/// to build the 102 intermediate terms needed for the Buchdahl formulation.
/// </summary>
public class BuchdahlCalculator
{
    private readonly ParaxialRayTracer _tracer = new();

    private const double Epsilon = 1e-12;

    private static double SafeInverse(double value)
    {
        return Math.Abs(value) > Epsilon ? 1.0 / value : 0.0;
    }

    private static double SafeDivide(double numerator, double denominator)
    {
        return Math.Abs(denominator) > Epsilon ? numerator / denominator : 0.0;
    }

    /// <summary>
    /// Calculates Buchdahl 5th-order aberration coefficients for the given optical system.
    /// </summary>
    public BuchdahlResult Calculate(OpticalSystem system, double wavelengthMicrons)
    {
        var result = new BuchdahlResult();
        int numSurfaces = system.Surfaces.Count;

        if (numSurfaces < 3)
            return result;

        // 1. Compute EFL for normalization
        double efl = _tracer.CalculateEfl(system, wavelengthMicrons);
        result.Efl = efl;
        double scale = Math.Abs(efl) > Epsilon ? 1.0 / efl : 1.0;
        if (scale == 0.0)
            scale = 1.0;

        // 2. Allocate intermediate arrays
        const int kMaxTerms = 141;
        var t = new double[numSurfaces][];
        for (int i = 0; i < numSurfaces; i++)
            t[i] = new double[kMaxTerms];

        // Per-surface secondary aberration contributions
        var s1p = new double[numSurfaces];
        var s1pBar = new double[numSurfaces];
        var s2p = new double[numSurfaces];
        var s2pBar = new double[numSurfaces];
        var s3p = new double[numSurfaces];
        var s3pBar = new double[numSurfaces];
        var s4p = new double[numSurfaces];
        var s4pBar = new double[numSurfaces];
        var s5p = new double[numSurfaces];
        var s5pBar = new double[numSurfaces];
        var s6p = new double[numSurfaces];
        var s6pBar = new double[numSurfaces];

        // 3. Canonical paraxial rays (EFL-normalized)
        double p_y = 1.0;
        double p_v = 0.0;
        double q_y = 0.0;
        double q_v = 1.0;
        double p_y_prime = 0.0;
        double p_v_prime = 0.0;
        double q_y_prime = 0.0;
        double q_v_prime = 0.0;

        double stopPosition = 0.0;
        double buchdahlDenom = 0.0;
        bool stopFound = false;

        // 4. Main surface loop: trace rays and build intermediate terms
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            var surface = system.Surfaces[i];
            var prevSurface = system.Surfaces[i - 1];

            double nPrev = prevSurface.GetRefractiveIndex(wavelengthMicrons);
            double nCurr = surface.GetRefractiveIndex(wavelengthMicrons);
            if (Math.Abs(nCurr) < Epsilon)
                nCurr = 1.0;

            double k = SafeDivide(nPrev, nCurr);
            double k1 = 1.0 - k;

            // EFL-normalized curvature and thickness
            double c = surface.Curvature * SafeInverse(scale);
            double d = surface.Thickness * scale;

            // Incidence angles
            double i1_p = c * p_y - p_v;
            double i1_q = c * q_y - q_v;

            // Refracted angles
            double i1_p_prime = k * i1_p;
            double i1_q_prime = k * i1_q;

            double N = nPrev;

            // Combined refraction + transfer
            p_v_prime = k1 * c * p_y + k * p_v;
            p_y_prime = (1.0 - k1 * c * d) * p_y - k * d * p_v;
            q_v_prime = k1 * c * q_y + k * q_v;
            q_y_prime = (1.0 - k1 * c * d) * q_y - k * d * q_v;

            // Build intermediate terms t[1..8]
            var ti = t[i];
            ti[1] = p_v;
            ti[2] = p_v_prime;
            ti[3] = q_v;
            ti[4] = q_v_prime;
            if (Math.Abs(c) > Epsilon)
                ti[5] = N * SafeInverse(c) * i1_p;
            ti[6] = i1_p - i1_p_prime;
            if (Math.Abs(c) > Epsilon)
                ti[7] = N * SafeInverse(c) * i1_q;
            ti[8] = i1_q - i1_q_prime;

            // t[9..27]: Products of slopes/angles
            ti[9] = i1_p * i1_p_prime;
            ti[10] = i1_p * i1_q_prime;
            ti[11] = i1_q * i1_q_prime;
            ti[12] = p_v * p_v;
            ti[13] = p_v * p_v_prime;
            ti[14] = p_v_prime * p_v_prime;
            ti[15] = q_v * q_v;
            ti[16] = q_v * q_v_prime;
            ti[17] = q_v_prime * q_v_prime;
            ti[18] = p_v * q_v_prime + q_v * p_v_prime;
            ti[19] = p_v * q_v;
            ti[20] = p_v_prime * q_v_prime;
            if (Math.Abs(k1) > Epsilon)
                ti[21] = k / (k1 * k1);
            ti[22] = ti[9] + ti[14];
            ti[23] = ti[10] + ti[20];
            ti[24] = ti[11] + ti[17];
            ti[25] = ti[12] - ti[14];
            ti[26] = ti[19] - ti[20];
            ti[27] = ti[15] - ti[17];

            // t[28..45]: Triple products
            ti[28] = ti[6] * ti[22];
            ti[29] = ti[6] * ti[23];
            ti[30] = ti[6] * ti[24];
            ti[31] = ti[8] * ti[22];
            ti[32] = ti[8] * ti[23];
            ti[33] = ti[8] * ti[24];
            ti[34] = ti[2] * ti[25];
            ti[35] = ti[2] * ti[26];
            ti[36] = ti[2] * ti[27];
            ti[37] = ti[4] * ti[25];
            ti[38] = ti[4] * ti[26];
            ti[39] = ti[4] * ti[27];
            ti[40] = 0.5 * (ti[28] + ti[34]);
            ti[41] = ti[29] + ti[35];
            ti[42] = 0.5 * (ti[30] + ti[36]);
            ti[43] = 0.5 * (ti[31] + ti[37]);
            ti[44] = ti[32] + ti[38];
            ti[45] = 0.5 * (ti[33] + ti[39]);

            // t[46..57]: Primary 5th-order contributions
            ti[46] = ti[5] * ti[40];
            ti[47] = ti[5] * ti[41];
            ti[48] = ti[5] * ti[42];
            ti[49] = ti[5] * ti[43];
            ti[50] = ti[5] * ti[44];
            ti[51] = ti[5] * ti[45];
            ti[52] = ti[7] * ti[40];
            ti[53] = ti[7] * ti[41];
            ti[54] = ti[7] * ti[42];
            ti[55] = ti[7] * ti[43];
            ti[56] = ti[7] * ti[44];
            ti[57] = ti[7] * ti[45];

            // Conic (aspherical) corrections to primary terms (Seidel level).
            // a4 = K·(n'-n)·c³ corrects Seidel aberrations for conic deformation.
            double conicK = surface.Conic;
            if (Math.Abs(conicK) > Epsilon && Math.Abs(c) > Epsilon)
            {
                double dn = nCurr - nPrev;
                double c2 = c * c;
                double c3 = c2 * c;
                double h = p_y;
                double hbar = q_y;
                double h2 = h * h;
                double hbar2 = hbar * hbar;

                double a4 = conicK * dn * c3;

                double dAp = 0.5 * a4 * h2 * h2;
                double dBp = a4 * h2 * h * hbar;
                double dCp = 0.5 * a4 * h2 * hbar2;
                double dAq = 0.5 * a4 * h2 * h * hbar;
                double dBq = a4 * h2 * hbar2;
                double dCq = 0.5 * a4 * h * hbar2 * hbar;

                ti[46] += dAp;
                ti[47] += dBp;
                ti[48] += dCp;
                ti[49] += dAq;
                ti[50] += dBq;
                ti[51] += dCq;

                if (Math.Abs(ti[5]) > Epsilon)
                {
                    double barRatio = ti[7] / ti[5];
                    ti[52] += barRatio * dAp;
                    ti[53] += barRatio * dBp;
                    ti[54] += barRatio * dCp;
                    ti[55] += barRatio * dAq;
                    ti[56] += barRatio * dBq;
                    ti[57] += barRatio * dCq;
                }
            }

            // t[70..82]: Additional intermediate combinations
            ti[70] = -0.25 * (3.0 * ti[12] + ti[14]);
            ti[71] = -0.5 * (3.0 * ti[19] + ti[20]);
            ti[72] = -0.25 * (3.0 * ti[15] + ti[17]);
            ti[73] = ti[70] + 0.75 * ti[22];
            ti[74] = ti[71] + 1.5 * ti[23];
            ti[75] = ti[72] + 0.75 * ti[24];
            ti[76] = ti[70] + ti[13];
            ti[77] = ti[71] + ti[18];
            ti[78] = ti[72] + ti[16];
            ti[79] = ti[21] * ti[6];
            ti[80] = 0.5 * (ti[1] * ti[22] + ti[79] * ti[25]);
            ti[81] = ti[1] * ti[23] + ti[79] * ti[26];
            ti[82] = 0.5 * (ti[1] * ti[24] + ti[79] * ti[27]);

            // t[83..88]: Tertiary combinations (spherical intrinsic 5th-order)
            ti[83] = ti[40] * ti[73] + ti[76] * ti[80];
            ti[84] = ti[41] * ti[73] + ti[40] * ti[74] + ti[76] * ti[81] + ti[77] * ti[80];
            ti[85] = ti[42] * ti[73] + ti[40] * ti[75] + ti[76] * ti[82] + ti[78] * ti[80];
            ti[86] = ti[41] * ti[74] + ti[77] * ti[81];
            ti[87] = ti[42] * ti[74] + ti[41] * ti[75] + ti[77] * ti[82] + ti[78] * ti[81];
            ti[88] = ti[42] * ti[75] + ti[78] * ti[82];


            // Stop position computation
            if (!stopFound && surface.IsStop)
            {
                double denom = p_y - p_v_prime * d;
                double numerator = q_y - q_v_prime * d;
                if (Math.Abs(denom) > Epsilon)
                {
                    stopPosition = -numerator / denom;
                    buchdahlDenom = denom;
                    result.P = stopPosition;
                    stopFound = true;
                }
            }

            // Store per-surface primary contributions
            result.SurfaceContributions.Add(new BuchdahlSurfaceContribution
            {
                SurfaceIndex = i,
                Ap = ti[46],
                Bp = ti[47],
                Cp = ti[48],
                Aq = ti[49],
                Bq = ti[50],
                Cq = ti[51]
            });

            // Update ray state for next surface
            q_v = q_v_prime;
            p_v = p_v_prime;
            q_y = q_y_prime;
            p_y = p_y_prime;
        }

        // 5. Compute prefix sums (t[58..69])
        for (int kk = 0; kk <= 6; kk += 6)
        {
            for (int termOffset = 0; termOffset < 6; termOffset++)
            {
                for (int i = 1; i < numSurfaces - 1; i++)
                {
                    double sum = 0.0;
                    for (int j = 0; j < i; j++)
                        sum += t[j][46 + kk + termOffset];
                    t[i][58 + kk + termOffset] = sum;
                }
            }
        }

        // 6. Compute additional intermediates (t[89..102])
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            var ti = t[i];
            ti[89] = ti[12] - ti[64];
            ti[90] = 2.0 * ti[67] + 3.0 * ti[62];
            ti[91] = ti[89] + ti[59];
            ti[92] = ti[19] - ti[62];
            ti[93] = ti[92] - 2.0 * ti[67];
            ti[94] = ti[12] + ti[61];
            ti[95] = ti[94] + ti[59];
            ti[96] = ti[66] - ti[68];
            ti[97] = ti[19] + ti[65];
            ti[98] = ti[96] - ti[68];
            ti[99] = ti[97] + 2.0 * ti[60];
            ti[100] = ti[67] + ti[66];
            ti[101] = ti[19] + ti[64];
            ti[102] = ti[100] + ti[60];
        }

        // Combined terms (t[103..108])
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            var ti = t[i];
            ti[103] = ti[90] * ti[41] + ti[61] * ti[91] + ti[92] * ti[46] + ti[93] * ti[58];
            ti[104] = ti[92] * ti[47] + ti[91] * ti[59] + ti[90] * ti[42] + ti[62] * ti[92];
            ti[105] = ti[92] * ti[48] + ti[91] * ti[60] + ti[90] * ti[43] + ti[63] * ti[92];
            ti[106] = ti[92] * ti[49] + ti[91] * ti[62] + ti[90] * ti[44] + ti[64] * ti[92];
            ti[107] = ti[92] * ti[50] + ti[91] * ti[63] + ti[90] * ti[45] + ti[65] * ti[92];
            ti[108] = ti[92] * ti[51] + ti[91] * ti[64] + ti[90] * ti[46] + ti[66] * ti[92];
        }

        // 7. Compute secondary contributions per surface (intrinsic + induced)
        double pStop = result.P;
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            var ti = t[i];
            s1p[i] = -3.0 * ti[61] * ti[46] + ti[58] * ti[52] + ti[58] * ti[47] + ti[5] * ti[83];
            s1pBar[i] = -ti[67] * ti[46] + ti[89] * ti[52] + ti[58] * ti[53] + ti[7] * ti[83];
            s2p[i] = -ti[90] * ti[46] + ti[59] * ti[52] + ti[91] * ti[47] + ti[103] + 2.0 * ti[58] * ti[48] + ti[5] * ti[84];
            s2pBar[i] = -ti[68] * ti[46] + ti[93] * ti[52] - ti[67] * ti[47] + ti[95] * ti[53] + 2.0 * ti[58] * ti[54] + ti[7] * ti[84];
            s3p[i] = -3.0 * ti[63] * ti[46] + ti[60] * ti[52] + 0.5 * ti[19] * ti[47] + ti[94] * ti[48] + 0.5 * ti[105] + ti[5] * ti[85];
            s3pBar[i] = -ti[69] * ti[46] + ti[96] * ti[52] + 0.5 * ti[19] * ti[53] - ti[67] * ti[48] + 3.0 * ti[64] * ti[54] + ti[7] * ti[85];
            s4p[i] = 2.0 * ti[104] + ti[92] * ti[47] + ti[59] * ti[53] + 2.0 * ti[59] * ti[48] + ti[5] * ti[86];
            s4pBar[i] = -2.0 * ti[68] * ti[52] - ti[68] * ti[47] + ti[97] * ti[53] + 2.0 * ti[59] * ti[54] + ti[7] * ti[86];
            s5p[i] = 2.0 * ti[106] + ti[98] * ti[47] + ti[60] * ti[53] + ti[99] * ti[48] + 0.5 * ti[107] + ti[5] * ti[87];
            s5pBar[i] = -2.0 * ti[69] * ti[52] - ti[69] * ti[47] + ti[101] * ti[53] - ti[68] * ti[48] + ti[102] * ti[54] + ti[7] * ti[87];
            s6p[i] = ti[108] + ti[100] * ti[48] + ti[60] * ti[54] + ti[5] * ti[88];
            s6pBar[i] = -ti[69] * ti[53] - ti[69] * ti[48] + 3.0 * ti[66] * ti[54] + ti[7] * ti[88];

            // Decompose secondary into intrinsic (last term) and induced (rest)
            var contrib = result.SurfaceContributions[i - 1];

            double intr1 = ti[5] * ti[83];  double intr1Bar = ti[7] * ti[83];
            double intr2 = ti[5] * ti[84];  double intr2Bar = ti[7] * ti[84];
            double intr3 = ti[5] * ti[85];  double intr3Bar = ti[7] * ti[85];
            double intr4 = ti[5] * ti[86];  double intr4Bar = ti[7] * ti[86];
            double intr5 = ti[5] * ti[87];  double intr5Bar = ti[7] * ti[87];
            double intr6 = ti[5] * ti[88];  double intr6Bar = ti[7] * ti[88];

            contrib.IntrinsicAp = intr1 + pStop * intr1Bar;
            contrib.IntrinsicBp = intr2 + pStop * intr2Bar;
            contrib.IntrinsicCp = intr3 + pStop * intr3Bar;
            contrib.IntrinsicAq = intr4 + pStop * intr4Bar;
            contrib.IntrinsicBq = intr5 + pStop * intr5Bar;
            contrib.IntrinsicCq = intr6 + pStop * intr6Bar;

            contrib.InducedAp = (s1p[i] - intr1) + pStop * (s1pBar[i] - intr1Bar);
            contrib.InducedBp = (s2p[i] - intr2) + pStop * (s2pBar[i] - intr2Bar);
            contrib.InducedCp = (s3p[i] - intr3) + pStop * (s3pBar[i] - intr3Bar);
            contrib.InducedAq = (s4p[i] - intr4) + pStop * (s4pBar[i] - intr4Bar);
            contrib.InducedBq = (s5p[i] - intr5) + pStop * (s5pBar[i] - intr5Bar);
            contrib.InducedCq = (s6p[i] - intr6) + pStop * (s6pBar[i] - intr6Bar);
        }

        // 8. Sum primary coefficients
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            result.Ap += t[i][46];
            result.Bp += t[i][47];
            result.Cp += t[i][48];
            result.Aq += t[i][49];
            result.Bq += t[i][50];
            result.Cq += t[i][51];
            result.ApBar += t[i][52];
            result.BpBar += t[i][53];
            result.CpBar += t[i][54];
            result.AqBar += t[i][55];
            result.BqBar += t[i][56];
            result.CqBar += t[i][57];
        }

        // 9. Sum secondary aberration contributions
        for (int i = 1; i < numSurfaces - 1; i++)
        {
            result.S1p += s1p[i];
            result.S1pBar += s1pBar[i];
            result.S2p += s2p[i];
            result.S2pBar += s2pBar[i];
            result.S3p += s3p[i];
            result.S3pBar += s3pBar[i];
            result.S4p += s4p[i];
            result.S4pBar += s4pBar[i];
            result.S5p += s5p[i];
            result.S5pBar += s5pBar[i];
            result.S6p += s6p[i];
            result.S6pBar += s6pBar[i];
        }

        return result;
    }
}
