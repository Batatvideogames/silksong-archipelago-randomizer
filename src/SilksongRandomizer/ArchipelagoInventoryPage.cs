using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TeamCherry.NestedFadeGroup;
using TMProOld;
using UnityEngine;

namespace SilksongRandomizer
{
    internal sealed class ArchipelagoInventoryPage : InventoryPane
    {
        private enum OverviewAction { Warp, Destination, PassTime, Flea }
        private sealed class IconView
        {
            internal SpriteRenderer Renderer;
            internal string Name;
            internal Sprite Source;
            internal string FleaName;
        }

        private sealed class RowView
        {
            internal TextMeshPro Title;
            internal TextMeshPro Detail;
            internal IconView Icon;
        }

        private InventoryPaneList owner;
        private InventoryPaneInput input;
        private Sprite tabIcon;
        private SpriteRenderer soulDivider;
        private Material textMaterial;
        private Material badgeMaterial;
        private Material badgeOutlineMaterial;
        private int iconSortingOrder;
        private static Sprite FallbackIcon => RandomizerPlugin.Instance?.MapCheckIcon ?? RandomizerPlugin.Instance?.ArchipelagoIcon;
        private readonly List<TextMeshPro> labels = new List<TextMeshPro>();
        private readonly List<RowView> rows = new List<RowView>();
        private readonly List<IconView> icons = new List<IconView>();
        private List<ArchipelagoInventorySheet> sheets = new List<ArchipelagoInventorySheet>();
        private QuestCompleteTotalGroup soul;
        private SaveState displayedSave;
        private int page;
        private int labelIndex;
        private int rowIndex;
        private int iconIndex;
        private float refreshAt;
        private bool reportedError;
        private readonly List<IconView> fleaButtons = new List<IconView>();
        private readonly AudioSource[] fleaVoices = new AudioSource[8];
        private int nextFleaVoice;
        private float lastOrdinaryFleaTime = float.NegativeInfinity;
        private bool actionsFocused;
        private bool waitingForActionRelease;
        private OverviewAction selectedAction;
        private int selectedFlea = -1;
        private float actionsReadyAt;
        private float nextTimePass;
        private float timePassPulseUntil;
        private TextMeshPro timeButton;
        private static readonly Rect TimeButtonRect = new Rect(1.7f, -13.6f, 5.2f, .65f);
        private static readonly Rect WarpButtonRect = new Rect(-7.6f, -13.1f, 5.3f, .55f);
        private static readonly Rect DestinationButtonRect = new Rect(-1.9f, -13.1f, 3.1f, .55f);

        public override bool IsAvailable => SaveState.Instance?.IsRoomBound == true;

        internal Sprite TabIcon
        {
            get
            {
                if (tabIcon != null) return tabIcon;
                Sprite source = RandomizerPlugin.Instance?.InventoryIcon ?? FallbackIcon;
                if (source == null) return null;
                float size = Mathf.Max(source.bounds.size.x, source.bounds.size.y);
                tabIcon = Sprite.Create(source.texture, source.rect,
                    new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height),
                    source.pixelsPerUnit * size / .9f, 0, SpriteMeshType.FullRect);
                tabIcon.name = "Archipelago Tab";
                tabIcon.hideFlags = HideFlags.HideAndDontSave;
                return tabIcon;
            }
        }

        internal void Initialize(InventoryPaneList list, TextMeshPro template)
        {
            textMaterial = template.fontSharedMaterial;
            iconSortingOrder = template.renderer.sortingOrder;
            badgeMaterial = new Material(textMaterial) { hideFlags = HideFlags.HideAndDontSave };
            badgeMaterial.SetFloat("_OutlineWidth", 0f);
            badgeMaterial.SetColor("_FaceColor", Color.white);
            badgeOutlineMaterial = new Material(badgeMaterial) { hideFlags = HideFlags.HideAndDontSave };
            badgeOutlineMaterial.SetColor("_FaceColor", Color.black);
            badgeOutlineMaterial.SetColor("_OutlineColor", Color.black);
            badgeOutlineMaterial.SetFloat("_OutlineWidth", .5f);
            badgeOutlineMaterial.SetFloat("_FaceDilate", .2f);
            badgeOutlineMaterial.EnableKeyword("OUTLINE_ON");
            owner = list;
            owner.ClosingInventory += EndInventory;
            input = gameObject.AddComponent<InventoryPaneInput>();
            input.enabled = false;
            var inventoryAudio = (AudioSource)AccessTools.Field(typeof(InventoryPaneList), "audioSourcePrefab").GetValue(owner);
            for (int i = 0; i < fleaVoices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 0f;
                voice.ignoreListenerPause = true;
                voice.outputAudioMixerGroup = inventoryAudio.outputAudioMixerGroup;
                fleaVoices[i] = voice;
            }
            AccessTools.Field(typeof(InventoryPaneInput), "allowHorizontalSelection").SetValue(input, true);
            AccessTools.Field(typeof(InventoryPaneInput), "allowVerticalSelection").SetValue(input, true);
            AccessTools.Field(typeof(InventoryPaneInput), "allowRepeat").SetValue(input, true);
            OnInputLeft += PreviousTab;
            OnInputRight += NextTab;
            OnInputUp += PreviousSheet;
            OnInputDown += NextSheet;
            for (int i = 0; i < ArchipelagoInventoryModel.SoulRowsPerColumn * 3 + 15; i++) labels.Add(CreateText(template, "Heading"));
            for (int i = 0; i < ArchipelagoInventoryModel.SoulsPerPage; i++)
                rows.Add(new RowView {
                    Title = CreateText(template, "Item Name"),
                    Detail = CreateText(template, "Item Details"),
                    Icon = CreateIcon(template)
                });
            for (int i = 0; i < 90; i++) icons.Add(CreateIcon(template));
            soulDivider = CreateIcon(template).Renderer;
            soulDivider.gameObject.name = "Soul Checklist Divider";
            soulDivider.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
            soulDivider.transform.localPosition = new Vector3(4.3f, -4.22f, -3.4f);
            soulDivider.transform.localScale = new Vector3(23.8f, .015f, 1f);
            soulDivider.color = new Color(.4f, .4f, .4f, 1f);
            var fade = gameObject.AddComponent<NestedFadeGroup>();
            fade.AddMissingBridgeComponents();
            fade.AlphaSelf = 0f;
        }

