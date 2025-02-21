using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Mapbox.BaseModule;
using Mapbox.BaseModule.Data.Interfaces;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using Mapbox.ImageModule;
using UnityEngine;

namespace Mapbox.Example.Scripts.ModuleBehaviours
{
	public class StaticApiLayerModuleScript : ModuleConstructorScript
	{
		public StaticLayerModuleSettings Settings = new StaticLayerModuleSettings()
		{
			DataSettings = new ImageSourceSettings()
			{
				RejectTilesBelowZoom = 5,
				ClampTilesToMax = 25
			}
		};
		public override ILayerModule ModuleImplementation { get; protected set; }

		private void Start()
		{
			
		}

		public override ILayerModule ConstructModule(MapService service, IMapInformation mapInformation,
			UnityContext unityContext)
		{
			if (Settings.SourceType == ImagerySourceType.None)
			{

			}
			else if (Settings.SourceType == ImagerySourceType.Custom)
			{
				Settings.DataSettings.TilesetId = Settings.CustomSourceId;
			}
			else
			{
				var imageryTileset = MapboxDefaultImagery.GetParameters(Settings.SourceType);
				Settings.DataSettings.TilesetId = imageryTileset.Id;
			}

			ModuleImplementation = new StaticApiLayerModule(service.GetStaticRasterSource(Settings.DataSettings), Settings);
			return ModuleImplementation;
		}
	}
}