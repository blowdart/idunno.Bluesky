// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.AtProto
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Starts the task in the background and suppresses all exceptions.
        /// </summary>
        /// <param name="task">The task to start.</param>
        public static void FireAndForget(this Task task)
        {
            if (!task.IsCompleted || task.IsFaulted)
            {
                _ = FireAndForgetAsync(task);
            }
        }

        /// <summary>
        /// Starts the task in the background and logs all exceptions.
        /// </summary>
        /// <param name="task">The task to start.</param>
        /// <param name="logger">The logger to log exceptions to.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all for error logging.")]
        public static async Task FireAndForgetAsync(this Task task, ILogger? logger)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (logger is not null)
                {
                    Logger.BackgroundTaskThrew(logger, ex);
                }
            }
        }

        private static async Task FireAndForgetAsync(Task task)
        {
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
