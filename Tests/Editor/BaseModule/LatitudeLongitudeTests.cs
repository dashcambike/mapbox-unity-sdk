using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.BaseModule.Utilities.JsonConverters;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Mapbox.BaseModuleTests
{
    public class LatitudeLongitudeTests
    {
        private ResilientWebRequestFileSource _fs;
        private string _lonLatStr = "[-77.0295,38.9165]";
        private Vector2d _latLonObject = new Vector2d(38.9165, -77.0295);
        
        [SetUp]
        public void SetUp()
        {
            Runnable.EnableRunnableInEditor();
            var mapboxContext = new MapboxContext();
            _fs = new ResilientWebRequestFileSource(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken);
        }

        [Test]
        public void NullIsland()
        {
            var lngLat = new LatitudeLongitude(0, 0);
            Assert.AreEqual("0,0", lngLat.ToString());
        }

        [Test]
        public void WashingtonDCLatitudeLontitude()
        {
            var lngLat = new LatitudeLongitude(-77.0295, 38.9165);
            Assert.AreEqual("-77.0295,38.9165", lngLat.ToString());
        }
        
        [Test]
        public void Deserialize()
        {
            Vector2d deserializedLonLat = JsonConvert.DeserializeObject<Vector2d>(_lonLatStr, JsonConverters.Converters);
            Assert.AreEqual(_latLonObject.ToString(), deserializedLonLat.ToString());
        }

        [Test]
        public void Serialize()
        {
            string serializedLonLat = JsonConvert.SerializeObject(_latLonObject, JsonConverters.Converters);
            Assert.AreEqual(_lonLatStr, serializedLonLat);
        }
    }
}