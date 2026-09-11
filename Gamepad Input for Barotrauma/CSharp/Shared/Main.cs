using System;
using System.Collections.Generic;
using System.Diagnostics;
using Barotrauma;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GamePadInput
{
    internal class GamePadHook : ACsMod
    {
        public const string HarmonyId = "GamePadInput.Barotrauma";

        private const float GyroBaseSpeed = 15f;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();

        private Harmony harmony;
        private readonly ModConfig config;
        private readonly OptionsUI optionsUI;

        private readonly SdlPadBackend sdlBackend = new SdlPadBackend();
        private readonly MonoPadBackend monoBackend = new MonoPadBackend();
        private IPadBackend padBackend;
        private float backendRetryTimer;

        private bool isModActive;
        private bool isStopped;
        private bool lockCursor = true;
        private bool lockCrouch;
        private int slot;
        private int appliedBackendMode = -1;

        private bool altJHeld;
        private bool comboHeld;
        private bool prevRawEscape;
        private float escapeHoldTimer;
        private bool escapeLongPressDone;
        private readonly SlotWheel slotWheel = new SlotWheel();
        private bool wheelOpen;
        private bool wheelStickReady;
        private double wheelLastChange;

        private readonly Dictionary<GPadButton, bool> prevButtons = new Dictionary<GPadButton, bool>();

        private Character lastCharacter;
        private float lastHp = 100f;
        private double rumbleUntil;
        private double lastFrameTime = -1.0;
        private bool steamInputHintShown;

        public GamePadHook()
        {
            try
            {
                harmony = new Harmony(HarmonyId);
                VirtualInput.ApplyPatches(harmony);

                config = ModConfig.Load();
                padBackend = SelectBackend();
                if (padBackend == null) { padBackend = monoBackend; }
                appliedBackendMode = config.BackendMode;
                optionsUI = new OptionsUI(config, () => padBackend.Poll());
                OptionsUI.BackendName = padBackend.Name;
                OptionsUI.ToggleRequested = () => optionsUI.Toggle();
                PauseMenuButton.Apply(harmony);
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: init failed:\n" + e);
                throw;
            }

            LuaCsLogger.LogMessage("——— GamepadInput: initialized (input backend: " + padBackend.Name + ") ———");
            GameMain.LuaCs.Hook.HookMethod("gamepad_hook",
                typeof(PlayerInput).GetMethod("Update"),
                (object self, Dictionary<string, object> args) =>
                {
                    if (!isStopped) { Update(); }
                    return true;
                },
                LuaCsHook.HookMethodType.After, this);
        }

        public override void Stop()
        {
            isStopped = true;
            OptionsUI.ToggleRequested = null;
            SteamGyro.Shutdown();
            try { optionsUI?.Close(); } catch { }
            try { VirtualInput.ReleaseAll(); } catch { }
            try { padBackend?.StopRumble(); } catch { }
            try { harmony?.UnpatchAll(HarmonyId); } catch { }
            LuaCsLogger.LogMessage("——— GamepadInput: stopped ———");
        }

        private void Update()
        {
            double now = Clock.Elapsed.TotalSeconds;
            float dt = lastFrameTime < 0.0 ? 0f : (float)Math.Min(now - lastFrameTime, 0.1);
            lastFrameTime = now;

            UpdateBackend(dt);
            PadSnapshot pad = padBackend.Poll();
            SteamGyro.Poll(now);

            HandleActivation(pad);

            PauseMenuButton.UpdatePending();

            bool wasStripped = VirtualInput.StripEscape;
            bool rawEscapeHeld = VirtualInput.LastRealKeyboardState.IsKeyDown(Keys.Escape);
            VirtualInput.StripEscape = optionsUI.IsOpen || (wasStripped && rawEscapeHeld);

            if (optionsUI.IsOpen)
            {
                if (optionsUI.BuiltInPause && !GUI.PauseMenuOpen) { optionsUI.Discard(); }
                else if (rawEscapeHeld && !prevRawEscape)
                {
                    if (optionsUI.IsCapturing) { optionsUI.CancelCapture(); }
                    else { optionsUI.Close(); }
                }
            }
            prevRawEscape = rawEscapeHeld;

            GPadButton escapeButton = config.GetBinding("Escape");
            if (pad.IsDown(escapeButton))
            {
                escapeHoldTimer += dt;
                if (!escapeLongPressDone && escapeHoldTimer >= 0.6f)
                {
                    escapeLongPressDone = true;
                    optionsUI.Toggle();
                }
            }
            else
            {
                if (escapeHoldTimer > 0f)
                {
                    if (!escapeLongPressDone && escapeHoldTimer < 0.6f && !optionsUI.IsOpen) { VirtualInput.PressKey(Keys.Escape); }
                    escapeHoldTimer = 0f;
                    escapeLongPressDone = false;
                }
            }

            if (PlayerInput.KeyHit(Keys.F8)) { optionsUI.Toggle(); }

            if (!isModActive) { return; }

            VirtualInput.Tick();
            UpdateRumble();

            if (!pad.IsConnected || !GameMain.WindowActive)
            {
                VirtualInput.ReleaseHeld();
                CancelSlotWheel();
                return;
            }

            if (optionsUI.IsOpen)
            {
                HandleOpenUI(pad, dt);
                return;
            }

            Character controlled = Character.Controlled;
            bool inMenu = IsInMenu(controlled);
            bool inCommandMenu = CrewManager.IsCommandInterfaceOpen;

            bool a = pad.IsDown(GPadButton.A);
            bool rb = pad.IsDown(GPadButton.RB);
            bool lb = pad.IsDown(GPadButton.LB);
            bool dpadDown = pad.IsDown(GPadButton.DPadDown);
            bool comboActive = lb && rb && dpadDown && a;

            bool slotNextPressed = PressedEdge(pad, config.GetBinding("SlotNext"));
            bool slotPrevPressed = PressedEdge(pad, config.GetBinding("SlotPrev"));
            bool usePressed = PressedEdge(pad, config.GetBinding("Use"));
            bool infoTabPressed = PressedEdge(pad, config.GetBinding("InfoTab"));
            bool middleClickPressed = PressedEdge(pad, config.GetBinding("MiddleClick"));
            bool healthPressed = PressedEdge(pad, config.GetBinding("Health"));
            bool grabPressed = PressedEdge(pad, config.GetBinding("Grab"));
            bool crewOrdersPressed = PressedEdge(pad, config.GetBinding("CrewOrders"));
            bool cursorLockPressed = PressedEdge(pad, config.GetBinding("CursorLockToggle"));
            bool crouchTogglePressed = PressedEdge(pad, config.GetBinding("CrouchToggle"));
            bool dpadLeftPressed = PressedEdge(pad, GPadButton.DPadLeft);
            bool dpadRightPressed = PressedEdge(pad, GPadButton.DPadRight);

            if (controlled != null)
            {
                if (lastCharacter != controlled)
                {
                    lastCharacter = controlled;
                    lastHp = controlled.Health;
                }
                if (controlled.Health < lastHp) { StartRumble(1f); }
                lastHp = controlled.Health;
            }

            if (wheelOpen)
            {
                VirtualInput.ClampCursorToScreen();
            }
            else
            {
                bool swapSticks = config.StickSwap && inMenu;
                ApplyPointer(pad, dt, swapSticks ? pad.LeftX : pad.RightX, swapSticks ? pad.LeftY : pad.RightY);
                if (!inMenu && lockCursor && !inCommandMenu)
                {
                    float halfRadius = config.CursorRadius / 2f;
                    float centerX = GameMain.GraphicsWidth / 2f;
                    float centerY = GameMain.GraphicsHeight / 2f;
                    VirtualInput.ClampCursor(centerX - halfRadius, centerY - halfRadius, centerX + halfRadius, centerY + halfRadius);
                }
                VirtualInput.ClampCursorToScreen();

                float moveX = swapSticks ? pad.RightX : pad.LeftX;
                float moveY = swapSticks ? pad.RightY : pad.LeftY;
                VirtualInput.SetKeyHeld(GKey.Left, moveX < -config.MoveThreshold);
                VirtualInput.SetKeyHeld(GKey.Right, moveX > config.MoveThreshold);
                VirtualInput.SetKeyHeld(GKey.Up, moveY > config.MoveThreshold);
                VirtualInput.SetKeyHeld(GKey.Down, moveY < -config.MoveThreshold);
            }

            VirtualInput.SetMouseHeld(!wheelOpen && !comboActive && (a || pad.IsDown(GPadButton.RT)), pad.IsDown(GPadButton.LT));
            VirtualInput.SetKeyHeld(GKey.Run, pad.IsDown(config.GetBinding("Run")));
            VirtualInput.SetKeyHeld(GKey.Ragdoll, !wheelOpen && pad.IsDown(config.GetBinding("Ragdoll")) && controlled != null);
            VirtualInput.SetKeyHeld(GKey.Crouch, !wheelOpen && lockCrouch && controlled != null);

            if (!wheelOpen)
            {
                if (infoTabPressed) { VirtualInput.PressKey(GKey.InfoTab); }
                if (middleClickPressed) { VirtualInput.ClickMiddle(); }
                if (usePressed)
                {
                    if (!inMenu && !inCommandMenu) { VirtualInput.PressKey(GKey.Use); }
                    else { VirtualInput.PressKey(Keys.Escape); }
                }

                if (controlled != null)
                {
                    if (healthPressed) { VirtualInput.PressKey(GKey.Health); }
                    if (grabPressed) { VirtualInput.PressKey(GKey.Grab); }
                    if (crewOrdersPressed) { VirtualInput.PressKey(GKey.CrewOrders); }
                }

                if (cursorLockPressed) { lockCursor = !lockCursor; }
                if (crouchTogglePressed && !comboActive) { lockCrouch = !lockCrouch; }
            }

            if (config.SlotWheelEnabled)
            {
                UpdateSlotWheel(pad, inMenu, comboActive, slotNextPressed, slotPrevPressed, dpadLeftPressed, dpadRightPressed, usePressed);
            }
            else if (controlled != null && !comboActive)
            {
                if (slotNextPressed) { CycleSlot(1); }
                if (slotPrevPressed) { CycleSlot(-1); }
            }
        }

        private void UpdateSlotWheel(PadSnapshot pad, bool inMenu, bool comboActive, bool slotNextPressed, bool slotPrevPressed, bool dpadLeftPressed, bool dpadRightPressed, bool usePressed)
        {
            if (!wheelOpen)
            {
                if (comboActive || (!slotNextPressed && !slotPrevPressed)) { return; }
                wheelOpen = true;
                wheelStickReady = (float)Math.Sqrt(pad.LeftX * pad.LeftX + pad.LeftY * pad.LeftY) < 0.3f;
                wheelLastChange = Clock.Elapsed.TotalSeconds;
                slotWheel.OpenWheel(slot);
                return;
            }

            if (inMenu || optionsUI.IsOpen || !pad.IsConnected) { CancelSlotWheel(); return; }
            if (usePressed) { CancelSlotWheel(); return; }
            if (Clock.Elapsed.TotalSeconds > wheelLastChange + 4.0) { CancelSlotWheel(); return; }

            float stickX = pad.LeftX;
            float stickY = pad.LeftY;
            float magnitude = (float)Math.Sqrt(stickX * stickX + stickY * stickY);
            if (!wheelStickReady && magnitude < 0.3f) { wheelStickReady = true; }
            if (wheelStickReady && magnitude > 0.45f)
            {
                double angleDeg = Math.Atan2(-stickY, stickX) * 180.0 / Math.PI;
                double rel = (angleDeg + 90.0 + 360.0) % 360.0;
                int idx = ((int)Math.Round(rel / 36.0)) % 10;
                if (slotWheel.Select(idx)) { wheelLastChange = Clock.Elapsed.TotalSeconds; }
            }

            int step = 0;
            if (pad.IsDown(config.GetBinding("SlotNext")) && slotPrevPressed) { step = 1; }
            else if (pad.IsDown(config.GetBinding("SlotPrev")) && slotNextPressed) { step = -1; }
            else if (dpadLeftPressed) { step = -1; }
            else if (dpadRightPressed) { step = 1; }
            if (step != 0 && slotWheel.Move(step)) { wheelLastChange = Clock.Elapsed.TotalSeconds; }

            bool bumperHeld = pad.IsDown(config.GetBinding("SlotNext")) || pad.IsDown(config.GetBinding("SlotPrev"));
            if (!bumperHeld)
            {
                int keyIndex = slotWheel.KeyIndexAt(slotWheel.SelectedIndex);
                if (slotWheel.SelectedIndex != slotWheel.InitialIndex && keyIndex >= 0)
                {
                    VirtualInput.PressKey(GKey.InventorySlot(keyIndex));
                    slot = keyIndex;
                }
                wheelOpen = false;
                slotWheel.Close();
            }
        }

        private void CancelSlotWheel()
        {
            if (!wheelOpen) { return; }
            wheelOpen = false;
            slotWheel.Close();
        }

        private IPadBackend SelectBackend()
        {
            try
            {
                switch (config.BackendMode)
                {
                    case 3: return monoBackend;
                    case 1:
                        return sdlBackend.IsAvailable && sdlBackend.TryConnect() ? (IPadBackend)sdlBackend : monoBackend;
                    case 2:
                        IPadBackend steamInner = sdlBackend.IsAvailable ? (IPadBackend)sdlBackend : monoBackend;
                        return WrapSteam(steamInner) ?? steamInner;
                    default:
                        if (sdlBackend.IsAvailable && sdlBackend.TryConnect())
                        {
                            PadSnapshot probe;
                            try { probe = sdlBackend.Poll(); }
                            catch { return sdlBackend; }
                            return probe.IsSteamVirtual ? WrapSteam(sdlBackend) ?? sdlBackend : (IPadBackend)sdlBackend;
                        }
                        return WrapSteam(monoBackend) ?? monoBackend;
                }
            }
            catch (Exception e)
            {
                LuaCsLogger.LogMessage("GamepadInput: backend selection failed, using MonoGame:\n" + e);
                return monoBackend;
            }
        }

        private IPadBackend steamWrapper;
        private IPadBackend steamWrappedInner;

        private IPadBackend WrapSteam(IPadBackend inner)
        {
            if (!SteamInputApi.EnsureReady(-1.0)) { return null; }
            if (steamWrapper != null && steamWrappedInner == inner) { return steamWrapper; }
            steamWrappedInner = inner;
            steamWrapper = new SteamPadBackend(inner);
            return steamWrapper;
        }

        private void UpdateBackend(float dt)
        {
            if (config.BackendMode != appliedBackendMode)
            {
                appliedBackendMode = config.BackendMode;
                padBackend.Disconnect();
                padBackend = SelectBackend() ?? monoBackend;
                OptionsUI.BackendName = padBackend.Name;
                prevButtons.Clear();
                LuaCsLogger.LogMessage("GamepadInput: input backend now " + padBackend.Name);
                return;
            }
            if (padBackend.IsConnected) { backendRetryTimer = 2f; return; }
            backendRetryTimer -= dt;
            if (backendRetryTimer > 0f) { return; }
            backendRetryTimer = 2f;
            IPadBackend selected = SelectBackend();
            if (selected != null && selected != padBackend)
            {
                padBackend = selected;
                OptionsUI.BackendName = padBackend.Name;
                prevButtons.Clear();
                LuaCsLogger.LogMessage("GamepadInput: switched input backend to " + padBackend.Name);
            }
        }

        private void ApplyPointer(PadSnapshot pad, float dt, float stickX, float stickY)
        {
            ApplyDeadzone(ref stickX, ref stickY, config.CursorDeadzone);
            if (dt > 0f && (stickX != 0f || stickY != 0f))
            {
                VirtualInput.MoveCursor(stickX * config.CursorSpeed * dt, -stickY * config.CursorSpeed * dt);
            }

            if (config.GyroEnabled)
            {
                bool preferSteam = config.GyroSource == 2 || (config.GyroSource == 0 && !pad.HasGyro);
                bool steamUsed = preferSteam && SteamGyro.Available;
                if (steamUsed)
                {
                    if (SteamGyro.HasMotion)
                    {
                        float scale = GyroBaseSpeed * config.GyroSensitivity;
                        VirtualInput.MoveCursor(-SteamGyro.Yaw * scale * dt, -SteamGyro.Pitch * scale * dt);
                    }
                }
                else if (pad.HasGyro)
                {
                    float scale = GyroBaseSpeed * config.GyroSensitivity;
                    VirtualInput.MoveCursor(-pad.GyroYaw * scale * dt, -pad.GyroPitch * scale * dt);
                }
            }

            if (config.TouchpadCursor && pad.HasTouchpad && pad.TouchpadDown)
            {
                VirtualInput.MoveCursor(pad.TouchDX * GameMain.GraphicsWidth, pad.TouchDY * GameMain.GraphicsHeight);
            }
        }

        private void HandleOpenUI(PadSnapshot pad, float dt)
        {
            optionsUI.UpdateCapture(pad);

            bool bPressed = PressedEdge(pad, GPadButton.B);
            if (!optionsUI.IsCapturing && bPressed)
            {
                optionsUI.Close();
            }

            ApplyPointer(pad, dt, pad.LeftX, pad.LeftY);
            VirtualInput.ClampCursorToScreen();

            VirtualInput.SetMouseHeld(pad.IsDown(GPadButton.A) || pad.IsDown(GPadButton.RT), pad.IsDown(GPadButton.LT));
            VirtualInput.SetKeyHeld(GKey.Left, false);
            VirtualInput.SetKeyHeld(GKey.Right, false);
            VirtualInput.SetKeyHeld(GKey.Up, false);
            VirtualInput.SetKeyHeld(GKey.Down, false);
            VirtualInput.SetKeyHeld(GKey.Run, false);
            VirtualInput.SetKeyHeld(GKey.Ragdoll, false);
            VirtualInput.SetKeyHeld(GKey.Crouch, false);
        }

        private void HandleActivation(PadSnapshot pad)
        {
            bool altJ = PlayerInput.KeyDown(Keys.J) && PlayerInput.IsAltDown();
            if (altJ)
            {
                if (!altJHeld) { altJHeld = true; Toggle(); }
            }
            else
            {
                altJHeld = false;
            }

            bool combo = pad.IsDown(GPadButton.LB)
                && pad.IsDown(GPadButton.RB)
                && pad.IsDown(GPadButton.DPadDown)
                && pad.IsDown(GPadButton.A);
            if (combo)
            {
                if (!comboHeld) { comboHeld = true; Toggle(); }
            }
            else
            {
                comboHeld = false;
            }
        }

        private void Toggle()
        {
            isModActive = !isModActive;
            VirtualInput.ModActive = isModActive;
            if (isModActive)
            {
                VirtualInput.ActivateMouse();
                LuaCsLogger.LogMessage("GamepadMod enabled");
                ShowSteamInputHintIfNeeded();
            }
            else
            {
                if (optionsUI.IsOpen) { optionsUI.Close(); }
                CancelSlotWheel();
                VirtualInput.ReleaseAll();
                lockCrouch = false;
                LuaCsLogger.LogMessage("GamepadMod disabled");
            }
        }

        private void ShowSteamInputHintIfNeeded()
        {
            if (steamInputHintShown) { return; }
            try
            {
                if (!SteamInputCompat.SteamAvailable) { return; }
                PadSnapshot pad = padBackend.Poll();
                if (pad.IsConnected) { return; }
                steamInputHintShown = true;
                new GUIMessageBox(
                    "Gamepad Input",
                    SteamInputCompat.Instructions,
                    new LocalizedString[] { "Close" },
                    new Vector2(0.55f, 0.4f),
                    new Point(540, 320));
            }
            catch { }
        }

        private void CycleSlot(int direction)
        {
            slot = (slot + direction + 10) % 10;
            VirtualInput.PressKey(GKey.InventorySlot(slot));
        }

        private void StartRumble(float strength)
        {
            if (!config.VibrationEnabled) { return; }
            try
            {
                padBackend.SetRumble(strength, config.VibrationDuration);
                rumbleUntil = Clock.Elapsed.TotalSeconds + config.VibrationDuration;
            }
            catch { }
        }

        private void UpdateRumble()
        {
            if (rumbleUntil > 0.0 && Clock.Elapsed.TotalSeconds >= rumbleUntil)
            {
                try { padBackend.StopRumble(); } catch { }
                rumbleUntil = 0.0;
            }
        }

        private bool PressedEdge(PadSnapshot pad, GPadButton button)
        {
            bool current = pad.IsDown(button);
            prevButtons.TryGetValue(button, out bool previous);
            prevButtons[button] = current;
            return current && !previous;
        }

        private static bool IsInMenu(Character controlled)
        {
            if (controlled == null) { return true; }
            if (controlled.SelectedItem != null) { return true; }
            if (GUI.InputBlockingMenuOpen) { return true; }
            if (CharacterHealth.OpenHealthWindow != null) { return true; }
            if (ConversationAction.IsDialogOpen) { return true; }
            return false;
        }

        private static void ApplyDeadzone(ref float x, ref float y, float deadzone)
        {
            float magnitude = (float)Math.Sqrt(x * x + y * y);
            if (magnitude <= deadzone)
            {
                x = 0f;
                y = 0f;
                return;
            }
            if (magnitude > 1f)
            {
                x /= magnitude;
                y /= magnitude;
            }
        }
    }
}
