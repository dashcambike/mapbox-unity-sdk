using System;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.UnityMapService
{
    public class SyncExtractElevationArray : IElevationDataExtractionStrategy
    {
        public void ExtractHeightData(Texture2D texture, Action<float[]> callback)
        {
            byte[] rgbData = texture.GetRawTextureData();
            var width = texture.width;
            float[] heightData = new float[width * width];

            for (float y = 0; y < width; y++)
            {
                for (float x = 0; x < width; x++)
                {
                    var xx = (x / width) * width;
                    var yy = (y / width) * width;
                    var index = ((int) yy * width) + (int) xx;

                    float r = rgbData[index * 4 + 1];
                    float g = rgbData[index * 4 + 2];
                    float b = rgbData[index * 4 + 3];
                    //var color = rgbData[index];
                    // float r = color.g;
                    // float g = color.b;
                    // float b = color.a;
                    //the formula below is the same as Conversions.GetAbsoluteHeightFromColor but it's inlined for performance
                    heightData[(int) (y * width + x)] = (-10000f + ((r * 65536f + g * 256f + b) * 0.1f));
                    //678 ==> 012345678
                    //345
                    //012
                }
            }
            callback?.Invoke(heightData);
        }
        
        public void ExtractHeightData(TerrainData terrainData)
        {
            ExtractHeightData(terrainData.Texture, terrainData);
        }
        
        public void ExtractHeightData(Texture2D texture, TerrainData terrainData)
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            byte[] rgbData = texture.GetRawTextureData();
            var width = texture.width;
            float[] heightData = new float[width * width];

            for (float y = 0; y < width; y++)
            {
                for (float x = 0; x < width; x++)
                {
                    var xx = (x / width) * width;
                    var yy = (y / width) * width;
                    var index = ((int) yy * width) + (int) xx;

                    float r = rgbData[index * 4 + 1];
                    float g = rgbData[index * 4 + 2];
                    float b = rgbData[index * 4 + 3];
                    //var color = rgbData[index];
                    // float r = color.g;
                    // float g = color.b;
                    // float b = color.a;
                    //the formula below is the same as Conversions.GetAbsoluteHeightFromColor but it's inlined for performance
                    var value = (-10000f + ((r * 65536f + g * 256f + b) * 0.1f));
                    heightData[(int)(y * width + x)] = value;
                    if(value < min) min = value;
                    if(value > max) max = value;
                    //678 ==> 012345678
                    //345
                    //012
                }
            }
            terrainData.SetElevationValues(heightData, min, max);
        }
    }
}