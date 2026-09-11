using System;
using System.Collections.Generic;
using Barotrauma;
using Microsoft.Xna.Framework;

namespace GamePadInput
{
    internal class OptionsUI
    {
        private class SliderRow
        {
            public GUIScrollBar Bar;
            public GUITextBlock ValueLabel;
            public Func<float> Getter;
            public Action<float> Setter;
            public Func<float, string> Formatter;
        }

        private class TickRow
        {
            public GUITickBox Box;
            public Func<bool> Getter;
        }

        private class BindRow
        {
            public string Action;
            public GUIButton Button;
        }

        private readonly ModConfig config;
        private readonly Func<PadSnapshot> padPoller;

        private GUIFrame blocker;
        private GUIFrame window;
        private readonly List<SliderRow> sliders = new List<SliderRow>();
        private readonly List<TickRow> tickBoxes = new List<TickRow>();
        private readonly List<BindRow> bindRows = new List<BindRow>();
        private readonly List<KeyValuePair<GUIButton, Func<string>>> cycleButtons = new List<KeyValuePair<GUIButton, Func<string>>>();
        private GUITextBlock statusText;

        private static readonly Color SectionColor = new Color(0, 255, 255, 255); // #00ffff

        public bool IsOpen { get; private set; }
        public bool IsCapturing { get; private set; }
        public bool BuiltInPause { get; private set; }
        private string capturingAction;
        private GUIButton capturingButton;
        private HashSet<GPadButton> captureSnapshot;

        public static Action ToggleRequested;

        public static void RequestToggle()
        {
            ToggleRequested?.Invoke();
        }

        public static string BackendName;

        public OptionsUI(ModConfig config, Func<PadSnapshot> padPoller)
        {
            this.config = config;
            this.padPoller = padPoller;
        }

        public void Toggle()
        {
            if (IsOpen) { Close(); }
            else { Open(); }
        }

        public void Open()
        {
            try
            {
                RebuildIfScreenChanged();
                RefreshValues();
                UpdateStatusText();
                blocker.Visible = true;
                window.Visible = true;
                IsOpen = true;
                LuaCsLogger.LogMessage("GamepadInput: options window opened");
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: ERROR opening options window: " + e.Message + "\n" + e.StackTrace);
            }
        }

        public void Close()
        {
            CancelCapture();
            config.Save();
            TearDown();
            IsOpen = false;
        }

        public void Discard()
        {
            CancelCapture();
            TearDown();
            IsOpen = false;
            LuaCsLogger.LogMessage("GamepadInput: options window discarded (pause menu closed)");
        }

        private RectTransform currentScreenFrame;

        private void RebuildIfScreenChanged()
        {
            bool inPause = GUI.PauseMenuOpen && GUI.PauseMenu != null;
            RectTransform target = inPause ? GUI.PauseMenu.RectTransform : Screen.Selected?.Frame?.RectTransform;
            if (target == null) { return; }
            if (window != null && blocker != null && currentScreenFrame == target) { return; }
            TearDown();
            Build(target);
            currentScreenFrame = target;
            BuiltInPause = inPause;
        }

        private void TearDown()
        {
            if (window != null) { window.Parent?.RemoveChild(window); window = null; }
            if (blocker != null) { blocker.Parent?.RemoveChild(blocker); blocker = null; }
            sliders.Clear();
            tickBoxes.Clear();
            bindRows.Clear();
            statusText = null;
        }

        public void BeginCapture(string action, GUIButton button)
        {
            CancelCapture();
            capturingAction = action;
            capturingButton = button;
            captureSnapshot = new HashSet<GPadButton>();
            PadSnapshot pad = padPoller();
            foreach (GPadButton b in GPadInput.AllButtons)
            {
                if (pad.IsDown(b)) { captureSnapshot.Add(b); }
            }
            button.TextBlock.Text = "PRESS A BUTTON";
            IsCapturing = true;
        }

        public void CancelCapture()
        {
            if (!IsCapturing) { return; }
            IsCapturing = false;
            capturingAction = null;
            capturingButton = null;
            captureSnapshot = null;
            RefreshValues();
        }

        public void UpdateCapture(PadSnapshot pad)
        {
            if (!IsCapturing) { return; }
            foreach (GPadButton b in GPadInput.AllButtons)
            {
                if (!captureSnapshot.Contains(b) && pad.IsDown(b))
                {
                    config.SetBinding(capturingAction, b);
                    IsCapturing = false;
                    capturingAction = null;
                    capturingButton = null;
                    captureSnapshot = null;
                    RefreshValues();
                    return;
                }
            }
        }

        private void UpdateStatusText()
        {
            if (statusText == null) { return; }
            try
            {
                PadSnapshot pad = padPoller();
                statusText.Text = SteamInputCompat.DescribeStatus(pad, BackendName);
            }
            catch
            {
                statusText.Text = "";
            }
        }

