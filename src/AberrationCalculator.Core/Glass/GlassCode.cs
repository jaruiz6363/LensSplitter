using System;
using System.Globalization;

namespace AberrationCalculator.Core.Glass;

/// <summary>
/// Reads the six-digit glass code that files use in place of a glass name.
///
/// The code packs the two numbers a first-order design actually needs: the first three
/// digits are the decimals of nd, the last three are Vd times ten. 517642 is nd 1.517,
/// Vd 64.2. Older files write it with a decimal point, as 564.610, and some formats carry
/// whole prescriptions this way, so a program that only accepts catalog names cannot open them
/// at all - it sees an unknown material and quietly treats the glass as air, which turns
/// the whole lens into a flat plate.
///
/// A code is not a substitute for the real catalog entry: it fixes the index at d and the
/// slope between F and C, and says nothing about the partial dispersion, so secondary
/// colour computed from it is approximate. It is, however, exactly what the file states.
/// </summary>
public static class GlassCode
{
    /// <summary>
    /// Tries to read <paramref name="text"/> as a glass code. Accepts "517642", "517.642"
    /// and "1.517/64.2"; rejects anything else, including catalog names that merely start
    /// with digits.
    /// </summary>
    public static bool TryParse(string? text, out double nd, out double vd)
    {
        nd = 0.0;
        vd = 0.0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text!.Trim();

        // "1.517/64.2" - index and Abbe number written out in full.
        int slash = s.IndexOf('/');
        if (slash > 0)
        {
            if (double.TryParse(s.Substring(0, slash), NumberStyles.Float, CultureInfo.InvariantCulture, out double n1) &&
                double.TryParse(s.Substring(slash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double v1) &&
                n1 > 1.0 && n1 < 4.0 && v1 > 0.0 && v1 < 200.0)
            {
                nd = n1;
                vd = v1;
                return true;
            }
            return false;
        }

        // The decimal point SEPARATES the two halves, so the number of digits after it is
        // free: the same glass appears as "580.56" in one file and padded to "580.5600" in
        // another, and the classic six-digit form appears as "564.610". Three digits of nd decimals
        // before the point; whatever follows is Vd carried as a fraction of a hundred, which
        // gives 56, 56.00 and 61.0 respectively. Splitting on the point handles all three;
        // stripping it and demanding six digits handles only the middle length, and rejects
        // a whole prescription over a trailing zero.
        int dot = s.IndexOfAny(new[] { '.', ',' });
        if (dot > 0)
        {
            string whole = s.Substring(0, dot);
            string frac = s.Substring(dot + 1);
            if (whole.Length != 3 || frac.Length < 1 || frac.Length > 4) return false;
            if (!AllDigits(whole) || !AllDigits(frac)) return false;

            nd = 1.0 + int.Parse(whole, CultureInfo.InvariantCulture) / 1000.0;
            vd = double.Parse("0." + frac, CultureInfo.InvariantCulture) * 100.0;
            return nd > 1.0 && vd > 0.0;
        }

        string digits = s;
        if (digits.Length != 6 || !AllDigits(digits)) return false;

        int ndPart = int.Parse(digits.Substring(0, 3), CultureInfo.InvariantCulture);
        int vdPart = int.Parse(digits.Substring(3, 3), CultureInfo.InvariantCulture);

        nd = 1.0 + ndPart / 1000.0;
        vd = vdPart / 10.0;

        // A code of all zeros, or one implying a physically impossible glass, is not a code.
        return nd > 1.0 && vd > 0.0;
    }

    private static bool AllDigits(string s)
    {
        foreach (char c in s) if (c < '0' || c > '9') return false;
        return s.Length > 0;
    }
}
