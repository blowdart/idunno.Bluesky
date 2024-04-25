// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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

        private static async Task FireAndForgetAsync(Task task)
        {
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

    }
}
