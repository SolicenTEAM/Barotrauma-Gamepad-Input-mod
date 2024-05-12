using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
//using GBF = SharpDX.XInput.GamepadButtonFlags;

namespace GamePadInput
{
	partial class GamePadHook : ACsMod
	{

		// Import the user32.dll
		[DllImport("user32.dll")]
		static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

		[DllImport("user32.dll", EntryPoint = "SetCursorPos")]
		[
			return :MarshalAs(UnmanagedType.Bool)
		]
		private static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		[
			return :MarshalAs(UnmanagedType.Bool)
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
		public GamePadHook()
		{
			bool lockCursor = true;
			bool isModActive = false;
			bool flagActiveCombo = false;

			bool lockCroth = false;

			bool flagDLeft = false;
			bool flagDUp = false;
			bool flagDRight = false;
			bool flagDDown = false;

			bool flagStart = false;
			bool flagBack = false;

			bool flagB = false;
			bool flagX = false;
			bool flagY = false;
			bool flagA = false;

			bool flagRB = false;
			bool flagLB = false;

			bool flagLS = false;
			bool flagRS = false;

			bool mLb = false;
			bool mRb = false;

			float CursorX = 0;
			float CursorY = 0;
			int CursorRadius = 175;
			int CursorSpeed = 20;

			int ScreenWidth = GameMain.GraphicsWidth;
			int ScreenHeight = GameMain.GraphicsHeight;
			int ScreenCenterX = ScreenWidth / 2;
			int ScreenCenterY = ScreenHeight / 2;

			int slot = 0;

			GamePadState gamePad = GamePad.GetState(0);
			KeyboardState keyboard = Keyboard.GetState();

			Character lastCharacter = null;
			float lastHp = 100;

			LuaCsLogger.LogMessage("Initialization GamepadInput Mod");
			GameMain.LuaCs.Hook.HookMethod("gamepad_hook",
				typeof(PlayerInput).GetMethod("Update"),
				(object self, Dictionary<string, object> args) =>
				{
					if (Character.Controlled != null)
					{
						//LuaCsLogger.LogMessage ($"{Character.Controlled}");
						//LuaCsLogger.LogMessage ($"{GamePad.GetCapabilities(0)}");

						bool keyJ = PlayerInput.KeyDown(Keys.J);
						bool keyAlt = PlayerInput.KeyDown(Keys.LeftAlt);
						//LuaCsLogger.LogMessage($"isModActive: {isModActive}");

						if (keyJ && keyAlt)
						{
							Console.Beep();
							if (!flagActiveCombo)
							{
								isModActive = !isModActive;
								flagActiveCombo = true;
							}
						}
						else
						{
							flagActiveCombo = false;
						}

						bool isSelected = Character.Controlled.SelectedItem != null ? true : false;
						bool inMenu = false;
						bool inCM = CrewManager.IsCommandInterfaceOpen;
						if (!isSelected
							&& !GUI.PauseMenuOpen && !GUI.SettingsMenuOpen
							&& !GameSession.IsTabMenuOpen && !GUI.InputBlockingMenuOpen
							&& !(CharacterHealth.OpenHealthWindow != null) && !ConversationAction.IsDialogOpen)
							inMenu = false;
						else
							inMenu = true;

						gamePad = GamePad.GetState(0); // get state of gamepad

						if (gamePad.IsConnected && GameMain.WindowActive)
						{

							#region Gamepad Input
							bool StartButton = (gamePad.Buttons.Start == ButtonState.Pressed); // Start button
							bool BackButton = (gamePad.Buttons.Back == ButtonState.Pressed); // Back button 
							bool LStButton = (gamePad.Buttons.LeftStick == ButtonState.Pressed); // Left stick button
							bool RStButton = (gamePad.Buttons.RightStick == ButtonState.Pressed); // Right stick button	
							bool AButton = (gamePad.Buttons.A == ButtonState.Pressed); // A button
							bool BButton = (gamePad.Buttons.B == ButtonState.Pressed); // B button
							bool XButton = (gamePad.Buttons.X == ButtonState.Pressed); // X button
							bool YButton = (gamePad.Buttons.Y == ButtonState.Pressed); // Y button

							float LTrigger = gamePad.Triggers.Left;
							float RTrigger = gamePad.Triggers.Right;
							bool RBButton = (gamePad.Buttons.RightShoulder == ButtonState.Pressed); // RB button
							bool LBButton = (gamePad.Buttons.LeftShoulder == ButtonState.Pressed); // LB button

							// DPad
							bool DPadLeftButton = (gamePad.DPad.Left == ButtonState.Pressed);
							bool DPadUpButton = (gamePad.DPad.Up == ButtonState.Pressed);
							bool DPadRightButton = (gamePad.DPad.Right == ButtonState.Pressed);
							bool DPadDownButton = (gamePad.DPad.Down == ButtonState.Pressed);
							#endregion

							// Activation using a gamepad
							if (LBButton && RBButton && DPadDownButton && AButton)
							{
								isModActive = !isModActive;
								
								flagActiveCombo = isModActive;
								Console.Beep();
								
								LuaCsLogger.LogMessage("GamepadMod State: - " + isModActive);
								InputEmulator.KeyUp(Keys.Tab);
								InputEmulator.KeyUp(Keys.CapsLock);
								InputEmulator.KeyUp(Keys.LeftControl);
								InputEmulator.KeyUp(Keys.LeftShift);
								return true;
							}

							if (!isModActive) return true;
							if (lastHp != Character.Controlled.Health)
							{
								//LuaCsLogger.LogMessage ($"HIT! {lastHp}=>{Character.Controlled.Health}");
								Vibrate();
								lastHp = Character.Controlled.Health;
							}
							//
							lastCharacter = Character.Controlled;
							lastHp = lastCharacter.Health;

							float rightStickX = 0;
							float rightStickY = 0;

							if (!inMenu)
							{
								rightStickX = gamePad.ThumbSticks.Right.X;
								rightStickY = gamePad.ThumbSticks.Right.Y;
								Move(gamePad, true);
							}
							else
							{
								rightStickX = gamePad.ThumbSticks.Left.X;
								rightStickY = gamePad.ThumbSticks.Left.Y;
								Move(gamePad, false);
							}

							CursorX += rightStickX * CursorSpeed;
							CursorY += rightStickY * -1 * CursorSpeed;

							if ((!inMenu && lockCursor) && !inCM)
							{
								CursorX = Math.Clamp(CursorX, ScreenCenterX - CursorRadius / 2, ScreenCenterX + CursorRadius / 2);
								CursorY = Math.Clamp(CursorY, ScreenCenterY - CursorRadius / 2, ScreenCenterY + CursorRadius / 2);
							}

							SetCursorPosition((int) CursorX, (int) CursorY);

							if (StartButton)
							{
								if (!flagStart)
								{
									InputEmulator.KeyPress(Keys.Escape);
									flagStart = true;
								}
							}
							else
							{
								flagStart = false;
							}

							if (BackButton)
							{
								if (!flagBack)
								{
									//InputEmulator.KeyPress(Keys.Tab);
									InputEmulator.KeyPress(GKey.InfoTab);
									flagBack = true;
								}
							}
							else
							{
								flagBack = false;
							}

							if (LStButton)
							{
								InputEmulator.KeyDown(GKey.Run);
								flagLS = true;
							}
							else
							{
								if (flagLS)
								{
									InputEmulator.KeyUp(GKey.Run);
									flagLS = false;
								}

							}

							if (RStButton)
							{
								if (!flagRS)
								{
									InputEmulator.Mouse.PressMouseButton(2);
									flagRS = true;
								}
							}
							else
							{
								flagRS = false;
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

							if (BButton)
							{
								if (!flagB)
								{
									if (!inMenu && !inCM)
										InputEmulator.KeyPress(GKey.Use);
									else
										InputEmulator.KeyPress(Keys.Escape);

									flagB = true;
								}
							}
							else
							{
								flagB = false;
							}

							if (XButton)
							{
								if (!flagX)
								{
									InputEmulator.KeyPress(GKey.Health);
									flagX = true;
								}
							}
							else
							{
								flagX = false;
							}
							if (YButton)
							{
								if (!flagY)
								{
									InputEmulator.KeyPress(GKey.Grab);
									flagY = true;
								}
							}
							else
							{
								flagY = false;
							}

							if (RTrigger == 1)
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
							if (LTrigger == 1)
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

							if (RBButton)
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

							if (LBButton)
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

							if (DPadLeftButton)
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

							if (DPadUpButton)
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
							if (DPadRightButton)
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
							if (DPadDownButton)
							{
								if (flagDDown)
								{
									lockCroth = !lockCroth;
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

						}
					}
					return true;
				}, LuaCsHook.HookMethodType.After, this);
		}

		bool w = true;
		bool a = true;
		bool s = true;
		bool d = true;
		public void Move(GamePadState gamePad, bool type)
		{
			int way = getStickWay(gamePad, type);

			//LuaCsLogger.LogMessage ($"way : {way}");
			switch (way)
			{
				case 0:
					InputEmulator.KeyDown(GKey.Left);
					//InputEmulator.KeyUp (GKey.Down);
					//InputEmulator.KeyUp (GKey.Up);
					InputEmulator.KeyUp(GKey.Right);
					break;
				case 1:
					InputEmulator.KeyDown(GKey.Up);
					//InputEmulator.KeyUp (GKey.Down);
					//InputEmulator.KeyUp (GKey.Left);
					//InputEmulator.KeyUp (GKey.Right);
					break;
				case 2:
					InputEmulator.KeyDown(GKey.Right);
					//InputEmulator.KeyUp (GKey.Down);
					InputEmulator.KeyUp(GKey.Left);
					//InputEmulator.KeyUp (GKey.Up);
					break;
				case 3:
					InputEmulator.KeyDown(GKey.Down);
					//InputEmulator.KeyUp (GKey.Left);
					//InputEmulator.KeyUp (GKey.Up);
					//InputEmulator.KeyUp (GKey.Right);
					break;
				default:
					moveRelease();
					break;
			}

		}
		void moveRelease()
		{
			InputEmulator.KeyUp(GKey.Up);
			InputEmulator.KeyUp(GKey.Left);
			InputEmulator.KeyUp(GKey.Down);
			InputEmulator.KeyUp(GKey.Right);
		}
		public int getStickWay(GamePadState gamePad, bool type)
		{
			float leftStickX = 0;
			float leftStickY = 0;
			if (type)
			{
				leftStickX = gamePad.ThumbSticks.Left.X;
				leftStickY = gamePad.ThumbSticks.Left.Y;
			}
			else
			{

				leftStickX = gamePad.ThumbSticks.Right.X;
				leftStickY = gamePad.ThumbSticks.Right.Y;
			}

			int result = -1;
			if (leftStickX < 0)
				result = 0;
			if (leftStickX > 0)
				result = 2;
			if (leftStickY > 0)
				result = 1;
			if (leftStickY < 0)
				result = 3;
			return result;
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
		public async void Vibrate()
		{
			GamePad.SetVibration(0, 1f, 1f); // make the controller rumble
			await Task.Delay(500);
			GamePad.SetVibration(0, 0, 0); // make the controller rumble
		}
	}

}
