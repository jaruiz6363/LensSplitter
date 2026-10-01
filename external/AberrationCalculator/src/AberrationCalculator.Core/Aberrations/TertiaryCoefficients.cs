using System;
using System.Collections.Generic;

namespace AberrationCalculator.Core.Aberrations;

/// <summary>
/// The twenty seventh-order aberration coefficients tau1..tau20, in the form Robb's
/// polynomial consumes.
///
/// <para>Buchdahl's computing scheme produces T1..T10 and their barred partners; Table II
/// of <i>J. Opt. Soc. Am.</i> <b>48</b>, 747 (1958) converts those into the tau. The
/// conversion is not one-to-one - tau2 mixes Tbar1 with half of T2, tau7 draws on four
/// different T at once - which is why the T are computed first and converted afterwards
/// rather than being produced in Robb's form directly.</para>
///
/// <para><b>Two routines.</b> A system of spheres goes through <see cref="BuchdahlTableI"/>,
/// Buchdahl's own arrangement, verified against his printed numbers. A figured system goes
/// through <see cref="BuchdahlAsphericScheme"/> with its <see cref="BuchdahlAsphericScheme.Options.Default"/>
/// arrangement of M Sec. 85, which agrees with Forbes' series trace to 2E-10 or better on every
/// figured design tested. A figured flat facing collimated light, where the incidence ratio is
/// infinite, goes through the same routine in Laurent series arithmetic
/// (TertiaryCoefficients.FlatCollimated.cs). <see cref="Attach"/> makes the choice;
/// <see cref="Compute"/> is the spherical routine alone.</para>
/// </summary>
public static partial class TertiaryCoefficients
{
    /// <summary>
    /// The transverse tau2..tau20 of a figured system with a flat surface facing collimated light,
    /// computed in Laurent series arithmetic with that surface's curvature as the series variable.
    /// Implemented in Core alone (TertiaryCoefficients.FlatCollimated.cs); in the linked
    /// arithmetics it has no body and the call compiles away. Leaves <paramref name="transverse"/>
    /// null when the series route cannot vouch for its answer.
    /// </summary>
    static partial void FlatCollimatedTau(Models.OpticalSystem system, Scalar[] indices,
                                          Scalar maxField, List<int> flatSurfaces,
                                          ref Scalar[]? transverse, ref Scalar[]?[]? perSurface);

    /// <summary>
    /// Computes tau1..tau20 for a system. The returned array is indexed 1..20; index 0 is
    /// unused, so the numbering matches the literature rather than being off by one.
    /// </summary>
    /// <param name="perSurface">
    /// Handed each surface's own tertiary totals, T1..T10 and their barred partners, indexed
    /// 1..10 - copies of exactly what is summed into the result, so a caller can split the tau
    /// by surface and have the parts add to the whole.
    /// </param>
    public static Scalar[] Compute(
        IReadOnlyList<Models.Surface> surfaces, Scalar[] indices, Scalar efl, Scalar stopParameter,
        IReadOnlyList<Scalar[]>? aspheric = null, Scalar iota = default,
        Action<int, Scalar[], Scalar[]>? perSurface = null)
    {
        var rows = BuchdahlTableI.Compute(surfaces, indices, efl, stopParameter, aspheric,
                                          iota: iota);

        var T = new Scalar[11];
        var Tb = new Scalar[11];
        for (int i = 1; i < surfaces.Count - 1; i++)
        {
            perSurface?.Invoke(i, (Scalar[])rows[i].TertiaryTotal.Clone(),
                               (Scalar[])rows[i].TertiaryTotalBar.Clone());
            for (int m = 1; m <= 10; m++)
            {
                T[m] += rows[i].TertiaryTotal[m];
                Tb[m] += rows[i].TertiaryTotalBar[m];
            }
        }

        return AssembleTau(T, Tb);
    }

