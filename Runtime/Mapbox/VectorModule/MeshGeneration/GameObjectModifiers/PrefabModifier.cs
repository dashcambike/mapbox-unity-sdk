using System;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.MeshGeneration.GameObjectModifiers
{
	[Serializable]
	public class PrefabModifierSettings
	{
		public GameObject Prefab;
	}

	[Serializable]
	public class PrefabModifier : GameObjectModifier
	{
		public Action<GameObject> PrefabCreated = (s) => { };

		private UnityContext _unityContext;
		private Dictionary<VectorEntity, GameObject> _objects;
		private readonly PrefabModifierSettings _settings;
		
		public PrefabModifier(UnityContext unityContext, PrefabModifierSettings settings)
		{
			_unityContext = unityContext;
			_settings = settings;
			if (_objects == null)
			{
				_objects = new Dictionary<VectorEntity, GameObject>();
			}
		}
		
		public override void Run(VectorEntity ve, IMapInformation mapInformation)
		{
			var rectd = Conversions.TileBoundsInUnitySpace(ve.Feature.TileId, mapInformation.CenterMercator, mapInformation.Scale);
			var tileScale = (float) rectd.Size.x;
			int selpos = ve.Feature.Points[0].Count / 2;
			var met = ve.Feature.Points[0][selpos] * tileScale;
			
			GameObject go;
			
			if (_objects.ContainsKey(ve))
			{
				go = _objects[ve];
				go.name = ve.Feature.Data.Id.ToString();
				go.transform.localPosition = met;
				return;
			}
			
			go = GameObject.Instantiate(_settings.Prefab, ve.Transform);
			go.transform.localPosition = met;
			
			_objects.Add(ve, go);
			go.name = ve.Feature.Data.Id.ToString();
			

			PrefabCreated(go);
		}

		public override void Finalize(VectorEntity entity)
		{
			base.Finalize(entity);
			if (_objects.TryGetValue(entity, out var poiGo))
			{
				GameObject.Destroy(poiGo);
				_objects.Remove(entity);
			}
		}
	}
}
