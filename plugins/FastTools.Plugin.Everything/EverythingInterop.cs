using System.Runtime.InteropServices;
using System.Text;

namespace FastTools.Plugin.Everything;

internal static class EverythingInterop
{
    private const string DllName = "Everything64.dll";

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    public static extern uint Everything_SetSearchW(string lpSearchString);

    [DllImport(DllName)]
    public static extern void Everything_SetMax(uint dwMax);

    [DllImport(DllName)]
    public static extern void Everything_SetOffset(uint dwOffset);

    [DllImport(DllName)]
    public static extern void Everything_SetRequestFlags(uint dwRequestFlags);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool bWait);

    [DllImport(DllName)]
    public static extern uint Everything_GetNumResults();

    [DllImport(DllName)]
    public static extern uint Everything_GetLastError();

    [DllImport(DllName, CharSet = CharSet.Unicode)]
    public static extern void Everything_GetResultFullPathNameW(uint nIndex, StringBuilder lpString, uint nMaxCount);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Everything_IsResultFolder(uint nIndex);

    [DllImport(DllName)]
    public static extern uint Everything_GetMajorVersion();

    [DllImport(DllName)]
    public static extern void Everything_SetSort(uint dwSortType);

    public const uint EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
    public const uint EVERYTHING_REQUEST_PATH = 0x00000002;
    public const uint EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;

    public const uint EVERYTHING_ERROR_SUCCESS = 0;
    public const uint EVERYTHING_ERROR_IPC = 2;

    public const uint EVERYTHING_SORT_NAME_ASCENDING = 1;

    public static bool IsAvailable()
    {
        try
        {
            Everything_GetMajorVersion();
            return Everything_GetLastError() != EVERYTHING_ERROR_IPC;
        }
        catch
        {
            return false;
        }
    }
}
