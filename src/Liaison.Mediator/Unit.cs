namespace Liaison.Mediator;

/// <summary>
/// Represents a void return type for request handlers.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// The shared <see cref="Unit"/> instance.
    /// </summary>
    public static Unit Value => default;
}
