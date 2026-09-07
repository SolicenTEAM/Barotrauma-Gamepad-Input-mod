using System;
using Barotrauma;
using Microsoft.Xna.Framework.Input;

public static class GKey
{
    public static Keys Get(InputType inputType)
    {
        try
        {
            KeyOrMouse binding = GameSettings.CurrentConfig.KeyMap.Bindings[inputType];
            return binding.MouseButton == MouseButton.None ? binding.Key : Keys.None;
        }
        catch
        {
            return Keys.None;
        }
    }

    public static Keys InventorySlot(int index)
    {
        try
        {
            var bindings = GameSettings.CurrentConfig.InventoryKeyMap.Bindings;
            if (index < 0 || index >= bindings.Length) { return Keys.None; }
            KeyOrMouse binding = bindings[index];
            return binding.MouseButton == MouseButton.None ? binding.Key : Keys.None;
        }
        catch
        {
            return Keys.None;
        }
    }

    public static Keys Ragdoll => Get(InputType.Ragdoll);
    public static Keys Select => Get(InputType.Select);
    public static Keys Attack => Get(InputType.Attack);
    public static Keys Aim => Get(InputType.Aim);
    public static Keys Up => Get(InputType.Up);
    public static Keys Down => Get(InputType.Down);
    public static Keys Left => Get(InputType.Left);
    public static Keys Right => Get(InputType.Right);
    public static Keys InfoTab => Get(InputType.InfoTab);
    public static Keys Health => Get(InputType.Health);
    public static Keys Grab => Get(InputType.Grab);
    public static Keys Shoot => Get(InputType.Shoot);
    public static Keys Use => Get(InputType.Use);
    public static Keys Run => Get(InputType.Run);
    public static Keys Command => Get(InputType.Command);
    public static Keys CrewOrders => Get(InputType.CrewOrders);
    public static Keys Crouch => Get(InputType.Crouch);
}
