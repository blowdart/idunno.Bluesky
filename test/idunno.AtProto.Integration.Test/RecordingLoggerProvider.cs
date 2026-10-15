// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Records the entries written through it, so a test can assert on what was logged.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();
    private readonly object _waitLock = new();
    private TaskCompletionSource _entryAdded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets the entries recorded so far.
    /// </summary>
    public IReadOnlyCollection<RecordedLogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(this);
    }

    internal async Task<RecordedLogEntry> WaitForEntryAsync(Func<RecordedLogEntry, bool> predicate, CancellationToken cancellationToken)
    {
        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            Task added;
            lock (_waitLock)
            {
                RecordedLogEntry? entry = _entries.FirstOrDefault(predicate);
                if (entry is not null)
                {
                    return entry;
                }

                added = _entryAdded.Task;
            }

            await added.WaitAsync(watchdog.Token);
        }
    }

    private void Record(RecordedLogEntry entry)
    {
        lock (_waitLock)
        {
            _entries.Enqueue(entry);
            _entryAdded.TrySetResult();
            _entryAdded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(RecordingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            provider.Record(new RecordedLogEntry(logLevel, eventId.Id, formatter(state, exception), exception));
        }
    }
}

/// <summary>
/// A single entry recorded by a <see cref="RecordingLoggerProvider"/>.
/// </summary>
/// <param name="Level">The level the entry was written at.</param>
/// <param name="EventId">The numeric id of the event which was written.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception attached to the entry, if any.</param>
[ExcludeFromCodeCoverage]
internal sealed record RecordedLogEntry(LogLevel Level, int EventId, string Message, Exception? Exception);
