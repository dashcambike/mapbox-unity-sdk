using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.BaseModule.Map
{
    [Serializable]
    public class ImageSourceSettings
    {
        [NonSerialized] public string TilesetId;
        [Tooltip("Use 2x texture sheets")]
        public bool UseRetinaTextures = true;
        [Tooltip("Non-readable textures halves the memory usage but cannot be queried for pixel values. Suitable if texture will be directly passed to shader.")]
        public bool UseNonReadableTextures = true;
        public int CacheSize = 100;
        
        [Tooltip("Maximum data level that'll be used for this module. Tiles can be higher zoom level but data will be lower level.")]
        public int ClampDataLevelToMax = 16;
    }
}