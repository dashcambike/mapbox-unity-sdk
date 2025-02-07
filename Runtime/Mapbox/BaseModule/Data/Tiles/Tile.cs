//-----------------------------------------------------------------------
// <copyright file="Tile.cs" company="Mapbox">
//     Copyright (c) 2016 Mapbox. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Platform.Cache;
using UnityEngine;

namespace Mapbox.BaseModule.Data.Tiles
{
	public enum CacheType
	{
		MemoryCache,
		FileCache,
		SqliteCache,
		NoCache,
		NoCacheUpdated
	}

	public enum TileState
	{
		New,/// <summary> New tile, not yet initialized. </summary>
		Loading,/// <summary> Loading data. </summary>
		Loaded,/// <summary> Data loaded and parsed. </summary>
		Canceled,/// <summary> Data loading cancelled. </summary>
		Errored, /// <summary> Data loading errored. </summary>
		Updated,/// <summary> Data has been loaded before and got updated. </summary>
		Processing,
		Destroyed
	}

	/// <summary>
	///    A Map tile, a square with vector or raster data representing a geographic
	///    bounding box. More info <see href="https://en.wikipedia.org/wiki/Tiled_web_map">
	///    here </see>.
	/// </summary>
	[Serializable]
	public abstract class Tile : IAsyncRequest
	{
		private int _key = 0;
		public CanonicalTileId Id;
		public string TilesetId;
		
		public DateTime ExpirationDate;
		public string ETag;
		

		

		public long StatusCode;
		protected TileState TileState = TileState.New;
		protected string _generatedUrl; 
		protected IAsyncRequest _request;
		protected IWebRequest _webRequest;
		protected Action<DataFetchingResult> _callback;

		public CacheType FromCache = CacheType.NoCache;
		public int Key
		{
			get
			{
				if (_key == 0)
				{
					_key = Id.GenerateKey(TilesetId);
				}

				return _key;
			}
		}
		public Action Cancelled = () => { };
		protected List<string> _logs;

		protected Tile()
		{

		}

		protected Tile(CanonicalTileId tileId, string tilesetId)
		{
			TilesetId = tilesetId;
			Id = tileId;
#if DEBUG
			_logs = new List<string>();
			AddLog(string.Format("{0} - {1}", Time.unscaledTime, " tile created"));
#endif
		}

		


		/// <summary>
		///     Gets the current state. When fully loaded, you must
		///     check if the data actually arrived and if the tile
		///     is accusing any error.
		/// </summary>
		/// <value> The tile state. </value>
		public TileState CurrentTileState
		{
			get { return TileState; }
		}


		public HttpRequestType RequestType
		{
			get { return _request.RequestType; }
		}


		public bool IsCompleted
		{
			get { return TileState == TileState.Loaded; }
		}

		/// <summary>
		///     Initializes the <see cref="T:Mapbox.BaseModule.Data.Tiles.Tile"/> object. It will
		///     start a network request and fire the callback when completed.
		/// </summary>
		/// <param name="param"> Initialization parameters. </param>
		/// <param name="callback"> The completion callback. </param>
		protected void Initialize(Parameters param, Action<DataFetchingResult> callback)
		{
			Initialize(param.Fs, param.Id, param.TilesetId, callback);
		}

		public virtual void Initialize(IFileSource fileSource, CanonicalTileId canonicalTileId, string tilesetId, Action<DataFetchingResult> p)
		{
			AddLog(string.Format("{0} - {1}", Time.unscaledTime, " tile initialized"));
			TileState = TileState.Loading;
			_callback = p;
			TilesetId = tilesetId;

			_generatedUrl = MakeTileResource(tilesetId).GetUrl();
			DoTheRequest(fileSource);
		}

		protected internal abstract void DoTheRequest(IFileSource fileSource);

		/// <summary>
		///     Returns a <see cref="T:System.String"/> that represents the current
		///     <see cref="T:Mapbox.BaseModule.Data.Tiles.Tile"/>.
		/// </summary>
		/// <returns>
		///     A <see cref="T:System.String"/> that represents the current
		///     <see cref="T:Mapbox.BaseModule.Data.Tiles.Tile"/>.
		/// </returns>
		public override string ToString()
		{
			return Id.ToString();
		}


