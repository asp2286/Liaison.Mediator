using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Liaison.Mediator.Tests;

public class ServiceProviderMediatorPublishTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task Publish_InvokesAllNotificationHandlers_FromDI(int handlerCount)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Counter>();

        for (var i = 0; i < handlerCount; i++)
        {
            services.AddScoped<INotificationHandler<TestNotification>, CountingHandler>();
        }

        services.AddMediator();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        _ = scope.ServiceProvider.GetServices<INotificationHandler<TestNotification>>();

        await mediator.Publish(new TestNotification());

        var counter = scope.ServiceProvider.GetRequiredService<Counter>();
        Assert.Equal(handlerCount, counter.Value);
    }

    private sealed record TestNotification : INotification;

    private sealed class CountingHandler : INotificationHandler<TestNotification>
    {
        private readonly Counter _counter;

        public CountingHandler(Counter counter)
        {
            _counter = counter;
        }

        public Task Handle(TestNotification notification, CancellationToken cancellationToken)
        {
            _counter.Increment();
            return Task.CompletedTask;
        }
    }

    private sealed class Counter
    {
        private int _value;

        public int Value => _value;

        public void Increment()
        {
            Interlocked.Increment(ref _value);
        }
    }
}

