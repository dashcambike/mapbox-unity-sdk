using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.TileJSON;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;

namespace Mapbox.UnityMapService.DataSources
{
    public abstract class UnitySource<T> : Source<T>, IMapboxCacheManager
    {
        public override bool IsReady()
        {
            return _isTileJsonReady;
        }

        protected string _tilesetId;
        private bool _isTileJsonReady;
        private TileJSONResponse _tileJsonResponse;
        protected int[] _sourceZoomRange;
        
        private readonly DataFetchingManager _dataFetchingManager;
        private readonly MapboxCacheManager _cacheManager;
        private IAsyncRequest _tileJsonRequest;
        private Dictionary<Tile, FetchInfo> _activeRequests;

        protected UnitySource(DataFetchingManager dataFetchingManager, MapboxCacheManager cacheManager, string tilesetId)
        {
            _tilesetId = tilesetId;
            _dataFetchingManager = dataFetchingManager;
            _cacheManager = cacheManager;
            _activeRequests = new Dictionary<Tile, FetchInfo>();
            _dataFetchingManager.FetchFinished += info =>
            {
                _activeRequests.Remove(info.Tile);
            };
            _dataFetchingManager.FetchCancelled += info =>
            {
                _activeRequests.Remove(info.Tile);
            };
        }

        public override IEnumerator Initialize()
        {
            while (!_isTileJsonReady)
            {
                if (_tileJsonRequest == null)
                {
                    _tileJsonRequest = _dataFetchingManager.GetTileJSON(1).Get(_tilesetId, (response) =>
                    {
                        if (response == null || response.MaxZoom == 0) //failed
                        {
                            //TODO fix this part
                            _tileJsonResponse = null;
                            _sourceZoomRange = new[] {0, 22};
                            _isTileJsonReady = true;
                        }
                        else
                        {
                            _tileJsonResponse = response;
                            _sourceZoomRange = new[] {_tileJsonResponse.MinZoom, _tileJsonResponse.MaxZoom};
                            _isTileJsonReady = true;
                        };
                    });
                }
                yield return null;
            }
        }

        protected void WebRequestData(Tile tile, Action<DataFetchingResult> callback)
        {
            var fetchInfo = new FetchInfo(tile, callback);
            _activeRequests.Add(tile, fetchInfo);
            _dataFetchingManager.EnqueueForFetching(fetchInfo);
        }
		
        protected void CancelFetching(Tile tile, string tilesetId)
        {
            tile.Cancel();
            _cacheManager.CancelFetching(tile.Id, tilesetId);
            //we removed data fetching cancel here as data fetching now track
            //removal through the tile.Cancel
            //no further calls are necessary
            // _dataFetchingManager.CancelFetching(tile, tilesetId);
        }

        public void SaveBlob(MapboxTileData vectorCacheItem, bool forceInsert)
        {
            _cacheManager.SaveBlob(vectorCacheItem, forceInsert);
        }

        public void SaveImage(RasterData textureCacheItem, bool forceInsert)
        {
            _cacheManager.SaveImage(textureCacheItem, forceInsert);
        }
        
        public void RemoveData(string tilesetId, int zoom, int x, int y)
        {
            _cacheManager.RemoveData(tilesetId, zoom, x, y);
        }
        
        public void GetImageAsync<T1>(CanonicalTileId tileId, string tilesetId, bool isTextureNonreadable, Action<T1> callback) where T1 : RasterData, new()
        {
            _cacheManager.GetImageAsync(tileId, tilesetId, isTextureNonreadable, callback);
        }
        
        public void GetTileInfoAsync<T1>(CanonicalTileId tileId, string tilesetid, Action<T1> callback, int priority = 1) where T1 : MapboxTileData, new()
        { 
            _cacheManager.GetTileInfoAsync<T1>(tileId, tilesetid, callback, priority);
        }
        
        public void ReadEtagExpiration<T1>(T1 data, Action callback, int priority = 1) where T1 : MapboxTileData, new()
        {
            _cacheManager.ReadEtagExpiration(data, callback, priority);
        }

        public void UpdateExpiration(CanonicalTileId tileId, string tilesetId, DateTime date)
        {
            _cacheManager.UpdateExpiration(tileId, tilesetId, date);
        }

        protected TypeMemoryCache<T1> RegisterTypeToMemoryCache<T1>(int owner, int cacheSize = 100) where T1 : MapboxTileData
        {
            return _cacheManager.RegisterMemoryCache<T1>(owner, cacheSize);
        }

        public override bool IsZinSupportedRange(int z)
        {
            return z >= _sourceZoomRange[0] && z <= _sourceZoomRange[1];
        }
    }
}