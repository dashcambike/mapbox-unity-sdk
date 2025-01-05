using System;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.VectorModule.Filters
{
    [CreateAssetMenu(menuName = "Mapbox/Filters/Height Filter")]
    public class HeightFilterObject : FilterBaseObject
    {
        private HeightFilter _filter;
        public HeightFilterSettings HeightFilterSettings;

        public override ILayerFeatureFilterComparer Filter
        {
            get
            {
                if(_filter == null)
                    _filter = new HeightFilter(HeightFilterSettings);
                return _filter;
            }
        }
    }
    
    [Serializable]
    public class HeightFilter : FilterBase
    {
        public HeightFilterSettings HeightFilterSettings;

        public HeightFilter(HeightFilterSettings heightFilterSettings)
        {
            HeightFilterSettings = heightFilterSettings;
        }

        public override bool Try(VectorFeatureUnity feature)
        {
            var hg = System.Convert.ToSingle(feature.Properties[Key]);
            if (HeightFilterSettings.Type == HeightFilterOptions.Above && hg > HeightFilterSettings.Height)
                return true;
            if (HeightFilterSettings.Type == HeightFilterOptions.Below && hg < HeightFilterSettings.Height)
                return true;

            return false;

        }
    }

    public class HeightFilterSettings
    {
        public HeightFilterOptions Type;
        public float Height;
    }
    
    public enum HeightFilterOptions
    {
        Above,
        Below
    }
}