using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>Why a semantic catalogue or authored draw refused before engine mutation.</summary>
	internal enum KingdomSemanticSelectionFault : byte
	{
		None = 0,
		InvalidCatalogue = 1,
		CatalogueTooLarge = 2,
		InvalidEntry = 3,
		WeightOverflow = 4,
		InvalidEvent = 5,
		RandomFailure = 6,
		InvalidProbe = 7
	}

	/// <summary>One simple merged population row before canonical duplicate folding.</summary>
	internal sealed class KingdomSemanticWeightedEntry
	{
		internal readonly string StableKey;
		internal readonly ulong Weight;

		internal KingdomSemanticWeightedEntry(string stableKey, ulong weight)
		{
			StableKey = stableKey;
			Weight = weight;
		}
	}

	/// <summary>
	/// Pure, versioned semantic selection. Engine adapters may read merged catalogues, but only
	/// this class orders, weights, and draws them. No mutable random stream exists.
	/// </summary>
	internal static class KingdomSemanticSelectionRules
	{
		internal const int RulesVersion = 1;
		internal const int NamingVersion = 1;
		internal const int MaxCatalogueEntries = 256;
		internal const int MaxStableKeyChars = 256;
		private const uint NamingDrawStride = 64U;

		private static readonly string[] NameStarts =
		{
			"A", "Aba", "Aru", "Ba", "Besh", "Cha", "Da", "Drom",
			"E", "Esh", "Fa", "Gha", "Ha", "I", "Ira", "Ja",
			"Ka", "Kesh", "La", "Lu", "Ma", "Naph", "O", "Pa",
			"Qa", "Ra", "Sef", "Sha", "Ta", "U", "Va", "Ya"
		};

		private static readonly string[] NameEnds =
		{
			"bar", "dan", "esh", "far", "gai", "har", "ith", "jar",
			"kai", "lem", "mar", "n", "or", "pesh", "qir", "ras",
			"sem", "tam", "ur", "vash", "wen", "xil", "yara", "zem",
			"ad", "ek", "im", "oth", "un", "et", "ir", "ul"
		};

		private static readonly string[] MerchantTitles =
		{
			"the salt-road factor", "the many-casked", "the chrome appraiser",
			"the water-counter", "the rust-market broker", "the caravan-wise",
			"the ledger beneath the sun", "the buyer of oddments",
			"the seller at six wells", "the keeper of weighed promises",
			"the long-road factor", "the bearer of balanced scales"
		};

		internal static bool TryCanonicalize(IList<KingdomSemanticWeightedEntry> source,
			out List<KingdomSemanticWeightedEntry> canonical, out ulong totalWeight,
			out KingdomSemanticSelectionFault fault)
		{
			canonical = null;
			totalWeight = 0UL;
			if (source == null)
			{
				fault = KingdomSemanticSelectionFault.InvalidCatalogue;
				return false;
			}
			if (source.Count == 0 || source.Count > MaxCatalogueEntries)
			{
				fault = source.Count > MaxCatalogueEntries
					? KingdomSemanticSelectionFault.CatalogueTooLarge
					: KingdomSemanticSelectionFault.InvalidCatalogue;
				return false;
			}

			Dictionary<string, ulong> merged =
				new Dictionary<string, ulong>(StringComparer.Ordinal);
			for (int i = 0; i < source.Count; i++)
			{
				KingdomSemanticWeightedEntry entry = source[i];
				if (entry == null || string.IsNullOrEmpty(entry.StableKey)
					|| entry.StableKey.Length > MaxStableKeyChars || entry.Weight == 0UL)
				{
					fault = KingdomSemanticSelectionFault.InvalidEntry;
					return false;
				}
				ulong before;
				merged.TryGetValue(entry.StableKey, out before);
				if (ulong.MaxValue - before < entry.Weight)
				{
					fault = KingdomSemanticSelectionFault.WeightOverflow;
					return false;
				}
				merged[entry.StableKey] = before + entry.Weight;
			}

			canonical = new List<KingdomSemanticWeightedEntry>(merged.Count);
			foreach (KeyValuePair<string, ulong> row in merged)
				canonical.Add(new KingdomSemanticWeightedEntry(row.Key, row.Value));
			canonical.Sort(delegate(KingdomSemanticWeightedEntry left,
				KingdomSemanticWeightedEntry right)
			{
				return string.CompareOrdinal(left.StableKey, right.StableKey);
			});
			for (int i = 0; i < canonical.Count; i++)
			{
				if (ulong.MaxValue - totalWeight < canonical[i].Weight)
				{
					canonical = null;
					totalWeight = 0UL;
					fault = KingdomSemanticSelectionFault.WeightOverflow;
					return false;
				}
				totalWeight += canonical[i].Weight;
			}
			if (totalWeight == 0UL)
			{
				canonical = null;
				fault = KingdomSemanticSelectionFault.InvalidCatalogue;
				return false;
			}
			fault = KingdomSemanticSelectionFault.None;
			return true;
		}

		internal static bool TryChoose(KernelSeed128 seed, int rulesVersion,
			string settlementId, string eventStreamId, uint eventKind, ulong ordinal,
			uint drawIndex, IList<KingdomSemanticWeightedEntry> source, out string stableKey,
			out KingdomSemanticSelectionFault fault)
		{
			stableKey = null;
			List<KingdomSemanticWeightedEntry> canonical;
			ulong total;
			if (!TryCanonicalize(source, out canonical, out total, out fault)) return false;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			if (!SemanticEventKey.TryCreate(rulesVersion, settlementId, eventStreamId,
				eventKind, ordinal, out key, out kernelFault))
			{
				fault = KingdomSemanticSelectionFault.InvalidEvent;
				return false;
			}
			ulong roll;
			if (!CounterRandom.TryDrawBelow(seed, key, drawIndex, total, out roll,
				out kernelFault))
			{
				fault = KingdomSemanticSelectionFault.RandomFailure;
				return false;
			}
			ulong cursor = 0UL;
			for (int i = 0; i < canonical.Count; i++)
			{
				ulong after = cursor + canonical[i].Weight;
				if (roll >= cursor && roll < after)
				{
					stableKey = canonical[i].StableKey;
					fault = KingdomSemanticSelectionFault.None;
					return true;
				}
				cursor = after;
			}
			fault = KingdomSemanticSelectionFault.InvalidCatalogue;
			return false;
		}

		internal static bool TryChooseIndex(KernelSeed128 seed, SemanticEventKey key,
			uint drawIndex, int count, out int index, out KingdomSemanticSelectionFault fault)
		{
			index = -1;
			if (count <= 0)
			{
				fault = KingdomSemanticSelectionFault.InvalidProbe;
				return false;
			}
			ulong value;
			KernelFaultCode kernelFault;
			if (!CounterRandom.TryDrawBelow(seed, key, drawIndex, (ulong)count, out value,
				out kernelFault))
			{
				fault = KingdomSemanticSelectionFault.RandomFailure;
				return false;
			}
			index = (int)value;
			fault = KingdomSemanticSelectionFault.None;
			return true;
		}

		internal static bool TryName(KernelSeed128 seed, SemanticEventKey key,
			uint firstDrawIndex, out string name, out KingdomSemanticSelectionFault fault)
		{
			name = null;
			ulong versioned = (ulong)firstDrawIndex
				+ (ulong)(NamingVersion - 1) * NamingDrawStride;
			if (NamingVersion <= 0 || versioned >= uint.MaxValue)
			{
				fault = KingdomSemanticSelectionFault.InvalidEvent;
				return false;
			}
			uint startDraw = (uint)versioned;
			int start;
			int end;
			if (!TryChooseIndex(seed, key, startDraw, NameStarts.Length, out start,
				out fault) || !TryChooseIndex(seed, key, startDraw + 1U,
				NameEnds.Length, out end, out fault)) return false;
			name = NameStarts[start] + NameEnds[end];
			fault = KingdomSemanticSelectionFault.None;
			return true;
		}

		internal static bool TryMerchantTitle(KernelSeed128 seed, SemanticEventKey key,
			uint drawIndex, out string title, out KingdomSemanticSelectionFault fault)
		{
			title = null;
			int index;
			if (!TryChooseIndex(seed, key, drawIndex, MerchantTitles.Length, out index,
				out fault)) return false;
			title = MerchantTitles[index];
			fault = KingdomSemanticSelectionFault.None;
			return true;
		}

		/// <summary>One drawn start followed by a fixed row-major cyclic probe.</summary>
		internal static bool TryProbeStart(KernelSeed128 seed, SemanticEventKey key,
			uint drawIndex, int width, int height, out int start,
			out KingdomSemanticSelectionFault fault)
		{
			start = -1;
			if (width <= 0 || height <= 0 || width > int.MaxValue / height)
			{
				fault = KingdomSemanticSelectionFault.InvalidProbe;
				return false;
			}
			return TryChooseIndex(seed, key, drawIndex, width * height, out start, out fault);
		}

		internal static int ProbeIndex(int start, int offset, int count)
		{
			if (start < 0 || start >= count || offset < 0 || offset >= count || count <= 0)
				return -1;
			int remaining = count - start;
			return offset < remaining ? start + offset : offset - remaining;
		}

		/// <summary>Turns an arbitrary durable owner ID into an admitted event-stream ID.</summary>
		internal static bool TryOwnerStreamId(string domain, string ownerId,
			out string streamId)
		{
			streamId = null;
			if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(ownerId)
				|| domain.Length > 24 || ownerId.Length > 512) return false;
			for (int i = 0; i < domain.Length; i++)
			{
				char c = domain[i];
				if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
					return false;
			}
			byte[] digest;
			try
			{
				using (SHA256 sha = SHA256.Create())
					digest = sha.ComputeHash(Encoding.UTF8.GetBytes(ownerId));
			}
			catch { return false; }
			StringBuilder text = new StringBuilder(96);
			text.Append("taf:semantic:").Append(domain).Append(":v1:");
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
			streamId = text.ToString();
			return KernelSemanticId.IsValid(streamId);
		}
	}
}
