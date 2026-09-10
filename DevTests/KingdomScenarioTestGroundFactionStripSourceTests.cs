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

		[Test]
		public void SourceContract_StripRefusesWholeWhenASiteReservationIsPendingAndTouchesNothing()
		{
			string source = Read(TestGround);
			string strip = Between(source,
				"internal static bool Strip(Zone Z, out int Removed, out int KeptStairs, out int KeptBare,",
				"private static string Describe(");
			Contains(source,
				"internal const string ReservationPendingCode = \"taf-scenario-testground-reservation-pending\";");
			// The reservation check must be the FIRST thing Strip does - before the object loop,
			// before "faction" is ever touched - so a refusal never leaves a half-stripped zone or
			// a half-erased reservation.
			Ordered(strip, "Failure = null;",
				"if (KingdomFoundingTransaction.HasSiteReservation(Z))",
				"Failure = \"[\" + ReservationPendingCode + \"] a founding-attempt site reservation \"",
				"return false;",
				"for (int y = 1; y < Z.Height - 1; y++)",
				"if (gone || !GameObject.Validate(item)) removed++;",
				"Z.RemoveZoneProperty(\"faction\");",
				"Removed = removed;",
				"return true;");
			// The whole reservation family is checked (HasSiteReservation), never a hand-rolled
			// subset of it - that is exactly the partial-erasure bug this guards against.
			ClassicAssert.AreEqual(1,
				Regex.Matches(strip, @"KingdomFoundingTransaction\.HasSiteReservation\s*\(").Count,
				"Strip must gate on the SAME whole-reservation predicate production reads, never "
					+ "duplicate or narrow it");
			StringAssert.DoesNotContain("SiteReservationVillageProperty", strip);
			StringAssert.DoesNotContain("SiteReservationDisplayProperty", strip);
		}

		[Test]
		public void SourceContract_BuildZoneAndRestripPropagateStripsRefusalIntoTheJournalRow()
		{
			string source = Read(TestGround);
			Ordered(Between(source, "public bool BuildZone(Zone Z)", "internal static void Restrip("),
				"bool ok = Strip(Z, out int removed, out int keptStairs, out int keptBare,",
				"out string failure);",
				"KingdomScenarioJournal.Append(BuiltRow, ok,",
				"ok ? Describe(Z, removed, keptStairs, keptBare) : failure);",
				"return ok;");
			Ordered(Between(source, "internal static void Restrip(", "internal static bool Strip("),
				"bool ok = Strip(Z, out int removed, out int keptStairs, out int keptBare,",
				"out string failure);",
				"KingdomScenarioJournal.Append(RestripRow, ok,",
				"ok ? Describe(Z, removed, keptStairs, keptBare) : failure);");
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
		public void SourceContract_BornCleanPromiseDocstringIsPinned()
		{
			string source = Read(TestGround);
			Contains(source,
				"Dev-only born-clean test ground: the scenario's starting zone is generated normally and then",
				"stripped, so a persona starts on flat bare passable ground instead of on whatever worldgen",
				"happened to paint there.");
			// The promise now names both halves of the guarantee: the zone-property clear AND the
			// refusal to ever partially erase a pending reservation, so a reader (and a future
			// edit) can see them without re-deriving either from the Strip body.
			Contains(source,
				"ALSO CLEARS the zone-level <c>\"faction\"</c> property worldgen paints on a village zone",
				"NEVER TOUCHES A PENDING SITE RESERVATION",
				"the strip refuses whole rather",
				"than partially erase");
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
