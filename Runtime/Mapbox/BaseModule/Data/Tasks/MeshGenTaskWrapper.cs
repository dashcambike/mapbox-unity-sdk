using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mapbox.BaseModule.Data.Tasks
{
    public class MeshGenTaskWrapper : TaskWrapper
    {
        public MeshGenTaskWrapperResult DataResult;
        public Func<MeshGenTaskWrapperResult> DataAction;
        public Action<Task, MeshGenTaskWrapperResult> DataContinueWith;

        public override void Action()
        {
            DataResult = DataAction();
        }

        public override void Completed(Task task)
        {
            if (task != null && !task.IsFaulted)
            {
                DataContinueWith(Task.CompletedTask, DataResult);
                return;
            }
            
            if (task == null)
            {
                DataResult ??= new MeshGenTaskWrapperResult();
                DataResult.ResultType = TaskResultType.Cancelled;
                DataContinueWith(null, DataResult);
            }
            else
            {
                DataResult ??= new MeshGenTaskWrapperResult();
                DataResult.ResultType = TaskResultType.MeshGenerationFailure;
                DataContinueWith(task, DataResult);
            }
        }
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