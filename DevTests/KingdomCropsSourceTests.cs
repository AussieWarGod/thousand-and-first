#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomCropsSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsFieldMillSowingRowsDeliveryAndPartsOrder()
		{
			string source = KingdomCropsLogicalSource.Read();
			Ordered(source,
				"public const string RowsTag",
				"public static int CycledFoodPerDay(",
				"public static bool IsMill(",
				"public static void AttemptSow(",
				"public static void Withdraw(",
				"public static int LayRows(",
				"public static List<GameObject> RowsOf(",
				"public static void RecordLarders(",
				"public static int Deposit(",
				"public static GameObject FieldUnder(",
				"public class r_KingdomSeed : IPart",
				"public class r_KingdomWildSeed : IPart");
		}

		[Test]
		public void CropAuthorityAndXmlPartDeclarationsHaveOneOwner()
		{
			string source = KingdomCropsLogicalSource.Read();
			Assert.AreEqual(8, Count(source, "public static partial class KingdomCrops"));
			Assert.AreEqual(1, Count(source, "public class r_KingdomSeed : IPart"));
			Assert.AreEqual(1, Count(source, "public class r_KingdomWildSeed : IPart"));
			Assert.AreEqual(1, Count(source, "public const string WildSeedTakenProperty"));
			StringAssert.DoesNotContain("public static class KingdomCrops", source);
		}

		private static void Ordered(string source, params string[] markers)
		{
			int position = -1;
			for (int i = 0; i < markers.Length; i++)
			{
				int next = source.IndexOf(markers[i], position + 1, StringComparison.Ordinal);
				Assert.Greater(next, position, markers[i]);
				position = next;
			}
		}

		private static int Count(string source, string token)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0;
				at += token.Length) count++;
			return count;
		}
	}
}
#endif
