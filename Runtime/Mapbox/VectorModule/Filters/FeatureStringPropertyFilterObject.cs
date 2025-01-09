using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.VectorModule.Filters
{
	[CreateAssetMenu(menuName = "Mapbox/Filters/Type Filter")]
	[DisplayName("String Property Filter")]
	public class FeatureStringPropertyFilterObject : FilterBaseObject
	{
		[NonSerialized] private FeatureStringPropertyFilter _filter;
		public FeatureStringPropertyFilterSettings PropertyFilterSettings;

		public override ILayerFeatureFilterComparer Filter
		{
			get
			{
				if (_filter == null)
					_filter = new FeatureStringPropertyFilter(PropertyFilterSettings);
				return _filter;
			}
		}
	}
	
	[Serializable]
	public class FeatureStringPropertyFilter : FilterBase
	{
		public FeatureStringPropertyFilterSettings PropertyFilterSettings;
		private HashSet<string> _types;
		private bool _operation;

		public FeatureStringPropertyFilter(FeatureStringPropertyFilterSettings propertyFilterSettings)
		{
			PropertyFilterSettings = propertyFilterSettings;
			_operation = PropertyFilterSettings.checkOperation == FilterCheckOperation.Equals ? true : false;
		}
		
		public override void Initialize()
		{
			base.Initialize();
			_types = new HashSet<string>();
			foreach (var s in PropertyFilterSettings.FilterString.Split(','))
			{
				_types.Add(s.Trim().ToLowerInvariant());
			}
		}

		public override bool Try(VectorFeatureUnity feature)
		{
			//this is a slightly cheesy way to negate by a flag
			//so right side is simply negated by the first part and equality check
			return _operation == _types.Contains(feature.Properties[PropertyFilterSettings.PropertyName].ToString().ToLowerInvariant());
		}
	}

	[Serializable]
	public class FeatureStringPropertyFilterSettings
	{
		public FilterCheckOperation checkOperation = FilterCheckOperation.Equals;
		public string PropertyName;
		public string FilterString;
	}

	public enum FilterCheckOperation
	{
		NotEquals,
		Equals,
		LessThan,
		LessThanOrEquals,
		MoreThan,
		MoreThanOrEquals
	}
}
