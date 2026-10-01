// Buildable against netstandard2.0 as well as net8.0, so that a .NET FRAMEWORK program can
// use this library. That is not a preference: some host applications expose their automation
// interface over .NET Remoting, which exists only on the Framework, so a program driving one
// of those from C# has to be a Framework program - and it should still be able to call the
// same validated code the rest of this repository uses rather than a second copy of it.
//
// The only thing netstandard2.0 lacks that this library uses is the marker type the compiler
// wants for `init` accessors and records. It is a marker and nothing more.

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif

#if NETSTANDARD2_0
namespace System.Collections.Generic
{
    /// <summary>Deconstruction of a dictionary entry, which netstandard2.0 does not carry.</summary>
    internal static class KeyValuePairDeconstruction
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair,
                                                     out TKey key, out TValue value)
        {
            key = pair.Key;
            value = pair.Value;
        }
    }
}
#endif
