using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("unittest")]
namespace Service
{
    public interface ICache
    {
        Task<long> IncrementAsync(string key, CancellationToken cancellationToken = default);
        Task<long> IncrementByAsync(string key, long number, CancellationToken cancellationToken = default);
        Task<long> DecrementAsync(string key, CancellationToken cancellationToken = default);
        Task<long> DecrementByAsync(string key, long number, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
        Task<bool> SetAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default);
        Task<bool> SetIfNotExistsAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default);
        Task<string> GetAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> ExpireKey(string key, int expireInSeconds, CancellationToken cancellationToken = default);
        Task<long> AddToListAsync(string key, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default);
        Task<List<string>> GetListAsync(string key, CancellationToken cancellationToken = default);
        Task<long> RemoveFromListAsync(string key, string value, CancellationToken cancellationToken = default);
        Task<long> LengthOfListAsync(string key, CancellationToken cancellationToken = default);
        Task<List<string>> GetSetAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> AddToSetAsync(string key, string value, int expireInSeconds, CancellationToken cancellationToken = default);
        Task<bool> RemoveFromSetAsync(string key, string value, CancellationToken cancellationToken = default);
        Task<long> LengthOfSetAsync(string key, CancellationToken cancellationToken = default);
        Task<string> GetHashAsync(string key, string field, CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> GetHashAllAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> AddToHashAsync(string key, string field, string value, int expireInSeconds, bool create_or_overwrite = true, CancellationToken cancellationToken = default);
        Task<bool> RemoveFromHashAsync(string key, string fieldKey, CancellationToken cancellationToken = default);
        Task<long> LengthOfHashAsync(string key, CancellationToken cancellationToken = default);
        

    }
}