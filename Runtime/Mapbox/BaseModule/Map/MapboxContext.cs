using System;
using Mapbox.BaseModule;
using Mapbox.BaseModule.Telemetry;
using Mapbox.BaseModule.Utilities;
using UnityEngine;

namespace Mapbox.BaseModule.Map
{
    public class MapboxContext : IMapboxContext
    {
        public MapboxConfiguration Configuration;
        private ITelemetryLibrary _telemetryLibrary;

        private MapboxToken _mapboxToken;
        private string _tokenNotSetErrorMessage = "No configuration file found! Configure your access token from the Mapbox > Setup menu.";

        public MapboxContext()
        {
            LoadConfiguration();
        }

        public string GetAccessToken()
        {
            return Configuration.AccessToken;
        }

        public string GetSkuToken()
        {
            return Configuration.GetMapsSkuToken();
        }

        public MapboxTokenStatus TokenStatus()
        {
            if (_mapboxToken == null)
                return MapboxTokenStatus.StatusNotYetSet;
            
            return _mapboxToken.Status;
        }
        
        private void LoadConfiguration()
        {
            TextAsset configurationTextAsset = Resources.Load<TextAsset>(Constants.Path.MAPBOX_RESOURCES_RELATIVE);
            if (null == configurationTextAsset)
            {
                Debug.LogError("Need Mapbox Access Token");
                throw new Exception();
            }

            var config = JsonUtility.FromJson<MapboxConfiguration>(configurationTextAsset.text);
            config.Initialize();
            var tokenValidator = new MapboxTokenApi();
            tokenValidator.Retrieve(config.GetMapsSkuToken, config.AccessToken, (response) =>
            {
                _mapboxToken = response;
                if (_mapboxToken.Status != MapboxTokenStatus.TokenValid)
                {
                    config.AccessToken = string.Empty;
                    Debug.LogError("Invalid Token");
                }
                else
                {
                    ConfigureTelemetry();
                }
            });

            Configuration = config;
        }

        private void ConfigureTelemetry()
        {
            //TODO: enable after token validation has been made async
            if (
            	null == Configuration ||
                string.IsNullOrEmpty(Configuration.AccessToken) ||
                _mapboxToken.Status != MapboxTokenStatus.TokenValid
            )
            {
            	Debug.LogError(_tokenNotSetErrorMessage);
            	return;
            }
            try
            {
                _telemetryLibrary = TelemetryFactory.GetTelemetryInstance();
                _telemetryLibrary.Initialize(Configuration.AccessToken);
                _telemetryLibrary.SetLocationCollectionState(GetTelemetryCollectionState());
                _telemetryLibrary.SendTurnstile();
                _telemetryLibrary.SendSdkEvent();
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Error initializing telemetry: {0}", ex);
            }
        }

        private bool GetTelemetryCollectionState()
        {
            if (!PlayerPrefs.HasKey(Constants.Path.SHOULD_COLLECT_LOCATION_KEY))
            {
                PlayerPrefs.SetInt(Constants.Path.SHOULD_COLLECT_LOCATION_KEY, 1);
            }
            return PlayerPrefs.GetInt(Constants.Path.SHOULD_COLLECT_LOCATION_KEY) != 0;
        }

        public void ValidateToken(Action callback = null)
        {
            var tokenValidator = new MapboxTokenApi();
            tokenValidator.Retrieve(GetSkuToken, GetAccessToken(), (response) =>
            {
                _mapboxToken = response;
                if (_mapboxToken.Status != MapboxTokenStatus.TokenValid)
                {
                    Debug.LogError("Invalid Token");
                }
                callback?.Invoke();
            });
        }
    }

    public interface IMapboxContext
    {
        public string GetAccessToken();
        public string GetSkuToken();
    }
}