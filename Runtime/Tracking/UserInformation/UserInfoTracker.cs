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
                // GeneralUserName se deja SIN INFORMAR, y es a proposito.
                //
                // Hasta la 2.0.28 aqui iba SystemInfo.deviceName, que en un visor
                // autonomo devuelve el MODELO: la columna User del dashboard decia
                // "Quest 3" en las siete filas de la tabla de avatares. Nunca fue
                // una persona, asi que no era un cruce roto sino el origen.
                //
                // Por confidencialidad el producto identifica al jugador por su ID
                // y no guarda su nombre, asi que no hay nada con que rellenarlo. El
                // backend solo escribe el campo si llega -ponerSiLlega descarta
                // undefined, null y cadena vacia-, y la lectura de avatares cae
                // entonces al playerID, que es justo lo que se quiere mostrar.
                //
                // El modelo del aparato sigue viajando aparte, en DeviceModel.
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
#if UNITY_ANDROID
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var defaultLocale = locale.CallStatic<AndroidJavaObject>("getDefault"))
                {
                    string country = defaultLocale.Call<string>("getCountry");
                    if (country != null && country.Length == 2)
                        return country.ToUpperInvariant();
                }
            }
            catch { }
            return "UN";
#else
            try
            {
                string region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
                if (region != null && region.Length == 2)
                    return region.ToUpperInvariant();
            }
            catch { }
            return "UN";
#endif
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
