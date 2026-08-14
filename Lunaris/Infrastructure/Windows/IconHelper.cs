namespace Lunaris.Infrastructure.Windows;

public static class IconHelper
{
    /// <summary>Loads an HICON from an .ico file for the tray icon.</summary>
    public static IntPtr LoadIconFromFile(string path, int size = 32)
    {
        try
        {
            return NativeMethods.LoadImage(IntPtr.Zero, path, NativeMethods.IMAGE_ICON, size, size,
                NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}