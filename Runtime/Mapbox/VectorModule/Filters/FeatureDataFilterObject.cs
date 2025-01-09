using System;
using System.ComponentModel;
using Mapbox.BaseModule.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mapbox.VectorModule.Filters
{
    [CreateAssetMenu(menuName = "Mapbox/Filters/Data Filter")]
    [DisplayName("Data Filter")]

    public class FeatureDataFilterObject : FilterBaseObject
    {
        [FormerlySerializedAs("Operation")] public FilterCheckOperation checkOperation;
        [NonSerialized] private FeatureDataFilter _filter;
        public FeatureDataNameEnum dataEnum;
        public string Value;

        public override ILayerFeatureFilterComparer Filter
        {
            get
            {
                if (_filter == null)
                    _filter = new FeatureDataFilter(dataEnum, Value, checkOperation);
                return _filter;
            }
        }
    }
    
    [Serializable]
    public class FeatureDataFilter : FilterBase
    {
        private bool _operation;
        private FeatureDataNameEnum _dataName;
        private string _value;

        public FeatureDataFilter(FeatureDataNameEnum dataName, string value, FilterCheckOperation checkOperation = FilterCheckOperation.Equals)
        {
            _dataName = dataName;
            _value = value.ToLowerInvariant();
            _operation = checkOperation == FilterCheckOperation.Equals ? true : false;
        }

        public override bool Try(VectorFeatureUnity feature)
        {
            switch (_dataName)
            {
                case FeatureDataNameEnum.GeometryType:
                {
                    //this is a slightly cheesy way to negate by a flag
                    //so right side is simply negated by the first part and equality check
                    return _operation == (feature.Data.GeometryType.ToString().ToLowerInvariant() == _value); 
                    break;
                }
                case FeatureDataNameEnum.Id:
                {
                    return _operation == (feature.Data.Id.ToString().ToLowerInvariant() == _value);
                }
            }

            return false;
        }
    }

    public enum FeatureDataNameEnum
    {
        GeometryType,
        Id
    }
}