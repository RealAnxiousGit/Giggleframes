using UnityEngine;
using UnityEngine.SceneManagement;

namespace GigglelandPerf
{
    /// Linear fog that hides the hard edge Draw Distance leaves. It goes in right before the main camera
    /// renders, next to the far-plane cap. RenderSettings belong to the scene, so the level's own fog is
    /// remembered once per scene and put back whenever the option has nothing to do.
    static class DistanceFog
    {
        const int Off = 0, Sync = 1;
        const float StartFraction = 0.55f;   // the fog begins at this share of its full distance
        const float SyncMargin = 0.95f;      // fully thick just before the far plane cuts the world off

        static int sceneHandle = -1;
        static bool captured, active;
        static bool baseFog;
        static FogMode baseMode;
        static float baseStart, baseEnd;
        static Color baseColor;

        /// Distance at which the fog is fully thick; 0 means no fog from this option.
        static float FullDistance(Camera cam, float farCap)
        {
            int v = Settings.DistanceFog.I;
            if (v == Off) return 0f;
            if (v == Sync) return farCap > 0f ? cam.farClipPlane * SyncMargin : 0f;
            return 5f * v;   // the slider's steps line up with Draw Distance: 2 = 10 m ... 9 = 45 m
        }

        static void Capture()
        {
            baseFog = RenderSettings.fog;
            baseMode = RenderSettings.fogMode;
            baseStart = RenderSettings.fogStartDistance;
            baseEnd = RenderSettings.fogEndDistance;
            baseColor = RenderSettings.fogColor;
            captured = true;
        }

        static void Restore()
        {
            RenderSettings.fog = baseFog;
            RenderSettings.fogMode = baseMode;
            RenderSettings.fogStartDistance = baseStart;
            RenderSettings.fogEndDistance = baseEnd;
            RenderSettings.fogColor = baseColor;
            active = false;
        }

        /// The level's fog colour if it has one, otherwise what the camera sees past the far plane.
        static Color PickColor(Camera cam)
        {
            if (baseFog) return baseColor;
            if (cam.clearFlags == CameraClearFlags.SolidColor) return cam.backgroundColor;
            return RenderSettings.ambientSkyColor;
        }

        public static void BeforeMainCamera(Camera cam, float farCap)
        {
            int scene = SceneManager.GetActiveScene().handle;
            if (scene != sceneHandle)
            {
                sceneHandle = scene;
                captured = active = false;
            }

            float full = FullDistance(cam, farCap);
            if (full <= 0f)
            {
                if (active) Restore();
                return;
            }

            if (!captured) Capture();
            active = true;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = full * StartFraction;
            RenderSettings.fogEndDistance = full;
            RenderSettings.fogColor = PickColor(cam);
        }

        public static string Status =>
            $"fog={RenderSettings.fog}/{RenderSettings.fogMode}/{RenderSettings.fogStartDistance:F1}-{RenderSettings.fogEndDistance:F1}/{RenderSettings.fogColor}";
    }
}
