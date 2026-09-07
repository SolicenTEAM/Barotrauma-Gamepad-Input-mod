using System;
using System.Collections.Generic;
using System.Reflection;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GamePadInput
{
    internal static class VirtualInput
    {
        private const int PulseReads = 2;

        private static readonly HashSet<Keys> HeldKeys = new HashSet<Keys>();
        private static readonly Dictionary<Keys, int> PulseKeys = new Dictionary<Keys, int>();

        private static bool leftHeld;
        private static bool rightHeld;
        private static bool middleHeld;
        private static int middlePulse;

        public static bool ModActive;
        public static bool MouseActive;
        public static Vector2 MousePos;
        public static MouseState LastRealMouseState;
        public static KeyboardState LastRealKeyboardState;
        public static bool StripEscape;

        public static void ApplyPatches(Harmony harmony)
        {
            MethodInfo keyboardPostfix = typeof(VirtualInput).GetMethod(nameof(KeyboardGetStatePostfix), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Patch(harmony, typeof(Keyboard).GetMethod("GetState", Type.EmptyTypes), keyboardPostfix);
            Patch(harmony, typeof(Keyboard).GetMethod("GetState", new[] { typeof(PlayerIndex) }), keyboardPostfix);

            MethodInfo mousePostfix = typeof(VirtualInput).GetMethod(nameof(MouseGetStatePostfix), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Patch(harmony, typeof(Mouse).GetMethod("GetState", Type.EmptyTypes), mousePostfix);
            Patch(harmony, typeof(Mouse).GetMethod("GetState", new[] { typeof(GameWindow) }), mousePostfix);
        }

        private static void Patch(Harmony harmony, MethodInfo method, MethodInfo postfix)
        {
            if (method == null || postfix == null)
            {
                LuaCsLogger.LogMessage("GamepadInput: ERROR input method not found, mod may not work correctly");
                return;
            }
            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage($"GamepadInput: ERROR failed to patch {method.DeclaringType}.{method.Name}: {e.Message}");
            }
        }

        private static void KeyboardGetStatePostfix(ref KeyboardState __result)
        {
            LastRealKeyboardState = __result;

            if (StripEscape && __result.IsKeyDown(Keys.Escape))
            {
                List<Keys> withoutEscape = new List<Keys>(__result.GetPressedKeys());
                withoutEscape.Remove(Keys.Escape);
                __result = new KeyboardState(withoutEscape.ToArray(), __result.CapsLock, __result.NumLock);
            }

            if (HeldKeys.Count == 0 && PulseKeys.Count == 0) { return; }

            Keys[] baseKeys = __result.GetPressedKeys();
            List<Keys> merged = new List<Keys>(baseKeys.Length + HeldKeys.Count + PulseKeys.Count);
            merged.AddRange(baseKeys);
            foreach (Keys key in HeldKeys)
            {
                if (!merged.Contains(key)) { merged.Add(key); }
            }
            foreach (Keys key in PulseKeys.Keys)
            {
                if (!merged.Contains(key)) { merged.Add(key); }
            }
            __result = new KeyboardState(merged.ToArray(), __result.CapsLock, __result.NumLock);
        }

        private static void MouseGetStatePostfix(ref MouseState __result)
        {
            LastRealMouseState = __result;

            bool baseLeft = __result.LeftButton == ButtonState.Pressed;
            bool baseRight = __result.RightButton == ButtonState.Pressed;
            bool baseMiddle = __result.MiddleButton == ButtonState.Pressed;

            bool posOverride = ModActive && MouseActive;
            bool left = baseLeft || (ModActive && leftHeld);
            bool right = baseRight || (ModActive && rightHeld);
            bool middle = baseMiddle || (ModActive && (middleHeld || middlePulse > 0));

            if (!posOverride && left == baseLeft && right == baseRight && middle == baseMiddle) { return; }

            int x = posOverride ? (int)MousePos.X : __result.X;
            int y = posOverride ? (int)MousePos.Y : __result.Y;
            __result = new MouseState(
                x, y, __result.ScrollWheelValue,
                left ? ButtonState.Pressed : ButtonState.Released,
                middle ? ButtonState.Pressed : ButtonState.Released,
                right ? ButtonState.Pressed : ButtonState.Released,
                __result.XButton1, __result.XButton2,
                __result.HorizontalScrollWheelValue);
        }

        public static void ActivateMouse()
        {
            MousePos = new Vector2(LastRealMouseState.X, LastRealMouseState.Y);
            MouseActive = true;
        }

        public static void MoveCursor(float dx, float dy)
        {
            if (!MouseActive) { return; }
            MousePos.X += dx;
            MousePos.Y += dy;
        }

        public static void ClampCursor(float minX, float minY, float maxX, float maxY)
        {
            if (!MouseActive) { return; }
            MousePos.X = MathHelper.Clamp(MousePos.X, minX, maxX);
            MousePos.Y = MathHelper.Clamp(MousePos.Y, minY, maxY);
        }

        public static void ClampCursorToScreen()
        {
            if (!MouseActive) { return; }
            MousePos.X = MathHelper.Clamp(MousePos.X, 0f, GameMain.GraphicsWidth - 1);
            MousePos.Y = MathHelper.Clamp(MousePos.Y, 0f, GameMain.GraphicsHeight - 1);
        }

        public static void PressKey(Keys key)
        {
            if (key == Keys.None) { return; }
            if (HeldKeys.Contains(key) || PulseKeys.ContainsKey(key)) { return; }
            PulseKeys[key] = PulseReads;
        }

        public static void SetKeyHeld(Keys key, bool held)
        {
            if (key == Keys.None) { return; }
            if (held)
            {
                PulseKeys.Remove(key);
                HeldKeys.Add(key);
            }
            else
            {
                HeldKeys.Remove(key);
            }
        }

        public static void SetMouseHeld(bool left, bool right)
        {
            leftHeld = left;
            rightHeld = right;
        }

        public static void ClickMiddle()
        {
            middlePulse = PulseReads;
        }

        public static void Tick()
        {
            if (PulseKeys.Count > 0)
            {
                List<Keys> expired = null;
                foreach (KeyValuePair<Keys, int> pair in PulseKeys)
                {
                    if (pair.Value <= 1)
                    {
                        if (expired == null) { expired = new List<Keys>(); }
                        expired.Add(pair.Key);
                    }
                    else
                    {
                        PulseKeys[pair.Key] = pair.Value - 1;
                    }
                }
                if (expired != null)
                {
                    foreach (Keys key in expired) { PulseKeys.Remove(key); }
                }
            }
            if (middlePulse > 0) { middlePulse--; }
        }

        public static void ReleaseHeld()
        {
            HeldKeys.Clear();
            leftHeld = false;
            rightHeld = false;
            middleHeld = false;
        }

        public static void ReleaseAll()
        {
            ReleaseHeld();
            PulseKeys.Clear();
            middlePulse = 0;
            MouseActive = false;
        }
    }
}
