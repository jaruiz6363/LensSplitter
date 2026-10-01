using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Reads OSLO .len lens files.
    /// Surfaces delimited by NXT keyword, properties via RD/TH/GLA/AST/AP/CC/AD-AH/RFH keywords.
    /// </summary>
    public static class OsloReader
    {
        public static OpticalSystem Read(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var system = new OpticalSystem();

            var surfaces = new List<Surface>();
            var wavelengths = new List<double>();
            var wavelengthWeights = new List<double>();
            // Deferred thickness pickups: (surfaceIndex, sourceSurfaceOffset, addedValue, isMirrorFlip)
            var thicknessPickups = new List<(int surfIdx, int offset, double addedValue, bool negate)>();
            // Track surfaces that need a marginal ray height solve (CALLBACK 1)
            var marginalRaySolves = new List<int>();
            // Per-surface special aperture data (AY1/AY2 with AAC type)
            double currentAY1 = 0, currentAY2 = 0;
            int currentAAC = 0; // 0=none, 2=obscuration
            Surface currentSurface = new Surface { Index = 0 }; // SRF 0 (object)
            surfaces.Add(currentSurface);
            int surfIdx = 0;
            double unitScale = 1.0; // default mm

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                var parts = SplitLine(line);
                if (parts.Length == 0) continue;

                string keyword = parts[0].ToUpperInvariant();

                switch (keyword)
                {
                    case "SNO1":
                    case "SNO2":
                    case "SNO3":
                    case "SNO4":
                    case "SNO5":
                    case "SNO6":
                    case "SNO7":
                    case "SNO8":
                    case "SNO9":
                    case "SNO10":
                    {
                        // SNO1 = title; SNO2..SNO10 = multi-line free-form
                        // notes, newline-joined into system.Notes so they
                        // survive the read and reach the report./
                        int q1 = line.IndexOf('"');
                        int q2 = line.LastIndexOf('"');
                        if (q1 < 0 || q2 <= q1) break;
                        string text = line.Substring(q1 + 1, q2 - q1 - 1);
                        if (keyword == "SNO1")
                            system.Title = text;
                        else
                            system.Notes = string.IsNullOrEmpty(system.Notes) ? text : system.Notes + "\n" + text;
                        break;
                    }

                    case "DES":
                        // DES "<designer or source>" — single-line attribution
                        int dq1 = line.IndexOf('"');
                        int dq2 = line.LastIndexOf('"');
                        if (dq1 >= 0 && dq2 > dq1)
                            system.Designer = line.Substring(dq1 + 1, dq2 - dq1 - 1);
                        break;

                    case "EBR":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double ebr))
                            system.Aperture = new Aperture(ApertureType.EPD, ebr * 2.0);
                        break;

                    case "ANG":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double ang))
                        {
                            // Single field angle — add on-axis + this angle
                            system.FieldType = FieldType.ObjectAngle;
                            system.Fields.Clear();
                            system.Fields.Add(new Field(0, 1.0));
                            // By size, as OBH is below. (A negative angle used to be dropped
                            // altogether, leaving only the on-axis field.)
                            if (Math.Abs(ang) > 0)
                                system.Fields.Add(new Field(Math.Abs(ang), 1.0));
                        }
                        break;

                    case "NAO":
                        // Object-space numerical aperture: OSLO's aperture for a finite object.
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double nao))
                            system.Aperture = new Aperture(ApertureType.ObjectSpaceNA, nao);
                        break;

                    case "OBH":
                        // Object height: OSLO's field for a finite object.
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double obh))
                        {
                            system.FieldType = FieldType.ObjectHeight;
                            system.Fields.Clear();
                            system.Fields.Add(new Field(0, 1.0));
                            // OBH is the object point's y, and a field angle exported from here comes
                            // out negative (see OsloWriter). It is read by size, the way fields are
                            // given here; for a rotationally symmetric lens that is only a mirror image.
                            if (Math.Abs(obh) > 0)
                                system.Fields.Add(new Field(Math.Abs(obh), 1.0));
                        }
                        break;

                    case "PFL":
                        // OSLO's perfect lens: an ideal lens of this focal length. (PFM, the
                        // magnification it is perfect at, has no counterpart: the ideal lens here
                        // is perfect at every conjugate. OSLO's obeys the sine condition, so at
                        // an object at infinity its cone differs slightly from this one's.)
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double pfl))
                        {
                            currentSurface.Type = SurfaceType.Paraxial;
                            currentSurface.FocalLength = pfl;
                        }
                        break;

                    case "WV":
                        // OSLO repeats the WV line before each model glass, so each one replaces
                        // the list rather than adding to it.
                        wavelengths.Clear();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (TryParseDouble(parts[i], out double wl))
                                wavelengths.Add(wl); // already in um
                        }
                        break;

                    case "WW":
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (TryParseDouble(parts[i], out double wt))
                                wavelengthWeights.Add(wt);
                        }
                        break;

                    case "UNI":
                        // UNI <scale> — lens unit as scale factor to mm (1.0=mm, 25.4=inches, 10.0=cm)
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double uni) && uni > 0)
                            unitScale = uni;
                        break;

                    case "NXT":
                        // Apply special aperture from previous surface
                        ApplySpecialAperture(currentSurface, currentAY1, currentAY2, currentAAC);
                        currentAY1 = 0; currentAY2 = 0; currentAAC = 0;
                        // New surface
                        surfIdx++;
                        currentSurface = new Surface { Index = surfIdx };
                        surfaces.Add(currentSurface);
                        break;

                    case "RD":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double rd))
                        {
                            if (Math.Abs(rd) > 1e-15)
                                currentSurface.Radius = rd;
                        }
                        break;

                    case "TH":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double th))
                        {
                            if (Math.Abs(th) > 1e18)
                                currentSurface.Thickness = double.PositiveInfinity;
                            else
                                currentSurface.Thickness = th;
                        }
                        break;

                    case "GLA":
                        if (parts.Length > 3 && parts[1].Equals("MOD", StringComparison.OrdinalIgnoreCase))
                        {
                            // GLA MOD <name> <n1> <n2> ...: a model glass, as OSLO writes one - its
                            // index at each wavelength of the WV line before it.
                            var modelIndices = new List<double>();
                            for (int i = 3; i < parts.Length; i++)
                                if (TryParseDouble(parts[i], out double ni))
                                    modelIndices.Add(ni);
                            if (modelIndices.Count > 0)
                            {
                                var (nd, vd) = ModelFromIndices(wavelengths, modelIndices);
                                currentSurface.ModelIndexEnabled = true;
                                currentSurface.ModelNd = nd;
                                currentSurface.ModelVd = vd;
                                currentSurface.ModelDPgF = 0.0;
                                currentSurface.Material = "";
                            }
                        }
                        else if (parts.Length > 1)
                        {
                            string glass = parts[1].Trim();
                            // OSLO prefixes some glasses with H_ (e.g., H_F5 = F5)
                            if (glass.StartsWith("H_", StringComparison.OrdinalIgnoreCase))
                                glass = glass.Substring(2);
                            currentSurface.Material = glass;
                        }
                        break;

                    case "AP":
                        // AP CHK <value> or AP <value>
                        if (parts.Length > 1 && parts[1].Equals("CHK", StringComparison.OrdinalIgnoreCase))
                        {
                            if (parts.Length > 2 && TryParseDouble(parts[2], out double apChk))
                            {
                                currentSurface.SemiDiameter = apChk;
                                currentSurface.SemiDiameterMode = SemiDiameterMode.Fixed;
                            }
                        }
                        else if (parts.Length > 1 && TryParseDouble(parts[1], out double ap))
                        {
                            // An aperture that is not checked does not block rays in OSLO; it
                            // sizes the surface for drawing. Here that is an automatic semi-
                            // diameter, which does not clip either.
                            if (ap > 0)
                            {
                                currentSurface.SemiDiameter = ap;
                                currentSurface.SemiDiameterMode = SemiDiameterMode.Auto;
                            }
                        }
                        break;

                    case "AST":
                        currentSurface.IsStop = true;
                        break;

                    case "RAIM":
                        // OSLO: "RAIM crr" = chief ray reference (real ray aiming)
                        if (parts.Length > 1 &&
                            parts[1].Equals("crr", StringComparison.OrdinalIgnoreCase))
                            system.RayAiming = RayAimingMode.Real;
                        break;

                    case "RFH":
                        // Reflect here — this is a mirror
                        currentSurface.Material = "MIRROR";
                        break;

                    case "CC":
                        // Conic constant
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double cc))
                        {
                            currentSurface.Conic = cc;
                            if (currentSurface.Type != SurfaceType.EvenAsphere)
                                currentSurface.Type = SurfaceType.EvenAsphere;
                        }
                        break;

                    // OSLO aspheric coefficients: AD=r⁴→[1], AE=r⁶→[2], AF=r⁸→[3], ...
                    // Internal model: [0]=r², [1]=r⁴, [2]=r⁶, ...
                    case "AD":
                        SetAsphericCoeff(currentSurface, 1, parts);
                        break;
                    case "AE":
                        SetAsphericCoeff(currentSurface, 2, parts);
                        break;
                    case "AF":
                        SetAsphericCoeff(currentSurface, 3, parts);
                        break;
                    case "AG":
                        SetAsphericCoeff(currentSurface, 4, parts);
                        break;
                    case "AH":
                        SetAsphericCoeff(currentSurface, 5, parts);
                        break;
                    case "AI":
                        SetAsphericCoeff(currentSurface, 6, parts);
                        break;
                    case "AJ":
                        SetAsphericCoeff(currentSurface, 7, parts);
                        break;

                    case "AY1":
                        // AY1 A <value> — lower Y aperture bound
                        if (parts.Length >= 3 && TryParseDouble(parts[2], out double ay1))
                            currentAY1 = ay1;
                        else if (parts.Length >= 2 && TryParseDouble(parts[1], out double ay1b))
                            currentAY1 = ay1b;
                        break;

                    case "AY2":
                        // AY2 A <value> — upper Y aperture bound
                        if (parts.Length >= 3 && TryParseDouble(parts[2], out double ay2))
                            currentAY2 = ay2;
                        else if (parts.Length >= 2 && TryParseDouble(parts[1], out double ay2b))
                            currentAY2 = ay2b;
                        break;

                    case "AAC":
                        // AAC A <type> — aperture action code: 2 = obscuration
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int aac))
                            currentAAC = aac;
                        else if (parts.Length >= 2 && int.TryParse(parts[1], out int aacb))
                            currentAAC = aacb;
                        break;

                    case "CALLBACK":
                        // CALLBACK 1 = marginal ray height solve (paraxial focus)
                        if (parts.Length >= 2 && parts[1] == "1")
                        {
                            marginalRaySolves.Add(surfIdx);
                            // Round-trip marker so OsloWriter can re-emit
                            // CALLBACK 1 on the same surface during export.
                            if (surfIdx >= 0 && surfIdx < surfaces.Count)
                                surfaces[surfIdx].HasMarginalRaySolve = true;
                        }
                        break;

                    case "PK":
                        // PK TH <offset> <added_value>  — pickup thickness from another surface
                        // PK THM <offset> <added_value> — pickup thickness with mirror sign flip (negate)
                        if (parts.Length >= 3)
                        {
                            string pkType = parts[1].ToUpperInvariant();
                            if (pkType == "TH" || pkType == "THM")
                            {
                                if (int.TryParse(parts[2], out int offset))
                                {
                                    double addedVal = 0.0;
                                    if (parts.Length > 3)
                                        TryParseDouble(parts[3], out addedVal);
                                    thicknessPickups.Add((surfIdx, offset, addedVal, pkType == "THM"));
                                }
                            }
                        }
                        break;
                }
            }

            // Apply special aperture for the last surface
            ApplySpecialAperture(currentSurface, currentAY1, currentAY2, currentAAC);

            // Resolve thickness pickups eagerly so the loaded geometry is
            // correct even before PickupSolver runs downstream. Also record
            // each pickup on system.Pickups so the PK TH(M) directive
            // survives a round-trip through OsloWriter — PickupSolver is
            // idempotent given the same scale/offset.
            foreach (var (pickupSurf, offset, addedValue, negate) in thicknessPickups)
            {
                int sourceSurf = pickupSurf + offset;
                if (sourceSurf >= 0 && sourceSurf < surfaces.Count)
                {
                    double srcThickness = surfaces[sourceSurf].Thickness;
                    surfaces[pickupSurf].Thickness = (negate ? -srcThickness : srcThickness) + addedValue;

                    system.Pickups.Add(new Pickup
                    {
                        TargetSurfaceIndex = pickupSurf,
                        SourceSurfaceIndex = sourceSurf,
                        Parameter = PickupParameter.Thickness,
                        ScaleFactor = negate ? -1.0 : 1.0,
                        Offset = addedValue,
                    });
                }
            }

            // Solve marginal ray height = 0 (CALLBACK 1) via paraxial ray trace
            foreach (int solveIdx in marginalRaySolves)
            {
                if (solveIdx > 0 && solveIdx < surfaces.Count)
                    SolveMarginalRayHeight(surfaces, solveIdx);
            }

            system.Surfaces = surfaces;

            // THE REFERENCE COLOUR IS THE FIRST ONE, as OSLO defines it.
            //
            // OSLO has no primary-wavelength keyword: its "Primary Wavln" is wavelength 1, and
            // it asks for the order middle, short, long - its own default is d, F, C (Program
            // Reference pp. 26, 123). OSLO itself traces a file that way: a Kingslake double Gauss
            // written F, d, C traced in OSLO EDU in the F line (chief ray 24.945679 at 14 degrees,
            // against 24.9495 in the d line).
            //
            // This reader used to take the middle entry, which was right only for files written
            // short-to-long by an exporter that did not put the primary first (LensHH-LT before
            // 1.0.158). On a file written OSLO's way it took the F line.
            int primary = 0;

            for (int i = 0; i < wavelengths.Count; i++)
            {
                double wt = i < wavelengthWeights.Count ? wavelengthWeights[i] : 1.0;
                system.Wavelengths.Add(new Wavelength(wavelengths[i], wt, i == primary));
            }

            if (system.Fields.Count == 0)
                system.Fields.Add(new Field(0, 1.0));

            FieldValidation.FilterImportedFields(system);

            // Convert from file units to mm. The scale is REMEMBERED as well as applied: an
            // optimised design has to be able to go back to the file it came from, in the units
            // that file is written in.
            system.FileUnitScale = unitScale;
            if (unitScale != 1.0)
                LensUnitConverter.ConvertToMm(system, unitScale);

            return system;
        }

        /// <summary>
        /// The model glass (nd, Vd) behind OSLO's indices at the file's wavelengths. With the d, F
        /// and C lines among them it is exact; otherwise a Cauchy fit n = A + B/λ² through the
        /// indices gives them. A single index carries no dispersion, and is taken as nd with Vd 0,
        /// the model glass's own "no dispersion": the same index at every wavelength, exactly. (A Vd
        /// of 1e6 was used, which still disperses: the index came back 1e-7 off at the lens's own
        /// wavelength.)
        /// </summary>
        internal static (double nd, double vd) ModelFromIndices(List<double> wavelengthsUm, List<double> n)
        {
            const double dLine = 0.58756, fLine = 0.48613, cLine = 0.65627;
            int count = Math.Min(wavelengthsUm.Count, n.Count);
            if (count < 2)
                return (n[0], 0.0);

            int Find(double target)
            {
                for (int i = 0; i < count; i++)
                    if (Math.Abs(wavelengthsUm[i] - target) < 1e-4)
                        return i;
                return -1;
            }
            int id = Find(dLine), iF = Find(fLine), iC = Find(cLine);
            if (id >= 0 && iF >= 0 && iC >= 0 && Math.Abs(n[iF] - n[iC]) > 1e-12)
                return (n[id], (n[id] - 1.0) / (n[iF] - n[iC]));

            // Least-squares Cauchy fit in x = 1/λ².
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < count; i++)
            {
                double x = 1.0 / (wavelengthsUm[i] * wavelengthsUm[i]);
                sx += x; sy += n[i]; sxx += x * x; sxy += x * n[i];
            }
            double det = count * sxx - sx * sx;
            if (Math.Abs(det) < 1e-300)
                return (n[0], 1e6);
            double bCoef = (count * sxy - sx * sy) / det;
            double aCoef = (sy - bCoef * sx) / count;
            double Cauchy(double lam) => aCoef + bCoef / (lam * lam);
            double nd = Cauchy(dLine);
            double dispersion = Cauchy(fLine) - Cauchy(cLine);
            return (nd, Math.Abs(dispersion) > 1e-12 ? (nd - 1.0) / dispersion : 1e6);
        }

        /// <summary>
        /// Apply OSLO special aperture (AY1/AY2 + AAC) to a surface.
        /// AAC 2 = obscuration/hole: on a mirror this is a central hole (InnerRadius/CLAP),
        /// on a non-mirror surface it is a central obscuration.
        /// </summary>
        private static void ApplySpecialAperture(Surface surface, double ay1, double ay2, int aac)
        {
            if (aac == 2 && ay2 > 0)
            {
                if (surface.IsMirror)
                    surface.InnerRadius = ay2; // central hole in mirror
                else
                    surface.ObscurationRadius = ay2; // central obscuration (e.g. baffle)
            }
        }

        /// <summary>
        /// Paraxial marginal ray solve: compute thickness of surface (solveIdx-1) so the
        /// marginal ray height is zero at surface solveIdx (paraxial focus).
        /// Uses y-nu paraxial ray trace with proper refractive index tracking for mirrors.
        /// </summary>
        private static void SolveMarginalRayHeight(List<Surface> surfaces, int solveIdx)
        {
            // For infinite conjugate: start with y = semi-diameter at first real surface, nu = 0
            double y = 0;
            double nu = 0; // nu = n * u (optical direction cosine * index)
            double n = 1.0; // current refractive index

            // Find initial marginal ray height from the stop or first real surface
            for (int i = 1; i < surfaces.Count; i++)
            {
                if (surfaces[i].SemiDiameter > 0)
                {
                    y = surfaces[i].SemiDiameter;
                    break;
                }
            }
            if (y == 0) y = 100.0;

            // Trace: refract at surface i, then transfer thickness of surface i to reach surface i+1
            for (int i = 1; i < surfaces.Count; i++)
            {
                var surf = surfaces[i];
                double c = double.IsInfinity(surf.Radius) ? 0.0 : 1.0 / surf.Radius;

                // Refraction: nu' = nu - (n' - n) * c * y
                if (surf.IsMirror)
                {
                    // Mirror: n' = -n, so (n' - n) = -2n → nu' = nu + 2*n*c*y
                    nu = nu + 2.0 * n * c * y;
                    n = -n;
                }

                // After refraction at surface i, before transfer:
                // if the *next* surface is the solve target, compute thickness
                if (i + 1 == solveIdx)
                {
                    // y_next = y + t * (nu / n), want y_next = 0
                    // t = -y * n / nu
                    if (Math.Abs(nu) > 1e-20)
                        surfaces[i].Thickness = -y * n / nu;
                    return;
                }

                // Transfer across thickness of surface i
                double thickness = surf.Thickness;
                if (!double.IsInfinity(thickness))
                    y = y + thickness * nu / n;
            }
        }

        private static void SetAsphericCoeff(Surface surface, int index, string[] parts)
        {
            if (parts.Length > 1 && TryParseDouble(parts[1], out double val))
            {
                surface.AsphericCoefficients[index] = val;
                if (surface.Type != SurfaceType.EvenAsphere)
                    surface.Type = SurfaceType.EvenAsphere;
            }
        }

        private static string[] SplitLine(string line)
        {
            return line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool TryParseDouble(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value);
        }
    }
}
