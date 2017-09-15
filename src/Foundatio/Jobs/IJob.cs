using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Logging;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Foundatio.Jobs {
    public interface IJob {
        Task<JobResult> RunAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    public static class JobExtensions {
        public static async Task<JobResult> TryRunAsync(this IJob job, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                return await job.RunAsync(cancellationToken).AnyContext();
            } catch (Exception ex) {
                return JobResult.FromException(ex);
            }
        }

        public static async Task RunContinuousAsync(this IJob job, TimeSpan? interval = null, int iterationLimit = -1, CancellationToken cancellationToken = default(CancellationToken), Func<Task<bool>> continuationCallback = null) {
            int iterations = 0;
            string jobName = job.GetType().Name;
            var logger = job.GetLogger();

            using (logger.BeginScope($"job: {jobName}")) {
                logger.LogInformation("Starting continuous job type \"{0}\" on machine \"{1}\"...", jobName, Environment.MachineName);

                while (!cancellationToken.IsCancellationRequested && (iterationLimit < 0 || iterations < iterationLimit)) {
                    try {
                        var result = await job.TryRunAsync(cancellationToken).AnyContext();
                        LogResult(result, logger, jobName);
                        iterations++;

                        if (result.Error != null)
                            await SystemClock.SleepAsync(Math.Max(interval?.Milliseconds ?? 0, 100), cancellationToken).AnyContext();
                        else if (interval.HasValue)
                            await SystemClock.SleepAsync(interval.Value, cancellationToken).AnyContext();
                        else if (iterations % 1000 == 0) // allow for cancellation token to get set
                            await SystemClock.SleepAsync(1, cancellationToken).AnyContext();
                    } catch (OperationCanceledException) { }

                    if (continuationCallback == null || cancellationToken.IsCancellationRequested)
                        continue;

                    try {
                        if (!await continuationCallback().AnyContext())
                            break;
                    } catch (Exception ex) {
                        logger.LogError(ex, "Error in continuation callback: {0}", ex.Message);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    logger.LogTrace("Job cancellation requested.");

                logger.LogInformation("Stopping continuous job type \"{0}\" on machine \"{1}\"...", jobName, Environment.MachineName);
            }
        }

        internal static void LogResult(JobResult result, ILogger logger, string jobName) {
            if (result != null) {
                if (result.IsCancelled)
                    logger.LogWarning(result.Error, "Job run \"{0}\" cancelled: {1}", jobName, result.Message);
                else if (!result.IsSuccess)
                    logger.LogError(result.Error, "Job run \"{0}\" failed: {1}", jobName, result.Message);
                else if (!String.IsNullOrEmpty(result.Message))
                    logger.LogInformation("Job run \"{0}\" succeeded: {1}", jobName, result.Message);
                else
                    logger.LogTrace("Job run \"{0}\" succeeded.", jobName);
            } else {
                logger.LogError("Null job run result for \"{0}\".", jobName);
            }
        }
    }
}