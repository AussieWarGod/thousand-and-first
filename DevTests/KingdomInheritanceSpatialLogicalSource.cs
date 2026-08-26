#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomInheritanceSpatialLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomInheritanceSpatial.CaptureResult.cs",
			"Core/KingdomInheritanceSpatial.cs",
			"Core/KingdomInheritanceSpatial.Evidence.cs",
			"Core/KingdomInheritanceSpatial.Boundary.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
