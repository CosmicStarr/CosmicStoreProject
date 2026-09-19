using System;

namespace Data.Interfaces;

/// <summary>
/// One work unit over the CJ staging database. Repositories share a single <c>ApplicationDbContext</c>.
/// </summary>
public interface IUnitOfWork:IDisposable
{
    /// <summary>Returns (and caches) a staging repository for entity <typeparamref name="T"/>.</summary>
    IRepository<T> Repository<T>() where T : class;

    /// <summary>Saves all staged Add/Update/Remove changes to dbo.</summary>
    Task<int> Complete();
}
