using System;
using System.Collections;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.MapDebug.Scripts.Logging;
using Mapbox.UnityMapService.DataSources;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mapbox.BaseModuleTests
{
    [TestFixture]
    internal class PbfSourceTests
    {
        private MockSqliteCache _sqliteCache;
        private MapboxCacheManager _cacheManager;
        private DataFetchingManager _fetchingManager;
        private VectorSource _vectorSource;

        private string _testTilesetName = "mapbox.mapbox-streets-v8";
        private CanonicalTileId _testTileId = new CanonicalTileId(16, 5, 7);
        private Texture2D _testTexture;
        private VectorData _testVectorData;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testTexture = Texture2D.whiteTexture;
            _testVectorData = new VectorData()
            {
                TilesetId = _testTilesetName,
                TileId = _testTileId,
                Data = ImageConversion.EncodeToJPG(_testTexture),
                ExpirationDate = DateTime.Now.AddHours(1),
                ETag = "testETAG"
            };

            var mapboxContext = new MapboxContext();
            var unityContext = new UnityContext();
            var taskManager = new MockTaskManager();
            taskManager.Initialize();
            unityContext.TaskManager = taskManager;
            _sqliteCache = new MockSqliteCache(taskManager);
            _sqliteCache.ReadySqliteDatabase();
            _cacheManager = new MapboxCacheManager(unityContext, new MemoryCache(), null, _sqliteCache);
            _fetchingManager =
                new LoggingDataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);
            _vectorSource = new VectorSource(_fetchingManager, _cacheManager,
                new VectorSourceSettings() { TilesetId = _testTilesetName });
            _vectorSource.Initialize();
        }

        [SetUp]
        public void Setup()
        {
            _sqliteCache.ClearDatabase();
            _sqliteCache.ReadySqliteDatabase();
        }

        [UnityTest]
        public IEnumerator SaveBlobTest()
        {
            _vectorSource.SaveBlob(_testVectorData, false);

            VectorData resultData = null;
            bool isDone = false;
            Runnable.Instance.StartCoroutine(_vectorSource.LoadTileCoroutine(_testTileId, data =>
            {
                isDone = true;
                resultData = data;
            }));
            while (!isDone) yield return null;
            
            Assert.NotNull(resultData);
            Assert.AreEqual(_testTileId, resultData.TileId);
            Assert.AreEqual(_testTilesetName, resultData.TilesetId);
            Assert.AreEqual(_testVectorData.Data, resultData.Data);
        }
    }

    [TestFixture]
    internal class ImageSourceTests
    {
        private MockFileCache _fileCache;
        private MockSqliteCache _sqliteCache;
        private MapboxCacheManager _cacheManager;
        private DataFetchingManager _fetchingManager;
        private ImageSource<RasterData> _imageSource;

        private string _testTilesetName = "mapbox.satellite";
        private CanonicalTileId _testTileId = new CanonicalTileId(16, 5, 7);
        private Texture2D _testTexture;
        private RasterData _testRasterData;
        
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testTexture = Texture2D.whiteTexture;
            _testRasterData = new RasterData()
            {
                TilesetId = _testTilesetName,
                TileId = _testTileId,
                Texture = _testTexture,
                Data = ImageConversion.EncodeToJPG(_testTexture),
                ExpirationDate = DateTime.Now.AddHours(1),
                ETag = "testETAG"
            };
            
            var mapboxContext = new MapboxContext();
            var unityContext = new UnityContext();
            var taskManager = new MockTaskManager();
            taskManager.Initialize();
            unityContext.TaskManager = taskManager;
            _fileCache = new MockFileCache(taskManager);
            _sqliteCache = new MockSqliteCache(taskManager); 
            _sqliteCache.ReadySqliteDatabase();
            _cacheManager = new MapboxCacheManager(unityContext, new MemoryCache(), _fileCache, _sqliteCache);
            _fetchingManager = new LoggingDataFetchingManager(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);
            _imageSource = new StaticSource(_fetchingManager, _cacheManager,
                new ImageSourceSettings() { TilesetId = _testTilesetName, UseNonReadableTextures = false, CacheSize = 0});
            _imageSource.Initialize();
        }

        [SetUp]
        public void Setup()
        {
            _fileCache.ClearAll();
            _sqliteCache.ClearDatabase();
            _sqliteCache.ReadySqliteDatabase();
        }
        
        [UnityTest]
        public IEnumerator SaveImageTest()
        {
            _imageSource.SaveImage(_testRasterData, false);
            
            Assert.AreEqual(_fileCache.GetFileList().Count, 1);
            Assert.AreEqual(_sqliteCache.GetAllTiles().Count(), 1);
            var metaData = _sqliteCache.Get<RasterData>(_testTilesetName, _testTileId);
            Assert.AreEqual(metaData.ETag, _testRasterData.ETag); 
             
            RasterData resultData = null;
            bool isDone = false;
            _fileCache.GetAsync<RasterData>(_testTileId, _testTilesetName, false, 
                data =>
                {
                    isDone = true;
                    resultData = data;
                });
            while (!isDone) yield return null;
            Assert.AreEqual(_testTexture.GetPixels32(), resultData.Texture.GetPixels32());
        }
        
        [UnityTest]
        public IEnumerator GetImageCallback()
        {
            yield return SaveImageTest();
            
            RasterData resultData = null;
            bool isDone = false;
            _imageSource.GetImageAsync<RasterData>(_testTileId, _testTilesetName, false, data =>
            {
                resultData = data;
                isDone = true;
            });
            while (!isDone) yield return null;
            
            Assert.AreEqual(_testRasterData.TilesetId, resultData.TilesetId);
            Assert.AreEqual(_testRasterData.TileId, resultData.TileId);
            Assert.AreEqual(_testTexture.GetPixels32(), resultData.Texture.GetPixels32());
        }
        
        [UnityTest]
        public IEnumerator GetImageCoroutineTest()
        {
            yield return SaveImageTest();
            
            RasterData resultData = null;
            
            //doing this inWorking trick because editor tests only supports yield return null
            bool isWorking = true;
            Runnable.Instance.StartCoroutine(_imageSource.GetImageCoroutine<RasterData>(_testTileId, _testTilesetName,
                false, data =>
                {
                    isWorking = false;
                    resultData = data;
                }));
            while(isWorking) yield return null;
            
            Assert.AreEqual(_testRasterData.TilesetId, resultData.TilesetId);
            Assert.AreEqual(_testRasterData.TileId, resultData.TileId);
            Assert.AreEqual(_testTexture.GetPixels32(), resultData.Texture.GetPixels32());
        }
        
        [UnityTest]
        public IEnumerator LoadTileCoroutineTestNoPreSaveShouldWeb()
        {
            RasterData resultData = null;
           
            //doing this inWorking trick because editor tests only supports yield return null
            bool isWorking = true; 
            Runnable.Instance.StartCoroutine(_imageSource.LoadTileCoroutine(_testTileId, data =>
            {
                isWorking = false;
                resultData = data;
            }));
            while(isWorking) yield return null;
            
            Assert.AreEqual(_testRasterData.TilesetId, resultData.TilesetId);
            Assert.AreEqual(_testRasterData.TileId, resultData.TileId);
            Assert.AreNotEqual(resultData.Texture.GetPixels32().Length, 0);
            Assert.AreEqual(resultData.CacheType, CacheType.NoCache);
        }
        
        [UnityTest]
        public IEnumerator LoadTileCoroutineTestShouldFileCache()
        {
            yield return SaveImageTest();
            
            RasterData resultData = null;
            
            //doing this inWorking trick because editor tests only supports yield return null
            bool isWorking = true; 
            Runnable.Instance.StartCoroutine(_imageSource.LoadTileCoroutine(_testTileId, data =>
            {
                isWorking = false;
                resultData = data;
            }));
            while(isWorking) yield return null;
            
            Assert.AreEqual(_testRasterData.TilesetId, resultData.TilesetId);
            Assert.AreEqual(_testRasterData.TileId, resultData.TileId);
            Assert.AreNotEqual(resultData.Texture.GetPixels32().Length, 0);
            Assert.AreEqual(resultData.CacheType, CacheType.FileCache);
        }
    }
}