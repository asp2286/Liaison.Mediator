using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Liaison.Mediator;

internal interface IRequestHandlerWrapper
{
    Task<object?> Handle(object request, CancellationToken cancellationToken);
}

internal interface INotificationHandlerWrapper
{
    Task Handle(object notification, CancellationToken cancellationToken);
}

internal sealed class Mediator : IMediator
{
    private readonly IReadOnlyDictionary<Type, IRequestHandlerWrapper> _requestHandlers;
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<INotificationHandlerWrapper>> _notificationHandlers;
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<IPipelineBehaviorWrapper>> _pipelineBehaviors;

    public Mediator(
        IReadOnlyDictionary<Type, IRequestHandlerWrapper> requestHandlers,
        IReadOnlyDictionary<Type, IReadOnlyList<INotificationHandlerWrapper>> notificationHandlers,
        IReadOnlyDictionary<Type, IReadOnlyList<IPipelineBehaviorWrapper>> pipelineBehaviors)
    {
        _requestHandlers = requestHandlers ?? throw new ArgumentNullException(nameof(requestHandlers));
        _notificationHandlers = notificationHandlers ?? throw new ArgumentNullException(nameof(notificationHandlers));
        _pipelineBehaviors = pipelineBehaviors ?? throw new ArgumentNullException(nameof(pipelineBehaviors));
    }

    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestType = request.GetType();
        if (!_requestHandlers.TryGetValue(requestType, out var handler))
        {
            throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");
        }

        var response = await InvokePipeline(request, handler, cancellationToken).ConfigureAwait(false);

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

    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        if (!_notificationHandlers.TryGetValue(notification.GetType(), out var handlers))
        {
            return;
        }

        foreach (var handler in handlers)
        {
            await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<object?> InvokePipeline(
        object request,
        IRequestHandlerWrapper handler,
        CancellationToken cancellationToken)
    {
        if (!_pipelineBehaviors.TryGetValue(request.GetType(), out var behaviors) || behaviors.Count == 0)
        {
            return handler.Handle(request, cancellationToken);
        }

        Func<CancellationToken, Task<object?>> next = ct => handler.Handle(request, ct);

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var continuation = next;
            next = ct => behavior.Handle(request, ct, continuation);
        }

        return next(cancellationToken);
    }
}

internal interface IPipelineBehaviorWrapper
{
    Task<object?> Handle(object request, CancellationToken cancellationToken, Func<CancellationToken, Task<object?>> next);
}
