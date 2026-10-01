// This assembly is the aberration chain again, in Laurent series arithmetic that also carries a
// derivative. See the project file for what that is for, and Core's Numerics/ScalarAlias.cs for
// why the chain is written against an alias at all.

global using Scalar = AberrationCalculator.Core.SeriesAd.DualSeries;
global using SMath = AberrationCalculator.Core.SeriesAd.DualSeriesMath;