        private IconView CreateIcon(TextMeshPro template)
        {
            var go = new GameObject("Item Icon");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID = template.renderer.sortingLayerID;
            renderer.sortingOrder = template.renderer.sortingOrder;
            renderer.enabled = false;
            return new IconView { Renderer = renderer };
        }

        private TextMeshPro CreateText(TextMeshPro template, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            var renderer = text.renderer;
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.enableAutoSizing = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.OverflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.text = "";
            renderer.sortingLayerID = template.renderer.sortingLayerID;
            renderer.sortingOrder = template.renderer.sortingOrder;
            return text;
        }

        private static void SetText(TextMeshPro text, string value, float x, float y,
            float width, float height, float size, float brightness = 1f,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var rect = (RectTransform)text.transform;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.localPosition = new Vector3(x, y, -3.3f);
            text.textContainer.pivot = new Vector2(0f, 1f);
            text.textContainer.size = new Vector2(width, height);
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.fontSize = size;
            text.fontSizeMin = size * .85f;
            text.fontSizeMax = size;
            text.color = new Color(brightness, brightness, brightness, text.color.a);
            text.richText = false;
            text.text = ArchipelagoInventoryModel.Plain(value);
        }

        private void Label(string value, float x, float y, float width, float height, float size, float brightness = 1f,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = labels[labelIndex++];
            label.fontSharedMaterial = textMaterial;
            SetText(label, value, x, y, width, height, size, brightness, alignment);
        }

