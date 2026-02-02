using System;
using System.Globalization;
using UnityEngine;
using GossipSDK.Core.Data;
using GossipSDK.Core.Messaging;
using GossipSDK.Core.Connection;
using Newtonsoft.Json;

namespace GossipSDK.Tracking.UserInformation
{
    public class UserInfoTracker
        : GenericSocketConnection<UserInfoTracker.EntityData, UserInfoTracker.TrackerMessage>
    {
        protected override string EventName => "TrackingInfoUser";

        private bool capturedOnce = false;

        public void CaptureOnce()
        {
            if (capturedOnce)
                return;

            capturedOnce = true;

            var data = new EntityData
            {
                DeviceLanguage = Application.systemLanguage.ToString(),
                Language = Application.systemLanguage.ToString(),
                UserName = SystemInfo.deviceName,
                UserAge = string.Empty,
                CountryCode = GetCountryCode(),
                DeviceBrand = GetDeviceBrand(),
                DeviceModel = SystemInfo.deviceModel,
                OSName = SystemInfo.operatingSystemFamily.ToString(),
                OSVersion = SystemInfo.operatingSystem,
                BatteryStatus = SystemInfo.batteryStatus.ToString()
            };

            CapSession(data);
        }

        private string GetCountryCode()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch
            {
                return "UN";
            }
        }

        private string GetDeviceBrand()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "Apple";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "Apple";
#elif UNITY_STANDALONE_LINUX
            return "Linux";
#else
            return SystemInfo.deviceModel;
#endif
        }

        [Serializable]
        public class EntityData : Data
        {
            [JsonProperty("GeneralDeviceLanguaje")]
            public string DeviceLanguage { get; set; }

            [JsonProperty("GeneralUserName")]
            public string UserName { get; set; }

            [JsonProperty("GeneralUserAge")]
            public string UserAge { get; set; }

            [JsonProperty("GeneralUserCountry")]
            public string CountryCode { get; set; }

            [JsonProperty("GeneralBrandDevice")]
            public string DeviceBrand { get; set; }

            [JsonProperty("GeneralModelDevice")]
            public string DeviceModel { get; set; }

            [JsonProperty("GeneralOperativeModelDevice")]
            public string OSName { get; set; }

            [JsonProperty("GeneralVersionOperativeSystemDevice")]
            public string OSVersion { get; set; }

            [JsonProperty("GeneralBatteryStatus")]
            public string BatteryStatus { get; set; }

            [JsonProperty("Language")]
            public string Language { get; set; }

            [JsonConstructor]
            public EntityData() { }
        }

        [Serializable]
        public class TrackerMessage : Message<EntityData> { }
    }
}
