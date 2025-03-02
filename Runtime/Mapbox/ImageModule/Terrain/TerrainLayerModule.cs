using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.ImageModule.Terrain
{
    public class TerrainLayerModule : ITerrainLayerModule
    {
        private TerrainLayerModuleSettings _settings;
        private Source<TerrainData> _rasterSource;
        private HashSet<CanonicalTileId> _retainedTerrainTiles;
        
        //Terrain module doesn't support cpu elevation now after TileCreator changes
        public TerrainLayerModule(Source<TerrainData> source, TerrainLayerModuleSettings settings) : base()
        {
            _settings = settings;
            _retainedTerrainTiles = new HashSet<CanonicalTileId>();
            _rasterSource = source;
        }
        
        public virtual IEnumerator Initialize()
        {
            yield return _rasterSource.Initialize();
            
            if(_settings.LoadBackgroundTextures)
            {
                _rasterSource?.DownloadAndCacheBaseTiles();
            }
        }

        public virtual void LoadTempTile(UnityMapTile unityTile)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.FlatTerrain || 
                unityTile.CanonicalTileId.Z < _settings.RejectTilesOutsideZoom.x)
            {
                unityTile.TerrainContainer.DisableTerrain();
                return;
            }
            
            var targetTileId = GetDataId(unityTile.CanonicalTileId);
            var parentTileId = targetTileId;
            for (int i = parentTileId.Z; i >= 2; i--)
            {
                parentTileId = parentTileId.Parent;
                if (_rasterSource.GetInstantData(parentTileId, out var instantData))
                {
                    unityTile.TerrainContainer.SetTerrainData(instantData, _settings.UseShaderTerrain, TileContainerState.Temporary);
                    return;
                }
            }
        }
        
        public virtual bool LoadInstant(UnityMapTile unityTile)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.FlatTerrain || 
                unityTile.CanonicalTileId.Z < _settings.RejectTilesOutsideZoom.x)
            {
                unityTile.TerrainContainer.DisableTerrain();
                return true;
            }
            
            var targetTileId = GetDataId(unityTile.CanonicalTileId);
            if (_rasterSource.GetInstantData(targetTileId, out var instantData) && instantData.IsElevationDataReady)
            {
                unityTile.TerrainContainer.SetTerrainData(instantData, _settings.UseShaderTerrain);
                return true;
            }
            
            return false;
        }
        
        public virtual bool RetainTiles(HashSet<CanonicalTileId> retainedTiles,
            Dictionary<UnwrappedTileId, UnityMapTile> activeTiles)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.FlatTerrain)
                return true;
            
            var isReady = true;
            _retainedTerrainTiles.Clear();
            foreach (var tileId in retainedTiles)
            {
                _retainedTerrainTiles.Add(GetDataId(tileId));
            }
            
            isReady = _rasterSource.RetainTiles(_retainedTerrainTiles);
            return isReady;
        }

        public float QueryElevation(CanonicalTileId tileId, float x, float y)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.TerrainWithElevation)
            {
                var originalTileId = tileId;
                var targetTileId = tileId;
                for (int i = 0; i < 5; i++)
                {
                    if (_rasterSource.GetInstantData(targetTileId, out var instantData))
                    {
                        return instantData.QueryHeightData(originalTileId, x, y);
                    }
                    targetTileId = targetTileId.Parent;
                }
                
                return 0;
            }
            else
            {
                return 0;
            }
        }
        
        public void UpdatePositioning(IMapInformation mapInfo)
        {
            
        }
                
        public void OnDestroy()
        {
            _rasterSource.OnDestroy();
        }
        
        //COROUTINE METHODS only used in initialization so far
        #region coroutine methods
        public virtual IEnumerator LoadTileData(CanonicalTileId tileId, Action<MapboxTileData> callback = null)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.TerrainWithElevation)
            {
                return _rasterSource.LoadTileCoroutine(tileId, callback);
            }
            return null;
        }

        public virtual IEnumerator LoadTiles(IEnumerable<CanonicalTileId> tiles)
        {
            if (_settings.ElevationLayerType == ElevationLayerType.TerrainWithElevation)
            {
                yield return _rasterSource.LoadTilesCoroutine(GetDataId(tiles));
            }
        }
        #endregion
        
        
        
        
        //PRIVATE METHODS
        private CanonicalTileId GetDataId(CanonicalTileId tileId)
        {
            var maxZoom = _settings.DataSettings.ClampDataLevelToMax;
            if (tileId.Z >= maxZoom)
            {
                return tileId.Z > maxZoom
                    ? tileId.ParentAt(maxZoom)
                    : tileId;
            }
            else
            {
                return tileId;
            }
        }
        
        private IEnumerable<CanonicalTileId> GetDataId(IEnumerable<CanonicalTileId> tileIdList)
        {
            return tileIdList.Select(GetDataId).ToList();
        }

    }
}