using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VNC_Server
{
    internal class MouseOperator
    {
        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
    }
}
