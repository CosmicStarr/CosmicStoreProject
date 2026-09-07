using System.Linq.Expressions;
using Data.Util;

namespace Data.Interfaces;

public interface IStoreRepo<T> where T : class
{
    Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T,bool>>? filter = null, Func<IQueryable<T>,IOrderedQueryable<T>>? orderby = null, string? includeProperties = null);
    Task<IEnumerable<T>> GetFromSqlAsync(string sql, params object[] parameters);
    Task<T> GetFirstOrDefault(Expression<Func<T,bool>>? filter = null,
    string? includeProperties = null);
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Remove(T entity);
    void Remove(string Id);
    void RemoveRange(IEnumerable<T> entities);
    void Update(T entity);
}