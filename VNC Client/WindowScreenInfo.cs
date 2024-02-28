using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VNC_Client
{
    internal class WindowScreenInfo
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        const int DESKTOPHORZRES = 118;
        const int DESKTOPVERTRES = 117;

        public static int GetScreenWidth
        {
            get
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                int width = GetDeviceCaps(hdc, DESKTOPHORZRES);

                return width;
            }
        }

        public static int GetScreenHeight
        {
            get
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                int height = GetDeviceCaps(hdc, DESKTOPVERTRES);

                return height;
            }
        }
    }
}
