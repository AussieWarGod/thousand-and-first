#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	// Source contracts only, matching the rest of the Harness/ suite: these two production files
	// key off Zone/GameObject/XRLGame and cannot be compiled into a portable test assembly, so the
	// contract is pinned as text exactly as KingdomFoundingHeartLifecycleSourceTests.cs does for
	// its own Harness sources. Refs #90.
	[TestFixture]
	public sealed class KingdomScenarioTestGroundFactionStripSourceTests
	{
		private const string TestGround = "Harness/KingdomScenarioTestGround.cs";
		private const string FoundingStep = "Harness/KingdomScenarioFoundingStep.cs";
		private const string Core = "Core/KingdomFoundingTransaction.00Core.cs";

		[Test]
		public void SourceContract_StripClearsWorldgenFactionAndVillageBookkeepingZoneProperties()
		{
			string source = Read(TestGround);
			string strip = Between(source,
				"internal static void Strip(Zone Z, out int Removed, out int KeptStairs, out int KeptBare)",
				"private static string Describe(");
			// The clearing must happen after the object-removal loop and before the out
			// parameters are assigned, so BuildZone and Restrip both see it on every call.
			Ordered(strip, "if (gone || !GameObject.Validate(item)) removed++;",
				"Z.RemoveZoneProperty(\"faction\");",
				"Z.RemoveZoneProperty(KingdomFoundingTransaction.SiteReservationVillageProperty);",
				"Z.RemoveZoneProperty(KingdomFoundingTransaction.SiteReservationDisplayProperty);",
				"Removed = removed;");
		}

		[Test]
		public void SourceContract_PreconditionRefusesForeignGroundBeforePublicationWithPinnedMessage()
		{
			string source = Read(FoundingStep);
			Contains(source,
				"internal const string CodeForeignFaction = \"taf-scenario-founding-foreign-ground\";");
			// TryProvePreconditions must reach the foreign-ground check only after the world is
			// proved unfounded, and it must be reached before TryFound ever runs the production
			// transaction - the whole point is refusing before publication, never after.
			Ordered(Between(source, "internal static bool TryProvePreconditions(",
					"internal static bool TryProveGroundNotForeign("),
				"if (!TryProveUnfounded(out detail, out Failure)) return false;",
				"return TryProveGroundNotForeign(Site, out Failure);");
			string check = Between(source, "internal static bool TryProveGroundNotForeign(",
				"internal static bool TryFound(");
			Ordered(check, "KingdomSystem system = Observe();",
				"string kingdomFactionName = system?.KingdomFactionName ?? \"\";",
				"string zoneFaction = Site?.GetZoneProperty(\"faction\");",
				"KingdomRules.GroundIsForeignFaction(zoneFaction, kingdomFactionName)",
				"\"[\" + CodeForeignFaction + \"] this ground already answers to a \"",
				"+ \"foreign faction (\" + zoneFaction + \"); a harness first-city founding \"",
				"+ \"refuses before production publication rather than publish then refuse\"");
			ClassicAssert.AreEqual(1,
				Regex.Matches(source, @"KingdomRules\.GroundIsForeignFaction\s*\(").Count,
				"the harness precondition must call the SAME production predicate exactly once, "
					+ "never duplicate its logic");
		}

		[Test]
		public void SourceContract_VillageBookkeepingPropertiesAreVisibleToTheHarnessAndUnchangedInValue()
		{
			// The strip needs the exact keys a founding attempt's site reservation writes so a
			// re-stripped ground never carries a stale village charter forward. Bumped from
			// private to internal for the harness only - the values and every production guard
			// that reads them are untouched.
			Contains(Read(Core),
				"internal const string SiteReservationVillageProperty = \"r_TAF_FoundingSiteVillage_v1\";",
				"internal const string SiteReservationDisplayProperty = \"r_TAF_FoundingSiteDisplay_v1\";");
		}

		[Test]
		public void SourceContract_BornCleanPromiseDocstringIsPinned()
		{
			string source = Read(TestGround);
			Contains(source,
				"Dev-only born-clean test ground: the scenario's starting zone is generated normally and then",
				"stripped, so a persona starts on flat bare passable ground instead of on whatever worldgen",
				"happened to paint there.");
			// The promise now names what else must be born-clean beyond the object sweep, so a
			// reader (and a future edit) can see the zone-property half of the guarantee without
			// re-deriving it from the Strip body.
			Contains(source,
				"ALSO CLEARS: the zone-level <c>\"faction\"</c> property worldgen paints on a village zone",
				"charter bookkeeping pair a prior founding attempt on this same zone can leave behind");
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }

		private static string Flat(string source) { return Regex.Replace(source, @"\s+", " "); }

		private static void Contains(string source, params string[] terms)
		{
			source = Flat(source);
			foreach (string term in terms) StringAssert.Contains(Flat(term), source);
		}

		private static void Ordered(string source, params string[] terms)
		{
			source = Flat(source);
			int cursor = 0;
			foreach (string term in terms)
			{
				string needle = Flat(term);
				int found = source.IndexOf(needle, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, term);
				cursor = found + needle.Length;
			}
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, end);
			return source.Substring(first, last - first);
		}
	}
}
#endif
