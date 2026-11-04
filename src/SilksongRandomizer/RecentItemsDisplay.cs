using System.Collections.Generic;
using System.Linq;
using SilksongRandomizer.AlphabetMode;
using TMProOld;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongRandomizer
{
    public static class RecentItemsDisplay
    {
        private const int MaxDisplayedItems = 5;
        private static TMP_FontAsset gameFont;
        private static Sprite bottomFleurSprite;
        private static Sprite topFleurSprite;
        private static DisplayHost instance;
        private static SaveState currentSave;
        private static readonly Queue<(Sprite sprite, string message)> pendingItems =
            new Queue<(Sprite, string)>();

        internal static void Update()
        {
            SaveState save = SaveState.Instance;
            if (!ReferenceEquals(currentSave, save))
            {
                Reset();
                currentSave = save;
            }
            if (save?.IsRoomBound != true)
            {
                Reset();
                return;
            }

            var manager = GameManager.SilentInstance;
            bool visible = RandomizerPlugin.Instance?.RecentItemsDisplayEnabled == true &&
                manager != null && manager.IsGameplayScene() &&
                !manager.IsInSceneTransition && PlayerData.instance != null &&
                !PlayerData.instance.isInventoryOpen && !RandomizerPlugin.IsConnectionGuiOpen;
            if (instance != null) instance.gameObject.SetActive(visible);
            if (!visible) return;
            Initialize();
            while (pendingItems.Count > 0)
            {
                var item = pendingItems.Dequeue();
                instance.AddItem(item.sprite, item.message);
            }
        }

        // Initialize the object and add DisplayHost
        public static void Initialize()
        {
            if (RandomizerPlugin.Instance?.RecentItemsDisplayEnabled != true || instance != null) return;
            var obj = new GameObject("Silksong.RecentItemsDisplayHost");
            Object.DontDestroyOnLoad(obj);
            instance = obj.AddComponent<DisplayHost>();
        }

        internal static void QueueItem(Sprite sprite, string message)
        {
            if (SaveState.Instance?.IsRoomBound != true) return;
            if (!ReferenceEquals(currentSave, SaveState.Instance))
            {
                Reset();
                currentSave = SaveState.Instance;
            }
            pendingItems.Enqueue((sprite, message));
            while (pendingItems.Count > MaxDisplayedItems) pendingItems.Dequeue();
        }

        internal static void Reset()
        {
            pendingItems.Clear();
            if (instance != null)
            {
                instance.gameObject.SetActive(false);
                Object.Destroy(instance.gameObject);
                instance = null;
            }
        }

        private class DisplayHost : MonoBehaviour
        {
            private readonly List<GameObject> activeItemNotifications = new List<GameObject>();
            private Transform container;
            private GameObject bottomFleurObj;
            private RectTransform bottomFleurRt;

            private void Awake()
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
                canvas.sortingOrder = 9999;
                // Scale the UI based on resolution
                // 2560 x 1440, 1920 x 1080 or 16:9 seem to work just fine
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                var containerObj = CreateUIObject("Container", transform, out var containerRt);
                container = containerObj.transform;
                ApplyLayout(containerRt, Vector2.one, Vector2.one, new Vector2(-200, -20));

                // Da Skong Font, there are like 9 or 10 other fonts, reminder to list them eventually
                if (gameFont == null)
                    gameFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(font =>
                        font != null && font.name == "TrajanPro-Bold SDF");
                // Fleurs that make the window look a bit more native, might need to be resized a bit
                if (bottomFleurSprite == null || topFleurSprite == null)
                {
                    var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    bottomFleurSprite = sprites.FirstOrDefault(sprite => sprite != null && sprite.name == "bottom_fleur0006");
                    topFleurSprite = sprites.FirstOrDefault(sprite => sprite != null && sprite.name == "pause_top_fleur0008");
                }

                // Create header, set layout, and set text styling
                var headerObj = CreateUIObject("Header", container, out var headerRt);
                ApplyLayout(headerRt, Vector2.up, Vector2.up, new Vector2(-25, 0), new Vector2(340, 44));
                ConfigureText(headerObj, "Recent Items", 36, TextAlignmentOptions.Left, FontStyles.Bold);

                // Create top fleur, set layout
                if (topFleurSprite != null)
                {
                    var topFleurObj = CreateUIObject("TopFleur", container, out var topFleurRt);
                    ApplyLayout(topFleurRt, Vector2.up, Vector2.up, new Vector2(-50, -24), new Vector2(340, 85));
                    var topFleurImg = topFleurObj.AddComponent<Image>();
                    topFleurImg.sprite = topFleurSprite;
                    topFleurImg.type = Image.Type.Sliced;
                    topFleurImg.raycastTarget = false;
                }
                // Create bottom fleur, set layout
                if (bottomFleurSprite != null)
                {
                    bottomFleurObj = CreateUIObject("BottomFleur", container, out bottomFleurRt);
                    var bottomFleurImg = bottomFleurObj.AddComponent<Image>();
                    bottomFleurImg.sprite = bottomFleurSprite;
                    bottomFleurImg.type = Image.Type.Sliced;
                    bottomFleurImg.raycastTarget = false;
                    bottomFleurObj.SetActive(false);
                }
            }

            internal void AddItem(Sprite sprite, string message)
            {
                if (activeItemNotifications.Count >= MaxDisplayedItems)
                {
                    var old = activeItemNotifications[activeItemNotifications.Count - 1];
                    old.SetActive(false);
                    Destroy(old);
                    activeItemNotifications.RemoveAt(activeItemNotifications.Count - 1);
                }

                // Create the notification and set the layout position in the container
                var root = CreateUIObject("Notification", container, out var rootRt);
                ApplyLayout(rootRt, Vector2.up, Vector2.up, Vector2.zero, new Vector2(340, 48));
                // Create the icon and set the anchoring and pivot
                var iconObj = CreateUIObject("Icon", root.transform, out var iconRt);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = new Vector2(36, 0);
                iconRt.sizeDelta = new Vector2(48, 48);
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.preserveAspect = true;
                // FUCK THIS SHIT I DONT GET HOW TO FLIP THE SPRITES PROPERLY EVERY SINGLE TIME MORCH HELPPPPPPPPPPP
                iconImg.useSpriteMesh = true; // lmao
                iconImg.raycastTarget = false;

                // Create the text and set the anchoring + offset
                var textObj = CreateUIObject("Text", root.transform, out var textRt);
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(76, 2);
                textRt.offsetMax = new Vector2(-8, -2);
                // Configure the text for the notiication
                ConfigureText(textObj, message.Replace("_", " "), 18, TextAlignmentOptions.MidlineLeft);
                // Insert the object into the list
                activeItemNotifications.Insert(0, root);
                RefreshLayout();
            }

            // Refresh the layout to push the new items to the top
            private void RefreshLayout()
            {
                // Fleur + Top
                const float startY = -109f;
                for (int i = 0; i < activeItemNotifications.Count; i++)
                    activeItemNotifications[i].GetComponent<RectTransform>().anchoredPosition =
                        new Vector2(-50, startY - i * 50);
                if (bottomFleurObj != null)
                {
                    bottomFleurObj.SetActive(activeItemNotifications.Count > 0);
                    ApplyLayout(bottomFleurRt, Vector2.up, Vector2.up,
                        new Vector2(-50, startY - activeItemNotifications.Count * 50 - 4), new Vector2(340, 66));
                }
            }

            private void OnDestroy()
            {
                if (instance == this) instance = null;
            }

            // Create the UI Object in a methodized way since it was repeated several times
            private static GameObject CreateUIObject(string name, Transform parent, out RectTransform rectTransform)
            {
                var obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                rectTransform = obj.GetComponent<RectTransform>();
                return obj;
            }

            // Applying layout to the transforms methodized since it was repeated several times
            private static void ApplyLayout(RectTransform rt, Vector2 anchor, Vector2 pivot,
                Vector2 anchoredPosition, Vector2? sizeDelta = null)
            {
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.anchoredPosition = anchoredPosition;
                if (sizeDelta.HasValue) rt.sizeDelta = sizeDelta.Value;
            }

            // Configuring text methodized since it was repeated several times
            private static void ConfigureText(GameObject obj, string text, float fontSize,
                TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
            {
                var tmp = obj.AddComponent<TextMeshProUGUI>();
                if (gameFont != null) tmp.font = gameFont;
                tmp.text = AlphabetModeManager.FilterDirectText(text);
                tmp.fontSize = fontSize;
                tmp.color = Color.white;
                tmp.alignment = alignment;
                tmp.fontStyle = style;
                tmp.richText = false;
                tmp.raycastTarget = false;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 12;
                tmp.fontSizeMax = fontSize;
                tmp.OverflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }
}