    /// <summary>
    /// Buchdahl's ten tertiary coefficients and their barred partners, read as Robb's twenty
    /// tau. Paper III Table II, and nothing else.
    ///
    /// <para>Split out so that <see cref="BuchdahlAsphericScheme"/> can reach the same tau from
    /// its own totals. The table is a fact about the two notations rather than about how the
    /// totals were arrived at, so two routes that disagree about a figured surface must still
    /// agree about this.</para>
    /// </summary>
    public static Scalar[] AssembleTau(Scalar[] T, Scalar[] Tb)
    {
        var tau = new Scalar[21];
        tau[1] = T[1];
        tau[2] = Tb[1] + T[2] / 2.0;
        tau[3] = T[2] / 2.0;
        tau[4] = Tb[2] + T[3];
        tau[5] = T[3];
        tau[6] = T[4];
        tau[7] = Tb[3] + Tb[4] / 2.0 + T[5] / 2.0 + 3.0 * T[7] / 8.0;
        tau[8] = (Tb[4] + T[5] + T[7]) / 2.0;
        tau[9] = T[5] / 2.0 + T[7] / 4.0;
        tau[10] = T[7] / 8.0;
        tau[11] = Tb[5] + T[6];
        tau[12] = Tb[7] + T[8];
        tau[13] = T[6];
        tau[14] = T[8];
        tau[15] = Tb[6] + Tb[8] / 2.0 + T[9] / 2.0;
        tau[16] = (Tb[8] + T[9]) / 2.0;
        tau[17] = T[9] / 2.0;
        tau[18] = Tb[9] + T[10];
        tau[19] = T[10];
        tau[20] = Tb[10];
        return tau;
    }

    /// <summary>M (13.4)'s iota, 1/l_01 in focal lengths; zero for an object at infinity.</summary>
    private static Scalar IotaOf(Models.OpticalSystem system, RayTrace.ParaxialResult paraxial)
    {
        Scalar t0 = system.Surfaces[0].Thickness;
        return Scalar.IsInfinity(t0) ? 0.0 : -paraxial.Efl / t0;
    }

    /// <summary>
    /// The aperture power of each tau in Robb's polynomial. Index 1..20; they run 7, 6, 6,
    /// 5, 5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 1, 1, 0, and the field power is always
    /// seven minus this.
    /// </summary>
    private static readonly int[] AperturePower =
        { 0, 7, 6, 6, 5, 5, 5, 4, 4, 4, 4, 3, 3, 3, 3, 2, 2, 2, 1, 1, 0 };


    /// <summary>
    /// The aperture power of <c>tau[n]</c> in Robb's polynomial, for n from 1 to 20; the field
    /// power is always seven minus it. Zero outside that range.
    ///
    /// <para>Exposed because <see cref="AberrationNames"/> works out what a tertiary coefficient
    /// IS from its monomial, there being no classical name to look up, and two copies of this
    /// table would be two chances to get it wrong.</para>
    /// </summary>
    public static int AperturePowerOf(int tau) =>
        tau >= 1 && tau <= 20 ? AperturePower[tau] : 0;

