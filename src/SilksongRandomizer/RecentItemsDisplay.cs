using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using TMProOld;
using SilksongRandomizer;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.Versioning;

namespace SilksongRandomizer
{
    public static class RecentItemsDisplay
    {
        private class ItemNotification
        {
            public GameObject rootObject;
        }

        private static readonly ConcurrentQueue<(UnityEngine.Sprite sprite, string message)> pendingItemsQueue = new ConcurrentQueue<(Sprite, string)>();
        private static readonly List<ItemNotification> activeItemNotifications = new List<ItemNotification>();
        private const int MaxDisplayedItems = 5;
        private static TMP_FontAsset gameFont;
        private static Sprite bottomFleurSprite;
        private static Sprite topFleurSprite;
        private static bool assetsLoaded = false;

        private class DisplayHost : MonoBehaviour
        {
            private Transform container;
            private GameObject bottomFleurObj;
            private RectTransform bottomFleurRt;

            private void Awake()
            {
                // Check if the item display is enabled otherwise return and destroy
                if (RandomizerPlugin.Instance == null || !RandomizerPlugin.Instance.RecentItemsDisplayEnabled)
                {
                    Destroy(gameObject);
                    return;
                }

                // Set the canvas and use placeholder 9999 for rendering, ideally should go under the inventory layout
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                
                // Scale the UI based on resolution, this needs to be tested with other resolutions
                // 2560 x 1440, 1920 x 1080 or 16:9 seem to work just fine
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var containerObj = new GameObject("Container", typeof(RectTransform));
                container = containerObj.transform;
                container.SetParent(transform, false);
                ApplyLayout(containerObj.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-200, -20));

                // Use the bool to instead use the cached sprites + font
                if (!assetsLoaded)
                {
                    // Da Skong Font, there are like 9 or 10 other fonts, reminder to list them eventually
                    // Some letters are still not rendering properly needs to be addressed
                    gameFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(font => font != null && font.name == "TrajanPro-Bold SDF");

                    // Fleurs that make the window look a bit more native, might need to be resized a bit
                    bottomFleurSprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite => sprite != null && sprite.name == "bottom_fleur0006");
                    topFleurSprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sprite => sprite != null && sprite.name == "pause_top_fleur0008");

                    // Load the assets and set the bool to true so they don't grab every single time
                    assetsLoaded = true;
                }

                // Create header, set layout, and set text styling
                var headerObj = CreateUIObject("Header", container, out RectTransform headerRt);
                ApplyLayout(headerRt, Vector2.up, Vector2.up, new Vector2(-25, 0), new Vector2(340, 22));
                ConfigureText(headerObj, "Recent Items", 36, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);

                // Create top fleur, set layout
                if (topFleurSprite != null)
                {
                    var topFleurObj = CreateUIObject("TopFleur", container, out RectTransform topFleurRt);
                    ApplyLayout(topFleurRt, Vector2.up, Vector2.up, new Vector2(-50, -24), new Vector2(340, 85));

                    var topFleurImg = topFleurObj.AddComponent<Image>();
                    topFleurImg.sprite = topFleurSprite;
                    topFleurImg.type = Image.Type.Sliced;
                    topFleurImg.fillCenter = true;
                }

