using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Liaison.Mediator.Tests;

public class DispatchTableTests
{
    /// <summary>
    /// Hand-written mirror of the output the source generator will emit: a static
    /// class exposing one immutable table built from the closed-generic factories.
    /// Proves the library surface is generator-sufficient before the generator exists.
    /// </summary>
    private static class HandWrittenDispatch
    {
        public static IMediatorDispatchTable Table { get; } = new MediatorDispatchTable(
            new[]
            {
                MediatorDispatch.ForRequest<TablePing, string>(),
                MediatorDispatch.ForRequest<TableVoidRequest, Unit>(),
            },
            new[]
            {
                MediatorDispatch.ForNotification<TableNotification>(),
            });
    }

    [Fact]
    public async Task Send_TableMode_ReturnsHandlerResponse()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TablePing, string>, TablePingHandler>();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new TablePing("ping"));

        Assert.Equal("ping pong", response);
    }

    [Fact]
    public async Task Send_TableMode_UnitRequest_Completes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TableVoidRequest, Unit>, TableVoidHandler>();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TableVoidRequest());

        Assert.Equal(Unit.Value, result);
    }

    [Fact]
    public async Task Publish_TableMode_InvokesAllHandlers()
    {
        var first = new CountingTableHandler();
        var second = new CountingTableHandler();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TableNotification>>(first);
        services.AddSingleton<INotificationHandler<TableNotification>>(second);
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Publish(new TableNotification());

        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(1, second.InvocationCount);
    }

    [Fact]
    public async Task Send_TableMode_MissingTableEntry_Throws()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<OrphanRequest, string>, OrphanRequestHandler>();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new OrphanRequest()));

        Assert.Contains(typeof(OrphanRequest).FullName!, exception.Message);
        Assert.Contains("has no entry in the mediator dispatch table", exception.Message);
        Assert.Contains("never falls back to reflection", exception.Message);
    }

    [Fact]
    public async Task Publish_TableMode_MissingTableEntry_Throws()
    {
        var services = new ServiceCollection();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Publish(new OrphanNotification()));

        Assert.Contains(typeof(OrphanNotification).FullName!, exception.Message);
        Assert.Contains("has no entry in the mediator dispatch table", exception.Message);
    }

    [Fact]
    public async Task Send_TableMode_RunsPipelineBehaviorsInRegistrationOrder()
    {
        var steps = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton<IPipelineBehavior<TablePing, string>>(new RecordingBehavior("outer", steps));
        services.AddSingleton<IPipelineBehavior<TablePing, string>>(new RecordingBehavior("inner", steps));
        services.AddSingleton<IRequestHandler<TablePing, string>>(new TablePingHandler(steps));
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new TablePing("value"));

        Assert.Equal("value pong", response);
        Assert.Equal(
            new[]
            {
                "outer before",
                "inner before",
                "handler",
                "inner after",
                "outer after",
            },
            steps);
    }

    [Fact]
    public async Task Send_TableMode_DuplicateHandlers_ThrowsInvalidOperationExceptionDirectly()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TablePing, string>, TablePingHandler>();
        services.AddScoped<IRequestHandler<TablePing, string>, TablePingHandler>();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // On the table path the wrapper is constructed through a closed-generic factory,
        // not Activator.CreateInstance, so the validation error is NOT wrapped in
        // TargetInvocationException (contrast: NotificationPublisherTests
        // .DI_DuplicateHandlers_ThrowsAtFirstSend pins the wrapped form on the legacy path).
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new TablePing("ping")));

        Assert.Contains("Multiple handlers registered for request type", exception.Message);
        Assert.Contains(typeof(TablePing).FullName!, exception.Message);
    }

    [Fact]
    public async Task Send_TableMode_MissingHandlerRegistration_ThrowsInvalidOperationExceptionDirectly()
    {
        var services = new ServiceCollection();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new TablePing("ping")));

        Assert.Contains("No handler registered for request type", exception.Message);
        Assert.Contains(typeof(TablePing).FullName!, exception.Message);
    }

    [Fact]
    public async Task Publish_TableMode_NoHandlersRegistered_CompletesSilently()
    {
        var services = new ServiceCollection();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // A table entry means the type is dispatchable, not that handlers exist;
        // zero registered handlers stays a silent no-op exactly like the legacy path.
        await mediator.Publish(new TableNotification());
    }

    [Fact]
    public async Task Publish_TableMode_UsesRegisteredNotificationPublisher()
    {
        var counting = new CountingTableHandler();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, TaskWhenAllNotificationPublisher>();
        services.AddSingleton<INotificationHandler<TableNotification>>(new SyncThrowingTableHandler());
        services.AddSingleton<INotificationHandler<TableNotification>>(counting);
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // TaskWhenAll semantics prove the pre-registered publisher flows into table
        // mode: the handler after the throwing one still runs (ForeachAwait would
        // have stopped before it).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Publish(new TableNotification()));

        Assert.Equal(1, counting.InvocationCount);
    }

    [Fact]
    public async Task Table_SharedAcrossScopes_DispatchesInEachScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<IRequestHandler<TablePing, string>, TablePingHandler>();
        services.AddMediator(HandWrittenDispatch.Table);

        await using var provider = services.BuildServiceProvider(validateScopes: true);

        using (var firstScope = provider.CreateScope())
        {
            var mediator = firstScope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.Equal("one pong", await mediator.Send(new TablePing("one")));
        }

        using (var secondScope = provider.CreateScope())
        {
            var mediator = secondScope.ServiceProvider.GetRequiredService<IMediator>();
            Assert.Equal("two pong", await mediator.Send(new TablePing("two")));
        }
    }

    [Fact]
    public void MediatorDispatchTable_DuplicateEntry_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new MediatorDispatchTable(
            new[]
            {
                MediatorDispatch.ForRequest<TablePing, string>(),
                MediatorDispatch.ForRequest<TablePing, string>(),
            },
            Array.Empty<NotificationDispatchEntry>()));

        Assert.Contains(typeof(TablePing).FullName!, exception.Message);
    }

    [Fact]
    public void AddMediator_WithNullDispatchTable_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddMediator((IMediatorDispatchTable)null!));
    }

    private sealed record TablePing(string Message) : IRequest<string>;

    private sealed record TableVoidRequest : IRequest;

    private sealed record TableNotification : INotification;

    private sealed record OrphanRequest : IRequest<string>;

    private sealed record OrphanNotification : INotification;

    private sealed class TablePingHandler : IRequestHandler<TablePing, string>
    {
        private readonly List<string>? _steps;

        public TablePingHandler()
        {
        }

        public TablePingHandler(List<string> steps)
        {
            _steps = steps;
        }

        public Task<string> Handle(TablePing request, CancellationToken cancellationToken)
        {
            _steps?.Add("handler");

            return Task.FromResult($"{request.Message} pong");
        }
    }

    private sealed class TableVoidHandler : IRequestHandler<TableVoidRequest, Unit>
    {
        public Task<Unit> Handle(TableVoidRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }

    private sealed class OrphanRequestHandler : IRequestHandler<OrphanRequest, string>
    {
        public Task<string> Handle(OrphanRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("orphan");
        }
    }

    private sealed class CountingTableHandler : INotificationHandler<TableNotification>
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public Task Handle(TableNotification notification, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);

            return Task.CompletedTask;
        }
    }

    private sealed class SyncThrowingTableHandler : INotificationHandler<TableNotification>
    {
        public Task Handle(TableNotification notification, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("table publish failure");
        }
    }

    private sealed class RecordingBehavior : IPipelineBehavior<TablePing, string>
    {
        private readonly string _name;
        private readonly List<string> _steps;

        public RecordingBehavior(string name, List<string> steps)
        {
            _name = name;
            _steps = steps;
        }

        public async Task<string> Handle(
            TablePing request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            _steps.Add($"{_name} before");
            var response = await next().ConfigureAwait(false);
            _steps.Add($"{_name} after");

            return response;
        }
    }
}
