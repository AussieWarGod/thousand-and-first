#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomChronicleLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Chronicle/KingdomChronicle.cs",
			"Chronicle/KingdomChronicle.Publication.cs",
			"Chronicle/KingdomChronicle.Telling.cs",
			"Chronicle/KingdomChronicle.Delivery.cs",
			"Chronicle/KingdomChronicle.JournalProjection.cs",
			"Chronicle/KingdomChronicle.Registry.cs"
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
