using System.Collections.Generic;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using UnityEngine;
using TerrainData = Mapbox.BaseModule.Data.DataFetchers.TerrainData;

namespace Mapbox.UnityMapService.DataSources
{
    public class TerrainSource : ImageSource<TerrainData>
    {
        protected ImageSourceSettings _settings;
        private IElevationDataExtractionStrategy _elevationDataExtractionStrategy;
        
        public TerrainSource(DataFetchingManager dataFetchingManager, MapboxCacheManager mapboxCacheManager, ImageSourceSettings settings) 
            : base(dataFetchingManager, mapboxCacheManager, settings)
        {
            _settings = settings;
            _elevationDataExtractionStrategy = SystemInfo.supportsAsyncGPUReadback
                ? new AsyncExtractElevationArray()
                : new SyncExtractElevationArray();
        }
        
        public override void DownloadAndCacheBaseTiles()
        {
            var backgroundTiles = new HashSet<CanonicalTileId>();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    backgroundTiles.Add(new CanonicalTileId(2, i, j));
                }
            }

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    backgroundTiles.Add(new CanonicalTileId(1, i, j));
                }
            }

            backgroundTiles.Add(new CanonicalTileId(0, 0, 0));

            foreach (var tileId in backgroundTiles)
            {
                BackgroundLoad(tileId, _tilesetId);
            }
        }
        
        protected override RasterTile CreateTile(CanonicalTileId tileId, string tilesetId)
        {
            RasterTile rasterTile;

            // //TODO fix this obviously
            if (tilesetId == "mapbox.mapbox-terrain-dem-v1")
            {
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    rasterTile = new DemRasterTile(tileId, tilesetId, true);
                }
                else
                {
                    rasterTile = new DemRasterTile(tileId, tilesetId, false);
                }

            }
            else
            {
                if (SystemInfo.supportsAsyncGPUReadback)
                {
                    rasterTile = new RawPngRasterTile(tileId, tilesetId, true);
                }
                else
                {
                    rasterTile = new RawPngRasterTile(tileId, tilesetId, false);
                }
            }

            return rasterTile;
        }

        protected override TerrainData CreateRasterDataWrapper(RasterTile tile)
        {
            TerrainData rasterData = new TerrainData()
            {
                TileId = tile.Id,
                TilesetId = tile.TilesetId,
                Texture = tile.Texture2D,
                CacheType = tile.FromCache,
                Data = tile.Data,
                ETag = tile.ETag,
                ExpirationDate = tile.ExpirationDate
            };

            return rasterData;
        }

        protected override void TextureReceivedFromFile(TerrainData cacheItem)
        {
            base.TextureReceivedFromFile(cacheItem);
            _elevationDataExtractionStrategy.ExtractHeightData(cacheItem);
        }

        protected override TerrainData TextureReceivedFromWeb(RasterTile tile)
        {
            var cacheItem = base.TextureReceivedFromWeb(tile);

            if (cacheItem != null)
            {
                _elevationDataExtractionStrategy.ExtractHeightData(cacheItem);
            }

            return cacheItem;
        }
    }
}