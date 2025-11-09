# Liaison.Mediator

Liaison.Mediator is a lightweight mediator library for .NET that keeps the familiar request/response and notification patterns of [MediatR](https://github.com/jbogard/MediatR) while removing assembly scanning. Handlers are registered explicitly via a fluent builder so the runtime footprint stays small and configuration is fully deterministic.

## Features

- **Explicit registration** – Use `MediatorBuilder` to register request and notification handlers without reflection or assembly scanning.
- **Request/response messaging** – Define requests by implementing `IRequest<TResult>` and handle them through `IRequestHandler<TRequest, TResult>` implementations.
- **Notifications** – Broadcast one-way messages by implementing `INotification` and attaching one or more `INotificationHandler<TNotification>` instances.
- **Unit result helper** – Return `Unit.Value` from commands that do not need to return data.
- **Microsoft.Extensions.DependencyInjection integration** – Call `AddMediator` to discover handlers and pipeline behaviors through dependency injection.

## Getting started

1. Install the `Liaison.Mediator` package from NuGet.
2. Choose the registration style that fits your app:
   - Build an `IMediator` manually with `MediatorBuilder` for full control over handler wiring.
   - Register handlers with Microsoft.Extensions.DependencyInjection using `AddMediator` and let the container construct them.
3. Define your request/notification types and handlers, then start sending messages.

### Manual builder usage

```csharp
var builder = new MediatorBuilder();

builder.AddRequestHandler<AddTodo, Todo>(new AddTodoHandler())
       .AddNotificationHandler<TodoAdded>(new TodoAddedHandler());

IMediator mediator = builder.Build();

var result = await mediator.Send(new AddTodo("Write docs"));
await mediator.Publish(new TodoAdded(result.Id));
```

### Dependency injection usage

```csharp
var services = new ServiceCollection();

services.AddMediator(typeof(AddTodoHandler).Assembly);

using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();

var result = await mediator.Send(new AddTodo("Write docs"));
await mediator.Publish(new TodoAdded(result.Id));
```

## Release flow

- **Stable** – Tag the desired commit with the semantic version (for example `1.2.3`) and push the tag to publish the exact build.
- **Release candidates** – Every push to `master` emits `-rc.*` packages. Include `[minor]` or `[major]` in the commit message to bump the respective version component before the prerelease is generated.

## Project layout

- `src/Liaison.Mediator` – Library source, including interfaces like [`IMediator`](src/Liaison.Mediator/IMediator.cs) and the [`MediatorBuilder`](src/Liaison.Mediator/MediatorBuilder.cs).
- `tests/Liaison.Mediator.Tests` – xUnit test project with coverage for core scenarios.

## License and ownership

This project is released under the MIT License and is developed and maintained by **INNOVATIVE INFLUENCE TECHNOLOGY, LLC**. See [LICENSE](LICENSE) for full terms.
