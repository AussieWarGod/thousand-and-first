using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		private bool TryWriteSeal(string PathValue, KingdomSealRecord Record, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			string text;
			try
			{
				text = Record.Compose();
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			string temp = TempPath(PathValue);
			try
			{
				if (!TryEnsureFolderOf(PathValue, out Failure))
				{
					return false;
				}
				bool tempExists;
				if (!TrySafeLeaf(temp, out tempExists, out Failure) || tempExists)
				{
					if (Failure.Length == 0) Failure = "the random seal staging leaf already exists";
					return false;
				}
				_files.WriteAllTextDurable(temp, text);
				KingdomSealRecord echo = ReadSlot(temp);
				if (echo == null || !SameRecord(echo, Record))
				{
					Failure = "the seal did not read back the same";
					return false;
				}
				return Install(temp, PathValue, ReplaceExisting, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				TryDelete(temp);
			}
		}

		private bool TryWriteReceiptFile(KingdomSealReceipt Receipt, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			string path = ReceiptPath(Receipt.LegacyId, Receipt.TargetGameId);
			string temp = TempPath(path);
			try
			{
				if (!TryEnsureFolderOf(path, out Failure))
				{
					return false;
				}
				bool tempExists;
				if (!TrySafeLeaf(temp, out tempExists, out Failure) || tempExists)
				{
					if (Failure.Length == 0) Failure = "the random receipt staging leaf already exists";
					return false;
				}
				string text = Receipt.Compose();
				_files.WriteAllTextDurable(temp, text);
				KingdomSealReceipt echo;
				string echoText = ReadText(temp);
				if (echoText == null || !KingdomSealReceipt.TryParse(echoText, out echo)
					|| !SameReceipt(echo, Receipt))
				{
					Failure = "the receipt did not read back the same";
					return false;
				}
				return Install(temp, path, ReplaceExisting, out Failure);
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			finally
			{
				TryDelete(temp);
			}
		}

		private bool Install(string Temp, string PathValue, bool ReplaceExisting, out string Failure)
		{
			Failure = "";
			try
			{
				bool tempExists;
				bool destinationExists;
				if (!TrySafeLeaf(Temp, out tempExists, out Failure) || !tempExists
					|| !TrySafeLeaf(PathValue, out destinationExists, out Failure))
				{
					if (Failure.Length == 0) Failure = "the atomic staging leaf disappeared";
					return false;
				}
				if (!destinationExists)
				{
					_files.MoveNew(Temp, PathValue);
					bool installed;
					return TrySafeLeaf(PathValue, out installed, out Failure) && installed;
				}
				if (!ReplaceExisting)
				{
					Failure = "the destination already exists";
					return false;
				}
				string backup = PathValue + ".backup." + Guid.NewGuid().ToString("N");
				bool backupExists;
				if (!TrySafeLeaf(backup, out backupExists, out Failure) || backupExists)
				{
					if (Failure.Length == 0) Failure = "the random backup leaf already exists";
					return false;
				}
				try
				{
					_files.ReplaceAtomic(Temp, PathValue, backup);
					bool installed;
					if (!TrySafeLeaf(PathValue, out installed, out Failure) || !installed)
					{
						if (Failure.Length == 0) Failure = "the atomic replacement did not leave a regular destination";
						return false;
					}
					TryDelete(backup);
					return true;
				}
				catch (Exception ex)
				{
					// No delete-then-move fallback: after a failed replacement only the
					// platform can still know whether the old durable leaf survived.
					Failure = ex.Message;
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
		}

		private KingdomSealRecord ReadSlot(string PathValue)
		{
			string text = ReadText(PathValue);
			if (text == null)
			{
				return null;
			}
			KingdomSealRecord record;
			KingdomSealFault fault;
			string detail;
			return KingdomSealRecord.TryParse(text, out record, out fault, out detail) ? record : null;
		}

		private string ReadText(string PathValue)
		{
			try
			{
				bool exists;
				string failure;
				if (!TrySafeLeaf(PathValue, out exists, out failure) || !exists)
				{
					return null;
				}
				long length = _files.Length(PathValue);
				if (length < 0L || length > KingdomSealFormat.MaxFileChars)
				{
					return null;
				}
				string text = _files.ReadAllText(PathValue);
				bool stillExists;
				return TrySafeLeaf(PathValue, out stillExists, out failure) && stillExists ? text : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private IEnumerable<string> Files(string Folder, string Extension, int RecognizedLimit,
			out bool Overflow, out int RefusedJunk)
		{
			Overflow = false;
			RefusedJunk = 0;
			try
			{
				string folder;
				bool folderExists;
				string folderFailure;
				if (!TrySafeFolder(Folder, false, out folder, out folderExists, out folderFailure))
				{
					Overflow = true;
					return new string[0];
				}
				if (!folderExists)
				{
					return new string[0];
				}
				List<string> found = new List<string>();
				int inspected = 0;
				int totalLimit = RecognizedLimit + MaxFilesScanned;
				foreach (string path in Directory.EnumerateFiles(folder, "*",
					SearchOption.TopDirectoryOnly))
				{
					if (++inspected > totalLimit)
					{
						Overflow = true;
						break;
					}
					string name = Path.GetFileName(path);
					bool leafExists;
					string leafFailure;
					if (!TrySafeLeaf(path, out leafExists, out leafFailure) || !leafExists)
					{
						RefusedJunk++;
						continue;
					}
					if (name.EndsWith(Extension, StringComparison.Ordinal))
					{
						if (found.Count >= RecognizedLimit)
						{
							Overflow = true;
							break;
						}
						found.Add(path);
						continue;
					}
					if (KnownOperationalJunk(Folder, name, Extension))
					{
						continue;
					}
					if (RefusedJunk < MaxFilesScanned + 1)
					{
						RefusedJunk++;
					}
				}
				found.Sort(StringComparer.Ordinal);
				return found;
			}
			catch (Exception)
			{
				Overflow = true;
				return new string[0];
			}
		}

		private static bool KnownOperationalJunk(string Folder, string Name, string Extension)
		{
			if (Folder == ReceiptsFolder && Name == ".claims.lock")
			{
				return true;
			}
			if (Folder == StagesFolder && Name.StartsWith(".journal-", StringComparison.Ordinal)
				&& Name.EndsWith(".lock", StringComparison.Ordinal))
			{
				return true;
			}
			return Name.IndexOf(Extension + ".writing.", StringComparison.Ordinal) >= 0
				|| Name.IndexOf(Extension + ".backup.", StringComparison.Ordinal) >= 0
				|| Name.IndexOf(Extension + ".released.", StringComparison.Ordinal) >= 0;
		}

	}
}
