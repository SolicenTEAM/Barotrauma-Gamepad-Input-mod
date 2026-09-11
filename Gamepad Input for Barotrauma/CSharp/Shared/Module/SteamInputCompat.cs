using System;
using System.Reflection;

namespace GamePadInput
{
    internal static class SteamInputCompat
    {
        public const string Instructions =
            "Steam Input is intercepting your controller, so the game cannot see it directly.\n\n" +
            "How to fix:\n" +
            "1. Steam Library: right click Barotrauma -> Properties -> Controller -> Override: \"Disable Steam Input\".\n" +
            "2. Or in-game: press Shift+Tab (Steam overlay) -> controller icon -> \"Disable Steam Input\".\n" +
            "3. Restart the game after changing this setting.";

        private static bool? steamAvailable;

        public static bool SteamAvailable
        {
            get
            {
                if (steamAvailable == true) { return true; }
                steamAvailable = QuerySteamAvailable();
                return steamAvailable.Value;
            }
        }

        private static bool QuerySteamAvailable()
        {
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name;
                    try { name = assembly.GetName().Name; }
                    catch { continue; }
                    if (name != "Facepunch.Steamworks") { continue; }
                    Type clientType = assembly.GetType("Steamworks.SteamClient");
                    PropertyInfo property = clientType?.GetProperty("IsValid", BindingFlags.Public | BindingFlags.Static);
                    return property != null && (bool)property.GetValue(null);
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string DescribeStatus(PadSnapshot pad, string backendName = null)
        {
            if (!pad.IsConnected)
            {
                return SteamAvailable
                    ? "No gamepad detected via SDL/MonoGame. Barotrauma's official Steam Input layout emulates keyboard/mouse, so the pad never reaches the mod. Fix: in Steam open this layout screen and set a Gamepad template for Barotrauma (then the mod sees the virtual pad), or disable Steam Input and connect the pad directly."
                    : "No gamepad connected.";
            }
            string text = pad.IsSteamVirtual
                ? "Steam Input detected (\"" + pad.DeviceName + "\"). If controls misbehave, disable Steam Input: right click Barotrauma -> Properties -> Controller."
                : "Gamepad: " + pad.DeviceName;
            if (!string.IsNullOrEmpty(backendName))
            {
                text += " (input: " + backendName + ")";
            }
            if (pad.Power != PadPower.Unknown)
            {
                text += " | Battery: " + pad.Power;
            }
            if (pad.SteamActionsInput)
            {
                text += "\nButtons: via Steam Input actions";
            }
            if (pad.IsSteamVirtual)
            {
                text += SteamGyro.Available
                    ? "\nGyro: via Steam Input"
                    : "\nSteam Input emulation has no gyro and the Steam Input API reports no motion-capable controller.";
                return text;
            }
            text += pad.GyroAvailable || SteamGyro.Available
                ? "\nGyro: available" + (pad.GyroAvailable ? "" : " (via Steam Input)")
                : "\nGyro: not available (enable Steam Input for Barotrauma to read motion from Switch/DualShock pads)";
            return text;
        }
    }
}
