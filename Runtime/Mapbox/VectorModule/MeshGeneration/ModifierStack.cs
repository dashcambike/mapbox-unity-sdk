using System;
using System.Collections.Generic;
using Mapbox.BaseModule.Data;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Utilities;
using Mapbox.VectorModule.MeshGeneration.GameObjectModifiers;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;

namespace Mapbox.VectorModule.MeshGeneration
{
    public interface IModifierStack
    {
        MeshData RunMeshModifiers(VectorFeatureUnity feature, MeshData meshData, IMapInformation mapInfo);
        void RunGoModifiers(VectorEntity entity, IMapInformation mapInformation);
    }

    [Serializable]
    public class ModifierStack : IModifierStack
    {
        public VectorFilterStack Filters { get; private set; }
        public List<IMeshModifier> MeshModifiers;
        public List<IGameObjectModifier> GoModifiers;

        public ModifierStack(VectorFilterStack filters = null)
        {
            Filters = filters;
            MeshModifiers = new List<IMeshModifier>();
            GoModifiers = new List<IGameObjectModifier>();
        }
        
        public void Initialize()
        {
            if (Filters != null && Filters.Filters != null)
            {
                foreach (var filter in Filters.Filters)
                {
                    filter.Initialize();
                }
            }
        }

        public MeshData RunMeshModifiers(VectorFeatureUnity feature, MeshData meshData, IMapInformation mapInfo)
        {
            var counter = MeshModifiers.Count;
            for (int i = 0; i < counter; i++)
            {
                if (MeshModifiers[i] != null)
                {
                    MeshModifiers[i].Run(feature, meshData, mapInfo);
                }
            }
            return meshData;
        }

        public void RunGoModifiers(VectorEntity entity, IMapInformation mapInformation)
        {
            var counter = GoModifiers.Count;
            for (int i = 0; i < counter; i++)
            {
                GoModifiers[i].Run(entity, mapInformation);
            }
        }

        public void UnregisterTile(CanonicalTileId tileId)
        {
            foreach (var goModifier in GoModifiers)
            {
                goModifier.Unregister(tileId);
            }
        }
    }
}