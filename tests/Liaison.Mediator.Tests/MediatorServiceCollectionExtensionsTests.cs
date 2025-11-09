using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Liaison.Mediator.Tests;

public class MediatorServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddMediator_RegistersMediatorAndHandlersFromAssemblies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<State>();
        services.AddMediator(typeof(MediatorServiceCollectionExtensionsTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var response = await mediator.Send(new SampleRequest(5));

        Assert.Equal(6, response);

        var state = provider.GetRequiredService<State>();
        Assert.Equal(new[] { "pipeline before", "handler", "pipeline after" }, state.Events);

        await mediator.Publish(new SampleNotification("notification"));

        Assert.Equal(new[] { "notification" }, state.Notifications);
    }

    [Fact]
    public void AddMediator_WithNullAssemblies_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddMediator((Assembly[])null!));
    }

    [Fact]
    public void AddMediator_WithNoAssemblies_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddMediator(Array.Empty<Assembly>()));
    }

    [Fact]
    public void AddMediator_WithOnlyNullAssemblyEntries_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddMediator(new Assembly[] { null! }));
    }
}

file sealed record SampleRequest(int Value) : IRequest<int>;

file sealed class SampleRequestHandler : IRequestHandler<SampleRequest, int>
{
    private readonly State _state;

    public SampleRequestHandler(State state)
    {
        _state = state;
    }

    public Task<int> Handle(SampleRequest request, CancellationToken cancellationToken)
    {
        _state.Events.Add("handler");
        return Task.FromResult(request.Value + 1);
    }
}

file sealed class SamplePipeline : IPipelineBehavior<SampleRequest, int>
{
    private readonly State _state;

    public SamplePipeline(State state)
    {
        _state = state;
    }

    public async Task<int> Handle(
        SampleRequest request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        _state.Events.Add("pipeline before");
        var response = await next(cancellationToken).ConfigureAwait(false);
        _state.Events.Add("pipeline after");
        return response;
    }
}

file sealed record SampleNotification(string Value) : INotification;

file sealed class SampleNotificationHandler : INotificationHandler<SampleNotification>
{
    private readonly State _state;

    public SampleNotificationHandler(State state)
    {
        _state = state;
    }

    public Task Handle(SampleNotification notification, CancellationToken cancellationToken)
    {
        _state.Notifications.Add(notification.Value);
        return Task.CompletedTask;
    }
}

file sealed class State
{
    public List<string> Events { get; } = new();

    public List<string> Notifications { get; } = new();
}
