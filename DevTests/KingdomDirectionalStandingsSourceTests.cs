#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomDirectionalStandingsSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		[Test]
		public void EventsUseExactDeltaAndObserveIgnoredPoststatesWithoutSpillover()
		{
			string handler = Source(Path.Combine("Core", "KingdomSystem.z22.Standings.cs"));
			string direction = Source(Path.Combine("Core",
				"KingdomSystem.z22c.DirectionalStandingSpillover.cs"));
			string rules = Source(Path.Combine("Core", "KingdomStandingRules.cs"));
			AssertOrdered(handler,
				"bool automaticWorkAllowed = KingdomMaster.AutomaticWorkAllowed(this);",
				"Guard(\"reputation observation\"",
				"(E.Transient || !automaticWorkAllowed)",
				"The.Game.PlayerReputation.Get(E.Faction) == E.To",
				"TryObservePersonalReputationPoststate(E.Faction.Name, E.To)",
				"if (!automaticWorkAllowed) return base.HandleEvent(E);",
				"if (Founded && !E.Transient",
				"TryApplyPersonalReputationSpillover(");
			StringAssert.DoesNotContain("E.To - E.From", handler);
			StringAssert.DoesNotContain("SpilloverDelta(E.To", handler);
			StringAssert.DoesNotContain("observed == reputationAfter", direction);
			StringAssert.DoesNotContain("return true; // replay", direction);
			StringAssert.DoesNotContain("hadObserved ? observed : reputationBefore", direction);
			StringAssert.Contains("reputationBefore, reputationAfter, Stage", direction);
			string application = Slice(direction,
				"private bool TryApplyPersonalReputationSpillover",
				"private bool TryObservePersonalReputationPoststate");
			StringAssert.DoesNotContain("addObserved", application);
			StringAssert.DoesNotContain(
				"RegardSpilloverObservedReputation.Count >=", application);
			AssertOrdered(application,
				"standings[factionName] = nextStanding",
				"remainders[factionName] = nextRemainder",
				"if (hadObserved || observed.Count <",
				"observed[factionName] = reputationAfter",
				"TryPublishRegardState(standings, remainders, observed)");
			StringAssert.DoesNotContain("\tStandings[factionName] =", application);
			StringAssert.DoesNotContain("\tRegardSpilloverRemainders[factionName] =", application);
			string transient = Slice(direction,
				"private bool TryObservePersonalReputationPoststate",
				"private bool TryPublishRegardState");
			StringAssert.Contains(
				"observed[factionName] = poststate;", transient);
			StringAssert.DoesNotContain("TrySpillover", transient);
			StringAssert.DoesNotContain("Standings[factionName]", transient);
			StringAssert.Contains("(long)reputationAfter - reputationBefore", rules);
			StringAssert.Contains("checked((long)standing * FractionScale + remainder", rules);
			StringAssert.Contains(
				"+ reputationDelta * KingdomRules.SpilloverPercent(stage)", rules);
			StringAssert.Contains("standing = int.MaxValue", rules);
			StringAssert.Contains("standing = int.MinValue", rules);
			StringAssert.Contains("remainder = (int)(scaled % FractionScale)", rules);
		}

		[Test]
		public void NamedDirectionsProjectOnlyTheirExactOppositeEdges()
		{
			string regard = Source(Path.Combine("Core", "KingdomSystem.z22.Standings.cs"));
			string policy = Source(Path.Combine("Core",
				"KingdomSystem.z22a.DirectionalStandings.cs"));
			StringAssert.Contains("public int GetRegardForRealm", regard);
			StringAssert.Contains("public bool TrySetRegardForRealm", policy);
			StringAssert.Contains("public bool TryGetRealmPolicyToward", policy);
			StringAssert.Contains("public bool TrySetRealmPolicyToward", policy);
			StringAssert.Contains("faction.SetFactionFeeling(KingdomFactionName", regard);
			StringAssert.Contains("realm.SetFactionFeeling(factionName", policy);
			StringAssert.Contains("foreach (KeyValuePair<string, int> standing in Standings)", regard);
			StringAssert.Contains(
				"foreach (KeyValuePair<string, int> policy in RealmPolicyToward)", regard);
		}

		[Test]
		public void CleanFoundingPublishesEmptyDirectionalAuthorityWithoutPersonalInheritance()
		{
			string founding = Source(Path.Combine("Core",
				"KingdomFounding.01.FirstPublication.cs"));
			string standingPublication = Source(Path.Combine("Core",
				"KingdomFounding.02.FoundingStandings.cs"));
			string report = Source(Path.Combine("Core", "KingdomReportsPeople.cs"));
			AssertOrdered(founding, "TryPublishFoundingStandings(system, resolvedStandings)",
				"faction.SetProperty(FoundingStepProperty, 2)", "system.ReassertFeelings()");
			AssertOrdered(standingPublication,
				"System.Standings.Count != 0",
				"System.RealmPolicyToward.Count != 0",
				"if (!ExactSubset(System.RegardSpilloverObservedReputation, desired))",
				"System.DirectionalStandingSchemaVersion = 1;",
				"return System.DirectionalStandingSchemaVersion == 1 &&",
				"System.Standings.Count == 0",
				"System.RealmPolicyToward.Count == 0");
			StringAssert.Contains("new List<KeyValuePair<string, int>>()", standingPublication);
			StringAssert.DoesNotContain("Factions.Loop()", standingPublication);
			StringAssert.DoesNotContain("PlayerReputation.Get", standingPublication);
			string publication = Slice(standingPublication,
				"private static bool TryPublishFoundingStandings",
				"private static bool ExactSubset");
			StringAssert.DoesNotContain("System.Standings[row.Key]", publication);
			StringAssert.DoesNotContain("System.RealmPolicyToward[row.Key]", publication);
			StringAssert.Contains("Their regard for us and our policy toward them are separate", report);
			StringAssert.Contains("their regard ", report);
			StringAssert.Contains("our policy ", report);
			Assert.GreaterOrEqual(Count(report, "\"unspecified\""), 2);
		}

		[Test]
		public void ArchiveExileAndReturnRetainDirectionsCarryAndAdvisoryObservation()
		{
			string core = Source(Path.Combine("Core", "KingdomRealmArchive.00Core.cs"));
			string capture = Source(Path.Combine("Core", "KingdomRealmArchive.01Capture.cs"));
			string hash = Source(Path.Combine("Core", "KingdomRealmArchive.02AuthorityHash.cs"));
			string graph = Source(Path.Combine("Core", "KingdomRealmArchive.04GraphMatch.cs"));
			string wire = Source(Path.Combine("Core", "KingdomRealmArchive.10WireEnvelope.cs"));
			string exile = Source(Path.Combine("Core", "KingdomSystem.z10.Exile.Mirrors.cs"));
			string restore = Source(Path.Combine("Core", "KingdomSystem.z18.Return.Restore.cs"));
			StringAssert.Contains("public const int CurrentVersion = 8", core);
			StringAssert.Contains("internal const int DirectionalStandingVersion = 7", core);
			foreach (string name in new[] { "RealmPolicyToward", "RegardSpilloverRemainders",
				"RegardSpilloverObservedReputation" })
			{
				StringAssert.Contains(name + " = CloneStandings", capture);
				StringAssert.Contains("WriteGraphDictionary(writer, " + name + ")", hash);
				StringAssert.Contains(name, graph);
				StringAssert.Contains(name, exile);
				StringAssert.Contains(name, restore);
			}
			AssertOrdered(wire,
				"Writer.Write((IComposite)SettlementTopology);",
				"WriteStandings(Writer, RealmPolicyToward);",
				"WriteStandings(Writer, RegardSpilloverRemainders);",
				"WriteStandings(Writer, RegardSpilloverObservedReputation);");
			StringAssert.Contains("if (wireVersion >= DirectionalStandingVersion)", wire);
			StringAssert.Contains(
				"RealmPolicyToward = new Dictionary<string, int>(StringComparer.Ordinal)", wire);
			StringAssert.Contains("Writer.Write(DirectionalStandingSchemaVersion)", wire);
			StringAssert.Contains("WriteString(Writer, DirectionalStandingDigest, 64)", wire);
			StringAssert.Contains("DirectionalStandingSchemaVersion = Archive.", restore);
		}

		[Test]
		public void LoadNormalizationBoundsAllFourMapsAndReservedDirections()
		{
			string normalize = Source(Path.Combine("Core",
				"KingdomSystem.z24a.DirectionalStandingNormalization.cs"));
			StringAssert.Contains("ValidateDirectionalStandingState(Standings", normalize);
			StringAssert.Contains("ValidateDirectionalStandingState(ExiledStandings", normalize);
			Assert.AreEqual(4, Count(normalize, "KingdomStandingRules.MaxRelationships"));
			Assert.AreEqual(4, Count(normalize,
				"KingdomStandingRules.EligibleForeignFaction"));
			StringAssert.Contains("KingdomStandingRules.CanonicalPairs(regard, remainders)",
				normalize);
			StringAssert.Contains("QuarantineIdentity(failure)", normalize);
			StringAssert.Contains("ValidateDirectionalFactionRegistryAfterLoad", normalize);
			StringAssert.Contains("RelationshipFactionAvailable(key, realmFaction)", normalize);
			string callback = Source(Path.Combine("Core",
				"KingdomSystem.z19.PersistenceAndCallbacks.cs"));
			AssertOrdered(callback, "NormalizeState(AllowLegacyIdentityMigration: false);",
				"MigrateDirectionalStandingStateAfterLoad();",
				"ValidateDirectionalFactionRegistryAfterLoad();");
		}

		[Test]
		public void PublicMutationAdmissionRequiresRegisteredPublishedRealmAuthority()
		{
			string direction = Source(Path.Combine("Core",
				"KingdomSystem.z22a.DirectionalStandings.cs"));
			StringAssert.Contains("Factions.GetIfExists(KingdomFactionName)", direction);
			StringAssert.Contains("realm.GetIntProperty(\"PlayerKingdom\") == 1", direction);
			StringAssert.Contains("KingdomFounding.DirectionalAuthorityPublished(realm)", direction);
			StringAssert.Contains("Factions.GetIfExists(factionName) == null", direction);
			StringAssert.Contains("polity.ProjectedFactionId == factionName", direction);
			string rules = Source(Path.Combine("Core", "KingdomStandingRules.cs"));
			StringAssert.Contains("new UTF8Encoding(false, true)", rules);
			StringAssert.Contains("catch (EncoderFallbackException)", rules);
		}

		private static void AssertOrdered(string source, params string[] needles)
		{
			int at = -1;
			for (int i = 0; i < needles.Length; i++)
			{
				int next = source.IndexOf(needles[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, "missing/out-of-order: " + needles[i]);
				at = next;
			}
		}

		private static int Count(string source, string needle)
		{
			int count = 0;
			for (int at = 0; (at = source.IndexOf(needle, at,
				StringComparison.Ordinal)) >= 0; at += needle.Length) count++;
			return count;
		}

		private static string Slice(string source, string startNeedle, string endNeedle)
		{
			int start = source.IndexOf(startNeedle, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, "missing start: " + startNeedle);
			int end = source.IndexOf(endNeedle, start + startNeedle.Length,
				StringComparison.Ordinal);
			Assert.Greater(end, start, "missing end: " + endNeedle);
			return source.Substring(start, end - start);
		}
	}
}
#endif
