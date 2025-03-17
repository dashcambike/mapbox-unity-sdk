using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Mapbox.BaseModule.Utilities
{
	[RequireComponent(typeof(Toggle))]
	public class TelemetryConfigurationButton : MonoBehaviour
	{
		private List<MapBehaviourCore> _core;
		private List<MapService> _mapServices;

		private Toggle _toggle;
		private bool _isItEnabled = true;

		protected void Awake()
		{
			_toggle = GetComponent<Toggle>();
			_mapServices = new List<MapService>();
			var cores = FindObjectsOfType<MapBehaviourCore>().ToList();
			foreach (var core in cores)
			{
				if (core.InitializationStatus < InitializationStatus.Initialized)
				{
					core.Initialized += map =>
					{
						_mapServices.Add(map.MapService);
						_isItEnabled &= map.MapService.GetTelemetryCollectionState();
						_toggle.isOn = _isItEnabled;
					};
				}
				else
				{
					_mapServices.Add(core.MapboxMap.MapService);
					_isItEnabled &= core.MapboxMap.MapService.GetTelemetryCollectionState();
					_toggle.isOn = _isItEnabled;
				}
			}
			
			_toggle.onValueChanged.AddListener(SetPlayerPref);
		}

		void SetPlayerPref(bool state)
		{
			foreach (var mapService in _mapServices)
			{
				var success = mapService.SetTelemetryCollectionState(state);
				if (!success)
				{
					Debug.Log("Couldn't save settings file");
				}
				else
				{
					_isItEnabled = state;
				}
			}
		}
	}
}
