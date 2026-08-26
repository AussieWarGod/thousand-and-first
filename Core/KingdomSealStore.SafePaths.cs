using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		private bool TrySafeFolder(string Folder, bool Create, out string PathValue,
			out bool Exists, out string Failure)
		{
			PathValue = "";
			Exists = false;
			Failure = "";
			if (!IsFixedFolder(Folder))
			{
				Failure = "the store path does not name a fixed folder";
				return false;
			}
			bool rootExists;
			if (!TrySafeDirectory(_root, Create, out rootExists, out Failure))
			{
				return false;
			}
			if (!rootExists)
			{
				return true;
			}
			PathValue = Path.GetFullPath(Path.Combine(_root, Folder));
			if (!Contained(PathValue))
			{
				Failure = "the store folder escapes its root";
				return false;
			}
			return TrySafeDirectory(PathValue, Create, out Exists, out Failure);
		}

		private bool TrySafeDirectory(string PathValue, bool Create, out bool Exists,
			out string Failure)
		{
			Exists = false;
			Failure = "";
			FileAttributes attributes;
			if (!TryDirectoryAttributes(PathValue, out attributes, out Exists, out Failure))
			{
				return false;
			}
			if (!Exists && Create)
			{
				try
				{
					Directory.CreateDirectory(PathValue);
				}
				catch (Exception ex)
				{
					Failure = "the store folder could not be created: " + ex.Message;
					return false;
				}
				if (!TryDirectoryAttributes(PathValue, out attributes, out Exists, out Failure))
				{
					return false;
				}
			}
			if (Exists && ((attributes & FileAttributes.Directory) == 0
				|| (attributes & FileAttributes.ReparsePoint) != 0))
			{
				Failure = "the store folder is not a direct regular directory";
				return false;
			}
			return true;
		}

		private static bool TryDirectoryAttributes(string PathValue, out FileAttributes Attributes,
			out bool Exists, out string Failure)
		{
			Attributes = 0;
			Exists = false;
			Failure = "";
			try
			{
				Attributes = File.GetAttributes(PathValue);
				Exists = true;
				return true;
			}
			catch (FileNotFoundException)
			{
				return true;
			}
			catch (DirectoryNotFoundException)
			{
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the store path could not be inspected: " + ex.Message;
				return false;
			}
		}

		private bool TrySafeLeaf(string PathValue, out bool Exists, out string Failure)
		{
			Exists = false;
			Failure = "";
			string full;
			try
			{
				full = Path.GetFullPath(PathValue);
			}
			catch (Exception ex)
			{
				Failure = "the store leaf path is invalid: " + ex.Message;
				return false;
			}
			if (!Contained(full) || !string.Equals(full, PathValue, _pathComparison))
			{
				Failure = "the store leaf escapes its root";
				return false;
			}
			string folderName;
			if (!TryFixedFolderOf(full, out folderName))
			{
				Failure = "the store leaf is outside a fixed folder";
				return false;
			}
			string folder;
			bool folderExists;
			if (!TrySafeFolder(folderName, false, out folder, out folderExists, out Failure))
			{
				return false;
			}
			if (!folderExists)
			{
				return true;
			}
			FileAttributes attributes;
			try
			{
				attributes = _files.Attributes(full);
				Exists = true;
			}
			catch (FileNotFoundException)
			{
				return true;
			}
			catch (DirectoryNotFoundException)
			{
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the store leaf could not be inspected: " + ex.Message;
				return false;
			}
			if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
			{
				Failure = "the store leaf is not a direct regular file";
				return false;
			}
			return true;
		}

		private bool TryEnsureFolderOf(string PathValue, out string Failure)
		{
			Failure = "";
			string folderName;
			if (!TryFixedFolderOf(Path.GetFullPath(PathValue), out folderName))
			{
				Failure = "the store write is outside a fixed folder";
				return false;
			}
			string folder;
			bool exists;
			return TrySafeFolder(folderName, true, out folder, out exists, out Failure) && exists;
		}

		private bool Contained(string PathValue)
		{
			return PathValue != null && PathValue.StartsWith(_rootPrefix, _pathComparison);
		}

		private bool TryFixedFolderOf(string PathValue, out string Folder)
		{
			Folder = "";
			string parent = Path.GetDirectoryName(PathValue);
			string[] fixedFolders = new string[]
			{
				StagesFolder, LegaciesFolder, ReceiptsFolder, ClaimsFolder
			};
			for (int i = 0; i < fixedFolders.Length; i++)
			{
				string expected = Path.GetFullPath(Path.Combine(_root, fixedFolders[i]));
				if (string.Equals(parent, expected, _pathComparison))
				{
					Folder = fixedFolders[i];
					return true;
				}
			}
			return false;
		}

		private static bool IsFixedFolder(string Folder)
		{
			return Folder == StagesFolder || Folder == LegaciesFolder
				|| Folder == ReceiptsFolder || Folder == ClaimsFolder;
		}

		private void TryDelete(string PathValue)
		{
			try
			{
				bool exists;
				string failure;
				if (TrySafeLeaf(PathValue, out exists, out failure) && exists)
				{
					_files.DeleteIfExists(PathValue);
				}
			}
			catch (Exception)
			{
			}
		}

	}
}
