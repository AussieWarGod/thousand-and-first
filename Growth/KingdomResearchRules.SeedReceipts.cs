using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomResearchRules
	{
		/// <summary>Canonical receipt identity for one concrete source on one node. The source is
		/// the actual roster key that satisfied an authored requirement, never the requirement's
		/// alias.</summary>
		internal static string SeedReceiptKey(string NodeKey, string ConcreteSource)
		{
			string node = KingdomZoningRules.ComposeKey(KindNode, NodeKey);
			string sourceName = KingdomZoningRules.NameOf(ConcreteSource);
			string sourceKind = KingdomZoningRules.KindOf(ConcreteSource);
			string source = KingdomZoningRules.ComposeKey(sourceKind, sourceName);
			if (node == null || source == null ||
				!string.Equals(source, Fold(ConcreteSource), StringComparison.Ordinal))
			{
				return null;
			}
			string nodeName = KingdomZoningRules.NameOf(node);
			if (nodeName.Length > MaxSeedReceiptNodeLength ||
				source.Length > MaxSeedReceiptSourceLength ||
				Encoding.UTF8.GetByteCount(nodeName) > MaxSeedReceiptNodeLength * 4 ||
				Encoding.UTF8.GetByteCount(source) > MaxSeedReceiptSourceLength * 4)
			{
				return null;
			}
			string body = nodeName.Length.ToString(CultureInfo.InvariantCulture) + "."
				+ nodeName + "=" + source;
			string receipt = SeedReceiptKind + KingdomZoningRules.KindSeparator + body;
			return receipt.Length <= MaxSeedReceiptRowLength
				&& receipt.IndexOf(KingdomZoningRules.RosterSeparator) < 0
				&& Encoding.UTF8.GetByteCount(receipt) <= MaxSeedReceiptRowLength * 4
				? receipt : null;
		}

		/// <summary>How many distinct durable receipts one node owns.</summary>
		internal static int SeedReceiptCount(string Encoded, string NodeKey)
		{
			string node = KingdomZoningRules.ComposeKey(KindNode, NodeKey);
			List<string> receipts;
			if (node == null || !TryReadSeedReceiptStore(Encoded, out receipts))
			{
				return 0;
			}
			string nodeName = KingdomZoningRules.NameOf(node);
			int count = 0;
			for (int i = 0; i < receipts.Count; i++)
			{
				string receiptNode;
				string source;
				if (TryReadSeedReceipt(receipts[i], out receiptNode, out source) &&
					receiptNode == nodeName && ++count >= MaxSeedSourcesPerNode)
				{
					return MaxSeedSourcesPerNode;
				}
			}
			return count;
		}

		internal static bool SeedReceiptStored(string Encoded, string NodeKey, string ConcreteSource)
		{
			string wanted = SeedReceiptKey(NodeKey, ConcreteSource);
			List<string> receipts;
			return wanted != null && TryReadSeedReceiptStore(Encoded, out receipts) &&
				receipts.Contains(wanted);
		}

		/// <summary>Adds one source receipt without duplication and reports the node's new total.
		/// Returning true means the encoded state is valid whether or not it changed.</summary>
		internal static bool TryApplySeedReceipt(string Encoded, string NodeKey,
			string ConcreteSource, out string Updated, out int SourceCount, out bool Changed)
		{
			Updated = Encoded ?? "";
			SourceCount = 0;
			Changed = false;
			string receipt = SeedReceiptKey(NodeKey, ConcreteSource);
			if (receipt == null)
			{
				return false;
			}
			List<string> receipts;
			if (!TryReadSeedReceiptStore(Encoded, out receipts))
			{
				return false;
			}
			List<string> canonical = new List<string>();
			for (int i = 0; i < receipts.Count; i++)
			{
				string receiptNode;
				string receiptSource;
				if (TryReadSeedReceipt(receipts[i], out receiptNode, out receiptSource))
				{
					canonical.Add(receipts[i]);
				}
			}
			receipts = canonical;
			string canonicalStore;
			if (!TryEncodeSeedReceiptStore(receipts, out canonicalStore)) return false;
			int existingCount = SeedReceiptCount(canonicalStore, NodeKey);
			if (existingCount >= MaxSeedSourcesPerNode)
			{
				Updated = canonicalStore;
				Changed = !string.Equals(Updated, Encoded ?? "", StringComparison.Ordinal);
				SourceCount = MaxSeedSourcesPerNode;
				return true;
			}
			if (!receipts.Contains(receipt))
			{
				if (receipts.Count >= MaxSeedReceiptRows)
				{
					return false;
				}
				receipts.Add(receipt);
			}
			if (!TryEncodeSeedReceiptStore(receipts, out Updated)) return false;
			Changed = !string.Equals(Updated, Encoded ?? "", StringComparison.Ordinal);
			SourceCount = SeedReceiptCount(Updated, NodeKey);
			return SourceCount > 0;
		}

		private static bool TryReadSeedReceiptStore(string Encoded, out List<string> Receipts)
		{
			Receipts = new List<string>();
			if (string.IsNullOrEmpty(Encoded))
			{
				return true;
			}
			if (Encoded.Length > MaxSeedReceiptEncodedLength ||
				Encoding.UTF8.GetByteCount(Encoded) > MaxSeedReceiptEncodedUtf8Bytes)
			{
				return false;
			}
			int rows = 1;
			for (int i = 0; i < Encoded.Length; i++)
			{
				if (Encoded[i] == KingdomZoningRules.RosterSeparator && ++rows > MaxSeedReceiptRows)
				{
					return false;
				}
			}
			string[] parts = Encoded.Split(KingdomZoningRules.RosterSeparator);
			HashSet<string> seen = new HashSet<string>();
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i] != null && (parts[i].Length > MaxSeedReceiptRowLength
					|| Encoding.UTF8.GetByteCount(parts[i]) > MaxSeedReceiptRowLength * 4))
				{
					Receipts.Clear();
					return false;
				}
				string row = Fold(parts[i]);
				if (row != null && seen.Add(row)) Receipts.Add(row);
			}
			return Receipts.Count <= MaxSeedReceiptRows;
		}

		private static bool TryEncodeSeedReceiptStore(IEnumerable<string> Receipts,
			out string Encoded)
		{
			Encoded = null;
			List<string> rows = new List<string>();
			HashSet<string> seen = new HashSet<string>();
			if (Receipts != null)
			{
				foreach (string raw in Receipts)
				{
					string row = Fold(raw);
					string node;
					string source;
					if (!TryReadSeedReceipt(row, out node, out source)) return false;
					if (seen.Add(row))
					{
						if (rows.Count >= MaxSeedReceiptRows) return false;
						rows.Add(row);
					}
				}
			}
			string value = string.Join(KingdomZoningRules.RosterSeparator.ToString(), rows.ToArray());
			if (value.Length > MaxSeedReceiptEncodedLength
				|| Encoding.UTF8.GetByteCount(value) > MaxSeedReceiptEncodedUtf8Bytes) return false;
			Encoded = value;
			return true;
		}

		private static bool TryReadSeedReceipt(string Receipt, out string NodeName,
			out string ConcreteSource)
		{
			NodeName = null;
			ConcreteSource = null;
			if (KingdomZoningRules.KindOf(Receipt) != SeedReceiptKind)
			{
				return false;
			}
			string body = KingdomZoningRules.NameOf(Receipt);
			int dot = (body == null) ? -1 : body.IndexOf('.');
			int nodeLength;
			if (dot <= 0 || !int.TryParse(body.Substring(0, dot), NumberStyles.None,
				CultureInfo.InvariantCulture, out nodeLength) || nodeLength <= 0 ||
				body.Substring(0, dot) != nodeLength.ToString(CultureInfo.InvariantCulture))
			{
				return false;
			}
			int nodeStart = dot + 1;
			long separator = (long)nodeStart + nodeLength;
			if (separator >= body.Length || body[(int)separator] != '=')
			{
				return false;
			}
			NodeName = body.Substring(nodeStart, nodeLength);
			ConcreteSource = body.Substring((int)separator + 1);
			string canonical = SeedReceiptKey(NodeName, ConcreteSource);
			if (!string.Equals(canonical, Receipt, StringComparison.Ordinal))
			{
				NodeName = null;
				ConcreteSource = null;
				return false;
			}
			return true;
		}

		/// <summary>
		/// The accrual a node stands at after a seed lands on it. Never lowers what is already
		/// there, never passes <see cref="MaxSeedPercent"/> of the effort, and never completes:
		/// a seed opens a door, and the city walks through it.
		/// </summary>
		/// <param name="EffortDays">The node's authored effort.</param>
		/// <param name="Accrued">What the city has already worked out, in ticks.</param>
		public static int Seeded(int EffortDays, int Accrued)
		{
			int effort = EffortTicks(EffortDays);
			int ceiling = (int)((long)effort * MaxSeedPercent / 100L);
			int seeded = Accrued + (int)((long)effort * SeedPercent / 100L);
			if (seeded > ceiling)
			{
				seeded = ceiling;
			}
			return (seeded < Accrued) ? Accrued : seeded;
		}

		/// <summary>Recoverable seed floor derived from durable distinct-source receipts. Writing a
		/// receipt before applying this floor is safe: a retry recomputes the same floor instead of
		/// adding another quarter.</summary>
		internal static int SeededBySources(int EffortDays, int Accrued, int Sources)
		{
			if (Sources <= 0)
			{
				return Accrued;
			}
			int effort = EffortTicks(EffortDays);
			long percent = (long)Sources * SeedPercent;
			if (percent > MaxSeedPercent)
			{
				percent = MaxSeedPercent;
			}
			int floor = (int)((long)effort * percent / 100L);
			return (Accrued > floor) ? Accrued : floor;
		}

		/// <summary>A failed later receipt must not erase the count already durable for this node.
		/// Counts are monotonic; a successful reread may only preserve or increase them.</summary>
		internal static int DurableSeedSourceCount(int DurableCount, int AttemptedCount)
		{
			int durable = (DurableCount > 0) ? DurableCount : 0;
			return (AttemptedCount > durable) ? AttemptedCount : durable;
		}

	}
}
