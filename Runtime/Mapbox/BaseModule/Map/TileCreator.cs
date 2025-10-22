using System;
using System.Collections;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.Settings;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public interface ITileCreator
    {
        IEnumerator Initialize(TerrainStrategy terrainStrategy = null);
        UnityMapTile GetTile();
        void PutTile(UnityMapTile tile);

        /// <summary>
        /// Some tile elements are destroyed so tile isn't good for use/reuse anymore
        /// </summary>
        event Action<UnwrappedTileId> OnTileBroken;
    }
    public class TileCreator : ITileCreator
    {
        public Material[] TileMaterials;
        private ObjectPool<UnityMapTile> _tilePool;
        private UnityContext _unityContext;
        private int _cacheSize;
        //private FlatTerrainStrategy _flatTerrainStrategy;
        private TerrainStrategy _terrainStrategy;

        public UnityMapTile GetTile() => _tilePool.GetObject();
        public void PutTile(UnityMapTile tile) => _tilePool.Put(tile);

        public TileCreator(UnityContext unityContext, Material[] tileMaterials = null, int cacheSize = 25)
        {
            TileMaterials = tileMaterials;
            _unityContext = unityContext;
            _cacheSize = cacheSize;
        }

        public IEnumerator Initialize(TerrainStrategy terrainStrategy = null)
        {
            _terrainStrategy = terrainStrategy;
            _tilePool = new ObjectPool<UnityMapTile>(() => CreateTile(_unityContext));
            yield return _tilePool.InitializeItems(_cacheSize);
        }

        private UnityMapTile CreateTile(UnityContext unityContext)
        {
            var tile = new GameObject("TilePoolObject").AddComponent<UnityMapTile>();
            if (_unityContext.BaseTileRoot != null)
            {
                tile.gameObject.layer = _unityContext.BaseTileRoot.gameObject.layer;
                tile.transform.SetParent(_unityContext.BaseTileRoot, false);
            }

            if (TileMaterials?.Length > 0)
            {
                tile.MeshRenderer.materials = TileMaterials;
            }

            tile.Material = tile.MeshRenderer.material;
            tile.gameObject.SetActive(false);
            _terrainStrategy?.RegisterTile(tile, false);
            tile.OnDataDisposed += OnTileBroken;
            
            return tile;
        }

        public event Action<UnwrappedTileId> OnTileBroken = (id) => { };
    }
}