#if UNITY_ANDROID
using System;
using UnityEngine;
using Mapbox.BaseModule.Telemetry;
using Mapbox.BaseModule.Utilities;

namespace Mapbox.BaseModule.Telemetry
{
	public class TelemetryAndroid : ITelemetryLibrary
	{
		private static ITelemetryLibrary _instance = new TelemetryAndroid();
		public static ITelemetryLibrary Instance => _instance;

		//Unity Activity
		private AndroidJavaObject _activityContext = null;
		private string _unityPlayerClassName = "com.unity3d.player.UnityPlayer";
		private string _unityPlayerActivityMethodName = "currentActivity";
		private string _couldNotGETCurrentActivityMessage = "Could not get current activity";

		//Telemetry
		private AndroidJavaObject _telemInstance = null;
		private bool _telemetryInitializationState = false;
		private string _mapboxTelemetryServiceClassName = "com.mapbox.common.TelemetryService";
		private string _mapboxTelemetryServiceGetMethodName = "getOrCreate";
		private string _couldNotGetClassMapboxTelemetryMessage = "Could not get class 'MapboxTelemetry'";

		//EventsService
		private AndroidJavaObject _mapboxEventService;
		private string _mapboxEventsServiceClassName = "com.mapbox.common.EventsService";
		private string _mapboxEventServiceGetMethodName = "getOrCreate";
		private string _mapboxEventsServerOptionsClassName = "com.mapbox.common.EventsServerOptions";
		private string _mapboxSdkInformationClassName = "com.mapbox.common.SdkInformation";
		private string _mapboxUserSkuIdentifierClassName = "com.mapbox.common.UserSKUIdentifier";
		
		//EventsService
		private AndroidJavaObject _mapboxBillingService;
		private string _mapboxBillingServiceClassName = "com.mapbox.common.BillingService";
		private string _mapboxBillingServiceFactoryClassName = "com.mapbox.common.BillingServiceFactory";
		private string _mapboxBillingFactoryGetMethodName = "getInstance";
		private string _mapboxSdkEventMethodName = "triggerUserBillingEvent";

		//Turnstile
		private string _unityMausEnumName = "UNITY_MAUS";
		private string _mapboxTurnstileEventClassName = "com.mapbox.common.TurnstileEvent";
		private string _sendTurnstileEventMethodName = "sendTurnstileEvent";
		private string _mapboxSdkInformationName = "Unity_SDK";
		private string _mapboxSdkInformationVersion = "3.0.0";
		private string _mapboxSdkInformationPackageName = "package_Name";
		private string _eventsServiceNullMessage = "events service null";
		private string _mapboxoptionsClassName = "com.mapbox.common.MapboxOptions";
		
		//Telemetry Utils
		private AndroidJavaClass _telemetryUtilsClass;
		private string _mapboxTelemetryUtilsClassName = "com.mapbox.common.TelemetryUtils";
		private string _setEventsCollectionStatMethodName = "setEventsCollectionState";

		//Mapbox Options
		private string _mapboxOptionsSetAccessTokenMethodName = "setAccessToken";
		private string _couldNotGetMapboxOptionsMessage = "Couldn't get Mapbox Options";

		private string _sdkInfoRegistryFactoryClassName = "com.mapbox.common.SdkInfoRegistryFactory";
		private string _sdkInfoRegistryFactoryGetMethodName = "getInstance"; 
		private string _sdkInfoRegistryRegisterMethodName = "registerSdkInformation";

		private AndroidJavaObject _sdkInformation;
		
