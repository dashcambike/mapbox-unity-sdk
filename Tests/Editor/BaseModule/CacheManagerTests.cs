using System;
using System.Collections;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Unity;
using Mapbox.MapDebug.Scripts.Logging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mapbox.BaseModuleTests
{
    [TestFixture]
    internal class CacheManagerTests
    {
        private IMapboxCacheManager _cacheManager;
        private MockFileCache _fileCache;
        private MockSqliteCache _sqliteCache;
        
        private string _testTilesetName = "test_tilesetId";
        private CanonicalTileId _testTileId = new CanonicalTileId(16, 5, 7);
        private Texture2D _testTexture;
        private RasterData _testRasterData;
        private VectorData _testVectorData;
        private DateTime _testExpirationDate;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var unityContext = new UnityContext();
            var taskManager = new MockTaskManager();
            taskManager.Initialize();
            unityContext.TaskManager = taskManager;
            _fileCache = new MockFileCache(taskManager);
            _sqliteCache = new MockSqliteCache(taskManager);
            _sqliteCache.ReadySqliteDatabase();
            _cacheManager = new MapboxCacheManager(unityContext, new MemoryCache(), _fileCache, _sqliteCache);
            
            _testExpirationDate = DateTime.Now.AddDays(5);
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
            _testVectorData = new VectorData()
            {
                TilesetId = _testTilesetName,
                TileId = _testTileId,
                Data = new byte[10] { 0, 1, 0, 1, 0, 2, 0, 3, 0, 4 },
                ExpirationDate = DateTime.Now.AddHours(1),
                ETag = "testETAG"
            };
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
            _cacheManager.SaveImage(_testRasterData, false);
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
        
        [Test]
        public void SaveBlobTest()
        {
            _cacheManager.SaveBlob(_testVectorData, false);
            Assert.AreEqual(_sqliteCache.GetAllTiles().Count(), 1);
            var metaData = _sqliteCache.Get<VectorData>(_testTilesetName, _testTileId);
            Assert.AreEqual(metaData.ETag, _testVectorData.ETag);
            Assert.AreEqual(metaData.Data, _testVectorData.Data);
        }

        [UnityTest]
        public IEnumerator GetImageTest()
        {
            yield return SaveImageTest();
            RasterData resultData = null;
            bool isDone = false;
            _cacheManager.GetImageAsync<RasterData>(_testTileId, _testTilesetName, false, data =>
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
        public IEnumerator GetTileInfoTest()
        {
            SaveBlobTest();
            VectorData resultData = null;
            bool isDone = false;
            var wrapper = _cacheManager.CreateGetTileInfoTask<VectorData>(_testTileId, _testTilesetName);
            wrapper.DataCompleted += (task, data) =>
            {
                resultData = data;
                isDone = true;
            };
            _cacheManager.AddTask(wrapper);
            while (!isDone) yield return null;
            
            Assert.AreEqual(_testVectorData.TilesetId, resultData.TilesetId);
            Assert.AreEqual(_testVectorData.TileId, resultData.TileId);
            Assert.AreEqual(_testVectorData.Data, resultData.Data);
        }

        [UnityTest]
        public IEnumerator ReadEtagTest()
        {
            yield return SaveImageTest();
            
            RasterData resultData = null;
            bool isDone = false;
            _cacheManager.GetImageAsync<RasterData>(_testTileId, _testTilesetName, false, data =>
            {
                resultData = data;
                isDone = true;
            });
            while (!isDone) yield return null;

            Assert.IsNull(resultData.ETag);
            Assert.IsNull(resultData.ExpirationDate);
            isDone = false;
            var wrapper = _cacheManager.CreateReadEtagExpirationTask(resultData);
            wrapper.DataCompleted += (task, data) => isDone = true;
            _cacheManager.AddTask(wrapper);
            while (!isDone) yield return null;
            
            Assert.AreEqual(_testRasterData.ETag, resultData.ETag);
            Assert.Less(_testRasterData.ExpirationDate.Value.Subtract(resultData.ExpirationDate.Value).TotalMinutes, 5);
        }

        [UnityTest]
        public IEnumerator UpdateExpirationTest()
        {
            yield return SaveImageTest();
            
            RasterData resultData = null;
            bool isDone = false;
            var wrapper = _cacheManager.CreateGetTileInfoTask<RasterData>(_testTileId, _testTilesetName);
            wrapper.DataCompleted += (task, data) =>
            {
                isDone = true;
                resultData = data;
            };
            _cacheManager.AddTask(wrapper);
            while(!isDone) yield return null;
            
            Assert.NotNull(resultData);
            Assert.Less(_testRasterData.ExpirationDate.Value.Subtract(resultData.ExpirationDate.Value).TotalMinutes, 5);
            
            _cacheManager.UpdateExpiration(_testTileId, _testTilesetName, _testExpirationDate);
            
            isDone = false;
            var wrapper2 = _cacheManager.CreateGetTileInfoTask<RasterData>(_testTileId, _testTilesetName);
            wrapper2.DataCompleted += (task, data) =>
            {
                isDone = true;
                resultData = data;
            };
            _cacheManager.AddTask(wrapper2);
            while(!isDone) yield return null;
            
            Assert.NotNull(resultData);
            Assert.Less(_testExpirationDate.Subtract(resultData.ExpirationDate.Value).TotalMinutes, 5);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _fileCache.ClearAll();
            _sqliteCache.ClearDatabase();
            _sqliteCache.DeleteSqliteFile();
            _sqliteCache.Dispose();
        }
    }
}