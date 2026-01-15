using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Liaison.Mediator.Tests;

public class MediatorBuilderTests
{
    [Fact]
    public async Task Send_ReturnsResponseFromRegisteredHandler()
    {
        var builder = new MediatorBuilder()
            .RegisterRequestHandler<PingRequest, string>(new PingRequest.Handler());

        var mediator = builder.Build();
        var result = await mediator.Send(new PingRequest("ping"));

        Assert.Equal("ping pong", result);
    }

    [Fact]
    public async Task Send_WhenHandlerMissing_Throws()
    {
        var mediator = new MediatorBuilder().Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new PingRequest("missing")));
    }

    [Fact]
    public async Task Publish_NotifiesAllRegisteredHandlers()
    {
        var handler1 = new CollectingNotificationHandler();
        var handler2 = new CollectingNotificationHandler();

        var mediator = new MediatorBuilder()
            .RegisterNotificationHandler<TestNotification>(handler1)
            .RegisterNotificationHandler<TestNotification>(handler2)
            .Build();

        var notification = new TestNotification("hello");
        await mediator.Publish(notification);

        Assert.Equal(new[] { notification }, handler1.Notifications);
        Assert.Equal(new[] { notification }, handler2.Notifications);
    }

    [Fact]
    public void RegisterRequestHandler_WhenDuplicate_Throws()
    {
        var builder = new MediatorBuilder()
            .RegisterRequestHandler<PingRequest, string>(new PingRequest.Handler());

        Assert.Throws<InvalidOperationException>(() =>
            builder.RegisterRequestHandler<PingRequest, string>(new PingRequest.Handler()));
    }

    [Fact]
    public async Task Send_RequestReturningUnit_Completes()
    {
        var handler = new VoidRequest.Handler();
        var mediator = new MediatorBuilder()
            .RegisterRequestHandler<VoidRequest, Unit>(handler)
            .Build();

        var result = await mediator.Send(new VoidRequest());

        Assert.Equal(Unit.Value, result);
        Assert.True(handler.Invoked);
    }

    [Fact]
    public async Task Send_InvokesPipelineBehaviorsInRegistrationOrder()
    {
        var steps = new List<string>();
        var mediator = new MediatorBuilder()
            .RegisterPipelineBehavior<PingRequest, string>(new RecordingPipeline("outer", steps))
            .RegisterPipelineBehavior<PingRequest, string>(new RecordingPipeline("inner", steps))
            .RegisterRequestHandler<PingRequest, string>(new PingRequest.Handler(steps))
            .Build();

        var response = await mediator.Send(new PingRequest("value"));

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
    public async Task Send_PipelineCanShortCircuitHandler()
    {
        var handler = new PingRequest.Handler();
        var mediator = new MediatorBuilder()
            .RegisterPipelineBehavior<PingRequest, string>(new ShortCircuitPipeline())
            .RegisterRequestHandler<PingRequest, string>(handler)
            .Build();

        var response = await mediator.Send(new PingRequest("ignored"));

        Assert.Equal("short-circuited", response);
        Assert.False(handler.Invoked);
    }

    private sealed record PingRequest(string Message) : IRequest<string>
    {
        internal sealed class Handler : IRequestHandler<PingRequest, string>
        {
            private readonly List<string>? _steps;

            public bool Invoked { get; private set; }

            public Handler()
            {
            }

            public Handler(List<string> steps)
            {
                _steps = steps;
            }

            public Task<string> Handle(PingRequest request, CancellationToken cancellationToken)
            {
                Invoked = true;
                _steps?.Add("handler");

                return Task.FromResult($"{request.Message} pong");
            }
        }
    }

    private sealed record TestNotification(string Value) : INotification;

    private sealed class CollectingNotificationHandler : INotificationHandler<TestNotification>
    {
        private readonly List<TestNotification> _notifications = new();

        public IReadOnlyList<TestNotification> Notifications => _notifications;

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            _notifications.Add(notification);

            return Task.CompletedTask;
        }
    }

    private sealed record VoidRequest : IRequest<Unit>
    {
        internal sealed class Handler : IRequestHandler<VoidRequest, Unit>
        {
            public bool Invoked { get; private set; }

            public Task<Unit> Handle(VoidRequest request, CancellationToken cancellationToken)
            {
                Invoked = true;

                return Task.FromResult(Unit.Value);
            }
        }
    }

    private sealed class RecordingPipeline : IPipelineBehavior<PingRequest, string>
    {
        private readonly string _name;
        private readonly List<string> _steps;

        public RecordingPipeline(string name, List<string> steps)
        {
            _name = name;
            _steps = steps;
        }

        public async Task<string> Handle(
            PingRequest request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            _steps.Add($"{_name} before");
            var response = await next().ConfigureAwait(false);
            _steps.Add($"{_name} after");

            return response;
        }
    }

    private sealed class ShortCircuitPipeline : IPipelineBehavior<PingRequest, string>
    {
        public Task<string> Handle(
            PingRequest request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            return Task.FromResult("short-circuited");
        }
    }
}
