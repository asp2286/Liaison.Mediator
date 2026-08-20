using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Liaison.Mediator.Tests;

public class NotificationPublisherTests
{
    [Fact]
    public async Task TaskWhenAll_FirstHandlerThrowsSync_RemainingHandlersInvoked()
    {
        var second = new CountingHandler();
        var third = new CountingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, TaskWhenAllNotificationPublisher>();
        services.AddSingleton<INotificationHandler<TestNotification>>(new SyncThrowingHandler(new InvalidOperationException("first")));
        services.AddSingleton<INotificationHandler<TestNotification>>(second);
        services.AddSingleton<INotificationHandler<TestNotification>>(third);
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Task task = Task.CompletedTask;
        var syncException = Record.Exception(() => { task = mediator.Publish(new TestNotification()); });

        Assert.Null(syncException);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(1, second.InvocationCount);
        Assert.Equal(1, third.InvocationCount);
    }

    [Fact]
    public async Task TaskWhenAll_MultipleHandlersThrow_AllExceptionsAggregated()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotificationPublisher, TaskWhenAllNotificationPublisher>();
        services.AddSingleton<INotificationHandler<TestNotification>>(new SyncThrowingHandler(new InvalidOperationException("first")));
        services.AddSingleton<INotificationHandler<TestNotification>>(new FaultedTaskHandler(new NotSupportedException("second")));
        services.AddSingleton<INotificationHandler<TestNotification>>(new SyncThrowingHandler(new TimeoutException("third")));
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var task = mediator.Publish(new TestNotification());

        await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        Assert.NotNull(task.Exception);
        var innerExceptions = task.Exception!.InnerExceptions;
        Assert.Equal(3, innerExceptions.Count);
        Assert.Contains(innerExceptions, static exception => exception is InvalidOperationException);
        Assert.Contains(innerExceptions, static exception => exception is NotSupportedException);
        Assert.Contains(innerExceptions, static exception => exception is TimeoutException);
    }

    [Fact]
    public async Task TaskWhenAllPublisher_FirstExecutorThrowsSync_RemainingExecutorsInvoked()
    {
        var publisher = new TaskWhenAllNotificationPublisher();
        var secondCount = 0;
        var thirdCount = 0;
        var executors = new[]
        {
            CreateExecutor((_, _) => throw new InvalidOperationException("first")),
            CreateExecutor((_, _) => { secondCount++; return Task.CompletedTask; }),
            CreateExecutor((_, _) => { thirdCount++; return Task.CompletedTask; }),
        };

        Task task = Task.CompletedTask;
        var syncException = Record.Exception(() => { task = publisher.Publish(executors, new TestNotification(), CancellationToken.None); });

        Assert.Null(syncException);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(1, secondCount);
        Assert.Equal(1, thirdCount);
    }

    [Fact]
    public async Task TaskWhenAllPublisher_MultipleExecutorsThrow_AllExceptionsAggregated()
    {
        var publisher = new TaskWhenAllNotificationPublisher();

        static IEnumerable<NotificationHandlerExecutor> EnumerateExecutors()
        {
            yield return CreateExecutor(static (_, _) => throw new InvalidOperationException("first"));
            yield return CreateExecutor(static (_, _) => Task.FromException(new NotSupportedException("second")));
            yield return CreateExecutor(static (_, _) => throw new TimeoutException("third"));
        }

        var task = publisher.Publish(EnumerateExecutors(), new TestNotification(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        Assert.NotNull(task.Exception);
        var innerExceptions = task.Exception!.InnerExceptions;
        Assert.Equal(3, innerExceptions.Count);
        Assert.Contains(innerExceptions, static exception => exception is InvalidOperationException);
        Assert.Contains(innerExceptions, static exception => exception is NotSupportedException);
        Assert.Contains(innerExceptions, static exception => exception is TimeoutException);
    }

    [Fact]
    public async Task ForeachAwait_HandlerThrows_LaterHandlersNotInvoked()
    {
        var first = new CountingHandler();
        var third = new CountingHandler();

        var services = new ServiceCollection();
        services.AddSingleton<INotificationHandler<TestNotification>>(first);
        services.AddSingleton<INotificationHandler<TestNotification>>(new SyncThrowingHandler(new InvalidOperationException("second")));
        services.AddSingleton<INotificationHandler<TestNotification>>(third);
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Publish(new TestNotification()));

        Assert.Equal(1, first.InvocationCount);
        Assert.Equal(0, third.InvocationCount);
    }

    [Fact]
    public async Task Send_WithNullRequest_Throws_BuilderMode()
    {
        var mediator = new MediatorBuilder().Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
    }

    [Fact]
    public async Task Publish_WithNullNotification_Throws_BuilderMode()
    {
        var mediator = new MediatorBuilder().Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish<TestNotification>(null!));
    }

    [Fact]
    public async Task Send_WithNullRequest_Throws_DIMode()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
    }

    [Fact]
    public async Task Publish_WithNullNotification_Throws_DIMode()
    {
        var services = new ServiceCollection();
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish<TestNotification>(null!));
    }

    [Fact]
    public async Task DI_DuplicateHandlers_ThrowsAtFirstSend()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<DuplicateRequest, string>, DuplicateHandlerOne>();
        services.AddTransient<IRequestHandler<DuplicateRequest, string>, DuplicateHandlerTwo>();
        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Current behavior: the duplicate check runs in the wrapper constructor, which
        // Activator.CreateInstance wraps in TargetInvocationException. Pinned as-is.
        var exception = await Assert.ThrowsAsync<TargetInvocationException>(() => mediator.Send(new DuplicateRequest()));

        var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("Multiple handlers registered for request type", inner.Message);
        Assert.Contains(typeof(DuplicateRequest).FullName!, inner.Message);
    }

    private static NotificationHandlerExecutor CreateExecutor(Func<INotification, CancellationToken, Task> callback)
    {
        return new NotificationHandlerExecutor(new object(), callback);
    }

    private sealed record TestNotification : INotification;

    private sealed record DuplicateRequest : IRequest<string>;

    private sealed class DuplicateHandlerOne : IRequestHandler<DuplicateRequest, string>
    {
        public Task<string> Handle(DuplicateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("one");
        }
    }

    private sealed class DuplicateHandlerTwo : IRequestHandler<DuplicateRequest, string>
    {
        public Task<string> Handle(DuplicateRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult("two");
        }
    }

    private sealed class CountingHandler : INotificationHandler<TestNotification>
    {
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _invocationCount);

            return Task.CompletedTask;
        }
    }

    private sealed class SyncThrowingHandler : INotificationHandler<TestNotification>
    {
        private readonly Exception _exception;

        public SyncThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    private sealed class FaultedTaskHandler : INotificationHandler<TestNotification>
    {
        private readonly Exception _exception;

        public FaultedTaskHandler(Exception exception)
        {
            _exception = exception;
        }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            return Task.FromException(_exception);
        }
    }
}
