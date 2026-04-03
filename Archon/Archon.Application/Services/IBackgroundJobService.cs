using System.Linq.Expressions;

namespace Archon.Application.Services
{
    public interface IBackgroundJobService
    {
        string Enqueue<T>(Expression<Action<T>> methodCall);

        void AddOrUpdateRecurringJob<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression);

        void RemoveRecurringJob(string jobId);
    }
}
