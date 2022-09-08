using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FacebookWidget
{
    public static class Helpers
    {
        public static TaskBarLocation GetTaskBarLocation(out Rectangle taskbarBouds)
        {
            var _taskbarLocation = TaskBarLocation.Bottom;
            var _bTaskbarHorizontal = Screen.PrimaryScreen.WorkingArea.Width == Screen.PrimaryScreen.Bounds.Width;

            var _screenBounds = Screen.PrimaryScreen.Bounds;

            var taskbarHeight = Math.Abs(_bTaskbarHorizontal ?
                _screenBounds.Height - Screen.PrimaryScreen.WorkingArea.Height :
                _screenBounds.Width - Screen.PrimaryScreen.WorkingArea.Width);

            var taskbarSize = new Size(
                _bTaskbarHorizontal ? _screenBounds.Width : taskbarHeight,
                _bTaskbarHorizontal ? taskbarHeight : _screenBounds.Height);

            var taskLocation = _screenBounds.Location;

            if (_bTaskbarHorizontal)
            {
                if (Screen.PrimaryScreen.WorkingArea.Top > 0)
                {
                }
                else
                {
                    taskLocation = new Point(_screenBounds.Left, _screenBounds.Bottom - taskbarHeight);
                }
            }
            else
            {
                if (Screen.PrimaryScreen.WorkingArea.Left > 0)
                {
                    _taskbarLocation = TaskBarLocation.Left;
                    taskLocation = _screenBounds.Location;
                }
                else
                {
                    _taskbarLocation = TaskBarLocation.Right;
                    taskLocation = new Point(_screenBounds.Right - taskbarHeight, _screenBounds.Top);
                }
            }

            taskbarBouds = new Rectangle(taskLocation, taskbarSize);
            return _taskbarLocation;
        }

        public static int toCurrentDpi(int pnVal)
        {
            using (var _g = Graphics.FromHwnd(IntPtr.Zero))
                return (int)(pnVal * (_g.DpiX / 96F));
        }
    }
}
