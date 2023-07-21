
using OpenTK.Graphics.OpenGL;
using System.Reflection;

namespace mhh
{
    public static class Extensions
    {
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
        public static float ToFloat(this string textValue, float defaultValue)
            => float.TryParse(textValue, out var parsed) ? parsed : defaultValue;

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

        /// <summary>
        /// Finds a concrete Type by name from an IReadOnlyList<Type> collection
        /// </summary>
        public static Type FindType(this IReadOnlyList<Type> list, string typeName)
            => list.FirstOrDefault(t => t.Name.ToLowerInvariant().Equals(typeName.ToLowerInvariant()));

        /// <summary>
        /// Used in command-line switch parsing. Yes, I'm that lazy.
        /// </summary>
        public static bool LowercaseEquals(this string lhv, string comparison)
            => (lhv.ToLowerInvariant().Equals(comparison.ToLowerInvariant()));
    }
}
