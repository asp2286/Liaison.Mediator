using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Liaison.Mediator;
using Liaison.Mediator.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for registering mediator components with <see cref="IServiceCollection"/>.
    /// </summary>
    public static class MediatorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers mediator components located in the provided assemblies.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="assemblies">Assemblies that contain mediator handlers or pipeline behaviors.</param>
        /// <returns>The configured service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="assemblies"/> does not contain any items.</exception>
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

        services.AddScoped<IMediator>(provider =>
        {
            var builder = new MediatorBuilder();

            foreach (var registration in provider.GetServices<IMediatorRegistration>())
            {
                registration.Register(builder);
            }

            return builder.Build();
        });

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, IReadOnlyCollection<Assembly> assemblies)
    {
        var requestRegistrations = new HashSet<Type>();
        var notificationRegistrations = new HashSet<Type>();
        var pipelineRegistrations = new HashSet<Type>();

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

                foreach (var implementedInterface in type.ImplementedInterfaces)
                {
                    if (!implementedInterface.IsGenericType)
                    {
                        continue;
                    }

                    var interfaceType = implementedInterface.GetGenericTypeDefinition();
                    var genericArguments = implementedInterface.GetGenericArguments();

                    if (interfaceType == typeof(IRequestHandler<,>))
                    {
                        services.AddTransient(implementedInterface, type.AsType());
                        RegisterMediatorDescriptor(services, requestRegistrations, typeof(RequestHandlerRegistration<,>), genericArguments);
                    }
                    else if (interfaceType == typeof(INotificationHandler<>))
                    {
                        services.AddTransient(implementedInterface, type.AsType());
                        RegisterMediatorDescriptor(services, notificationRegistrations, typeof(NotificationHandlerRegistration<>), genericArguments);
                    }
                    else if (interfaceType == typeof(IPipelineBehavior<,>))
                    {
                        services.AddTransient(implementedInterface, type.AsType());
                        RegisterMediatorDescriptor(services, pipelineRegistrations, typeof(PipelineBehaviorRegistration<,>), genericArguments);
                    }
                }
            }
        }
    }

    private static void RegisterMediatorDescriptor(
        IServiceCollection services,
        ISet<Type> registrationTracker,
        Type registrationTypeDefinition,
        IReadOnlyList<Type> genericArguments)
    {
        var registrationType = registrationTypeDefinition.MakeGenericType(genericArguments.ToArray());
        if (registrationTracker.Add(registrationType))
        {
            services.AddTransient(typeof(IMediatorRegistration), registrationType);
        }
    }

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
}

namespace Liaison.Mediator.DependencyInjection
{
using System;
using System.Collections.Generic;
using System.Linq;

internal interface IMediatorRegistration
{
    void Register(MediatorBuilder builder);
}

internal sealed class RequestHandlerRegistration<TRequest, TResponse> : IMediatorRegistration
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IRequestHandler<TRequest, TResponse>> _handlers;

    public RequestHandlerRegistration(IEnumerable<IRequestHandler<TRequest, TResponse>> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public void Register(MediatorBuilder builder)
    {
        var handlers = _handlers.ToArray();
        if (handlers.Length == 0)
        {
            throw new InvalidOperationException($"No handler registered for '{typeof(TRequest).FullName}'.");
        }

        if (handlers.Length > 1)
        {
            throw new InvalidOperationException($"Multiple handlers registered for '{typeof(TRequest).FullName}'.");
        }

        builder.RegisterRequestHandler<TRequest, TResponse>(handlers[0]);
    }
}

internal sealed class NotificationHandlerRegistration<TNotification> : IMediatorRegistration
    where TNotification : INotification
{
    private readonly IEnumerable<INotificationHandler<TNotification>> _handlers;

    public NotificationHandlerRegistration(IEnumerable<INotificationHandler<TNotification>> handlers)
    {
        _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public void Register(MediatorBuilder builder)
    {
        foreach (var handler in _handlers)
        {
            builder.RegisterNotificationHandler(handler);
        }
    }
}

internal sealed class PipelineBehaviorRegistration<TRequest, TResponse> : IMediatorRegistration
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IPipelineBehavior<TRequest, TResponse>> _behaviors;

    public PipelineBehaviorRegistration(IEnumerable<IPipelineBehavior<TRequest, TResponse>> behaviors)
    {
        _behaviors = behaviors ?? throw new ArgumentNullException(nameof(behaviors));
    }

    public void Register(MediatorBuilder builder)
    {
        foreach (var behavior in _behaviors)
        {
            builder.RegisterPipelineBehavior(behavior);
        }
    }
}
}
