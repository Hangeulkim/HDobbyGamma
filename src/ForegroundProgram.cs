using System.Drawing;
using System.Text;

namespace GammaControl;

internal static class ForegroundProgram
{
    internal static bool TryGetClientBounds(string executablePath, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero || !NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window) ||
            NativeMethods.GetWindowThreadProcessId(window, out var processId) == 0 || processId == 0)
        {
            return false;
        }

        var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var path = new StringBuilder(32768);
            uint length = (uint)path.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(process, 0, path, ref length) ||
                !string.Equals(path.ToString(), executablePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }

        if (!NativeMethods.GetClientRect(window, out var client) ||
            client.Right <= client.Left || client.Bottom <= client.Top)
        {
            return false;
        }

        var topLeft = new NativeMethods.PointL { X = client.Left, Y = client.Top };
        var bottomRight = new NativeMethods.PointL { X = client.Right, Y = client.Bottom };
        if (!NativeMethods.ClientToScreen(window, ref topLeft) ||
            !NativeMethods.ClientToScreen(window, ref bottomRight))
        {
            return false;
        }

        bounds = Rectangle.Intersect(
            Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y),
            SystemInformation.VirtualScreen);
        return bounds.Width >= 32 && bounds.Height >= 32;
    }
}