		/// <summary>
		///     Cancels the request for the <see cref="T:Mapbox.BaseModule.Data.Tiles.Tile"/> object.
		///     It will stop a network request and set the tile's state to Canceled.
		/// </summary>
		/// <example>
		/// <code>
		/// // Do not request tiles that we are already requesting
		///	// but at the same time exclude the ones we don't need
		///	// anymore, cancelling the network request.
		///	tiles.RemoveWhere((T tile) =>
		///	{
		///		if (cover.Remove(tile.Id))
		///		{
		///			return false;
		///		}
		///		else
		///		{
		///			tile.Cancel();
		///			NotifyNext(tile);
		///			return true;
		/// 	}
		///	});
		/// </code>
		/// </example>
		public virtual void Cancel()
		{
			if (_request != null)
			{
				_request.Cancel();
				_request = null;
			}

			if (_webRequest != null)
			{
				_webRequest.Abort();
				_webRequest = null;
			}

			TileState = TileState.Canceled;
			AddLog(string.Format("{0} - {1}", Time.unscaledTime, " state cancelled with cancel call"));
			Cancelled();
		}

		// Get the tile resource (raster/vector/etc).
		protected abstract TileResource MakeTileResource(string tilesetId);

		// TODO: Currently the tile decoding is done on the main thread. We must implement
		// a Worker class to abstract this, so on platforms that support threads (like Unity
		// on the desktop, Android, etc) we can use worker threads and when building for
		// the browser, we keep it single-threaded.

		// private abstract void HandleTileResponse(Response response)
		// {
		// 	if (response.HasError)
		// 	{
		// 		response.Exceptions.ToList().ForEach(e => AddException(e));
		// 	}
		// 	else
		// 	{
		// 		// only try to parse if request was successful
		//
		// 		// current implementation doesn't need to check if parsing is successful:
		// 		// * Mapbox.Map.VectorTile.ParseTileData() already adds any exception to the list
		// 		// * Mapbox.Map.RasterTile.ParseTileData() doesn't do any parsing
		//
		// 		//ParseTileData(response.Data);
		// 	}
		//
		// 	// Cancelled is not the same as loaded!
		// 	if (TileState != TileState.Canceled)
		// 	{
		// 		if (response.IsUpdate)
		// 		{
		// 			TileState = TileState.Updated;
		// 		}
		// 		else
		// 		{
		// 			TileState = TileState.Loaded;
		// 		}
		// 	}
		//
		// 	_callback();
		// }

		public virtual void Clear()
		{
			if (_request != null)
			{
				_request.Cancel();
				_request = null;
			}

			if (_webRequest != null)
			{
				_webRequest.Abort();
				_webRequest = null;
			}
		}

		/// <summary>
		///    Parameters for initializing a Tile object.
		/// </summary>
		/// <example>
		/// <code>
		/// var parameters = new Tile.Parameters();
		/// parameters.Fs = MapboxAccess.Instance;
		/// parameters.Id = new CanonicalTileId(_zoom, _tileCoorindateX, _tileCoordinateY);
		/// parameters.TilesetId = "mapbox.mapbox-streets-v7";
		/// </code>
		/// </example>
		public struct Parameters
		{
			/// <summary> The tile id. </summary>
			public CanonicalTileId Id;

			/// <summary>
			///     The tileset ID, usually in the format "user.mapid". Exceptionally,
			///     <see cref="T:Mapbox.BaseModule.Data.Tiles.RasterTile"/> will take the full style URL
			///     from where the tile is composited from, like mapbox://styles/mapbox/streets-v9.
			/// </summary>
			public string TilesetId;

			/// <summary> The data source abstraction. </summary>
			public IFileSource Fs;
		}

		public virtual void Prune()
		{
			TileState = TileState.Destroyed;
		}
		
#region Logs
		public List<string> GetLogs => _logs;
		public void AddLog(string text)
		{
	#if DEBUG
			_logs.Add(text);
	#endif
		}

		public void AddLog(string text, CanonicalTileId relatedTileId)
		{
	#if DEBUG
			_logs.Add(string.Format("{0} - {1}", text, relatedTileId));
	#endif
		}
#endregion

	}
}