using System;
using Mapbox.BaseModule.Data.Tasks;
using UnityEngine;

namespace Mapbox.BaseModule.Unity
{
    [Serializable]
    public class UnityContext
    {
        public TaskManager TaskManager;
        [NonSerialized] public MonoBehaviour CoroutineStarter;
        [Tooltip("Root object to hold all map related game objects")]
        public Transform MapRoot;
        [Tooltip("Root object for all tile objects which created the base map")]
        public Transform BaseTileRoot;
        [Tooltip("Root object for all runtime generated visuals. Mainly the vector feature visuals.")]
        public Transform RuntimeGenerationRoot;

        public UnityContext()
        {
            //TaskManager = new TaskManager();
        }
        
        public void Initialize(TaskManager providedTaskManager = null)
        {
            if (TaskManager == null)
            {
                TaskManager = providedTaskManager ?? new TaskManager();
            }
        
            TaskManager.Initialize();

            BaseTileRoot = BaseTileRoot == null ? new GameObject("BaseTiles").transform : BaseTileRoot;
            BaseTileRoot.SetParent(MapRoot);
            BaseTileRoot.transform.localPosition = Vector3.zero;

            RuntimeGenerationRoot = RuntimeGenerationRoot == null ? new GameObject("RuntimeObjectsRoot").transform : RuntimeGenerationRoot;
            RuntimeGenerationRoot.SetParent(MapRoot);
            RuntimeGenerationRoot.transform.localPosition = Vector3.zero;
        }

        public void OnDestroy()
        {
            TaskManager.OnDestroy();
        }
    }
}