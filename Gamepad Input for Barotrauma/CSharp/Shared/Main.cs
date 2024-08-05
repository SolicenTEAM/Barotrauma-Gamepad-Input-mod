using System;
using System.Runtime.InteropServices;
using Steamworks;
using System.Reflection;
using System.Linq;
using Barotrauma;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Barotrauma.Extensions;
using EventInput;
partial class TestHook : ACsMod
{
	[DllImport("user32.dll", EntryPoint = "SetCursorPos")]
	[
				return: MarshalAs(UnmanagedType.Bool)
			]
	private static extern bool SetCursorPos(int x, int y);

	[DllImport("user32.dll")]
	[
		return: MarshalAs(UnmanagedType.Bool)
	]
	private static extern bool GetCursorPos(out MousePoint lpMousePoint);

	[DllImport("user32.dll")]
	private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);


	public override void Stop()
	{
		// stopping code, e.g. save custom data
#if SERVER
			// server-side code
#elif CLIENT
			// client-side code
#endif
	}
	public TestHook()
	{

		bool flagA = false;

		bool lockCursor = true;
		bool isModActive = true;
		bool flagActiveCombo = false;

		float CursorX = 0;
		float CursorY = 0;
		int CursorRadius = 175;
		int CursorSpeed = 20;

		int ScreenWidth = GameMain.GraphicsWidth;
		int ScreenHeight = GameMain.GraphicsHeight;
		int ScreenCenterX = ScreenWidth / 2;
		int ScreenCenterY = ScreenHeight / 2;


		SteamInput.RunFrame();
		Character lastCharacter = null;

		LuaCsLogger.LogError("StartHook");
		SteamManager.Init_SteamInput();
		SteamManager.GetAllControlles();

		Vector2 targetMovement = Vector2.Zero;

		var controller = SteamInput.Controllers.ToList()[0];


		GameMain.LuaCs.Hook.HookMethod("gamepad_hook",
			typeof(PlayerInput).GetMethod("Update"),
			(object self, Dictionary<string, object> args) =>
			{
				bool keyJ = PlayerInput.KeyDown(Keys.J);
				bool keyAlt = PlayerInput.KeyDown(Keys.LeftAlt);
				//LuaCsLogger.LogMessage($"isModActive: {isModActive}");

				if (keyJ && keyAlt)
				{
					if (!flagActiveCombo)
					{
						isModActive = !isModActive;
						flagActiveCombo = true;
						LuaCsLogger.LogMessage("Mod active: " + isModActive.ToString());
					}
				}
				else
				{
					flagActiveCombo = false;
				}

				if (isModActive && GameMain.WindowActive)
				{
					bool isSelected = Character.Controlled.SelectedItem != null ? true : false;
					bool inMenu = false;
					bool inCM = CrewManager.IsCommandInterfaceOpen;


					bool AButton = (controller.GetDigitalState("atack").Pressed); // A button
					if (AButton)
					{
						InputEmulator.Mouse.LeftDown();
						flagA = true;
					}
					else
					{
						if (flagA)
						{
							InputEmulator.Mouse.LeftUp();
							flagA = false;
						}
					}

					if (!isSelected &&
							!GUI.PauseMenuOpen && !GUI.SettingsMenuOpen &&
							!GameSession.IsTabMenuOpen && !GUI.InputBlockingMenuOpen &&
							!(CharacterHealth.OpenHealthWindow != null) && !ConversationAction.IsDialogOpen)
						inMenu = false;
					else
						inMenu = true;


					float CamY = controller.GetAnalogState("camera").Y;
					float CamX = controller.GetAnalogState("camera").X;

					float MoveX = controller.GetAnalogState("move").X;
					float MoveY = controller.GetAnalogState("move").Y;

					/*
										LuaCsLogger.LogMessage("CAM: " + CamX.ToString() + " : " + CamY.ToString());
										LuaCsLogger.LogMessage("MOVE: " + MoveX.ToString() + " : " + MoveY.ToString());*/


					CursorX += CamX * CursorSpeed;
					CursorY += CamY * -1 * CursorSpeed;

					if ((!inMenu && lockCursor) && !inCM)
					{
						CursorX = Math.Clamp(CursorX, ScreenCenterX - CursorRadius / 2, ScreenCenterX + CursorRadius / 2);
						CursorY = Math.Clamp(CursorY, ScreenCenterY - CursorRadius / 2, ScreenCenterY + CursorRadius / 2);
					}
					SetCursorPosition((int)CursorX, (int)CursorY);

				}

				return true;
			}, LuaCsHook.HookMethodType.After, this);


		GameMain.LuaCs.Hook.HookMethod("gamepad_hook",
			typeof(Character).GetMethod("Control"),
			(object self, Dictionary<string, object> args) =>
			{

				// Получение данных о нажатиях кнопок для всех подключенных контроллеров
				for (int i = 0; i < SteamInput.Controllers.ToList().Count; i++)
				{
					//var controller = SteamInput.Controllers.ToList()[i];
					float x = controller.GetAnalogState("move").X;
					float y = controller.GetAnalogState("move").Y;

					bool run = controller.GetDigitalState("run").Pressed;
					bool jump = controller.GetDigitalState("atack").Pressed;
					targetMovement.X = x;
					targetMovement.Y = y;

					bool run_ = false;
					if ((run && Character.Controlled.AnimController.ForceSelectAnimationType == AnimationType.NotDefined) || Character.Controlled.ForceRun)
					{
						run_ = Character.Controlled.CanRun;
					}

					Vector2 targetMovement1 = Character.Controlled.ApplyMovementLimits(targetMovement, Character.Controlled.AnimController.GetCurrentSpeed(run));
					Character.Controlled.AnimController.TargetMovement = targetMovement1;
					Character.Controlled.AnimController.IgnorePlatforms = Character.Controlled.AnimController.TargetMovement.Y < -0.1f;

				}
				return true;
			}, LuaCsHook.HookMethodType.After, this);
	}
	public static void SetCursorPosition(int x, int y)
	{
		SetCursorPos(x, y);
	}
	public static MousePoint GetCursorPosition()
	{
		MousePoint currentMousePoint;
		var gotPoint = GetCursorPos(out currentMousePoint);
		if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
		return currentMousePoint;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MousePoint
	{
		public int X;
		public int Y;

		public MousePoint(int x, int y)
		{
			X = x;
			Y = y;
		}
	}
}


