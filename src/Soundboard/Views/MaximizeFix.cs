using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Soundboard.Views;

/// <summary>
/// Con una barra de título propia (WindowStyle=None + WindowChrome), Windows maximiza la ventana
/// más grande que la pantalla: la infla por el ancho del borde de redimensionado, y lo que quede
/// abajo — en esta app, la barra de estado — se sale del área visible.
///
/// La solución buena no es un margen a ojo, sino contestar a WM_GETMINMAXINFO con el área de
/// trabajo del monitor donde esté la ventana. Así respeta también la barra de tareas, esté en el
/// borde que esté, y funciona igual con varios monitores.
/// </summary>
public static class MaximizeFix
{
    const int WmGetMinMaxInfo = 0x0024;
    const int MonitorDefaultToNearest = 0x00000002;

    public static void Apply(Window window)
    {
        if (PresentationSource.FromVisual(window) is HwndSource source)
            source.AddHook(Hook);
        else
            window.SourceInitialized += (_, _) =>
            {
                if (PresentationSource.FromVisual(window) is HwndSource created)
                    created.AddHook(Hook);
            };
    }

    static nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WmGetMinMaxInfo) return nint.Zero;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == nint.Zero) return nint.Zero;

        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return nint.Zero;

        var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var work = info.rcWork;
        var screen = info.rcMonitor;

        // Posición y tamaño en coordenadas relativas al monitor.
        minMax.ptMaxPosition.x = work.left - screen.left;
        minMax.ptMaxPosition.y = work.top - screen.top;
        minMax.ptMaxSize.x = work.right - work.left;
        minMax.ptMaxSize.y = work.bottom - work.top;

        Marshal.StructureToPtr(minMax, lParam, true);
        handled = true;
        return nint.Zero;
    }

    [DllImport("user32.dll")]
    static extern nint MonitorFromWindow(nint hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Point
    {
        public int x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }
}
