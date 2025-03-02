using System;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using UnityEngine;

namespace Mapbox.ImageModule.Terrain
{
    [Serializable]
    public class TerrainLayerModuleSettings
    {
        public ElevationLayerType ElevationLayerType = ElevationLayerType.FlatTerrain;
        [NonSerialized] public ElevationSourceType SourceType = ElevationSourceType.MapboxTerrain;
        public bool LoadBackgroundTextures = false;
        public bool UseShaderTerrain = true;
        public ImageSourceSettings DataSettings;
        
        [Tooltip("Tile outside this range will be rejected.")]
        public Vector2 RejectTilesOutsideZoom = new Vector2(12, 16);
    }
}