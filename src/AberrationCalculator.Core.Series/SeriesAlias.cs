// This assembly is the aberration chain again, in Laurent series arithmetic.
//
// It compiles the SAME source files as AberrationCalculator.Core - linked, not copied - with
// `Scalar` aliased to a truncated Laurent series in one small parameter. See the project file
// for what that is for, and Core's Numerics/ScalarAlias.cs for why the chain is written against
// an alias at all.

global using Scalar = AberrationCalculator.Core.SeriesArithmetic.LaurentSeries;
global using SMath = AberrationCalculator.Core.SeriesArithmetic.LaurentMath;