        private void SetIcon(IconView view, string name, bool dim, float x, float y, float size)
        {
            var renderer = view.Renderer;
            if (view.Name != name || (name != null && (view.Source == null || view.Source == FallbackIcon)))
            {
                view.Name = name;
                view.Source = name == null ? null : ItemIcons.GetSprite(name, FallbackIcon);
            }
            renderer.sortingOrder = iconSortingOrder;
            renderer.sprite = view.Source;
            renderer.enabled = renderer.sprite != null && name != null;
            if (renderer.sprite == null) return;
            var bounds = renderer.sprite.bounds;
            float scale = size / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.y));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.transform.localPosition = new Vector3(x - bounds.center.x * scale, y - bounds.center.y * scale, -3.4f);
            float brightness = dim ? .3f : 1f;
            renderer.color = new Color(brightness, brightness, brightness, renderer.color.a);
            string number = ArchipelagoInventoryModel.BossIconBadge(name);
            if (number != null)
            {
                float width = size * .6f;
                float height = size * .8f;
                float inset = size * .02f;
                float right = x + bounds.size.x * scale * .5f - inset;
                float bottom = y - bounds.size.y * scale * .5f + inset;
                for (int layer = 0; layer < 2; layer++)
                {
                    var badge = labels[labelIndex];
                    Label(number, right - width, bottom + height, width, height,
                        Mathf.Max(4.2f, size * 5.2f), 1f, TextAlignmentOptions.BottomRight);
                    badge.enableAutoSizing = false;
                    badge.fontSharedMaterial = layer == 0 ? badgeOutlineMaterial : badgeMaterial;
                    badge.UpdateMeshPadding();
                    badge.ForceMeshUpdate(true);
                    if (badge.textInfo.characterCount > 0)
                    {
                        var character = badge.textInfo.characterInfo[0];
                        var glyph = character.textElement;
                        if (glyph != null)
                        {
                            float glyphRight = character.origin + (glyph.xOffset + glyph.width) * character.scale;
                            float glyphBottom = character.baseLine +
                                (badge.font.fontInfo.Baseline + glyph.yOffset - glyph.height) * character.scale;
                            var position = badge.transform.localPosition;
                            badge.transform.localPosition = new Vector3(right - glyphRight, bottom - glyphBottom, position.z);
                        }
                    }
                    badge.renderer.sortingOrder = renderer.sortingOrder + layer + 1;
                }
            }
        }

        private void Icon(ArchipelagoInventoryRow row, float x, float y, float size)
        {
            SetIcon(icons[iconIndex++], row.Icon, row.Dim, x, y, size);
        }

        private void Row(ArchipelagoInventoryRow row, float x, float y, float width,
            float size = 4.1f, float iconSize = .8f, float detailHeight = .45f)
        {
            var view = rows[rowIndex++];
            float inset = row.Icon == null ? 0f : iconSize + .25f;
            SetIcon(view.Icon, row.Icon, row.Dim, x + iconSize / 2, y - .4f, iconSize);
            SetText(view.Title, row.Title, x + inset, y, width - inset, .55f, size, row.Dim ? .5f : 1f);
            SetText(view.Detail, row.Detail, x + inset, y - .58f, width - inset, detailHeight,
                size * .76f, row.Dim ? .45f : .7f);
            if (row.CompletionText != null)
            {
                view.Detail.color = Color.white;
                view.Detail.richText = true;
                view.Detail.text = "<color=#" + (row.Dim ? "737373" : "FFFFFF") + ">" +
                    ArchipelagoInventoryModel.Plain(row.Detail) + "</color>  |  <color=#" +
                    (row.LocationCompleted == true ? "FFFFFF" : "737373") + ">" +
                    ArchipelagoInventoryModel.Plain(row.CompletionText) + "</color>";
            }
        }

        public override void PaneStart()
        {
            base.PaneStart();
            owner.CloseBlocked = false;
            owner.InSubMenu = false;
            owner.IsPaneMoveCustom = false;
            owner.CanSwitchPanes = true;
            page = 0;
            actionsFocused = waitingForActionRelease = false;
            selectedAction = OverviewAction.Warp;
            selectedFlea = -1;
            lastOrdinaryFleaTime = float.NegativeInfinity;
            timePassPulseUntil = 0f;
            actionsReadyAt = Time.unscaledTime + .3f;
            Refresh();
            input.enabled = true;
        }

        public override void PaneEnd()
        {
            RestoreBrowsing();
            input.enabled = false;
            foreach (var voice in fleaVoices) voice?.Stop();
            input.CancelRepeat();
            base.PaneEnd();
            sheets.Clear();
            displayedSave = null;
        }

        private void EndInventory()
        {
            if (IsPaneActive) PaneEnd();
        }

        private void OnDestroy()
        {
            if (owner != null) owner.ClosingInventory -= EndInventory;
            OnInputLeft -= PreviousTab;
            OnInputRight -= NextTab;
            OnInputUp -= PreviousSheet;
            OnInputDown -= NextSheet;
            if (tabIcon != null) Destroy(tabIcon);
            if (soulDivider != null) Destroy(soulDivider.sprite);
            if (badgeMaterial != null) Destroy(badgeMaterial);
            if (badgeOutlineMaterial != null) Destroy(badgeOutlineMaterial);
        }

        private void PreviousTab() => SwitchTab("MOVE PANE L");
        private void NextTab() => SwitchTab("MOVE PANE R");

        private void SwitchTab(string moveEvent)
        {
            if (!IsPaneActive || actionsFocused || waitingForActionRelease || owner == null || !owner.CanSwitchPanes || owner.InSubMenu) return;
            input.CancelRepeat();
            FSMUtility.SendEventToGameObject(owner.gameObject, moveEvent);
        }

        private void PreviousSheet() => Browse(-1);
        private void NextSheet() => Browse(1);

        private void Browse(int direction)
        {
            if (!IsPaneActive || !IsAvailable || actionsFocused || waitingForActionRelease || sheets.Count < 2) return;
            page = (page + direction + sheets.Count) % sheets.Count;
            Render();
        }

        private void Update()
        {
            if (!IsPaneActive || !IsAvailable)
            {
                if (actionsFocused || waitingForActionRelease) RestoreBrowsing();
                return;
            }
            if (Time.unscaledTime >= refreshAt) Refresh();
            UpdateActions();
            UpdateTimeButtonFeedback();
        }

        private void UpdateTimeButtonFeedback()
        {
            if (timeButton == null) return;
            float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01((timePassPulseUntil - Time.unscaledTime) / .45f));
            float size = 4f + .6f * pulse;
            timeButton.fontSize = size;
            timeButton.fontSizeMin = size * .85f;
            timeButton.fontSizeMax = size;
            float brightness = .85f + .15f * pulse;
            timeButton.color = new Color(brightness, brightness, brightness, timeButton.color.a);
        }

        private void FocusActions(OverviewAction action = OverviewAction.Warp, int flea = -1)
        {
            actionsFocused = true;
            waitingForActionRelease = true;
            selectedAction = action;
            selectedFlea = flea;
            input.CancelRepeat();
            input.enabled = false;
            owner.CanSwitchPanes = false;
            owner.InSubMenu = true;
            owner.CloseBlocked = true;
            Render();
        }

        private void RestoreBrowsing()
        {
            actionsFocused = waitingForActionRelease = false;
            if (owner == null) return;
            owner.CanSwitchPanes = true;
            owner.InSubMenu = false;
            owner.CloseBlocked = false;
            input.CancelRepeat();
            input.enabled = IsPaneActive;
        }

        private void UpdateActions()
        {
            if (sheets.Count == 0 || sheets[page].Key != "overview" ||
                Time.unscaledTime < actionsReadyAt || InventoryPaneInput.IsInputBlocked || CheatManager.IsOpen) return;
            GameManager manager = GameManager.UnsafeInstance;
            if (manager == null || !manager.IsGameplayScene() || manager.IsLoadingSceneTransition ||
                manager.IsInSceneTransition || PlayerData.instance == null || PlayerData.instance.health <= 0)
            {
                if (actionsFocused || waitingForActionRelease) RestoreBrowsing();
                return;
            }
            var controls = manager.inputHandler?.inputActions;
            if (controls == null) return;
            if (waitingForActionRelease)
            {
                if (Platform.Current.GetMenuAction(controls) != Platform.MenuActions.None ||
                    Platform.Current.GetMenuAction(controls, isContinuous: true) != Platform.MenuActions.None ||
                    controls.Left.WasPressed || controls.Right.WasPressed || controls.Up.WasPressed || controls.Down.WasPressed ||
                    controls.Left.IsPressed || controls.Right.IsPressed || controls.Up.IsPressed || controls.Down.IsPressed ||
                    InventoryPaneInput.GetInventoryInputPressed(controls) != InventoryPaneList.PaneTypes.None ||
                    Input.GetMouseButton(0)) return;
                waitingForActionRelease = false;
                if (!actionsFocused) RestoreBrowsing();
                return;
            }
            if (!actionsFocused && (owner.InSubMenu || owner.CloseBlocked)) return;
            if (Input.GetMouseButtonDown(0))
            {
                HandleActionClick();
                return;
            }
            var action = Platform.Current.GetMenuAction(controls);
            if (!actionsFocused)
            {
                if (!owner.InSubMenu && !owner.CloseBlocked && action == Platform.MenuActions.Submit)
                    FocusActions();
                return;
            }
            if (action == Platform.MenuActions.Cancel || InventoryPaneInput.IsInventoryButtonPressed(controls))
            {
                actionsFocused = false;
                waitingForActionRelease = true;
                Render();
                return;
            }
            if (action == Platform.MenuActions.Submit)
            {
                ActivateAction();
                waitingForActionRelease = true;
                return;
            }
            var previousAction = selectedAction;
            int previousFlea = selectedFlea;
            if (controls.Down.WasPressed && selectedAction == OverviewAction.Flea)
                selectedAction = OverviewAction.Warp;
            else if (controls.Up.WasPressed && fleaButtons.Count > 0)
            {
                selectedAction = OverviewAction.Flea;
                selectedFlea = Math.Max(0, selectedFlea);
            }
            else if (controls.Left.WasPressed)
            {
                if (selectedAction == OverviewAction.Flea && selectedFlea > 0) selectedFlea--;
                else if (selectedAction != OverviewAction.Warp) selectedAction--;
            }
            else if (controls.Right.WasPressed)
            {
                if (selectedAction == OverviewAction.Flea)
                    selectedFlea = Math.Min(fleaButtons.Count - 1, selectedFlea + 1);
                else if (selectedAction != OverviewAction.PassTime || fleaButtons.Count > 0)
                {
                    selectedAction++;
                    if (selectedAction == OverviewAction.Flea) selectedFlea = 0;
                }
            }
            if (previousAction != selectedAction || previousFlea != selectedFlea) Render();
        }

        private void HandleActionClick()
        {
            Camera camera = GameCameras.SilentInstance?.hudCamera;
            if (camera == null) return;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(transform.forward, transform.TransformPoint(0f, 0f, -3.3f));
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 point = transform.InverseTransformPoint(ray.GetPoint(distance));
            if (TimeButtonRect.Contains(new Vector2(point.x, point.y)))
            {
                FocusActions(OverviewAction.PassTime);
                ActivateAction();
                return;
            }
            if (WarpButtonRect.Contains(new Vector2(point.x, point.y)) ||
                DestinationButtonRect.Contains(new Vector2(point.x, point.y)))
            {
                FocusActions(WarpButtonRect.Contains(new Vector2(point.x, point.y))
                    ? OverviewAction.Warp : OverviewAction.Destination);
                ActivateAction();
                return;
            }
            var clicked = fleaButtons.Where(flea => flea.Renderer.enabled && flea.Renderer.bounds.IntersectRay(ray))
                .OrderByDescending(flea => flea.Renderer.sortingOrder).FirstOrDefault();
            if (clicked != null)
            {
                FocusActions(OverviewAction.Flea, fleaButtons.IndexOf(clicked));
                ActivateAction();
            }
            else if (actionsFocused)
            {
                actionsFocused = false;
                waitingForActionRelease = true;
                Render();
            }
        }

        private void ActivateAction()
        {
            if (selectedAction == OverviewAction.Warp)
            {
                var plugin = RandomizerPlugin.Instance;
                if (plugin == null) return;
                RestoreBrowsing();
                input.enabled = false;
                plugin.StartCoroutine(WarpAfterInventory());
                return;
            }
            if (selectedAction == OverviewAction.Destination)
            {
                FastTravelUtil.TryCyclePreferredHub(1);
                Render();
                return;
            }
            if (selectedAction == OverviewAction.Flea)
            {
                if (selectedFlea < fleaButtons.Count)
                {
                    string name = fleaButtons[selectedFlea].FleaName;
                    float pitch = name == "Flea" && Time.unscaledTime - lastOrdinaryFleaTime < 3f
                        ? UnityEngine.Random.Range(.85f, 1.2f) : 1f;
                    if (FleaRescueAudio.TryPlayInventoryFlea(name, fleaVoices[nextFleaVoice], pitch))
                    {
                        nextFleaVoice = (nextFleaVoice + 1) % fleaVoices.Length;
                        if (name == "Flea") lastOrdinaryFleaTime = Time.unscaledTime;
                    }
                }
                return;
            }
            if (Time.unscaledTime < nextTimePass) return;
            nextTimePass = Time.unscaledTime + 1f;
            try
            {
                Patches.WorldInteractionPatches.PassInventoryTime(GameManager.UnsafeInstance);
                timePassPulseUntil = Time.unscaledTime + .45f;
                Refresh();
            }
            catch (Exception error)
            {
                RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not pass time from inventory: " + error);
                Render();
            }
        }

        private IEnumerator WarpAfterInventory()
        {
            var game = GameManager.UnsafeInstance;
            var state = SaveState.Instance;
            string scene = game.GetSceneNameString();
            game.inventoryFSM.SendEvent("INVENTORY CANCEL");
            float deadline = Time.unscaledTime + 3f;
            try
            {
                do
                {
                    yield return null;
                    if (game == null || !ReferenceEquals(state, SaveState.Instance) ||
                        game.GetSceneNameString() != scene || game.IsInSceneTransition) yield break;
                }
                while (PlayerData.instance?.isInventoryOpen == true && Time.unscaledTime < deadline);
                if (PlayerData.instance?.isInventoryOpen != false) yield break;
                RandomizerPlugin.Instance?.TryWarpToPreferredHub();
            }
            finally
            {
                if (this != null && IsPaneActive)
                {
                    RestoreBrowsing();
                    Render();
                }
            }
        }

        private void Refresh()
        {
            refreshAt = Time.unscaledTime + 1f;
            try
            {
                SaveState state = SaveState.Instance;
                if (!ReferenceEquals(displayedSave, state))
                {
                    if (actionsFocused || waitingForActionRelease) RestoreBrowsing();
                    displayedSave = state;
                    page = 0;
                    soul = null;
                }
                string key = page < sheets.Count ? sheets[page].Key : "overview";
                if (state?.goal == Archipelago.ActThreeGoal && soul == null)
                    soul = Resources.FindObjectsOfTypeAll<QuestCompleteTotalGroup>().FirstOrDefault(q => q.name == "Soul Snare");
                sheets = ArchipelagoInventoryModel.Build(state, soul);
                int updated = sheets.FindIndex(sheet => sheet.Key == key);
                page = updated < 0 ? 0 : updated;
                Render();
                reportedError = false;
            }
            catch (Exception error)
            {
                if (!reportedError)
                    RandomizerPlugin.Log?.LogError("[RANDOMIZER] Could not refresh the Archipelago inventory page: " + error);
                reportedError = true;
                Clear();
                Label("Progress unavailable", -7.6f, -2.3f, 23.8f, 1f, 5f);
                Label("Close and reopen the inventory to try again.", -7.6f, -3.5f, 23.8f, 1f, 4f);
            }
        }

        private static void ClearIcon(IconView icon)
        {
            icon.Renderer.enabled = false;
            icon.Renderer.sprite = null;
        }

        private void Clear()
        {
            labelIndex = rowIndex = iconIndex = 0;
            fleaButtons.Clear();
            timeButton = null;
            soulDivider.enabled = false;
            foreach (var label in labels) label.text = "";
            foreach (var row in rows) { row.Title.text = ""; row.Detail.text = ""; ClearIcon(row.Icon); }
            foreach (var icon in icons) ClearIcon(icon);
        }

        private void Render()
        {
            Clear();
            if (sheets.Count == 0) return;
            var sheet = sheets[page];
            if (sheet.Key == "overview") RenderOverview(sheet);
            else if (sheet.BellProgress) RenderBellProgress(sheet);
            else if (sheet.SoulChecklist) RenderSoul(sheet);
            else if (sheet.IconGrid) RenderBosses(sheet);
            else RenderDetails(sheet);
            string navigation = "";
            if (sheets.Count > 1)
                navigation = "Up: " + SheetName(sheets[(page + sheets.Count - 1) % sheets.Count]) +
                    "    /    Down: " + SheetName(sheets[(page + 1) % sheets.Count]);
            bool overview = sheet.Key == "overview";
            Label(actionsFocused ? "Back / B: Page navigation" : navigation,
                -7.6f, -13.2f, overview ? 8.8f : 23.8f, .55f, 3.3f, .85f);
            if (overview)
            {
                string warp = "F4: " + FastTravelUtil.GetPreferredHubName();
                Label(actionsFocused && selectedAction == OverviewAction.Warp ? "[ " + warp + " ]" : warp,
                    WarpButtonRect.x, WarpButtonRect.yMax, WarpButtonRect.width, WarpButtonRect.height, 3.3f, .85f);
                Label("|", -2.4f, -12.55f, .5f, .55f, 3.3f, .6f, TextAlignmentOptions.Top);
                Label(actionsFocused && selectedAction == OverviewAction.Destination ? "[ F3: Destination ]" : "F3: Destination",
                    DestinationButtonRect.x, DestinationButtonRect.yMax, DestinationButtonRect.width,
                    DestinationButtonRect.height, 3.3f, FastTravelUtil.CanCyclePreferredHub() ? .85f : .45f);
                Label("Total traps received: " + sheet.TrapsReceived, 4.8f, -12.6f, 11.4f, .5f, 3.2f, .85f, TextAlignmentOptions.TopRight);
                Label(sheet.DeathSummary, 4.8f, -13.2f, 11.4f, .45f, 3f, .85f, TextAlignmentOptions.TopRight);
            }
        }

        private string SheetName(ArchipelagoInventorySheet sheet)
        {
            if (sheet.Key == "overview") return "Overview";
            string category = sheet.Key.Split(':')[0];
            var siblings = sheets.Where(s => s.Key.StartsWith(category + ":", StringComparison.Ordinal)).ToList();
            return sheet.LeftHeading + (siblings.Count > 1 ? " " + (siblings.IndexOf(sheet) + 1) + "/" + siblings.Count : "");
        }

        private void RenderOverview(ArchipelagoInventorySheet sheet)
        {
            timeButton = labels[labelIndex];
            Label(actionsFocused && selectedAction == OverviewAction.PassTime ? "[ Pass Time ]" : "Pass Time",
                TimeButtonRect.x, TimeButtonRect.yMax, TimeButtonRect.width, TimeButtonRect.height,
                4f, .85f, TextAlignmentOptions.Top);
            UpdateTimeButtonFeedback();
            Label("Confirm / A Button: Actions",
                -2.1f, -1.25f, 18.3f, .45f, 3.1f, .8f);
            Row(sheet.Left[0], -7.6f, -2.3f, 10.8f, 5.5f);
            Label("Recently Received", -7.6f, -4.2f, 10.8f, .7f, 4.5f);
            for (int i = 0; i < sheet.Right.Count; i++)
                Row(sheet.Right[i], -7.6f, -5.1f - i * 1.05f, 10.8f, 3.9f, .7f);

            Label(sheet.Left[1].Title, 4.8f, -2.3f, 3.4f, .65f, 5f);
            Label("Flea caravan can move to:", 8.4f, -2.3f, 7.8f, .4f, 3.2f);
            Label("", 8.4f, -2.75f, 7.8f, .35f, 2.9f);
            var destinations = labels[labelIndex - 1];
            destinations.richText = true;
            destinations.text = string.Join(" ", sheet.Caravan.Select((row, index) =>
                "<color=#" + (row.Dim ? "737373" : "FFFFFF") + ">" +
                ArchipelagoInventoryModel.Plain(row.Title) +
                (index < sheet.Caravan.Count - 1 ? "," : "") + "</color>"));
            float fleaLabelX = 4.8f;
            for (int i = 0; i < sheet.Fleas.Count; i++)
            {
                bool front = i % 2 == 1;
                float x = 5.225f + (front ? .325f : 0f) + (i / 2) * (9.275f / 14f);
                bool selected = actionsFocused && selectedAction == OverviewAction.Flea && selectedFlea == i;
                if (selected) fleaLabelX = x - 1.7f;
                Icon(sheet.Fleas[i], x, front ? -3.625f : -3.475f, selected ? .8f : .65f);
                var flea = icons[iconIndex - 1];
                flea.FleaName = sheet.Fleas[i].Icon;
                fleaButtons.Add(flea);
                flea.Renderer.sortingOrder += selected ? 2 : front ? 1 : 0;
            }
            Label(actionsFocused && selectedAction == OverviewAction.Flea && selectedFlea >= 0 && selectedFlea < sheet.Fleas.Count
                ? sheet.Fleas[selectedFlea].Icon : "", fleaLabelX, -3.98f, 3.4f, .3f, 3f,
                1f, TextAlignmentOptions.Top);

            var soulRow = sheet.Left.FirstOrDefault(r => r.Title == "Silk and Soul");
            if (soulRow != null)
            {
                Row(soulRow, 4.8f, -4.3f, 11.4f, 4.5f, detailHeight: .55f);
                for (int i = 0; i < Math.Min(4, sheet.Story.Count); i++)
                {
                    var story = sheet.Story[i];
                    string name = story.Title.Replace("Caravan reaches Fleatopia", "Caravan at Fleatopia");
                    Label(name, 4.8f + i % 2 * 5.8f, -5.55f - i / 2 * .5f,
                        5.6f, .45f, 3.2f, story.Dim ? .45f : 1f);
                }
            }
            var letters = sheet.Left.FirstOrDefault(r => r.Title == "Required letters");
            if (letters != null) Row(letters, 4.8f, -4.3f, 11.4f, 4.5f, detailHeight: 1.4f);
            if (sheet.Melodies.Count > 0)
            {
                float y = soulRow == null && letters == null ? -4.7f : -6.85f;
                int owned = sheet.Melodies.Take(3).Count(r => !r.Dim);
                Label("Threefold Melody   " + owned + " / 3", 4.8f, y, 11.4f, .6f, 4.1f);
                for (int i = 0; i < Math.Min(3, sheet.Melodies.Count); i++)
                {
                    Icon(sheet.Melodies[i], 6.2f + i * 3.8f, y - .95f, .8f);
                    Label(sheet.Melodies[i].Title.Replace(" Melody", ""), 4.4f + i * 3.8f, y - 1.4f,
                        3.6f, .4f, 2.9f, sheet.Melodies[i].Dim ? .45f : .85f, TextAlignmentOptions.Top);
                }
            }
            if (sheet.Bosses.Count > 0)
            {
                Label("Boss Credits   " + sheet.Bosses.Count(r => !r.Dim) + " / " + sheet.Bosses.Count,
                    -7.6f, -8.65f, 14f, .6f, 4.4f);
                for (int i = 0; i < Math.Min(42, sheet.Bosses.Count); i++)
                    Icon(sheet.Bosses[i], -7f + i % 14 * 1.72f, -9.8f - i / 14, .85f);
            }
        }

        private void RenderBellProgress(ArchipelagoInventorySheet sheet)
        {
            if (sheet.CursedRequirements.Count > 0)
            {
                Label("Bells   " + sheet.Bells.Count(row => !row.Dim) + " / " + sheet.Bells.Count,
                    -7.6f, -2.3f, 23.8f, .7f, 4.5f);
                RenderProgressIcons(sheet.Bells, -3.9f, 1.2f);
                if (sheet.Left.Count > 0)
                {
                    Label("Melodies   " + sheet.Left.Count(row => !row.Dim) + " / " + sheet.Left.Count,
                        -7.6f, -6.05f, 23.8f, .65f, 4.5f);
                    RenderProgressIcons(sheet.Left, -7.5f, 1.2f);
                }
                float y = sheet.Left.Count > 0 ? -9.2f : -6.05f;
                Label("Cursed Ending", -7.6f, y, 23.8f, .65f, 4.5f);
                RenderProgressIcons(sheet.CursedRequirements, y - 1.45f, 1.2f);
                return;
            }
            if (sheet.Left.Count == 0)
            {
                Label("Bells   " + sheet.Bells.Count(row => !row.Dim) + " / " + sheet.Bells.Count,
                    -7.6f, -3.4f, 23.8f, .9f, 5.5f);
                RenderProgressIcons(sheet.Bells, -7f, 2.4f);
                return;
            }
            Label(sheet.LeftHeading, -7.6f, -2.3f, 23.8f, .8f, 5.5f);
            Label("Bells   " + sheet.Bells.Count(row => !row.Dim) + " / " + sheet.Bells.Count,
                -7.6f, -3.6f, 23.8f, .65f, 4.5f);
            RenderProgressIcons(sheet.Bells, -5.1f);
            Label("Melodies   " + sheet.Left.Count(row => !row.Dim) + " / " + sheet.Left.Count,
                -7.6f, -7.6f, 23.8f, .65f, 4.5f);
            RenderProgressIcons(sheet.Left, -9.2f);
        }

        private void RenderProgressIcons(IReadOnlyList<ArchipelagoInventoryRow> entries, float y, float iconSize = 1.4f)
        {
            float first = 4.3f - (entries.Count - 1) * 2.375f;
            for (int i = 0; i < entries.Count; i++)
            {
                var row = entries[i];
                float x = first + i * 4.75f;
                Icon(row, x, y, iconSize);
                float titleY = y - iconSize / 2f - .25f;
                Label(row.Title, x - 2.15f, titleY, 4.3f, .65f, iconSize > 1.4f ? 4.2f : 3.7f,
                    row.Dim ? .7f : 1f, TextAlignmentOptions.Top);
                if (row.LocationCompleted.HasValue)
                {
                    Label("Did the location", x - 2.15f, titleY - .65f,
                        4.3f, .45f, iconSize > 1.4f ? 3.3f : 2.8f,
                        row.LocationCompleted.Value ? 1f : .45f, TextAlignmentOptions.Top);
                }
            }
        }

        private void RenderSoul(ArchipelagoInventorySheet sheet)
        {
            soulDivider.enabled = true;
            Label(SheetName(sheet), -7.6f, -2.3f, 7.6f, .8f, 5.5f);
            string[] progress = sheet.RightHeading.Split('|');
            Label(progress[0].Trim(), -7.6f, -3.15f, 7.6f, .4f, 3.6f, .8f);
            if (progress.Length > 1)
                Label(progress[1].Trim(), -7.6f, -3.62f, 7.6f, .4f, 3f, .8f);
            for (int i = 0; i < sheet.SoulItems.Count; i++)
            {
                var row = sheet.SoulItems[i];
                float x = 2.1f + i * 4f;
                Icon(row, x, -2.65f, 1.05f);
                Label(row.Title, x - 1.95f, -3.3f, 3.9f, .55f, 3.2f,
                    row.Dim ? .7f : 1f, TextAlignmentOptions.Top);
            }
            int split = (sheet.Right.Count + 1) / 2;
            for (int column = 0; column < 3; column++)
            {
                float x = -7.6f + column * 8.1f;
                Label(column == 0 ? "Required: " + sheet.RequiredProgress : "Wish points", x, -4.48f, 7.6f, .55f, 4f);
                var entries = column == 0 ? sheet.Left : column == 1
                    ? sheet.Right.Take(split).ToList() : sheet.Right.Skip(split).ToList();
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = entries[i];
                    string points = row.Points > 0 ? "  (" + row.Points.ToString("0.#", CultureInfo.InvariantCulture) +
                        (row.Points == 1 ? " point)" : " points)") : "";
                    Label(row.Title + points,
                        x, -5.1f - i * .48f, 7.6f, .45f, 3.5f, row.Dim ? .45f : 1f);
                }
            }
        }

        private void RenderBosses(ArchipelagoInventorySheet sheet)
        {
            Label(SheetName(sheet), -7.6f, -2.3f, sheet.SoulCollection ? 16.8f : 23.8f, .8f, 5.5f);
            if (sheet.SoulCollection)
                Label(sheet.RightHeading, 9.4f, -2.5f, 6.8f, .55f, 3.7f, .85f, TextAlignmentOptions.TopRight);
            var bosses = sheet.Left.Concat(sheet.Right).ToList();
            for (int i = 0; i < bosses.Count; i++)
            {
                if (sheet.SoulCollection)
                    Row(bosses[i], -7.6f + i % 4 * 6f, -4.35f - i / 4 * 1.6f, 5.6f, 3.5f, 1f, .8f);
                else
                {
                    float x = -7.6f + i % 2 * 12f;
                    float y = -4.35f - i / 2 * 2.1f;
                    Row(bosses[i], x, y, 11.4f, 3.5f, 1f);
                    Label(bosses[i].EffectDescription, x + 1.25f, y - 1.05f, 10.15f, .85f, 2.8f, .85f);
                }
            }
        }

        private void RenderDetails(ArchipelagoInventorySheet sheet)
        {
            Label(SheetName(sheet), -7.6f, -2.3f, 23.8f, .8f, 5.5f);
            Label(sheet.RightHeading, -7.6f, -3.25f, 23.8f, .6f, 3.8f, .7f);
            for (int column = 0; column < 2; column++)
            {
                var entries = column == 0 ? sheet.Left : sheet.Right;
                for (int i = 0; i < entries.Count; i++)
                    Row(entries[i], column == 0 ? -7.6f : 4.8f, -4.3f - i * 1.3f, 11.4f, 4f);
            }
        }
    }
}
