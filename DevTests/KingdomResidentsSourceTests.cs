#if TAF_TESTS
using System;

using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomResidentsSourceTests
	{
		[Test]
		public void LogicalFamilyKeepsIdentityBindingRosterAndAccessionOrder()
		{
			string source = KingdomResidentsLogicalSource.Read();
			Ordered(source,
				"public const string ResidentIdProperty",
				"internal static List<KingdomResidentRow> RollRows(",
				"internal static bool TryResolveBoundBody(",
				"public static bool Bind(",
				"internal static KingdomCityState ReadRoster(",
				"public static bool TryEnsureRow(",
				"internal static bool TryDepart(",
				"internal static KingdomAccessionOutcome TryAccede(",
				"internal static KingdomAccessionOutcome TryRepairAccession(",
				"private static KingdomResidentRow Witnessed(",
				"private static KingdomBodyPresence PresenceOf(",
				"private static IEnumerable<KingdomCityBook> Books(");
		}

		[Test]
		public void OnePartialAuthorityOwnsEveryDeclarationOnce()
		{
			string source = KingdomResidentsLogicalSource.Read();
			Assert.AreEqual(8, Count(source, "public static partial class KingdomResidents"));
			Assert.AreEqual(1, Count(source, "public const string ResidentIdProperty"));
			Assert.AreEqual(1, Count(source, "internal static bool TryResolveBoundBody("));
			Assert.AreEqual(1, Count(source, "internal static KingdomCityState ReadRoster("));
			StringAssert.DoesNotContain("public static class KingdomResidents", source);
		}

		[Test]
		public void BindingResolutionAndPresenceKeepExactIdentityRules()
		{
			string source = KingdomResidentsLogicalSource.Read();
			StringAssert.Contains("GameObject.FindByID(Binding.ObjectId)", source);
			StringAssert.Contains("Binding.Kind == KingdomBindingKind.Resident", source);
			StringAssert.Contains("Binding.Kind == KingdomBindingKind.Transient", source);
			StringAssert.Contains("FindExactBindingObject(binding)", source);
			StringAssert.Contains("KingdomCitizenshipRemovalReason.Accession", source);
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
