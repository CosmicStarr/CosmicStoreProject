using System;

namespace Data.Interfaces;

public interface IUnitOfWork:IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> Complete();
}
