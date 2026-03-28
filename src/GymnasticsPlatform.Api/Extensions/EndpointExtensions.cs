using System.Reflection;

namespace GymnasticsPlatform.Api.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointGroupType = typeof(IEndpointGroup);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("System") == false)
            .ToList();

        foreach (var assembly in assemblies)
        {
            var endpointGroupTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && endpointGroupType.IsAssignableFrom(t));

            foreach (var type in endpointGroupTypes)
            {
                if (Activator.CreateInstance(type) is IEndpointGroup instance)
                {
                    instance.Map(app);
                }
            }
        }

        return app;
    }
}
