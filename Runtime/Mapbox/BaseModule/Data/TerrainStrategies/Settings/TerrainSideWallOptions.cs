using System;
using UnityEngine;

namespace Mapbox.ImageModule.Terrain.Settings
{
	[Serializable]
	public class TerrainSideWallOptions
	{
		[Tooltip("Adds side walls to terrain meshes, reduces visual artifacts.")]
		public bool isActive = false;
		[Tooltip("Height of side walls.")]
		public float wallHeight = 10;
	}
}
