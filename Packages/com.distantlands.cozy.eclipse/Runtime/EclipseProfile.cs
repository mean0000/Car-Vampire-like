#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace DistantLands.Cozy.Data
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Eclipse Profile", order = 361)]
    public class EclipseProfile : CozyProfile
    {
        [GradientUsage(true)] public Gradient skyZenithColor;
        [GradientUsage(true)] public Gradient skyHorizonColor;

        [GradientUsage(true)] public Gradient cloudColor;
        [GradientUsage(true)] public Gradient cloudHighlightColor;
        [GradientUsage(true)] public Gradient highAltitudeCloudColor;
        [GradientUsage(true)] public Gradient sunlightColor;
        [GradientUsage(true)] public Gradient moonlightColor;
        [GradientUsage(true)] public Gradient starColor;
        [GradientUsage(true)] public Gradient ambientLightHorizonColor;
        [GradientUsage(true)] public Gradient ambientLightZenithColor;
        public AnimationCurve ambientLightMultiplier;
        public AnimationCurve galaxyIntensity;

        [GradientUsage(true)] public Gradient fogColor1;
        [GradientUsage(true)] public Gradient fogColor2;
        [GradientUsage(true)] public Gradient fogColor3;
        [GradientUsage(true)] public Gradient fogColor4;
        [GradientUsage(true)] public Gradient fogColor5;
        [GradientUsage(true)] public Gradient fogFlareColor;
        [GradientUsage(true)] public Gradient fogMoonFlareColor;
        public AnimationCurve fogSmoothness;

        [GradientUsage(true)] public Gradient sunColor;

        public AnimationCurve sunFlareFalloff;
        [GradientUsage(true)] public Gradient sunFlareColor;
        public AnimationCurve moonFalloff;
        [GradientUsage(true)] public Gradient moonFlareColor;
        [GradientUsage(true)] public Gradient galaxy1Color;
        [GradientUsage(true)] public Gradient galaxy2Color;
        [GradientUsage(true)] public Gradient galaxy3Color;
        [GradientUsage(true)] public Gradient lightScatteringColor;

        public AnimationCurve fogLightFlareIntensity;
        public AnimationCurve fogLightFlareFalloff;

        [GradientUsage(true)] public Gradient cloudMoonColor;
        [GradientUsage(true)] public Gradient cloudTextureColor;
    }

// #if UNITY_EDITOR
//     [CustomEditor(typeof(EclipseProfile))]
//     [CanEditMultipleObjects]
//     public class E_EclipseProfile : Editor
//     {

//         protected static bool tooltips;

//         public override void OnInspectorGUI()
//         {
//             serializedObject.Update();
//             tooltips = EditorPrefs.GetBool("CZY_Tooltips", true);

//             E_CozyAtmosphereModule.atmosphereOptionsWindow = EditorGUILayout.BeginFoldoutHeaderGroup(E_CozyAtmosphereModule.atmosphereOptionsWindow,
//                 new GUIContent("    Atmosphere & Lighting", "Skydome, fog, and lighting settings."), EditorUtilities.FoldoutStyle);

//             if (E_CozyAtmosphereModule.atmosphereOptionsWindow)
//             {

//                 DrawAtmosphereTab();

//             }

//             EditorGUILayout.EndFoldoutHeaderGroup();

//             E_CozyAtmosphereModule.fogOptionsWindow = EditorGUILayout.BeginFoldoutHeaderGroup(E_CozyAtmosphereModule.fogOptionsWindow,
//                             new GUIContent("    Fog", "Manage fog settings."), EditorUtilities.FoldoutStyle);

//             if (E_CozyAtmosphereModule.fogOptionsWindow)
//             {

//                 DrawFogTab();

//             }

//             EditorGUILayout.EndFoldoutHeaderGroup();

//             E_CozyAtmosphereModule.cloudsOptionsWindow = EditorGUILayout.BeginFoldoutHeaderGroup(E_CozyAtmosphereModule.cloudsOptionsWindow,
//                             new GUIContent("    Clouds", "Cloud color, generation, and variation settings."), EditorUtilities.FoldoutStyle);

//             if (E_CozyAtmosphereModule.cloudsOptionsWindow)
//             {

//                 DrawCloudsTab();

//             }

//             EditorGUILayout.EndFoldoutHeaderGroup();


//             E_CozyAtmosphereModule.celestialsOptionsWindow = EditorGUILayout.BeginFoldoutHeaderGroup(E_CozyAtmosphereModule.celestialsOptionsWindow,
//                             new GUIContent("    Celestials & VFX", "Sun, moon, and light FX settings."), EditorUtilities.FoldoutStyle);

//             if (E_CozyAtmosphereModule.celestialsOptionsWindow)
//             {

//                 DrawCelestialsTab();

//             }

//             EditorGUILayout.EndFoldoutHeaderGroup();


//             serializedObject.ApplyModifiedProperties();


//         }



//         void DrawAtmosphereTab()
//         {

//             GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
//             {
//                 fontStyle = FontStyle.Bold
//             };

