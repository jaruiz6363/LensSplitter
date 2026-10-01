// This assembly is the aberration chain again, differentiated.
//
// It compiles the SAME source files as AberrationCalculator.Core - linked, not copied, so there
// is one source of truth and no possibility of the two drifting apart - with `Scalar` aliased
// to a forward-mode dual number instead of to `double`. Every quantity those files compute
// therefore arrives with its exact derivative with respect to one design variable attached.
//
// The types land in AberrationCalculator.Core.Ad.* rather than AberrationCalculator.Core.*
// because <RootNamespace> and the AD_NAMESPACE constant redirect them there, so the two
// arithmetics are distinct type universes and nothing can be passed from one to the other by
// accident. See ScalarAlias.cs in Core for why this is done with an alias rather than with
// .NET generic math.

global using Scalar = AberrationCalculator.Core.Ad.Dual;
global using SMath = AberrationCalculator.Core.Ad.DualMath;
