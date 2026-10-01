// The arithmetic type the aberration chain is written in.
//
// Every file that computes a coefficient - the paraxial trace, the real ray trace, Buchdahl's
// scheme and its tables, the tertiary cubics, Forbes' series trace - says `Scalar` rather than
// `double`, and `SMath` rather than `Math`. Here that alias IS `double`, so this assembly
// computes exactly what it always computed, to the bit.
//
// The point is the OTHER assembly. AberrationCalculator.Core.Ad compiles the very same source
// files with `Scalar` aliased to a forward-mode dual number, and gets the DERIVATIVE of every
// one of those quantities with respect to a design variable - analytically, to machine
// precision, with no step size to choose and no cancellation to fear. That is what the
// optimiser's Jacobian is built from.
//
// One source, two arithmetics. The alternative - .NET generic math, `INumber<T>` - founders on
// the literals: there are some five thousand numeric constants in that chain, and every one of
// them would have to be written `T.CreateChecked(...)`. A struct with an implicit conversion
// from double leaves all five thousand exactly as Buchdahl and Rimmer wrote them.

global using Scalar = System.Double;
global using SMath = AberrationCalculator.Core.Numerics.DoubleMath;
