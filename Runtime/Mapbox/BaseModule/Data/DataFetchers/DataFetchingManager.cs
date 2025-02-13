using System;
using System.Collections;
using System.Collections.Generic;
using Mapbox.BaseModule.Data.Platform;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Platform.TileJSON;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Data.DataFetchers
{
	public class DataFetchingManager : IFileSource
	{
		protected Action<FetchInfo> TileInitialized = (t)=> {};
		private const float _requestDelay = 0.2f;

		protected IFileSource _fileSource;
		protected Queue<int> _tileOrder;
		protected Dictionary<int, FetchInfo> _tileFetchInfos;
		protected Dictionary<int, Tile> _globalActiveRequests;
		protected int _activeRequestLimit = 30;
		private bool _isDestroying = false;

		public DataFetchingManager(string getAccessToken, Func<string> getSkuToken)
		{
			_fileSource = new ResilientWebRequestFileSource(getAccessToken, getSkuToken);
			_tileOrder = new Queue<int>();
			_tileFetchInfos = new Dictionary<int, FetchInfo>();
			_globalActiveRequests = new Dictionary<int, Tile>();
			Runnable.Run(UpdateTick());
		}

		public virtual void EnqueueForFetching(FetchInfo info)
		{
			var key = info.Tile.Id.GenerateKey(info.Tile.TilesetId);
			
			if (!_tileFetchInfos.ContainsKey(key))
			{
				info.Callback += (result) =>
				{
					if (!_isDestroying)
					{
						_globalActiveRequests.Remove(key);
					}
				};

				info.Tile.AddLog(Time.frameCount + " Enqueued for fetching");
				_tileOrder.Enqueue(key);
				info.QueueTime = Time.time;
				_tileFetchInfos.Add(key, info);
			}
			else
			{
				//same requests is already in queue.
				//this probably means first one was supposed to be cancelled but for some reason has not.
				//ensure all data fetchers (including unorthodox ones like file data fetcher) handling
				//tile cancelling properly
#if DEPLOY_DEV || UNITY_EDITOR
				Debug.Log("tile request is already in queue. This most likely means first request was supposed to be cancelled but not. " + info.Tile.Id + " " + info.Tile.TilesetId);
#endif
			}
		}

		private IEnumerator UpdateTick()
		{
			while (!_isDestroying)
			{
				var fallbackCounter = 0;
				while (_tileOrder.Count > 0 &&
				       _globalActiveRequests.Count < _activeRequestLimit &&
				       fallbackCounter < _activeRequestLimit)
				{
					fallbackCounter++;
					var tileKey = _tileOrder.Peek(); //we just peek first as we might want to hold it until delay timer runs out
					if (!_tileFetchInfos.ContainsKey(tileKey))
					{
						_tileOrder.Dequeue(); //but we dequeue it if it's not in tileFetchInfos, which means it's cancelled
						continue;
					}

					if (QueueTimeHasMatured(_tileFetchInfos[tileKey].QueueTime, _requestDelay) || !Application.isPlaying)
					{
						tileKey = _tileOrder.Dequeue();
						var fi = _tileFetchInfos[tileKey];

						if (fi.Tile.CurrentTileState == TileState.Canceled)
							continue;

						_tileFetchInfos.Remove(tileKey);
						if (!_globalActiveRequests.ContainsKey(tileKey))
						{
							_globalActiveRequests.Add(tileKey, fi.Tile);
						}
						else
						{
							Debug.Log("here");
						}
						TileInitialized(fi);
						fi.Tile.Initialize(
							_fileSource,
							fi.Tile.Id,
							fi.Tile.TilesetId,
							(dataFetchingResult) =>
							{
								fi.Callback(dataFetchingResult);
							});
						yield return null;
					}
				}

				//Debug.Log("request count " + _tileFetchInfos.Count + " " + _globalActiveRequests.Count);
				yield return null;
			}
		}

		private static bool QueueTimeHasMatured(float queueTime, float maturationAge)
		{
			return Time.time - queueTime >= maturationAge;
		}

		public virtual void CancelFetching(Tile tile, string tilesetId)
		{
			var key = tile.Id.GenerateKey(tilesetId);

			//is this correct?
			if (_globalActiveRequests.ContainsKey(key))
			{
				_globalActiveRequests.Remove(key);
			}

			if (_tileFetchInfos.ContainsKey(key))
			{
				tile.AddLog(Time.frameCount + " CancelFetching executing");
				tile.Cancel();
				_tileFetchInfos[key].Callback(new DataFetchingResult()
				{
					State = WebResponseResult.Cancelled
				});
				_tileFetchInfos.Remove(key);
			}
		}
		
		public virtual TileJSON GetTileJSON(int timeout = 10)
		{
			return new TileJSON(_fileSource, timeout);
		}

		public void OnDestroy()
		{
			_isDestroying = true;
			foreach (var request in _globalActiveRequests)
			{
				request.Value.Cancel();
			}
			_globalActiveRequests.Clear();
			_globalActiveRequests = null;
			_tileFetchInfos.Clear();
			_tileFetchInfos = null;
			_tileOrder.Clear();
			_tileOrder = null;
			_fileSource.OnDestroy();
			_fileSource = null;
		}


		#region IFileSource interface for direct access without queue
		public IAsyncRequest Request(string uri, Action<Response> callback, int timeout = 10)
		{
			return _fileSource.Request(uri, callback, timeout);
		}

		public IWebRequest MapboxImageRequest(string uri, Action<WebRequestResponse> callback, string etag = "", int timeout = 10,
			bool isNonReadable = true)
		{
			return _fileSource.MapboxImageRequest(uri, callback, etag, timeout, isNonReadable);	
		}

		public IWebRequest CustomImageRequest(string uri, Action<WebRequestResponse> callback, string etag = null, int timeout = 10,
			bool isNonReadable = true)
		{
			return _fileSource.CustomImageRequest(uri, callback, etag, timeout, isNonReadable);
		}

		public IWebRequest MapboxDataRequest(string uri, Action<WebRequestResponse> callback, string etag = "", int timeout = 10)
		{
			return _fileSource.MapboxDataRequest(uri, callback, etag, timeout);
		}
		#endregion
	}
}
