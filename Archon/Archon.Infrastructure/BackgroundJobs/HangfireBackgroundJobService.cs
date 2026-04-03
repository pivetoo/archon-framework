using System.Linq.Expressions;
using Archon.Application.Services;
using Hangfire;

namespace Archon.Infrastructure.BackgroundJobs
{
    public sealed class HangfireBackgroundJobService : IBackgroundJobService
    {
        public string Enqueue<T>(Expression<Action<T>> methodCall)
        {
            return BackgroundJob.Enqueue(methodCall);
        }

        public void AddOrUpdateRecurringJob<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression)
        {
            RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression);
        }

        public void RemoveRecurringJob(string jobId)
        {
            RecurringJob.RemoveIfExists(jobId);
        }
    }
}
