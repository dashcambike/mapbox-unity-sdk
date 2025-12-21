using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace Mapbox.BaseModuleTests
{
    public class ConversionTests
    {
        [Test]
        public void TileIdToBoundsCenterEqualsToLatlngToTileId()
        {
            var tileId = new CanonicalTileId(16, 37309, 18968);
            var bounds = Conversions.TileIdToBounds(tileId);
            var center = bounds.Center;
            var newTileId = Conversions.LatitudeLongitudeToTileId(center, 16);
            Assert.AreEqual(tileId.ToString(), newTileId.ToString());
        }
        
        [Test]
        public void TileIdToCenterLatLngToTile01ToLatlng()
        {
            var tileId = new CanonicalTileId(16, 37309, 18968);
            var bounds = Conversions.TileIdToBounds(tileId);
            var center = bounds.Center;
            var zeroOne = Conversions.LatitudeLongitudeToInTile01(center, tileId);
            var newLatLng = Conversions.Tile01ToLatitudeLongitude(zeroOne, tileId);
            Assert.AreEqual(center.ToString(), newLatLng.ToString());
        }
        
        [Test]
        public void StringToLatLngToMercatorToLatLng()
        {
            var str = "-77.0295,38.9165";
            var latlng = Conversions.StringToLatLon(str);
            var mercator = Conversions.LatitudeLongitudeToWebMercator(latlng);
            var newLatlng = Conversions.WebMercatorToLatLon(mercator);
            Assert.AreEqual(latlng.Latitude, newLatlng.Latitude, 0.001d);
            Assert.AreEqual(latlng.Longitude, newLatlng.Longitude, 0.001d);
        }
        
        [Test]
        public void Test_TileEdgeSizeInMercator()
        {
            var testTiles = new[]
            {
                new CanonicalTileId(10, 0, 0),
                new CanonicalTileId(10, 512, 512),
                new CanonicalTileId(10, 1023, 1023),

                new CanonicalTileId(11, 0, 0),
                new CanonicalTileId(11, 900, 400),
                new CanonicalTileId(11, 2047, 2047),

                new CanonicalTileId(12, 0, 0),
                new CanonicalTileId(12, 1500, 1200),
                new CanonicalTileId(12, 4095, 4095),

                new CanonicalTileId(13, 0, 0),
                new CanonicalTileId(13, 4000, 2000),
                new CanonicalTileId(13, 8191, 8191),

                new CanonicalTileId(14, 0, 0),
                new CanonicalTileId(14, 10000, 8000),
                new CanonicalTileId(14, 16383, 16383),

                new CanonicalTileId(15, 0, 0),
                new CanonicalTileId(15, 20000, 15000),
                new CanonicalTileId(15, 32767, 32767),

                new CanonicalTileId(16, 0, 0),
                new CanonicalTileId(16, 40000, 30000),
                new CanonicalTileId(16, 65535, 65535),
            };


            const float epsilon = 1e-4f;
            bool allMatch = true;

            foreach (var tile in testTiles)
            {
                float a = Conversions.CalculateTileEdgeSizeInMercator(tile);
                float b = Conversions.TileEdgeSizeInMercator(tile);

                if (Mathf.Abs(a - b) > epsilon)
                {
                    allMatch = false;
                    Debug.LogError($"Mismatch at Z={tile.Z}, X={tile.X}, Y={tile.Y} → A={a}, B={b}");
                }
            }
        }

    }
}