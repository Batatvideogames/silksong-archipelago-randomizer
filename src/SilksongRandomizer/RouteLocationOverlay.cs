using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace SilksongRandomizer
{
    internal static class RouteLocationOverlay
    {
        private static ConfigEntry<bool> visible, showTrackerIds, showCoordinates;
        private static ConfigEntry<float> scale, topOffset;
        private static RoomGeometry geometry;
        private static RoomGeometry.Reading reading;
        private static string footer = "";
        private static bool loadFailed;
        private static float nextRefresh;
        private static GUIStyle headingStyle, detailStyle, smallStyle;

        internal static void Initialize(ConfigFile config)
        {
            const string section = "Route Location Overlay";
            visible = config.Bind(section, "Enabled", false,
                ModMenuOptions.Describe("Room Overlay", "Show Room Overlay", "Show your current area, room and mapped subroom."));
            showTrackerIds = config.Bind(section, "Show Tracker IDs", false,
                ModMenuOptions.Describe("Room Overlay", "Show Tracker IDs", "Add the technical room IDs used in Universal Tracker and mapping reports."));
            showCoordinates = config.Bind(section, "Show Coordinates", false,
                ModMenuOptions.Describe("Room Overlay", "Show Coordinates", "Add Hornet's exact position to the overlay for mapping reports."));
            scale = config.Bind(section, "Scale", 1f,
                ModMenuOptions.Describe("Room Overlay", "Size (1 = Normal)",
                    "1 is normal size, 0.75 is smaller and 1.25 is larger.", new AcceptableValueRange<float>(0.6f, 2f)));
            topOffset = config.Bind(section, "Top Offset", 24f,
                ModMenuOptions.Describe("Room Overlay", "Distance from Top",
                    "Move the overlay down from the top of the screen. Default: 24.", new AcceptableValueRange<float>(0f, 600f)));
        }

        private static bool CanDraw()
        {
            if (visible?.Value != true || RandomizerPlugin.IsConnectionGuiOpen)
                return false;
            var manager = GameManager.SilentInstance;
            var hero = HeroController.instance;
            return manager != null && manager.IsGameplayScene() && !manager.isPaused &&
                !manager.IsLoadingSceneTransition && !manager.IsInSceneTransition &&
                hero != null && hero.isHeroInPosition && !hero.cState.dead &&
                PlayerData.instance?.isInventoryOpen != true;
        }

        internal static void Update()
        {
            if (!CanDraw())
            {
                reading = null;
                nextRefresh = 0;
                return;
            }
            if (loadFailed || Time.unscaledTime < nextRefresh)
                return;
            if (geometry == null)
            {
                try
                {
                    geometry = RoomGeometry.Load(BundledMapLogic.RoomGeometry);
                }
                catch (Exception error)
                {
                    loadFailed = true;
                    RandomizerPlugin.Log.LogError("Could not load route overlay locations: " + error);
                    return;
                }
            }
            nextRefresh = Time.unscaledTime + 0.1f;
            var position = HeroController.instance.transform.position;
            string scene = GameManager.SilentInstance.sceneName ?? "";
            reading = geometry.Locate(scene, position.x, position.y);
            footer = scene;
            if (showCoordinates.Value)
                footer += string.Format(CultureInfo.InvariantCulture, "   x {0:0.0}  y {1:0.0}", position.x, position.y);
            if (showTrackerIds.Value && !string.IsNullOrEmpty(reading.Tracker))
                footer += "\n" + reading.Tracker;
        }

        internal static void Draw()
        {
            if (reading == null || !CanDraw())
                return;
            if (headingStyle == null)
            {
                headingStyle = MakeStyle(21, Color.white, FontStyle.Bold);
                detailStyle = MakeStyle(18, new Color(0.89f, 0.9f, 0.92f), FontStyle.Normal);
                smallStyle = MakeStyle(13, new Color(0.68f, 0.73f, 0.78f), FontStyle.Normal);
            }
            float factor = Mathf.Clamp(Screen.height / 1080f, 0.65f, 2f) * scale.Value;
            float screenWidth = Screen.width / factor;
            float width = Mathf.Min(780f, screenWidth - 32f);
            if (width <= 30)
                return;
            float innerWidth = width - 32;
            float h1 = headingStyle.CalcHeight(new GUIContent(reading.Heading), innerWidth);
            float h2 = detailStyle.CalcHeight(new GUIContent(reading.Detail), innerWidth);
            float h3 = smallStyle.CalcHeight(new GUIContent(footer), innerWidth);
            float height = 26 + h1 + h2 + h3;
            float y = Mathf.Min(topOffset.Value, Mathf.Max(0, Screen.height / factor - height));
            float x = (screenWidth - width) * 0.5f;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            int oldDepth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(factor, factor, 1));
                GUI.depth = -80;
                GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.8f);
                GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 16, y + 10, innerWidth, h1), reading.Heading, headingStyle);
                GUI.Label(new Rect(x + 16, y + 13 + h1, innerWidth, h2), reading.Detail, detailStyle);
                GUI.Label(new Rect(x + 16, y + 17 + h1 + h2, innerWidth, h3), footer, smallStyle);
            }
            finally
            {
                GUI.matrix = oldMatrix;
                GUI.color = oldColor;
                GUI.depth = oldDepth;
            }
        }

        private static GUIStyle MakeStyle(int size, Color color, FontStyle weight)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = size,
                fontStyle = weight,
                wordWrap = true,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = color;
            return style;
        }
    }
}
