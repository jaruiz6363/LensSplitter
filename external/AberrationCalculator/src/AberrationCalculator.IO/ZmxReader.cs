using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Core.IO
{
    public static class ZmxReader
    {
        public static OpticalSystem Read(string filePath) => Read(filePath, null);

        /// <summary>
        /// Read a .zmx file, bringing in the glasses it needs from outside this program's own
        /// catalogs:
        /// <list type="bullet">
        /// <item>each OpticStudio table glass (<c>GLAS NAME.ZTG</c>) becomes a TABLE-catalog glass
        /// or a model glass;</item>
        /// <item>a glass from a GCAT catalog that isn't loaded has that catalog found, copied to the
        /// user's glass folder and loaded into <paramref name="glass"/>.</item>
        /// </list>
        /// What was done is added to the system's notes.
        /// </summary>
        public static OpticalSystem Read(string filePath, Core.Glass.GlassCatalog? glass)
        {
            var system = ReadCore(filePath);
            var notes = ResolveTableGlasses(system, filePath, glass);
            if (glass != null)
                notes.AddRange(LoadLensCatalogs(system, filePath, glass));
            if (notes.Count > 0)
            {
                string text = string.Join(Environment.NewLine, notes);
                system.Notes = string.IsNullOrEmpty(system.Notes) ? text : system.Notes + Environment.NewLine + text;
            }
            return system;
        }

        /// <summary>
        /// The surfaces whose glass is an OpticStudio table glass. A table of six or more points
        /// is fitted with the Schott formula and added to the user's TABLE catalog under the
        /// file's name. A shorter table is fitted with a Conrady curve and becomes a model glass.
        /// That is exact for the three-point tables OpticStudio's Code V converter writes, since a
        /// model glass is a Conrady curve. A table that cannot be found is left named, so the
        /// surface shows as an unresolved glass, and reported.
        /// </summary>
        private static List<string> ResolveTableGlasses(OpticalSystem system, string lensPath, Core.Glass.GlassCatalog? glass)
        {
            var notes = new List<string>();
            var done = new Dictionary<string, Action<Surface>>(StringComparer.OrdinalIgnoreCase);
            var inv = CultureInfo.InvariantCulture;

            for (int i = 0; i < system.Surfaces.Count; i++)
            {
                var s = system.Surfaces[i];
                string? mat = s.Material;
                if (string.IsNullOrEmpty(mat) || !mat!.EndsWith(".ZTG", StringComparison.OrdinalIgnoreCase)) continue;

                if (!done.TryGetValue(mat, out var apply))
                {
                    string? file = TableGlass.Find(mat, lensPath);
                    if (file == null)
                    {
                        notes.Add($"Surface {i}: table glass {mat} not found beside the lens or in Documents\\Zemax\\Glasscat; the surface has no glass until it is.");
                        done[mat] = _ => { };
                        continue;
                    }

                    var points = TableGlass.Read(file);
                    string name = Path.GetFileNameWithoutExtension(file).Replace(' ', '_').ToUpperInvariant();
                    string range = points.Count > 0
                        ? string.Format(inv, "{0:0.###}–{1:0.###} µm", points[0].Um, points[points.Count - 1].Um) : "";

                    if (points.Count >= TableGlass.MinSchottPoints)
                    {
                        var (c, err) = TableGlass.FitSchott(points);
                        UserGlassCatalog.AddSchottGlass(name, c, points[0].Um, points[points.Count - 1].Um,
                            $"from {Path.GetFileName(file)}, {points.Count} points, fitted with the Schott formula", glass);
                        notes.Add(string.Format(inv, "Surface {0}: table glass {1} ({2} points, {3}) added to your TABLE catalog as {4}; the Schott formula fits the table to {5:0.0e0}.",
                            i, Path.GetFileName(file), points.Count, range, name, err));
                        apply = surf =>
                        {
                            surf.Material = name;
                            if (!system.GlassCatalogs.Contains(UserGlassCatalog.TableCatalog, StringComparer.OrdinalIgnoreCase))
                                system.GlassCatalogs.Add(UserGlassCatalog.TableCatalog);
                        };
                    }
                    else if (points.Count > 0)
                    {
                        var (c0, c1, c2, err) = TableGlass.FitConrady(points);
                        var model = Core.Glass.IndexResolver.ModelFromConrady(c0, c1, c2);
                        if (model == null)
                        {
                            notes.Add($"Surface {i}: table glass {Path.GetFileName(file)} could not be converted; the surface has no glass.");
                            done[mat] = _ => { };
                            continue;
                        }
                        var (nd, vd, dPgF) = model.Value;
                        notes.Add(string.Format(inv, "Surface {0}: table glass {1} ({2} points, {3}) is too short to fit a catalog formula to, and was made a model glass (nd {4:F6}, Vd {5:F4}, ΔPgF {6:F5}){7}.",
                            i, Path.GetFileName(file), points.Count, range, nd, vd, dPgF,
                            err < 1e-9 ? ", which passes through every point" : string.Format(inv, " that fits the table to {0:0.0e0}", err)));
                        apply = surf =>
                        {
                            surf.Material = "";
                            surf.ModelIndexEnabled = true;
                            surf.ModelNd = nd;
                            surf.ModelVd = vd;
                            surf.ModelDPgF = dPgF;
                        };
                    }
                    else
                    {
                        notes.Add($"Surface {i}: table glass {Path.GetFileName(file)} holds no wavelength–index pairs; the surface has no glass.");
                        done[mat] = _ => { };
                        continue;
                    }
                    done[mat] = apply;
                }
                apply(s);
            }
            return notes;
        }

        /// <summary>
        /// The catalogs on the lens's GCAT line that it needs and this program does not have. A
        /// glass that resolves from no loaded catalog is looked for in the lens's own catalogs,
        /// beside the lens or in OpticStudio's glass folder (Documents\Zemax\Glasscat). A catalog
        /// that holds one is copied into the user's glass folder and loaded, so the lens opens
        /// with it again. A catalog already loaded is never replaced, and a catalog the lens lists
        /// but does not use is left alone.
        /// </summary>
        private static List<string> LoadLensCatalogs(OpticalSystem system, string lensPath, Core.Glass.GlassCatalog glass)
        {
            var notes = new List<string>();
            var unresolved = system.Surfaces
                .Select(s => s.Material)
                .Where(m => !string.IsNullOrWhiteSpace(m)
                            && !m!.Equals("MIRROR", StringComparison.OrdinalIgnoreCase)
                            && !m.EndsWith(".ZTG", StringComparison.OrdinalIgnoreCase)
                            && glass.Find(m, system.GlassCatalogs) == null)
                .Select(m => m!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unresolved.Count == 0) return notes;

            foreach (var catalog in system.GlassCatalogs.ToList())
            {
                if (unresolved.Count == 0) break;
                if (glass.LoadedCatalogs.Contains(catalog, StringComparer.OrdinalIgnoreCase)) continue;

                string? file = TableGlass.FindCatalog(catalog + ".AGF", lensPath);
                if (file == null)
                {
                    notes.Add($"The lens's glass catalog {catalog} was not found beside the lens or in Documents\\Zemax\\Glasscat.");
                    continue;
                }

                var probe = new Core.Glass.GlassCatalog();
                probe.LoadFile(file);
                var names = probe.InCatalog(Path.GetFileNameWithoutExtension(file)).Select(g => g.Name).ToList();
                var provides = unresolved.Where(u => names.Contains(u, StringComparer.OrdinalIgnoreCase)).ToList();
                if (provides.Count == 0) continue;

                UserGlassCatalog.AddCatalogFile(file, glass);
                notes.Add($"Glass catalog {catalog} ({names.Count} glasses, for {string.Join(", ", provides)}) loaded from {file} and copied to your glass folder.");
                unresolved.RemoveAll(u => provides.Contains(u, StringComparer.OrdinalIgnoreCase));
            }
            return notes;
        }

        private static OpticalSystem ReadCore(string filePath)
        {
            var lines = ReadFileLines(filePath);
            var system = new OpticalSystem();

            system.Title = ReadTitle(lines);
            system.FieldType = ReadFieldType(lines);
            system.IsAfocal = ReadAfocalMode(lines);
            system.Aperture = ReadAperture(lines);
            system.TelecentricObjectSpace = system.Aperture.Type == ApertureType.ObjectSpaceNA
                                            && ReadTelecentricObjectSpace(lines);
            system.Wavelengths = ReadWavelengths(lines);
            system.Fields = ReadFields(lines);
            FieldValidation.FilterImportedFields(system);
            system.Surfaces = ReadSurfaces(lines);

            SetPrimaryWavelength(system, lines);
            SetRayAiming(system, lines);
            SetGlassCatalogs(system, lines);

            // FLOA "Float by Stop Size": ReadAperture marked the EPD with value
            // 0 as a sentinel. Resolve to 2 × stop-surface SemiDiameter now that
            // surfaces are parsed. If the stop has no SemiDiameter set, the
            // hardcoded 10.0 fallback below kicks in.
            if (system.Aperture.Type == ApertureType.EPD && system.Aperture.Value == 0.0)
            {
                int floaStopIdx = system.StopSurfaceIndex;
                if (floaStopIdx > 0 && floaStopIdx < system.Surfaces.Count)
                {
                    double stopSd = system.Surfaces[floaStopIdx].SemiDiameter;
                    if (stopSd > 0)
                        system.Aperture = new Aperture(ApertureType.EPD, stopSd * 2.0);
                    else
                        system.Aperture = new Aperture(ApertureType.EPD, 10.0);
                }
                else
                {
                    system.Aperture = new Aperture(ApertureType.EPD, 10.0);
                }
            }

            // Stock-lens EPD override: if Aperture is EPD-type and the stop surface
            // has a CLAP outer radius defined, replace the ENPD-derived value with
            // the optical CA diameter (= 2 × CLAP outer radius). Vendor stock-lens
            // .zmx files set ENPD = mechanical OD (the part's full diameter), but
            // the lens's effective optical aperture is the smaller CLAP zone. Using
            // CLAP as the EPD prevents marginal rays from being launched outside
            // the lens's good optical region (which previously caused rays to
            // appear "through air" past the lens edge in layout drawings).
            if (system.Aperture.Type == ApertureType.EPD)
            {
                double stopClapOuter = ExtractStopClapOuter(lines, system.StopSurfaceIndex);
                if (stopClapOuter > 0)
                    system.Aperture = new Aperture(ApertureType.EPD, stopClapOuter * 2.0);
            }

            // NOT DONE HERE: the stock-lens stop dummy, and the trailing-dummy collapse.
            //
            // A program that composes stock parts into new designs has reason to insert a
            // zero-thickness stop surface ahead of a catalog element, and to sum away air-only
            // surfaces before the image. Both are sensible there. Both RENUMBER the surfaces
            // relative to the file that was read.
            //
            // This program exists to report a prescription and to say which surface an
            // aberration comes from, and both of those answers are useless if its surface
            // numbers disagree with the program the user is looking at. "Surface 3 is your
            // problem" has to mean surface 3 in their own file. So the surface list is
            // kept exactly as written.

            // Convert all distances from file lens unit to mm
            double scale = ReadLensUnitScale(lines);
            system.FileUnitScale = scale;
            if (scale != 1.0)
                LensUnitConverter.ConvertToMm(system, scale);

            return system;
        }

        /// <summary>
        /// For stock-lens imports: if the stop sits on a refractive vertex and a
        /// CLAP/MEMA is defined on it, insert a dummy stop in air in front of the
        /// lens element group and set the lens vertices to SemiDiameter=MEMA Fixed.
        /// </summary>
        private static void InsertStockLensStopDummy(OpticalSystem system)
        {
            int stopIdx = system.StopSurfaceIndex;
            if (stopIdx <= 0 || stopIdx >= system.Surfaces.Count - 1) return;

            var stopSurf = system.Surfaces[stopIdx];
            if (string.IsNullOrEmpty(stopSurf.Material)) return; // stop already in air
            double clap = stopSurf.SemiDiameter > 0
                ? stopSurf.SemiDiameter
                : stopSurf.ClapOuterRadius; // MEMA-only vendor files: no DIAM/CLAP, only MEMA
            if (clap <= 0) return;                                // no aperture info at all

            // Walk forward through the bonded element group: contiguous vertices
            // whose outgoing medium is glass (Material non-empty). The group ends
            // at the first surface with empty Material (air on the back side).
            int groupEnd = stopIdx;
            while (groupEnd < system.Surfaces.Count - 1
                   && !string.IsNullOrEmpty(system.Surfaces[groupEnd].Material))
            {
                groupEnd++;
            }
            // groupEnd is the air-side back surface of the element.

            double mema = 0.0;
            for (int i = stopIdx; i <= groupEnd; i++)
            {
                double v = system.Surfaces[i].ClapOuterRadius;
                if (v > mema) mema = v;
            }
            if (mema <= 0) mema = clap;
            double cap = clap > 0 ? mema / clap * 100.0 : 100.0;

            var dummy = new Surface
            {
                Type = SurfaceType.Standard,
                Radius = double.PositiveInfinity,
                Thickness = 0.0,
                Material = string.Empty,
                SemiDiameter = clap,
                SemiDiameterMode = SemiDiameterMode.Auto,
                IsStop = true,
                Comment = string.Empty
            };

            for (int i = stopIdx; i <= groupEnd; i++)
            {
                var s = system.Surfaces[i];
                s.SemiDiameter = mema;
                s.SemiDiameterMode = SemiDiameterMode.Fixed;
                s.ClearAperturePercent = cap;
                if (i == stopIdx) s.IsStop = false;
            }

            system.Surfaces.Insert(stopIdx, dummy);
            for (int i = 0; i < system.Surfaces.Count; i++)
                system.Surfaces[i].Index = i;

            // Stock-lens convention: real ray aiming buys nothing for a singlet
            // (or simple cemented multiplet) at a modest aperture. Some Edmund
            // vendor .zmx files ship with RAIM=Real anyway; force Off here so
            // the .lhlt convention is consistent regardless of source. Users
            // who genuinely need ray aiming can re-enable it after import.
            system.RayAiming = AberrationCalculator.Core.Enums.RayAimingMode.Off;

            // Some vendor .zmx files insert extra air-only "best-focus offset"
            // dummies between the last refractive vertex and IMG (e.g. Edmund
            // 29-094 / 29-095 plano-convex singlets; a few Thorlabs AC / LA /
            // LJ). For a stock-lens-as-building-block use the offset is noise
            // — host systems determine their own image plane. They also break
            // the engine's paraxial BFL = -y/u at surface[N-2] (which then
            // measures from the dummy, not the lens back). Collapse them into
            // the lens-back vertex's thickness.
            // Deliberately not collapsing trailing dummies - see the note above on
            // keeping surface numbering identical to the source file.
        }

        /// <summary>
        /// Sum any air-only surfaces between the last refractive vertex and
        /// IMG into the lens-back vertex's thickness, then remove the
        /// intermediate dummies. After this runs, the IMG surface always sits
        /// directly after the lens-back air-side vertex.
        /// </summary>
        private static void CollapseTrailingDummies(OpticalSystem system)
        {
            int n = system.Surfaces.Count;
            if (n < 4) return;

            int lastGlassIdx = -1;
            for (int i = 0; i < n; i++)
            {
                // A model glass carries no material NAME - the index comes from its nd/Vd
                // instead - so testing the name alone declares a real element to be a dummy
                // and collapses it away. An aspherized achromat, whose aspheric layer is
                // exactly such a surface, would lose that layer and end up with its image in
                // glass: the reason this test asks whether the surface has an index, not
                // whether it has a name.
                var su = system.Surfaces[i];
                if (!string.IsNullOrEmpty(su.Material) || (su.ModelIndexEnabled && su.ModelNd > 0.0))
                    lastGlassIdx = i;
            }
            if (lastGlassIdx < 0) return;

            int lastBackIdx = lastGlassIdx + 1;
            int imgIdx = n - 1;
            int nTrail = imgIdx - lastBackIdx - 1;
            if (nTrail <= 0) return;

            double sumT = 0.0;
            for (int i = lastBackIdx; i < imgIdx; i++)
                sumT += system.Surfaces[i].Thickness;
            system.Surfaces[lastBackIdx].Thickness = sumT;

            // Remove surfaces (lastBackIdx+1) through (imgIdx-1) in reverse so
            // indices stay valid through the loop.
            for (int i = imgIdx - 1; i >= lastBackIdx + 1; i--)
                system.Surfaces.RemoveAt(i);

            for (int i = 0; i < system.Surfaces.Count; i++)
                system.Surfaces[i].Index = i;
        }

        /// <summary>
        /// Walk the raw .zmx lines and return the CLAP outer radius written on
        /// the given stop-surface block, or 0 if no CLAP keyword appears there.
        /// Independent re-scan because ParseSurfaceBlock writes CLAP into
        /// surface.ClapOuterRadius only when MEMA hasn't already populated it
        /// — so the surface model alone can't tell us the original CLAP value.
        /// </summary>
        private static double ExtractStopClapOuter(string[] lines, int stopIndex)
        {
            int currentSurfIdx = -1;
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimStart();
                if (line.Length == 0) continue;
                var parts = SplitLine(line);
                if (parts.Length == 0) continue;
                if (parts[0] == "SURF" && parts.Length > 1 && int.TryParse(parts[1], out int idx))
                {
                    currentSurfIdx = idx;
                    continue;
                }
                if (currentSurfIdx == stopIndex
                    && parts[0] == "CLAP" && parts.Length >= 3
                    && TryParseDouble(parts[2], out double clapOuter))
                {
                    return clapOuter;
                }
            }
            return 0;
        }

        private static string[] ReadFileLines(string filePath)
        {
            // ZMX files can be UTF-16 LE or UTF-8
            byte[] bytes = File.ReadAllBytes(filePath);
            string[] lines;

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                string text = Encoding.Unicode.GetString(bytes);
                lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            }
            else
            {
                lines = File.ReadAllLines(filePath);
            }

            // Trim leading whitespace — some exporters (e.g. Optalix) indent keywords
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimStart();

            return lines;
        }

        private static string ReadTitle(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("TITL"))
                    return line.Length > 5 ? line.Substring(5).Trim() : string.Empty;
                // NAME is used by some exporters (e.g. Optalix) as an alias for TITL
                if (line.StartsWith("NOTE 1 ") || line.StartsWith("NAME "))
                {
                    int space = line.IndexOf(' ');
                    if (line.StartsWith("NOTE"))
                    {
                        // NOTE 1 <title>
                        int secondSpace = line.IndexOf(' ', space + 1);
                        return secondSpace >= 0 ? line.Substring(secondSpace + 1).Trim() : string.Empty;
                    }
                    return space >= 0 ? line.Substring(space + 1).Trim() : string.Empty;
                }
            }
            return string.Empty;
        }

        private static FieldType ReadFieldType(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("FTYP"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int ftype))
                    {
                        switch (ftype)
                        {
                            case 0: return FieldType.ObjectAngle;
                            case 1: return FieldType.ObjectHeight;
                            default: return FieldType.ObjectAngle;
                        }
                    }
                }
            }
            return FieldType.ObjectAngle;
        }

        private static bool ReadAfocalMode(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("FTYP"))
                {
                    var parts = SplitLine(line);
                    // FTYP format: FTYP [0]field_type [1]telecentric [2]num_fields [3]num_wavelengths [4]? [5]? [6]afocal [7]?
                    // parts[7] corresponds to FTYP_Settings[6] (0-indexed values, 1-indexed parts due to keyword)
                    if (parts.Length > 7 && int.TryParse(parts[7], out int afocal))
                    {
                        return afocal != 0;
                    }
                }
            }
            return false;
        }

        private static Aperture ReadAperture(string[] lines)
        {
            // FLOA "Float by Stop Size" supersedes ENPD/FNUM in Zemax: the
            // pupil diameter is set from the stop surface's DIAM at runtime.
            // We return EPD=0 as a sentinel here; Read() resolves the actual
            // value after surfaces are parsed (see ResolveFloaAperture).
            foreach (var line in lines)
            {
                if (line.StartsWith("FLOA"))
                    return new Aperture(ApertureType.EPD, 0.0);
            }
            foreach (var line in lines)
            {
                if (line.StartsWith("ENPD"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 1 && TryParseDouble(parts[1], out double val))
                        return new Aperture(ApertureType.EPD, val);
                }
                else if (line.StartsWith("FNUM"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 1 && TryParseDouble(parts[1], out double val))
                        return new Aperture(ApertureType.FNumber, val);
                }
                else if (line.StartsWith("OBNA"))
                {
                    // OBNA <object-space NA> <0>. The trailing field is NOT the telecentric flag —
                    // The format carries object-space telecentric in the FTYP line's 2nd field
                    // (see ReadTelecentricObjectSpace). Aperture carries only type+value.
                    var parts = SplitLine(line);
                    if (parts.Length > 1 && TryParseDouble(parts[1], out double val))
                        return new Aperture(ApertureType.ObjectSpaceNA, val);
                }
            }
            return new Aperture(ApertureType.EPD, 10.0);
        }

        /// <summary>
        /// Reads the object-space telecentric flag from the FTYP line's 2nd field
        /// (FTYP &lt;field-type&gt; &lt;telecentric&gt; &lt;num-fields&gt; &lt;num-waves&gt; ...).
        /// Only meaningful when the aperture is Object Space NA; the caller gates on that.
        /// </summary>
        private static bool ReadTelecentricObjectSpace(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("FTYP"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 2 && int.TryParse(parts[2], out int tele))
                        return tele != 0;
                    return false;
                }
            }
            return false;
        }

        private static List<Wavelength> ReadWavelengths(string[] lines)
        {
            // Read number of wavelengths from FTYP line (4th value, index 3)
            int numWavelengths = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("FTYP"))
                {
                    var parts = SplitLine(line);
                    // FTYP format: FTYP <field_type> <telecentric> <num_fields> <num_wavelengths> ...
                    if (parts.Length > 4 && int.TryParse(parts[4], out int nw))
                        numWavelengths = nw;
                    break;
                }
            }

            var wavelengths = new List<Wavelength>();

            // Try WAVM format first (one line per wavelength)
            foreach (var line in lines)
            {
                if (line.StartsWith("WAVM"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length >= 3)
                    {
                        if (TryParseDouble(parts[2], out double wl) && wl > 0)
                        {
                            double weight = 1.0;
                            if (parts.Length > 3)
                                TryParseDouble(parts[3], out weight);

                            wavelengths.Add(new Wavelength(wl, weight));

                            // Stop once we've read the expected number of wavelengths
                            if (numWavelengths > 0 && wavelengths.Count >= numWavelengths)
                                break;
                        }
                    }
                }
            }

            // Fallback: WAVL/WWGT format (all wavelengths on one line, used by some exporters)
            if (wavelengths.Count == 0)
            {
                var wlValues = new List<double>();
                var wlWeights = new List<double>();
                foreach (var line in lines)
                {
                    if (line.StartsWith("WAVL"))
                    {
                        var parts = SplitLine(line);
                        for (int i = 1; i < parts.Length; i++)
                            if (TryParseDouble(parts[i], out double wl) && wl > 0) wlValues.Add(wl);
                    }
                    else if (line.StartsWith("WWGT"))
                    {
                        var parts = SplitLine(line);
                        for (int i = 1; i < parts.Length; i++)
                            if (TryParseDouble(parts[i], out double wt)) wlWeights.Add(wt);
                    }
                }
                for (int i = 0; i < wlValues.Count; i++)
                {
                    double wt = i < wlWeights.Count ? wlWeights[i] : 1.0;
                    wavelengths.Add(new Wavelength(wlValues[i], wt));
                }
            }

            return wavelengths;
        }

        private static void SetPrimaryWavelength(OpticalSystem system, string[] lines)
        {
            int primaryIndex = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("PWAV"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int idx))
                    {
                        primaryIndex = idx - 1; // ZMX is 1-indexed
                    }
                    break;
                }
            }

            if (primaryIndex >= 0 && primaryIndex < system.Wavelengths.Count)
            {
                system.Wavelengths[primaryIndex].IsPrimary = true;
            }
            else if (system.Wavelengths.Count > 0)
            {
                system.Wavelengths[0].IsPrimary = true;
            }
        }

        private static void SetRayAiming(OpticalSystem system, string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("RAIM"))
                {
                    var parts = SplitLine(line);
                    // RAIM format: RAIM <v0> <mode> <v2> <v3> <v4> <robust> ...
                    // Mode: 0=Off, 1=Paraxial, 2=Real
                    // Robust flag at index 6 (parts[6]): 1=robust
                    if (parts.Length > 2 && int.TryParse(parts[2], out int mode))
                    {
                        bool robust = parts.Length > 6
                            && int.TryParse(parts[6], out int robustFlag) && robustFlag != 0;
                        switch (mode)
                        {
                            case 1:
                            case 2:
                                system.RayAiming = robust
                                    ? Enums.RayAimingMode.Robust
                                    : Enums.RayAimingMode.Real;
                                break;
                            default:
                                system.RayAiming = Enums.RayAimingMode.Off;
                                break;
                        }
                    }
                    break;
                }
            }
        }

        private static List<Field> ReadFields(string[] lines)
        {
            var fields = new List<Field>();
            double[] yValues = new double[0];
            double[] weights = new double[0];
            int numFields = -1; // -1 = use length-based fallback

            foreach (var line in lines)
            {
                if (line.StartsWith("YFLN"))
                {
                    yValues = ParseDoubleList(line);
                }
                else if (line.StartsWith("FWGN") || line.StartsWith("FWGT"))
                {
                    weights = ParseDoubleList(line);
                }
                else if (line.StartsWith("FTYP"))
                {
                    // FTYP format: FTYP <field_type> <telecentric> <num_fields> ...
                    // ZMX always writes 12 XFLN/YFLN slots regardless of how many
                    // fields are actually defined; honor num_fields so beam
                    // expanders and other on-axis-only systems don't import as
                    // 12 zero-field copies.
                    var parts = SplitLine(line);
                    if (parts.Length > 3 && int.TryParse(parts[3], out int nf) && nf > 0)
                        numFields = nf;
                }
            }

            int limit = numFields > 0 ? Math.Min(numFields, yValues.Length) : yValues.Length;
            for (int i = 0; i < limit; i++)
            {
                double w = (i < weights.Length && weights[i] > 0) ? weights[i] : 1.0;
                fields.Add(new Field(yValues[i], w));
            }

            // Always have at least the on-axis field
            if (fields.Count == 0)
                fields.Add(new Field(0, 1.0));

            return fields;
        }

        private static List<Surface> ReadSurfaces(string[] lines)
        {
            var surfaces = new List<Surface>();
            var surfaceBlocks = ExtractSurfaceBlocks(lines);

            foreach (var block in surfaceBlocks)
            {
                var surface = ParseSurfaceBlock(block);
                surfaces.Add(surface);
            }

            return surfaces;
        }

        private static List<List<string>> ExtractSurfaceBlocks(string[] lines)
        {
            var blocks = new List<List<string>>();
            List<string>? currentBlock = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("SURF"))
                {
                    currentBlock = new List<string> { line };
                    blocks.Add(currentBlock);
                }
                else if (currentBlock != null)
                {
                    currentBlock.Add(line);
                }
            }

            return blocks;
        }

        private static Surface ParseSurfaceBlock(List<string> block)
        {
            var surface = new Surface();

            // Parse surface index from SURF line
            var surfParts = SplitLine(block[0]);
            if (surfParts.Length > 1 && int.TryParse(surfParts[1], out int idx))
                surface.Index = idx;

            foreach (var rawLine in block)
            {
                var line = rawLine.TrimStart();
                var parts = SplitLine(line);
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "TYPE":
                        surface.Type = ParseSurfaceType(parts);
                        break;

                    case "CURV":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double curv))
                            surface.Curvature = curv;
                        break;

                    case "DISZ":
                        if (parts.Length > 1)
                        {
                            // Accept "INFINITY" / "Infinity" / "infinity" — the
                            // .zmx format uses all caps, but tolerate any
                            // casing on read.
                            if (string.Equals(parts[1], "Infinity", StringComparison.OrdinalIgnoreCase))
                                surface.Thickness = double.PositiveInfinity;
                            else if (TryParseDouble(parts[1], out double disz))
                            {
                                // Some converted .zmx files use 1e20 as the
                                // object-at-infinity sentinel instead of the
                                // "INFINITY" keyword. Normalize on read.
                                if (Math.Abs(disz) > 1e18)
                                    surface.Thickness = double.PositiveInfinity;
                                else
                                    surface.Thickness = disz;
                            }
                        }
                        break;

                    case "GLAS":
                        if (parts.Length > 1)
                        {
                            // Standard Zemax GLAS layout:
                            //   GLAS <name> <pickupFrom> <solveType> <nd> <Vd> <PartialDispersion> ...
                            // Most files have a real catalog name in parts[1]
                            // (e.g. "N-BK7"). Some converted files use a fictitious
                            // numeric label there (e.g. "6200.6030") with the
                            // real nd/V at parts[4]/parts[5] — the engine can't
                            // resolve the label, so the surface ends up
                            // unresolved-glass. When parts[4]/parts[5] are
                            // physical nd/V, repack as a Schott-style numeric
                            // code (XXX.XXXX = (nd-1)·1000 . V·100), which the
                            // index resolver can match against the loaded
                            // catalogs instead of reporting it unresolved./
                            string mat = parts[1];
                            // Model glass: name "___BLANK" with the model index
                            // parameters in parts[4..6] = Nd, Vd (Abbe), dPgF (relative
                            // partial-dispersion deviation). Import as an LT model-index
                            // surface — the index is computed from (Nd, Vd, dPgF), not a
                            // catalog lookup (GlassCatalog gives ModelIndexEnabled
                            // precedence over Material), so Material is left blank.
                            if (mat.Equals("___BLANK", StringComparison.OrdinalIgnoreCase) &&
                                parts.Length > 5 &&
                                TryParseDouble(parts[4], out double modelNd) &&
                                TryParseDouble(parts[5], out double modelVd))
                            {
                                surface.ModelIndexEnabled = true;
                                surface.ModelNd = modelNd;
                                surface.ModelVd = modelVd;
                                if (parts.Length > 6 && TryParseDouble(parts[6], out double modelDPgF))
                                    surface.ModelDPgF = modelDPgF;
                                surface.Material = "";
                                break;
                            }
                            // Only repack when the name token isn't already
                            // a real glass name. Treat any token that starts
                            // with a digit as a fictitious numeric label;
                            // letter-starting tokens (e.g. "N-BK7", "N-SF10",
                            // "___BLANK") are kept verbatim for the engine
                            // to resolve normally.
                            bool nameIsNumeric = mat.Length > 0 && char.IsDigit(mat[0]);
                            if (nameIsNumeric &&
                                parts.Length > 5 &&
                                TryParseDouble(parts[4], out double glasNd) &&
                                TryParseDouble(parts[5], out double glasVd) &&
                                glasNd >= 1.0 && glasNd <= 3.0 &&
                                glasVd >= 5.0 && glasVd <= 200.0)
                            {
                                int ndCode = (int)System.Math.Round((glasNd - 1.0) * 1000.0);
                                int vdCode = (int)System.Math.Round(glasVd * 100.0);
                                mat = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                    "{0:D3}.{1:D4}", ndCode, vdCode);
                            }
                            surface.Material = mat;
                        }
                        break;

                    case "DIAM":
                        // DIAM format: DIAM <value> <user_defined_flag> <x_dec> <y_dec> <display_flag> <comment>
                        // field 2 (user_defined_flag): 0 = auto (value is last-computed semi-diameter from ray trace),
                        //                              1 = user-fixed (value is user input, locked).
                        // Empirically verified against Edmund Optics vendor .zmx 2026-05-17: fixed surfaces write
                        // field 2 = 1; auto surfaces write field 2 = 0. The earlier "0..3" interpretation was wrong.
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double diam))
                        {
                            int diamMode = 0;
                            if (parts.Length > 2) int.TryParse(parts[2], out diamMode);

                            if (diamMode >= 1 && diam > 0)
                            {
                                // User-fixed semi-diameter
                                surface.SemiDiameter = diam;
                                surface.SemiDiameterMode = Enums.SemiDiameterMode.Fixed;
                            }
                            else if (diam > 0)
                            {
                                // Auto: keep last-computed value as initial estimate
                                surface.SemiDiameter = diam;
                            }
                        }
                        break;

                    case "CONI":
                        if (parts.Length > 1 && TryParseDouble(parts[1], out double coni))
                            surface.Conic = coni;
                        break;

                    case "PARM":
                        ParseAsphericParam(surface, parts);
                        break;

                    case "STOP":
                        surface.IsStop = true;
                        break;

                    case "COMM":
                        if (parts.Length > 1)
                            surface.Comment = string.Join(" ", parts.Skip(1));
                        break;

                    case "CLAP":
                        // CLAP inner_radius outer_radius x_decenter
                        // inner_radius : central hole (annular aperture, e.g. Cassegrain primary).
                        // outer_radius : optical clear-aperture zone (the "good" optical region).
                        //
                        // For stock-lens imports we prefer MEMA (mechanical extent) over CLAP
                        // for ClapOuterRadius (the drawn extent). Apply CLAP only if MEMA
                        // hasn't already populated it — keeps the answer order-independent
                        // regardless of which keyword the file happens to carry first.
                        // The CLAP outer is RE-EXTRACTED at end of Read() to override the
                        // system EPD aperture (see ExtractStopClapOuter).
                        if (parts.Length >= 2 && TryParseDouble(parts[1], out double clapInner))
                            surface.InnerRadius = clapInner;
                        if (parts.Length >= 3 && TryParseDouble(parts[2], out double clapOuter)
                            && surface.ClapOuterRadius <= 0)
                            surface.ClapOuterRadius = clapOuter;
                        break;

                    case "MEMA":
                        // MEMA semi_diameter ... — mechanical maximum aperture (full lens OD / 2).
                        // Drives ClapOuterRadius (drawn extent) for stock-lens imports, where
                        // we want the layout to show the part's mechanical edge, not just the
                        // optical CA zone. Overrides any CLAP value previously set on this
                        // surface (CLAP only sets ClapOuterRadius when ClapOuterRadius is 0).
                        if (parts.Length >= 2 && TryParseDouble(parts[1], out double memaR) && memaR > 0)
                            surface.ClapOuterRadius = memaR;
                        break;

                    case "OBSC":
                        // OBSC min_radius max_radius x_decenter
                        // Central obscuration (secondary mirror shadow)
                        if (parts.Length >= 3 && TryParseDouble(parts[2], out double obscMax))
                            surface.ObscurationRadius = obscMax;
                        break;

                    case "FLAP":
                        // FLAP min_radius max_radius x_decenter
                        // Floating aperture
                        if (parts.Length >= 3 && TryParseDouble(parts[2], out double flapMax))
                            surface.FloatingApertureRadius = flapMax;
                        break;
                }
            }

            return surface;
        }

        private static SurfaceType ParseSurfaceType(string[] parts)
        {
            if (parts.Length < 2) return SurfaceType.Standard;

            switch (parts[1].ToUpperInvariant())
            {
                case "STANDARD":
                    return SurfaceType.Standard;
                case "EVENASPH":
                    return SurfaceType.EvenAsphere;
                case "PARAXIAL":
                    // Ideal thin lens: PARM 1 = focal length (mm), PARM 2 = OPD mode.
                    return SurfaceType.Paraxial;
                case "COORDBRK":
                    // Coordinate break: PARM 1-5 = Decenter X, Decenter Y, Tilt X,
                    // Tilt Y, Tilt Z; PARM 6 = Order. Maps onto Surface.Parameters[0-4]
                    // + Settings[0]. (PRO trace; the format handling is public.)
                    return SurfaceType.CoordinateBreak;
                case "ABCDSURF":
                    // ABCD ray-transfer-matrix surface. PARM 1-8 = Ax,Bx,Cx,Dx,
                    // Ay,By,Cy,Dy; this implementation enforces x==y, so only PARM 1-4
                    // (A,B,C,D) are read → Surface.Parameters[0-3]. Fixed values only.
                    return SurfaceType.Abcd;
                default:
                    // Unsupported surface types default to Standard
                    return SurfaceType.Standard;
            }
        }

        private static void ParseAsphericParam(Surface surface, string[] parts)
        {
            // PARM index value
            if (parts.Length >= 3 && int.TryParse(parts[1], out int paramIndex))
            {
                if (TryParseDouble(parts[2], out double val))
                {
                    // Paraxial (ideal thin lens): PARM 1 = focal length (mm), PARM 2 = OPD
                    // mode (ignored — LT's ideal lens is always diffraction-limited).
                    if (surface.Type == SurfaceType.Paraxial)
                    {
                        if (paramIndex == 1) surface.FocalLength = val;
                        return;
                    }
                    // Coordinate break: PARM 1-5 (1-based) → Parameters[0-4]
                    // (DecX, DecY, TiltX, TiltY, TiltZ); PARM 6 → Order (Settings[0]).
                    if (surface.Type == SurfaceType.CoordinateBreak)
                    {
                        if (paramIndex >= 1 && paramIndex <= 5)
                            surface.SetParameter(paramIndex, val);
                        else if (paramIndex == 6)
                            surface.SetSetting(1, (int)System.Math.Round(val));
                        return;
                    }
                    // ABCD: PARM 1-4 = Ax,Bx,Cx,Dx → Parameters[0-3] (A,B,C,D). PARM 5-8
                    // = Ay,By,Cy,Dy are ignored (this implementation enforces x==y).
                    if (surface.Type == SurfaceType.Abcd)
                    {
                        if (paramIndex >= 1 && paramIndex <= 4)
                            surface.SetParameter(paramIndex, val);
                        return;
                    }
                    // PARM 1-8 map to AsphericCoefficients[0-7]
                    int arrayIndex = paramIndex - 1;
                    if (arrayIndex >= 0 && arrayIndex < surface.AsphericCoefficients.Length)
                    {
                        surface.AsphericCoefficients[arrayIndex] = val;
                        if (surface.Type == SurfaceType.Standard && val != 0)
                            surface.Type = SurfaceType.EvenAsphere;
                    }
                }
            }
        }

        private static double[] ParseDoubleList(string line)
        {
            var parts = SplitLine(line);
            var values = new List<double>();

            for (int i = 1; i < parts.Length; i++)
            {
                if (TryParseDouble(parts[i], out double val))
                    values.Add(val);
            }

            return values.ToArray();
        }

        private static void SetGlassCatalogs(OpticalSystem system, string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("GCAT"))
                {
                    var parts = SplitLine(line);
                    for (int i = 1; i < parts.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(parts[i]))
                            system.GlassCatalogs.Add(parts[i].ToUpperInvariant());
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Read the UNIT line and return the scale factor to convert to mm.
        /// Supported units: MM (1.0), IN (25.4), CM (10.0), M (1000.0).
        /// </summary>
        private static double ReadLensUnitScale(string[] lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("UNIT"))
                {
                    var parts = SplitLine(line);
                    if (parts.Length > 1)
                    {
                        switch (parts[1].ToUpperInvariant())
                        {
                            case "MM": return 1.0;
                            case "IN": return 25.4;
                            case "CM": return 10.0;
                            case "M":
                            case "METER": return 1000.0;
                        }
                    }
                    break;
                }
            }
            return 1.0; // default to mm
        }

        // Unit conversion now handled by LensUnitConverter.ConvertToMm()

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
