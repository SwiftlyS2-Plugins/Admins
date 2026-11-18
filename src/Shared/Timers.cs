using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Scheduler;

namespace Admins.Shared;

public static class Timers
{
    /// <summary><see cref="ISchedulerService.DelayAndRepeatBySeconds"/> that outlives a throwing callback, which SwiftlyS2 never re-arms.</summary>
    public static CancellationTokenSource DelayAndRepeatGuarded(this ISchedulerService scheduler, float delaySeconds, float periodSeconds, ILogger logger, Action task)
    {
        var quietUntil = 0L;

        return scheduler.DelayAndRepeatBySeconds(delaySeconds, periodSeconds, () =>
        {
            try
            {
                task();
            }
            catch (Exception ex)
            {
                // At most once a minute: a task failing on every run would bury the log.
                var now = Environment.TickCount64;
                if (now < quietUntil) return;

                quietUntil = now + 60_000;
                logger.LogError(ex, "A repeating task failed; it runs again on its next interval.");
            }
        });
    }
}
