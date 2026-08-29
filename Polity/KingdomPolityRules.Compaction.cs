using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		public static bool TryCompactRetiredProfiles(KingdomPolityLedger Ledger,
			string ReceiptId, long Tick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (!TypedId(ReceiptId, "taf:compaction:") || Tick < 0L)
				return Fail("compaction input is invalid", out Failure);
			if (Ledger.Revision == long.MaxValue)
				return Fail("polity revision is exhausted", out Failure);
			List<KingdomPolityProfileRevision> removed = new List<KingdomPolityProfileRevision>();
			for (int i = 0; i < Ledger.Profiles.Count; i++)
				if (!ProfilePinned(Ledger, Ledger.Profiles[i])) removed.Add(Ledger.Profiles[i]);
			if (removed.Count == 0) return Fail("no unreferenced retired profile exists", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			for (int i = candidate.Profiles.Count - 1; i >= 0; i--)
				if (!ProfilePinned(candidate, candidate.Profiles[i])) candidate.Profiles.RemoveAt(i);
			KingdomPolityCompactionReceipt receipt = new KingdomPolityCompactionReceipt
			{
				ReceiptId = ReceiptId, SourceRevision = Ledger.Revision,
				CommittedRevision = Ledger.Revision + 1L, CommitTick = Tick,
				RemovedDigest = Sha256(KingdomPolityCodec.EncodeProfileSetForDigest(removed))
			};
			for (int i = 0; i < removed.Count; i++) receipt.RemovedProfiles.Add(
				new KingdomPolityProfileRef { ProfileId = removed[i].ProfileId, Revision = removed[i].Revision });
			if (candidate.Compactions.Count == MaxCompactions)
			{
				if (candidate.FoldedCompactionCount == long.MaxValue)
					return Fail("compaction fold counter is exhausted", out Failure);
				KingdomPolityCompactionReceipt oldest = candidate.Compactions[0];
				candidate.FoldedCompactionDigest = Sha256(
					KingdomPolityCodec.EncodeCompactionForFold(candidate.FoldedCompactionDigest, oldest));
				candidate.FoldedCompactionCount++; candidate.Compactions.RemoveAt(0);
			}
			for (int i = 0; i < candidate.Compactions.Count; i++)
				if (candidate.Compactions[i].ReceiptId == ReceiptId)
					return Fail("compaction receipt id already exists", out Failure);
			InsertCompaction(candidate.Compactions, receipt); candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		private static bool ProfilePinned(KingdomPolityLedger L,
			KingdomPolityProfileRevision Profile)
		{
			// Revision one is the immutable faction/foundation projection root. Later polity
			// pointers may move, but recovery must still prove the receipt that introduced it.
			if (Profile.Revision == 1) return true;
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].ProfileId == Profile.ProfileId &&
					L.Polities[i].ProfileRevision == Profile.Revision) return true;
			for (int i = 0; i < L.Cohorts.Count; i++)
				if (L.Cohorts[i].ProfileId == Profile.ProfileId &&
					L.Cohorts[i].ProfileRevision == Profile.Revision) return true;
			return false;
		}

		private static void InsertCompaction(List<KingdomPolityCompactionReceipt> Values,
			KingdomPolityCompactionReceipt Value)
		{
			int at = 0;
			while (at < Values.Count && string.CompareOrdinal(Values[at].ReceiptId,
				Value.ReceiptId) < 0) at++;
			Values.Insert(at, Value);
		}

		internal static string Sha256(byte[] Bytes)
		{
			if (Bytes == null) throw new ArgumentNullException(nameof(Bytes));
			byte[] digest;
			using (SHA256 provider = SHA256.Create())
			{
				if (provider == null) throw new InvalidOperationException("SHA-256 is unavailable.");
				digest = provider.ComputeHash(Bytes);
			}
			char[] result = new char[digest.Length * 2]; const string hex = "0123456789abcdef";
			for (int i = 0; i < digest.Length; i++)
			{
				result[i * 2] = hex[digest[i] >> 4]; result[i * 2 + 1] = hex[digest[i] & 15];
			}
			return new string(result);
		}
	}
}
