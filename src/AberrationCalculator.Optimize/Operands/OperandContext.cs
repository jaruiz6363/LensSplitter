using System;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Optimize.Operands;

/// <summary>
/// The parts of the evaluation set an operand needs but which never move: which field an index
/// refers to, what each field and wavelength weighs, and which two wavelengths bound the
/// spectrum.
///
/// <para>None of this is differentiated - a field angle and a spectral weight are constants of
/// the problem - so it is worked out once when the merit function is built rather than on every
/// pass.</para>
/// </summary>
public sealed class OperandContext
{
    /// <summary>Field values, in the units the design's field type names. Index 0 is the maximum.</summary>
    public double[] Fields { get; }

    /// <summary>Weight of each field, aligned with <see cref="Fields"/>.</summary>
    public double[] FieldWeights { get; }

    /// <summary>Weight of each wavelength.</summary>
    public double[] WaveWeights { get; }

    /// <summary>The largest field, sign kept: what fractional field heights are measured against.</summary>
    public double MaxField { get; }

    /// <summary>Index of the shortest wavelength.</summary>
    public int WaveShort { get; }

    /// <summary>Index of the longest wavelength.</summary>
    public int WaveLong { get; }

    /// <summary>Index of the reference wavelength.</summary>
    public int WavePrimary { get; }

    /// <summary>
    /// True when the spectrum has more than one colour, so the chromatic operands have
    /// something to measure. On a monochromatic design LCF and AXC are identically zero, and
    /// saying so is better than differencing a wavelength against itself.
    /// </summary>
    public bool HasSpectrum => WaveShort != WaveLong;

    /// <summary>
    /// True when the object is at infinity. Informational: the real ray trace and the real-ray
    /// operands handle either conjugate.
    /// </summary>
    public bool InfiniteConjugate { get; }

    public OperandContext(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));

        double max = 0.0;
        foreach (var f in system.Fields) if (Math.Abs(f.Y) > Math.Abs(max)) max = f.Y;
        MaxField = max;

        int nf = Math.Max(1, system.Fields.Count);
        Fields = new double[nf + 1];
        FieldWeights = new double[nf + 1];
        Fields[0] = max;
        FieldWeights[0] = 1.0;
        for (int i = 0; i < nf; i++)
        {
            Fields[i + 1] = i < system.Fields.Count ? system.Fields[i].Y : 0.0;
            FieldWeights[i + 1] = i < system.Fields.Count ? system.Fields[i].Weight : 1.0;
        }

        int nw = Math.Max(1, system.Wavelengths.Count);
        WaveWeights = new double[nw];
        int shortest = 0, longest = 0;
        for (int i = 0; i < nw; i++)
        {
            WaveWeights[i] = i < system.Wavelengths.Count ? system.Wavelengths[i].Weight : 1.0;
            if (i < system.Wavelengths.Count)
            {
                if (system.Wavelengths[i].Value < system.Wavelengths[shortest].Value) shortest = i;
                if (system.Wavelengths[i].Value > system.Wavelengths[longest].Value) longest = i;
            }
        }
        WaveShort = shortest;
        WaveLong = longest;
        WavePrimary = Math.Max(0, system.PrimaryWavelengthIndex);

        double t0 = system.Surfaces.Count > 0 ? system.Surfaces[0].Thickness : double.PositiveInfinity;
        InfiniteConjugate = double.IsInfinity(t0) || Math.Abs(t0) >= 1e12;
    }

    /// <summary>Number of fields the design defines.</summary>
    public int FieldCount => Fields.Length - 1;

    /// <summary>Number of wavelengths.</summary>
    public int WaveCount => WaveWeights.Length;

    /// <summary>
    /// The field an operand's <c>hy</c> refers to, in the units the design's field type names.
    ///
    /// <para><c>hy</c> is a FRACTION of the maximum field - 0 on axis, 1 at the corner - so a
    /// merit function can ask for seven tenths of the field whether or not the design defines a
    /// field point there. The fields the design lists are what PRMSA averages over; they are not
    /// the only places a ray may be traced, and tying a ray operand to the list would mean
    /// editing the merit function every time somebody added a field.</para>
    /// </summary>
    public double FieldFor(double hy) => hy * MaxField;

    /// <summary>The wavelength an operand's index refers to: 0 is the reference colour.</summary>
    public int WaveIndex(int index) =>
        index <= 0 ? WavePrimary : Math.Min(index - 1, WaveCount - 1);
}