    /// <summary>
    /// Converts Buchdahl's coefficients into the convention the rest of this program uses.
    ///
    /// <para>Buchdahl works in units of the focal length, and his pupil and field variables
    /// are not fractions: his rho is the ray SLOPE, running from zero to the marginal angle
    /// u, and his H is the TANGENT of the field angle. So a coefficient multiplying
    /// rho^a H^b converts by u^a tan(theta)^b, and the whole displacement then scales by the
    /// focal length to reach millimetres.</para>
    ///
    /// <para><b>The aperture and length parts are confirmed; the field part is NOT.</b></para>
    ///
    /// <para>Confirmed: on a Cooke triplet the seventh-order spherical coefficient, which
    /// has a = 7 and b = 0, differs between the two pipelines by exactly EFL u^7 -
    /// 50 x (-0.1)^7 = -5e-6 against a measured -5.0000108e-6. And at fifth order,
    /// Buchdahl's S1p times EFL u^5 reproduces the macro's B5 to a ratio of 1.000. Two
    /// orders, both with b = 0.</para>
    ///
    /// <para>The field factor is confirmed too, but by a different kind of evidence. No
    /// coefficient with a non-zero field power is produced by both pipelines, so it cannot
    /// be calibrated against one the way the aperture factor was. What can be done instead
    /// is to vary it and watch the prediction: tau18 carries H^6, so a five per cent error
    /// in H is a thirty-four per cent error in that term, and the agreement below could not
    /// survive it. Scaling the field factor away from tan(theta) makes the prediction
    /// worse in both directions - see the field-factor note in `docs/verification.md` - which
    /// places it inside about five per cent. The shallow minimum sits a little above 1.00
    /// rather than exactly on it, and that offset is smaller than the truncation error it
    /// would be fitted to, so it is left alone.</para>
    ///
    /// <para><b>Measured on one lens</b> - the all-spherical Cooke triplet, at 0.55 um,
    /// centroid-referenced RMS spot radius at paraxial focus, ray aiming off. Against a
    /// ray trace the seventh-order prediction is within 1.1 per cent from the axis out to
    /// about nine tenths of the field:</para>
    ///
    /// <code>
    ///   H      +5th     +7th     ray trace   err(+7th)
    ///   0.00   0.014337 0.013788 0.013699     +0.6%
    ///   0.30   0.014993 0.014371 0.014286     +0.6%
    ///   0.50   0.017083 0.016373 0.016300     +0.4%
    ///   0.70   0.020539 0.019578 0.019480     +0.5%
    ///   0.85   0.023100 0.020930 0.020697     +1.1%
    ///   0.91   0.023944 0.020844 0.020884     -0.2%
    ///   0.95   0.024438 0.020663 0.021420     -3.5%
    ///   1.00   0.024994 0.020620 0.023603    -12.6%
    /// </code>
    ///
    /// <para>Past about H = 0.9 the truncated series turns over while the traced spot keeps
    /// climbing, and the two part company. That is the ninth order arriving, not a fault in
    /// this conversion: the departure is smooth, it is not moved by ray aiming, and nothing
    /// is being clipped. It does mean the last tenth of the field is outside what seventh
    /// order can describe on this lens.</para>
    ///
    /// <para>One lens is one lens. This triplet has little seventh-order content to begin
    /// with - third order alone overstates its full-field spot by a factor of two, and
    /// fifth order gets within six per cent - so the test shows the conversion is not
    /// wrong rather than showing it works hard. The designs whose full-field error
    /// motivated this work are ASPHERIC; their tau now come from the aspheric routine, which
    /// uses this same conversion and agrees with Forbes' transverse tau on them to 2E-10.</para>
    /// </summary>
    /// <param name="tau">Coefficients in Buchdahl's convention, indexed 1..20.</param>
    /// <param name="efl">The system's focal length.</param>
    /// <param name="marginalAngle">
    /// The marginal ray angle in image space, -1/(2 F#) for a system in air.
    /// </param>
    /// <param name="fieldTangent">Tangent of the maximum field angle.</param>
    /// <param name="sphericalSeventh">
    /// The seventh-order spherical aberration in transverse units, which REPLACES tau1 when
    /// supplied. Pass the fifth-order code's <c>Totals.B7</c>.
    ///
    /// <para>The two routes now AGREE on an asphere - the scheme's own tau1 matches the same
    /// closed form to 1e-5 over conics from -6 to +1, three decades of A4 and sixth-order
    /// figuring up to a 33 um aberration - so this is no longer a repair. It is kept because
    /// B7 remains the better-tested of the two, being checked against a closed-form mirror as
    /// well as a refractor, and because on a multi-surface system the two decompose their
    /// induced parts differently and B7 is the one validated end to end. On the aspheric
    /// triplets the two agree to 1.5% and 0.7%. See
    /// <c>ExactConicSurfaceTests</c> and `docs/verification.md`.</para>
    ///
    /// <para>Both quantities are already transverse here, so the substitution needs no
    /// conversion and cannot introduce one. On a SPHERICAL system it changes nothing: the two
    /// routes agree exactly there, which is what makes it a substitution rather than a fudge.
    /// It replaces tau1 ONLY; the other nineteen come from the scheme - the aspheric routine,
    /// for a figured system - and agree with Forbes there to 2E-10.</para>
    /// </param>
    public static Scalar[] ToTransverse(Scalar[] tau, Scalar efl, Scalar marginalAngle,
                                        Scalar fieldTangent, Scalar? sphericalSeventh = null)
    {
        if (tau == null) throw new ArgumentNullException(nameof(tau));

        var scaled = new Scalar[21];
        for (int n = 1; n <= 20; n++)
        {
            int a = AperturePower[n];
            int b = 7 - a;
            scaled[n] = tau[n] * efl
                      * SMath.Pow(marginalAngle, a)
                      * (b == 0 ? 1.0 : SMath.Pow(fieldTangent, b));
        }
        if (sphericalSeventh.HasValue) scaled[1] = sphericalSeventh.Value;
        return scaled;
    }

