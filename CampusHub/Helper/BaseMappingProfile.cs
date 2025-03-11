using AutoMapper;
using System.Reflection;

namespace CampusHub.Helper
{
    public class BaseMappingProfile : Profile
    {
        private static readonly IList<Type> _mappedTypes;

        static BaseMappingProfile()
        {
            _mappedTypes = Assembly.GetExecutingAssembly().GetExportedTypes()
                .Where(IsMappingType)
                .ToList();
        }

        public BaseMappingProfile()
        {
            foreach (var type in _mappedTypes)
            {
                ApplyMapping(type);
            }
        }

        private static bool IsMappingType(Type type)
        {
            return type.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMapFrom<>));
        }

        private void ApplyMapping(Type type)
        {
            try
            {
                var instance = Activator.CreateInstance(type);
                var methodInfo = type.GetMethod("Mapping") ?? type.GetInterface("IMapFrom`1")?.GetMethod("Mapping");

                methodInfo?.Invoke(instance, new object[] { this });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying mapping for type: {type.Name}");
                Console.WriteLine($"Exception Message: {ex.Message}");
                Console.WriteLine($"Exception Stack Trace: {ex.StackTrace}");
            }
        }
    }
}