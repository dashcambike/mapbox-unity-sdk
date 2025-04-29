using System;
using System.Linq;
using Mapbox.BaseModule;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.Cache.SQLiteCache;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.Example.Scripts.ModuleBehaviours;
using Mapbox.Example.Scripts.TileProviderBehaviours;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using Mapbox.UnityMapService;
using Mapbox.UnityMapService.TileProviders;
using UnityEngine;

namespace Mapbox.Example.Scripts.Map
{
    public class MapboxMapBehaviour : MapBehaviourCore
    {
        [Tooltip("Unity tools for map to use")]
        public UnityContext UnityContext;

        [SerializeField] protected TileCreatorBehaviour _tileCreatorBehaviour;
        [SerializeField] protected TileProviderBehaviour TileProvider;
        [SerializeField] protected DataFetchingManagerBehaviour DataFetcher;
        [SerializeField] protected MapboxCacheManagerBehaviour CacheManager;
        private MapService _mapService;
        private bool _waitForFirstLoadEvent = true;
        
        public bool InitializeOnStart = true;
        public Action<MapService> MapServiceReady = (v) => { };

        public virtual void Start()
        {
            if (InitializeOnStart)
                Initialize();
        }
        
        [ContextMenu("Initialize")]
        public override void Initialize()
        {
            if (InitializationStatus != InitializationStatus.WaitingForInitialization)
                return;

            MapInformation.Initialize();
            UnityContext.Initialize();
            
            var mapboxContext = new MapboxContext();
            _mapService = GetMapService(mapboxContext, UnityContext);
            MapServiceReady(_mapService);

            MapboxMap = new MapboxMap(MapInformation, UnityContext, _mapService);
            //passing map info to visualizer for root object, default tile material/texture
            var mapVisualizer = CreateMapVisualizer(MapInformation, UnityContext);
            foreach (var moduleBaseScript in GetComponents<ModuleConstructorScript>())
            {
                if (moduleBaseScript.enabled)
                {
                    var layerModule = moduleBaseScript.ConstructModule(_mapService, MapInformation, UnityContext);
                    mapVisualizer.LayerModules.Add(layerModule);
                }
            }
            MapboxMap.MapVisualizer = mapVisualizer;
            MapboxMap.Initialized += InitializationCompleted;
             
            StartCoroutine(MapboxMap.Initialize());
        }
        
        private void InitializationCompleted()
        {
            Initialized(MapboxMap);
            if (!_waitForFirstLoadEvent)
            {
                //_readyForUpdates = true;
            }
            else
            {
                MapboxMap.LoadMapView(() =>
                {
                    //_readyForUpdates = true;
                });
            }
        }

        private void Update()
        {
            if (InitializationStatus == InitializationStatus.ReadyForUpdates && _mapService.IsReady())
            {
                MapboxMap.MapUpdated();
            }
        }
        
        private void OnValidate()
        {
            if(UnityContext == null)
                UnityContext = new UnityContext();
            if (UnityContext.MapRoot == null)
                UnityContext.MapRoot = transform;
            if (UnityContext.CoroutineStarter == null)
                UnityContext.CoroutineStarter = this;
        }

        private void OnDestroy()
        {
            MapboxMap?.OnDestroy();
            UnityContext.OnDestroy();
        }

        protected virtual MapService GetMapService(MapboxContext mapboxContext, UnityContext unityContext)
        {
            var tileProvider = TileProvider != null ? TileProvider.Core : new UnityTileProvider(new UnityTileProviderSettings(Camera.main));
            var dataFetchingManager = CreateDataFetchingManager(mapboxContext);

            var cacheManager = GetCacheManager(unityContext, dataFetchingManager);

            return new MapUnityService(
                unityContext,
                mapboxContext,
                tileProvider,
                cacheManager,
                dataFetchingManager);
        }

        private MapboxCacheManager GetCacheManager(UnityContext unityContext, DataFetchingManager dataFetchingManager)
        {
            if (CacheManager != null)
                return CacheManager.GetCacheManager(unityContext, dataFetchingManager);
            
            SqliteCache sqliteCache = null;
            FileCache fileCache = null;
            sqliteCache = new SqliteCache(unityContext.TaskManager, 1000);
            fileCache = new FileCache(unityContext.TaskManager);

            var cacheManager = new MapboxCacheManager(
                unityContext,
                new MemoryCache(),
                fileCache,
                sqliteCache);
            return cacheManager;
        }

        protected virtual MapboxMapVisualizer CreateMapVisualizer(IMapInformation mapInfo, UnityContext unityContext)
        {
            var tileCreator = _tileCreatorBehaviour != null
                ? _tileCreatorBehaviour.GetTileCreator(unityContext)
                : new TileCreator(unityContext);
            return new MapboxMapVisualizer(mapInfo, unityContext, tileCreator);
        }
        
        protected virtual DataFetchingManager CreateDataFetchingManager(MapboxContext mapboxContext)
        {
            return DataFetcher != null
                ? DataFetcher.GetDataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken)
                : new DataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);
        }
    }
}