    /// <summary>
    /// Computes tau2..tau20 for a system and writes them into the coefficient set, in the
    /// transverse convention the rest of the program uses.
    ///
    /// <para><b>Why this exists.</b> Everything above was reachable only by assembling the
    /// scheme, the stop parameter, the aspheric increments and the transverse conversion by
    /// hand, and the only code that ever did so was the test suite. The reported coefficient
    /// set therefore carried tau1 and NINETEEN ZEROS for every design ever run through it,
    /// and <see cref="Prms"/> - which reads eighteen of the twenty - was quietly predicting
    /// spots from a set it was never given. The assembly belongs here, once, where every
    /// caller gets it.</para>
    ///
    /// <para>Systems the scheme cannot describe are left alone rather than half-filled: a
    /// coefficient set of zeros is at least honestly third-and-fifth order, where a partial
    /// one would be neither.</para>
    /// </summary>
    /// <summary>
    /// The surfaces whose tertiary can only be reached through the Laurent-series route: a
    /// FIGURED surface that is flat and faces collimated light. Empty for everything else,
    /// which is almost every design.
    ///
    /// <para><b>Why anyone outside needs to ask.</b> There the marginal incidence is identically
    /// zero, the incidence ratio q is infinite, and the finite coefficients arrive only after
    /// terms carrying different powers of q cancel. <see cref="Attach"/> handles it by running
    /// the whole chain again in Laurent series arithmetic with that surface's curvature as the
    /// variable - but that route is implemented in Core alone. In the linked arithmetics the
    /// call compiles away to nothing, so a caller differentiating this chain would get a value
    /// that is right and a derivative that is not, with nothing to say so. A caller that cannot
    /// live with that has to be able to detect the case before it runs, and this is how.</para>
    ///
    /// <para>The condition is read off the scheme rather than re-derived from the geometry, so
    /// that it cannot drift from the one <see cref="Attach"/> actually branches on.</para>
    /// </summary>
    public static List<int> SeriesOnlySurfaces(Models.OpticalSystem system, Scalar[] indices,
                                               RayTrace.ParaxialResult paraxial)
    {
        var found = new List<int>();
        if (system == null || indices == null || paraxial == null) return found;

        int stop = system.StopSurfaceIndex;
        if (stop < 0 || stop >= system.Surfaces.Count) return found;

        bool anyFigured = false;
        int last = system.LastOpticalSurface();
        for (int i = 1; i <= last && i < system.Surfaces.Count; i++)
            if (system.Surfaces[i].IsFigured) { anyFigured = true; break; }
        if (!anyFigured) return found;

        var scheme = BuchdahlScheme.Compute(system.Surfaces, indices, paraxial.Efl,
                                            system.Surfaces[stop].SemiDiameter,
                                            IotaOf(system, paraxial));
        var rows = BuchdahlTableI.Compute(system.Surfaces, indices, paraxial.Efl, scheme.P,
                                          iota: IotaOf(system, paraxial));

        for (int i = 1; i <= last && i < rows.Length; i++)
            if (rows[i] != null && rows[i].FlatInCollimatedSpace && system.Surfaces[i].IsFigured)
                found.Add(i);

        return found;
    }

