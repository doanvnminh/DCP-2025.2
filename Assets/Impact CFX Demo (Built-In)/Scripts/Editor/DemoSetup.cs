using ImpactCFX.EditorScripts;
using UnityEditor;
using UnityEngine;

namespace ImpactCFXDemo.EditorScripts
{
    public class DemoSetup : EditorWindow
    {
        private static Vector2 windowDimensions = new Vector2(500, 500);

        private static string[] impactCFXDemoTags = new string[]
        {
            "Material_Hard",
            "Material_Soft",
            "Weapon_Bullet",
            "Footstep_Left",
            "Footstep_Right",
            "Material_Metallic",
            "Material_Dusty",
            "Object_Marble"
        };

        [InitializeOnLoadMethod]
        public static void InitializeSetupWindow()
        {
            ImpactCFXSettings impactCFXSettings = ImpactCFXSettings.instance;

            if (impactCFXSettings.HasSeenDemoSetup)
                return;

            EditorApplication.update += initializeSetupWindowDelayed;
        }

        private static void initializeSetupWindowDelayed()
        {
            if (EditorApplication.isUpdating)
            {
                return;
            }

            EditorApplication.update -= initializeSetupWindowDelayed;

            ShowDemoSetupWindow();
        }

        [MenuItem("Window/Impact CFX/Demo Setup")]
        public static void ShowDemoSetupWindow()
        {
            DemoSetup demoSetupWindow = GetWindow(typeof(DemoSetup), true, "Impact CFX Demo Setup") as DemoSetup;
            demoSetupWindow.minSize = demoSetupWindow.maxSize = windowDimensions;

            Rect position = new Rect(Vector2.zero, windowDimensions);
            Vector2 screenCenter = new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) / 2;
            position.center = screenCenter / EditorGUIUtility.pixelsPerPoint;
            demoSetupWindow.position = position;

            demoSetupWindow.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("This window provides some setup information about the Impact CFX demo that you have imported into your project.", MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Some portions of the Impact CFX Demo use a lot of audio effects.\n\n" +
                "If things don't sound right, you may want to increase the 'Max Real Voices' setting in your 'Audio' project settings.", EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Impact CFX Project Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("The Impact CFX Demo was created with its own set of Impact CFX Settings. " +
                "Do you want to overwrite your current Impact CFX Settings with the values used by the demo? \n\n" +
                "This will only overwrite the settings in the Impact CFX section. " +
                "The demo will still work if you don't want to overwrite your settings.", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Apply Impact CFX Demo Settings", GUILayout.Height(EditorGUIUtility.singleLineHeight * 1.5f)))
            {
                ImpactCFXSettings impactCFXSettings = ImpactCFXSettings.instance;

                for (int i = 0; i < impactCFXDemoTags.Length; i++)
                {
                    impactCFXSettings[i] = impactCFXDemoTags[i];
                }

                impactCFXSettings.SaveSettings();
            }

            EditorGUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            ImpactCFXSettings impactCFXSettings = ImpactCFXSettings.instance;

            impactCFXSettings.HasSeenDemoSetup = true;
            impactCFXSettings.SaveSettings();
        }
    }
}