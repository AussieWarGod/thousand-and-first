using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.Tools
{
	/// <summary>Exact Windows metadata for one already-open scenario-profile file.</summary>
	public static class ScenarioFileTrust
	{
		[StructLayout(LayoutKind.Sequential)]
		private struct FileStandardInformation
		{
			public long AllocationSize;
			public long EndOfFile;
			public uint NumberOfLinks;
			[MarshalAs(UnmanagedType.U1)] public bool DeletePending;
			[MarshalAs(UnmanagedType.U1)] public bool Directory;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,
			int informationClass, out FileStandardInformation information, uint size);

		public static uint GetLinkCount(SafeFileHandle handle)
		{
			if (handle == null || handle.IsClosed || handle.IsInvalid)
				throw new ArgumentException("File handle is not open.", "handle");
			FileStandardInformation information;
			uint size = (uint)Marshal.SizeOf(typeof(FileStandardInformation));
			if (!GetFileInformationByHandleEx(handle, 1, out information, size))
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return information.NumberOfLinks;
		}
	}
}
