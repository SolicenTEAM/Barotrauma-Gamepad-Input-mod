using System;
using System.Linq;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace GamePadInput
{
    internal static class PauseMenuButton
    {
        private const string ButtonTag = "GamepadInputOptions";
        private static bool pendingAdd;

        public static void Apply(Harmony harmony)
        {
            MethodInfo method = typeof(GUI).GetMethod("TogglePauseMenu", Type.EmptyTypes);
            if (method == null)
            {
                LuaCsLogger.LogMessage("GamepadInput: GUI.TogglePauseMenu not found, pause menu button skipped");
                return;
            }
            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(PauseMenuButton).GetMethod(nameof(TogglePauseMenuPostfix), BindingFlags.NonPublic | BindingFlags.Static)));
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: failed to patch pause menu: " + e.Message);
            }
        }

        private static void TogglePauseMenuPostfix()
        {
            pendingAdd = GUI.PauseMenuOpen && GUI.PauseMenu != null;
        }

        public static void UpdatePending()
        {
            if (!pendingAdd) { return; }
            pendingAdd = false;
            try
            {
                if (!GUI.PauseMenuOpen || GUI.PauseMenu == null) { return; }

                GUILayoutGroup container = GUI.PauseMenu.GetAllChildren<GUILayoutGroup>().FirstOrDefault();
                if (container == null) { return; }
                if (container.Children.Any(c => c.UserData as string == ButtonTag)) { return; }

                GUIButton button = new GUIButton(new RectTransform(new Vector2(1.0f, 0.1f), container.RectTransform), "Gamepad Input", style: "GUIButtonSmall")
                {
                    UserData = ButtonTag,
                    OnClicked = (btn, obj) =>
                    {
                        LuaCsLogger.LogMessage("GamepadInput: pause menu button clicked");
                        OptionsUI.RequestToggle();
                        return true;
                    }
                };
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: ERROR adding pause menu button: " + e.Message);
            }
        }
    }
}
