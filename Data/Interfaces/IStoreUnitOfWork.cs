using System;

namespace Data.Interfaces;

public interface IStoreUnitOfWork:IDisposable
{
    IStoreRepo<T> Repository<T>() where T : class;
    Task<int> Complete();
}