                // Create bottom fleur, set layout
                if (bottomFleurSprite != null)
                {
                    bottomFleurObj = CreateUIObject("BottomFleur", container, out bottomFleurRt);

                    var bottomFleurImg = bottomFleurObj.AddComponent<Image>();
                    bottomFleurImg.sprite = bottomFleurSprite;
                    bottomFleurImg.type = Image.Type.Sliced;
                    bottomFleurImg.fillCenter = true;
                }
            }

            private void Update()
            {
                // Check if the item display is enabled otherwise return and destroy
                if (RandomizerPlugin.Instance == null || !RandomizerPlugin.Instance.RecentItemsDisplayEnabled)
                {
                    Destroy(gameObject);
                    return;
                }

                // Skip update if the queue is empty
                if (pendingItemsQueue.IsEmpty)
                {
                    return;
                }

                // Additionally only dequeue if one item is found with a bool to set for one update frame
                bool itemQueued = false;

                while (pendingItemsQueue.TryDequeue(out var item))
                {
                    itemQueued = true;

                    if (activeItemNotifications.Count >= MaxDisplayedItems)
                    {
                        var old = activeItemNotifications[activeItemNotifications.Count - 1];
                        if (old.rootObject != null) Destroy(old.rootObject);
                        activeItemNotifications.RemoveAt(activeItemNotifications.Count - 1);
                    }

                    // Create the notification and set the layout position in the container
                    var root = CreateUIObject("Notification", container, out RectTransform rootRt);
                    ApplyLayout(rootRt, Vector2.up, Vector2.up, Vector2.zero, new Vector2(340, 48));

                    // Create the icon and set the anchoring and pivot
                    var iconObj = CreateUIObject("Icon", root.transform, out RectTransform iconRt);
                    iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = new Vector2(36, 0);
                    iconRt.sizeDelta = new Vector2(48, 48);

                    var iconImg = iconObj.AddComponent<Image>();
                    iconImg.sprite = item.sprite;
                    iconImg.preserveAspect = true;

                    // FUCK THIS SHIT I DONT GET HOW TO FLIP THE SPRITES PROPERLY EVERY SINGLE TIME MORCH HELPPPPPPPPPPP
                    if (item.sprite?.uv != null && item.sprite.uv.Length >= 2 && item.sprite.uv[0].y < item.sprite.uv[1].y)
                    {
                        iconRt.localScale = new Vector3(1, -1, 1);
                    }

                    // Create the text and set the anchoring + offset
                    var textObj = CreateUIObject("Text", root.transform, out RectTransform textRt);
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.offsetMin = new Vector2(76, 2);
                    textRt.offsetMax = new Vector2(-8, -2);

                    // Configure the text for the notiication
                    ConfigureText(textObj, item.message, 18, Color.white, TextAlignmentOptions.MidlineLeft);

                    // Insert the object into the list
                    activeItemNotifications.Insert(0, new ItemNotification { rootObject = root });
                }

                // Refresh the layout one update frame at a time
                if (itemQueued)
                {
                    RefreshLayout();
                }
            }

            // Refresh the layout to push the new items to the top
            private void RefreshLayout()
            {
                // Fleur + Top
                float startY = -109f;
                for (int i = 0; i< activeItemNotifications.Count; i++)
                {
                    var rt = activeItemNotifications[i].rootObject.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(-50, startY - (i * 50));
                }

                if (bottomFleurObj != null)
                {
                    if (activeItemNotifications.Count > 0)
                    {
                        bottomFleurObj.SetActive(true);
                        float bottomY = startY - (activeItemNotifications.Count * 50) - 4;
                        var bottomFleurRt = bottomFleurObj.GetComponent<RectTransform>();
                        ApplyLayout(bottomFleurRt, Vector2.up, Vector2.up, new Vector2(-50, bottomY), new Vector2(340, 66));
                    }
                    else
                    {
                        bottomFleurObj.SetActive(false);
                    }
                }
            }

            // Destroy the instance and clear the list
            private void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }

                activeItemNotifications.Clear();
            }

            // Create the UI Object in a methodized way since it was repeated several times
            private GameObject CreateUIObject(string name, Transform parent, out RectTransform rectTransform)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                rectTransform = obj.GetComponent<RectTransform>();
                return obj;
            }

            // Applying layout to the transforms methodized since it was repeated several times
            private void ApplyLayout(RectTransform rt,  Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2? sizeDelta = null)
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.anchoredPosition = anchoredPosition;
                if (sizeDelta.HasValue)
                {
                    rt.sizeDelta = sizeDelta.Value;
                }
            }

            // Configuring text methodized since it was repeated several times
            private void ConfigureText(GameObject obj, string text, float fontSize, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
            {
                var tmp = obj.AddComponent<TextMeshProUGUI>();
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = alignment;
                tmp.fontStyle = style;
                if (gameFont != null)
                {
                    tmp.font = gameFont;
                }
            }
        }

        private static DisplayHost instance;
        
        // Initialize the object and add DisplayHost
        public static void Initialize()
        {
            if (RandomizerPlugin.Instance == null || !RandomizerPlugin.Instance.RecentItemsDisplayEnabled)
            {
                return;
            }

            if (instance != null)
            {
                return;
            }

            var obj = new GameObject("Silksong.RecentItemsDisplayHost");
            Object.DontDestroyOnLoad(obj);
            instance = obj.AddComponent<DisplayHost>();
        }

        // Taken from the previous RecentItemsDisplay plugin, we want to queue to avoid any crashing
        internal static void QueueItem(UnityEngine.Sprite sprite, string message)
        {
            if (RandomizerPlugin.Instance == null || !RandomizerPlugin.Instance.RecentItemsDisplayEnabled)
            {
                return;
            }

            Initialize();
            pendingItemsQueue.Enqueue((sprite, message));
        }
    }
}