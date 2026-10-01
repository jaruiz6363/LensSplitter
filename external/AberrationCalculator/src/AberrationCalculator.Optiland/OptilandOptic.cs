using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Python.Runtime;

using AberrationCalculator.Core.Aberrations;
using AberrationCalculator.Core.Enums;
using AberrationCalculator.Core.Models;
using AberrationCalculator.Core.RayTrace;

namespace AberrationCalculator.Optiland;

/// <summary>Optiland's Seidel sums, indexed by this program's surface numbers.</summary>
public sealed class OptilandSeidel
{
    /// <summary>Per-surface contributions; entry 0 (the object) and the image are zero.</summary>
    public double[] S1 { get; init; } = Array.Empty<double>();
    public double[] S2 { get; init; } = Array.Empty<double>();
    public double[] S3 { get; init; } = Array.Empty<double>();
    public double[] S4 { get; init; } = Array.Empty<double>();
    public double[] S5 { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Totals as <c>optic.aberrations.seidels()</c> returned them - Optiland's public answer,
    /// which the per-surface split above must add up to.
    /// </summary>
    public double[] Totals { get; init; } = Array.Empty<double>();

    public double[] this[int term] => term switch
    {
        1 => S1, 2 => S2, 3 => S3, 4 => S4, 5 => S5,
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };
}

/// <summary>
/// First-order data as Optiland's own paraxial trace gives it.
///
/// <para><see cref="Efl"/> is Optiland's <c>paraxial.f2()</c>, the image-space focal length
/// n'f: it equals this program's EFL only when the image is in air.
/// <see cref="ChiefSlope"/> is the paraxial chief ray's slope in object space at full field.</para>
/// </summary>
public readonly record struct OptilandFirstOrder(double Efl, double Epd, double EntrancePupil,
                                                 double ChiefSlope);

/// <summary>
/// The same lens inside Optiland, built from the prescription this program parsed rather than
/// from Optiland's own file import.
///
/// <para>That is deliberate. Optiland 0.6.2 resolves a glass name without the catalog the file
/// names, so a lens it imported need not be the lens this program analysed. Handing the
/// prescription over explicitly - radii, thicknesses, conics, even-asphere coefficients and
/// the indices at one wavelength - makes both programs work on the same lens, and any
/// difference is then in the calculation. See docs/optiland.md.</para>
///
/// <para>The Optiland calls live in <c>python/abcalc_optiland.py</c>; this class passes JSON to
/// it and reads JSON back, so only a few Python.NET calls are needed.</para>
/// </summary>
public sealed class OptilandOptic
{
    private readonly PyObject _module;
    private readonly PyObject _optic;
    private readonly OpticalSystem _system;
    private readonly ParaxialResult _paraxial;

    /// <summary>The JSON handed to Optiland, kept so a disagreement can be reproduced in Python alone.</summary>
    public string PrescriptionJson { get; }

    private OptilandOptic(PyObject module, PyObject optic, OpticalSystem system, ParaxialResult paraxial,
                           string prescriptionJson)
    {
        PrescriptionJson = prescriptionJson;
        _module = module;
        _optic = optic;
        _system = system;
        _paraxial = paraxial;
    }

    /// <summary>What this bridge cannot hand to Optiland. Null when the lens can be built.</summary>
    public static string? Unsupported(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (system.Surfaces[0].HasPolynomialTerms()) return "the object surface carries even-asphere terms";
        for (int i = 1; i < system.Surfaces.Count; i++)
        {
            var s = system.Surfaces[i];
            if (s.Type != SurfaceType.Standard && s.Type != SurfaceType.EvenAsphere)
                return $"surface {i} is of type {s.Type}";
            if (s.IsPerturbed) return $"surface {i} is decentred or tilted";
            if (s.HasZernike) return $"surface {i} carries Zernike terms";
        }
        if (system.Surfaces[system.ImageSurfaceIndex].HasPolynomialTerms())
            return "the image surface carries even-asphere terms";
        return null;
    }

