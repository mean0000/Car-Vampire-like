using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DistantLands.Cozy.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DistantLands.Cozy
{
    [ExecuteAlways]
    public class EclipseModule : CozyModule
    {

        [Range(0, 1)]
        public float eclipseRatio;
        public enum EclipseStyle { manual, occlusion }
        public EclipseStyle eclipseStyle = EclipseStyle.occlusion;

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
        [Range(0, 1)]
        public float moonSize = 0.95f;

        public EclipseProfile profile;

        public void Update()
        {

            Shader.SetGlobalVector(CozyShaderIDs.CZY_EclipseDirectionID, weatherSphere.moonDirection);


        }

        public override void PropogateVariables()
        {

            Shader.SetGlobalFloat(CozyShaderIDs.CZY_MoonSizeID, moonSize);

            if (eclipseStyle == EclipseStyle.occlusion)
            {
                eclipseRatio = Mathf.Clamp01((-Vector3.Dot(weatherSphere.sunTransform.forward, weatherSphere.moonDirection) - 0.995f) / (1 - 0.995f)) * Mathf.Clamp01(MathF.Sin(Mathf.PI * 2 * (weatherSphere.dayPercentage - 0.25f)));
            }

            if (eclipseRatio > 0)
            {
                Color cachedColor;
                float cachedValue;
                cachedColor = profile.skyZenithColor.Evaluate(eclipseRatio);
                weatherSphere.skyZenithColor = Color.Lerp(weatherSphere.skyZenithColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.skyZenithColor.a), cachedColor.a);

                cachedColor = profile.skyHorizonColor.Evaluate(eclipseRatio);
                weatherSphere.skyHorizonColor = Color.Lerp(weatherSphere.skyHorizonColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.skyHorizonColor.a), cachedColor.a);

                cachedColor = profile.cloudColor.Evaluate(eclipseRatio);
                weatherSphere.cloudColor = Color.Lerp(weatherSphere.cloudColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.cloudColor.a), cachedColor.a);

                cachedColor = profile.cloudHighlightColor.Evaluate(eclipseRatio);
                weatherSphere.cloudHighlightColor = Color.Lerp(weatherSphere.cloudHighlightColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.cloudHighlightColor.a), cachedColor.a);

                cachedColor = profile.highAltitudeCloudColor.Evaluate(eclipseRatio);
                weatherSphere.highAltitudeCloudColor = Color.Lerp(weatherSphere.highAltitudeCloudColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.highAltitudeCloudColor.a), cachedColor.a);

                cachedColor = profile.sunlightColor.Evaluate(eclipseRatio);
                weatherSphere.sunlightColor = Color.Lerp(weatherSphere.sunlightColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.sunlightColor.a), cachedColor.a);

                cachedColor = profile.moonlightColor.Evaluate(eclipseRatio);
                weatherSphere.moonlightColor = Color.Lerp(weatherSphere.moonlightColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.moonlightColor.a), cachedColor.a);

                cachedColor = profile.starColor.Evaluate(eclipseRatio);
                weatherSphere.starColor = Color.Lerp(weatherSphere.starColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.starColor.a), cachedColor.a);

                cachedColor = profile.ambientLightHorizonColor.Evaluate(eclipseRatio);
                weatherSphere.ambientLightHorizonColor = Color.Lerp(weatherSphere.ambientLightHorizonColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.ambientLightHorizonColor.a), cachedColor.a);

                cachedColor = profile.ambientLightZenithColor.Evaluate(eclipseRatio);
                weatherSphere.ambientLightZenithColor = Color.Lerp(weatherSphere.ambientLightZenithColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.ambientLightZenithColor.a), cachedColor.a);

                cachedValue = profile.ambientLightMultiplier.Evaluate(eclipseRatio);
                weatherSphere.ambientLightMultiplier = Mathf.Lerp(weatherSphere.ambientLightMultiplier, cachedValue, eclipseRatio);

                cachedValue = profile.galaxyIntensity.Evaluate(eclipseRatio);
                weatherSphere.galaxyIntensity = Mathf.Lerp(weatherSphere.galaxyIntensity, cachedValue, eclipseRatio);

                cachedValue = profile.sunFlareFalloff.Evaluate(eclipseRatio);
                weatherSphere.sunFalloff = Mathf.Lerp(weatherSphere.sunFalloff, cachedValue, eclipseRatio);

                cachedColor = profile.fogColor1.Evaluate(eclipseRatio);
                weatherSphere.fogColor1 = Color.Lerp(weatherSphere.fogColor1, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogColor1.a), cachedColor.a);

                cachedColor = profile.fogColor2.Evaluate(eclipseRatio);
                weatherSphere.fogColor2 = Color.Lerp(weatherSphere.fogColor2, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogColor2.a), cachedColor.a);

                cachedColor = profile.fogColor3.Evaluate(eclipseRatio);
                weatherSphere.fogColor3 = Color.Lerp(weatherSphere.fogColor3, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogColor3.a), cachedColor.a);

                cachedColor = profile.fogColor4.Evaluate(eclipseRatio);
                weatherSphere.fogColor4 = Color.Lerp(weatherSphere.fogColor4, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogColor4.a), cachedColor.a);

                cachedColor = profile.fogColor5.Evaluate(eclipseRatio);
                weatherSphere.fogColor5 = Color.Lerp(weatherSphere.fogColor5, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogColor5.a), cachedColor.a);

                cachedColor = profile.fogFlareColor.Evaluate(eclipseRatio);
                weatherSphere.fogFlareColor = Color.Lerp(weatherSphere.fogFlareColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogFlareColor.a), cachedColor.a);

                cachedColor = profile.fogMoonFlareColor.Evaluate(eclipseRatio);
                weatherSphere.fogMoonFlareColor = Color.Lerp(weatherSphere.fogMoonFlareColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.fogMoonFlareColor.a), cachedColor.a);

                cachedValue = profile.fogSmoothness.Evaluate(eclipseRatio);
                weatherSphere.fogSmoothness = Mathf.Lerp(weatherSphere.fogSmoothness, cachedValue, eclipseRatio);

                cachedColor = profile.sunColor.Evaluate(eclipseRatio);
                weatherSphere.sunColor = Color.Lerp(weatherSphere.sunColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.sunColor.a), cachedColor.a);

                cachedValue = profile.sunFlareFalloff.Evaluate(eclipseRatio);
                weatherSphere.sunFalloff = Mathf.Lerp(weatherSphere.sunFalloff, cachedValue, eclipseRatio);

                cachedColor = profile.sunFlareColor.Evaluate(eclipseRatio);
                weatherSphere.sunFlareColor = Color.Lerp(weatherSphere.sunFlareColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.sunFlareColor.a), cachedColor.a);

                cachedValue = profile.moonFalloff.Evaluate(eclipseRatio);
                weatherSphere.moonFalloff = Mathf.Lerp(weatherSphere.moonFalloff, cachedValue, eclipseRatio);

                cachedColor = profile.moonFlareColor.Evaluate(eclipseRatio);
                weatherSphere.moonFlareColor = Color.Lerp(weatherSphere.moonFlareColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.moonFlareColor.a), cachedColor.a);

                cachedColor = profile.galaxy1Color.Evaluate(eclipseRatio);
                weatherSphere.galaxy1Color = Color.Lerp(weatherSphere.galaxy1Color, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.galaxy1Color.a), cachedColor.a);

                cachedColor = profile.galaxy2Color.Evaluate(eclipseRatio);
                weatherSphere.galaxy2Color = Color.Lerp(weatherSphere.galaxy2Color, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.galaxy2Color.a), cachedColor.a);

                cachedColor = profile.galaxy3Color.Evaluate(eclipseRatio);
                weatherSphere.galaxy3Color = Color.Lerp(weatherSphere.galaxy3Color, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.galaxy3Color.a), cachedColor.a);

                cachedColor = profile.lightScatteringColor.Evaluate(eclipseRatio);
                weatherSphere.lightScatteringColor = Color.Lerp(weatherSphere.lightScatteringColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.lightScatteringColor.a), cachedColor.a);

                cachedValue = profile.fogLightFlareIntensity.Evaluate(eclipseRatio);
                weatherSphere.fogLightFlareIntensity = Mathf.Lerp(weatherSphere.fogLightFlareIntensity, cachedValue, eclipseRatio);

                cachedValue = profile.fogLightFlareFalloff.Evaluate(eclipseRatio);
                weatherSphere.fogLightFlareFalloff = Mathf.Lerp(weatherSphere.fogLightFlareFalloff, cachedValue, eclipseRatio);

                cachedColor = profile.cloudMoonColor.Evaluate(eclipseRatio);
                weatherSphere.cloudMoonColor = Color.Lerp(weatherSphere.cloudMoonColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.cloudMoonColor.a), cachedColor.a);

                cachedColor = profile.cloudTextureColor.Evaluate(eclipseRatio);
                weatherSphere.cloudTextureColor = Color.Lerp(weatherSphere.cloudTextureColor, new Color(cachedColor.r, cachedColor.g, cachedColor.b, weatherSphere.cloudTextureColor.a), cachedColor.a);


            }
        }


        public void GetAngles(out float orbit, out float declination)
        {
            if (!weatherSphere.timeModule || !weatherSphere.GetModule<CozySatelliteModule>())
            {
                orbit = 1;
                declination = 1;
                return;
            }

            weatherSphere.GetModule(out CozySatelliteModule satelliteModule);
            SatelliteProfile moon = satelliteModule.satellites[weatherSphere.GetModule<CozySatelliteModule>().mainMoon];
            int currentDayAdjusted = weatherSphere.timeModule.AbsoluteDay - moon.rotationPeriodOffset;

            int declinationMidPoint = moon.declinationPeriod / 2;
            orbit = currentDayAdjusted % moon.rotationPeriod / (float)moon.rotationPeriod;
            declination = currentDayAdjusted % declinationMidPoint / (float)declinationMidPoint;
        }

        public int DaysTillNextEclipse()
        {
            if (!weatherSphere.timeModule || !weatherSphere.GetModule<CozySatelliteModule>())
                return -1;


            weatherSphere.GetModule(out CozySatelliteModule satelliteModule);
            SatelliteProfile moon = satelliteModule.satellites[weatherSphere.GetModule<CozySatelliteModule>().mainMoon];

            int declinationMidPoint = moon.declinationPeriod / 2;
            int daysBetweenEclipses = moon.rotationPeriod * declinationMidPoint / GCD(moon.rotationPeriod, declinationMidPoint);

            int currentDayAdjusted = weatherSphere.timeModule.AbsoluteDay - moon.rotationPeriodOffset;
            int daysSinceLastEclipse = currentDayAdjusted % daysBetweenEclipses;
            int daysTillNextEclipse = (daysBetweenEclipses - daysSinceLastEclipse) % daysBetweenEclipses;

            return daysTillNextEclipse;
        }
        public void GetNextEclipseDate(out int day, out int year)
        {

            day = -1;
            year = -1;

            if (!weatherSphere.timeModule || !weatherSphere.GetModule<CozySatelliteModule>())
                return;


            weatherSphere.GetModule(out CozySatelliteModule satelliteModule);
            SatelliteProfile moon = satelliteModule.satellites[weatherSphere.GetModule<CozySatelliteModule>().mainMoon];

            int declinationMidPoint = moon.declinationPeriod / 2;
            int daysBetweenEclipses = moon.rotationPeriod * declinationMidPoint / GCD(moon.rotationPeriod, declinationMidPoint);

            int currentDayAdjusted = weatherSphere.timeModule.AbsoluteDay - moon.rotationPeriodOffset;
            int daysSinceLastEclipse = currentDayAdjusted % daysBetweenEclipses;
            int daysTillNextEclipse = (daysBetweenEclipses - daysSinceLastEclipse) % daysBetweenEclipses;

            day = daysTillNextEclipse % weatherSphere.timeModule.DaysPerYear;
            year = Mathf.FloorToInt(daysTillNextEclipse / weatherSphere.timeModule.DaysPerYear);

            return;
        }

        public float DeclinationCyclePosition()
        {
            return DeclinationCyclePosition(0);
        }

        public float DeclinationCyclePosition(int dayOffset)
        {
            if (!weatherSphere.timeModule || !weatherSphere.GetModule<CozySatelliteModule>())
                return -1;

            weatherSphere.GetModule(out CozySatelliteModule satelliteModule);
            SatelliteProfile moon = satelliteModule.satellites[weatherSphere.GetModule<CozySatelliteModule>().mainMoon];
            float halfDecPeriod = moon.declinationPeriod;

            return (weatherSphere.timeModule.AbsoluteDay - moon.rotationPeriodOffset + dayOffset) % halfDecPeriod / halfDecPeriod;
        }

        public float OrbitCyclePosition()
        {
            return OrbitCyclePosition(0);
        }

        public float OrbitCyclePosition(int dayOffset)
        {
            if (!weatherSphere.timeModule || !weatherSphere.GetModule<CozySatelliteModule>())
                return -1;

            weatherSphere.GetModule(out CozySatelliteModule satelliteModule);
            SatelliteProfile moon = satelliteModule.satellites[weatherSphere.GetModule<CozySatelliteModule>().mainMoon];

            return (weatherSphere.timeModule.AbsoluteDay - moon.rotationPeriodOffset + dayOffset) % (float)moon.rotationPeriod / (float)moon.rotationPeriod;
        }

        static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        public override void DeinitializeModule()
        {
            base.DeinitializeModule();

            Shader.SetGlobalVector(CozyShaderIDs.CZY_EclipseDirectionID, -Vector3.up);
            Shader.SetGlobalFloat(CozyShaderIDs.CZY_MoonSizeID, 1);
        }

    }
}