#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomPurposeFamilySourceTests
	{
		[Test]
		public void LogicalFamilyKeepsCatalogueTransportCommitCargoDeliveryAndSiteOrder()
		{
			string source = KingdomPurposeLogicalSource.Read();
			Ordered(source,
				"public const string CargoSchemaProperty",
				"internal static void Dispatch(",
				"internal static void RetryConstruction(",
				"internal static bool TryQuoteCommit(",
				"private static List<KingdomPurposeDefinition> DefinitionsInOrder(",
				"private static bool ExactEndpoints(",
				"private static KingdomPhysicalLookupState FindExactKnown(",
				"private static void SettleDelivery(",
				"private static bool TrySiteProof(",
				"private static bool Fail(");
		}

		[Test]
		public void ConstantsAndStaticCollectionsHaveOneOwner()
		{
			string source = KingdomPurposeLogicalSource.Read();
			Assert.GreaterOrEqual(Count(source, "public static partial class KingdomPurpose"), 12);
			Assert.AreEqual(1, Count(source, "public const int CargoSchema = 1"));
			Assert.AreEqual(1, Count(source,
				"private static readonly Dictionary<string, KingdomPurposeDefinition> Definitions"));
			Assert.AreEqual(1, Count(source,
				"private static readonly HashSet<string> InvalidDefinitions"));
			StringAssert.DoesNotContain("public static class KingdomPurpose\n\t{", source);
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
