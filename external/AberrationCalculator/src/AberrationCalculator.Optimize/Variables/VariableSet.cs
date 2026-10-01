using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using AberrationCalculator.Core.Models;

namespace AberrationCalculator.Optimize.Variables;

/// <summary>
/// The variables of one optimisation run, and the two operations everything else needs of
/// them: read the current design into a vector, and write a vector back into a design.
///
/// <para>The vector is in physical units - curvatures in reciprocal lens units, thicknesses in
/// lens units - so a Jacobian column means what it says and a step can be read by eye. Bounds
/// are applied on the way in by <see cref="Reflection"/>, which is the only place a value is
/// ever altered behind the caller's back and does so precisely so that no other code has to
/// know a variable is bounded at all.</para>
/// </summary>
public sealed class VariableSet
{
    private readonly List<Variable> _list = new();

    public ReadOnlyCollection<Variable> Items => _list.AsReadOnly();
    public int Count => _list.Count;
    public Variable this[int i] => _list[i];

    public void Add(Variable v)
    {
        if (v == null) throw new ArgumentNullException(nameof(v));
        _list.Add(v);
    }

    public void AddRange(IEnumerable<Variable> vs)
    {
        if (vs == null) throw new ArgumentNullException(nameof(vs));
        foreach (var v in vs) Add(v);
    }

    public void Clear() => _list.Clear();

    public string[] Names()
    {
        var names = new string[_list.Count];
        for (int i = 0; i < _list.Count; i++) names[i] = _list[i].Name;
        return names;
    }

    /// <summary>The design's present values, in physical units.</summary>
    public double[] Read(OpticalSystem system)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        var x = new double[_list.Count];
        for (int i = 0; i < _list.Count; i++) x[i] = Read(system, _list[i]);
        return x;
    }

    public static double Read(OpticalSystem system, Variable v)
    {
        var s = system.Surfaces[v.Surface];
        if (v.Kind == VariableKind.Curvature) return s.Curvature;
        if (v.Kind == VariableKind.Thickness) return s.Thickness;
        if (v.Kind == VariableKind.Conic) return s.Conic;

        int k = v.AsphericIndex;
        return k >= 0 && k < s.AsphericCoefficients.Length ? s.AsphericCoefficients[k] : 0.0;
    }

    /// <summary>
    /// Writes a vector into the design, folding each value inside its bounds on the way.
    ///
    /// <para>The folded vector is written back into <paramref name="x"/> as well, so that the
    /// caller's copy and the design cannot disagree about where the optimiser actually is - a
    /// disagreement that would make the next Jacobian the derivative at a point nobody is
    /// standing on.</para>
    /// </summary>
    public void Write(OpticalSystem system, double[] x)
    {
        if (system == null) throw new ArgumentNullException(nameof(system));
        if (x == null) throw new ArgumentNullException(nameof(x));
        if (x.Length != _list.Count)
            throw new ArgumentException("Vector length does not match the variable count.", nameof(x));

        for (int i = 0; i < _list.Count; i++)
        {
            var v = _list[i];
            double value = v.IsBounded ? Reflection.Fold(x[i], v.Min, v.Max) : x[i];
            x[i] = value;

            var s = system.Surfaces[v.Surface];
            switch (v.Kind)
            {
                case VariableKind.Curvature: s.Curvature = value; break;
                case VariableKind.Thickness: s.Thickness = value; break;
                case VariableKind.Conic: s.Conic = value; break;
                default:
                    int k = v.AsphericIndex;
                    if (k >= 0 && k < s.AsphericCoefficients.Length)
                        s.AsphericCoefficients[k] = value;
                    break;
            }
        }
    }

    /// <summary>Folds a vector inside its bounds without touching a design.</summary>
    public void Fold(double[] x)
    {
        if (x == null) throw new ArgumentNullException(nameof(x));
        for (int i = 0; i < _list.Count && i < x.Length; i++)
        {
            var v = _list[i];
            if (v.IsBounded) x[i] = Reflection.Fold(x[i], v.Min, v.Max);
        }
    }
}
