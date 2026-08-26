#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomArchivedSettlementCodecLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomArchivedSettlementCodec.cs",
			"Core/KingdomArchivedSettlementCodec.EncodeV1ToV4.cs",
			"Core/KingdomArchivedSettlementCodec.EncodeV5ToV11.cs",
			"Core/KingdomArchivedSettlementCodec.DecodeCloneHash.cs",
			"Core/KingdomArchivedSettlementCodec.MutableGraph.cs",
			"Core/KingdomArchivedSettlementCodec.Topology.cs",
			"Core/KingdomArchivedSettlementCodec.ValueWriter.cs",
			"Core/KingdomArchivedSettlementCodec.ValueReader.cs",
			"Core/KingdomArchivedSettlementCodec.Schema.cs",
			"Core/KingdomArchivedSettlementCodec.Shape.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			return source.ToString();
		}
	}
}
#endif
