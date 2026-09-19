using System;

namespace Data.Interfaces;

/// <summary>
/// One work unit over the storefront database. Repositories share a single <c>ApplicationDbStoreContext</c>.
/// </summary>
public interface IStoreUnitOfWork:IDisposable
{
    /// <summary>Returns (and caches) a store repository for entity <typeparamref name="T"/>.</summary>
    IStoreRepo<T> Repository<T>() where T : class;

    /// <summary>Saves all staged Add/Update/Remove changes to the store schema.</summary>
    Task<int> Complete();
}
