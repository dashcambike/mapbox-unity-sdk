using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.Cache.SQLiteCache;
using Mapbox.BaseModule.Unity;
using UnityEngine;

namespace Mapbox.Example.Scripts.ModuleBehaviours
{
    public class RuntimeCacheManagerBehaviour : MapboxCacheManagerBehaviour
    {
        public MapboxCacheManager CacheManager;
        public MemoryCache MemoryCache;

        public MapboxCacheManager GetCacheManager() => CacheManager;

        public bool CreateSqliteCache = true;
        public bool CreateFileCache = true;
        
        public override MapboxCacheManager GetCacheManager(UnityContext unityContext, DataFetchingManager dataFetchingManager)
        {
            if (CacheManager == null)
            {
                SqliteCache sqliteCache = null;
                FileCache fileCache = null;
                sqliteCache = CreateSqliteCache ? new SqliteCache(unityContext.TaskManager, 1000) : null;
                fileCache = CreateFileCache ? new FileCache(unityContext.TaskManager) : null;
                MemoryCache = new MemoryCache();
                
                CacheManager = new MapboxCacheManager(
                    unityContext,
                    MemoryCache,
                    fileCache,
                    sqliteCache);
            }

            return CacheManager;
        }
    }

    public abstract class MapboxCacheManagerBehaviour : MonoBehaviour
    {
        public abstract MapboxCacheManager GetCacheManager(UnityContext unityContext, DataFetchingManager dataFetchingManager);
    }
}