		public void Initialize(string accessToken)
		{
			
			_telemetryUtilsClass = new AndroidJavaClass(_mapboxTelemetryUtilsClassName);
			
			if (string.IsNullOrEmpty(accessToken))
			{
				throw new System.ArgumentNullException();
			}

			using (AndroidJavaClass activityClass = new AndroidJavaClass(_unityPlayerClassName))
			{
				_activityContext = activityClass.GetStatic<AndroidJavaObject>(_unityPlayerActivityMethodName);
			}

			if (_activityContext == null)
			{
				Debug.LogError(_couldNotGETCurrentActivityMessage);
				return;
			}
			
			// SdkInformation testInformation = new SdkInformation(applicationName, packageVersion, packageName);
			// SdkInfoRegistry registry = SdkInfoRegistryFactory.getInstance();
			// registry.registerSdkInformation(testInformation);

			_sdkInformation = new AndroidJavaObject(
				_mapboxSdkInformationClassName,
				Constants.SDK_IDENTIFIER,
				Constants.SDK_VERSION,
				Constants.PACKAGE_NAME);
			
			var sdkInfoRegistryFactory = new AndroidJavaClass(_sdkInfoRegistryFactoryClassName);
			var sdkInfoRegistry = sdkInfoRegistryFactory.CallStatic<AndroidJavaObject>(_sdkInfoRegistryFactoryGetMethodName);
			sdkInfoRegistry.Call(_sdkInfoRegistryRegisterMethodName, _sdkInformation);
			
			//if (SetAccessToken(accessToken)) return;

			InitializeTelemetryService();
		}
		
		public void SendTurnstile()
		{
			using (AndroidJavaObject eventsServerOptions = new AndroidJavaObject(_mapboxEventsServerOptionsClassName, _sdkInformation, null))
			{
				var eventServiceFactory = new AndroidJavaClass(_mapboxEventsServiceClassName);
				_mapboxEventService = eventServiceFactory.CallStatic<AndroidJavaObject>(_mapboxEventServiceGetMethodName, eventsServerOptions);

				if (_mapboxEventService == null)
				{
					Debug.Log(_eventsServiceNullMessage);
					return;
				}

				var skuid = new AndroidJavaObject(_mapboxUserSkuIdentifierClassName);
				using (AndroidJavaObject turnstileEvent = new AndroidJavaObject(_mapboxTurnstileEventClassName, skuid.GetStatic<AndroidJavaObject>(_unityMausEnumName)))
				{
					_mapboxEventService.Call(_sendTurnstileEventMethodName, turnstileEvent, null);
				}
			}
		}

		public void SendSdkEvent()
		{
			var billingServiceFactory = new AndroidJavaClass(_mapboxBillingServiceFactoryClassName);
			var billingService = billingServiceFactory.CallStatic<AndroidJavaObject>(_mapboxBillingFactoryGetMethodName);
			
			var skuid = new AndroidJavaObject(_mapboxUserSkuIdentifierClassName);
			billingService.Call(_mapboxSdkEventMethodName, _sdkInformation, skuid.GetStatic<AndroidJavaObject>(_unityMausEnumName), null);
		}

		public void SetLocationCollectionState(bool enable)
		{
			if (enable)
			{
				Input.location.Start();
			}
			else
			{
				Input.location.Stop();
			}
			
			_telemetryUtilsClass.CallStatic(_setEventsCollectionStatMethodName, enable, null);
		}

		private bool SetAccessToken(string accessToken)
		{
			var mapboxOptionsClass = new AndroidJavaClass(_mapboxoptionsClassName);
			if (mapboxOptionsClass == null)
			{
				Debug.LogError(_couldNotGetMapboxOptionsMessage);
				return true;
			}

			mapboxOptionsClass.CallStatic(_mapboxOptionsSetAccessTokenMethodName, accessToken);
			return false;
		}

		private void InitializeTelemetryService()
		{
			var systemProvider = new AndroidJavaClass(_mapboxTelemetryServiceClassName);
			_telemInstance = systemProvider.CallStatic<AndroidJavaObject>(_mapboxTelemetryServiceGetMethodName);

			if (_telemInstance == null)
			{
				Debug.LogError(_couldNotGetClassMapboxTelemetryMessage);
				return;
			}
			else
			{
				_telemetryUtilsClass.CallStatic(_setEventsCollectionStatMethodName, _telemetryInitializationState, null);
			}
		}
	}
}
#endif