//             EditorGUILayout.LabelField(" Skydome Settings", labelStyle);
//             EditorGUI.indentLevel++;

//             EditorGUILayout.PropertyField(serializedObject.FindProperty("skyZenithColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("skyHorizonColor"), false);

//             EditorGUILayout.Space(5);
//             EditorGUI.indentLevel--;
//             EditorGUILayout.LabelField(" Lighting Settings", labelStyle);
//             EditorGUI.indentLevel++;

//             EditorGUILayout.PropertyField(serializedObject.FindProperty("sunlightColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("moonlightColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientLightHorizonColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientLightZenithColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientLightMultiplier"), false);
//             EditorGUI.indentLevel--;


//         }


//         void DrawFogTab()
//         {

//             GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
//             labelStyle.fontStyle = FontStyle.Bold;

//             if (tooltips)
//                 EditorGUILayout.HelpBox("Interpolate controls change the value depending on the time of day. These range from 00:00 to 23:59, which means that morning is about 25% through the curve, midday 50%, evening 75%, etc. \n \n Constant controls set the value to a single value that remains constant regardless of the time of day.", MessageType.Info);


//             EditorGUILayout.Space(5);
//             EditorGUILayout.LabelField(" Fog Settings", labelStyle);
//             EditorGUI.indentLevel++;

//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor1"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor2"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor3"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor4"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogColor5"), false);
//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogSmoothness"), false);


//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogFlareColor"), new GUIContent("Light Flare Color",
//                 "Sets the color of the fog for a false \"light flare\" around the main sun directional light."), false);
//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogMoonFlareColor"), new GUIContent("Moon Flare Color",
//                 "Sets the color of the fog for a false \"light flare\" around the main moon directional light."), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogLightFlareIntensity"), new GUIContent("Light Flare Intensity",
//                 "Modulates the brightness of the light flare."), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("fogLightFlareFalloff"), new GUIContent("Light Flare Falloff",
//                 "Sets the falloff speed for the light flare."), false);
//             EditorGUILayout.Space(5);

//             EditorGUI.indentLevel--;

//         }

//         void DrawCloudsTab()
//         {

//             if (tooltips)
//                 EditorGUILayout.HelpBox("Interpolate controls change the value depending on the time of day. These range from 00:00 to 23:59, which means that morning is about 25% through the curve, midday 50%, evening 75%, etc. \n \n Constant controls set the value to a single value that remains constant regardless of the time of day.", MessageType.Info);


//             GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
//             labelStyle.fontStyle = FontStyle.Bold;

//             EditorGUILayout.LabelField(" Color Settings", labelStyle);
//             EditorGUI.indentLevel++;


//             EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudColor"), new GUIContent("Cloud Color", "The main color of the unlit clouds."), false);

//             EditorGUILayout.PropertyField(serializedObject.FindProperty("highAltitudeCloudColor"), new GUIContent("High Altitude Color", "The main color multiplier of the high altitude clouds. The cloud types affected are the cirrostratus and the altocumulus types."), false);
//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudHighlightColor"), new GUIContent("Sun Highlight Color", "The color multiplier for the clouds in a \"dot\" around the sun."), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("cloudMoonColor"), new GUIContent("Moon Highlight Color", "The color multiplier for the clouds in a \"dot\" around the moon."), false);

//             EditorGUI.indentLevel--;


//         }

//         void DrawCelestialsTab()
//         {

//             if (tooltips)
//                 EditorGUILayout.HelpBox("Interpolate controls change the value depending on the time of day. These range from 00:00 to 23:59, which means that morning is about 25% through the curve, midday 50%, evening 75%, etc. \n \n Constant controls set the value to a single value that remains constant regardless of the time of day.", MessageType.Info);

//             GUIStyle labelStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
//             labelStyle.fontStyle = FontStyle.Bold;

//             EditorGUILayout.LabelField(" Sun Settings", labelStyle);
//             EditorGUI.indentLevel++;
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("sunColor"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("sunFlareColor"), new GUIContent("Sun Halo Color"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("sunFlareFalloff"), new GUIContent("Sun Halo Falloff"), false);
//             EditorGUI.indentLevel--;

//             EditorGUILayout.Space(16);

//             EditorGUILayout.LabelField(" Moon Settings", labelStyle);
//             EditorGUI.indentLevel++;
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("moonFlareColor"), new GUIContent("Moon Halo Color"), false);
//             EditorGUI.indentLevel--;



//             EditorGUILayout.Space(15);
//             EditorGUILayout.LabelField(" VFX", labelStyle);
//             EditorGUI.indentLevel++;

//             EditorGUILayout.PropertyField(serializedObject.FindProperty("starColor"), false);

//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("galaxyIntensity"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("galaxy1Color"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("galaxy2Color"), false);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("galaxy3Color"), false);
//             EditorGUILayout.Space(5);
//             EditorGUILayout.PropertyField(serializedObject.FindProperty("lightScatteringColor"), false);
//             EditorGUILayout.Space(5);

//             EditorGUI.indentLevel--;


//         }
//     }
// #endif
}