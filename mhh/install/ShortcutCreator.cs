
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace mhhinstall
{
    // Interface declaration for IShellLinkW (Unicode version)
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    // Interface declaration for IPersistFile
    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    // CLSID for ShellLinkObject
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    public class ShellLinkObject { }

    public static class ShortcutCreator
    {
        public static void CreateShortcut(
            string linkPath,
            string targetPath,
            string arguments = null,
            string workingDirectory = null,
            string description = null,
            ushort hotkey = 0)
        {
            // Create instance of ShellLinkObject
            IShellLinkW link = (IShellLinkW)new ShellLinkObject();

            // Set target path
            link.SetPath(targetPath);

            // Set arguments if provided
            if (!string.IsNullOrEmpty(arguments))
            {
                link.SetArguments(arguments);
            }

            // Set working directory if provided
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                link.SetWorkingDirectory(workingDirectory);
            }

            // Set description if provided
            if (!string.IsNullOrEmpty(description))
            {
                link.SetDescription(description);
            }

            // Set hotkey if provided (e.g., Ctrl+Alt+F = 0x0466)
            if (hotkey != 0)
            {
                link.SetHotkey(hotkey);
            }

            // Query for IPersistFile interface
            IPersistFile persistFile = (IPersistFile)link;

            // Save the shortcut (.lnk file)
            persistFile.Save(linkPath, true);
        }
    }
}
