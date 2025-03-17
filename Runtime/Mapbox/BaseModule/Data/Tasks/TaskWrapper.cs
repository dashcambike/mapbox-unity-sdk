using System;
using System.Threading;
using System.Threading.Tasks;
using Mapbox.BaseModule.Data.Tiles;

namespace Mapbox.BaseModule.Data.Tasks
{
    public class TaskWrapper
    {
        public string TilesetId;
        public float EnqueueFrame;
        public float StartingTime;
        public float FinishedTime;
        public CanonicalTileId TileId;
        public CanonicalTileId OwnerTileId;
        public Action Action;
        public Action<Task> ContinueWith;
        public Action OnCancelled = () => {};
        public string Info;
        
        private bool _isCanceled = false;
        public bool IsCancelled { get { return _isCanceled; } }

        public void Cancel()
        {
            _isCanceled = true;
        }
        
        
    }
}