
using OpenTK.Graphics.OpenGL;
using System.Reflection;
using System.Text.RegularExpressions;

namespace mhh;

public static class Extensions
{
    //-----------------------------------------------------------------------------------------------
    // Caching support
    //-----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns classes that inherit from the requested Type in the current Assembly.
    /// </summary>
    public static List<Type> GetAllDerivedTypes(this Type type)
    {
        return Assembly.GetAssembly(type).GetAllDerivedTypes(type);
    }

    /// <summary>
    /// Returns classes that inherit from the requested Type in the requested Assembly.
    /// </summary>
    public static List<Type> GetAllDerivedTypes(this Assembly assembly, Type type)
    {
        // the IsAssignableFrom version returns interfaces as well as subclasses
        return assembly
            .GetTypes()
            .Where(t => t != type && type.IsAssignableFrom(t))
            //.Where(t => t != type && type.IsSubclassOf(t))
            .ToList();
    }

    //-----------------------------------------------------------------------------------------------
    // Config file support
    //-----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the standard OpenGL PrimitiveType that corresponds to the MHH-supported ArrayDrawingMode.
    /// </summary>
    public static PrimitiveType GetGLDrawingMode(this ArrayDrawingMode arrayDrawingMode)
        => (PrimitiveType)arrayDrawingMode;

    /// <summary>
    /// A "safe" ConfigFile section/key-reading extension that returns an empty string if the section or key does not exist.
    /// Also makes the section and key values case-insensitve.
    /// </summary>
    public static string ReadValue(this ConfigFile config, string section, string key)
        => (config.Content.ContainsKey(section.ToLower()) && config.Content[section.ToLower()].ContainsKey(key.ToLower()))
        ? config.Content[section.ToLower()][key.ToLower()]
        : string.Empty;

    /// <summary>
    /// Config sections with content not in the key=value format use a sequential integer as the key. This
    /// returns a List of string values ordered by that key.
    /// </summary>
    public static IReadOnlyList<string> SequentialSection(this ConfigFile config, string section)
        => (config.Content.ContainsKey(section.ToLower()))
        ? config.Content[section.ToLower()].OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList()
        : new();

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static bool ToBool(this string textValue, bool defaultValue)
        => bool.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static int ToInt32(this string textValue, int defaultValue)
        => int.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static long ToLong(this string textValue, long defaultValue)
        => long.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static float ToFloat(this string textValue, float defaultValue)
        => float.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static double ToDouble(this string textValue, double defaultValue)
        => double.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// Enum-conversion helper
    /// </summary>
    public static T ToEnum<T>(this string textValue, T defaultValue)
        where T : Enum
        => Enum.IsDefined(typeof(T), textValue)
        ? Enum.TryParse(typeof(T), textValue, true, out var parsed) ? (T)parsed : defaultValue
        : defaultValue;

    /// <summary>
    /// Returns a default value for a null or whitespace string.
    /// </summary>
    public static string DefaultString(this string textValue, string defaultValue)
        => string.IsNullOrWhiteSpace(textValue) ? defaultValue : textValue;

    //-----------------------------------------------------------------------------------------------
    // Visualizer / Rendering support
    //-----------------------------------------------------------------------------------------------

    /// <summary>
    /// Finds a concrete Type by name from an IReadOnlyList<Type> collection
    /// </summary>
    public static Type FindType(this IReadOnlyList<Type> list, string typeName)
        => list.FirstOrDefault(t => t.Name.ToLowerInvariant().Equals(typeName.ToLowerInvariant()));

    /// <summary>
    /// Determines whether a character is A-Z (case-insensitive)
    /// </summary>
    public static bool IsAlpha(this string value)
        => !Regex.IsMatch(value, "[^a-zA-Z]");

    /// <summary>
    /// Converts an A-Z character to integer 0-25
    /// </summary>
    public static int ToOrdinal(this char value)
        => (int)value % 32 - 1;

    /// <summary>
    /// Converts an A-Z character to integer 0-25
    /// </summary>
    public static int ToOrdinal(this string value)
        => ((char)value[0]).ToOrdinal();

    /// <summary>
    /// Converts a 0-25 integer to string A-Z
    /// </summary>
    public static string ToAlpha(this int value)
        => ((char)('A' + value)).ToString();

    //-----------------------------------------------------------------------------------------------
    // Command Parsing support
    //-----------------------------------------------------------------------------------------------

    /// <summary>
    /// Used in command-line switch parsing. Yes, I'm that lazy.
    /// </summary>
    public static bool LowercaseEquals(this string lhv, string comparison)
        => (lhv.ToLowerInvariant().Equals(comparison.ToLowerInvariant()));
}
