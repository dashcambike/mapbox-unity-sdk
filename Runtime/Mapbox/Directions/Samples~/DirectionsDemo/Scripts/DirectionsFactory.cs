using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Data.Platform.Cache;
using Mapbox.BaseModule.Data.Vector2d;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.Directions;
using Mapbox.Example.Scripts.Map;
using UnityEngine;

namespace Samples.Directions
{
	public class DirectionsFactory : MonoBehaviour
	{
		public Action ArrangingWaypointsStarted = () => { };
		public Action QuerySent = () => { };
		public Action<Vector3[]> ArrangingWaypoints = (positions) => { };
		public Action ArrangingWaypointsFinished = () => { };
		public Action<Vector3, float> RouteDrawn = (midPoint, TotalLength) => { };
		public RoutingProfile RoutingProfile = RoutingProfile.Driving;

		[SerializeField] float RoadSizeMultiplier = 1;
		[SerializeField] private AnimationCurve RoadSizeCurve;
		[SerializeField] MapboxMapBehaviour _map;
		[SerializeField] private LineRenderer _lineRenderer;
		//[SerializeField] private LoftModifier _loftModifier;
		[SerializeField] Material _material;
		[SerializeField] Transform _waypointsParent;

		Transform[] _waypoints;

		private List<Vector3> _cachedWaypoints;
		private Mapbox.Directions.Directions _directions;
		private int _counter;
		private bool _isDragging = false;
		private Vector3[] _pointArray;
		private Vector3 _pointUpDelta = new Vector3(0, 3, 0);
		GameObject _directionsGO;
		private bool _recalculateNext;

		protected virtual void Awake()
		{
			if (_map == null)
			{
				_map = FindObjectOfType<MapboxMapBehaviour>();
			}

			var mapboxContext = new MapboxContext();
			_directions = new Mapbox.Directions.Directions(new ResilientWebRequestFileSource(mapboxContext.GetAccessToken(), mapboxContext.GetSkuToken));
			// _map.OnInitialized += Query;
			// _map.OnUpdated += Query;

			_waypoints = new Transform[_waypointsParent.childCount];
			for (int i = 0; i < _waypointsParent.childCount; i++)
			{
				_waypoints[i] = _waypointsParent.GetChild(i);
			}

			_pointArray = new Vector3[_waypoints.Length];

			foreach (var wp in GetComponentsInChildren<DragableDirectionWaypoint>())
			{
				wp.MouseDown += () =>
				{
					ArrangingWaypointsStarted();
					_lineRenderer.gameObject.SetActive(true);
					_directionsGO.SetActive(false);
					_isDragging = true;
				};

				wp.MouseDraging += () =>
				{
					_lineRenderer.positionCount = _waypoints.Length;
					for (int i = 0; i < _waypoints.Length; i++)
					{
						_pointArray[i] = _waypoints[i].position + _pointUpDelta;
					}

					_lineRenderer.SetPositions(_pointArray);
					ArrangingWaypoints(_pointArray);
				};
				wp.MouseDrop += () =>
				{
					ArrangingWaypointsFinished();
					_lineRenderer.gameObject.SetActive(false);
					_isDragging = false;
					Query();
				};
			}
		}

		public void Start()
		{
			_cachedWaypoints = new List<Vector3>(_waypoints.Length);
			foreach (var item in _waypoints)
			{
				_cachedWaypoints.Add(item.position);
			}

			_recalculateNext = false;
			//_loftModifier.Initialize();
		}

		protected virtual void OnDestroy()
		{
			// _map.OnInitialized -= Query;
			// _map.OnUpdated -= Query;
		}

		[ContextMenu("Query")]
		public void Query()
		{
			var count = _waypoints.Length;
			var wp = new LatitudeLongitude[count];
			for (int i = 0; i < count; i++)
			{
				wp[i] = _waypoints[i].GetGeoPosition(_map.MapInformation.CenterMercator, _map.MapInformation.Scale);
			}

			var directionResource = new DirectionResource(wp, RoutingProfile);
			directionResource.Steps = true;
			_directions.Query(directionResource, HandleDirectionsResponse);
			QuerySent();
		}

		void HandleDirectionsResponse(DirectionsResponse response)
		{
			Debug.Log(response);
			if (response == null || null == response.Routes || response.Routes.Count < 1)
			{
				return;
			}

			var meshData = new MeshData();
			var unitySpacePositions = new List<Vector3>();

			var totalLength = 0f;
			Vector3 prevPoint = Vector3.zero;
			foreach (var point in response.Routes[0].Geometry)
			{
				var newPoint = _map.MapInformation.ConvertLatLngToPosition(new LatitudeLongitude(point.y, point.x));
				unitySpacePositions.Add(newPoint);

				if (prevPoint != Vector3.zero)
				{
					totalLength += Vector3.Distance(prevPoint, newPoint);
				}

				prevPoint = newPoint;
			}

			var midLength = totalLength / 2;

			if (_waypoints.Length > 0 && unitySpacePositions.Count > 0)
			{
				_waypoints[0].transform.position = unitySpacePositions[0];
				_waypoints[_waypoints.Length - 1].transform.position = unitySpacePositions[unitySpacePositions.Count - 1];
			}

			var feat = new VectorFeatureUnity();
			feat.Points.Add(unitySpacePositions);

			// _loftModifier.SliceScaleMultiplier = RoadSizeCurve.Evaluate(_map.MapInformation.Zoom) * RoadSizeMultiplier;
			// _loftModifier.Run(feat, meshData, _map.MapInformation.Scale);

			CreateGameObject(meshData);
			_directionsGO.SetActive(true);


			var midPoint = unitySpacePositions[0];
			for (int i = 1; i < unitySpacePositions.Count; i++)
			{
				var dist = (unitySpacePositions[i] - unitySpacePositions[i - 1]).magnitude;
				if (midLength > dist)
				{
					midLength -= dist;
				}
				else
				{
					midPoint = Vector3.Lerp(unitySpacePositions[i - 1], unitySpacePositions[i], (float) midLength / dist);
					break;
				}
			}

			RouteDrawn(midPoint, totalLength / _map.MapInformation.Scale);
		}

		GameObject CreateGameObject(MeshData data)
		{
			if (_directionsGO != null)
			{
				Destroy(_directionsGO);
			}

			_directionsGO = new GameObject("direction waypoint " + " entity");
			if (_map != null)
			{
				_directionsGO.transform.SetParent(_map.transform);
			}

			var mesh = _directionsGO.AddComponent<MeshFilter>().mesh;
			mesh.subMeshCount = data.Triangles.Count;

			mesh.SetVertices(data.Vertices);
			_counter = data.Triangles.Count;
			for (int i = 0; i < _counter; i++)
			{
				var triangle = data.Triangles[i];
				mesh.SetTriangles(triangle, i);
			}

			_counter = data.UV.Count;
			for (int i = 0; i < _counter; i++)
			{
				var uv = data.UV[i];
				mesh.SetUVs(i, uv);
			}

			mesh.RecalculateNormals();
			_directionsGO.AddComponent<MeshRenderer>().material = _material;
			return _directionsGO;
		}

		public void ChangeRoutingProfile(RoutingProfile profile, bool forceQuery = true)
		{
			RoutingProfile = profile;
			if (forceQuery)
			{
				Query();
			}
		}
	}
}