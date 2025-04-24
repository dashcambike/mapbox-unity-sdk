using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.UnityMapService.DataSources
{
    public abstract class ImageSource<T> : UnitySource<T> where T : RasterData, new()
    {
        protected Dictionary<CanonicalTileId, RasterTile> _waitingList;
        protected TypeMemoryCache<T> _memoryCache;
        private HashSet<CanonicalTileId> _activeRequestsToCancel;
        private ImageSourceSettings _settings;

        protected ImageSource(DataFetchingManager dataFetchingManager, MapboxCacheManager cacheManager, ImageSourceSettings settings) : base(dataFetchingManager, cacheManager, settings.TilesetId)
        {
            _settings = settings;
            _waitingList = new Dictionary<CanonicalTileId, RasterTile>();
            _activeRequestsToCancel = new HashSet<CanonicalTileId>();

            _memoryCache = RegisterTypeToMemoryCache<T>(this.GetHashCode(), _settings.CacheSize);
            _memoryCache.CacheItemDisposed += (t) =>
            {
                CacheItemDisposed(t);
            };
        }

        public override void LoadTile(CanonicalTileId requestedDataTileId)
        {
            LoadTileCore(requestedDataTileId);
        }
        
        public override bool CheckInstantData(CanonicalTileId tileId)
        {
            return _memoryCache.Exists(tileId);
        }
        
        public override bool GetInstantData(CanonicalTileId tileId, out T data)
        {
            return _memoryCache.Get(tileId, out data);
        }

        public override bool RetainTiles(HashSet<CanonicalTileId> retainedTiles)
        {
            foreach (var id in retainedTiles)
            {
                if (!IsZinSupportedRange(id.Z)) continue;
                
                if (!CheckInstantData(id))
                {
                    LoadTile(id);
                }
            }
            
            _activeRequestsToCancel.Clear();
            foreach (var activeTile in _waitingList)
            {
                if (!retainedTiles.Contains(activeTile.Key) && (activeTile.Value != null && !activeTile.Value.IsBackgroundData))
                {
                    _activeRequestsToCancel.Add(activeTile.Key);
                }
            }
            
            foreach (var id in _activeRequestsToCancel)
            {
                CancelActiveRequests(id);
            }

            _memoryCache.RetainTiles(retainedTiles);

            return true;
        }
        
        public override void CancelActiveRequests(CanonicalTileId unityTileId)
        {
            if (_waitingList.ContainsKey(unityTileId))
            {
                var tile = _waitingList[unityTileId];
                if (tile != null)
                {
                    CancelFetching(tile, _tilesetId);
                }

                _waitingList.Remove(unityTileId);
            }
        }
        
        public override void DownloadAndCacheBaseTiles()
        {
            var backgroundTiles = new HashSet<CanonicalTileId>();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    backgroundTiles.Add(new CanonicalTileId(2, i, j));
                }
            }

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    backgroundTiles.Add(new CanonicalTileId(1, i, j));
                }
            }

            backgroundTiles.Add(new CanonicalTileId(0, 0, 0));

            foreach (var tileId in backgroundTiles)
            {
                BackgroundLoad(tileId, _tilesetId);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var tile in _waitingList)
            {
                tile.Value?.Cancel();
            }
            foreach (var rasterData in _memoryCache.GetAllDatas())
            {
                GameObject.Destroy(rasterData.Texture);
            }
        }
        
        


        //COROUTINE METHODS only used in initialization so far
        #region coroutines
        public override IEnumerator LoadTileCoroutine(CanonicalTileId requestedDataTileId, Action<T> callback = null)
        {
            T resultData = null;
            if (GetInstantData(requestedDataTileId, out resultData))
            {
                
            }
            else if (_waitingList.ContainsKey(requestedDataTileId))
            {
                while(_waitingList.ContainsKey(requestedDataTileId))
                {
                    yield return null;
                }
                _memoryCache.Get(requestedDataTileId, out resultData);
            }
            else
            {
                var isDone = false;
                LoadTileCore(requestedDataTileId, (result) =>
                {
                    isDone = true;
                    resultData = result;
                });
                while (!isDone)
                {
                    yield return null;
                }
            }

            
            callback?.Invoke(resultData);
        }
        
        public override IEnumerator LoadTilesCoroutine(IEnumerable<CanonicalTileId> retainedTiles, Action<List<T>> callback = null)
        {
            if(callback != null)
            {
                var results = new List<T>();
                var coroutines = retainedTiles.Select(x => LoadTileCoroutine(x, (data) => results.Add(data)));
                yield return coroutines.WaitForAll();
                callback?.Invoke(results);
            }
            else
            {
                var coroutines = retainedTiles.Select(x => LoadTileCoroutine(x));
                yield return coroutines.WaitForAll();
            }
        }
        #endregion
        
        
        
        
        protected abstract RasterTile CreateTile(CanonicalTileId tileId, string tilesetId);
        protected abstract T CreateRasterDataWrapper(RasterTile tile);
        
        private void LoadTileCore(CanonicalTileId requestedDataTileId, Action<T> callback = null)
        {
            if (_waitingList.ContainsKey(requestedDataTileId))
            {
                callback?.Invoke(null);
                return;
            }
            _waitingList[requestedDataTileId] = null;

            GetImageAsync<T>(requestedDataTileId, _tilesetId, true, (cacheItem) =>
            {
                if (cacheItem != null)
                {
                    TextureReceivedFromFile(cacheItem);
                    CheckExpiration(cacheItem);
                    if (_waitingList.ContainsKey(requestedDataTileId))
                        _waitingList.Remove(requestedDataTileId);
                    callback?.Invoke(cacheItem);
                }
                else
                {
                    _waitingList.Remove(requestedDataTileId);
                    
                    var dataTile = CreateTile(requestedDataTileId, _tilesetId);
                    _waitingList[requestedDataTileId] = dataTile;
                    WebRequestData(dataTile, (fetchingResult) =>
                    {
                        T resultDataItem = null;
                        if (dataTile.CurrentTileState == TileState.Loaded)
                        {
                            resultDataItem = TextureReceivedFromWeb(dataTile);
                        }
                        else
                        {
                            //?
                        }
                        if (_waitingList.ContainsKey(requestedDataTileId))
                            _waitingList.Remove(requestedDataTileId);
                        callback?.Invoke(resultDataItem);
                    });
                }
            });
        }
        
        protected virtual void TextureReceivedFromFile(T textureCacheItem)
        {
            //var tile = (RasterTile) textureCacheItem.Tile;
            //textureCacheItem.Tile = tile;
            //tile.SetTextureFromCache(textureCacheItem.Texture2D);
            //tile.FromCache = CacheType.FileCache;
            textureCacheItem.CacheType = CacheType.FileCache;

            //IMPORTANT file is read from file cache and it's not automatically
            //moved to memory cache. we have to do it here.
            _memoryCache.Add(textureCacheItem);
        }

        protected virtual T TextureReceivedFromWeb(RasterTile tile)
        {
            tile.AddLog(string.Format("{0} - {1}", Time.unscaledTime, " TextureReceivedHandler"));
            if (tile.Texture2D != null)
            {
                tile.AddLog("updated and old texture is destroyed");
                GameObject.Destroy(tile.Texture2D);
            }

            if (tile.CurrentTileState == TileState.Loaded && tile.Data != null)
            {
                //IMPORTANT This is where we create a Texture2D
                tile.AddLog("extracting texture ", tile.Id);
                tile.ExtractTextureFromRequest();

                var newTextureCacheItem = CreateRasterDataWrapper(tile);

                _memoryCache.Add(newTextureCacheItem);
                SaveImage(newTextureCacheItem, true);

                return newTextureCacheItem;
            }

            return null;
        }
        
        
        protected void BackgroundLoad(CanonicalTileId tileId, string tilesetId)
        {
            GetImageAsync<T>(tileId, tilesetId, SystemInfo.supportsAsyncGPUReadback, (cacheItem) =>
            {
                if (cacheItem != null)
                {
                    TextureReceivedFromFile(cacheItem);
                    _memoryCache.MarkFallback(cacheItem.TileId);
                    CheckExpiration(cacheItem);
                }
                else
                {
                    var dataTile = CreateTile(tileId, tilesetId);
                    dataTile.IsBackgroundData = true;
                    WebRequestData(dataTile, (fetchingResult) =>
                    {
                        if (dataTile.CurrentTileState != TileState.Canceled)
                        {
                            TextureReceivedFromWeb(dataTile);
                            _memoryCache.MarkFallback(dataTile.Id);
                        }
                    });
                }
            });
        }

        private void CheckExpiration(T cacheItem)
        {
            var dataTask = ReadEtagExpiration(cacheItem, 4);
            dataTask.DataCompleted += OnDataTaskDataContinueWith;
        }

        private void OnDataTaskDataContinueWith(Task task, T data)
        {
            if (data.ExpirationDate == null || DateTime.Compare((DateTime)data.ExpirationDate, DateTime.Now) < 0)
            {
                var dataTile = CreateTile(data.TileId, data.TilesetId);
                dataTile.ETag = data.ETag;
                WebRequestData(dataTile, (tile) =>
                {
                    if (dataTile.CurrentTileState != TileState.Canceled)
                    {
                        if (dataTile.StatusCode == 200)
                        {
                            //Debug.Log("expired and returned 200");
                            TextureReceivedFromWeb(dataTile);
                        }
                        else if (dataTile.StatusCode == 304)
                        {
                            //not changed, just update meta?
                            //Debug.Log("expired but not changed, just update meta?");
                            UpdateExpiration(dataTile.Id, dataTile.TilesetId, dataTile.ExpirationDate);
                        }
                    }
                });
                //Debug.Log("tile needs an update");
            }
            else
            {
                //Debug.Log("doesnt needs an update");
            }
        }
    }
    
}