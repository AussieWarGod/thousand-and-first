using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	// Standalone old-source overlay: no dependency on newer scenario Harness helpers.
	internal static class KingdomUpgradeFiles
	{
		internal const long MaxSaveBytes = 268435456;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static string Root()
		{
			Check(Environment.OSVersion.Platform == PlatformID.Win32NT && !string.IsNullOrEmpty(XRLCore.SavePath),
				"upgrade witness requires the owned Windows profile");
			string root = Path.GetDirectoryName(XRLCore.SavePath);
			Check(root != null && root.Length > 3 && root[1] == ':' && root[2] == '\\'
				&& char.IsLetter(root[0]) && Path.GetDirectoryName(root) == root.Substring(0, 3), "upgrade profile is not a drive-root child");
			string name = Path.GetFileName(root);
			string prefix = name.StartsWith("taf-smoke.", StringComparison.Ordinal) ? "taf-smoke."
				: name.StartsWith("taf-scenario.", StringComparison.Ordinal) ? "taf-scenario." : null;
			Check(prefix != null && name.Length > prefix.Length, "upgrade profile name is not owned");
			for (int i = prefix.Length; i < name.Length; i++) Check(Ascii(name[i]), "upgrade profile suffix is not canonical");
			Check(Same(XRLCore.SavePath, Path.Combine(root, "Save")) && Same(XRLCore.LocalPath, Path.Combine(root, "Local"))
				&& Same(XRLCore.SyncedPath, Path.Combine(root, "Synced")), "upgrade paths do not share their exact root");
			DirectoryExact(root); DirectoryExact(XRLCore.LocalPath); DirectoryExact(XRLCore.SyncedPath);
			return root;
		}

		internal static string SaveDirectory(string root, string gameId)
		{
			Check(KingdomUpgradeSnapshotCodec.GameId(gameId), "upgrade game ID is not canonical");
			string saves = Path.Combine(root, "Synced", "Saves"); DirectoryExact(saves);
			string selected = Path.Combine(saves, gameId); DirectoryExact(selected);
			return selected; // Other saves and backups are neither selected nor rewritten.
		}

		internal static string Read(string path, int maximum)
		{
			using (FileStream file = Open(path, maximum))
			{
				byte[] bytes = new byte[checked((int)file.Length)]; int at = 0;
				while (at < bytes.Length)
				{ int count = file.Read(bytes, at, bytes.Length - at); if (count == 0) throw new EndOfStreamException(); at += count; }
				Check(file.Position == file.Length, "upgrade evidence changed length");
				return Utf8.GetString(bytes);
			}
		}

		internal static string HashFile(string path, long maximum)
		{
			using (FileStream file = Open(path, maximum))
			using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(file));
		}

		internal static string HashText(string value)
		{ using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Utf8.GetBytes(value))); }

		internal static FileStream Open(string path, long maximum)
		{
			FileInfo info = new FileInfo(path);
			Check(info.Exists && (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0
				&& info.Length > 0 && info.Length <= maximum, "upgrade evidence is not a bounded ordinary file");
			FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				Check(GetFileInformationByHandleEx(file.SafeFileHandle, 1, out FileStandardInformation facts,
					(uint)Marshal.SizeOf(typeof(FileStandardInformation))) && facts.NumberOfLinks == 1
					&& !facts.DeletePending && !facts.Directory && file.Length == info.Length, "upgrade evidence is not singly owned");
				return file;
			}
			catch { file.Dispose(); throw; }
		}

		internal static void New(string path, string text)
		{
			byte[] bytes = Utf8.GetBytes(text);
			using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{ file.Write(bytes, 0, bytes.Length); file.Flush(true); }
		}

		internal static void Vacant(string path)
		{ Check(!File.Exists(path) && !Directory.Exists(path), "upgrade evidence already exists"); }

		internal static void DirectoryExact(string path)
		{
			DirectoryInfo info = new DirectoryInfo(path);
			Check(info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0, "upgrade directory is missing or linked");
		}

		internal static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
		internal static void Check(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
		private static bool Ascii(char c) { return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'; }
		private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }

		[StructLayout(LayoutKind.Sequential)]
		private struct FileStandardInformation
		{
			internal long AllocationSize, EndOfFile;
			internal uint NumberOfLinks;
			[MarshalAs(UnmanagedType.U1)] internal bool DeletePending;
			[MarshalAs(UnmanagedType.U1)] internal bool Directory;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind,
			out FileStandardInformation information, uint size);
	}
}
