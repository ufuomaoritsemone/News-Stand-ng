using Microsoft.Extensions.Logging;

namespace NigerianNewGrid.Extensions;

/// <summary>
/// Extension methods for fire-and-forget task execution with fault isolation.
/// Ensures unobserved task exceptions are logged instead of crashing the process.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a <see cref="Task"/> without awaiting, catching and logging any exceptions.
    /// Use this instead of <c>_ = SomeAsync()</c> to prevent <see cref="TaskScheduler.UnobservedTaskException"/>.
    /// </summary>
    public static async void SafeFireAndForget(this Task task, ILogger? logger = null, string? context = null)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected and benign; do not log as a warning.
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                logger.LogWarning(ex, "Fire-and-forget task failed{Context}.",
                    string.IsNullOrWhiteSpace(context) ? string.Empty : $" [{context}]");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SafeFireAndForget] {context ?? "Task"} failed: {ex.Message}");
            }
        }
    }
}
