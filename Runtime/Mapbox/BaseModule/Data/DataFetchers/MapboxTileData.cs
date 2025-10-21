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
        
        private Action _onDispose;

        public virtual void Dispose()
        {
            _onDispose?.Invoke();
        }

        internal void SetDisposeCallback(Action callback)
        {
            _onDispose = callback;
        }
    }
}