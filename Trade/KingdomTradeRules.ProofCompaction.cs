using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		internal static bool TryCompactProofRows(KingdomTradeBook Book,
			List<KingdomTradeProof> Proofs, List<int> Indexes, out string Failure)
		{
			Failure = null;
			if (!BookUsable(Book) || Proofs == null || Indexes == null || Proofs.Count < 1 ||
				Proofs.Count != Indexes.Count || Proofs.Count > MaxRecentProofs)
				return CompactFail("Trade proof compaction request is invalid", out Failure);
			for (int i = 0; i < Proofs.Count; i++)
				if (Indexes[i] < 0 || Indexes[i] >= Book.RecentProofs.Count || i > 0 &&
					Indexes[i] <= Indexes[i - 1] || !ReferenceEquals(
						Book.RecentProofs[Indexes[i]], Proofs[i]) || !ValidProof(Book,
							Proofs[i], true) || Proofs[i].RealmId != Book.RealmId)
					return CompactFail("Trade proof compaction source changed", out Failure);
			for (int i = 0; i < Book.CompactedProofs.Count; i++)
				if (!ValidProofCompaction(Book.CompactedProofs[i]) ||
					Book.CompactedProofs[i].RealmId != Book.RealmId)
					return CompactFail("Trade proof compaction history is foreign or invalid",
						out Failure);
			string digest = ProofCompactionDigest(Proofs, Book.CompactedProofs);
			if (!ValidId(digest)) return CompactFail(
				"Trade proof compaction digest could not be frozen", out Failure);
			long first = Proofs[0].Sequence, last = Proofs[0].Sequence;
			for (int i = 1; i < Proofs.Count; i++)
			{
				first = Math.Min(first, Proofs[i].Sequence);
				last = Math.Max(last, Proofs[i].Sequence);
			}
			int total = Proofs.Count;
			bool merge = Book.CompactedProofs.Count >= MaxCompactedProofs;
			if (merge)
				for (int i = 0; i < Book.CompactedProofs.Count; i++)
				{
					KingdomTradeProofCompaction prior = Book.CompactedProofs[i];
					if (prior.ProofCount > int.MaxValue - total)
						return CompactFail("Trade proof compaction history is invalid", out Failure);
					total += prior.ProofCount;
					first = Math.Min(first, prior.FirstSequence);
					last = Math.Max(last, prior.LastSequence);
				}
			KingdomTradeProofCompaction compact = new KingdomTradeProofCompaction
			{
				RealmId = Book.RealmId, FirstSequence = first, LastSequence = last,
				ProofCount = total, EvidenceHash = digest
			};
			for (int i = Indexes.Count - 1; i >= 0; i--)
				Book.RecentProofs.RemoveAt(Indexes[i]);
			if (merge) Book.CompactedProofs.Clear();
			Book.CompactedProofs.Add(compact);
			return true;
		}

		private static string ProofCompactionDigest(List<KingdomTradeProof> Proofs,
			List<KingdomTradeProofCompaction> Prior)
		{
			try
			{
				KingdomTradeBook evidence = new KingdomTradeBook
				{
					RecentProofs = new List<KingdomTradeProof>(Proofs),
					CompactedProofs = new List<KingdomTradeProofCompaction>(Prior)
				};
				byte[] encoded = KingdomTradeCodec.EncodePayload(evidence);
				using (MemoryStream canonical = new MemoryStream())
				{
					if (!WriteCanonicalField(canonical, IdentityNamespace) ||
						!WriteCanonicalField(canonical, "proof-compaction")) return null;
					WriteInt32(canonical, encoded.Length);
					canonical.Write(encoded, 0, encoded.Length);
					using (SHA256 sha = SHA256.Create())
						return Hex(sha.ComputeHash(canonical.ToArray()));
				}
			}
			catch { return null; }
		}

		private static bool CompactFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
