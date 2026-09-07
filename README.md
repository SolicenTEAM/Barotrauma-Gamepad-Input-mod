# [Gamepad Input for Barotrauma](https://steamcommunity.com/sharedfiles/filedetails/?id=2967824521)
This mod adds full gamepad controls and vibration to Barotrauma.<br>
*Note: Steam Input may intercept your controller — the mod works both ways. With Steam Input disabled, the SDL backend reads the pad directly. With Steam Input enabled, motion/gyro comes through the Steam Input API (select the source in the mod options: Auto / SDL direct / Steam Input). If controls misbehave with Steam Input on, disable it: right click Barotrauma → Properties → Controller → Override: "Disable Steam Input".*

* This is repository of the mod in Steam Workshop - **Gamepad Input for Barotrauma**
* If you want to help us with this mod, you can create issue or fork of this repo and take your pull request to changes this.

Read full readme in Steam Workshop page.

## Our goals:
- [x] Create UI and options for detailed customization of your input control in game.
- [ ] Replace old XNA Input in MonoGame to hook Steam Input directly and full support all controllers based on Steam Input.

## Roadmap:

### Phase 0 — Legacy version bugfixes (done in v0.2.0)
- [x] mod toggle fired every frame while the combo was held — fixed with edge detection (`Start+Back` was removed later, see Phase 2)
- [x] `KeyPress` sent KeyUp *before* KeyDown (inverted WinAPI emulation sequence)
- [x] `async void` + `Task.Delay` races causing stuck buttons — replaced with frame-based pulses and timer-based rumble
- [x] inverted DPad crouch-toggle logic; per-frame `SendInput` spam while crouch was toggled
- [x] `GKey` bindings cached once at mod load — game rebinds were ignored; bindings are now read dynamically from `KeyMap` / `InventoryKeyMap` on every use
- [x] `Stop()` now unhooks all patches, releases stuck virtual keys and stops vibration

### Phase 1 — Cross-platform engine-level input (v0.2.0)
- [x] removed all WinAPI emulation (`user32.dll`: SendInput / SetCursorPos / mouse_event / MapVirtualKey, Console.Beep)
- [x] virtual gamepad state injected directly into the engine: Harmony postfixes on MonoGame `Keyboard.GetState()` / `Mouse.GetState()` (SDL2 backend, works on Win / Linux / Steam Deck / macOS)
- [x] cursor and mouse buttons are fully virtual — no OS-level input leakage, DPI/window-focus safe, works under Steam Remote Play
- [x] analog sticks with configurable dead zones, independent axes (diagonal movement), frame-rate-independent cursor speed, on-screen cursor clamping
- [x] vibration via `GamePad.SetVibration` (SDL haptics), triggered by damage, auto-stopped by timer
- [x] mod activation: `Alt+J` or gamepad combo `LB+RB+D-Down+A` (fires once per press)

