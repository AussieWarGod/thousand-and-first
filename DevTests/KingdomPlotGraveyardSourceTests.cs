#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only; no engine destruction, graveyard behavior or native recovery is executed.
	[TestFixture]
	public sealed class KingdomPlotGraveyardSourceTests
	{
		private const string Loaded = "Growth/KingdomPlot2.32c.LoadedGraveyards.cs";
		private const string Removal = "Growth/KingdomPlot2.32b.FinishRemovalRecovery.cs";
		private const string Identity = "Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs";
		private const string Custody = "Growth/KingdomPlot2.07g.FoundingHeartCustody.cs";
		private const string World = "Harness/KingdomFoundingHeartLifecycleWorld.cs";

		[Test]
		public void SourceContract_LoadedCollectorIncludesZoneAndManagerGraveyardsWithoutDeduplicatingBodies()
		{
			string source = Read(Loaded);
			Ordered(source, "Tombstones = new List<GameObject>()", "var manager = The.ZoneManager",
				"if (manager == null) return false", "try", "HashSet<Zone> zones = new HashSet<Zone>()",
				"if (manager.ActiveZone != null) zones.Add(manager.ActiveZone)", "if (manager.CachedZones != null)",
				"foreach (Zone zone in manager.CachedZones.Values)", "if (zone != null) zones.Add(zone)",
				"HashSet<Graveyard> graveyards = new HashSet<Graveyard>()", "graveyards.Add(manager.Graveyard)",
				"foreach (Zone zone in zones) graveyards.Add(zone.Graveyard)", "foreach (Graveyard graveyard in graveyards)",
				"for (int i = 0; i < graveyard.Objects.Count; i++)", "Tombstones.Add(graveyard.Objects[i])",
				"return ReferenceEquals(manager, The.ZoneManager)");
			Assert.IsFalse(Regex.IsMatch(source, @"HashSet\s*<\s*GameObject\s*>|\.(?:Distinct|ToHashSet)\s*\(|\bTombstones\.Contains\s*\("));
			Assert.AreEqual(1, Regex.Matches(source, @"\bTombstones\.Add\s*\(").Count);
		}

		[Test]
		public void SourceContract_CollectorHasOneAggregateBoundAndReadFailuresNeverRepairOrLoad()
		{
			Contains(Read(Identity), "private const int MaximumFoundingHeartCustodyObjects = 65536");
			string source = Read(Loaded);
			Contains(source, "manager.CachedZones.Count > MaximumFoundingHeartCustodyObjects",
				"zones.Count > MaximumFoundingHeartCustodyObjects");
			Ordered(source, "foreach (Graveyard graveyard in graveyards)",
				"if (graveyard?.Objects == null || graveyard.Objects.Count > MaximumFoundingHeartCustodyObjects - Tombstones.Count) return false",
				"Tombstones.Add(graveyard.Objects[i])", "return ReferenceEquals(manager, The.ZoneManager)",
				"catch", "KingdomLog.Log(\"plot removal: loaded graveyards are unreadable\")", "return false");
			string readers = source + Between(Read(Removal),
				"private static bool ExactGraveyardTombstone(string Id, GameObject Expected, out GameObject Tombstone)",
				"private static bool TryReadGraveyardId(") + Tail(Read(Removal), "private static bool TryReadGraveyardId(");
			Assert.IsFalse(Regex.IsMatch(readers,
				@"\b(?:GetZone|LoadZone|FetchZone|ThawZone|Pool\w*|Remove\w*|Destroy\w*|Obliterate\w*|Clear|Set\w*GameState|Set\w*Property)\s*\("));
			Assert.IsFalse(Regex.IsMatch(readers, @"\b(?:manager|graveyard)\.[\w.]+\s*=(?!=)"));
		}

		[Test]
		public void SourceContract_TombstoneLookupSeparatesExactAbsentAndAmbiguousAndBindsExpectedReference()
		{
			string source = Read(Removal);
			string exact = Between(source,
				"private static bool ExactGraveyardTombstone(string Id, GameObject Expected, out GameObject Tombstone)",
				"private static KingdomPhysicalLookupState FindGraveyardTombstone(");
			Contains(exact, "return FindGraveyardTombstone(Id, out Tombstone) == KingdomPhysicalLookupState.Exact",
				"&& (Expected == null || object.ReferenceEquals(Expected, Tombstone))");
			string lookup = Between(source, "private static KingdomPhysicalLookupState FindGraveyardTombstone(",
				"private static bool TryReadGraveyardId(");
			Ordered(lookup, "Tombstone = null", "string.IsNullOrEmpty(Id) || !TryLoadedPlotTombstones(out List<GameObject> rows)",
				"return KingdomPhysicalLookupState.Ambiguous", "int count = 0", "try", "for (int i = 0; i < rows.Count; i++)",
				"GameObject item = rows[i]", "if (item == null) continue",
				"if (!TryReadGraveyardId(item, out string itemId)) { Tombstone = null; return KingdomPhysicalLookupState.Ambiguous; }",
				"if (itemId == Id) { count++; Tombstone = item; }", "catch", "Tombstone = null",
				"return KingdomPhysicalLookupState.Ambiguous", "if (count == 1) return KingdomPhysicalLookupState.Exact",
				"Tombstone = null", "return count == 0 ? KingdomPhysicalLookupState.Absent : KingdomPhysicalLookupState.Ambiguous");
			Assert.AreEqual(1, Regex.Matches(lookup, @"\bcount\s*\+\+").Count);
			Assert.IsFalse(Regex.IsMatch(lookup, @"HashSet\s*<|\.(?:Distinct|Contains)\s*\("));
			Ordered(Tail(source, "private static bool TryReadGraveyardId("), "Id = null", "if (Item == null) return false",
				"try { Id = Item.IDIfAssigned; return true; }", "catch", "return false");
			Contains(source, "!ExactGraveyardTombstone(Id, Expected, out GameObject tombstone)",
				"tombstone == null || GameObject.Validate(tombstone)", "return ExactGraveyardTombstone(Id, Expected, out _)");
		}

		[Test]
		public void SourceContract_FoundingIdentityAndCustodyShareLoadedGraveyardCoverageAndFailClosed()
		{
			string identity = Read(Identity), custody = Read(Custody);
			Ordered(Between(identity, "private static bool TryFoundingHeartCustodyRoots(", "private static string FoundingHeartRootKey("),
				"if (!TryLoadedPlotTombstones(out List<GameObject> tombstones)) return false",
				"foreach (GameObject item in tombstones)", "if (item != null) { Pending.Add(item); Graveyard.Add(item); }");
			Ordered(Between(custody, "private static int FoundingHeartLoadedReferenceCount(", "private static bool ExactFoundingHeartOwnedRoster("),
				"if (!TryLoadedPlotTombstones(out List<GameObject> tombstones)) return -1", "roots.AddRange(tombstones)",
				"for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)", "HashSet<GameObject> expanded = new HashSet<GameObject>()",
				"if (++visited > MaximumFoundingHeartCustodyObjects) return -1", "if (ReferenceEquals(item, Expected)) count++",
				"if (!expanded.Add(item)) continue", "return count", "catch { return -1; }");
			Ordered(Tail(custody, "private static bool HasFoundingHeartEvidenceInZone("),
				"if (!TryLoadedPlotTombstones(out List<GameObject> tombstones)) return true",
				"foreach (GameObject item in tombstones)", "if (item != null) graveyard.Add(item)");
			Assert.IsFalse(Regex.IsMatch(identity + custody, @"\b(?:The\.ZoneManager|manager)\.Graveyard\b"));
		}

		[Test]
		public void SourceContract_HarnessUsesIndependentExactZoneGraveyardOracleBesideLiveAbsence()
		{
			string source = Read(World);
			Ordered(Between(source, "internal GameObject Completed(", "private static KingdomPhysicalLookupState Lookup("),
				"!GameObject.Validate(predecessor)", "Lookup(terminal.PredecessorId, out _, out _) == KingdomPhysicalLookupState.Absent",
				"ExactTombstone(terminal.PredecessorId, predecessor)");
			string oracle = Between(source, "private bool ExactTombstone(", "private void Absent(");
			Ordered(oracle, "var rows = Zone.Graveyard?.Objects", "rows == null || rows.Count > 65536",
				"predecessor == null || predecessor.IDIfAssigned != id", "return false", "int matches = 0",
				"foreach (GameObject body in rows)", "if (body != null && body.IDIfAssigned == id)",
				"if (!ReferenceEquals(body, predecessor) || GameObject.Validate(body)) return false", "matches++", "return matches == 1");
			Assert.IsFalse(Regex.IsMatch(oracle,
				@"\b(?:TryLoadedPlotTombstones|ExactGraveyardTombstone|FindGlobalFoundingHeartId|GetMethod|Invoke)\s*\(|HashSet\s*<|\.Distinct\s*\("));
			StringAssert.DoesNotContain("The.ZoneManager.Graveyard", oracle);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Compact(string value) { return Regex.Replace(value, @"\s+", ""); }
		private static string Tail(string source, string start) { return Between(source, start, null); }
		private static string Between(string source, string start, string end)
		{
			string compact = Compact(source), marker = Compact(start);
			int first = compact.IndexOf(marker, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, start);
			MatchCollection characters = Regex.Matches(source, @"\S");
			if (end == null) return source.Substring(characters[first].Index);
			int last = compact.IndexOf(Compact(end), first + marker.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, end);
			return source.Substring(characters[first].Index, characters[last].Index - characters[first].Index);
		}
		private static void Contains(string source, params string[] tokens)
		{
			foreach (string token in tokens) StringAssert.Contains(Compact(token), Compact(source), token);
		}
		private static void Ordered(string source, params string[] tokens)
		{
			string compact = Compact(source); int cursor = 0;
			foreach (string token in tokens)
			{
				string expected = Compact(token);
				int at = compact.IndexOf(expected, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, token);
				cursor = at + expected.Length;
			}
		}
	}
}
#endif
