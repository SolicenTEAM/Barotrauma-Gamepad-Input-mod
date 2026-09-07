---
name: Controller / device compatibility
about: Your specific controller does not work, or part of it (gyro, rumble, touchpad, buttons) is missing
labels: ["compatibility"]
---

<!--
Use this template when a specific controller model doesn't work at all,
or works partially (e.g. buttons fine but no gyro / no rumble / no touchpad).
-->

## Your controller

| Item | Value |
| --- | --- |
| Controller model | <!-- e.g. "Nintendo Switch Joy-Con (L)+(R)", "DualSense", "8BitDo Pro 2" --> |
| Connection | <!-- USB / Bluetooth / adapter (which one?) --> |
| Platform | <!-- Windows / Linux / Steam Deck --> |
| Steam Input | <!-- Enabled (which layout template — Gamepad / keyboard-mouse / custom?) / Disabled --> |
| How is the pad visible to the PC? | <!-- What does Windows "Set up USB game controllers" (joy.cpl) show? Or on Deck/Linux — is it detected by the system? --> |

## What works

<!-- Tick what you SEE working in game:
- [ ] Buttons (A/B/X/Y, bumpers, triggers...)
- [ ] Sticks (movement / cursor)
- [ ] DPad
- [ ] Cursor movement with stick
- [ ] Item wheel (hold RB/LB)
- [ ] Gyro (cursor moves when tilting the controller)
- [ ] Vibration / rumble
- [ ] Touchpad cursor (DS4 / DS5)
- [ ] Battery level in the mod's options window
-->

## What does NOT work

<!-- Describe precisely: which buttons/actions are dead, inverted, or weird.
Mention e.g. "cursor with gyro moves into the wrong direction", "LB/RB do nothing". -->

## Mod's options window — status line

<!-- Open the mod options (F8 / hold Start) and copy the status text under the settings.
Example: "Gamepad: Steam Input: Joy-Con pair (input: Steam Input) | Gyro: available (via Steam Input)" -->

```
<paste status text here>
```

## Console log

<!-- Every line starting with "GamepadInput:" from the in-game console (F3).
These lines tell us which backend was chosen and what the sensors/actions did:
```
<paste lines here>
```
-->

## Steps you already tried

<!-- e.g. "disabled Steam Input", "set Gamepad template in Steam", "re-paired the Joy-Cons",
"installed DS4Windows", "restarted the game" — so we don't ask you to do it again. -->

## Anything else?

<!-- Firmware version of the controller, adapters/dongles, other mods that hook input, etc. -->
