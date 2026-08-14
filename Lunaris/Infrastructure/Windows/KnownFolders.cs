using System.Runtime.InteropServices;

namespace Lunaris.Infrastructure.Windows;

/// <summary>Resolves known user folder paths (Desktop, Documents, ...) via the Windows shell.</summary>
public static class KnownFolders
{
    public static readonly Guid Desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    public static readonly Guid Documents = new("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    public static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");
    public static readonly Guid Pictures = new("33E28130-4E1E-4676-835A-98395C3BC3BB");
    public static readonly Guid Videos = new("18989B1D-99B5-455B-841C-AB7C74E4DDFC");
    public static readonly Guid Music = new("4BD8D571-6D19-48D3-BE97-422220080E43");
    public static readonly Guid StartMenuPrograms = new("A77F5D77-2E2B-44C3-A6A2-ABA601054A51");
    public static readonly Guid RoamingStartMenuPrograms = new("DE92C1C7-837F-4F69-A3BB-86E631204A23");

    public static string? GetPath(Guid knownFolder)
    {
        try
        {
            var hr = SHGetKnownFolderPath(knownFolder, 0, IntPtr.Zero, out var path);
            if (hr != 0)
                return null;
            return Marshal.PtrToStringUni(path);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}