#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomChronicleReceiptRulesLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Chronicle/KingdomChronicleReceiptRules.cs",
			"Chronicle/KingdomChronicleReceiptRules.RegistryRead.cs",
			"Chronicle/KingdomChronicleReceiptRules.RowParsing.cs",
			"Chronicle/KingdomChronicleReceiptRules.RegistryWrite.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Paths.Length; i++)
				source.Append(TestMain.ReadRepositoryText(Paths[i])).Append('\n');
			return source.ToString();
		}
	}
}
#endif
