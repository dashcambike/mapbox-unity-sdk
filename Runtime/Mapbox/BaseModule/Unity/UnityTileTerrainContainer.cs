using System;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.BaseModule.Unity
{
    [Serializable]
    public class UnityTileTerrainContainer
    {
        public TileContainerState State = TileContainerState.Final;
        
        private Action ElevationValuesUpdated;
        private Action _onDisposeCallback;
        private const string ElevationMultiplierFieldNameID = "_ElevationMultiplier";
        private const string ElevationChangeTimerFieldNameID = "_ElevationChangeTime";
        private const string ShaderElevationTextureScaleOffsetFieldNameID = "_HeightTexture_ST";
        private const string ShaderElevationTextureFieldNameID = "_HeightTexture";
		
        private static readonly int ElevationMultiplier = Shader.PropertyToID(ElevationMultiplierFieldNameID);
        private static readonly int ElevationChangeTime = Shader.PropertyToID(ElevationChangeTimerFieldNameID);
        private static readonly int HeightTextureST = Shader.PropertyToID(ShaderElevationTextureScaleOffsetFieldNameID);
        private static readonly int HeightTexture = Shader.PropertyToID(ShaderElevationTextureFieldNameID);
        
        
        private UnityMapTile _unityMapTile;
        [SerializeField] public TerrainData TerrainData;
        private Vector4 _terrainTextureScaleOffset;

        public UnityTileTerrainContainer(UnityMapTile unityMapTile, Action elevationUpdatedCallback, Action onDisposeCallback)
        {
            _unityMapTile = unityMapTile;
            _onDisposeCallback = onDisposeCallback;
            ElevationValuesUpdated = elevationUpdatedCallback;
        }

        public void SetTerrainData(TerrainData terrainData, bool useShaderElevation, TileContainerState state = TileContainerState.Final)
        {
            terrainData?.SetDisposeCallback(null);
            
            State = state;
            if (terrainData.Texture == null || terrainData.TileId.Z == 0)
            {
                Debug.Log("no texture?");
            }
            TerrainData = terrainData;
            TerrainData.SetDisposeCallback(_onDisposeCallback);
            
            OnTerrainUpdated();
            if (TerrainData.IsElevationDataReady)
            {
                OnElevationValuesUpdated();
            }

            TerrainData.SetElevationChangedCallback(OnElevationValuesUpdated);
            //TerrainData.ElevationValuesUpdated += OnElevationValuesUpdated;

            _unityMapTile.Material.SetFloat(ElevationMultiplier, useShaderElevation ? 1 : 0);
        }

        public void OnTerrainUpdated()
        {
            if (TerrainData == null)
                return;
        
            _terrainTextureScaleOffset = _unityMapTile.CanonicalTileId.CalculateScaleOffsetAtZoom(TerrainData.TileId.Z);
            
            _unityMapTile.Material.SetVector(HeightTextureST, _terrainTextureScaleOffset);
            _unityMapTile.Material.SetTexture(HeightTexture, TerrainData.Texture);

            //_unityMapTile._material.SetFloat(_tileScaleFieldNameID, _unityMapTile.TileScale);
            _unityMapTile.Material.SetFloat("_IsFallbackTexture", 0);
            _unityMapTile.Material.SetFloat(ElevationChangeTime, Time.time);
        }

        public void OnElevationValuesUpdated()
        {
            if (TerrainData == null)
            {
                Debug.Log("TerrainData is null, missing a isRecycled check?");
                return;
            }
            TerrainData.IsElevationDataReady = true;
            ElevationValuesUpdated();
        }

        public TerrainData GetAndClearTerrainData()
        {
            if (TerrainData == null)
                return null;

            TerrainData.ElevationValuesUpdated -= OnElevationValuesUpdated;
            _unityMapTile.Material.SetTexture(HeightTexture, Texture2D.grayTexture);
            var rd = TerrainData;
            TerrainData = null;
            return rd;
        }
        
        public float QueryHeightData(float x, float y)
        {
            if (TerrainData != null && TerrainData.ElevationValues.Length > 0)
            {
                var width = (int)Mathf.Sqrt(TerrainData.ElevationValues.Length);
                var sectionWidth = width * _terrainTextureScaleOffset.x - 1;
                var padding = width * new Vector2(_terrainTextureScaleOffset.z, _terrainTextureScaleOffset.w);
                
                var xx = padding.x + (x * sectionWidth);
                var yy = padding.y + (y * sectionWidth);

                var index = (int) yy * width
                            + (int) xx;
                if (TerrainData.ElevationValues.Length <= index)
                {
                    return 0;
                }
                else
                {
                    return TerrainData.ElevationValues[(int) yy * width + (int) xx];
                }

            }
            return 0;
        }

        public void DisableTerrain()
        {
            State = TileContainerState.Final;
            _unityMapTile.Material.SetFloat(ElevationMultiplier, 0);
        }

        public void OnDestroy()
        {
            if (TerrainData != null)
            {
                TerrainData.ElevationValuesUpdated -= OnElevationValuesUpdated;
                TerrainData = null;
            }
        }
    }
    
    public enum TileContainerState
    {
        Temporary,
        Final
    }
}