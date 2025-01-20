

using System;
using UnityEngine;

#if UNITY_IOS
namespace Mapbox.BaseModule.Telemetry
{
	using System.Runtime.InteropServices;
	using Mapbox.BaseModule.Utilities;

	public class TelemetryIos : ITelemetryLibrary
	{
		private IntPtr _telemetryService = IntPtr.Zero;
		
		[DllImport("__Internal")] private static extern void setAccessTokenForToken(string accessToken);
		[DllImport("__Internal")] private static extern string getAccessToken();
		
		[DllImport("__Internal")] private static extern IntPtr getOrCreateTelemetryService();
		
		[DllImport("__Internal")] private static extern void setEventsCollectionStateForEnableCollection(bool state);
		
		[DllImport("__Internal")] private static extern void sendTurnstileEvent(string sdkIdentifier, string version);
		
		[DllImport("__Internal")] private static extern void sendSdkEvent(string sdkIdentifier, string version);
		

		public void Initialize(string accessToken)
		{
			setAccessTokenForToken(accessToken);
			Debug.Log("token " + getAccessToken().ToString());
			_telemetryService = getOrCreateTelemetryService();
			Debug.Log("telemetry service is null = " + (_telemetryService == null).ToString());
		}
		
		// [DllImport("__Internal")]
		// static extern void sendTurnstileEvent();
		//
		// [DllImport("__Internal")]
		// private static extern void setLocationCollectionState(bool enable);
		//
		// [DllImport("__Internal")]
		// private static extern void setSkuId(string skuId);

		static ITelemetryLibrary _instance = new TelemetryIos();
		public static ITelemetryLibrary Instance
		{
			get
			{
				return _instance;
			}
		}

		public void SendTurnstile()
		{
			sendTurnstileEvent(Constants.SDK_IDENTIFIER, Constants.SDK_VERSION);
		}

		public void SendSdkEvent()
		{
			sendSdkEvent(Constants.SDK_IDENTIFIER, Constants.SDK_VERSION);
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

			setEventsCollectionStateForEnableCollection(enable);
		}
	}
}
#endif