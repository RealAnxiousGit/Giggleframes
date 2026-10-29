using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using RowType = SettingsListPanel.GraphicsSettingType;

namespace GigglelandPerf
{
    /// Drop-down lists for the Advanced page's rows. The rows stay the game's own Options rows (label,
    /// value text, colours, animation); the side arrows are swapped for a down caret, and clicking a row
    /// or pressing Submit on it opens a list of every choice under the value instead of cycling it.
    ///
    /// While a list is open, the page's own input is redirected to it (see DropdownPatches), so keyboard,
    /// controller and rebinds all work with no input code here: Navigate moves, Submit picks, Back closes.
    static class Dropdown
    {
        static SettingsListPanel panel;
        static string key;
        static GameObject blocker;
        static readonly List<Image> itemBg = new();
        static readonly List<TMP_Text> itemText = new();
        static int sel, current;
        static Color accent, textColor;

        public static bool IsOpen(SettingsListPanel p) => blocker != null && panel != null && p != null && p.Pointer == panel.Pointer;
        public static bool AnyOpen => blocker != null;

        /// Down caret instead of the left/right arrows on every Options row of an Advanced page, and
        /// whole-step sliders.
        public static void StyleRows(SettingsListPanel p)
        {
            var rows = p.rows;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                var r = rows[i];
                if (r?.setting?.type == RowType.Slider && r.slider != null)
                {
                    // Whole steps only, inside the range: dragging snaps to a real value and can't overshoot.
                    r.slider.wholeNumbers = true;
                    r.slider.minValue = r.setting.minValue;
                    r.slider.maxValue = r.setting.maxValue;
                    continue;
                }
                if (r?.setting == null || r.setting.type != RowType.Options) continue;
                if (r.arrowL != null) r.arrowL.gameObject.SetActive(false);
                var a = r.arrowR;
                if (a == null || a.transform.Find("Caret") != null) continue;
                // The page animates the arrow itself (hover, punch), so the caret rides along as a child:
                // the same glyph turned to point down, centred on the arrow.
                var go = new GameObject("Caret", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(a.rectTransform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = a.rectTransform.rect.size;
                rt.localEulerAngles = new Vector3(0f, 0f, -90f);
                var t = go.AddComponent<TextMeshProUGUI>();
                t.font = a.font;
                t.fontSharedMaterial = a.fontSharedMaterial;
                t.fontSize = a.fontSize;
                t.fontStyle = a.fontStyle;
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.raycastTarget = false;
                t.text = a.text;
                a.text = "";
            }
        }

        public static int IndexOf(SettingsListPanel p, SettingsListPanel.Row row)
        {
            var rows = p.rows;
            for (int i = 0; rows != null && i < rows.Count; i++) if (rows[i] != null && rows[i].Pointer == row.Pointer) return i;
            return -1;
        }

        public static bool IsOptionsRow(SettingsListPanel p, int index)
        {
            var rows = p.rows;
            return rows != null && index >= 0 && index < rows.Count && rows[index]?.setting?.type == RowType.Options;
        }

        public static void Open(SettingsListPanel p, int index)
        {
            Close();
            var row = p.rows[index];
            var s = row.setting;
            var anchor = row.valueText != null ? row.valueText.rectTransform : row.root;
            var canvas = row.root.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null || s.options == null || s.options.Length == 0) return;

            panel = p;
            key = s.key;
            current = sel = Mathf.Clamp(s.optionIndex, 0, s.options.Length - 1);
            accent = p.accentColor;
            textColor = p.valueTextColor.a > 0f ? p.valueTextColor : Color.white;

            // Full-screen layer on top of the menu: dims it, eats clicks, closes the list when clicked.
            blocker = new GameObject("GiggleframesDropdown", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            var brt = blocker.GetComponent<RectTransform>();
            brt.SetParent(canvas.transform, false);
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            var sub = blocker.AddComponent<Canvas>();
            sub.overrideSorting = true;
            sub.sortingOrder = 30000;
            blocker.AddComponent<GraphicRaycaster>();
            var dim = blocker.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.35f);
            OnEvent(blocker, EventTriggerType.PointerClick, Close);

            // The list itself: one entry per choice, in the row's own font, right under the value.
            float font = row.valueText != null ? row.valueText.fontSize : 28f;
            float itemH = Mathf.Round(font * 1.55f), pad = 8f;
            float width = Mathf.Max(anchor.rect.width + 60f, 300f);
            int n = s.options.Length;

            var list = new GameObject("List", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            var lrt = list.GetComponent<RectTransform>();
            lrt.SetParent(brt, false);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(width, n * itemH + 2 * pad);
            var bg = list.AddComponent<Image>();
            bg.sprite = SettingsListPanel.RoundedRuntimeSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.06f, 0.09f, 0.97f);
            OnEvent(list, EventTriggerType.PointerClick, () => { });   // clicks between items don't close it

            itemBg.Clear();
            itemText.Clear();
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var item = new GameObject("Item_" + i, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
                var irt = item.GetComponent<RectTransform>();
                irt.SetParent(lrt, false);
                irt.anchorMin = new Vector2(0f, 1f); irt.anchorMax = new Vector2(1f, 1f); irt.pivot = new Vector2(0.5f, 1f);
                irt.offsetMin = new Vector2(pad, 0f); irt.offsetMax = new Vector2(-pad, 0f);
                irt.anchoredPosition = new Vector2(0f, -pad - i * itemH);
                irt.sizeDelta = new Vector2(irt.sizeDelta.x, itemH);
                var ibg = item.AddComponent<Image>();
                ibg.sprite = bg.sprite;
                ibg.type = Image.Type.Sliced;
                itemBg.Add(ibg);

                var tgo = new GameObject("Text", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
                var trt = tgo.GetComponent<RectTransform>();
                trt.SetParent(irt, false);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(18f, 0f); trt.offsetMax = new Vector2(-18f, 0f);
                var t = tgo.AddComponent<TextMeshProUGUI>();
                if (row.valueText != null)
                {
                    t.font = row.valueText.font;
                    t.fontSharedMaterial = row.valueText.fontSharedMaterial;
                    t.fontStyle = row.valueText.fontStyle;
                }
                t.fontSize = font;
                t.enableWordWrapping = false;
                t.alignment = TextAlignmentOptions.Midline;
                t.raycastTarget = false;
                t.text = s.options[i];
                itemText.Add(t);

                OnEvent(item, EventTriggerType.PointerEnter, () => { sel = idx; Paint(); });
                OnEvent(item, EventTriggerType.PointerClick, () => Choose(idx));
            }

            // Centred under the value text; flip above the row if it would run off the bottom.
            var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            anchor.GetWorldCorners(corners);
            var rowCorners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            row.root.GetWorldCorners(rowCorners);
            lrt.position = new Vector3((corners[0].x + corners[2].x) * 0.5f, rowCorners[0].y, corners[2].z);
            var canvasRt = canvas.GetComponent<RectTransform>();
            var lc = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            var cc = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            lrt.GetWorldCorners(lc);
            canvasRt.GetWorldCorners(cc);
            if (lc[0].y < cc[0].y)
            {
                lrt.pivot = new Vector2(0.5f, 0f);
                lrt.position = new Vector3(lrt.position.x, rowCorners[1].y, lrt.position.z);
                lrt.GetWorldCorners(lc);
                if (lc[1].y > cc[1].y) lrt.position += new Vector3(0f, cc[1].y - lc[1].y, 0f);
            }
            if (lc[0].x < cc[0].x) lrt.position += new Vector3(cc[0].x - lc[0].x, 0f, 0f);
            if (lc[2].x > cc[2].x) lrt.position += new Vector3(cc[2].x - lc[2].x, 0f, 0f);
            Paint();
        }

        static void Paint()
        {
            for (int i = 0; i < itemBg.Count; i++)
            {
                bool hi = i == sel;
                itemBg[i].color = hi ? new Color(accent.r, accent.g, accent.b, 0.35f) : new Color(1f, 1f, 1f, 0f);
                itemText[i].color = i == current ? accent : textColor;
                itemText[i].transform.localScale = hi ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            }
        }

        public static void Move(int dir)
        {
            if (itemBg.Count == 0) return;
            sel = Mathf.Clamp(sel + Math.Sign(dir), 0, itemBg.Count - 1);
            Paint();
        }

        public static void Choose() => Choose(sel);

        static void Choose(int i)
        {
            var p = panel;
            var k = key;
            Close();
            if (p != null && k != null) p.SetOptionIndex(k, i);
        }

        public static void Close()
        {
            if (blocker != null) UnityEngine.Object.Destroy(blocker);
            blocker = null;
            panel = null;
            key = null;
            itemBg.Clear();
            itemText.Clear();
        }

        /// Every frame: a page that closed underneath an open list (pause menu closed, scene change) closes it.
        public static void Tick()
        {
            if (blocker == null && panel == null) return;
            if (panel == null || blocker == null || !panel.IsVisible) Close();
        }

        static void OnEvent(GameObject go, EventTriggerType type, Action a)
        {
            var et = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener((UnityAction<BaseEventData>)(Action<BaseEventData>)(_ => a()));
            et.triggers.Add(e);
        }
    }
}
