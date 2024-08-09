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
using Barotrauma.Items.Components;
using Barotrauma.Abilities;
using GamePadInput;
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


	// Declare some keyboard keys as constants with its respective code
	// See Virtual Code Keys: https://msdn.microsoft.com/en-us/library/dd375731(v=vs.85).aspx
	public const int KEYEVENTF_EXTENDEDKEY = 0x0001; //Key down flag
	public const int KEYEVENTF_KEYUP = 0x0002; //Key up flag
	public const int VK_TAB = 0x09; //Right Control key code

	public static readonly List<Keys> NumberKeys = new List<Keys> { Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 };
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
		bool lockCroth = false;
		bool LBFlag = false;

		bool flagDLeft = false;
		bool flagDUp = false;
		bool flagDRight = false;
		bool flagDDown = false;

		bool flagA = false;
		bool flagB = false;
		bool flagY = false;


		bool flagLB = false;
		bool flagRB = false;

		bool mLb = false;
		bool mRb = false;

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

		int slot = 0;

		SteamInput.RunFrame();
		Character lastCharacter = null;

		LuaCsLogger.LogError("StartHook");
		SteamManager.Init_SteamInput();
		SteamManager.GetAllControlles();

		Vector2 targetMovement = Vector2.Zero;
		if (SteamInput.Controllers.ToList().Count != 0)
			LuaCsLogger.LogMessage("GOTOVO");

		else
			LuaCsLogger.LogMessage("KAPEC");
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


					bool back = controller.GetDigitalState("run").Pressed;
					bool use = controller.GetDigitalState("duck").Pressed;
					bool AButton = (controller.GetDigitalState("atack").Pressed); // A button

					bool up = controller.GetDigitalState("action_1").Pressed;
					bool down = controller.GetDigitalState("action_2").Pressed;
					bool left = controller.GetDigitalState("action_3").Pressed;
					bool right = controller.GetDigitalState("action_4").Pressed;


					bool RTrigger = controller.GetDigitalState("action_6").Pressed;
					bool LTrigger = controller.GetDigitalState("action_5").Pressed;

					bool RBTrigger = controller.GetDigitalState("action_8").Pressed;
					bool LBTrigger = controller.GetDigitalState("action_7").Pressed;

					if (use && !flagY)
					{
						InputEmulator.KeyPress(GKey.Use);
						flagY = true;
					}
					if (!use)
					{
						flagY = false;
					}

					if (back && !flagB)
					{
						menu();
						flagB = true;
					}
					if (!back)
					{
						flagB = false;
					}

					if (LBTrigger && !LBFlag)
					{
						lockCroth = !lockCroth;
						LBFlag = true;
					}
					if (!LBTrigger)
					{
						LBFlag = false;
					}


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


					if (RTrigger)
					{
						InputEmulator.Mouse.LeftDown();
						mLb = true;
					}
					else
					{
						if (mLb)
						{
							InputEmulator.Mouse.LeftUp();
							mLb = false;
						}
					}
					if (LTrigger)
					{
						InputEmulator.Mouse.RightDown();
						mRb = true;
					}
					else
					{
						if (mRb)
						{
							InputEmulator.Mouse.RightUp();
							mRb = false;
						}
					}

					if (left)
					{
						InputEmulator.KeyDown(GKey.Ragdoll);
						flagDLeft = true;
					}
					else
					{
						if (flagDLeft)
						{
							InputEmulator.KeyUp(GKey.Ragdoll);
							flagDLeft = false;
						}
					}

					if (up)
					{
						if (!flagDUp)
						{
							lockCursor = !lockCursor;
							flagDUp = true;
						}
					}
					else
					{
						flagDUp = false;
					}
					if (right)
					{
						if (!flagDRight)
						{
							InputEmulator.KeyPress(GKey.CrewOrders);
							flagDRight = true;
						}
					}
					else
					{
						flagDRight = false;
					}
					if (down)
					{
						if (flagDDown)
						{
							InputEmulator.KeyPress(GKey.Health);
							flagDDown = false;
						}
					}
					else
					{
						flagDDown = true;
					}
					if (lockCroth)
					{
						InputEmulator.KeyDown(GKey.Crouch);
					}
					else
					{
						InputEmulator.KeyUp(GKey.Crouch);
					}

					bool RSButton = (controller.GetDigitalState("next_item").Pressed); // A button
					if (RSButton)
					{
						if (!flagRB)
						{
							slot++;
							if (slot > 9) slot = 0;
							InputEmulator.KeyPress(NumberKeys[slot]);
							flagRB = true;
						}
					}
					else
					{
						flagRB = false;
					}
					bool LSButton = (controller.GetDigitalState("prev_item").Pressed); // A button
					if (LSButton)
					{
						if (!flagLB)
						{
							slot--;
							if (slot < 0) slot = 9;
							InputEmulator.KeyPress(NumberKeys[slot]);
							flagLB = true;
						}
					}
					else
					{
						flagLB = false;
					}





					if (!GUI.InputBlockingMenuOpen)
					{
						if (Character.Controlled.SelectedItem != null && back)
						{
							LuaCsLogger.LogMessage(Character.Controlled.SelectedItem.Name.ToString());
							if (GameMain.Client != null)
							{
								//emulate a Deselect input to get the character to deselect the item server-side
								Character.Controlled.EmulateInput(Barotrauma.InputType.Deselect);
							}
							//reset focus to prevent us from accidentally interacting with another entity
							Character.Controlled.FocusedCharacter = null;
							Character.Controlled.SelectedItem = null;
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
					//if (Character.Controlled.FocusedItem != null)
					//LuaCsLogger.LogMessage(Character.Controlled.FocusedItem.Name.ToString());
				}

				return true;
			}, LuaCsHook.HookMethodType.After, this);


		GameMain.LuaCs.Hook.HookMethod("gamepad_hook",
			typeof(Character).GetMethod("Control"),
			(object self, Dictionary<string, object> args) =>
			{

				if (isModActive && GameMain.WindowActive)
				{
					// Получение данных о нажатиях кнопок для всех подключенных контроллеров
					for (int i = 0; i < SteamInput.Controllers.ToList().Count; i++)
					{
						//var controller = SteamInput.Controllers.ToList()[i];
						float x = controller.GetAnalogState("move").X;
						float y = controller.GetAnalogState("move").Y;

						bool run = controller.GetDigitalState("use").Pressed;
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



						var head = Character.Controlled.AnimController.GetLimb(LimbType.Head);
						bool headInWater = head == null ?
							Character.Controlled.AnimController.InWater :
							head.InWater;
						//climb ladders automatically when pressing up/down inside their trigger area
						Ladder currentLadder = Character.Controlled.SelectedSecondaryItem?.GetComponent<Ladder>();
						if ((Character.Controlled.SelectedSecondaryItem == null || currentLadder != null) &&
							!headInWater && Screen.Selected != GameMain.SubEditorScreen)
						{
							bool climbInput = (y >= 0.8) || (y <= -0.8);
							bool isControlled = Character.Controlled.IsLocalPlayer;

							Ladder nearbyLadder = null;
							if (isControlled || climbInput)
							{
								float minDist = float.PositiveInfinity;
								foreach (Ladder ladder in Ladder.List)
								{
									if (ladder == currentLadder)
									{
										continue;
									}
									else if (currentLadder != null)
									{
										//only switch from ladder to another if the ladders are above the current ladders and pressing up, or vice versa
										if (ladder.Item.WorldPosition.Y > currentLadder.Item.WorldPosition.Y != (y >= 0.5))
										{
											continue;
										}
									}

									if (Character.Controlled.CanInteractWith(ladder.Item, out float dist, checkLinked: false) && dist < minDist)
									{
										minDist = dist;
										nearbyLadder = ladder;
										if (isControlled)
										{
											ladder.Item.IsHighlighted = true;
										}
										break;
									}
								}
							}


							if (nearbyLadder != null && climbInput)
							{
								if (nearbyLadder.Select(Character.Controlled))
								{
									Character.Controlled.SelectedSecondaryItem = nearbyLadder.Item;
								}
							}
						}

						bool isClimbing = true;
						if ((x >= 0.8) || (x <= -0.8) &&
											(!(y >= 0.8) && (y <= -0.8)))
						{
							isClimbing = false;
						}

						if (!isClimbing)
						{
							Character.Controlled.StopClimbing();
						}
					}
				}
				return true;
			}, LuaCsHook.HookMethodType.After, this);
	}
	/*
		void use(string l)
		{
			InputEmulator.KeyPress(GKey.Use);
		}
		void back(string l)
		{
			InputEmulator.KeyPress(GKey.Use);
		}
		void butt(bool button, Action<string> myMethodName)
		{
			myMethodName("123213");
			bool flag = false;
			if (button && !flag)
			{
				InputEmulator.KeyPress(GKey.Use);
				flag = true;
			}
			if (!button)
			{
				flag = false;
			}
		}*/

	public void menu()
	{

		// Check if a text input is selected.
		if (GUI.KeyboardDispatcher.Subscriber != null)
		{
			if (GUI.KeyboardDispatcher.Subscriber is GUITextBox textBox)
			{
				textBox.Deselect();
			}
			GUI.KeyboardDispatcher.Subscriber = null;
		}
		//if a verification prompt (are you sure you want to x) is open, close it
		else if (GUIMessageBox.VisibleBox as GUIMessageBox != null &&
				GUIMessageBox.VisibleBox.UserData as string == "verificationprompt")
		{
			((GUIMessageBox)GUIMessageBox.VisibleBox).Close();
		}
		else if (GUIMessageBox.VisibleBox?.UserData is RoundSummary roundSummary &&
				roundSummary.ContinueButton != null &&
				roundSummary.ContinueButton.Visible)
		{
			GUIMessageBox.MessageBoxes.Remove(GUIMessageBox.VisibleBox);
		}
		else if (ObjectiveManager.ContentRunning)
		{
			ObjectiveManager.CloseActiveContentGUI();
		}
		else if (GameSession.IsTabMenuOpen)
		{
			GameMain.GameSession.ToggleTabMenu();
		}
		else if (GUIMessageBox.VisibleBox as GUIMessageBox != null &&
				 GUIMessageBox.VisibleBox.UserData as string == "bugreporter")
		{
			((GUIMessageBox)GUIMessageBox.VisibleBox).Close();
		}
		else if (GUI.PauseMenuOpen)
		{
			GUI.TogglePauseMenu();
		}
		else if (GameMain.GameSession?.Campaign is { ShowCampaignUI: true, ForceMapUI: false })
		{
			GameMain.GameSession.Campaign.ShowCampaignUI = false;
		}
		//open the pause menu if not controlling a character OR if the character has no UIs active that can be closed with ESC
		else if ((Character.Controlled == null || !itemHudActive())
			&& CharacterHealth.OpenHealthWindow == null
			&& !CrewManager.IsCommandInterfaceOpen
			&& !(Screen.Selected is SubEditorScreen editor && !editor.WiringMode && Character.Controlled?.SelectedItem != null))
		{
			// Otherwise toggle pausing, unless another window/interface is open.
			GUI.TogglePauseMenu();
		}

		static bool itemHudActive()
		{
			if (Character.Controlled?.SelectedItem == null) { return false; }
			return
				Character.Controlled.SelectedItem.ActiveHUDs.Any(ic => ic.GuiFrame != null) ||
				((Character.Controlled.ViewTarget as Item)?.Prefab?.FocusOnSelected ?? false);
		}
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
