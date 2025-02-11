using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public static class MapInformationStaticMethods
    {
        public static LatitudeLongitude ConvertPositionToLatLng(this IMapInformation mapInfo, Vector3 position)
        {
            return ConvertPositionToLatLngForScale(mapInfo, position, mapInfo.Scale);
        }
        
        public static LatitudeLongitude ConvertPositionToLatLngForScale(this IMapInformation mapInfo, Vector3 position, float scale)
        {
            var unscaledPos = position * scale;
            var currentMercator = mapInfo.CenterMercator;
            var mercator = new Vector2d(currentMercator.x + unscaledPos.x, currentMercator.y + unscaledPos.z);
            return Conversions.WebMercatorToLatLon(mercator);
        }
        
        public static Vector3 ConvertLatLngToPositionForScale(this IMapInformation mapInfo, LatitudeLongitude latlng, float scale)
        {
            var mercator = Conversions.LatitudeLongitudeToWebMercator(latlng);
            var deltaMercator = mercator - mapInfo.CenterMercator;
            var scaledDeltaMercator = deltaMercator / scale;
            var scaledDeltaMercatorVector3 = scaledDeltaMercator.ToVector3xz();
            
            return scaledDeltaMercatorVector3;
        }
        
        public static Vector3 ConvertLatLngToPosition(this IMapInformation mapInfo, LatitudeLongitude latlng)
        {
            var mercator = Conversions.LatitudeLongitudeToWebMercator(latlng);
            var deltaMercator = mercator - mapInfo.CenterMercator;
            var scaledDeltaMercator = deltaMercator / mapInfo.Scale;
            var scaledDeltaMercatorVector3 = scaledDeltaMercator.ToVector3xz();
            
            return scaledDeltaMercatorVector3;
        }
        
        public static void PositionObjectFor(this IMapInformation mapInfo, GameObject go, CanonicalTileId tileId)
        {
            var topLeftValues = Conversions.TileTopLeftInUnitySpace(tileId, mapInfo.CenterMercator, mapInfo.Scale);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, mapInfo.Scale);
            go.transform.localPosition = topLeftValues;
            go.transform.localScale = Vector3.one * tileSize;
        }
        
        public static void PositionObjectFor(this IMapInformation mapInfo, CanonicalTileId tileId, out Vector3 position, out Vector3 scale)
        {
            var topLeftValues = Conversions.TileTopLeftInUnitySpace(tileId, mapInfo.CenterMercator, mapInfo.Scale);
            var tileSize = Conversions.TileSizeInUnitySpace(tileId.Z, mapInfo.Scale);
            position = topLeftValues;
            scale = Vector3.one * tileSize;
        }
    }
}