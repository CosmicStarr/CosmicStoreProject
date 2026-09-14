using System;
using System.Collections;
using Data.Interfaces;

namespace Data.Classes;

/// <summary>
/// Unit of work for the storefront database. Caches <see cref="StoreRepo{T}"/> instances per entity type.
/// </summary>
public class StoreUnitOfWork(ApplicationDbStoreContext context) : IStoreUnitOfWork
{
    private Hashtable? _repo;
    private readonly ApplicationDbStoreContext _context = context;

    /// <summary>Persists staged store-schema changes.</summary>
    public async Task<int> Complete()
    {
        return await _context.SaveChangesAsync();
    }

    /// <summary>Disposes the storefront DbContext.</summary>
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
    {
        _context.Dispose();
    }

    /// <summary>
    /// Returns a cached <see cref="StoreRepo{T}"/> for this entity type, creating it on first use.
    /// </summary>
    public IStoreRepo<T> Repository<T>() where T : class
    {
        _repo ??= [];
        var type = typeof(T).Name;
        if (!_repo.ContainsKey(type))
        {
            var repoType = typeof(StoreRepo<>);
            var repoInstance = Activator.CreateInstance(repoType.MakeGenericType(typeof(T)),_context);
            _repo.Add(type,repoInstance);
        }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
        return (IStoreRepo<T>)_repo[type];
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
    }
}
