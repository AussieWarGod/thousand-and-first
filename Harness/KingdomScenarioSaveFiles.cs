using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using XRL.Core;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioSaveFiles
	{
		internal const string SnapshotFile = "scenario-save-snapshot.txt";
		internal const string ReceiptFile = "scenario-save-receipt.txt";
		internal const string LoadedSnapshotFile = "scenario-load-snapshot.txt";
		internal const string SnapshotKey = "r_TAF_ScenarioSaveSnapshot_v1";
		internal const long MaxSaveBytes = 268435456;
		private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

		internal static string Root()
		{
			string root = KingdomScenarioJournal.ProfileRoot();
			Require(Environment.OSVersion.Platform == PlatformID.Win32NT && root != null
				&& root.Length > 16 && (root[0] >= 'A' && root[0] <= 'Z' || root[0] >= 'a' && root[0] <= 'z')
				&& root[1] == ':' && root[2] == '\\'
				&& root.Substring(3).StartsWith("taf-scenario.", StringComparison.Ordinal), "non-scenario profile refused");
			for (int i = 16; i < root.Length; i++)
				Require(root[i] >= 'a' && root[i] <= 'z' || root[i] >= 'A' && root[i] <= 'Z'
					|| root[i] >= '0' && root[i] <= '9', "noncanonical profile suffix");
			Require(Same(XRLCore.SavePath, Path.Combine(root, "Save"))
				&& Same(XRLCore.LocalPath, Path.Combine(root, "Local"))
				&& Same(XRLCore.SyncedPath, Path.Combine(root, "Synced")), "profile paths do not share exact ownership");
			DirectoryExact(root); DirectoryExact(Path.Combine(root, "Local"));
			DirectoryExact(Path.Combine(root, "Synced")); DirectoryExact(Path.Combine(root, "Synced", "Saves"));
			return root;
		}

		internal static string SaveDirectory(string Root, string GameId)
		{
			Guid id;
			Require(Guid.TryParseExact(GameId, "D", out id) && id.ToString("D") == GameId, "save game ID is not canonical");
			string path = Path.Combine(Root, "Synced", "Saves", GameId);
			DirectoryExact(path);
			string[] saves = Directory.GetDirectories(Path.Combine(Root, "Synced", "Saves"));
			Require(saves.Length == 1 && Same(saves[0], path), "profile does not contain exactly the named save");
			return path;
		}

		internal static bool LoadPresent()
		{
			string local = XRLCore.LocalPath;
			if (string.IsNullOrEmpty(local)) return false;
			string path = Path.Combine(local, KingdomScenarioLoadRules.FileName);
			return File.Exists(path) || Directory.Exists(path);
		}

		internal static string ReadText(string Path, int MaxBytes)
		{
			using (FileStream file = Open(Path, MaxBytes))
			using (StreamReader reader = new StreamReader(file, Utf8, false)) return reader.ReadToEnd();
		}

		internal static string HashFile(string Path, long MaxBytes)
		{
			using (FileStream file = Open(Path, MaxBytes))
			using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(file));
		}

		internal static string HashText(string Text)
		{
			using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Utf8.GetBytes(Text)));
		}

		internal static void WriteNew(string Path, string Text)
		{
			byte[] bytes = Utf8.GetBytes(Text);
			using (FileStream file = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
			{ file.Write(bytes, 0, bytes.Length); file.Flush(true); }
		}

		internal static FileStream Open(string Path, long MaxBytes)
		{
			FileInfo info = new FileInfo(Path);
			Require(info.Exists && info.Name == System.IO.Path.GetFileName(Path)
				&& (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0
				&& info.Length > 0 && info.Length <= MaxBytes, "save evidence is not a bounded ordinary file");
			FileStream file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
			try
			{
				FileStandardInformation facts;
				Require(GetFileInformationByHandleEx(file.SafeFileHandle, 1, out facts,
					(uint)Marshal.SizeOf(typeof(FileStandardInformation))) && facts.NumberOfLinks == 1
					&& !facts.DeletePending && !facts.Directory && file.Length == info.Length,
					"save evidence file identity is not singly owned");
				return file;
			}
			catch { file.Dispose(); throw; }
		}

		internal static void DirectoryExact(string Path)
		{
			DirectoryInfo directory = new DirectoryInfo(Path);
			Require(directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) == 0,
				"save evidence directory is missing or linked");
		}

		internal static bool Same(string Left, string Right)
		{
			return string.Equals(Left, Right, StringComparison.OrdinalIgnoreCase);
		}

		private static string Hex(byte[] Bytes)
		{
			return BitConverter.ToString(Bytes).Replace("-", "").ToLowerInvariant();
		}

		internal static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}

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
		private static extern bool GetFileInformationByHandleEx(SafeFileHandle Handle, int Class,
			out FileStandardInformation Information, uint Size);
	}
}
