﻿
using eyecandy;
using System.Numerics;

namespace mhh.Utils;

// TODO: .net7 adds a UInt128 type from two 64-bit ints, probably better than
// BigInteger and likely to become a true value type in a future C# version
// https://learn.microsoft.com/en-us/dotnet/api/system.int128?view=net-7.0

public class CachedShader : Shader
{
    /// <summary>
    /// Combines shader pathanmes into a single input string for cache key generation.
    /// </summary>
    public static string KeySource(string vertexPathname, string fragmentPathname)
        => string.Concat(vertexPathname, "*", fragmentPathname);

    /// <summary>
    /// Produces a cache key from a set of shader pathnames.
    /// </summary>
    public static BigInteger KeyFrom(string vertexPathname, string fragmentPathname)
        => KeyFrom(KeySource(vertexPathname, fragmentPathname));

    /// <summary>
    /// Produces a cache key from a value generated by the KeySource method.
    /// </summary>
    public static BigInteger KeyFrom(string keySource)
        => CacheKeyHash.ComputeHash(keySource).ToBigInteger();

    /// <summary>
    /// The unique hash key for this shader (generated from the vert and frag pathnames).
    /// </summary>
    public readonly BigInteger Key;

    public CachedShader(string vertexPathname, string fragmentPathname) 
        : base(vertexPathname, fragmentPathname)
    {
        Key = KeyFrom(vertexPathname, fragmentPathname);
    }
}
