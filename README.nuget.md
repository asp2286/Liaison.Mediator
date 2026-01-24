# Liaison.Mediator

Lightweight mediator library for .NET with explicit (non-scanning) handler registration and a MediatR-style `IMediator` API.

## Installation

```bash
dotnet add package Liaison.Mediator
```

## Usage

### Manual builder

```csharp
using Liaison.Mediator;

IMediator mediator = new MediatorBuilder()
    .RegisterRequestHandler<Ping, string>(new PingHandler())
    .RegisterNotificationHandler<Pinged>(new PingedHandler())
    .Build();

string response = await mediator.Send(new Ping("hello"));
await mediator.Publish(new Pinged(response));
```

### Dependency injection (Microsoft.Extensions.DependencyInjection)

```csharp
using Liaison.Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddScoped<IRequestHandler<Ping, string>, PingHandler>();
services.AddScoped<INotificationHandler<Pinged>, PingedHandler>();
services.AddMediator();

using var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();

string response = await mediator.Send(new Ping("hello"));
await mediator.Publish(new Pinged(response));
```

## When to use

- You want MediatR-style request/response + notifications, but prefer explicit, deterministic handler wiring.
- You want to avoid required assembly scanning/reflection at startup.

Full documentation and benchmarks: https://github.com/asp2286/Liaison.Mediator

