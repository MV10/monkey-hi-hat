using System.Reflection;

namespace mhh
{
    public static class TypeExtensions
    {
        public static List<Type> GetAllDerivedTypes(this Type type)
        {
            return Assembly.GetAssembly(type).GetAllDerivedTypes(type);
        }

        public static List<Type> GetAllDerivedTypes(this Assembly assembly, Type type)
        {
            return assembly
                .GetTypes()
                .Where(t => t != type && type.IsSubclassOf(t))
                .ToList();
            // could also use IsAssignableFrom to find interfaces as well as classes
        }
    }
}
