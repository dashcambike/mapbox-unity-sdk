using System;
using System.Threading;
using System.Threading.Tasks;
using Mapbox.BaseModule.Data.Tiles;

namespace Mapbox.BaseModule.Data.Tasks
{
    public abstract class TaskWrapper
    {
        public float EnqueueFrame;
        public float StartingTime;
        public float FinishedTime;
        public CanonicalTileId TileId;
        
        public bool IsCancelled { get; private set; }
        public void Cancel() { IsCancelled = true; }

        public abstract void Action();
        public abstract void Completed(Task task);
    }
    
    public class DataTaskWrapper<T> : TaskWrapper
    {
        public T DataResult;
        public Func<T> DataAction;
        public Action<Task, T> DataCompleted;

        public override void Action()
        {
            DataResult = DataAction();
        }

        public override void Completed(Task task)
        {
            if (DataCompleted != null)
            {
                DataCompleted(task, DataResult);
            }
        }
    }
}