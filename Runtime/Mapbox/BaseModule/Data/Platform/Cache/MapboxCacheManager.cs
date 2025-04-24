using System;
using System.IO;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache.SQLiteCache;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Unity;
using UnityEditor;
using UnityEngine;

namespace Mapbox.BaseModule.Data.Platform.Cache
{
    public interface IMapboxCacheManager
    {
        void SaveBlob(MapboxTileData vectorCacheItem, bool forceInsert);
        void SaveImage(RasterData textureCacheItem, bool forceInsert);
        void GetImageAsync<T>(CanonicalTileId tileId, string tilesetId, bool isTextureNonreadable, Action<T> callback) where T : RasterData, new();
        DataTaskWrapper<T> CreateGetTileInfoTask<T>(CanonicalTileId tileId, string tilesetid , int priority = 1) where T : MapboxTileData, new();
        DataTaskWrapper<T> CreateReadEtagExpirationTask<T>(T data, int priority = 1) where T : MapboxTileData, new();
        void UpdateExpiration(CanonicalTileId tileId, string tilesetId, DateTime date);
        
        void AddTask(TaskWrapper task);
    }

    public class MapboxCacheManager : IMapboxCacheManager
    {
        private IMemoryCache _memoryCache;
        private IFileCache _textureFileCache;
        private ISqliteCache _sqLiteCache;
        private TaskManager _taskManager;

        public MapboxCacheManager(UnityContext unityContext, MemoryCache memoryCache, FileCache fileCache = null, ISqliteCache cache = null)
        {
            _taskManager = unityContext.TaskManager;
            _memoryCache = memoryCache;
            _textureFileCache = fileCache;
            _sqLiteCache = cache;

            if (_textureFileCache != null)
            {
                if (_textureFileCache.TestAvailability() == false)
                    _textureFileCache = null;
            }

            if (_sqLiteCache != null && _textureFileCache != null)
            {
                _sqLiteCache.DataPrunedForFile += path => _textureFileCache.DeleteByFileRelativePath(path);
            }
            
            if (_sqLiteCache != null)
            {
                if (!_sqLiteCache.IsUpToDate())
                {
                    Debug.Log("renewing sqlite cache file");
                    var sqliteDeleteSuccess = _sqLiteCache.ClearDatabase();
                    if (sqliteDeleteSuccess && _textureFileCache != null)
                    {
                        _textureFileCache.ClearAll();
                    }
                    _sqLiteCache.ReadySqliteDatabase();
                }

                CheckSqlAndFileIntegrity();
            }
        }

        public void SaveBlob(MapboxTileData vectorCacheItem, bool forceInsert)
        {
            _sqLiteCache?.Add(vectorCacheItem, forceInsert);
        }

        public void SaveImage(RasterData textureCacheItem, bool forceInsert)
        {
            _textureFileCache?.Add(textureCacheItem, forceInsert, (path) =>
            {
                _sqLiteCache?.SyncAdd(textureCacheItem.TilesetId, textureCacheItem.TileId, null, path, textureCacheItem.ETag, textureCacheItem.ExpirationDate, true);
            });
        }

        public void GetImageAsync<T>(CanonicalTileId tileId, string tilesetId, bool isTextureNonreadable, Action<T> callback) where T : RasterData, new()
        {
            if (_textureFileCache != null)
            {
                var fileExists = _textureFileCache.GetAsync<T>(
                    tileId,
                    tilesetId,
                    isTextureNonreadable,
                    (textureCacheItem) =>
                    {
                        if (textureCacheItem.HasError)
                        {
                            callback(null);
                        }
                        else
                        {

                            callback(textureCacheItem);
                        }
                    });
                
                if (!fileExists)
                {
                    callback(null);
                }
            }
            else
            {
                callback(null);    
            }
        }

