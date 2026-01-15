using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

/// <summary>
/// Provides a simple way to register handlers and build an <see cref="IMediator"/> instance.
/// </summary>
public sealed class MediatorBuilder
{
    private readonly Dictionary<Type, IRequestHandlerWrapper> _requestHandlers = new();
    private readonly Dictionary<Type, List<INotificationHandlerWrapper>> _notificationHandlers = new();
    private readonly Dictionary<Type, List<IPipelineBehaviorWrapper>> _pipelineBehaviors = new();

    /// <summary>
    /// Registers a request handler.
    /// </summary>
    /// <typeparam name="TRequest">Type of the request.</typeparam>
    /// <typeparam name="TResponse">Type of the response.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when another handler is already registered for the same request type.</exception>
    public MediatorBuilder RegisterRequestHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var requestType = typeof(TRequest);
        if (_requestHandlers.ContainsKey(requestType))
        {
            throw new InvalidOperationException($"A handler for '{requestType.FullName}' has already been registered.");
        }

        _requestHandlers[requestType] = new RequestHandlerWrapper<TRequest, TResponse>(handler);

        return this;
    }

    /// <summary>
    /// Registers a notification handler.
    /// </summary>
    /// <typeparam name="TNotification">Type of the notification.</typeparam>
    /// <param name="handler">The handler instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is <see langword="null"/>.</exception>
    public MediatorBuilder RegisterNotificationHandler<TNotification>(INotificationHandler<TNotification> handler)
        where TNotification : INotification
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var notificationType = typeof(TNotification);
        if (!_notificationHandlers.TryGetValue(notificationType, out var handlers))
        {
            handlers = new List<INotificationHandlerWrapper>();
            _notificationHandlers[notificationType] = handlers;
        }

        handlers.Add(new NotificationHandlerWrapper<TNotification>(handler));

        return this;
    }

    /// <summary>
    /// Registers a pipeline behavior for the specified request.
    /// </summary>
    /// <typeparam name="TRequest">Type of the request.</typeparam>
    /// <typeparam name="TResponse">Type of the response.</typeparam>
    /// <param name="behavior">The behavior instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="behavior"/> is <see langword="null"/>.</exception>
    public MediatorBuilder RegisterPipelineBehavior<TRequest, TResponse>(IPipelineBehavior<TRequest, TResponse> behavior)
        where TRequest : IRequest<TResponse>
    {
        if (behavior is null)
        {
            throw new ArgumentNullException(nameof(behavior));
        }

        var requestType = typeof(TRequest);
        if (!_pipelineBehaviors.TryGetValue(requestType, out var behaviors))
        {
            behaviors = new List<IPipelineBehaviorWrapper>();
            _pipelineBehaviors[requestType] = behaviors;
        }

        behaviors.Add(new PipelineBehaviorWrapper<TRequest, TResponse>(behavior));

        return this;
    }

    /// <summary>
    /// Builds the mediator instance.
    /// </summary>
    public IMediator Build()
    {
        var requestHandlers = new Dictionary<Type, IRequestHandlerWrapper>(_requestHandlers);
        var notificationHandlers = _notificationHandlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<INotificationHandlerWrapper>)pair.Value.ToArray());
        var pipelineBehaviors = _pipelineBehaviors.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<IPipelineBehaviorWrapper>)pair.Value.ToArray());

        return new Mediator(requestHandlers, notificationHandlers, pipelineBehaviors);
    }

    private sealed class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper
        where TRequest : IRequest<TResponse>
    {
        private readonly IRequestHandler<TRequest, TResponse> _handler;

        public RequestHandlerWrapper(IRequestHandler<TRequest, TResponse> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public async Task<object?> Handle(object request, CancellationToken cancellationToken)
        {
            if (request is not TRequest typedRequest)
            {
                throw new ArgumentException($"Request must be of type {typeof(TRequest)}.", nameof(request));
            }

            var response = await _handler.Handle(typedRequest, cancellationToken).ConfigureAwait(false);

            return response;
        }
    }

    private sealed class NotificationHandlerWrapper<TNotification> : INotificationHandlerWrapper
        where TNotification : INotification
    {
        private readonly INotificationHandler<TNotification> _handler;

        public NotificationHandlerWrapper(INotificationHandler<TNotification> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Task Handle(object notification, CancellationToken cancellationToken)
        {
            if (notification is not TNotification typedNotification)
            {
                throw new ArgumentException($"Notification must be of type {typeof(TNotification)}.", nameof(notification));
            }

            return _handler.Handle(typedNotification, cancellationToken);
        }
    }

    private sealed class PipelineBehaviorWrapper<TRequest, TResponse> : IPipelineBehaviorWrapper
        where TRequest : IRequest<TResponse>
    {
        private readonly IPipelineBehavior<TRequest, TResponse> _behavior;

        public PipelineBehaviorWrapper(IPipelineBehavior<TRequest, TResponse> behavior)
        {
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));
        }

        public async Task<object?> Handle(object request, CancellationToken cancellationToken, Func<CancellationToken, Task<object?>> next)
        {
            if (request is not TRequest typedRequest)
            {
                throw new ArgumentException($"Request must be of type {typeof(TRequest)}.", nameof(request));
            }

            async Task<TResponse> TypedNext()
            {
                var response = await next(cancellationToken).ConfigureAwait(false);
                if (response is null)
                {
                    if (default(TResponse) is null)
                    {
                        return default!;
                    }

                    throw new InvalidOperationException(
                        $"The pipeline for request type '{typeof(TRequest).FullName}' returned null but '{typeof(TResponse).FullName}' is not nullable.");
                }

                return (TResponse)response;
            }

            var result = await _behavior.Handle(typedRequest, TypedNext, cancellationToken).ConfigureAwait(false);

            return result;
        }
    }
}
