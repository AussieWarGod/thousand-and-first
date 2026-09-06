#if TAF_TESTS
using System.IO;

namespace ThousandAndFirst
{
	// Test-only constant alias, not a Chronicle dispatcher or an authority stub.
	public static partial class KingdomChronicle
	{
		public const int MaxEntries = KingdomChronicleReceiptRules.MaxEntries;
	}

	public sealed partial class KingdomRealmArchive
	{
		// Verbatim TASK53 schema-threaded method from .13SettlementTopology.cs:128-142.
		// The whole original file depends on engine KingdomSystem; only this pure
		// hash dependency is included here. No validation or hash body is replaced.
		private static void WriteTopologyGraph(BinaryWriter Writer,
			KingdomSettlementTopology Topology, int SettlementSchema)
		{
			if (Topology == null || Topology.HasOpaqueEvidence ||
				Topology.Count > KingdomSettlementTopologyRules.MaxNonSeatSettlements)
				throw new InvalidDataException("Realm settlement topology is not hashable.");
			Writer.Write(Topology.Count);
			for (int i = 0; i < Topology.Count; i++)
			{
				if (!KingdomArchivedSettlementCodec.TryEncodeVersion(Topology.Get(i),
					SettlementSchema, out byte[] payload, out string failure))
					throw new InvalidDataException(failure);
				WriteGraphBytes(Writer, payload);
			}
		}
	}
}
#endif
