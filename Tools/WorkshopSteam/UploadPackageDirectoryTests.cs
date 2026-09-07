using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.WorkshopSteam
{
    public static partial class UploadPackageTests
    {
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
            ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle DirectoryWriteHandle(string path, uint access, uint sharing,
            IntPtr security, uint creation, uint flags, IntPtr template);

        private static void EmptyDirectoryWriter()
        {
            Fixture fixture = EmptyDirectoryFixture();
            string path = Path.Combine(fixture.Content, "Empty");
            int before = ProbeDirectoryWrite(path, "before");
            Check(before == 0);
            int held;
            UploadPackage package = fixture.Open();
            try
            {
                Check(package.Revalidate());
                held = ProbeDirectoryWrite(path, "held");
                Check(package.Revalidate());
            }
            finally { package.Dispose(); }
            int after = ProbeDirectoryWrite(path, "after-dispose");
            Check(!package.Revalidate());
            if (held != 32 || after != 0)
                throw new InvalidOperationException("Directory write lease expected before=0 held=32 after=0; observed "
                    + before + "/" + held + "/" + after);
        }

        private static int ProbeDirectoryWrite(string path, string phase)
        {
            using (SafeFileHandle handle = DirectoryWriteHandle(path, 0x40000000u, 7u,
                IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                bool opened = !handle.IsInvalid;
                Console.WriteLine("directory-write phase=" + phase + "; opened=" + opened + "; win32=" + error);
                return opened ? 0 : error == 0 ? -1 : error;
            }
        }

        private static void EmptyDirectoryReplacement()
        {
            Fixture fixture = EmptyDirectoryFixture();
            string path = Path.Combine(fixture.Content, "Empty"), moved = Path.Combine(fixture.Root, "retained-empty");
            using (UploadPackage package = fixture.Open())
            {
                Check(package.Revalidate());
                try { Directory.Move(path, moved); }
                catch (IOException error)
                {
                    if ((error.HResult & 0xffff) != 32) throw;
                    Console.WriteLine("empty-directory rename blocked with sharing32; replacement not exercised");
                    Check(Directory.Exists(path) && !Directory.Exists(moved) && package.Revalidate());
                    return;
                }
                Directory.CreateDirectory(path);
                Check(Directory.Exists(moved) && Directory.GetFileSystemEntries(moved).Length == 0
                    && Directory.GetFileSystemEntries(path).Length == 0);
                bool exact = package.Revalidate();
                Console.WriteLine("empty-directory same-name replacement completed; revalidated=" + exact);
                if (exact) throw new InvalidOperationException("Package accepted a replaced empty directory identity; no file-byte or Steam claim.");
            }
        }

        private static Fixture EmptyDirectoryFixture()
        {
            // Retain both positive and failed native fixtures for independent readback.
            Fixture fixture = new Fixture();
            Directory.CreateDirectory(Path.Combine(fixture.Content, "Empty"));
            Console.WriteLine("retained directory-lease fixture=" + fixture.Root);
            return fixture;
        }
    }
}