public class SteamManager
{
	public static void Init()
	{
		// Инициализация Steam Input была успешной
		GetAllControlles();
	}

	public static void Shutdown()
	{
		SteamClient.Shutdown();
	}

	public static void Init_SteamInput() // Прозвонка SteamInput.Internal.Init() для активации SteamInput, по советам с Github.
	{
		try
		{
			// Получение типа SteamInput
			Type steamInputType = typeof(SteamInput);

			// Получение внутреннего свойства Internal
			PropertyInfo internalProperty = steamInputType.GetProperty("Internal", BindingFlags.NonPublic | BindingFlags.Static);
			if (internalProperty == null)
			{
				LuaCsLogger.LogError("Failed to find Internal property.");
				return;
			}

			// Получение значения свойства Internal
			var internalInstance = internalProperty.GetValue(null);
			if (internalInstance == null)
			{
				LuaCsLogger.LogError("[GInput] Internal property returned null.");
				return;
			}

			// Получение метода Init у объекта Internal
			MethodInfo initMethod = internalInstance.GetType().GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Instance);
			if (initMethod == null)
			{
				LuaCsLogger.LogError("[GInput] Failed to find Init method.");
				return;
			}

			// Вызов метода Init с параметром false
			bool result = (bool)initMethod.Invoke(internalInstance, new object[] { false });
			if (result)
			{
				LuaCsLogger.LogMessage("[GInput] Steam Input initialized successfully.");
			}
			else
			{
				LuaCsLogger.LogError("[GInput] Failed to initialize Steam Input.");
			}
		}
		catch (Exception ex)
		{
			LuaCsLogger.LogError($"Exception during SteamInput initialization: {ex.Message}");
		}

	}

	public static void GetAllControlles()
	{
		foreach (var controller in SteamInput.Controllers.ToList())
		{
			LuaCsLogger.LogMessage($"[GInput] {controller.ToString()}");
		}
	}

	public static void Update()
	{
		SteamInput.RunFrame();

		// Получение данных о нажатиях кнопок для всех подключенных контроллеров
		for (int i = 0; i < SteamInput.Controllers.ToList().Count; i++)
		{
			var controller = SteamInput.Controllers.ToList()[i];
			bool use = controller.GetDigitalState("use").Pressed;
			bool jump = controller.GetDigitalState("jump").Pressed;
			if (use || jump)
			{
				LuaCsLogger.LogMessage("[GInput] pressed!");
				// Здесь можно добавить логику обработки нажатия кнопки
			}
		}
	}

}