        public DataTaskWrapper<T> CreateGetTileInfoTask<T>(CanonicalTileId tileId, string tilesetid, int priority = 1) where T : MapboxTileData, new()
        {
            if (_sqLiteCache != null)
            {
                var task = new DataTaskWrapper<T>();

                task.TileId = tileId;
                task.DataAction = () => { return _sqLiteCache.Get<T>(tilesetid, tileId); };
                return task;
            }
            else
            {
                return null;
            }
        }

        public DataTaskWrapper<T> CreateReadEtagExpirationTask<T>(T data, int priority = 4) where T : MapboxTileData, new()
        {
            var task = new DataTaskWrapper<T>();
            
            task.TileId = data.TileId;
            task.DataAction = () => { return _sqLiteCache.Get<T>(data.TilesetId, data.TileId, data); };
            return task;
        }

        public void AddTask(TaskWrapper taskWrapper)
        {
            _taskManager.AddTask(taskWrapper);
        }

        public void UpdateExpiration(CanonicalTileId tileId, string tilesetId, DateTime date)
        {
            _sqLiteCache.UpdateExpiration(tilesetId, tileId, date);
        }
        
        public void RemoveData(string tilesetId, int zoom, int x, int y)
        {
            _sqLiteCache.RemoveData(tilesetId, zoom, x, y);
        }

        public TypeMemoryCache<T> RegisterMemoryCache<T>(int owner, int cacheSize = 100) where T : MapboxTileData
        {
            return _memoryCache.RegisterType<T>(owner, cacheSize);
        }

        /// <summary>
        /// We check for files that exists but not tracked in sqlite file and delete them all
        /// If we don't do that, those files will pile up (assuming systems loses track due to a bug somehow) and fill all the disk
        /// Vice versa (file doesn't exists, sqlite entry does) isn't important as entry will be cycled out soon anyway
        /// </summary>
        private void CheckSqlAndFileIntegrity(bool firstRun = true)
        {
            if (_sqLiteCache == null || _textureFileCache == null) return;
            
            var sqlTileList = _sqLiteCache.GetAllTiles();
            var fileList = _textureFileCache.GetFileList();

            // Debug.Log("sqlite " + string.Join(Environment.NewLine, sqlTileList.Select(x => x.tile_path)));
            // Debug.Log("file " + string.Join(Environment.NewLine, fileList));
            
            foreach (var tile in sqlTileList)
            {
                if (fileList.Contains(tile.tile_path))
                {
                    fileList.Remove(tile.tile_path);
                }
            }
            
            if (fileList.Count > 0)
            {
                Debug.Log(string.Format("{0} files will be deleted to sync sqlite and file cache", fileList.Count));
                foreach (var fileRelativePath in fileList)
                {
                    _textureFileCache.DeleteByFileRelativePath(fileRelativePath);
                }

                if (firstRun)
                {
                    CheckSqlAndFileIntegrity(false);
                }
            }
            else
            { 
                //Debug.Log("Sqlite and File Caches are in sync");
            }
        }
        
        public void OnDestroy()
        {
            //close sqlite&file caches here?
            _memoryCache.OnDestroy();
        }

        public void CancelFetching(CanonicalTileId tileId, string tilesetId)
        {
            // var key = tileId.GenerateKey(tilesetId, "GetTileInfoAsync");
            // _taskManager.CancelTask(key);
            // key = tileId.GenerateKey(tilesetId, "ReadEtagExpiration");
            // _taskManager.CancelTask(key);
        }
        
        public static void DeleteAllCache()
        {
            var sqliteDeleted = SqliteCache.DeleteSqliteFolder();
            var fileCacheDeleted = FileCache.ClearAllFiles();
            if (sqliteDeleted && fileCacheDeleted)
            {
                Debug.Log("Mapbox cache cleared");
            }
        }
    }

    public class LoggingCacheManager : MapboxCacheManager
    {
        public LoggingCacheManager(UnityContext unityContext, MemoryCache memoryCache, FileCache fileCache = null, SQLiteCache.SqliteCache cache = null) : base(unityContext, memoryCache, fileCache, cache)
        {
        }
    }
}