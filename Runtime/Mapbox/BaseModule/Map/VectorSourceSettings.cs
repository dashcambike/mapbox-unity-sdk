using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.BaseModule.Map
{
    [Serializable]
    public class VectorSourceSettings
    {
        [NonSerialized] public string TilesetId;
        public int CacheSize = 100;
        [Tooltip("Tile will be processed using data from this range, values bigger or smaller will be clipped to this.")]
        public Vector2 ActiveZoomLevelRange = new Vector2(12, 16);
    }
}