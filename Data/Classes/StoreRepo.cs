using System;
using System.Linq.Expressions;
using Data.Interfaces;
using Data.Util;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace Data.Classes;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

public class StoreRepo<T>(ApplicationDbStoreContext context) : IStoreRepo<T> where T : class
{
    private readonly ApplicationDbStoreContext _context = context;
    internal DbSet<T> dbSet = context.Set<T>();

    public void Add(T entity)
    {
        dbSet.Add(entity);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        dbSet.AddRange(entities);
    }
    
    // NEW METHOD: Execute raw SQL and return a list
    public async Task<IEnumerable<T>> GetFromSqlAsync(string sql, params object[] parameters)
    {
        // AsNoTracking is recommended for read-only stored procedure results
        return await dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync();
    }

    public async Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderby = null, string? includeProperties = null)
    {
        pageParams ??= new PageParams();
        IQueryable<T> query = dbSet.AsNoTracking();
        if(filter != null)
        {
            query = query.Where(filter);
        }
        if(includeProperties != null)
        {
            foreach(var ItemData in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(ItemData);
            }
        }
        if(orderby != null)
        {
            return await PagerList<T>.CreateAsync(orderby(query),pageParams.PageNumber,pageParams.PageSize);
        }
        return await PagerList<T>.CreateAsync(query,pageParams.PageNumber,pageParams.PageSize);
    }

    public async Task<T> GetFirstOrDefault(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
    {
        IQueryable<T> query = dbSet.AsNoTracking();
        if(filter != null)
        {
            query = query.Where(filter);
        }
        if(includeProperties != null)
        {
            foreach(var ItemData in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                query = query.Include(ItemData);
            }
        }
#pragma warning disable CS8603 // Possible null reference return.
        return await query.FirstOrDefaultAsync();
#pragma warning restore CS8603 // Possible null reference return.
    }

    public void Remove(T entity)
    {
        dbSet.Remove(entity);
    }

    public void Remove(string Id)
    {
        var info = dbSet.Find(Id);
        if(info != null)
        {
            dbSet.Remove(info);
        }
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        dbSet.RemoveRange(entities);
    }

    public void Update(T entity)
    {
        dbSet.Update(entity);
    }
}