    /// <summary>
    /// The indices with a reflection carried in their sign, as <see cref="RayTrace.ParaxialTrace"/>
    /// carries it: negated after an odd number of mirrors.
    ///
    /// <para>The scheme's recurrences take a mirror as a refraction into index -n, and nothing
    /// else tells them a surface reflects. Handed the plain indices, a mirror is a curved surface
    /// with no index step - no power at all, while the paraxial data it is scaled by says f = 100
    /// on the parabola - and tau2..tau20 came out NaN there. The fifth-order code never had the
    /// problem because it reads the signed indices from the paraxial trace.</para>
    ///
    /// <para>The same array comes back untouched when nothing reflects, so a refracting design
    /// computes exactly what it always did. Signing is idempotent, which matters because some of
    /// what is downstream re-traces from the indices it is given.</para>
    /// </summary>
    internal static Scalar[] SignedIndices(Models.OpticalSystem system, Scalar[] indices)
    {
        bool anyMirror = false;
        foreach (var s in system.Surfaces) if (s.IsMirror) { anyMirror = true; break; }
        if (!anyMirror) return indices;

        var signed = new Scalar[indices.Length];
        Scalar sign = 1.0;
        for (int i = 0; i < indices.Length; i++)
        {
            if (i < system.Surfaces.Count && system.Surfaces[i].IsMirror) sign = -sign;
            signed[i] = sign * SMath.Abs(indices[i]);
        }
        return signed;
    }

    public static void Attach(Models.OpticalSystem system, Scalar[] indices,
                              RayTrace.ParaxialResult paraxial, BuchdahlResult coefficients,
                              Scalar maxField)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        if (coefficients == null) throw new ArgumentNullException(nameof(coefficients));

        // With no field, tau2..tau20 all multiply a power of it and are exactly zero, which is
        // what BuchdahlCoefficients already set. (Run anyway they came out as -0.)
        if (coefficients.OnAxisOnly) return;

        int stop = system.StopSurfaceIndex;
        if (stop < 0 || stop >= system.Surfaces.Count) return;
        if (SMath.Abs(coefficients.FNumber) < 1e-12) return;

        indices = SignedIndices(system, indices);

        var scheme = BuchdahlScheme.Compute(system.Surfaces, indices, paraxial.Efl,
                                            system.Surfaces[stop].SemiDiameter,
                                            IotaOf(system, paraxial));

        // The conjugate, which enters ONLY through the starting values of the two rays.
        // III says so outright - "the choice of different coordinate systems reflects itself
        // only in the starting values of y_p, v_p, y_q, v_q (cf. M Secs. 12-13)" - and then
        // sends the reader to the monograph for what they are. M (13.4), reduced OT: the p
        // ray leaves at unit height with reduced angle iota = 1/l_01, in focal lengths.
        Scalar objectDistance = system.Surfaces[0].Thickness;
        bool infinite = Scalar.IsInfinity(objectDistance);
        Scalar iota = infinite ? 0.0 : -paraxial.Efl / objectDistance;

        // p is the entrance pupil position in focal lengths at ANY conjugate: by (13.4) the
        // chief ray has S = 0, so its height over its angle at surface one is exactly p. The
        // scheme derives p instead as the q/p ray-height ratio at the stop, which holds only
        // while the two conventions for the q ray differ by p times the p ray - an identity
        // that fails once iota is non-zero. The derived value is kept at infinity so the
        // results validated there stay bit-identical.
        Scalar stopParameter = infinite
            ? scheme.P
            : paraxial.EntrancePupilPosition / paraxial.Efl;