        private void RefreshValues()
        {
            foreach (SliderRow row in sliders)
            {
                row.Bar.BarScrollValue = row.Getter();
                row.ValueLabel.Text = row.Formatter(row.Getter());
            }
            foreach (TickRow row in tickBoxes)
            {
                row.Box.Selected = row.Getter();
            }
            foreach (BindRow row in bindRows)
            {
                row.Button.TextBlock.Text = GPadInput.GetLabel(config.GetBinding(row.Action));
            }
            foreach (KeyValuePair<GUIButton, Func<string>> pair in cycleButtons)
            {
                pair.Key.TextBlock.Text = pair.Value();
            }
        }

        private static string GetBackendLabel(int mode)
        {
            switch (mode)
            {
                case 1: return "Pad backend: SDL direct";
                case 2: return "Pad backend: Steam Input";
                case 3: return "Pad backend: MonoGame";
                default: return "Pad backend: Auto";
            }
        }

        private static string GetGyroSourceLabel(int source)
        {
            switch (source)
            {
                case 1: return "Gyro source: SDL direct";
                case 2: return "Gyro source: Steam Input";
                default: return "Gyro source: Auto (SDL, then Steam Input)";
            }
        }

        private void AddTickBoxRow(GUIComponent parent, string labelText, Func<bool> getter, Action<bool> setter)
        {
            GUITickBox box = new GUITickBox(new RectTransform(new Vector2(1.0f, 0.042f), parent.RectTransform), labelText);
            box.OnSelected = (b) =>
            {
                setter(b.Selected);
                return true;
            };
            box.Selected = getter();
            tickBoxes.Add(new TickRow { Box = box, Getter = getter });
        }

