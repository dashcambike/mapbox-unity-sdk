using System;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using UnityEngine;

namespace Mapbox.VectorModule
{
    [Serializable]
    public class PointLayerVisualizer : VectorLayerVisualizer
    {
        public PointLayerVisualizer(string name, IMapInformation mapInformation, UnityContext unityContext = null, VectorLayerVisualizerSettings settings = null) : base(name, mapInformation, unityContext, settings)
        {
            
        }

        public override void UpdateForView(CanonicalTileId canonicalTileId, IMapInformation information)
        {
            if (_results.TryGetValue(canonicalTileId, out var visuals))
            {
                foreach (var entity in visuals)
                {
                    _mapInformation.PositionObjectFor(canonicalTileId, out var position, out var scale);
                    entity.GameObject.transform.localPosition = new Vector3(
                        position.x - _layerRootObject.transform.position.x, 
                        entity.GameObject.transform.localPosition.y, 
                        position.z - _layerRootObject.transform.position.z);
                }
            }
        }

        // public override void SetActive(CanonicalTileId canonicalTileId, bool isActive, IMapInformation mapInformation)
        // {
        //     if (_results.TryGetValue(canonicalTileId, out var visuals))
        //     {
        //         if (!isActive)
        //         {
        //             foreach (var entity in visuals)
        //             {
        //                 _stackList[entity.StackId].Finalize(entity);
        //             }
        //
        //             visuals.Clear();
        //             _results.Remove(canonicalTileId);
        //         }
        //         else
        //         {
        //             foreach (var entity in visuals)
        //             {
        //                 if (_stackList.TryGetValue(entity.StackId, out var stack))
        //                 {
        //                     var isVisible = isActive && stack.IsZinSupportedRange(_mapInformation.AbsoluteZoom);
        //                     entity.GameObject.SetActive(isVisible);
        //                 }
        //             }
        //         }
        //     }
        // }
    }
}