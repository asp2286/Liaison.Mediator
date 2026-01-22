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
    private readonly Func<Type, IRequestHandlerWrapper> _createRequestHandlerWrapper;
    private readonly Func<Type, INotificationHandlerWrapper> _createNotificationHandlerWrapper;

    public ServiceProviderMediator(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
        _createRequestHandlerWrapper = CreateRequestHandlerWrapper;
        _createNotificationHandlerWrapper = CreateNotificationHandlerWrapper;
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestType = request.GetType();
        var wrapper = _requestHandlerWrappers.GetOrAdd(requestType, _createRequestHandlerWrapper);
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
        var wrapper = _notificationHandlerWrappers.GetOrAdd(notificationType, _createNotificationHandlerWrapper);

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
        private readonly bool _hasPipelineBehaviors;

        public ServiceProviderRequestHandlerWrapper(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            EnsureExactlyOneHandler(serviceProvider);
            _hasPipelineBehaviors = HasPipelineBehaviors(serviceProvider);
        }

        public async Task<object?> Handle(object request, CancellationToken cancellationToken)
        {
            if (request is not TRequest typedRequest)
            {
                throw new ArgumentException($"Request must be of type {typeof(TRequest)}.", nameof(request));
            }

            var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            if (!_hasPipelineBehaviors)
            {
                return await handler.Handle(typedRequest, cancellationToken).ConfigureAwait(false);
            }

            var behaviorEnumerable = _serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var behaviors = behaviorEnumerable as IPipelineBehavior<TRequest, TResponse>[] ?? behaviorEnumerable.ToArray();
            if (behaviors.Length == 0)
            {
                return await handler.Handle(typedRequest, cancellationToken).ConfigureAwait(false);
            }

            var execution = new PipelineExecution(typedRequest, cancellationToken, handler, behaviors);
            return await execution.Next().ConfigureAwait(false);
        }

        private static void EnsureExactlyOneHandler(IServiceProvider serviceProvider)
        {
            var handlerEnumerable = serviceProvider.GetServices<IRequestHandler<TRequest, TResponse>>();
            if (handlerEnumerable is IRequestHandler<TRequest, TResponse>[] handlerArray)
            {
                if (handlerArray.Length == 0)
                {
                    throw new InvalidOperationException($"No handler registered for request type '{typeof(TRequest).FullName}'.");
                }

                if (handlerArray.Length > 1)
                {
                    throw new InvalidOperationException($"Multiple handlers registered for request type '{typeof(TRequest).FullName}'.");
                }

                return;
            }

            using var enumerator = handlerEnumerable.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException($"No handler registered for request type '{typeof(TRequest).FullName}'.");
            }

            if (enumerator.MoveNext())
            {
                throw new InvalidOperationException($"Multiple handlers registered for request type '{typeof(TRequest).FullName}'.");
            }
        }

        private static bool HasPipelineBehaviors(IServiceProvider serviceProvider)
        {
            var behaviorsEnumerable = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            if (behaviorsEnumerable is IPipelineBehavior<TRequest, TResponse>[] behaviorsArray)
            {
                return behaviorsArray.Length > 0;
            }

            using var enumerator = behaviorsEnumerable.GetEnumerator();
            return enumerator.MoveNext();
        }

        private sealed class PipelineExecution
        {
            private readonly TRequest _request;
            private readonly CancellationToken _cancellationToken;
            private readonly IRequestHandler<TRequest, TResponse> _handler;
            private readonly IPipelineBehavior<TRequest, TResponse>[] _behaviors;
            private int _nextIndex;
            private readonly RequestHandlerDelegate<TResponse> _next;

            public PipelineExecution(
                TRequest request,
                CancellationToken cancellationToken,
                IRequestHandler<TRequest, TResponse> handler,
                IPipelineBehavior<TRequest, TResponse>[] behaviors)
            {
                _request = request;
                _cancellationToken = cancellationToken;
                _handler = handler;
                _behaviors = behaviors;
                _next = Next;
            }

            public Task<TResponse> Next()
            {
                var index = _nextIndex++;
                if (index < _behaviors.Length)
                {
                    return _behaviors[index].Handle(_request, _next, _cancellationToken);
                }

                return _handler.Handle(_request, _cancellationToken);
            }
        }
    }

    private sealed class ServiceProviderNotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
        where TNotification : INotification
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly NotificationPublisherKind _publisherKind;

        public ServiceProviderNotificationHandlerWrapper(IServiceProvider serviceProvider, INotificationPublisher notificationPublisher)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _notificationPublisher = notificationPublisher ?? throw new ArgumentNullException(nameof(notificationPublisher));
            _publisherKind = NotificationPublisherKindResolver.Get(notificationPublisher);
        }

        public Task Handle(object notification, CancellationToken cancellationToken)
        {
            if (notification is not TNotification typedNotification)
            {
                throw new ArgumentException($"Notification must be of type {typeof(TNotification)}.", nameof(notification));
            }

            var handlerEnumerable = _serviceProvider.GetServices<INotificationHandler<TNotification>>();
            var handlers = handlerEnumerable as INotificationHandler<TNotification>[] ?? handlerEnumerable.ToArray();
            if (handlers.Length == 0)
            {
                return Task.CompletedTask;
            }

            return _publisherKind switch
            {
                NotificationPublisherKind.ForeachAwait => PublishForeachAwait(handlers, typedNotification, cancellationToken),
                NotificationPublisherKind.TaskWhenAll => PublishTaskWhenAll(handlers, typedNotification, cancellationToken),
                _ => PublishWithPublisher(handlers, typedNotification, cancellationToken),
            };
        }

        private static Task PublishForeachAwait(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < handlers.Length; i++)
            {
                var task = handlers[i].Handle(notification, cancellationToken);
                if (task.Status != TaskStatus.RanToCompletion)
                {
                    return PublishForeachAwaitSlow(i, task, handlers, notification, cancellationToken);
                }
            }

            return Task.CompletedTask;
        }

        private static async Task PublishForeachAwaitSlow(
            int startIndex,
            Task firstTask,
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
        {
            await firstTask.ConfigureAwait(false);
            for (var i = startIndex + 1; i < handlers.Length; i++)
            {
                await handlers[i].Handle(notification, cancellationToken).ConfigureAwait(false);
            }
        }

        private static Task PublishTaskWhenAll(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
        {
            if (handlers.Length == 1)
            {
                return handlers[0].Handle(notification, cancellationToken);
            }

            var tasks = new Task[handlers.Length];
            for (var i = 0; i < handlers.Length; i++)
            {
                tasks[i] = handlers[i].Handle(notification, cancellationToken);
            }

            return Task.WhenAll(tasks);
        }

        private Task PublishWithPublisher(
            INotificationHandler<TNotification>[] handlers,
            TNotification notification,
            CancellationToken cancellationToken)
        {
            var executors = new NotificationHandlerExecutor[handlers.Length];
            for (var i = 0; i < handlers.Length; i++)
            {
                var handler = handlers[i];
                executors[i] = new NotificationHandlerExecutor(
                    handler,
                    (not, ct) => handler.Handle((TNotification)not, ct));
            }

            return _notificationPublisher.Publish(executors, notification, cancellationToken);
        }
    }

    private enum NotificationPublisherKind
    {
        Other = 0,
        ForeachAwait = 1,
        TaskWhenAll = 2,
    }

    private static class NotificationPublisherKindResolver
    {
        public static NotificationPublisherKind Get(INotificationPublisher publisher)
            => publisher switch
            {
                ForeachAwaitNotificationPublisher => NotificationPublisherKind.ForeachAwait,
                TaskWhenAllNotificationPublisher => NotificationPublisherKind.TaskWhenAll,
                _ => NotificationPublisherKind.Other,
            };
    }
}
