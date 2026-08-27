using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Solace.DB.Utils;

public static class IQueryableExtensions
{
    extension<T>(IQueryable<T> queryable)
        where T : class, new()
    {
        public async Task<T> FirstOrNewAsync(Expression<Func<T, bool>> predicate, bool trackNew = true, CancellationToken cancellationToken = default)
        {
            var value = await queryable.FirstOrDefaultAsync(predicate, cancellationToken);
            if (value is null)
            {
                value = new T();

                if (trackNew && queryable.Provider is IInfrastructure<IServiceProvider> infra)
                {
                    var context = infra.Instance.GetService<ICurrentDbContext>()?.Context;

                    context?.Add(value);
                }
            }

            return value;
        }
    }
}