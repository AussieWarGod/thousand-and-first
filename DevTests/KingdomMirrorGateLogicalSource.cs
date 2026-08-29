#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomMirrorGateLogicalSource
	{
		private static readonly string[] Paths = new string[]
		{
			"Growth/KingdomMirrorGate.cs",
			"Growth/KingdomMirrorGate.Runtime.cs",
			"Growth/KingdomMirrorGate.Removal.cs",
			"Growth/KingdomMirrorGate.Destination.cs",
			"Growth/KingdomMirrorGate.Purpose.cs",
			"Growth/KingdomMirrorGate.Register.cs"
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
