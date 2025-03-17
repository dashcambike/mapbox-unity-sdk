using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mapbox.BaseModule.Data.DataFetchers;
using Mapbox.BaseModule.Data.Tiles;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Data.Tasks
{
	public class TaskManager
	{
		public Action<TaskWrapper> TaskStarted = (t) => { };
		public Action<TaskWrapper> TaskCancelled = (t) => { };
		public int ActiveTaskLimit = 10;
		protected const float _requestDelay = 0.2f;
		
		protected HashSet<TaskWrapper> _runningTasks;
		protected List<Queue<TaskWrapper>> _taskQueue;
		private bool _isDestroying = false;
		private CancellationTokenSource _globalCancellationTokenSource;

		private static object _lock = new object();

		public TaskManager()
		{
			_runningTasks = new HashSet<TaskWrapper>();
			_globalCancellationTokenSource = new CancellationTokenSource();
			_taskQueue = new List<Queue<TaskWrapper>>()
			{
				new Queue<TaskWrapper>(),
				new Queue<TaskWrapper>(),
				new Queue<TaskWrapper>(),
				new Queue<TaskWrapper>(),
				new Queue<TaskWrapper>()
			};
		}

		public void Initialize()
		{
			//TODO remove runnable here?
			Runnable.Run(UpdateTaskManager());
		}

		private bool TaskQueueAny()
		{
			foreach (var queue in _taskQueue)
			{
				if (queue.Count != 0)
					return true;
			}

			return false;
		}

		private TaskWrapper TaskQueuePeek()
		{
			foreach (var queue in _taskQueue)
			{
				if (queue.Count != 0)
				{
					return queue.Peek();
				}
			}

			return null;
		}

		private TaskWrapper TaskQueueDequeue()
		{
			foreach (var queue in _taskQueue)
			{
				if (queue.Count != 0)
				{
					return queue.Dequeue();
				}
			}

			return null;
		}

		protected IEnumerator UpdateTaskManager()
		{
			while (!_isDestroying)
			{
				while (TaskQueueAny() && _runningTasks.Count <= ActiveTaskLimit)
				{
					var task = TaskQueuePeek();
					if (task.IsCancelled)
					{
						TaskQueueDequeue();
						TaskCancelled(task);
						continue;
					}

					if (QueueTimeHasMatured(task.EnqueueFrame, _requestDelay) || !Application.isPlaying)
					{
						TaskQueueDequeue();
						_runningTasks.Add(task);
						if (task is MeshGenTaskWrapper meshWrapper)
						{
							HandleMeshGenTask(meshWrapper);
						}
						else
						{
							HandleTask(task);
						}
					}
					else
					{
						yield return null;
					}
				}
				yield return null;
			}
		}
		
		private bool QueueTimeHasMatured(float queueTime, float maturationAge)
		{
			return Time.time - queueTime >= maturationAge;
		}

		private void HandleTask(TaskWrapper wrapper)
		{
			TaskStarting(wrapper);
			var task = Task.Run(wrapper.Action, _globalCancellationTokenSource.Token);
			_runningTasks.Add(wrapper);
			task.ContinueWith((t) =>
			{
				if (t.IsFaulted)
				{
					Debug.Log(t.Exception?.Message);
					Debug.Break();
				}
				TaskFinished(wrapper);
				_runningTasks.Remove(wrapper);
				wrapper.ContinueWith?.Invoke(task);
			}, TaskScheduler.FromCurrentSynchronizationContext());
			TaskStarted(wrapper);
		}

		private void HandleMeshGenTask(MeshGenTaskWrapper wrapper)
		{
			TaskStarting(wrapper);
			var task = Task.Run(wrapper.MeshGen);
			_runningTasks.Add(wrapper);
			task.ContinueWith((t) =>
			{
				if (t.IsFaulted)
				{
					// Debug.Log(t.Exception?.Message);
					// Debug.Log(wrapper.TileId);
					// Debug.Break();
					//return error
				}
				
				TaskFinished(wrapper);
				_runningTasks.Remove(wrapper);
				if (wrapper.ContinueMeshWith != null)
				{
					//Debug.Log(taskWrapper.FinishedFrame - taskWrapper.StartingFrame + " task timer");
					if (!t.IsFaulted)
					{
						
						wrapper.ContinueMeshWith(t.Result);
					}
					else
					{
						t.Result.ResultType = TaskResultType.MeshGenerationFailure;
						wrapper.ContinueMeshWith(null);
					}
				}
			}, TaskScheduler.FromCurrentSynchronizationContext());
			TaskStarted(wrapper);
		}

		protected virtual void TaskStarting(TaskWrapper task)
		{
			task.StartingTime = Time.time;
		}

		protected virtual void TaskFinished(TaskWrapper task)
		{
			task.FinishedTime = Time.time;
		}

		public virtual void AddTask(TaskWrapper taskWrapper, int priorityLevel = 3)
		{
			lock (_lock)
			{
				taskWrapper.EnqueueFrame = Time.time;
				_taskQueue[priorityLevel].Enqueue(taskWrapper);
				//Debug.Log(taskWrapper.Info);
			}
		}
		
		public void OnDestroy()
		{
			_globalCancellationTokenSource.Cancel();
			_isDestroying = true;
			// _allTasks.Clear();
			// _allTasks = null;
			// _tasksByTile.Clear();
			// _tasksByTile = null;
			//_taskQueueList = null;
			_taskQueue.Clear();
			TaskStarted = null;
			TaskCancelled = null;
		}
	}
}