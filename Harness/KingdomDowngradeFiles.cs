using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomDowngradeFiles : IDisposable
	{
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
		private readonly List<DirectoryHold> Directories = new List<DirectoryHold>();
		private readonly List<FileHold> Files = new List<FileHold>();
		private bool Closed;
		private sealed class DirectoryHold
		{
			internal string Path;
			internal SafeFileHandle Handle;
			internal Identity Identity;
		}
		private sealed class FileHold
		{
			internal string Path, Hash;
			internal FileStream Stream;
			internal Identity Identity;
		}

		internal void Anchor(string path)
		{
			Require(!Closed, "file observation already closed");
			SafeFileHandle handle = OpenDirectory(path);
			try
			{
				Identity identity = ReadIdentity(handle);
				Require((identity.Attributes & 0x10) != 0, "directory anchor is not a directory");
				Directories.Add(new DirectoryHold { Path = path, Handle = handle, Identity = identity });
			}
			catch { handle.Dispose(); throw; }
		}

		internal string Read(string path, int maximum, out string hash)
		{
			Require(!Closed, "file observation already closed");
			Regular(path);
			FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				Identity identity = ReadIdentity(stream.SafeFileHandle);
				Require(identity.Links == 1 && (identity.Attributes & 0x10) == 0
					&& stream.Length > 0 && stream.Length <= maximum, "input is not bounded single-link data");
				byte[] bytes = Bytes(stream);
				hash = Hash(bytes);
				string text = Utf8.GetString(bytes);
				Files.Add(new FileHold { Path = path, Stream = stream, Identity = identity, Hash = hash });
				return text;
			}
			catch { stream.Dispose(); throw; }
		}

		internal void Reprove()
		{
			Require(!Closed, "file observation already closed");
			foreach (DirectoryHold directory in Directories)
			{
				Require(SameDirectory(directory.Identity, ReadIdentity(directory.Handle)), "held directory changed");
				using (SafeFileHandle named = OpenDirectory(directory.Path))
					Require(SameDirectory(directory.Identity, ReadIdentity(named)), "directory name changed owner");
			}
			foreach (FileHold file in Files)
			{
				Regular(file.Path);
				Require(Same(file.Identity, ReadIdentity(file.Stream.SafeFileHandle))
					&& Hash(Bytes(file.Stream)) == file.Hash, "held input changed");
				using (FileStream named = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
					Require(Same(file.Identity, ReadIdentity(named.SafeFileHandle)), "input name changed owner");
			}
		}

		internal static bool Present(string path)
		{
			try { File.GetAttributes(path); return true; }
			catch (FileNotFoundException) { return false; }
			catch (DirectoryNotFoundException) { return false; }
		}
		internal static void RequireAbsent(string path) => Require(!Present(path), "expected absent path exists: " + path);
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure); }
		internal static string Hash(byte[] bytes)
		{
			using (SHA256 sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
		}
		internal static string HashText(string text) => Hash(Utf8.GetBytes(text));
		internal static int ByteCount(string text) => Utf8.GetByteCount(text);

		internal static void WriteReport(string root, string text)
		{
			using (var hold = new KingdomDowngradeFiles())
			{
				hold.Anchor(root);
				string path = Path.Combine(root, KingdomDowngradeRequest.ReportName);
				RequireAbsent(path);
				byte[] bytes = Utf8.GetBytes(text);
				Require(bytes.Length <= 16384, "report exceeds bound");
				using (FileStream report = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{ report.Write(bytes, 0, bytes.Length); report.Flush(true); }
				hold.Reprove();
			}
		}

		private static byte[] Bytes(FileStream stream)
		{
			Require(stream.Length > 0 && stream.Length <= KingdomDowngradeRequest.MaximumSlotBytes,
				"input length changed or exceeds bound");
			byte[] bytes = new byte[(int)stream.Length]; stream.Position = 0;
			int count = 0;
			while (count < bytes.Length)
			{
				int read = stream.Read(bytes, count, bytes.Length - count);
				Require(read > 0, "input truncated while reading"); count += read;
			}
			Require(stream.ReadByte() == -1, "input grew while reading"); return bytes;
		}
		private static void Regular(string path)
		{
			Require((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0,
				"input is not a regular direct file");
		}
		private static SafeFileHandle OpenDirectory(string path)
		{
			Require(Path.GetFullPath(path) == path && (File.GetAttributes(path) &
				(FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory,
				"directory is missing, linked, or noncanonical");
			SafeFileHandle handle = CreateFile(path, 0x80000000, 1, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
			if (handle.IsInvalid)
			{
				int error = Marshal.GetLastWin32Error(); handle.Dispose();
				throw new IOException("directory read lease refused: " + error);
			}
			return handle;
		}
		private static Identity ReadIdentity(SafeFileHandle handle)
		{
			Require(GetFileInformationByHandle(handle, out Identity identity)
				&& (identity.Attributes & 0x400) == 0, "native input identity unavailable or linked");
			return identity;
		}
		private static bool SameDirectory(Identity a, Identity b) => a.Volume == b.Volume
			&& a.IndexHigh == b.IndexHigh && a.IndexLow == b.IndexLow
			&& a.CreatedLow == b.CreatedLow && a.CreatedHigh == b.CreatedHigh
			&& a.Links == b.Links && a.Attributes == b.Attributes;
		private static bool Same(Identity a, Identity b) => SameDirectory(a, b)
			&& a.SizeHigh == b.SizeHigh && a.SizeLow == b.SizeLow
			&& a.WrittenLow == b.WrittenLow && a.WrittenHigh == b.WrittenHigh;

		public void Dispose()
		{
			if (Closed) return; Closed = true; Exception failure = null;
			foreach (FileHold file in Files)
				try { file.Stream.Dispose(); } catch (Exception ex) { failure = failure ?? ex; }
			for (int i = Directories.Count - 1; i >= 0; i--)
				try { Directories[i].Handle.Dispose(); } catch (Exception ex) { failure = failure ?? ex; }
			if (failure != null) throw new IOException("input observation cleanup failed", failure);
		}
		[StructLayout(LayoutKind.Sequential)]
		private struct Identity
		{
			internal uint Attributes, CreatedLow, CreatedHigh, AccessedLow, AccessedHigh,
				WrittenLow, WrittenHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
		}
		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern SafeFileHandle CreateFile(string name, uint access, uint share,
			IntPtr security, uint creation, uint flags, IntPtr template);
		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Identity identity);
	}
}