    /// <summary>
    /// Builds the lens inside Optiland.
    ///
    /// <para>The image surface is put flat at the PARAXIAL FOCUS, wherever the file put it,
    /// because that is where the aberration coefficients are referred to and where
    /// <see cref="RealRayTrace.Trace"/> catches its rays. Seidel sums do not depend on it.</para>
    /// </summary>
    /// <param name="indices">Index after each surface at <paramref name="wavelengthUm"/>.</param>
    /// <param name="maxField">The field the paraxial trace was run at, in the file's field units.</param>
    public static OptilandOptic Build(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                      double maxField, double wavelengthUm)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (indices == null) throw new ArgumentNullException(nameof(indices));
        if (paraxial == null) throw new ArgumentNullException(nameof(paraxial));
        string? no = Unsupported(system);
        if (no != null) throw new NotSupportedException($"Optiland cross-check: {no}.");

        string spec = Prescription(system, indices, paraxial, maxField, wavelengthUm);
        return PythonSession.WithGil(() =>
        {
            var module = ImportHelper();
            var optic = module.InvokeMethod("build", new PyString(spec));
            return new OptilandOptic(module, optic, system, paraxial, spec);
        });
    }

    private static string Prescription(OpticalSystem system, double[] indices, ParaxialResult paraxial,
                                       double maxField, double wavelengthUm)
    {
        int last = system.LastOpticalSurface();
        var surfaces = new List<Dictionary<string, object?>>();
        for (int i = 0; i <= last; i++)
        {
            var s = system.Surfaces[i];
            double thickness = i == last ? paraxial.ParaxialFocusDistance : s.Thickness;

            // Trailing zeros dropped, so that a surface with none left is built as a plain
            // conic rather than as an even asphere with nothing in it.
            var coefficients = new List<double>(s.AsphericCoefficients);
            while (coefficients.Count > 0 && coefficients[coefficients.Count - 1] == 0.0)
                coefficients.RemoveAt(coefficients.Count - 1);

            surfaces.Add(new Dictionary<string, object?>
            {
                // The BASE radius. An r^2 coefficient travels in the polynomial, as it does in
                // the file, and not folded into the radius - folding it would hide exactly the
                // question of whether Optiland accounts for it.
                ["radius"] = s.Curvature == 0.0 ? null : 1.0 / s.Curvature,
                ["thickness"] = i == 0 && paraxial.InfiniteConjugate ? null
                                : double.IsInfinity(thickness) ? 0.0 : thickness,
                ["conic"] = s.Conic,
                ["coefficients"] = i == 0 ? new List<double>() : coefficients,
                ["is_stop"] = s.IsStop,
                ["mirror"] = s.IsMirror,
                ["index_after"] = Math.Abs(indices[i]),
            });
        }

        // A flat image at the paraxial focus.
        surfaces.Add(new Dictionary<string, object?>
        {
            ["radius"] = null, ["thickness"] = 0.0, ["conic"] = 0.0,
            ["coefficients"] = new List<double>(), ["is_stop"] = false, ["mirror"] = false,
            ["index_after"] = Math.Abs(indices[last]),
        });

        var spec = new Dictionary<string, object?>
        {
            ["surfaces"] = surfaces,
            ["epd"] = paraxial.Epd,
            ["field_type"] = system.FieldType == FieldType.ObjectAngle ? "angle" : "object_height",
            ["max_field"] = Math.Abs(maxField),
            ["wavelength_um"] = wavelengthUm,
        };
        return JsonSerializer.Serialize(spec);
    }

    /// <summary>
    /// Optiland's own reading of an Optiland .json FILE: each surface's geometry type and its
    /// sag at height <paramref name="r"/>, in the file's units. For checking that a file this
    /// program saved is one Optiland loads, with the figuring that was written.
    /// </summary>
    public static (string Type, double Sag)[] SagsFromFile(string path, double r)
    {
        string json = PythonSession.WithGil(() =>
            ImportHelper().InvokeMethod("sags", new PyString(Path.GetFullPath(path)),
                                        new PyFloat(r)).ToString() ?? "[]");
        using var doc = JsonDocument.Parse(json);
        var result = new List<(string, double)>();
        foreach (var e in doc.RootElement.EnumerateArray())
            result.Add((e.GetProperty("type").GetString() ?? "", Num(e.GetProperty("sag"))));
        return result.ToArray();
    }

    /// <summary>Imports the helper module, adding its folder to sys.path on first use.</summary>
    private static PyObject ImportHelper()
    {
        string dir = Path.Combine(Path.GetDirectoryName(typeof(OptilandOptic).Assembly.Location) ?? ".", "python");
        if (!File.Exists(Path.Combine(dir, "abcalc_optiland.py")))
            throw new FileNotFoundException("The Optiland helper module was not copied next to the assembly.",
                                            Path.Combine(dir, "abcalc_optiland.py"));

        dynamic sys = Py.Import("sys");
        bool present = false;
        foreach (PyObject p in sys.path) if (p.ToString() == dir) { present = true; break; }
        if (!present) sys.path.insert(0, dir);
        return Py.Import("abcalc_optiland");
    }

