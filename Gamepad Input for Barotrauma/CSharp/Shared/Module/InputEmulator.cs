using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Barotrauma;
using EventInput;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GamePadInput {
	public class InputEmulator {
		internal static class NativeMethods {
			internal static ushort HIWORD (IntPtr dwValue) {
				return (ushort) ((((long) dwValue) >> 0x10) & 0xffff);
			}

			internal static ushort HIWORD (uint dwValue) {
				return (ushort) (dwValue >> 0x10);
			}

			internal static int GET_WHEEL_DELTA_WPARAM (IntPtr wParam) {
				return (short) HIWORD (wParam);
			}

			internal static int GET_WHEEL_DELTA_WPARAM (uint wParam) {
				return (short) HIWORD (wParam);
			}
		}

		[DllImport ("user32.dll")]
		static extern bool SetCursorPos (int X, int Y);

		[DllImport ("user32.dll")]
		public static extern void mouse_event (MouseEventFlags dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

		[Flags]
		public enum MouseEventFlags {
			Move = 0x0001, LeftDown = 0x0002, LeftUp = 0x0004, RightDown = 0x0008,
			RightUp = 0x0010, Absolute = 0x8000, MiddleDown = 0x0020,
			MiddleUp = 0x0040,
		}

		[DllImport ("user32.dll")]
		static extern bool PostMessage (IntPtr hWnd, UInt32 Msg, int wParam, int lParam);

		[DllImport ("user32.dll", EntryPoint = "MapVirtualKeyA")]
		private extern static int MapVirtualKey (int wCode, int wMapType);

		[DllImport ("user32.dll", SetLastError = true)]
		private extern static void SendInput (int nInputs, Input[] pInputs, int cbSize);

		[DllImport ("user32.dll")]
		private static extern IntPtr GetMessageExtraInfo ();

		[StructLayout (LayoutKind.Sequential)]
		public struct KeyboardInput {
			public short wVk;
			public short wScan;
			public int dwFlags;
			public int time;
			public IntPtr dwExtraInfo;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct MouseInput {
			public int dx;
			public int dy;
			public uint mouseData;
			public uint dwFlags;
			public uint time;
			public IntPtr dwExtraInfo;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct HardwareInput {
			public uint uMsg;
			public ushort wParamL;
			public ushort wParamH;
		}

		[StructLayout (LayoutKind.Explicit)]
		public struct InputUnion {
			[FieldOffset (0)] public MouseInput mi;
			[FieldOffset (0)] public KeyboardInput ki;
			[FieldOffset (0)] public HardwareInput hi;
		}

		public struct Input {
			public int type;
			public InputUnion u;
		}

		[Flags]
		public enum InputType {
			Mouse = 0,
			Keyboard = 1,
			Hardware = 2
		}

		[Flags]
		public enum KeyEventF {
			KeyDown = 0x0000,
			ExtendedKey = 0x0001,
			KeyUp = 0x0002,
			Unicode = 0x0004,
			Scancode = 0x0008
		}

		[Flags]
		public enum MouseEventF {
			Absolute = 0x8000,
			HWheel = 0x01000,
			Move = 0x0001,
			MoveNoCoalesce = 0x2000,
			LeftDown = 0x02,
			LeftUp = 0x04,
			RightDown = 0x08,
			RightUp = 0x10,
			MiddleDown = 0x20,
			MiddleUp = 0x40,
			VirtualDesk = 0x4000,
			Wheel = 0x0800,
			XDown = 0x0080,
			XUp = 0x0100
		}

		const int WM_KEYDOWN = 0x100;
		const int WM_SYSCOMMAND = 0x018;
		const int SC_CLOSE = 0x053;

		private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
		private const int KEYEVENTF_KEYUP = 0x0002;
		private const int KEYEVENTF_SCANCODE = 0x0008;
		private const int KEYEVENTF_UNICODE = 0x0004;
		private const int MAPVK_VK_TO_VSC = 0;

		public class Mouse {

			public static void MiddleDown () {
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.MiddleDown, 0, 0, 0, UIntPtr.Zero);
			}
			public static async void MiddleUp () {
				await Task.Delay (100);
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.MiddleUp, 0, 0, 0, UIntPtr.Zero);
			}
			public static void LeftDown () {
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.LeftDown, 0, 0, 0, UIntPtr.Zero);
			}
			public static async void LeftUp () {
				await Task.Delay (100);
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.LeftUp, 0, 0, 0, UIntPtr.Zero);
			}
			public static void RightDown () {
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.RightDown, 0, 0, 0, UIntPtr.Zero);
			}
			public static async void RightUp () {
				await Task.Delay (100);
				InputEmulator.mouse_event (InputEmulator.MouseEventFlags.Absolute | InputEmulator.MouseEventFlags.RightUp, 0, 0, 0, UIntPtr.Zero);
			}

			public async static void PressMouseButton (int index) {
				switch (index) {
					case 0:
						LeftDown ();
						LeftUp ();
						break;
					case 1:
						RightDown ();
						RightUp ();
						break;
					case 2:
						MiddleDown ();
						MiddleUp ();
						break;
					default:
						break;
				}

			}
		}

		public async static void KeyPress (Keys key) {
			int vsc = MapVirtualKey ((int) key, MAPVK_VK_TO_VSC);
			Input input = new Input ();
			input.type = (int) InputType.Keyboard;
			input.u.ki = new KeyboardInput {
				wVk = (short) key,
					wScan = (short) vsc,
					dwFlags = (int) KeyEventF.KeyUp,
					time = 0,
					dwExtraInfo = IntPtr.Zero
			};
			SendInput (1, new Input[] { input }, Marshal.SizeOf (input));
			await Task.Delay (1);

			input.u.ki.dwFlags = (int) KeyEventF.KeyDown;
			SendInput (1, new Input[] { input }, Marshal.SizeOf (input));

			await Task.Delay (25);
			input.u.ki.dwFlags = (int) KeyEventF.KeyUp;
			SendInput (1, new Input[] { input }, Marshal.SizeOf (input));

		}

		public static void KeyDown (Keys key) {
			int vsc = MapVirtualKey ((int) key, MAPVK_VK_TO_VSC);
			Input[] inputs = new Input[] {
				new Input {
					type = (int) InputType.Keyboard,
						u = new InputUnion {
							ki = new KeyboardInput {
								wVk = (short) key,
									wScan = (short) vsc,
									dwFlags = (int) KeyEventF.KeyDown,
									time = 0,
									dwExtraInfo = IntPtr.Zero
							}
						}
				}
			};
			SendInput (inputs.Length, inputs, Marshal.SizeOf (typeof (Input)));
		}

		public static void KeyUp (Keys key) {
			int vsc = MapVirtualKey ((int) key, MAPVK_VK_TO_VSC);
			Input[] inputs = new Input[] {
				new Input {
					type = (int) InputType.Keyboard,
						u = new InputUnion {
							ki = new KeyboardInput {
								wVk = (short) key,
									wScan = (short) vsc,
									dwFlags = (int) KeyEventF.KeyUp,
									time = 0,
									dwExtraInfo = IntPtr.Zero
							}
						}
				}
			};
			SendInput (inputs.Length, inputs, Marshal.SizeOf (typeof (Input)));
		}

		private static void Send (Keys key) {
			int vsc = MapVirtualKey ((int) key, MAPVK_VK_TO_VSC);

			Input[] inputs = new Input[] {
				new Input {
					type = (int) InputType.Keyboard,
						u = new InputUnion {
							ki = new KeyboardInput {
								wVk = (short) key,
									wScan = (short) vsc,
									dwFlags = (int) KeyEventF.KeyDown,
									time = 0,
									dwExtraInfo = IntPtr.Zero
							}
						}
				},
				new Input {
					type = (int) InputType.Keyboard,
						u = new InputUnion {
							ki = new KeyboardInput {
								wVk = (short) key,
									wScan = (short) vsc,
									dwFlags = (int) KeyEventF.KeyUp,
									time = 0,
									dwExtraInfo = IntPtr.Zero
							}
						}
				}
			};
			SendInput (inputs.Length, inputs, Marshal.SizeOf (typeof (Input)));
		}

	}
}