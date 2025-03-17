using System;
using System.Collections.Generic;

namespace Mapbox.BaseModule.Data.Tasks
{
    public class MeshGenTaskWrapper : TaskWrapper
    {
        public Func<MeshGenTaskWrapperResult> MeshGen; 
        public Action<MeshGenTaskWrapperResult> ContinueMeshWith;
    }
    
    public class MeshGenTaskWrapperResult : TaskResult
    {
        public TaskResultType ResultType;
        public Dictionary<string, Dictionary<int, HashSet<MeshData>>> Data;
		
		
        public MeshGenTaskWrapperResult()
        {
            Data = new Dictionary<string, Dictionary<int, HashSet<MeshData>>>();
        }
    }
	
    public enum TaskResultType
    {
        DataProcessingFailure,
        MeshGenerationFailure,
        GameObjectFailure,
        Success,
        Cancelled
    }
}