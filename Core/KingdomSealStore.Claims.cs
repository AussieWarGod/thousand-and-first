using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal List<KingdomSealReceipt> ReadReceipts()
		{
			int refused;
			return ReadReceipts(out refused);
		}

		internal List<KingdomSealReceipt> ReadReceipts(out int Refused)
		{
			Refused = 0;
			List<KingdomSealReceipt> receipts = new List<KingdomSealReceipt>();
			bool overflow;
			int refusedJunk;
			foreach (string path in Files(ReceiptsFolder, ReceiptExtension,
				MaxFilesScanned, out overflow, out refusedJunk))
			{
				if (!path.EndsWith(ReceiptExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string legacy;
				string target;
				KingdomSealReceipt receipt;
				string text = ReadText(path);
				if (TryParseReceiptTuple(Path.GetFileName(path), out legacy, out target)
					&& text != null && KingdomSealReceipt.TryParse(text, out receipt)
					&& receipt.LegacyId == legacy && receipt.TargetGameId == target)
				{
					receipts.Add(receipt);
				}
				else
				{
					Refused++;
				}
			}
			if (overflow)
			{
				Refused++;
			}
			Refused += refusedJunk;
			return receipts;
		}

		internal HashSet<string> SpentLegacyIds()
		{
			HashSet<string> spent = new HashSet<string>();
			List<KingdomSealReceipt> receipts = ReadReceipts();
			for (int i = 0; i < receipts.Count; i++)
			{
				if (receipts[i].State != KingdomSealReceiptState.Reserved)
				{
					spent.Add(receipts[i].LegacyId);
				}
			}
			return spent;
		}

		private bool TryFindReceipt(string LegacyId, out KingdomSealReceipt Receipt, out string Failure)
		{
			Receipt = null;
			Failure = "";
			bool overflow;
			int refusedJunk;
			IEnumerable<string> paths = Files(ReceiptsFolder, ReceiptExtension,
				MaxFilesScanned, out overflow, out refusedJunk);
			if (overflow || refusedJunk > 0)
			{
				Failure = overflow
					? "the receipt folder holds too many files to claim safely"
					: "the receipt folder contains unrecognized files";
				return false;
			}
			foreach (string path in paths)
			{
				if (!path.EndsWith(ReceiptExtension, StringComparison.Ordinal))
				{
					continue;
				}
				string namedLegacy;
				string namedTarget;
				if (!TryParseReceiptTuple(Path.GetFileName(path), out namedLegacy, out namedTarget))
				{
					Failure = "the receipt folder contains an invalid filename tuple";
					return false;
				}
				string text = ReadText(path);
				KingdomSealReceipt parsed;
				if (text == null || !KingdomSealReceipt.TryParse(text, out parsed)
					|| parsed.LegacyId != namedLegacy || parsed.TargetGameId != namedTarget)
				{
					Failure = "an existing receipt does not match its filename tuple";
					return false;
				}
				if (namedLegacy != LegacyId)
				{
					continue;
				}
				if (Receipt != null)
				{
					Failure = "that legacy has more than one receipt";
					return false;
				}
				Receipt = parsed;
			}
			return true;
		}


		private bool TryAcquireLiveClaim(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out string Failure)
		{
			bool contended;
			if (TryAcquireLiveClaim(Receipt, out Lease, out contended, out Failure))
			{
				return true;
			}
			if (contended)
			{
				Failure = "the reservation is held by a live target world";
			}
			return false;
		}

		private bool TryAcquireLiveClaim(KingdomSealReceipt Receipt,
			out KingdomSealReservationLease Lease, out bool Contended, out string Failure)
		{
			Lease = null;
			Contended = false;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(ClaimsFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string claim = ClaimPath(Receipt.LegacyId, Receipt.TargetGameId);
				bool claimExists;
				if (!TrySafeLeaf(claim, out claimExists, out Failure))
				{
					return false;
				}
				FileStream gate = new FileStream(claim,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(claim, out openedExists, out Failure) || !openedExists)
				{
					gate.Dispose();
					return false;
				}
				Lease = new KingdomSealReservationLease(Receipt, gate);
				return true;
			}
			catch (IOException)
			{
				// A sharing violation is the positive live-claim signal. Other I/O failures are
				// deliberately treated the same way: without exclusive proof, release is unsafe.
				Contended = true;
				return false;
			}
			catch (Exception ex)
			{
				Failure = "the reservation live-claim lock is unavailable: " + ex.Message;
				return false;
			}
		}

		private bool TryRemoveReservationLocked(KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			string path = ReceiptPath(Receipt.LegacyId, Receipt.TargetGameId);
			string released = path + ".released." + Guid.NewGuid().ToString("N");
			try
			{
				bool sourceExists;
				bool releasedExists;
				if (!TrySafeLeaf(path, out sourceExists, out Failure) || !sourceExists
					|| !TrySafeLeaf(released, out releasedExists, out Failure) || releasedExists)
				{
					if (Failure.Length == 0) Failure = "the exact reserved receipt leaf is unavailable";
					return false;
				}
				_files.MoveNew(path, released);
				if (!TrySafeLeaf(released, out releasedExists, out Failure) || !releasedExists)
				{
					if (Failure.Length == 0) Failure = "the released receipt is not a regular leaf";
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = ex.Message;
				return false;
			}
			TryDelete(released);
			return true;
		}

		private bool TryLockReceipts(out FileStream Gate, out string Failure)
		{
			Gate = null;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(ReceiptsFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string path = Path.Combine(_root, ReceiptsFolder, ".claims.lock");
				bool exists;
				if (!TrySafeLeaf(path, out exists, out Failure))
				{
					return false;
				}
				Gate = new FileStream(path,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(path, out openedExists, out Failure) || !openedExists)
				{
					Gate.Dispose();
					Gate = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the receipt claim lock is unavailable: " + ex.Message;
				return false;
			}
		}

		private bool TryLockStage(string OriginGameId, out FileStream Gate, out string Failure)
		{
			Gate = null;
			Failure = "";
			try
			{
				string folder;
				bool folderExists;
				if (!TrySafeFolder(StagesFolder, true, out folder, out folderExists, out Failure)
					|| !folderExists)
				{
					return false;
				}
				string path = Path.Combine(_root, StagesFolder,
					".journal-" + OriginGameId + ".lock");
				bool exists;
				if (!TrySafeLeaf(path, out exists, out Failure))
				{
					return false;
				}
				Gate = new FileStream(path,
					FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				bool openedExists;
				if (!TrySafeLeaf(path, out openedExists, out Failure) || !openedExists)
				{
					Gate.Dispose();
					Gate = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "the origin journal lock is unavailable: " + ex.Message;
				return false;
			}
		}

	}
}