        // The aspheric increments need the all-spherical scheme to difference against, which
        // is the same run the increments are then fed back into. Build returns null when no
        // surface is figured, and the scheme takes that as "spherical throughout".
        //
        // AT THE STOP PARAMETER THE COEFFICIENTS ARE ACTUALLY COMPUTED AT, which is not
        // scheme.P at a finite conjugate. The note above says why scheme.P is wrong there -
        // it is a derived value resting on an identity that iota breaks - and this table used
        // it anyway while the run it is differenced against used the paraxial one. A figured
        // system at a finite conjugate was therefore taking its increments against a reference
        // built for a different pupil, and came out about 5E-5 from Forbes' series trace where
        // spheres agree to 5E-15. Spheres never saw it, because Build returns null for them and
        // this table is then unused; an infinite conjugate never saw it either, because the two
        // stop parameters are the same expression there. The combination is in no fixture.
        var spherical = BuchdahlTableI.Compute(system.Surfaces, indices, paraxial.Efl,
                                               stopParameter, iota: iota);
        var increments = AsphericSchemeIncrements.Build(coefficients, spherical,
                                                        system.LastOpticalSurface());

        // The conversion, M (31.11) with the variables of (13.5) and (31.13). Each factor
        // collapses to the one used before when iota is zero, which is the regression guard:
        // v'_pk becomes one, g becomes one, and the object height over the object distance
        // becomes the tangent of the field angle.
        Scalar g = 1.0 - stopParameter * iota;
        // |N'|, not the signed index. After an odd number of mirrors the signed image index is
        // negative, and dividing by it turns the sign of every tau - which is exactly the trap
        // BuchdahlCoefficients documents, and avoids, for the F/number the third and fifth orders
        // are scaled by. Taking it signed here put the seventh order in the opposite frame from
        // the orders below it on a mirror, and Prms multiplies them together. One whenever there
        // is no mirror, so nothing else moves.
        Scalar lengthFactor = paraxial.Efl
                            / (SMath.Abs(paraxial.N[system.LastOpticalSurface()]) * scheme.PRayFinalAngle);
        Scalar u = -(0.5 * paraxial.Epd / paraxial.Efl) / g;

        // The field variable multiplies the q ray, and that ray is started in REDUCED
        // coordinates - a plain angle of 1/N_0, so that the pair carries a Lagrange invariant
        // of one, which the scheme requires and which an immersed object space would otherwise
        // break. See the long note at the ray start in BuchdahlTableI. The physical chief ray's
        // plain angle is therefore N_0 times the q ray it is expressed in, and that factor
        // belongs here. One when object space is air, which is every design in this repository
        // but the immersed E fixtures built to test exactly this.
        Scalar nObject = SMath.Abs(paraxial.N[0]);
        if (nObject < 1e-12) nObject = 1.0;

        Scalar hmax = nObject * (infinite
            ? SMath.Tan(maxField * SMath.PI / 180.0)
            : -(paraxial.ParaxialImageHeight / paraxial.Magnification) / objectDistance);

        // The two routines. Spheres keep Buchdahl's own arrangement, bit for bit as validated; a
        // figured system takes the Sec. 85 arrangement, which needs the dual run's increments for
        // its sixth barred q member.
        // Each surface's own tertiary totals, kept as they are summed, so that the tau can be
        // split by surface below. Both routes sum their surfaces into the system totals, and
        // Table II and the transverse conversion are linear, so the parts add to the whole.
        int count = system.Surfaces.Count;
        var surfaceT = new Scalar[count][];
        var surfaceTbar = new Scalar[count][];

