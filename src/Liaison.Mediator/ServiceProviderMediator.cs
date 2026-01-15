using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Liaison.Mediator;

internal sealed class ServiceProviderMediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ConcurrentDictionary<Type, IRequestHandlerWrapper> _requestHandlerWrappers = new();
    private readonly ConcurrentDictionary<Type, INotificationHandlerWrapper> _notificationHandlerWrappers = new();

    public ServiceProviderMediator(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestType = request.GetType();
        var wrapper = _requestHandlerWrappers.GetOrAdd(requestType, CreateRequestHandlerWrapper);
        var response = await wrapper.Handle(request, cancellationToken).ConfigureAwait(false);

        if (response is null)
        {
            if (default(TResponse) is null)
            {
                return default!;
            }

            throw new InvalidOperationException(
                $"The handler for request type '{requestType.FullName}' returned null but '{typeof(TResponse).FullName}' is not nullable.");
        }

        if (response is TResponse typedResponse)
        {
            return typedResponse;
        }

        throw new InvalidOperationException(
            $"Handler registered for '{requestType.FullName}' returned '{response.GetType().FullName}' which cannot be cast to '{typeof(TResponse).FullName}'.");
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        var notificationType = notification.GetType();
        var wrapper = _notificationHandlerWrappers.GetOrAdd(notificationType, CreateNotificationHandlerWrapper);

        return wrapper.Handle(notification, cancellationToken);
    }

    private IRequestHandlerWrapper CreateRequestHandlerWrapper(Type requestType)
    {
        var requestInterface = requestType
            .GetTypeInfo()
            .ImplementedInterfaces
            .Where(static candidate => candidate.IsGenericType)
            .Where(static candidate => candidate.GetGenericTypeDefinition() == typeof(IRequest<>))
            .Select(static candidate => new
            {
                RequestInterface = candidate,
                ResponseType = candidate.GetGenericArguments()[0],
            })
            .FirstOrDefault();

        if (requestInterface is null)
        {
            throw new InvalidOperationException($"Request type '{requestType.FullName}' does not implement '{typeof(IRequest<>).FullName}'.");
        }

        var wrapperType = typeof(ServiceProviderRequestHandlerWrapper<,>).MakeGenericType(requestType, requestInterface.ResponseType);

        return (IRequestHandlerWrapper)Activator.CreateInstance(wrapperType, _serviceProvider)!;
    }

    private INotificationHandlerWrapper CreateNotificationHandlerWrapper(Type notificationType)
    {
        var wrapperType = typeof(ServiceProviderNotificationHandlerWrapper<>).MakeGenericType(notificationType);

        return (INotificationHandlerWrapper)Activator.CreateInstance(wrapperType, _serviceProvider, _notificationPublisher)!;
    }

    private sealed class ServiceProviderRequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper
        where TRequest : IRequest<TResponse>
    {
        private readonly IServiceProvider _serviceProvider;

        public ServiceProviderRequestHandlerWrapper(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<object?> Handle(object request, CancellationToken cancellationToken)
        {
            if (request is not TRequest typedRequest)
            {
                throw new ArgumentException($"Request must be of type {typeof(TRequest)}.", nameof(request));
            }

            var handlers = _serviceProvider.GetServices<IRequestHandler<TRequest, TResponse>>().ToArray();
            if (handlers.Length == 0)
            {
                throw new InvalidOperationException($"No handler registered for request type '{typeof(TRequest).FullName}'.");
            }

            if (handlers.Length > 1)
            {
                throw new InvalidOperationException($"Multiple handlers registered for request type '{typeof(TRequest).FullName}'.");
            }

            var handler = handlers[0];
            var behaviors = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

            RequestHandlerDelegate<TResponse> next = () => handler.Handle(typedRequest, cancellationToken);

            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var continuation = next;
                next = () => behavior.Handle(typedRequest, continuation, cancellationToken);
            }

            var response = await next().ConfigureAwait(false);

            return response;
        }
    }

    private sealed class ServiceProviderNotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
        where TNotification : INotification
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INotificationPublisher _notificationPublisher;

        public ServiceProviderNotificationHandlerWrapper(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
        }

        public Task Handle(object notification, CancellationToken cancellationToken)
        {
            if (notification is not TNotification typedNotification)
            {
                throw new ArgumentException($"Notification must be of type {typeof(TNotification)}.", nameof(notification));
            }

            var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>().ToArray();
            if (handlers.Length == 0)
            {
                return Task.CompletedTask;
            }

            var executors = handlers.Select(handler =>
                new NotificationHandlerExecutor(
                    handler,
                    (not, ct) => handler.Handle((TNotification)not, ct)));

            return _notificationPublisher.Publish(executors, typedNotification, cancellationToken);
        }
    }
}
