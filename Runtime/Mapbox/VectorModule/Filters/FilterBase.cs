using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.Filters
{
	public abstract class FilterBaseObject : ScriptableObject, IFilterObject
	{
		[SerializeField, HideInInspector] private bool m_Active = true;
		
		public abstract ILayerFeatureFilterComparer Filter { get; }
	}
	
	public class FilterBase : ILayerFeatureFilterComparer
	{
		public virtual string Key { get { return ""; } }

		public virtual void Initialize()
		{
			
		}
		
		public virtual bool Try(VectorFeatureUnity feature)
		{
			return true;
		}
	}

	public interface IFilterObject
	{
		ILayerFeatureFilterComparer Filter { get; }
	}
}