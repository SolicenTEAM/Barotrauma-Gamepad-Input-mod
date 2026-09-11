using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GamePadInput
{
    public enum GPadButton
    {
        A, B, X, Y,
        LB, RB,
        LS, RS,
        Start, Back,
        DPadUp, DPadDown, DPadLeft, DPadRight,
        LT, RT
    }

    public static class GPadInput
    {
        public const float TriggerThreshold = 0.5f;

        public static readonly GPadButton[] AllButtons =
        {
            GPadButton.A, GPadButton.B, GPadButton.X, GPadButton.Y,
            GPadButton.LB, GPadButton.RB, GPadButton.LS, GPadButton.RS,
            GPadButton.Start, GPadButton.Back,
            GPadButton.DPadUp, GPadButton.DPadDown, GPadButton.DPadLeft, GPadButton.DPadRight,
            GPadButton.LT, GPadButton.RT
        };

        public static bool IsDown(PadSnapshot pad, GPadButton button)
        {
            return pad.IsDown(button);
        }

        public static string GetLabel(GPadButton button)
        {
            switch (button)
            {
                case GPadButton.A: return "A";
                case GPadButton.B: return "B";
                case GPadButton.X: return "X";
                case GPadButton.Y: return "Y";
                case GPadButton.LB: return "LB";
                case GPadButton.RB: return "RB";
                case GPadButton.LS: return "L-Stick";
                case GPadButton.RS: return "R-Stick";
                case GPadButton.Start: return "Start";
                case GPadButton.Back: return "Back";
                case GPadButton.DPadUp: return "D-Up";
                case GPadButton.DPadDown: return "D-Down";
                case GPadButton.DPadLeft: return "D-Left";
                case GPadButton.DPadRight: return "D-Right";
                case GPadButton.LT: return "LT";
                case GPadButton.RT: return "RT";
                default: return "?";
            }
        }
    }

    public struct BindingEntry
    {
        public string Action;
        public GPadButton Button;
    }

    public class ModConfig
    {
        public static readonly string[] ActionOrder =
        {
            "Use", "Escape", "InfoTab", "MiddleClick", "Run", "Health", "Grab",
            "CrewOrders", "Ragdoll", "CursorLockToggle", "CrouchToggle", "SlotNext", "SlotPrev"
        };

        public static readonly Dictionary<string, GPadButton> DefaultBindings = new Dictionary<string, GPadButton>
        {
            { "Use", GPadButton.B },
            { "Escape", GPadButton.Start },
            { "InfoTab", GPadButton.Back },
            { "MiddleClick", GPadButton.RS },
            { "Run", GPadButton.LS },
            { "Health", GPadButton.X },
            { "Grab", GPadButton.Y },
            { "CrewOrders", GPadButton.DPadRight },
            { "Ragdoll", GPadButton.DPadLeft },
            { "CursorLockToggle", GPadButton.DPadUp },
            { "CrouchToggle", GPadButton.DPadDown },
            { "SlotNext", GPadButton.RB },
            { "SlotPrev", GPadButton.LB }
        };

        public static readonly Dictionary<string, string> ActionLabels = new Dictionary<string, string>
        {
            { "Use", "Use / interact" },
            { "Escape", "Escape / pause" },
            { "InfoTab", "Info tab" },
            { "MiddleClick", "Middle mouse click" },
            { "Run", "Run (hold)" },
            { "Health", "Health window" },
            { "Grab", "Grab" },
            { "CrewOrders", "Crew orders" },
            { "Ragdoll", "Ragdoll (hold)" },
            { "CursorLockToggle", "Toggle cursor lock" },
            { "CrouchToggle", "Toggle crouch" },
            { "SlotNext", "Next inventory slot" },
            { "SlotPrev", "Previous inventory slot" }
        };

        public static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Daedalic Entertainment GmbH", "Barotrauma", "GamepadInputConfig.xml");
            }
        }

        public float CursorSpeed = 1200f;
        public float CursorDeadzone = 0.2f;
        public float MoveThreshold = 0.5f;
        public float CursorRadius = 175f;
        public bool StickSwap = true; // true = в меню стики меняются местами (левый = курсор); false = левый всегда движение, правый всегда курсор
        public bool VibrationEnabled = true;
        public float VibrationDuration = 0.5f;
        public bool GyroEnabled = false;
        public float GyroSensitivity = 2f;
        public int GyroSource = 0; // 0 = auto (SDL, потом Steam Input), 1 = только SDL, 2 = только Steam Input
        public int BackendMode = 0; // 0 = auto, 1 = SDL direct, 2 = Steam Input, 3 = MonoGame
        public bool TouchpadCursor = false;
        public bool SlotWheelEnabled = true;

        public BindingEntry[] Bindings;

        public ModConfig()
        {
            Bindings = DefaultBindings.Select(kvp => new BindingEntry { Action = kvp.Key, Button = kvp.Value }).ToArray();
        }

        public GPadButton GetBinding(string action)
        {
            if (Bindings != null)
            {
                foreach (BindingEntry entry in Bindings)
                {
                    if (entry.Action == action) { return entry.Button; }
                }
            }
            return DefaultBindings.TryGetValue(action, out GPadButton def) ? def : GPadButton.A;
        }

        public void SetBinding(string action, GPadButton button)
        {
            List<BindingEntry> list = new List<BindingEntry>(Bindings ?? new BindingEntry[0]);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Action == action)
                {
                    list[i] = new BindingEntry { Action = action, Button = button };
                    Bindings = list.ToArray();
                    return;
                }
            }
            list.Add(new BindingEntry { Action = action, Button = button });
            Bindings = list.ToArray();
        }

        public void ResetToDefaults()
        {
            CursorSpeed = 1200f;
            CursorDeadzone = 0.2f;
            MoveThreshold = 0.5f;
            CursorRadius = 175f;
            StickSwap = true;
            VibrationEnabled = true;
            VibrationDuration = 0.5f;
            GyroEnabled = false;
            GyroSensitivity = 2f;
            GyroSource = 0;
            BackendMode = 0;
            TouchpadCursor = false;
            SlotWheelEnabled = true;
            Bindings = DefaultBindings.Select(kvp => new BindingEntry { Action = kvp.Key, Button = kvp.Value }).ToArray();
        }

        public static ModConfig Load()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    XDocument doc = XDocument.Load(path);
                    XElement root = doc.Root;
                    if (root != null)
                    {
                        ModConfig config = new ModConfig();
                        config.CursorSpeed = GetFloat(root, "CursorSpeed", config.CursorSpeed);
                        config.CursorDeadzone = GetFloat(root, "CursorDeadzone", config.CursorDeadzone);
                        config.MoveThreshold = GetFloat(root, "MoveThreshold", config.MoveThreshold);
                        config.CursorRadius = GetFloat(root, "CursorRadius", config.CursorRadius);
                        config.StickSwap = GetBool(root, "StickSwap", config.StickSwap);
                        config.VibrationEnabled = GetBool(root, "VibrationEnabled", config.VibrationEnabled);
                        config.VibrationDuration = GetFloat(root, "VibrationDuration", config.VibrationDuration);
                        config.GyroEnabled = GetBool(root, "GyroEnabled", config.GyroEnabled);
                        config.GyroSensitivity = GetFloat(root, "GyroSensitivity", config.GyroSensitivity);
                        config.GyroSource = GetInt(root, "GyroSource", config.GyroSource);
                        config.BackendMode = GetInt(root, "BackendMode", config.BackendMode);
                        config.TouchpadCursor = GetBool(root, "TouchpadCursor", config.TouchpadCursor);
                        config.SlotWheelEnabled = GetBool(root, "SlotWheelEnabled", config.SlotWheelEnabled);

                        XElement bindingsElement = root.Element("Bindings");
                        if (bindingsElement != null)
                        {
                            List<BindingEntry> bindings = new List<BindingEntry>();
                            foreach (XElement entry in bindingsElement.Elements("BindingEntry"))
                            {
                                string action = (string)entry.Element("Action");
                                string buttonName = (string)entry.Element("Button");
                                if (!string.IsNullOrEmpty(action) && Enum.TryParse(buttonName, out GPadButton button))
                                {
                                    bindings.Add(new BindingEntry { Action = action, Button = button });
                                }
                            }
                            config.Bindings = bindings.ToArray();
                        }
                        return config;
                    }
                }
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: failed to load config: " + e.Message);
            }
            return new ModConfig();
        }

        public void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                XElement bindings = new XElement("Bindings",
                    (Bindings ?? new BindingEntry[0]).Select(b => new XElement("BindingEntry",
                        new XElement("Action", b.Action),
                        new XElement("Button", b.Button.ToString()))));
                XElement root = new XElement("GamepadInputConfig",
                    new XElement("CursorSpeed", CursorSpeed.ToString(CultureInfo.InvariantCulture)),
                    new XElement("CursorDeadzone", CursorDeadzone.ToString(CultureInfo.InvariantCulture)),
                    new XElement("MoveThreshold", MoveThreshold.ToString(CultureInfo.InvariantCulture)),
                    new XElement("CursorRadius", CursorRadius.ToString(CultureInfo.InvariantCulture)),
                    new XElement("StickSwap", StickSwap),
                    new XElement("VibrationEnabled", VibrationEnabled),
                    new XElement("VibrationDuration", VibrationDuration.ToString(CultureInfo.InvariantCulture)),
                    new XElement("GyroEnabled", GyroEnabled),
                    new XElement("GyroSensitivity", GyroSensitivity.ToString(CultureInfo.InvariantCulture)),
                    new XElement("GyroSource", GyroSource.ToString(CultureInfo.InvariantCulture)),
                    new XElement("BackendMode", BackendMode.ToString(CultureInfo.InvariantCulture)),
                    new XElement("TouchpadCursor", TouchpadCursor),
                    new XElement("SlotWheelEnabled", SlotWheelEnabled),
                    bindings);
                new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(path);
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: failed to save config: " + e.Message);
            }
        }

        private static float GetFloat(XElement parent, string name, float fallback)
        {
            XElement element = parent.Element(name);
            if (element == null) { return fallback; }
            return float.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private static int GetInt(XElement parent, string name, int fallback)
        {
            XElement element = parent.Element(name);
            if (element == null) { return fallback; }
            return int.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private static bool GetBool(XElement parent, string name, bool fallback)
        {
            XElement element = parent.Element(name);
            if (element == null) { return fallback; }
            return bool.TryParse(element.Value, out bool value) ? value : fallback;
        }
    }
}
