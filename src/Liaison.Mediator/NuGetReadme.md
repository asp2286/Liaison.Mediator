# Liaison.Mediator

Liaison.Mediator is a lightweight mediator library that keeps the familiar request/response and notification patterns of MediatR while removing assembly scanning. Register handlers explicitly or opt into dependency injection support via `AddMediator`.

## Installation

```powershell
Install-Package Liaison.Mediator
```

## Usage

### Manual builder

```csharp
var builder = new MediatorBuilder();

builder.AddRequestHandler<CreateOrder, Order>(new CreateOrderHandler())
       .AddNotificationHandler<OrderCreated>(new OrderCreatedHandler());

IMediator mediator = builder.Build();
```

### Microsoft.Extensions.DependencyInjection

```csharp
var services = new ServiceCollection();
services.AddMediator(typeof(CreateOrderHandler).Assembly);

await using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();
```

## Documentation

Find samples and additional guidance on GitHub: https://github.com/asp2286/Liaison.Mediator
