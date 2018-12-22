using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

using IWshRuntimeLibrary;
using Shell32;
using File = System.IO.File;

namespace OmniBox
{
    public static class ShortcutHelper
    {
        #region Classes

        [SuppressUnmanagedCodeSecurity]
        private static class SafeNativeMethods
        {
            #region Methods

            [DllImport("shell32.dll", EntryPoint = "ExtractAssociatedIcon", CharSet = CharSet.Auto)]
            internal static extern IntPtr ExtractAssociatedIcon(HandleRef hInst, StringBuilder iconPath, ref int index);

            #endregion Methods
        }

        #endregion Classes

        #region Methods

        private static WshShell wsshell = new WshShell();
        private static Shell shell = new Shell();

        public static string GetFilePathFromlnk(string filePath)
        {
            var link = (IWshShortcut)wsshell.CreateShortcut(filePath);
            return link.TargetPath;
        }

        public static bool IsAccessibleLink(string shortcutFilename, out string targetPath)
        {
            bool result = false;
            targetPath = null;
            try
            {
                var pathOnly = Path.GetDirectoryName(shortcutFilename);
                var filenameOnly = Path.GetFileName(shortcutFilename);

                var folder = shell.NameSpace(pathOnly);
                var folderItem = folder.ParseName(filenameOnly);

                if (folderItem != null)
                {
                    var link = (ShellLinkObject)folderItem.GetLink;
                    targetPath = link.Target.Path;

                    // it is prefixed with {54A35DE2-guid-for-program-files-x86-QZ32BP4}
                    if (targetPath.StartsWith("{"))
                    {
                        var endguid = targetPath.IndexOf("}");
                        if (endguid > 0)
                            targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), targetPath.Substring(endguid + 1));
                    }

                    result = folderItem.IsLink;
                }

            }
            catch (UnauthorizedAccessException) { }
            catch (NotImplementedException) { }

            if (!File.Exists(targetPath))
                return false;

            return result;
        }

        #endregion Methods
    }
}
