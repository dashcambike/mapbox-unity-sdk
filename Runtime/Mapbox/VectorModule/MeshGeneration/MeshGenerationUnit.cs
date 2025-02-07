using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tasks;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Map;
using Mapbox.BaseModule.Unity;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.MeshGeneration
{
    public class MeshGenerationUnit
    {
        private bool _isActive = true;
        private UnityContext _unityContext;
        private Dictionary<CanonicalTileId, TaskWrapper> _activeTasks;
        private Dictionary<string, IVectorLayerVisualizer> _layerVisualizers;
        public MeshGenerationUnit(UnityContext unityContext, Dictionary<string, IVectorLayerVisualizer> layerVisualizers)
        {
            _unityContext = unityContext;
            _activeTasks = new Dictionary<CanonicalTileId, TaskWrapper>();
            _layerVisualizers = layerVisualizers;
        }

        public bool IsInWork(CanonicalTileId tileId) { return _activeTasks.ContainsKey(tileId); }
		
        public void MeshGeneration(VectorData data, Action<MeshGenerationTaskResult> callback)
        {
            if (data.Data == null)
            {
                callback(new MeshGenerationTaskResult(TaskResultType.Success));
            }

            var meshTask = new MeshGenTaskWrapper(data.TileId.GenerateKey(data.TilesetId, "VectorTile"))
            {
                OwnerTileId = data.TileId,
                TileId = data.TileId,
                MeshGen = () =>
                {
                    var result = new MeshGenTaskWrapperResult();
                    try
                    {
                        var decompressed = Compression.Decompress(data.Data);
                        data.VectorTileData = new Mapbox.VectorTile.VectorTile(decompressed);
                    }
                    catch (Exception e)
                    {
                        result.ResultType = TaskResultType.DataProcessingFailure;
                        result.AddException(e);
                        return result;
                    }

                    try
                    {
                        var layers = data.VectorTileData.LayerNames();
                        foreach (var layerName in layers)
                        {
                            if (_layerVisualizers.TryGetValue(layerName, out var layerVisualizer))
                            {
                                if(layerVisualizer.ContainsVisualFor(data.TileId))
                                    continue;
                                if (layerVisualizer.Active)
                                {
                                    result.Data.Add(layerName, layerVisualizer.CreateMesh(data.TileId, data.VectorTileData.GetLayer(layerName)));
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        result.ResultType = TaskResultType.MeshGenerationFailure;
                        result.AddException(e);
                        return result;
                    }

                    result.ResultType = TaskResultType.Success;
                    return result;
                },
                ContinueMeshWith = (taskResult) =>
                {
                    if (!_isActive)
                        return;
					
                    _activeTasks.Remove(data.TileId);
					
                    if (taskResult.ResultType == TaskResultType.MeshGenerationFailure)
                    {
                        var failResult = new MeshGenerationTaskResult(taskResult.ResultType);
                        foreach (var e in taskResult.GetExceptions())
                        {
                            failResult.AddException(e);
                        }
                        //Debug.Log(string.Format("{0} mesh gen exception: {1}", data.TileId, task.Exception.Message));
                        failResult.AddException(new Exception(string.Format("{0} mesh gen exception: {1}", data.TileId, taskResult.ExceptionsAsString)));
                        callback(failResult);
                        return;
                    }
					
                    var resultGameObjects = new List<GameObject>();
                    foreach (var layerName in data.VectorTileData.LayerNames())
                    {
                        if (!taskResult.Data.ContainsKey(layerName))
                            continue;

                        if (_layerVisualizers.TryGetValue(layerName, out var layerVisualizer))
                        {
                            var tileMeshData = taskResult.Data[layerName];
                            var layerGameObjects = layerVisualizer.CreateGo(data.TileId, tileMeshData);
                            foreach (var gameObject in layerGameObjects)
                            {
                                gameObject.SetActive(true);
                                resultGameObjects.Add(gameObject);
                            }
                        }
                    }
                    callback(new MeshGenerationTaskResult(TaskResultType.Success, resultGameObjects));
                    
                },
                OnCancelled = () =>
                {
                    _activeTasks.Remove(data.TileId);
                    var failResult = new MeshGenerationTaskResult(TaskResultType.Cancelled);
                    callback(failResult);
                },
                Info = "VectorTile.HandleTileResponse"
            };

            _activeTasks.Add(data.TileId, meshTask);
            _unityContext.TaskManager.AddTask(meshTask, 0);
        }

        public void Cancel(CanonicalTileId tileId)
        {
            if (_activeTasks.TryGetValue(tileId, out var task))
            {
                _unityContext.TaskManager.CancelTask(task);
            }
        }

        public void OnDestroy()
        {
            _isActive = false;
            foreach (var visualizer in _layerVisualizers)
            {
                visualizer.Value.OnDestroy();
            }
        }

        public void SetVisualActive(CanonicalTileId tileId, bool isRetained, IMapInformation mapInformation)
        {
            foreach (var visualizer in _layerVisualizers)
            {
                visualizer.Value.SetActive(tileId, isRetained, mapInformation);
            }
        }

        public IEnumerator Initialize()
        {
            foreach (var visualizer in _layerVisualizers.Values)
            {
                yield return visualizer.Initialize();
            }
        }

        public void UpdateForView(CanonicalTileId tileId, IMapInformation information)
        {
            foreach (var visualizer in _layerVisualizers)
            {
                visualizer.Value.UpdateForView(tileId, information);
            }
        }

        public void ClearDisposedDataVisual(CanonicalTileId tileId)
        {
            foreach (var visualizer in _layerVisualizers)
            {
                visualizer.Value.UnregisterTile(tileId);
            }
        }

        public void RetainTiles(HashSet<CanonicalTileId> retainedTiles)
        {
            if (_activeTasks.Count == 0)
                return;
            
            var toRemove = new List<KeyValuePair<CanonicalTileId, TaskWrapper>>();
            foreach (var task in _activeTasks)
            {
                if (!retainedTiles.Contains(task.Key))
                {
                    toRemove.Add(task);
                }
            }

            foreach (var pair in toRemove)
            {
                _activeTasks.Remove(pair.Key);
                _unityContext.TaskManager.CancelTask(pair.Value);
            }
        }

        public bool TryGetLayerVisualizer(string name, out IVectorLayerVisualizer visualizer)
        {
            return _layerVisualizers.TryGetValue(name, out visualizer);
        }
    }
}