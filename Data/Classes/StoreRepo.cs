using System;
using System.Linq.Expressions;
using Data.Interfaces;
using Data.Util;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;

namespace Data.Classes;

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

/// <summary>
/// EF Core repository for the storefront context (<see cref="ApplicationDbStoreContext"/>).
/// </summary>
public class StoreRepo<T>(ApplicationDbStoreContext context) : IStoreRepo<T> where T : class
{
    private readonly ApplicationDbStoreContext _context = context;
    internal DbSet<T> dbSet = context.Set<T>();

    /// <summary>Stages a new storefront row (product, order, variant, etc.).</summary>
    public void Add(T entity)
    {
        dbSet.Add(entity);
    }

    /// <summary>Stages many new storefront rows in one call.</summary>
    public void AddRange(IEnumerable<T> entities)
    {
        dbSet.AddRange(entities);
    }
    
    /// <summary>
    /// Executes a stored procedure or raw SQL and maps rows to <typeparamref name="T"/> without tracking.
    /// </summary>
    public async Task<IEnumerable<T>> GetFromSqlAsync(string sql, params object[] parameters)
    {
        // AsNoTracking is recommended for read-only stored procedure results
        return await dbSet.FromSqlRaw(sql, parameters).AsNoTracking().ToListAsync();
    }

    /// <summary>Paged storefront query. Ignores auto-includes so lists stay cheap unless Include is requested.</summary>
    public async Task<PagerList<T>> GetAllParams(PageParams? pageParams = null, Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderby = null, string? includeProperties = null)
    {
        pageParams ??= new PageParams();
        IQueryable<T> query = dbSet.AsNoTracking().IgnoreAutoIncludes();
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

    /// <summary>First matching storefront row, or null.</summary>
    public async Task<T> GetFirstOrDefault(Expression<Func<T, bool>>? filter = null, string? includeProperties = null)
    {
        IQueryable<T> query = dbSet.AsNoTracking().IgnoreAutoIncludes();
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

    /// <summary>Stages deletion of one entity instance.</summary>
    public void Remove(T entity)
    {
        dbSet.Remove(entity);
    }

    /// <summary>Looks up a string-key row and stages its deletion if it exists.</summary>
    public void Remove(string Id)
    {
        var info = dbSet.Find(Id);
        if(info != null)
        {
            dbSet.Remove(info);
        }
    }

    /// <summary>Stages deletion of many rows.</summary>
    public void RemoveRange(IEnumerable<T> entities)
    {
        dbSet.RemoveRange(entities);
    }

    /// <summary>Marks a storefront row as modified. If another copy of the same key is already tracked, copies values onto that instance instead of attaching a duplicate.</summary>
    public void Update(T entity)
    {
        var tracked = FindTracked(entity);
        if (tracked is null)
        {
            dbSet.Update(entity);
            return;
        }

        if (!ReferenceEquals(tracked, entity))
        {
            _context.Entry(tracked).CurrentValues.SetValues(entity);
        }
    }

    /// <summary>Returns the change-tracker instance for this entity's primary key, if one is already attached.</summary>
    private T? FindTracked(T entity)
    {
        var key = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey();
        if (key is null || key.Properties.Count == 0)
        {
            return null;
        }

        var keyValues = key.Properties.Select(property => GetPropertyValue(entity, property)).ToArray();

        foreach (var candidate in dbSet.Local)
        {
            var sameKey = true;
            for (var index = 0; index < key.Properties.Count; index++)
            {
                if (!Equals(GetPropertyValue(candidate, key.Properties[index]), keyValues[index]))
                {
                    sameKey = false;
                    break;
                }
            }

            if (sameKey)
            {
                return candidate;
            }
        }

        return null;
    }

    private static object? GetPropertyValue(object instance, Microsoft.EntityFrameworkCore.Metadata.IProperty property)
    {
        return property.PropertyInfo?.GetValue(instance) ?? property.FieldInfo?.GetValue(instance);
    }
}
