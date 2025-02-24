using System.Collections;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.Settings;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public interface ITileCreator
    {
        IEnumerator Initialize();
        UnityMapTile GetTile();
        void PutTile(UnityMapTile tile);
    }
    public class TileCreator : ITileCreator
    {
        public Material[] TileMaterials;
        private readonly int UsingLinearColorspace = Shader.PropertyToID("_UsingLinearColorspace");
        private ObjectPool<UnityMapTile> _tilePool;
        private UnityContext _unityContext;
        private TerrainStrategy _terrainStrategy;
        private ElevationLayerProperties _settings;
        private int _cacheSize;

        public UnityMapTile GetTile() => _tilePool.GetObject();
        public void PutTile(UnityMapTile tile) => _tilePool.Put(tile);

        public TileCreator(UnityContext unityContext, TerrainStrategy terrainStrategy,
            ElevationLayerProperties elevationProperties = null, Material[] tileMaterials = null, int cacheSize = 25)
        {
            TileMaterials = tileMaterials;
            _unityContext = unityContext;
            _terrainStrategy = terrainStrategy;
            _settings = elevationProperties ?? new ElevationLayerProperties();
            _cacheSize = cacheSize;
        }

        public IEnumerator Initialize()
        {
            _tilePool = new ObjectPool<UnityMapTile>(() => CreateTile(_unityContext));
            _terrainStrategy.Initialize(_settings);
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

            //settings colrospace flag for elevation calculations
            foreach (var material in tile.MeshRenderer.materials)
            {
                material.SetFloat(UsingLinearColorspace, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1.0f : 0.0f);
            }
            
            tile.gameObject.SetActive(false);

            _terrainStrategy.RegisterTile(tile, false);
            return tile;
        }
    }
}