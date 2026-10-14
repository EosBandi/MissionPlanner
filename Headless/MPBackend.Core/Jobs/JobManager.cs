using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MPBackend.Core.Jobs
{
    public enum JobState
    {
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    /// <summary>
    /// A long-running operation (connect, param download, mission upload, ...) that clients can track and cancel.
    /// </summary>
    public sealed class Job
    {
        internal readonly CancellationTokenSource Cts = new CancellationTokenSource();

        public string Id { get; } = Guid.NewGuid().ToString("N").Substring(0, 12);
        public string Kind { get; init; }
        public string Title { get; init; }
        public int? LinkId { get; init; }
        public JobState State { get; internal set; } = JobState.Running;
        /// <summary>0-100, or -1 when indeterminate.</summary>
        public int Progress { get; internal set; } = -1;
        public string Message { get; internal set; }
        public string Error { get; internal set; }
        public object Result { get; internal set; }
        public DateTime StartedAt { get; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; internal set; }

        internal Exception Exception { get; set; }

        /// <summary>
        /// Completes when the job reaches a terminal state.
        /// </summary>
        internal TaskCompletionSource Completion { get; } =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class JobContext
    {
        private readonly Job job;
        private readonly JobManager manager;
        private DateTime lastPublish = DateTime.MinValue;

        internal JobContext(Job job, JobManager manager)
        {
            this.job = job;
            this.manager = manager;
        }

        public CancellationToken Token => job.Cts.Token;

        public string JobId => job.Id;

        /// <summary>
        /// Report progress; publishing is throttled to 10 Hz except for the final 100%.
        /// </summary>
        public void Report(int percent, string message = null)
        {
            job.Progress = percent;
            if (message != null)
                job.Message = message;

            if (percent >= 100 || DateTime.UtcNow - lastPublish > TimeSpan.FromMilliseconds(100))
            {
                lastPublish = DateTime.UtcNow;
                manager.PublishJob(job);
            }
        }
    }

    public sealed class JobManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(JobManager));
        private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, Job> jobs = new ConcurrentDictionary<string, Job>();
        private readonly EventHub events;

        public JobManager(EventHub events)
        {
            this.events = events;
        }

        public IReadOnlyList<Job> List() => jobs.Values.OrderByDescending(j => j.StartedAt).ToList();

        public Job Get(string id) => id != null && jobs.TryGetValue(id, out var job) ? job : null;

        public bool Cancel(string id)
        {
            var job = Get(id);
            if (job == null || job.State != JobState.Running)
                return false;
            job.Cts.Cancel();
            return true;
        }

        /// <summary>
        /// Wait for a job to finish (used by tests and synchronous API variants).
        /// </summary>
        public async Task<Job> WaitAsync(Job job, TimeSpan timeout)
        {
            await Task.WhenAny(job.Completion.Task, Task.Delay(timeout)).ConfigureAwait(false);
            return job;
        }

        /// <summary>
        /// Run work as a job (visible on the jobs topic with progress) and wait for it. Cancelling
        /// <paramref name="ct"/> (e.g. the HTTP request) cancels the job; failures rethrow the original exception.
        /// </summary>
        public async Task<T> RunAsync<T>(string kind, string title, Func<JobContext, Task<T>> work, int? linkId,
            CancellationToken ct)
        {
            var job = Start(kind, title, async c => await work(c).ConfigureAwait(false), linkId);
            using (ct.Register(() => Cancel(job.Id)))
                await job.Completion.Task.ConfigureAwait(false);

            switch (job.State)
            {
                case JobState.Succeeded:
                    return (T)job.Result;
                case JobState.Cancelled:
                    throw new OperationCanceledException(ct);
                default:
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(job.Exception).Throw();
                    throw job.Exception;
            }
        }

        /// <summary>
        /// Start work in the background and return the tracking job immediately.
        /// </summary>
        public Job Start(string kind, string title, Func<JobContext, Task<object>> work, int? linkId = null)
        {
            Prune();
            var job = new Job { Kind = kind, Title = title, LinkId = linkId };
            jobs[job.Id] = job;
            PublishJob(job);

            var ctx = new JobContext(job, this);
            _ = Task.Run(async () =>
            {
                try
                {
                    job.Result = await work(ctx).ConfigureAwait(false);
                    job.Progress = 100;
                    job.State = JobState.Succeeded;
                }
                catch (OperationCanceledException) when (job.Cts.IsCancellationRequested)
                {
                    job.State = JobState.Cancelled;
                }
                catch (Exception ex)
                {
                    log.Error($"job {kind} failed", ex);
                    job.Exception = ex;
                    job.Error = ex.Message;
                    job.State = JobState.Failed;
                }
                finally
                {
                    job.FinishedAt = DateTime.UtcNow;
                    PublishJob(job);
                    job.Completion.TrySetResult();
                }
            });

            return job;
        }

        internal void PublishJob(Job job) => events.Publish(Topics.Jobs, job);

        private void Prune()
        {
            foreach (var job in jobs.Values)
            {
                if (job.FinishedAt.HasValue && DateTime.UtcNow - job.FinishedAt.Value > Retention)
                    jobs.TryRemove(job.Id, out _);
            }
        }
    }
}
