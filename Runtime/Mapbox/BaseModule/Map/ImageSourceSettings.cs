using System;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    [Serializable]
    public class ImageSourceSettings
    {
        [NonSerialized] public string TilesetId;
        public bool UseRetinaTextures = true;
        public bool UseNonReadableTextures = true;
        public int CacheSize = 100;
        public int RejectTilesBelowZoom = 2;
        public int ClampTilesToMax = 16;
    }
}