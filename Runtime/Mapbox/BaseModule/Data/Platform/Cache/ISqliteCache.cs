
using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform.Cache.SQLiteCache;
using Mapbox.BaseModule.Data.Tiles;

namespace Mapbox.BaseModule.Data.Platform.Cache
{
	public interface ISqliteCache
	{
		event Action<string> DataPruned;
		
		void ReadySqliteDatabase();
		bool IsUpToDate();
		
		uint MaxCacheSize { get; }
		long TileCount(string tilesetName);
		
		void Add(MapboxTileData item, bool replaceIfExists);
		void SyncAdd(string tilesetName, CanonicalTileId tileId, byte[] data, string path, string etag, DateTime? expirationDate, bool forceInsert);
		T Get<T>(string tilesetId, CanonicalTileId tileId) where T : MapboxTileData, new();
		void ReadEtagAndExpiration<T>(T data) where T : MapboxTileData;
		void UpdateExpiration(string tilesetId, CanonicalTileId tileId, DateTime date);
		int RemoveData(string tilesetId, int zoom, int i, int i1);
		List<tiles> GetAllTiles();
		
		bool ClearDatabase();
		int Clear(string tilesetId);
		bool DeleteSqliteFile();
	}
}
