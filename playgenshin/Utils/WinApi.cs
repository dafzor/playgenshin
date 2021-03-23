﻿using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace bnetlauncher.Utils
{
    public static class WinApi
    {
        internal static class NativeMethods
        {
            // Windows Event KeyDown
            public const int WM_KEYDOWN = 0x100;

            // Constant for Enter Key
            public const int VK_RETURN = 0x0D;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;        // x position of upper-left corner
                public int Top;         // y position of upper-left corner
                public int Right;       // x position of lower-right corner
                public int Bottom;      // y position of lower-right corner
            }

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


            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowTextLength(IntPtr hWnd);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

            [DllImport("User32.dll")]
            public static extern int SetForegroundWindow(IntPtr point);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

            [DllImport("user32.dll")]
            public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

            [DllImport("user32.dll")]
            public static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        }

        #region Unusued functions
        //static public bool IsForegroundWindowByTitle(string title)
        //{
        //    IntPtr hwnd = NativeMethods.GetForegroundWindow();

        //    int length = NativeMethods.GetWindowTextLength(hwnd);
        //    StringBuilder windowtitle = new StringBuilder(length + 1);
        //    int result_len = NativeMethods.GetWindowText(hwnd, windowtitle, windowtitle.Capacity);

        //    if (result_len != length)
        //    {
        //        Logger.Warning($"Got missmatched Windows title length. {length} vs {result_len}");
        //        return false;
        //    }

        //    Logger.Information($"Foreground Window title = '{windowtitle.ToString()}'");
        //    return (windowtitle.ToString().Equals(title, StringComparison.OrdinalIgnoreCase));
        //}

        //static public bool SetForegroundWindowByTitle(string title)
        //{
        //    try
        //    {
        //        var client = Process.GetProcessesByName(title)[0];
        //        return NativeMethods.SetForegroundWindow(client.MainWindowHandle) != 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Exception wile trying to bring '{title}' to the foreground.", ex);
        //    }

        //    return false;
        //}

        //static public bool SetForegroundWindowByHandle(IntPtr handle)
        //{
        //    if (handle != null)
        //    {
        //        return NativeMethods.SetForegroundWindow(handle) != 0;
        //    }
        //    return false;
        //}

        //static public bool SendEnterByTitle(string title)
        //{
        //    var windows = Process.GetProcesses();

        //    bool sent = false;
        //    foreach (var window in windows)
        //    {
        //        if (window.MainWindowTitle == title)
        //        {
        //            NativeMethods.SendMessage(window.MainWindowHandle,
        //                NativeMethods.WM_KEYDOWN, NativeMethods.VK_RETURN, IntPtr.Zero);

        //            Logger.Information($"Sending Enter to '{window.MainWindowTitle}'");
        //            sent = true;
        //        }
        //    }

        //    return sent;            
        //}
        #endregion unused functions

        static public void SendEnterByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                Logger.Error($"Given null handle. aborting...");
                return;
            }

            Logger.Information("Sending enter key to window");
            NativeMethods.SendMessage(handle, NativeMethods.WM_KEYDOWN, ToPtr(NativeMethods.VK_RETURN), IntPtr.Zero);
        }

        public static IntPtr ToPtr(int val)
        {
            IntPtr ptr = Marshal.AllocHGlobal(sizeof(int));

            byte[] byteVal = BitConverter.GetBytes(val);
            Marshal.Copy(byteVal, 0, ptr, byteVal.Length);
            return ptr;
        }

        // https://stackoverflow.com/questions/30965343/printwindow-could-not-print-google-chrome-window-chrome-widgetwin-1
        public static Bitmap CaptureWindow(Object bind, IntPtr handle)
        {
            NativeMethods.RECT wnd;
            NativeMethods.GetWindowRect(new HandleRef(bind, handle), out wnd);

            var bmp = new Bitmap(wnd.Right - wnd.Left, wnd.Bottom - wnd.Top);  // content only

            using (Graphics graphics = Graphics.FromImage(bmp))
            {
                IntPtr hDC = graphics.GetHdc();
                try
                {
                    NativeMethods.PrintWindow(handle, hDC, (uint)0x00000002);
                }
                finally
                {
                    graphics.ReleaseHdc(hDC);
                }
            }
            return bmp;
        }

        // https://stackoverflow.com/questions/10355286/programmatically-mouse-click-in-another-window
        public static void ClickOnWindow(IntPtr wndHandle, Point clientPoint)
        {
            var oldPos = Cursor.Position;

            /// get screen coordinates
            NativeMethods.ClientToScreen(wndHandle, ref clientPoint);

            /// set cursor on coords, and press mouse
            Cursor.Position = new Point(clientPoint.X, clientPoint.Y);

            var inputMouseDown = new NativeMethods.INPUT();
            inputMouseDown.Type = 0; /// input type mouse
            inputMouseDown.Data.Mouse.Flags = 0x0002; /// left button down

            var inputMouseUp = new NativeMethods.INPUT();
            inputMouseUp.Type = 0; /// input type mouse
            inputMouseUp.Data.Mouse.Flags = 0x0004; /// left button up

            var inputs = new NativeMethods.INPUT[] { inputMouseDown, inputMouseUp };
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));

            /// return mouse 
            Cursor.Position = oldPos;
        }
    }
}
