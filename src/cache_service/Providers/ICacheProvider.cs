using Service.Configuration;

namespace Service.Providers
{
    public interface ICacheProvider
    {
        ICache CreateCache(CacheOptions options);
        ICache CreateCache();
    }
}