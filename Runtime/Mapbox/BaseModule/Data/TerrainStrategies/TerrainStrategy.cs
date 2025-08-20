using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.ImageModule.Terrain.Settings;
using UnityEngine;

namespace Mapbox.ImageModule.Terrain.TerrainStrategies
{
	public class TerrainStrategy
	{
		public virtual int RequiredVertexCount
		{
			get { return 0; }
		}

		public virtual void Initialize(ElevationLayerProperties elOptions)
		{
			
		}


		public virtual void RegisterTile(UnityMapTile tile, bool createElevatedMesh)
		{

		}
	}
}
