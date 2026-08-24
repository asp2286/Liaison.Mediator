using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Liaison.Mediator;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering mediator components with <see cref="IServiceCollection"/>.
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    private const string ScanningRequiresUnreferencedCodeMessage =
        "Assembly scanning walks types via reflection; trimmed applications may have removed handler types or interfaces.";

    /// <summary>
    /// Registers the mediator with the service collection.
    /// Handlers and pipeline behaviors must already be registered in the container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(ServiceProviderMediator.RequiresDynamicCodeMessage)]
    public static IServiceCollection AddMediator(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(INotificationPublisher)))
        {
            services.AddSingleton<INotificationPublisher, ForeachAwaitNotificationPublisher>();
        }

        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IMediator)))
        {
            services.AddScoped<IMediator, ServiceProviderMediator>();
        }

        return services;
    }

    /// <summary>
    /// Registers the mediator using a pre-built dispatch table, keeping dependency-injection
    /// dispatch free of runtime reflection so it stays trim- and Native-AOT-clean.
    /// Handlers and pipeline behaviors must already be registered in the container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="dispatchTable">Maps every dispatchable message type to its entry; typically source-generated.</param>
    /// <returns>The configured service collection.</returns>
    /// <remarks>
    /// Table-based dispatch never falls back to reflection: sending or publishing a message type
    /// without a table entry throws <see cref="InvalidOperationException"/>. If an
    /// <see cref="IMediator"/> or <see cref="INotificationPublisher"/> registration already
    /// exists, it is left in place, mirroring the other overloads.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="dispatchTable"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddMediator(this IServiceCollection services, IMediatorDispatchTable dispatchTable)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (dispatchTable is null)
        {
            throw new ArgumentNullException(nameof(dispatchTable));
        }

        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(INotificationPublisher)))
        {
            services.AddSingleton<INotificationPublisher, ForeachAwaitNotificationPublisher>();
        }

        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IMediator)))
        {
            services.AddScoped<IMediator>(provider => new ServiceProviderMediator(
                provider,
                provider.GetRequiredService<INotificationPublisher>(),
                dispatchTable));
        }

        return services;
    }

    /// <summary>
    /// Registers mediator handlers and pipeline behaviors located in the provided assemblies.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="assemblies">Assemblies that contain mediator handlers or pipeline behaviors.</param>
    /// <returns>The configured service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> does not contain any items.</exception>
    [RequiresUnreferencedCode(ScanningRequiresUnreferencedCodeMessage)]
    [RequiresDynamicCode(ServiceProviderMediator.RequiresDynamicCodeMessage)]
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (assemblies is null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        if (assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));
        }

        var distinctAssemblies = assemblies.Where(static assembly => assembly is not null).Distinct().ToArray();
        if (distinctAssemblies.Length == 0)
        {
            throw new ArgumentException("At least one non-null assembly must be provided.", nameof(assemblies));
        }

        RegisterHandlers(services, distinctAssemblies);

        return services.AddMediator();
    }

    [RequiresUnreferencedCode(ScanningRequiresUnreferencedCodeMessage)]
    private static void RegisterHandlers(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in GetDefinedTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (type.IsNested && !type.IsNestedPublic && !type.IsNestedAssembly && !type.IsNestedFamORAssem)
                {
                    continue;
                }

                RegisterHandlerInterfaces(services, type.AsType());
            }
        }
    }

    private static void RegisterHandlerInterfaces(
        IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type implementationType)
    {
        foreach (var implementedInterface in implementationType.GetTypeInfo().ImplementedInterfaces)
        {
            if (!implementedInterface.IsGenericType)
            {
                continue;
            }

            var interfaceType = implementedInterface.GetGenericTypeDefinition();
            if (interfaceType != typeof(IRequestHandler<,>) &&
                interfaceType != typeof(INotificationHandler<>) &&
                interfaceType != typeof(IPipelineBehavior<,>))
            {
                continue;
            }

            services.AddTransient(implementedInterface, implementationType);
        }
    }

    [RequiresUnreferencedCode(ScanningRequiresUnreferencedCodeMessage)]
    private static IEnumerable<TypeInfo> GetDefinedTypes(Assembly assembly)
    {
        try
        {
            return assembly.DefinedTypes;
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null).Select(static type => type!.GetTypeInfo());
        }
    }
}
