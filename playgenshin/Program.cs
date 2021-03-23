using bnetlauncher.Utils;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace playgenshin
{
    class Program
    {
        static void LaunchGenshin()
        {
            var proc = Process.Start(GenshinLauncherPath);
            var button_color = Color.FromArgb(255, 255, 205, 11);

            while (proc.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(500);
            }


            var button_location = Point.Empty;

            for (int i = 0; i < 3; i++)
            {
                button_location = FindButtonByColor(proc, button_color);

                if (button_location != Point.Empty)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            WinApi.NativeMethods.SetForegroundWindow(proc.MainWindowHandle);
            ClickOnWindow(proc.MainWindowHandle, button_location);
            Thread.Sleep(1000);
            proc.CloseMainWindow();
        }


        static string GenshinLauncherPath
        {
            get
            {
                var regkey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\launcher", false);
                return Path.Combine((string)regkey.GetValue("InstPath"), "launcher.exe");
            }
        }

        static bool IsElevated
        {
            get
            {
                return WindowsIdentity.GetCurrent().Owner
                  .IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
            }
        }


        static void Main(string[] args)
        {
            var start_date = DateTime.Now;
            if (IsElevated)
            {
                if (Tasker.Exists(Application.ProductName))
                {
                    LaunchGenshin();
                }
                else
                {
                    Tasker.Create(Application.ProductName, Application.ExecutablePath, true);
                }
                return;
            }
            else
            {
                if (!Tasker.Exists(Application.ProductName))
                {
                    // create task
                    var process_info = new ProcessStartInfo();
                    process_info.Verb = "runas";
                    process_info.FileName = Application.ExecutablePath;
                    Process.Start(process_info);
                }
                Tasker.Run(Application.ProductName);
            }

            Process genshin = null;

            var retry = 100;
            while (retry > 0)
            {
                var processes = Process.GetProcessesByName("GenshinImpact");

                if (processes.Length > 0)
                {
                    genshin = processes[0];
                    break;
                }
                retry -= 1;
                Thread.Sleep(200);
            }

            genshin.WaitForExit();
        }

        static Process FindProcessByName(string name)
        {
            var processes = Process.GetProcessesByName("GenshinImpact");

            if (processes.Length > 0)
            {
                return processes[0];
            }
            return null;
        }

        static Point FindButtonByColor(Process proc, Color button_color)
        {
            var bmp = CaptureWindow(proc);
            bmp.Save(Path.Combine(Path.GetTempPath(), Application.ProductName + "_window_capture.bmp"));

            for (int y = bmp.Height - 1; y > (bmp.Height / 3); y--)
            {
                // for (int x = (bmp.Width / 2); x < (bmp.Width); x++)
                for (int x = bmp.Width - 1; x > (bmp.Width / 2); x--)
                {
                    var pixel = bmp.GetPixel(x, y);
                    if (pixel == button_color)
                    {
                        return new Point(x, y);
                    }
                }
            }
            return Point.Empty;
        }


        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        // https://stackoverflow.com/questions/30965343/printwindow-could-not-print-google-chrome-window-chrome-widgetwin-1
        public static Bitmap CaptureWindow(Process proc)
        {
            SetProcessDPIAware();

            RECT wnd;
            GetWindowRect(new HandleRef(proc, proc.MainWindowHandle), out wnd);

            var bmp = new Bitmap(wnd.Right - wnd.Left, wnd.Bottom - wnd.Top);  // content only

            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                IntPtr hDC = graphics.GetHdc();
                try { PrintWindow(proc.MainWindowHandle, hDC, (uint)0x00000002); }
                finally { graphics.ReleaseHdc(hDC); }
            }
            return bmp;
        }


        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

#pragma warning disable 649
        internal struct INPUT
        {
            public UInt32 Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        internal struct MOUSEINPUT
        {
            public Int32 X;
            public Int32 Y;
            public UInt32 MouseData;
            public UInt32 Flags;
            public UInt32 Time;
            public IntPtr ExtraInfo;
        }

#pragma warning restore 649

        // https://stackoverflow.com/questions/10355286/programmatically-mouse-click-in-another-window
        public static void ClickOnWindow(IntPtr wndHandle, Point clientPoint)
        {
            var oldPos = Cursor.Position;

            /// get screen coordinates
            ClientToScreen(wndHandle, ref clientPoint);

            /// set cursor on coords, and press mouse
            Cursor.Position = new Point(clientPoint.X, clientPoint.Y);

            var inputMouseDown = new INPUT();
            inputMouseDown.Type = 0; /// input type mouse
            inputMouseDown.Data.Mouse.Flags = 0x0002; /// left button down

            var inputMouseUp = new INPUT();
            inputMouseUp.Type = 0; /// input type mouse
            inputMouseUp.Data.Mouse.Flags = 0x0004; /// left button up

            var inputs = new INPUT[] { inputMouseDown, inputMouseUp };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            /// return mouse 
            Cursor.Position = oldPos;
        }
    }
}
