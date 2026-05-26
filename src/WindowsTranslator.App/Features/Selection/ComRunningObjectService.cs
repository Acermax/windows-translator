using System.Runtime.InteropServices;

namespace WindowsTranslator.App.Features.Selection;

internal static class ComRunningObjectService
{
    public static object? GetActiveObject(string progId)
    {
        if (NativeMethods.CLSIDFromProgID(progId, out var classId) != 0)
        {
            return null;
        }

        NativeMethods.GetActiveObject(ref classId, IntPtr.Zero, out var activeObject);
        return activeObject;
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        internal static extern int CLSIDFromProgID(string progId, out Guid classId);

        [DllImport("oleaut32.dll", PreserveSig = false)]
        internal static extern void GetActiveObject(
            ref Guid classId,
            IntPtr reserved,
            [MarshalAs(UnmanagedType.IUnknown)] out object activeObject);
    }
}
