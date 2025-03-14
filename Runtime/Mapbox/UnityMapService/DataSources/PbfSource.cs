using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.UnityMapService.DataSources
{
    public abstract class PbfSource<T> : UnitySource<T> where T : MapboxTileData, new()
    {
        protected Dictionary<CanonicalTileId, Tile> _waitingList;
        private TypeMemoryCache<T> _memoryCache;
        private HashSet<CanonicalTileId> _activeRequestsToCancel;

        protected PbfSource(DataFetchingManager dataFetchingManager, MapboxCacheManager cacheManager, VectorSourceSettings settings) : base(dataFetchingManager, cacheManager, settings.TilesetId)
        {
            _waitingList = new Dictionary<CanonicalTileId, Tile>();
            _activeRequestsToCancel = new HashSet<CanonicalTileId>();
            
            _memoryCache = RegisterTypeToMemoryCache<T>(this.GetHashCode(), settings.CacheSize);
            _memoryCache.CacheItemDisposed += (t) =>
            {
                CacheItemDisposed(t);
            };
        }
        
        public override void LoadTile(CanonicalTileId requestedDataTileId) => LoadTileCore(requestedDataTileId);
        
        public override bool CheckInstantData(CanonicalTileId tileId)
        {
            return _memoryCache.Exists(tileId);
        }
        
        public override bool GetInstantData(CanonicalTileId tileId, out T data)
        {
            return _memoryCache.Get(tileId, out data);
        }

        public override void InvalidateData(CanonicalTileId tileId)
        {
            _memoryCache.Remove(tileId);
            RemoveData(_tilesetId, tileId.Z, tileId.X, tileId.Y);
        }

        public override bool RetainTiles(HashSet<CanonicalTileId> retainedTiles)
        {
            foreach (var id in retainedTiles)
            {
                if (CheckInstantData(id) || !IsZinSupportedRange(id.Z))
                {
                    continue;
                }
                
                LoadTileCore(id);
            }

            if (_waitingList.Count > 0)
            {
                _activeRequestsToCancel.Clear();
                foreach (var activeTile in _waitingList)
                {
                    if (!retainedTiles.Contains(activeTile.Key))
                    {
                        _activeRequestsToCancel.Add(activeTile.Key);
                    }
                }

                foreach (var id in _activeRequestsToCancel)
                {
                    CancelActiveRequests(id);
                }
            }

            _memoryCache.RetainTiles(retainedTiles);
            return true;
        }
        
        public override void CancelActiveRequests(CanonicalTileId unityTileId)
        {
            if (_waitingList.ContainsKey(unityTileId))
            {
                var tile = _waitingList[unityTileId];
                CancelFetching(tile, _tilesetId);
                _waitingList.Remove(unityTileId);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var tile in _waitingList)
            {
                tile.Value.Cancel();
            }
        }

        
        
        
        //COROUTINE METHODS only used in initialization so far
        #region coroutines
        public override IEnumerator LoadTileCoroutine(CanonicalTileId requestedDataTileId, Action<T> callback = null)
        {
            var isDone = false;
            T resultData = null;
            LoadTileCore(requestedDataTileId, (result) =>
            {
                isDone = true;
                resultData = result;
            });

            while (!isDone)
            {
                yield return null;
            }

            callback?.Invoke(resultData);
        }

        public override IEnumerator LoadTilesCoroutine(IEnumerable<CanonicalTileId> retainedTiles, Action<List<T>> callback = null)
        {
            if (callback != null)
            {
                var results = new List<T>();
                var coroutines = retainedTiles.Select(x =>
                    LoadTileCoroutine(x,
                        (data) =>
                        {
                            if (data != null) results.Add(data);
                        }));
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
        
        
        private void LoadTileCore(CanonicalTileId requestedDataTileId, Action<T> callback = null)
        {
            if (_waitingList.ContainsKey(requestedDataTileId))
            {
                callback?.Invoke(null);
                return;
            }
            _waitingList.Add(requestedDataTileId, null);
            
            var dataTile = CreateTile(requestedDataTileId, _tilesetId);
            _waitingList[requestedDataTileId] = dataTile;
            GetTileInfoAsync<T>(requestedDataTileId, _tilesetId, (cacheItem) =>
            {
                if (dataTile.CurrentTileState == TileState.Canceled) return;
                if (cacheItem != null)
                {
                    VectorReceivedFromSqlite(cacheItem);
                    // TextureReceivedFromFile(cacheItem);
                    CheckExpiration(cacheItem);
                    if (_waitingList.ContainsKey(requestedDataTileId))
                        _waitingList.Remove(requestedDataTileId);
                    callback?.Invoke(cacheItem);
                }
                else
                {
                    WebRequestData(dataTile, (fetchingResult) =>
                    {
                        var resultDataItem = VectorReceivedFromWeb(dataTile);
                        if (_waitingList.ContainsKey(requestedDataTileId))
                            _waitingList.Remove(requestedDataTileId);
                        callback?.Invoke(resultDataItem);
                    });
                }
            }, 0);
        }

        private void VectorReceivedFromSqlite(T vectorCacheItemFromSqlite)
        {
            //TODO ADD expiration date check if tile is from sqlite
            //TODO REREQUEST if data has expired if tile is from sqlite
            //TODO ADD to memory cache
            // var tile = (Map.VectorTile) vectorCacheItemFromSqlite.Tile;
            // if (tile.CurrentTileState == TileState.Canceled)
            //     return;
            
            //CheckAndRequestExpiredTile(vectorCacheItemFromSqlite, tile);

            // tile.AddLog(string.Format("{0} - {1}", Time.unscaledTime, " VectorReceivedFromSqlite"));
            // var cacheItem = CreateCacheItem(tile);
            
            if (_waitingList.ContainsKey(vectorCacheItemFromSqlite.TileId))
            {
                _waitingList.Remove(vectorCacheItemFromSqlite.TileId);
            }
            else
            {
                //this can mean bunch of things (like cancellation)
                //but one case is; data we read from filecache was expired
                //since we received it first time, it's not in waiting list anymore
                //then server call for updating the data hits here and it wasn't expected at all
                Debug.Log("tile fetched but it isn't expected anymore?");
            }

            _memoryCache.Add(vectorCacheItemFromSqlite);
        }

        private T VectorReceivedFromWeb(ByteArrayTile tile)
        {
            tile.AddLog(string.Format("{0} - {1}", Time.unscaledTime, " VectorReceivedHandler"));
            
            if (_waitingList.ContainsKey(tile.Id))
            {
                _waitingList.Remove(tile.Id);
            }
            else
            {
                //this can mean bunch of things (like cancellation)
                //but one case is; data we read from filecache was expired
                //since we received it first time, it's not in waiting list anymore
                //then server call for updating the data hits here and it wasn't expected at all
                //Debug.Log("tile fetched but it isn't expected anymore?");
                tile.AddLog("tile fetched from web but it isn't expected anymore?");
            }

            if (tile.CurrentTileState != TileState.Loaded)
            {
                //aborted web requests end up here
                return null;
            }

            var cacheItem = CreateVectorData(tile);
            _memoryCache.Add(cacheItem);
            SaveBlob(cacheItem, true);
            return cacheItem;
        }

        protected abstract ByteArrayTile CreateTile(CanonicalTileId canonicalTileId, string tilesetId);
        
        protected abstract T CreateVectorData(ByteArrayTile tile);
        
        private void CheckExpiration(T cacheItem)
        {
            if (cacheItem.ExpirationDate != null && 
                DateTime.Compare(cacheItem.ExpirationDate.Value, DateTime.Now) < 0)
            {
                var dataTile = CreateTile(cacheItem.TileId, cacheItem.TilesetId);
                dataTile.ETag = cacheItem.ETag;
                _waitingList.Add(cacheItem.TileId, dataTile);
                WebRequestData(dataTile, (fetchingResult) =>
                {
                    _waitingList.Remove(cacheItem.TileId);
                    if (dataTile.CurrentTileState != TileState.Canceled)
                    {
                        if (dataTile.StatusCode == 200)
                        {
                            //Debug.Log(string.Format("{0} - {1} : expired and returned 200, cached etag {0} new etag {1}", cacheItem.TileId, cacheItem.TilesetId, cacheItem.ETag, dataTile.ETag));
                            VectorReceivedFromWeb(dataTile);
                        }
                        else if (dataTile.StatusCode == 304)
                        {
                            //not changed, just update meta?
                            //Debug.Log(string.Format("{0} - {1} : expired but not changed, just update meta?", cacheItem.TileId, cacheItem.TilesetId));
                            UpdateExpiration(dataTile.Id, dataTile.TilesetId, dataTile.ExpirationDate);
                        }
                    }
                });
                //Debug.Log(cacheItem.TileId + " tile needs an update");
            }
            else
            {
                //Debug.Log(cacheItem.TileId + " doesnt needs an update");
            }
        }
    }
}