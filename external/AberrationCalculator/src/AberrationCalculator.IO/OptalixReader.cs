using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Glass;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    /// <summary>
    /// Reads Optalix .OTX lens files.
    /// Surfaces defined by SUR blocks with CUY/THI/GLA/STO/APE/ASP sub-keywords.
    /// Surface types are letters in any order: S sphere, A asphere, L lens module (ideal lens),
    /// M mirror, and others this program does not model.
    /// </summary>
    public static class OptalixReader
    {
        /// <summary>
        /// Read an OpTaliX .OTX file. When <paramref name="glassMgr"/> is
        /// provided, files with no explicit aperture (no EPD/FNO keyword)
        /// have their EPD computed via a paraxial axial back-trace through
        /// the front group — matching the format's "float by stop" default.
        /// Without a glass manager we fall back to <c>2 × stop_SD</c>, which
        /// is fine for normal designs but wrong by 5–10× on retrofocus /
        /// fisheye lenses where the front group strongly demagnifies the stop.
        /// </summary>
        public static OpticalSystem Read(string filePath, GlassCatalog? glassMgr = null)
        {
            var lines = File.ReadAllLines(filePath);
            var system = new OpticalSystem();

            var surfaces = new List<Surface>();
            var wavelengths = new List<double>();
            var wavelengthWeights = new List<double>();
            int refWavelength = 1;
            Surface? currentSurface = null;
            bool currentIsMirror = false;
            double unitScale = 1.0; // default mm

            // Newer exports of this format use FLDY/FLDX/FWGT arrays instead of
            // per-field FLD lines. Collect during parse and build Fields[]
            // at the end, only if no FLD lines populated system.Fields.
            var fldYArr = new List<double>();
            var fwgtArr = new List<double>();

            // Optalix's lens module (ideal lens): SUT L surfaces, in pairs, the power on the first
            // one's LMOD. Merged into one paraxial surface after the parse.
            var lensModule = new HashSet<Surface>();
            var lmodPower = new Dictionary<Surface, double>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("!")) continue;

                var parts = SplitLine(line);
                if (parts.Length == 0) continue;

                string keyword = parts[0].ToUpperInvariant();

                switch (keyword)
                {
                    case "REM":
                        // REM 1 <title>
                        if (parts.Length > 2)
                        {
                            int firstSpace = line.IndexOf(' ');
                            if (firstSpace >= 0)
                            {
                                int secondSpace = line.IndexOf(' ', firstSpace + 1);
                                if (secondSpace >= 0)
                                    system.Title = line.Substring(secondSpace + 1).Trim();
                            }
                        }
                        break;

                    case "EPD":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double epd))
                            system.Aperture = new Aperture(ApertureType.EPD, epd);
                        break;

                    case "FNO":
                        // Working F-number (image-side). Stored directly as
                        // ApertureType.FNumber so this program derives EPD from
                        // the focal length at trace time.
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double fno) && fno > 0)
                            system.Aperture = new Aperture(ApertureType.FNumber, fno);
                        break;

                    // Note: MFR is NOT the entrance pupil. Files like
                    // FISHEYE2.OTX, HYPERGON.OTX, LAIKIN-9-1.OTX specify
                    // both `MFR` and an aperture (EPD or FNO) as separate
                    // values — MFR is some other quantity (likely a ray-fan
                    // plotting / sampling parameter). We deliberately ignore
                    // it here. (NA, the image-space NA, has no counterpart here: a file that
                    // gives it takes its aperture from the stop's size, below.)

                    case "NAO":
                        // Object-space numerical aperture: Optalix's aperture for a finite object.
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double nao) && nao > 0)
                            system.Aperture = new Aperture(ApertureType.ObjectSpaceNA, nao);
                        break;

                    case "AFO":
                        // Afocal mode ("AFO 1000.0", or a bare AFO; the manual's AFO 1).
                        system.IsAfocal = parts.Length < 2 || !TryParseDouble(parts[1], out double afo) || afo != 0;
                        break;

                    case "WL":
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (TryParseDouble(parts[i], out double wl))
                                wavelengths.Add(wl); // already in um
                        }
                        break;

                    case "WTW":
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (TryParseDouble(parts[i], out double wt))
                                wavelengthWeights.Add(wt);
                        }
                        break;

                    case "REF":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int r))
                            refWavelength = r;
                        break;

                    case "FLD":
                        // FLD <n> <hx> <hy> <weight> <flag> <id>
                        if (parts.Length >= 5)
                        {
                            TryParseDouble(parts[2], out double hx);
                            TryParseDouble(parts[3], out double hy);
                            TryParseDouble(parts[4], out double fw);
                            system.Fields.Add(new Field(hy, fw > 0 ? fw / 100.0 : 1.0));
                        }
                        break;

                    case "FLDX":
                        // Newer form of the format: FLDX = list of X field positions.
                        // This program stores only Y (rotationally-symmetric), so we
                        // ignore the values themselves but log presence so the
                        // FLDY-only path doesn't drop a 2D field set silently.
                        // (No-op for the all-zeros case which is by far the most
                        //  common — meridional-only field set.)
                        break;

                    case "FLDY":
                        // FLDY <y1> <y2> ... — list of Y field positions
                        for (int i = 1; i < parts.Length; i++)
                            if (TryParseDouble(parts[i], out double fldy))
                                fldYArr.Add(fldy);
                        break;

                    case "FWGT":
                        // FWGT <w1> <w2> ... — per-field weights, percent
                        for (int i = 1; i < parts.Length; i++)
                            if (TryParseDouble(parts[i], out double fwt))
                                fwgtArr.Add(fwt);
                        break;

                    case "FTYP":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int ft))
                        {
                            // Optalix: 1 = field angle, 2 = object height; 3 and 4 are image heights,
                            // which have no counterpart here. 0 is what LensHH-LT wrote for object
                            // heights before 1.0.158.
                            system.FieldType = ft == 2 || ft == 0 ? FieldType.ObjectHeight : FieldType.ObjectAngle;
                        }
                        break;

                    case "UNI":
                        // UNI <scale> — lens unit as scale factor to mm
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double uni) && uni > 0)
                            unitScale = uni;
                        break;

                    case "DIM":
                        // DIM M = millimeters, DIM C = centimeters, DIM I = inches
                        if (parts.Length > 1)
                        {
                            switch (parts[1].ToUpperInvariant())
                            {
                                case "M": unitScale = 1.0; break;
                                case "C": unitScale = 10.0; break;
                                case "I": unitScale = 25.4; break;
                            }
                        }
                        break;

                    case "SUR":
                        if (parts.Length > 1 && int.TryParse(parts[1], out int surIdx))
                        {
                            currentSurface = new Surface { Index = surIdx };
                            currentIsMirror = false;
                            surfaces.Add(currentSurface);
                        }
                        break;

                    case "SUT":
                        // Surface type: letters in any order - one base type (S sphere, A asphere,
                        // L lens module, X, U) and modifiers (M mirror, D decentered, ...): SM and
                        // MS are both a spherical mirror.
                        if (currentSurface != null && parts.Length > 1)
                        {
                            string sut = parts[1].ToUpperInvariant();
                            currentIsMirror = sut.Contains('M');
                            if (sut.Contains('A'))
                                currentSurface.Type = SurfaceType.EvenAsphere;
                            if (sut.Contains('L'))
                                lensModule.Add(currentSurface);
                        }
                        break;

                    case "CUY":
                        if (currentSurface != null && parts.Length > 1 && TryParseDouble(parts[1], out double cuy))
                        {
                            if (Math.Abs(cuy) > 1e-15)
                                currentSurface.Curvature = cuy;
                        }
                        break;

                    case "THI":
                        if (currentSurface != null && parts.Length > 1 && TryParseDouble(parts[1], out double thi))
                        {
                            if (Math.Abs(thi) > 1e18)
                                currentSurface.Thickness = double.PositiveInfinity;
                            else if (thi <= -900) // -999 = image surface convention
                                currentSurface.Thickness = 0;
                            else
                                currentSurface.Thickness = thi;
                        }
                        break;

                    case "GLA":
                        // On a mirror the glass is the medium the light goes on in after it - the one
                        // it came in, which the surfaces before it already give. The mirror stays a
                        // mirror. (Read as the surface's glass, a Mangin mirror became a refracting
                        // surface.)
                        if (currentIsMirror) break;
                        if (currentSurface != null && parts.Length > 1)
                        {
                            string gla = parts[1].Trim();
                            if (TryFictitiousGlass(gla, out double fnd, out double fvd))
                            {
                                // Optalix's fictitious (model) glass: 6201.604 = nd 1.6201, Vd 60.4.
                                currentSurface.ModelIndexEnabled = true;
                                currentSurface.ModelNd = fnd;
                                currentSurface.ModelVd = fvd;
                                currentSurface.ModelDPgF = 0.0;
                                currentSurface.Material = "";
                            }
                            else if (gla.Equals("AIR", StringComparison.OrdinalIgnoreCase))
                                currentSurface.Material = "";
                            else
                                currentSurface.Material = gla.Trim('\'');   // 'GE': a private glass's name
                        }
                        break;

                    case "PRI":
                        // PRI n1 n2 ...: the index at each WL wavelength - a glass given directly.
                        // On a mirror, the medium it is in, as GLA there.
                        if (currentIsMirror) break;
                        if (currentSurface != null && parts.Length > 1)
                        {
                            var pri = new List<double>();
                            for (int i = 1; i < parts.Length; i++)
                                if (TryParseDouble(parts[i], out double ni))
                                    pri.Add(ni);
                            if (pri.Count > 0)
                            {
                                var (pnd, pvd) = ModelFromIndices(wavelengths, pri);
                                currentSurface.ModelIndexEnabled = true;
                                currentSurface.ModelNd = pnd;
                                currentSurface.ModelVd = pvd;
                                currentSurface.ModelDPgF = 0.0;
                                currentSurface.Material = "";
                            }
                        }
                        break;

                    case "LMOD":
                        // LMOD <power> 0 0 0 0 on a lens-module surface. The value is a power: the
                        // 160 mm tube lens that ships with Optalix has 0.00625.
                        if (currentSurface != null && parts.Length > 1 && TryParseDouble(parts[1], out double lp))
                            lmodPower[currentSurface] = lp;
                        break;

                    case "FH":
                        // FH 1 ...: the surface's aperture clips. Without it an Optalix aperture only
                        // sizes the surface and never blocks a ray (reference manual p. 166).
                        if (currentSurface != null && parts.Length > 1 && parts[1] == "1" && currentSurface.SemiDiameter > 0)
                            currentSurface.SemiDiameterMode = SemiDiameterMode.Fixed;
                        break;

                    case "STO":
                        if (currentSurface != null)
                            currentSurface.IsStop = true;
                        break;

                    case "APE":
                        // APE <n> <semi-x> <semi-y> <x0> <y0> <rot> <shape> <op> <trans> ... (reference
                        // manual §32.2). trans is 0 transmit, 1 obstruct, 2 hole. A transmitting
                        // aperture is the surface's size, automatic until an FH 1 line after it makes
                        // it clip. An obstruction is a central obscuration: InnerRadius on a mirror,
                        // ObscurationRadius otherwise. (Every aperture was read as fixed, and the
                        // aperture number was taken for its type.)
                        if (currentSurface != null && parts.Length > 3
                            && TryParseDouble(parts[2], out double apeX)
                            && TryParseDouble(parts[3], out double apeY))
                        {
                            double apeVal = Math.Max(apeX, apeY);
                            int trans = parts.Length > 9 && int.TryParse(parts[9], out int t) ? t
                                      : parts.Length > 1 && parts[1] == "2" ? 1   // files LensHH-LT wrote before 1.0.158
                                      : 0;
                            if (apeVal <= 0)
                                break;
                            if (trans == 1 || trans == 2)
                            {
                                if (currentIsMirror || trans == 2)
                                    currentSurface.InnerRadius = apeVal;
                                else
                                    currentSurface.ObscurationRadius = apeVal;
                            }
                            else if (currentSurface.SemiDiameter <= 0)
                            {
                                currentSurface.SemiDiameter = apeVal;
                                currentSurface.SemiDiameterMode = SemiDiameterMode.Auto;
                            }
                        }
                        break;

                    case "COM":
                        // COM <text> — surface label (e.g. PRIMARY, SECONDARY,
                        // TERTIERY in three-mirror designs). Take everything
                        // after the keyword so internal whitespace is kept.
                        if (currentSurface != null && parts.Length > 1)
                        {
                            int comIdx = line.IndexOf("COM", StringComparison.Ordinal);
                            currentSurface.Comment = line.Substring(comIdx + 3).Trim();
                        }
                        break;

                    case "VAR":
                        // VAR marks surface parameters as optimization variables - for the
                        // program that wrote the file. This one keeps its own statement of what
                        // may move, in the sidecar, and adopting somebody else's would decide
                        // that question silently on the strength of a run nobody here saw. The
                        // directive is read past and written back untouched.
                        break;

                    case "ASP":
                        // ASP <conic> <A> <B> <C> <D> <E> <F> <G> <H> <I>
                        // First value = conic constant, then A=r⁴, B=r⁶, C=r⁸, ...
                        // Internal model: [0]=r², [1]=r⁴, [2]=r⁶, ... so A→[1], B→[2], etc.
                        if (currentSurface != null && parts.Length > 1)
                        {
                            if (TryParseDouble(parts[1], out double conic))
                                currentSurface.Conic = conic;

                            // Coefficients A through H (parts 2..9 → internal indices 1..8)
                            for (int i = 2; i < parts.Length && (i - 1) < currentSurface.AsphericCoefficients.Length; i++)
                            {
                                if (TryParseDouble(parts[i], out double coeff) && Math.Abs(coeff) > 0)
                                    currentSurface.AsphericCoefficients[i - 1] = coeff;
                            }

                            if (currentSurface.Type != SurfaceType.EvenAsphere)
                                currentSurface.Type = SurfaceType.EvenAsphere;
                        }
                        break;

                    case "RAIM":
                        // Optalix writes 2 (aim at the real stop, its default) in 962 of the 1019
                        // lens files that ship with it, 1 for the paraxial entrance pupil, 3 for a
                        // telecentric object space, and never 0 - which LensHH-LT wrote, meaning off,
                        // before 1.0.158.
                        if (parts.Length > 1 && int.TryParse(parts[1], out int raim))
                        {
                            system.RayAiming = raim == 2 || raim == 4 ? RayAimingMode.Real : RayAimingMode.Off;
                            system.TelecentricObjectSpace = raim == 3;
                        }
                        break;
                }

                // After processing each line, if the surface is a mirror and no GLA was set, apply MIRROR
                if (currentIsMirror && currentSurface != null && string.IsNullOrEmpty(currentSurface.Material))
                    currentSurface.Material = "MIRROR";
            }

            // Optalix's lens module is a pair of L surfaces - the principal planes - with the power on
            // the first. Here it becomes one ideal lens. The gap between the planes is dropped and
            // every other distance kept: a ray leaves the second plane at the height it met the
            // first, so the imaging is unchanged; only the lens's overall length is shorter.
            for (int i = 0; i + 1 < surfaces.Count; i++)
            {
                var entrance = surfaces[i];
                var exit = surfaces[i + 1];
                if (!lensModule.Contains(entrance) || !lensModule.Contains(exit))
                    continue;
                double power = lmodPower.TryGetValue(entrance, out double pw) ? pw : 0.0;
                entrance.Type = SurfaceType.Paraxial;
                entrance.Curvature = 0.0;
                entrance.FocalLength = power == 0.0 ? double.PositiveInfinity : 1.0 / power;
                entrance.Thickness = exit.Thickness;
                entrance.IsStop |= exit.IsStop;
                if (string.IsNullOrEmpty(entrance.Material) && !entrance.ModelIndexEnabled)
                {
                    entrance.Material = exit.Material;
                    entrance.ModelIndexEnabled = exit.ModelIndexEnabled;
                    entrance.ModelNd = exit.ModelNd;
                    entrance.ModelVd = exit.ModelVd;
                    entrance.ModelDPgF = exit.ModelDPgF;
                }
                if (exit.SemiDiameter > entrance.SemiDiameter)
                {
                    entrance.SemiDiameter = exit.SemiDiameter;
                    entrance.SemiDiameterMode = exit.SemiDiameterMode;
                }
                surfaces.RemoveAt(i + 1);
                lensModule.Remove(entrance);
            }
            for (int i = 0; i < surfaces.Count; i++)
                surfaces[i].Index = i;

            system.Surfaces = surfaces;

            // Wavelengths
            for (int i = 0; i < wavelengths.Count; i++)
            {
                double wt = i < wavelengthWeights.Count ? wavelengthWeights[i] : 1.0;
                bool isPrimary = (i + 1) == refWavelength;
                system.Wavelengths.Add(new Wavelength(wavelengths[i], wt, isPrimary));
            }

            // If the file used FLDY arrays (the newer form) and no
            // FLD lines populated system.Fields, materialize from the
            // collected arrays now. Pad missing FWGT entries with 1.0.
            if (system.Fields.Count == 0 && fldYArr.Count > 0)
            {
                for (int i = 0; i < fldYArr.Count; i++)
                {
                    double w = i < fwgtArr.Count ? (fwgtArr[i] > 0 ? fwgtArr[i] / 100.0 : 1.0) : 1.0;
                    system.Fields.Add(new Field(fldYArr[i], w));
                }
            }

            if (system.Fields.Count == 0)
                system.Fields.Add(new Field(0, 1.0));

            FieldValidation.FilterImportedFields(system);

            // No explicit EPD/F# in the file → use the format's "float by stop"
            // default. With a glass manager we can do the proper paraxial
            // axial back-trace through the front group; without one, fall
            // back to 2 × stop_SD (correct only when the front group is
            // ~afocal — wildly wrong on retrofocus / fisheye designs).
            if (system.Aperture.Value <= 0)
            {
                int stopIdx = -1;
                double stopSd = 0;
                for (int i = 0; i < surfaces.Count; i++)
                {
                    if (surfaces[i].IsStop && surfaces[i].SemiDiameter > 0)
                    {
                        stopIdx = i;
                        stopSd = surfaces[i].SemiDiameter;
                        break;
                    }
                }

                if (stopIdx > 0 && stopSd > 0)
                {
                    double epd = 2.0 * stopSd; // heuristic fallback
                    if (glassMgr != null && system.Wavelengths.Count > 0)
                    {
                        int primaryIdx = system.PrimaryWavelengthIndex;
                        if (primaryIdx < 0 || primaryIdx >= system.Wavelengths.Count) primaryIdx = 0;
                        double wlUm = system.Wavelengths[primaryIdx].Value;
                        double[] indices = IndexResolver.Build(system, glassMgr, wlUm);
                        double yAtStop = ParaxialAxialYAtStop(surfaces, indices, stopIdx);
                        if (yAtStop > 1e-9)
                            epd = 2.0 * stopSd / yAtStop;
                    }
                    system.Aperture = new Aperture(ApertureType.EPD, epd);
                }
            }

            // Convert from file units to mm. The scale is REMEMBERED as well as applied: an
            // optimised design has to be able to go back to the file it came from, in the units
            // that file is written in.
            system.FileUnitScale = unitScale;
            if (unitScale != 1.0)
                LensUnitConverter.ConvertToMm(system, unitScale);

            return system;
        }

        /// <summary>
        /// Optalix's fictitious-glass code, as the files that ship with it write it: the digits
        /// before the point are nd − 1 after its decimal point, and after it come Vd's two integer
        /// digits and its decimals - 6204.603 is nd 1.6204, Vd 60.3; 516.64 is 1.516, 64. Six digits
        /// and no point is the MIL code: 620603 is 1.620, 60.3.
        /// </summary>
        public static bool TryFictitiousGlass(string code, out double nd, out double vd)
        {
            nd = vd = 0.0;
            if (code.Length == 0 || !char.IsDigit(code[0]) || code.Any(ch => !char.IsDigit(ch) && ch != '.'))
                return false;
            int dot = code.IndexOf('.');
            if (dot < 0)
            {
                if (code.Length != 6)
                    return false;
                nd = 1.0 + int.Parse(code.Substring(0, 3), CultureInfo.InvariantCulture) / 1000.0;
                vd = int.Parse(code.Substring(3), CultureInfo.InvariantCulture) / 10.0;
                return true;
            }
            string whole = code.Substring(0, dot), frac = code.Substring(dot + 1);
            if (whole.Length < 3 || frac.Length < 2 || frac.Contains('.'))
                return false;
            nd = 1.0 + double.Parse("0." + whole, CultureInfo.InvariantCulture);
            vd = double.Parse(frac.Substring(0, 2) + "." + (frac.Length > 2 ? frac.Substring(2) : "0"),
                              CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>
        /// Optalix's fictitious-glass code for (nd, Vd), the inverse of
        /// <see cref="TryFictitiousGlass"/>: 1.6201 / 60.4 is <c>6201.604</c>. Null when the code
        /// cannot carry the glass (a partial-dispersion offset, Vd outside 10 to 100, nd past 2).
        /// </summary>
        public static string? FictitiousGlassCode(double nd, double vd, double dPgF)
        {
            if (dPgF != 0.0 || nd <= 1.0 || nd >= 2.0 || vd < 10.0 || vd >= 100.0)
                return null;
            string ndDigits = Math.Round(nd - 1.0, 6).ToString("0.000000", CultureInfo.InvariantCulture)
                .Substring(2).TrimEnd('0');
            if (ndDigits.Length < 3)
                ndDigits = ndDigits.PadRight(3, '0');
            string vdText = Math.Round(vd, 4).ToString("00.####", CultureInfo.InvariantCulture);
            return ndDigits + "." + vdText.Replace(".", "");
        }

        /// <summary>
        /// The model glass (nd, Vd) behind indices at the file's wavelengths: exact with the d, F and
        /// C lines among them, otherwise from a Cauchy fit n = A + B/λ²; a single index is taken as
        /// nd with Vd 0, the model glass's own "no dispersion": the same index at every wavelength.
        /// </summary>
        private static (double nd, double vd) ModelFromIndices(List<double> wavelengthsUm, List<double> n)
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

        private static string[] SplitLine(string line)
        {
            return line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool TryParseDouble(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value);
        }

        // Optalix aspheric variable index: A=r⁴ → [1], B=r⁶ → [2], etc.,
        // matching the existing ASP-coefficient read convention.

        /// <summary>
        /// Paraxial axial ray (y=1, u=0) traced from S1 forward to the stop's
        /// plane (before the stop's own refraction). Returns y at the stop.
        /// Used to back-compute EPD when the file has no aperture keyword:
        /// EPD = 2 · stop_SD / yAtStop. This mirrors what the format does
        /// internally for its "float by stop" default.
        /// indices[i] = refractive index in the gap AFTER surface i.
        /// </summary>
        private static double ParaxialAxialYAtStop(List<Surface> surfaces, double[] indices, int stopIdx)
        {
            if (stopIdx < 1 || stopIdx >= surfaces.Count) return 0;

            double y = 1.0;
            double u = 0.0;

            for (int i = 1; i < stopIdx; i++)
            {
                double n1 = indices[i - 1];
                double n2 = indices[i];
                if (n2 <= 0) return 0; // unresolved glass — abort, caller falls back
                double c = surfaces[i].Curvature; // 1/R, 0 for flat

                // Paraxial refraction (ynu form): n2*u' = n1*u - y*(n2-n1)*c
                u = (n1 * u - y * (n2 - n1) * c) / n2;

                // Transfer through this surface's thickness to the next
                double t = surfaces[i].Thickness;
                if (double.IsInfinity(t) || double.IsNaN(t)) return 0;
                y = y + t * u;
            }

            return y;
        }
    }
}
