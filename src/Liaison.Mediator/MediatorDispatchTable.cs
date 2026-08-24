using System;
using System.Collections.Generic;

namespace Liaison.Mediator;

/// <summary>
/// The standard <see cref="IMediatorDispatchTable"/>: an immutable set of
/// entries keyed by concrete message type.
/// </summary>
/// <remarks>
/// The table is fully built in the constructor and never mutates afterwards,
/// so one instance is safe to share across threads, scopes, and containers —
/// this is the shape a source-generated table stores in a static property.
/// </remarks>
public sealed class MediatorDispatchTable : IMediatorDispatchTable
{
    private readonly Dictionary<Type, RequestDispatchEntry> _requestEntries = new();
    private readonly Dictionary<Type, NotificationDispatchEntry> _notificationEntries = new();

    /// <summary>
    /// Creates a table from the given entries.
    /// </summary>
    /// <param name="requestEntries">Entries for every dispatchable request type; may be empty.</param>
    /// <param name="notificationEntries">Entries for every dispatchable notification type; may be empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when either sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a sequence contains a <see langword="null"/> entry.</exception>
    /// <exception cref="InvalidOperationException">Thrown when two entries target the same message type.</exception>
    public MediatorDispatchTable(
        IEnumerable<RequestDispatchEntry> requestEntries,
        IEnumerable<NotificationDispatchEntry> notificationEntries)
    {
        if (requestEntries is null)
        {
            throw new ArgumentNullException(nameof(requestEntries));
        }

        if (notificationEntries is null)
        {
            throw new ArgumentNullException(nameof(notificationEntries));
        }

        foreach (var entry in requestEntries)
        {
            if (entry is null)
            {
                throw new ArgumentException("Entries must not contain null.", nameof(requestEntries));
            }

            if (_requestEntries.ContainsKey(entry.RequestType))
            {
                throw new InvalidOperationException(
                    $"The dispatch table already contains an entry for request type '{entry.RequestType.FullName}'.");
            }

            _requestEntries.Add(entry.RequestType, entry);
        }

        foreach (var entry in notificationEntries)
        {
            if (entry is null)
            {
                throw new ArgumentException("Entries must not contain null.", nameof(notificationEntries));
            }

            if (_notificationEntries.ContainsKey(entry.NotificationType))
            {
                throw new InvalidOperationException(
                    $"The dispatch table already contains an entry for notification type '{entry.NotificationType.FullName}'.");
            }

            _notificationEntries.Add(entry.NotificationType, entry);
        }
    }

    /// <inheritdoc />
    public RequestDispatchEntry? FindRequestEntry(Type requestType)
    {
        if (requestType is null)
        {
            throw new ArgumentNullException(nameof(requestType));
        }

        return _requestEntries.TryGetValue(requestType, out var entry) ? entry : null;
    }

    /// <inheritdoc />
    public NotificationDispatchEntry? FindNotificationEntry(Type notificationType)
    {
        if (notificationType is null)
        {
            throw new ArgumentNullException(nameof(notificationType));
        }

        return _notificationEntries.TryGetValue(notificationType, out var entry) ? entry : null;
    }
}
