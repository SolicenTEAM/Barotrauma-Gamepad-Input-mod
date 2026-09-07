using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GamePadInput
{
    internal class SlotWheel
    {
        private static readonly Color EmptyCellColor = new Color(70, 70, 70, 255);
        private static readonly Color ItemCellColor = new Color(125, 125, 125, 255);
        private static readonly Color SelectedCellColor = new Color(255, 255, 255, 255);

        private GUIFrame root;
        private GUICustomComponent highlight;
        private int lastHighlighted = -1;

        private readonly List<GUIFrame> cells = new List<GUIFrame>();
        private readonly List<GUIImage> icons = new List<GUIImage>();
        private readonly List<GUITextBlock> labels = new List<GUITextBlock>();
        private readonly List<int> keyIndices = new List<int>();

        public bool Open { get; private set; }
        public int SelectedIndex { get; private set; }
        public int InitialIndex { get; private set; }
        public int SlotCount { get { return cells.Count; } }

        public void OpenWheel(int preferredKeyIndex)
        {
            Rebuild();
            if (cells.Count == 0) { return; }
            int preferredCell = keyIndices.IndexOf(preferredKeyIndex);
            SelectedIndex = preferredCell >= 0 ? preferredCell : 0;
            InitialIndex = SelectedIndex;
            RefreshIcons();
            Highlight();
            root.Visible = true;
            Open = true;
        }

        public void Close()
        {
            if (root != null) { root.Visible = false; }
            Open = false;
        }

        public bool Move(int delta)
        {
            if (!Open || cells.Count == 0) { return false; }
            return Select((SelectedIndex + delta + cells.Count) % cells.Count);
        }

        public bool Select(int index)
        {
            if (!Open || index < 0 || index >= cells.Count || index == SelectedIndex) { return false; }
            SelectedIndex = index;
            Highlight();
            return SelectedIndex != InitialIndex;
        }

        public int KeyIndexAt(int cellIndex)
        {
            return cellIndex >= 0 && cellIndex < keyIndices.Count ? keyIndices[cellIndex] : -1;
        }

        private void Rebuild()
        {
            TearDown();
            Build();
        }

        private void Build()
        {
            RectTransform target = Screen.Selected?.Frame?.RectTransform;
            if (target == null) { return; }
            Inventory inventory = Character.Controlled?.Inventory;
            if (inventory == null) { return; }

            root = new GUIFrame(new RectTransform(Vector2.One, target, Anchor.Center), style: null) { CanBeFocused = false };

            // та же раскладка клавиш, что использует сама игра (AssignQuickUseNumKeys):
            // цифры назначаются только видимым слотам типа Any, индекс клавиши берём готовый
            List<int> keyedSlotIndices = new List<int>();
            VisualSlot[] visualSlots = inventory.visualSlots;
            if (visualSlots != null)
            {
                int bindingCount = GameSettings.CurrentConfig.InventoryKeyMap.Bindings.Length;
                for (int i = 0; i < visualSlots.Length; i++)
                {
                    VisualSlot slot = visualSlots[i];
                    if (slot == null || slot.InventoryKeyIndex < 0 || slot.InventoryKeyIndex >= bindingCount) { continue; }
                    keyedSlotIndices.Add(i);
                }
            }
            keyedSlotIndices = keyedSlotIndices
                .Select(slotIndex => (slotIndex, keyIndex: visualSlots[slotIndex].InventoryKeyIndex))
                .OrderBy(pair => pair.keyIndex)
                .Select(pair => pair.slotIndex)
                .ToList();

            if (keyedSlotIndices.Count == 0)
            {
                int fallbackCount = Math.Min(10, visualSlots?.Length ?? 10);
                for (int i = 0; i < fallbackCount; i++) { keyedSlotIndices.Add(i); }
            }

            int screenH = GameMain.GraphicsHeight;
            int cellSize = Math.Max(48, (int)(screenH * 0.055f));
            int radius = (int)(screenH * 0.17f);
            double angleStep = 360.0 / keyedSlotIndices.Count;

            for (int n = 0; n < keyedSlotIndices.Count; n++)
            {
                int slotIndex = keyedSlotIndices[n];
                int keyIndex = visualSlots != null ? visualSlots[slotIndex].InventoryKeyIndex : n;
                keyIndices.Add(keyIndex);

                double angleRad = (-90.0 + n * angleStep) * Math.PI / 180.0;
                int ox = (int)(Math.Cos(angleRad) * radius);
                int oy = (int)(Math.Sin(angleRad) * radius);

                GUIFrame cell = new GUIFrame(new RectTransform(new Point(cellSize, cellSize), root.RectTransform, Anchor.Center) { AbsoluteOffset = new Point(ox - cellSize / 2, oy - cellSize / 2) })
                {
                    CanBeFocused = false,
                    Color = EmptyCellColor
                };
                cells.Add(cell);

                GUITextBlock label = new GUITextBlock(new RectTransform(new Vector2(0.45f, 0.38f), cell.RectTransform, Anchor.BottomRight), keyIndex == 9 ? "0" : (keyIndex + 1).ToString(), textAlignment: Alignment.Center)
                {
                    CanBeFocused = false
                };
                labels.Add(label);
            }

            highlight = new GUICustomComponent(new RectTransform(Vector2.One, root.RectTransform), onDraw: (sb, component) =>
            {
                if (!Open || SelectedIndex < 0 || SelectedIndex >= cells.Count) { return; }
                GUIFrame cell = cells[SelectedIndex];
                if (cell == null) { return; }
                Rectangle rect = cell.Rect;
                rect.Inflate(5, 5);
                GUI.DrawRectangle(sb, rect, Color.Black, false, 0.0f, 5f);
                rect.Inflate(-2, -2);
                GUI.DrawRectangle(sb, rect, GUIStyle.Green, false, 0.0f, 3f);
            })
            {
                CanBeFocused = false
            };
        }

        private void RefreshIcons()
        {
            Inventory inventory = Character.Controlled?.Inventory;
            for (int i = 0; i < cells.Count; i++)
            {
                int slotIndex = i < keyIndices.Count ? KeyIndexToSlotIndex(i) : -1;
                Item item = slotIndex >= 0 ? inventory?.GetItemAt(slotIndex) : null;
                Sprite sprite = item != null ? (item.OverrideInventorySprite ?? item.Prefab.Sprite) : null;
                if (sprite == null)
                {
                    if (i < icons.Count && icons[i] != null) { icons[i].Visible = false; }
                    continue;
                }
                if (i >= icons.Count)
                {
                    while (icons.Count < cells.Count) { icons.Add(null); }
                }
                if (icons[i] == null)
                {
                    icons[i] = new GUIImage(new RectTransform(Vector2.One * 0.7f, cells[i].RectTransform, Anchor.Center), sprite, true) { CanBeFocused = false };
                }
                else
                {
                    icons[i].Sprite = sprite;
                    icons[i].Visible = true;
                }
            }
        }

        private int KeyIndexToSlotIndex(int cellIndex)
        {
            int keyIndex = KeyIndexAt(cellIndex);
            VisualSlot[] visualSlots = Character.Controlled?.Inventory?.visualSlots;
            if (visualSlots == null) { return -1; }
            for (int i = 0; i < visualSlots.Length; i++)
            {
                if (visualSlots[i] != null && visualSlots[i].InventoryKeyIndex == keyIndex) { return i; }
            }
            return -1;
        }

        private void Highlight()
        {
            for (int i = 0; i < cells.Count; i++)
            {
                bool hasItem = i < icons.Count && icons[i] != null && icons[i].Visible;
                bool selected = i == SelectedIndex;
                cells[i].Color = selected ? SelectedCellColor : (hasItem ? ItemCellColor : EmptyCellColor);
                if (i < labels.Count && labels[i] != null)
                {
                    int keyIndex = KeyIndexAt(i);
                    labels[i].Text = keyIndex == 9 ? "0" : (keyIndex + 1).ToString();
                    labels[i].TextColor = selected ? GUIStyle.Green : Color.White * 0.7f;
                }
            }
            if (lastHighlighted != SelectedIndex && lastHighlighted >= 0 && SelectedIndex < cells.Count)
            {
                cells[SelectedIndex].Flash(color: GUIStyle.Green * 0.5f);
            }
            lastHighlighted = SelectedIndex;
        }

        private void TearDown()
        {
            if (root != null) { root.Parent?.RemoveChild(root); root = null; }
            highlight = null;
            lastHighlighted = -1;
            cells.Clear();
            icons.Clear();
            labels.Clear();
            keyIndices.Clear();
        }
    }
}
