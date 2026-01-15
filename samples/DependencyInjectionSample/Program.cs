using Liaison.Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<TodoRepository>();
services.AddScoped<IRequestHandler<AddTodo, Todo>, AddTodoHandler>();
services.AddScoped<INotificationHandler<TodoAdded>, TodoAddedHandler>();
services.AddMediator();

await using ServiceProvider provider = services.BuildServiceProvider();
IMediator mediator = provider.GetRequiredService<IMediator>();

Todo todo = await mediator.Send(new AddTodo("Ship the release"));
await mediator.Publish(new TodoAdded(todo.Id, todo.Title));

public sealed record AddTodo(string Title) : IRequest<Todo>;

public sealed record Todo(int Id, string Title);

public sealed class AddTodoHandler : IRequestHandler<AddTodo, Todo>
{
    private readonly TodoRepository _repository;

    public AddTodoHandler(TodoRepository repository)
    {
        _repository = repository;
    }

    public Task<Todo> Handle(AddTodo request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_repository.Create(request.Title));
    }
}

public sealed record TodoAdded(int Id, string Title) : INotification;

public sealed class TodoAddedHandler : INotificationHandler<TodoAdded>
{
    public Task Handle(TodoAdded notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Observed todo #{notification.Id}: {notification.Title}");

        return Task.CompletedTask;
    }
}

public sealed class TodoRepository
{
    private int _nextId = 1;

    public Todo Create(string title)
    {
        return new Todo(_nextId++, title);
    }
}
