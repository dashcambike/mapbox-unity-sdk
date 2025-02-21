using System;
using Mapbox.BaseModule.Data.Tiles;
using UnityEngine;

namespace Mapbox.BaseModule.Data.DataFetchers
{
    public class MapboxTileData
    {
        public CanonicalTileId TileId;
        public string TilesetId;
        public CacheType CacheType;
        public string ETag;
        public DateTime? ExpirationDate;
        public bool HasError = false;
        [HideInInspector] public byte[] Data;

        public virtual void Dispose()
        {
            
        }
    }
}