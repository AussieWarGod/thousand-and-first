#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts only. These tests do not execute Qud or establish native refusal.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceNativeClockChecksSourceTests
	{
		private const string Path = "Harness/KingdomSubsidenceNativeClockChecks.cs";
		private static string Source() { return TestMain.ReadRepositoryText(Path); }

		[Test]
		public void SourceContract_FourNegativeCallsUseTheActualReckoningEntry()
		{
			string source = Source();
			Ordered(Body(source, "internal static void Verify("),
				"now > 0 && now < long.MaxValue", "Owner(fixture, zone, survey, game, now);",
				"book.Admission == KingdomSubsidenceAdmission.Admitted", "book.Sequence == 0 && book.Active == null",
				"row.ObservedTick == original", "now, now - 1L, null, original, capture)",
				"now, now + 1L, null, original, capture)", "now, now, -1L, original, capture)",
				"now, now, now + 1L, original, capture)");
			Assert.AreEqual(4, Regex.Matches(source, @"\bProbe\(fixture,").Count);
			StringAssert.Contains("KingdomSubsidence.TryReckon(system, zone, survey, supplied, out string refusal)", source);
			StringAssert.DoesNotContain("KingdomSubsidence.Reckon(", source);
		}

		[Test]
		public void SourceContract_OnlyInjectedClockRestoresAfterEveryProofAndRefusal()
		{
			string source = Source(), probe = Body(source, "private static void Probe(");
			Ordered(probe, "Owner(fixture, zone, survey, game, now); capture.Check();",
				"system.LastSubsidenceTick == original", "if (injected.HasValue) system.LastSubsidenceTick = injected.Value;",
				"KingdomSubsidence.TryReckon(", "Owner(fixture, zone, survey, game, now); capture.Check();",
				"system.LastSubsidenceTick == (injected ?? original)", "!accepted && !string.IsNullOrEmpty(refusal)",
				"if (injected.HasValue) system.LastSubsidenceTick = original;",
				"Owner(fixture, zone, survey, game, now); capture.Check();", "system.LastSubsidenceTick == original");
			Assert.AreEqual(2, Regex.Matches(source, @"\.LastSubsidenceTick\s*=(?!=)").Count);
			Assert.IsFalse(Regex.IsMatch(source, @"\bfinally\s*\{"));
			Assert.IsFalse(Regex.IsMatch(source, @"\.(?:TimeTicks|Population|Stage|SubsidenceModel)\s*=(?!=)"));
			StringAssert.DoesNotContain(".SetValue(", source);
		}

		[Test]
		public void SourceContract_HealthyBaselineProvesActualPassAndRunAuthorityWithoutPublication()
		{
			string source = Source(), healthy = Body(source, "private static void Healthy(");
			Contains(healthy, "KingdomSubsidenceStepRuntime.TryPassGuard(system, zone, survey",
				"exact != null && sameSeat != null && exact() && sameSeat()",
				"KingdomSubsidenceOptionRuntime.TryObserve(KingdomSubsidence.Enabled, now",
				"ReferenceEquals(observed.Game, The.Game)", "ReferenceEquals(observed.System, system)",
				"ReferenceEquals(observed.City, system.City)", "observed.Snapshot.Decision.Valid",
				"observed.Snapshot.Decision.Action == KingdomElapsedOptionAction.Run");
			Ordered(Body(source, "private static void Probe("), "system.LastSubsidenceTick == original",
				"Healthy(system, zone, survey, now); Owner(fixture, zone, survey, game, now); capture.Check();",
				"system.LastSubsidenceTick = injected.Value;", "KingdomSubsidence.TryReckon(",
				"!accepted && !string.IsNullOrEmpty(refusal)", "system.LastSubsidenceTick = original;",
				"system.LastSubsidenceTick == original",
				"Healthy(system, zone, survey, now); Owner(fixture, zone, survey, game, now); capture.Check();");
			StringAssert.DoesNotContain("TryPublish(", source);
			StringAssert.DoesNotContain("TryBeforePass(", source);
		}

		[Test]
		public void SourceContract_OwnershipRequiresRetainedSeedAndStampedCommittedProvider()
		{
			string owner = Body(Source(), "private static void Owner(");
			Contains(owner, "ReferenceEquals(The.Game, game)", "game.TimeTicks == now",
				"ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, fixture)",
				"ReferenceEquals(game.GetSystem<KingdomSystem>(), fixture.System)",
				"ReferenceEquals(KingdomSurvey.ActiveFor(zone), survey)", "ReferenceEquals(rung.Base, fixture)",
				"typeof(KingdomSubsidenceNativeClockSeed).GetField(\"Attempt\"", "seed owner ",
				"TryBindStampedPlan(", "plan.Key == \"founding-first-city\"", "KingdomScenarioFoundingStep.FoundingAuthority",
				"KingdomScenarioTransactionShape.Committed", "script.Count == 3", "script[0] == \"stagedigest\"",
				"script[2] == \"stagedigest\"", "ProvesExactText(receipts[i], \"intent\")", "foreign provider receipt",
				"Require(matches == 1");
		}

		[Test]
		public void SourceContract_CapturedScopeIncludesCityZoneSightingsAndEmptyDepartureAuthority()
		{
			string source = Source();
			Contains(source, "Not a proof of arbitrary world effects", "PriorCook == null", "PriorOffice == null", "PriorPolity == null",
				"Population Stage SupportedLevel SubsidenceBinding SubsidenceAnnounced", "City Bindings Ledger ResidentDeparture",
				"ChronicleEntries OutsiderEntries ClaimedZones", "ZoneLastReadTicks", "ZoneWaterLevels", "ZoneRoofs",
				"SubsidenceModel SubsidenceReadFailed", "Departures Notes BrinkLines ExpeditionLines",
				"AuthorizationEventId AuthorizationOwnerObjectId AuthorizationCauseDigest", "field.Name != \"DistanceCache\"");
			foreach (string forbidden in new[] { ".Normalize(", ".City.TryRead(", ".Bindings.TryRead(", "SerializationWriter", "SerializationReader",
				".GetProperty(", ".GetProperties(", ".GetDisplayName(", ".TryRetire(", ".WriteGameObject(" })
				StringAssert.DoesNotContain(forbidden, source);
		}

		[Test]
		public void SourceContract_BodyGraphUsesRawFieldsAndClosedBoundedSchemas()
		{
			string source = Source();
			Contains(source, "Items Size Length Variant", "_ParentObject _CurrentCell _InInventory _Equipped",
				"X Y ParentZone Objects", "ReferenceEquals(item, body)", "Require(copies == 1", "Require(receipts == 1",
				"Flags LeaderReference Allegiance", "SourceID Previous Reason Flags Buckets Slots",
				"schema.Contains(\" \" + field.Name + \" \")", "record field count ", "unknown record type ",
				"unknown raw value type", "count <= 4096", "++depth <= 16", "BindingFlags.DeclaredOnly",
				"Settlers CitizenBodies Objects LoadedObjects Works Built Defences");
			StringAssert.DoesNotContain(".PartyLeader", source);
			StringAssert.DoesNotContain(".CurrentCell", source);
		}

		[Test]
		public void SourceContract_AllFiveKeyTablesKeepPresenceValuesAndReferences()
		{
			string source = Source(), keys = Body(source, "private static void Keys(");
			Contains(source, "StringGameState IntGameState Int64GameState BooleanGameState ObjectGameState",
				"r_TAF_ChronicleEventRegistry_v1", "KingdomScenarioProvenanceRules.ProvenanceState",
				"KingdomScenarioTransactionMarker.TransactionState");
			foreach (string table in new[] { "String", "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("game." + table + "GameState", keys);
			Contains(keys, "bool present = table.Contains(key)", "present ? table[key] : null",
				"unknown key value type", "table.Contains(key) == present", "Same(table[key], value)");
		}

		private static string Body(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.IsTrue(start >= 0, signature); start = source.IndexOf('{', start);
			int depth = 0;
			for (int i = start; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed source body: " + signature); return null;
		}
		private static void Ordered(string source, params string[] expected)
		{
			int at = 0;
			foreach (string token in expected)
			{
				int next = source.IndexOf(token, at, StringComparison.Ordinal);
				Assert.IsTrue(next >= at, token); at = next + token.Length;
			}
		}
		private static void Contains(string source, params string[] expected)
		{
			foreach (string token in expected) StringAssert.Contains(token, source);
		}
	}
}
#endif
