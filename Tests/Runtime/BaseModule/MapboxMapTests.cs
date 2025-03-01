using System.Collections;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.TileJSON;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule.Terrain.TerrainStrategies;
using Mapbox.MapDebug.Scripts.Logging;
using Mapbox.UnityMapService;
using Mapbox.UnityMapService.TileProviders;
using NUnit.Framework;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mapbox.BaseModuleTests.DataTests
{
    public class MapboxMapTests
    {
        private string _helsinkiLatitudeLongitudeString = "60.1734031,24.9428875";
        private string _sanFranciscoLatitudeLongitudeString = "60.1734031,24.9428875";
        private LatitudeLongitude _helsinkiLatLng;
        private LatitudeLongitude _sfLatLng;
        private MapboxMap _map;
        
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _helsinkiLatLng = Conversions.StringToLatLon(_helsinkiLatitudeLongitudeString);
            _sfLatLng = Conversions.StringToLatLon(_sanFranciscoLatitudeLongitudeString);

            var mapInfo = new MapInformation(_helsinkiLatitudeLongitudeString);
            mapInfo.SetInformation(null, 16, 45, null, 1000);
            mapInfo.Initialize();
            var mapboxContext = new MapboxContext();
            var unityContext = new UnityContext();
            unityContext.Initialize();

            var taskManager = new TaskManager();
            taskManager.Initialize();
            unityContext.TaskManager = taskManager;
            var dataManager = new DataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);

            var sqliteCache = new MockSqliteCache(taskManager);
            sqliteCache.ReadySqliteDatabase();
            
            var mapService = new MapUnityService(
                unityContext,
                mapboxContext,
                new UnityFixedAreaTileProvider(),
                new MapboxCacheManager(
                    unityContext, 
                    new MemoryCache(),
                    new MockFileCache(taskManager),
                    sqliteCache),
                dataManager);
            
            _map = new MapboxMap(mapInfo, unityContext, mapService);
            var mapVisualizer = new MapboxMapVisualizer(mapInfo, unityContext, new TileCreator(unityContext, new FlatTerrainStrategy()));
            _map.MapVisualizer = mapVisualizer;
        }

        [UnitySetUp]
        public IEnumerator Setup()
        {
            var initialization = Runnable.Instance.StartCoroutine(_map.Initialize());
            yield return initialization;
            
            Assert.IsNotNull(_map);
            Assert.IsNotNull(_map.MapVisualizer);
            Assert.IsTrue(_map.Status >= InitializationStatus.Initialized);
        }

        [Test]
        public void LatLngConversion()
        {
            var latlngToPosition = _map.MapInformation.ConvertLatLngToPosition(_helsinkiLatLng);
            var posToLatlng = _map.MapInformation.ConvertPositionToLatLng(Vector3.zero);
            
            Assert.AreEqual(latlngToPosition.x, 0);
            Assert.AreEqual(latlngToPosition.y, 0);
            Assert.AreEqual(latlngToPosition.z, 0);
            Assert.AreEqual(posToLatlng.Latitude, _helsinkiLatLng.Latitude, 0.001f);
            Assert.AreEqual(posToLatlng.Longitude, _helsinkiLatLng.Longitude, 0.001f);
            
            _map.UpdateTileCover();
            Assert.IsNotNull(_map.TileCover.Tiles);
        }

        [Test]
        public void CacheManager()
        {
            var mapService = (MapUnityService) _map.MapService;
            var cacheManager = mapService.GetCacheManager();
            Assert.IsNotNull(cacheManager);
        }

        [UnityTest]
        public IEnumerator TileJson()
        {
            var mapService = (MapUnityService) _map.MapService;
            var dataFetcher = mapService.GetFetchingManager();
            Assert.IsNotNull(dataFetcher);
            var tileJson = dataFetcher.GetTileJSON();
            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            TileJSONResponse response = null;
            var request = tileJson.Get(vectorTileset.Id, r =>
            {
                response = r;
            });
            while (request.IsCompleted == false) yield return null;
            Assert.IsNotNull(response);
        }

        [UnityTest]
        public IEnumerator LoadMapView()
        {
            var mapLoaded = false;
            var firstViewCompletedEventFired = false;
            _map.OnFirstViewCompleted += () => { firstViewCompletedEventFired = true; };
            
            var coroutine = Runnable.Instance.StartCoroutine(_map.LoadMapViewCoroutine(() =>
            {
                mapLoaded = true;
            }));
            while(mapLoaded == false) yield return null;
            
            Assert.IsTrue(mapLoaded);
            Assert.IsTrue(firstViewCompletedEventFired);
            Assert.IsTrue(_map.Status > InitializationStatus.Initialized);
            Assert.IsNotEmpty(_map.TileCover.Tiles);
        }

        [Test]
        public void ChangeViewToSF()
        {
            _map.ChangeView(_sfLatLng);
            var latlngToPosition = _map.MapInformation.ConvertLatLngToPosition(_sfLatLng);
            var posToLatlng = _map.MapInformation.ConvertPositionToLatLng(Vector3.zero);
            
            Assert.AreEqual(latlngToPosition.x, 0);
            Assert.AreEqual(latlngToPosition.y, 0);
            Assert.AreEqual(latlngToPosition.z, 0);
            Assert.AreEqual(posToLatlng.Latitude, _sfLatLng.Latitude, 0.001f);
            Assert.AreEqual(posToLatlng.Longitude, _sfLatLng.Longitude, 0.001f);

            _map.ChangeView(_sfLatLng, 12, 45, 30);
            Assert.AreEqual(_map.MapInformation.Zoom, 12);
            Assert.AreEqual(_map.MapInformation.Pitch, 45);
            Assert.AreEqual(_map.MapInformation.Bearing, 30);
        }
    }

    public class DataFetcherTests
    {
        private LoggingDataFetchingManager _datafetcher;
        private CanonicalTileId _tileId;
        private string _tilesetId;
        
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            var mapboxContext = new MapboxContext();
            _datafetcher = new LoggingDataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);
            var vectorTileset = MapboxDefaultVector.GetParameters(VectorSourceType.MapboxStreetsV8);
            _tilesetId = vectorTileset.Id;
            _tileId = Conversions.LatitudeLongitudeToTileId(Conversions.StringToLatLon("60.1734031,24.9428875"), 16).Canonical;
        }

        [UnityTest]
        public IEnumerator VectorFetching()
        {
            var tile = new BaseModule.Data.Tiles.VectorTile(_tileId, _tilesetId);
            bool isDone = false;
            _datafetcher.EnqueueForFetching(new FetchInfo(tile, (result) =>
            {
                isDone = true;
            }));
            //Assert.AreEqual(_datafetcher.TotalRequestCount, 1);
            while (isDone == false) yield return null;
            
            Assert.NotNull(tile);
            Assert.NotNull(tile.ByteData);
            Assert.IsNotEmpty(tile.ByteData);
            Assert.AreEqual(_datafetcher.TotalRequestCount, 0);
        }
        
        
        [UnityTest]
        public IEnumerator VectorFetchingCancelled()
        {
            var tile = new BaseModule.Data.Tiles.VectorTile(_tileId, _tilesetId);
            bool isDone = false;
            _datafetcher.EnqueueForFetching(new FetchInfo(tile, (result) =>
            {
                isDone = true;
            }));
            Assert.AreEqual(_datafetcher.TotalRequestCount, 1);
            _datafetcher.CancelFetching(tile, _tilesetId);
            while (isDone == false) yield return null;
            
            Assert.NotNull(tile);
            Assert.AreEqual(tile.CurrentTileState, TileState.Canceled);
            Assert.AreEqual(_datafetcher.TotalRequestCount, 0);
        }
    }
}