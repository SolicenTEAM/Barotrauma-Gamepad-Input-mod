using System;
using System.Collections.Generic;
using System.Linq;
using Barotrauma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

public static class GKey
{
    public static Keys Ragdoll = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Ragdoll].Key;
    public static Keys Select = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Select].Key;
    public static Keys Attack = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Attack].Key;
    public static Keys Aim = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Aim].Key;
    public static Keys Up = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Up].Key;
    public static Keys Down = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Down].Key;
    public static Keys Left = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Left].Key;
    public static Keys Right = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Right].Key;
    public static Keys InfoTab = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.InfoTab].Key;
    public static Keys Health = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Health].Key;
    public static Keys Grab = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Grab].Key;
    public static Keys Shoot = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Shoot].Key;
    public static Keys Use = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Use].Key;
    public static Keys Run = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Run].Key;
    public static Keys Command = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Command].Key;
    public static Keys CrewOrders = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.CrewOrders].Key;
    public static Keys Crouch = GameSettings.CurrentConfig.KeyMap.Bindings[InputType.Crouch].Key;
}