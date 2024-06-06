//  Distant Lands 2024
//  COZY: Stylized Weather 3
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using DistantLands.Cozy.Data;
using System.Collections.Generic;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    public class CozySaveLoadModule : CozyModule
    {

        public struct DataSave
        {
            public MeridiemTime currentTime;
            public int day;
            public int year;
            public AmbienceProfile currentAmbience;
            public float ambienceTimer;
            public WeatherProfile currentWeather;
            public float weatherTimer;
            public List<CozyEcosystem.WeatherPattern> forecast;

        }

        // Start is called before the first frame update
        void Awake()
        {

            if (!enabled)
                return;

            InitializeModule();

        }

        public void Save()
        {
            Save(0);
        }

        public void Save(int slot)
        {

            if (weatherSphere == null)
                InitializeModule();


            DataSave save = new DataSave();

            if (weatherSphere.GetModule(out CozyAmbienceModule module))
            {
                save.ambienceTimer = module.ambienceTimer;
                save.currentAmbience = module.currentAmbienceProfile;
            }
            if (weatherSphere.weatherModule)
            {
                save.forecast = weatherSphere.weatherModule.ecosystem.currentForecast;
                save.currentWeather = weatherSphere.weatherModule.ecosystem.currentWeather;
                save.weatherTimer = weatherSphere.weatherModule.ecosystem.weatherTimer;
            }
            if (weatherSphere.timeModule)
            {
                save.currentTime = weatherSphere.timeModule.currentTime;
                save.day = weatherSphere.timeModule.currentDay;
                save.year = weatherSphere.timeModule.currentYear;
            }

            PlayerPrefs.SetString($"CZY_Save_{slot}", JsonUtility.ToJson(save));

            Debug.Log($"Saved COZY instance to slot 0\n{save}");

        }

        public string SaveToExternalJSON()
        {

            DataSave save = new DataSave();

            if (weatherSphere.GetModule(out CozyAmbienceModule module))
            {
                save.ambienceTimer = module.ambienceTimer;
                save.currentAmbience = module.currentAmbienceProfile;
            }
            if (weatherSphere.weatherModule)
            {
                save.forecast = weatherSphere.weatherModule.ecosystem.currentForecast;
                save.currentWeather = weatherSphere.weatherModule.ecosystem.currentWeather;
                save.weatherTimer = weatherSphere.weatherModule.ecosystem.weatherTimer;
            }
            if (weatherSphere.timeModule)
            {
                save.currentTime = weatherSphere.timeModule.currentTime;
                save.day = weatherSphere.timeModule.currentDay;
                save.year = weatherSphere.timeModule.currentYear;
            }


            Debug.Log("Wrote COZY instance to external JSON");
            return JsonUtility.ToJson(save);
        }

        public void Load()
        {

            Load(0);

        }
        public void Load(int slot)
        {


            if (weatherSphere == null)
                InitializeModule();

            DataSave save = JsonUtility.FromJson<DataSave>(PlayerPrefs.GetString("CZY_Save_0"));

            if (weatherSphere.GetModule(out CozyAmbienceModule module))
            {
                module.ambienceTimer = save.ambienceTimer;
                module.currentAmbienceProfile = save.currentAmbience;
            }
            weatherSphere.weatherModule.ecosystem.currentForecast = save.forecast;
            weatherSphere.weatherModule.ecosystem.currentWeather = save.currentWeather;
            weatherSphere.weatherModule.ecosystem.weatherTimer = save.weatherTimer;
            weatherSphere.timeModule.currentTime = save.currentTime;
            weatherSphere.timeModule.currentDay = save.day;
            weatherSphere.timeModule.currentYear = save.year;

            weatherSphere.SetupReferences();

            Debug.Log("Loaded COZY save to current instance");
        }

        public void LoadFromExternalJSON(string JSONSave)
        {

            DataSave save = JsonUtility.FromJson<DataSave>(JSONSave);

            JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString("CZY_Save_0"), save);

            if (weatherSphere.GetModule(out CozyAmbienceModule module))
            {
                module.ambienceTimer = save.ambienceTimer;
                module.currentAmbienceProfile = save.currentAmbience;
            }
            weatherSphere.weatherModule.ecosystem.currentForecast = save.forecast;
            weatherSphere.weatherModule.ecosystem.currentWeather = save.currentWeather;
            weatherSphere.weatherModule.ecosystem.weatherTimer = save.weatherTimer;
            weatherSphere.timeModule.currentTime = save.currentTime;
            weatherSphere.timeModule.currentDay = save.day;
            weatherSphere.timeModule.currentYear = save.year;

            weatherSphere.SetupReferences();

            weatherSphere.SetupReferences();

            Debug.Log("Loaded external JSON to current COZY instance");

        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CozySaveLoadModule))]
    public class E_CozySaveLoad : E_CozyModule, IControlPanel
    {

        CozySaveLoadModule saveLoad;
        public static int saveSlot = 0;

        void OnEnable()
        {

            saveLoad = (CozySaveLoadModule)target;

        }

        public override GUIContent GetGUIContent()
        {

            return new GUIContent("    Save & Load", (Texture)Resources.Load("Save"), "Manage save and load commands within the COZY system.");

        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-stylized-weather-documentation/how-it-works/modules/save-and-load-module");
        }

        public override void OnInspectorGUI()
        {


        }

        public override void DisplayInCozyWindow()
        {
            EditorGUI.indentLevel = 0;
            EditorGUILayout.BeginHorizontal();

            saveSlot = EditorGUILayout.IntPopup(saveSlot,
            new GUIContent[5] { new GUIContent("Save Slot 0 (Default)"), new GUIContent("Save Slot 1"), new GUIContent("Save Slot 2"), new GUIContent("Save Slot 3"), new GUIContent("Save Slot 4") },
            new int[5] { 0, 1, 2, 3, 4 });

            if (GUILayout.Button("Save"))
                saveLoad.Save(saveSlot);
            if (GUILayout.Button("Load"))
                saveLoad.Load(saveSlot);

            EditorGUILayout.EndHorizontal();

        }
        public void GetControlPanel()
        {
            if (GUILayout.Button("Save"))
                saveLoad.Save();
            if (GUILayout.Button("Load"))
                saveLoad.Load();
        }

    }
#endif
}