    /// <summary>First-order data from Optiland's own paraxial trace. NaN where Optiland gave NaN.</summary>
    public OptilandFirstOrder Describe()
    {
        string json = PythonSession.WithGil(() => _module.InvokeMethod("describe", _optic).ToString() ?? "{}");
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new OptilandFirstOrder(Num(r.GetProperty("efl")), Num(r.GetProperty("epd")),
                                      Num(r.GetProperty("epl")), Num(r.GetProperty("chief_slope")));
    }

    /// <summary>A number, or NaN where the Python side sent null for a non-finite value.</summary>
    private static double Num(JsonElement v) => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : double.NaN;

    /// <summary>Optiland's Seidel sums, totals and per surface.</summary>
    public OptilandSeidel Seidels()
    {
        string json = PythonSession.WithGil(() => _module.InvokeMethod("seidels", _optic).ToString() ?? "{}");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int count = _system.Surfaces.Count;
        var split = new double[5][];
        int t = 0;
        foreach (var term in root.GetProperty("per_surface").EnumerateArray())
        {
            // Optiland's terms run over its surfaces 1..N-2: every surface but the object and
            // the image, which is this program's 1..LastOpticalSurface.
            var row = new double[count];
            int j = 1;
            foreach (var v in term.EnumerateArray()) row[j++] = Num(v);
            split[t++] = row;
        }

        var totals = new List<double>();
        foreach (var v in root.GetProperty("totals").EnumerateArray()) totals.Add(Num(v));

        return new OptilandSeidel
        {
            S1 = split[0], S2 = split[1], S3 = split[2], S4 = split[3], S5 = split[4],
            Totals = totals.ToArray(),
        };
    }

    /// <summary>
    /// Traces a batch of rays with Optiland's real-ray trace, in one call, and returns where
    /// each met the paraxial image plane - the same quantity <see cref="RealRayTrace.Trace"/>
    /// returns, so the two can be swapped. Object at infinity only, as there.
    /// </summary>
    public RealRayTrace.Landing[] Trace(IReadOnlyList<CoefficientInversion.RayRequest> rays)
    {
        if (rays == null) throw new ArgumentNullException(nameof(rays));
        if (!_paraxial.InfiniteConjugate)
            throw new NotSupportedException("Optiland rays are launched for an object at infinity only.");

        var field = new double[rays.Count];
        var py = new double[rays.Count];
        var pz = new double[rays.Count];
        for (int k = 0; k < rays.Count; k++)
        {
            field[k] = rays[k].FieldDeg;
            py[k] = rays[k].Py;
            pz[k] = rays[k].Pz;
        }

        // Far enough in front of surface 1 that no ray starts behind it, whatever its sag.
        double r1 = _system.Surfaces[1].Curvature == 0.0 ? 0.0 : Math.Abs(1.0 / _system.Surfaces[1].Curvature);
        var spec = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["field_deg"] = field, ["py"] = py, ["pz"] = pz,
            ["epr"] = 0.5 * _paraxial.Epd,
            ["ep"] = _paraxial.EntrancePupilPosition,
            ["start_offset"] = 10.0 + _paraxial.Epd + Math.Min(r1, 1e3),
        });

        string json = PythonSession.WithGil(() =>
            _module.InvokeMethod("trace", _optic, new PyString(spec)).ToString() ?? "{}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var x = root.GetProperty("x");
        var y = root.GetProperty("y");
        var ok = root.GetProperty("ok");
        var result = new RealRayTrace.Landing[rays.Count];
        for (int k = 0; k < rays.Count; k++)
        {
            bool good = ok[k].GetBoolean();
            // Landing names the sagittal coordinate Z.
            result[k] = good ? new RealRayTrace.Landing(y[k].GetDouble(), x[k].GetDouble(), true)
                             : new RealRayTrace.Landing(double.NaN, double.NaN, false);
        }
        return result;
    }
}

internal static class SurfaceExtensions
{
    public static bool HasPolynomialTerms(this Surface s)
    {
        foreach (double a in s.AsphericCoefficients) if (a != 0.0) return true;
        return false;
    }
}
