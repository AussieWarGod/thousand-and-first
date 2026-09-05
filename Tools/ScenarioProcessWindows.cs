using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ThousandAndFirst.Tools
{
    public static class ScenarioProcessWindows
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern IntPtr CommandLineToArgvW(
            [MarshalAs(UnmanagedType.LPWStr)] string CommandLine, out int Count);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr Memory);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            IntPtr File, StringBuilder Path, uint Length, uint Flags);

        public static string[] Arguments(string CommandLine)
        {
            if (string.IsNullOrEmpty(CommandLine) || CommandLine.Length > 32768
                || CommandLine.IndexOf('\0') >= 0)
                throw new InvalidOperationException("Process command line is missing or malformed.");
            int count;
            IntPtr block = CommandLineToArgvW(CommandLine, out count);
            if (block == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            try
            {
                if (count < 1 || count > 128)
                    throw new InvalidOperationException("Process argument count is outside the bound.");
                string[] result = new string[count];
                for (int i = 0; i < count; i++)
                    result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(block, i * IntPtr.Size));
                return result;
            }
            finally { LocalFree(block); }
        }

        public static string Executable(string Path)
        {
            using (FileStream file = new FileStream(Path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                StringBuilder path = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(file.SafeFileHandle.DangerousGetHandle(),
                    path, (uint)path.Capacity, 0);
                if (length == 0 || length >= path.Capacity)
                    throw new System.ComponentModel.Win32Exception();
                string result = path.ToString();
                if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                    return @"\\" + result.Substring(8);
                if (result.StartsWith(@"\\?\", StringComparison.Ordinal)) return result.Substring(4);
                throw new InvalidOperationException("Executable did not resolve to a filesystem path.");
            }
        }
    }
}
