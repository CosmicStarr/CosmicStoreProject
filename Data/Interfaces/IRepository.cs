using System.Linq.Expressions;
using Data.Util;

namespace Data.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T> Get(int Id);
    Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T,bool>>? filter = null, Func<IQueryable<T>,IOrderedQueryable<T>>? orderby = null, string? includeProperties = null);
    Task<IEnumerable<T>> GetAll(Expression<Func<T,bool>>? filter = null,
    Func<IQueryable<T>,IOrderedQueryable<T>>? orderby = null,
    string? includeProperties = null);
    Task<T> GetFirstOrDefault(Expression<Func<T,bool>>? filter = null,
    string? includeProperties = null);
    void Add(T entity);
    void Remove(T entity);
    void Remove(string Id);
    void RemoveRange(IEnumerable<T> entities);
    void Update(T entity);
}