        private void AddCycleButtonRow(GUIComponent parent, Func<int, string> formatter, Func<int> getter, Action<int> setter, int count)
        {
            GUIButton button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.042f), parent.RectTransform), formatter(getter()));
            button.OnClicked += (b, data) =>
            {
                int next = (getter() + 1) % count;
                setter(next);
                b.TextBlock.Text = formatter(next);
                return true;
            };
            cycleButtons.Add(new KeyValuePair<GUIButton, Func<string>>(button, () => formatter(getter())));
        }

        private static void AddDivider(GUIComponent parent, string text)
        {
            // увеличенная высота блока при центрированном тексте даёт воздух сверху и снизу
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.07f), parent.RectTransform), text, textColor: SectionColor, textAlignment: Alignment.Center);
        }

        private void Build(RectTransform screenFrame)
        {
            blocker = new GUIFrame(new RectTransform(Vector2.One, screenFrame, Anchor.Center), style: null);

            new GUIFrame(new RectTransform(GUI.Canvas.RelativeSize, blocker.RectTransform, Anchor.Center), style: "GUIBackgroundBlocker");

            window = new GUIFrame(new RectTransform(new Vector2(0.5f, 0.85f), blocker.RectTransform, Anchor.Center) { MinSize = new Point(660, 520) });

            GUILayoutGroup layout = new GUILayoutGroup(new RectTransform(Vector2.One * 0.94f, window.RectTransform, Anchor.Center, Pivot.Center), isHorizontal: false, childAnchor: Anchor.TopLeft)
            {
                RelativeSpacing = 0.012f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.04f), layout.RectTransform), "Gamepad Input — Settings", font: GUIStyle.SubHeadingFont, textAlignment: Alignment.Center);

            GUIListBox list = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.85f), layout.RectTransform));
            GUIFrame listContent = list.Content;

            AddDivider(listContent, "Cursor & Movement");
            AddSliderRow(listContent, "Cursor speed", 200f, 3000f, 50f, () => config.CursorSpeed, v => config.CursorSpeed = v, v => ((int)v).ToString());
            AddSliderRow(listContent, "Cursor deadzone", 0.05f, 0.5f, 0.05f, () => config.CursorDeadzone, v => config.CursorDeadzone = v, v => v.ToString("0.00"));
            AddSliderRow(listContent, "Move threshold", 0.1f, 0.9f, 0.05f, () => config.MoveThreshold, v => config.MoveThreshold = v, v => v.ToString("0.00"));
            AddSliderRow(listContent, "Cursor lock radius", 50f, 600f, 25f, () => config.CursorRadius, v => config.CursorRadius = v, v => ((int)v).ToString());
            AddTickBoxRow(listContent, "Swap sticks in menus (left stick = cursor)", () => config.StickSwap, v => config.StickSwap = v);

            AddDivider(listContent, "Gyro & Motion");
            AddTickBoxRow(listContent, "Gyro cursor (Steam Deck / DualShock / Switch)", () => config.GyroEnabled, v => config.GyroEnabled = v);
            AddSliderRow(listContent, "Gyro sensitivity", 0.5f, 8f, 0.1f, () => config.GyroSensitivity, v => config.GyroSensitivity = v, v => v.ToString("0.0"));
            AddTickBoxRow(listContent, "Touchpad cursor (DualShock / DualSense)", () => config.TouchpadCursor, v => config.TouchpadCursor = v);
            AddCycleButtonRow(listContent, GetGyroSourceLabel, () => config.GyroSource, v => config.GyroSource = v, 3);

            AddDivider(listContent, "Inventory Wheel");
            AddTickBoxRow(listContent, "Item wheel (hold bumper, point with stick, release to take)", () => config.SlotWheelEnabled, v => config.SlotWheelEnabled = v);

            AddDivider(listContent, "Vibration");
            AddTickBoxRow(listContent, "Vibration enabled", () => config.VibrationEnabled, v => config.VibrationEnabled = v);
            AddSliderRow(listContent, "Vibration time", 0.1f, 2f, 0.1f, () => config.VibrationDuration, v => config.VibrationDuration = v, v => v.ToString("0.0"));

            AddDivider(listContent, "Advanced");
            AddCycleButtonRow(listContent, GetBackendLabel, () => config.BackendMode, v => config.BackendMode = v, 4);

            AddDivider(listContent, "Status");
            statusText = new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.07f), listContent.RectTransform), "", wrap: true);
            UpdateStatusText();

            AddDivider(listContent, "Bindings");
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.03f), listContent.RectTransform), "Click a slot, then press a gamepad button");

            GUILayoutGroup columns = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.3f), listContent.RectTransform), isHorizontal: true, childAnchor: Anchor.TopLeft);
            GUILayoutGroup col1 = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 1.0f), columns.RectTransform), isHorizontal: false, childAnchor: Anchor.TopLeft);
            GUILayoutGroup col2 = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 1.0f), columns.RectTransform), isHorizontal: false, childAnchor: Anchor.TopLeft);

            int half = (ModConfig.ActionOrder.Length + 1) / 2;
            for (int i = 0; i < ModConfig.ActionOrder.Length; i++)
            {
                string action = ModConfig.ActionOrder[i];
                GUIComponent column = i < half ? col1 : col2;
                AddBindRow(column, action);
            }

            GUILayoutGroup bottom = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), layout.RectTransform), isHorizontal: true) { Stretch = true, RelativeSpacing = 0.01f };

            GUIButton saveButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), bottom.RectTransform), "Save & close");
            saveButton.OnClicked = (button, obj) =>
            {
                Close();
                return true;
            };

            GUIButton resetButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), bottom.RectTransform), "Reset defaults");
            resetButton.OnClicked = (button, obj) =>
            {
                config.ResetToDefaults();
                RefreshValues();
                return true;
            };
        }

        private void AddSliderRow(GUIComponent parent, string labelText, float min, float max, float step, Func<float> getter, Action<float> setter, Func<float, string> formatter)
        {
            GUILayoutGroup row = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.042f), parent.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft);

            new GUITextBlock(new RectTransform(new Vector2(0.30f, 1.0f), row.RectTransform), labelText);

            GUIScrollBar bar = new GUIScrollBar(new RectTransform(new Vector2(0.48f, 0.7f), row.RectTransform), style: "GUISlider");
            bar.Range = new Vector2(min, max);
            bar.StepValue = step;
            bar.BarScrollValue = getter();

            GUITextBlock valueLabel = new GUITextBlock(new RectTransform(new Vector2(0.14f, 1.0f), row.RectTransform), formatter(getter()));

            SliderRow sliderRow = new SliderRow
            {
                Bar = bar,
                ValueLabel = valueLabel,
                Getter = getter,
                Setter = setter,
                Formatter = formatter
            };

            bar.OnMoved = (b, scroll) =>
            {
                float value = b.BarScrollValue;
                sliderRow.Setter(value);
                valueLabel.Text = formatter(value);
                return true;
            };

            sliders.Add(sliderRow);
        }

        private void AddBindRow(GUIComponent parent, string action)
        {
            GUILayoutGroup row = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.105f), parent.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft);

            new GUITextBlock(new RectTransform(new Vector2(0.62f, 1.0f), row.RectTransform), ModConfig.ActionLabels.TryGetValue(action, out string label) ? label : action);

            GUIButton button = new GUIButton(new RectTransform(new Vector2(0.34f, 0.9f), row.RectTransform), GPadInput.GetLabel(config.GetBinding(action)), Alignment.Center, "GUIButtonSmall");
            button.OnClicked = (b, obj) =>
            {
                BeginCapture(action, button);
                return true;
            };

            bindRows.Add(new BindRow { Action = action, Button = button });
        }
    }
}