### Phase 2 — In-game settings UI (v0.3.x – v0.4.x)
- [x] options window on the native Barotrauma GUI (`GUIFrame` / `GUIListBox` / `GUIScrollBar` / `GUITickBox`), parented to the active screen frame, centered, scrollable content
- [x] opened from: pause menu button, `F8`, or holding Start for 0.6 s
- [x] sliders: cursor speed, cursor deadzone, move threshold, cursor lock radius, vibration time, gyro sensitivity
- [x] tickboxes: vibration, gyro cursor, touchpad cursor, item wheel
- [x] full gamepad rebinding: 13 actions, click a slot → press a gamepad button (press-to-bind capture ignores buttons already held at click time), `Esc` cancels
- [x] config persisted to XML in the game user data folder (`%LOCALAPPDATA%/Daedalic Entertainment GmbH/Barotrauma/GamepadInputConfig.xml`), invariant-culture number parsing
- [x] `Esc` closes the window: Escape is stripped from the game's keyboard state while the window is open and handled by the mod itself; `B` on gamepad also closes it
- [x] when opened from pause the window lives inside the open pause menu (like other mods' windows), otherwise inside the screen
- [x] pause menu button added via Harmony postfix on `GUI.TogglePauseMenu` (small `GUIButtonSmall` style, appended below other mods' buttons)

### Steam Input compatibility (v0.3.1)
- [x] Steam build detection via reflection (`Facepunch.Steamworks` `SteamClient.IsValid` — compiles even without the assembly referenced)
- [x] Steam virtual gamepad detection (vendor ID `0x28DE` / SDL type `VIRTUAL`)
- [x] status line in the options window (device name, battery, Steam Input warning) + one-time hint when activation finds no gamepad, with step-by-step instructions to disable Steam Input

### Phase 3 — Native SDL2 backend (v0.4.0)
- [x] `IGamePadBackend` abstraction with a normalized `PadSnapshot` (buttons, sticks, triggers, gyro, touchpad, battery, device identity)
- [x] `SdlPadBackend`: direct SDL2 P/Invoke against the SDL2 already shipped with the game (`SDL2.dll` / `libSDL2-2.0.so.0` / `.dylib`); functions from SDL 2.0.6+ (sensors, rumble, touchpads, battery) are optional exports and degrade gracefully on older SDL
- [x] `MonoPadBackend`: fallback on MonoGame `GamePad` — automatic backend selection at load, retry every 2 s, hot-swap at runtime
- [x] gyro cursor aiming (`SDL_SENSOR_GYRO`, 2°/s dead zone, configurable sensitivity) for Steam Deck / DualShock / Switch controllers
- [x] touchpad cursor for DualShock/DualSense; battery status; controller type & vendor identification; hot-plug (auto reopen after disconnect); SDL rumble with duration-based auto stop

### Item wheel — safe inventory selection (v0.5.0+)
- [x] old blind LB/RB cycling (reported by users as dangerous: random items end up in hands) replaced by a radial wheel: hold a bumper → wheel opens around the crosshair with 10 slots — slot numbers, item icons, empty slots dimmed
- [x] selection by left stick pointing (like weapon wheels everywhere): one flick reaches any slot; character movement and cursor input are suppressed while the wheel is open; there is a re-center guard so opening while walking does not auto-select a slot
- [x] additional step inputs: D-pad left/right and the opposite bumper tap; release the bumper → equips the selected slot; opening without changing selection → nothing equipped (safe); `B` cancels; 4 s idle timeout auto-cancels; mode is force-closed on menu open / disconnect / mod disable
- [x] selected slot is clearly highlighted: bright outline (black + green border), brightened cell, green slot number, flash on every selection change
- [x] legacy blind cycling still available as a toggle in the options window
### Phase 4 — Steam Input backend (v0.5.4–0.5.9, done with platform notes)

- [x] gyro via the Steam Input API: motion read from `ISteamInput::GetMotionData` through Facepunch.Steamworks (reflection); buttons keep coming through the emulated pad (SDL or MonoGame)
- [x] `SteamPadBackend` — full backend behind the same `IPadBackend` abstraction: wraps the emulated pad for buttons/axes/touch, adds Steam gyro, Steam haptics (HD rumble on Joy-Con/DualShock, falls back to the inner rumble) and device identification (device type in the status line)
- [x] own action manifest (`game_actions_4920.vdf`, SDL-style legacy set) auto-generated to `%LOCALAPPDATA%/Daedalic Entertainment GmbH/Barotrauma/GamepadInput/steam_input/` and loaded via `SetInputActionManifestPath` (raw `steam_api64` call, optional — older SDKs just skip it); digital/analog actions polled as a button source when no emulated pad exists
- [x] selectable in options: pad backend (Auto / SDL direct / Steam Input / MonoGame) and gyro source (Auto / SDL direct / Steam Input); Auto picks SDL direct for real pads and wraps into Steam Input when a virtual pad is detected or SDL is unavailable
- [x] platform reality (v0.5.9): **Windows build of Barotrauma has no SDL** (MonoGame WindowsDX + SharpDX.XInput) — the SDL backend is Linux/Deck-only; on Windows the direct path is XInput (via MonoGame). Steam Input for a "non-controller game" runs the keyboard/mouse template and hides the pad — users must set a **Gamepad template** layout for Barotrauma in Steam (then the virtual pad is visible) or disable Steam Input
- [ ] future: drive custom mod actions through the manifest handles (contextual buttons)