        Scalar[] raw;
        if (increments == null)
        {
            raw = Compute(system.Surfaces, indices, paraxial.Efl, stopParameter, null, iota,
                          (i, T, Tb) => { surfaceT[i] = T; surfaceTbar[i] = Tb; });
        }
        else
        {
            // The same stop parameter as the direct increments, for the same reason - its
            // own documentation says it must be "the stop parameter the direct increments
            // were bridged at", and at a finite conjugate that is no longer scheme.P.
            var dualIncrements = AsphericSchemeIncrements.BuildDual(system, paraxial, indices,
                                                                    stopParameter, iota);
            raw = BuchdahlAsphericScheme.Tau(system.Surfaces, indices, paraxial.Efl, stopParameter,
                                             increments, iota, BuchdahlAsphericScheme.Options.Default,
                                             dualIncrements,
                                             (i, hat, hatBar, check, checkBar) =>
                                             {
                                                 // (85.3): a surface's total is its two passes.
                                                 var T = new Scalar[11];
                                                 var Tb = new Scalar[11];
                                                 for (int m = 1; m <= 10; m++)
                                                 {
                                                     T[m] = hat[m] + check[m];
                                                     Tb[m] = hatBar[m] + checkBar[m];
                                                 }
                                                 surfaceT[i] = T;
                                                 surfaceTbar[i] = Tb;
                                             });
        }

        // Per surface, in the same transverse convention as the totals. No B7 substitution:
        // tau1 is not stored per surface, the fifth-order code's own per-surface B7 being used.
        var surfaceTau = new Scalar[]?[count];
        for (int i = 1; i < count - 1; i++)
            if (surfaceT[i] != null)
                surfaceTau[i] = ToTransverse(AssembleTau(surfaceT[i], surfaceTbar[i]),
                                             lengthFactor, u, hmax);

        var tau = ToTransverse(raw, lengthFactor, u, hmax, coefficients.Totals.B7);

        // A figured flat facing collimated light makes q infinite, and every formula above that
        // divides by its incidence has no finite form there. The same formulas in Laurent series
        // arithmetic, with that surface's curvature as the variable, give the finite answer as
        // their zeroth-order coefficient; where that route vouches for itself it replaces
        // tau2..tau20. tau1 stays the fifth-order code's B7, as everywhere else.
        if (increments != null)
        {
            var flat = new List<int>();
            for (int i = 1; i < system.Surfaces.Count - 1 && i < spherical.Length; i++)
                if (spherical[i].FlatInCollimatedSpace && i < increments.Length && increments[i] != null)
                    flat.Add(i);

            if (flat.Count > 0)
            {
                Scalar[]? series = null;
                Scalar[]?[]? seriesPerSurface = null;
                FlatCollimatedTau(system, indices, maxField, flat, ref series, ref seriesPerSurface);
                if (series != null)
                {
                    for (int k = 2; k <= 20; k++) tau[k] = series[k];
                    // Null when a surface's own share is not finite: its divergent terms cancel
                    // only against its neighbours', and a split would print their e^0 parts as if
                    // they meant something. Then nothing is attributed, and the report says so.
                    surfaceTau = seriesPerSurface ?? new Scalar[]?[count];
                    coefficients.TertiaryUnattributed = seriesPerSurface == null;
                }
            }
        }

        // Stored unscaled, as every other per-surface coefficient is: the totals carry the
        // F/number, so these times it sum to them.
        for (int i = 1; i < count - 1 && i < coefficients.PerSurface.Length; i++)
        {
            var s = coefficients.PerSurface[i];
            if (s == null) continue;
            var st = surfaceTau[i];
            for (int k = 2; k <= 20; k++)
                s.SetTau(k, st != null ? st[k] / coefficients.FNumber : 0.0);
        }

        var t = coefficients.Totals;
        t.Tau2 = tau[2];   t.Tau3 = tau[3];   t.Tau4 = tau[4];   t.Tau5 = tau[5];
        t.Tau6 = tau[6];   t.Tau7 = tau[7];   t.Tau8 = tau[8];   t.Tau9 = tau[9];
        t.Tau10 = tau[10]; t.Tau11 = tau[11]; t.Tau12 = tau[12]; t.Tau13 = tau[13];
        t.Tau14 = tau[14]; t.Tau15 = tau[15]; t.Tau16 = tau[16]; t.Tau17 = tau[17];
        t.Tau18 = tau[18]; t.Tau19 = tau[19]; t.Tau20 = tau[20];
    }
}
