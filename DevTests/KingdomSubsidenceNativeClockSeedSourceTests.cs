#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts only; these do not execute engine ownership or fixture publication.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceNativeClockSeedSourceTests
	{
		private const string Path = "Harness/KingdomSubsidenceNativeClockSeed.cs";
		private static string Read() { return TestMain.ReadRepositoryText(Path); }

		[Test]
		public void SourceContract_SeedRequiresTheRetainedExactFixtureAndBoundSurvey()
		{
			Contains(Read(), "KingdomSubsidenceNativeFixture.LastAttempt", "ReferenceEquals(fixture.System, system)",
				"ReferenceEquals(frame.Fixture.Game, frame.Game)", "ReferenceEquals(frame.Fixture.Zone, frame.Zone)",
				"ReferenceEquals(KingdomSurvey.ActiveFor(frame.Zone), frame.Survey)", "FixtureSurvey(frame)",
				"ReferenceEquals(rung.Base, frame.Fixture)", "ReferenceEquals(rung.Survey, frame.Survey)",
				"rung.Now == frame.Now", "system.Population != 50", "system.Stage != GrowthStage.City");
		}

		[Test]
		public void SourceContract_OnlyCommittedStampedFourProviderFixturesMaySeed()
		{
			Contains(Read(), "KingdomScenarioRealizer.TryBindStampedPlan(", "plan.Key != \"founding-first-city\"",
				"plan.AuthorityClass != KingdomScenarioFoundingStep.FoundingAuthority",
				"KingdomScenarioTransactionShape.Committed", "script.Count != 3",
				"script[0] != \"stagedigest\"", "script[2] != \"stagedigest\"",
				"KingdomSubsidenceNativeProvider.Verb", "KingdomSubsidenceRungNativeProvider.ExactScript(script[1])",
				"KingdomScenarioSaveProvider.Verb", "KingdomSubsidenceRungSaveProvider.Verb",
				"KingdomScenarioDurableState.ProvesExactText(receipt, \"intent\")");
		}

		[Test]
		public void SourceContract_ExistingOrPartialAuthorityCannotBeReplaced()
		{
			Contains(Read(), "Attempt != null", "system.City.SubsidenceModel != KingdomSubsidenceStepCodec.FreshWire",
				"KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture)", "book.Sequence == 0",
				"book.Active == null", "book.LastRetiredTick == 0", "book.OptionModel == KingdomSubsidenceStepRules.NoOption",
				"book.BatchModel == KingdomSubsidenceBatchRules.None", "book.FailureModel == KingdomSubsidenceReportArchive.None");
			Ordered(Read(), "Attempt = frame;", "frame.City.SubsidenceModel = wire;", "frame.Strings.Add(frame.Key, option);");
			StringAssert.DoesNotContain("SetStringGameState(", Read());
		}

		[Test]
		public void SourceContract_PureCanonicalRecordsArePreparedBeforeAnyPublication()
		{
			Ordered(Read(), "KingdomSubsidenceStepRules.TryAdmit(", "KingdomSubsidenceStepCodec.TryEncode(admitted, out string wire)",
				"KingdomElapsedOptionRules.Observe(", "KingdomElapsedOptionRecord.Unobserved, true, frame.Token, anchor",
				"KingdomElapsedOptionRules.Encode(decision.Record)", "KingdomElapsedOptionRules.TryDecode(option, out var decoded)",
				"KingdomElapsedOptionRules.Encode(decoded) != option", "Attempt = frame;", "frame.City.SubsidenceModel = wire;");
		}

		[Test]
		public void SourceContract_FiveTablesAndFrozenReferencesProveAbsentThenExactText()
		{
			Contains(Read(), "value.Tables.Any(table => table == null)", "SameReferences(frame.Tables, Tables(frame.Game))",
				"game.HasIntGameState(key)", "game.HasInt64GameState(key)", "game.HasObjectGameState(key)",
				"game.HasBooleanGameState(key)", "expected == null ? !game.HasStringGameState(key)",
				"game.HasStringGameState(key) && game.GetStringGameState(key) == expected",
				"game.StringGameState", "game.IntGameState", "game.Int64GameState", "game.ObjectGameState", "game.BooleanGameState");
		}

		[Test]
		public void SourceContract_EachSeedWriteHasItsOwnExactPostcondition()
		{
			Ordered(Read(), "frame.City.SubsidenceModel = wire;",
				"Exact(frame, wire, null, frame.Clock, frame.Support, frame.Binding)", "frame.Strings.Add(frame.Key, option);",
				"Exact(frame, wire, option, frame.Clock, frame.Support, frame.Binding)", "system.LastSubsidenceTick = anchor;",
				"Exact(frame, wire, option, anchor, frame.Support, frame.Binding)", "system.SupportedLevel = support;",
				"Exact(frame, wire, option, anchor, support, frame.Binding)", "system.SubsidenceBinding = binding;",
				"Exact(frame, wire, option, anchor, support, binding)", "failure = null; return true;");
		}

		[Test]
		public void SourceContract_MeasuredSupportDoesNotSimulateHistoricalPasses()
		{
			Ordered(Read(), "KingdomSubsidence.ScopedSupports(system, zone, survey)",
				"KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.City, system.Shade)",
				"KingdomSubsidenceRules.BindingSupportFor(tally, GrowthStage.City)",
				"Exact(frame, KingdomSubsidenceStepCodec.FreshWire, null, frame.Clock, frame.Support, frame.Binding)", "Attempt = frame;");
			foreach (string forbidden in new[] { ".Reckon(", ".TryReckon(", ".RecordZone(", ".Normalize(",
				".Clear(", ".Remove(", ".Reset(", "KingdomChronicle.Record", "MessageQueue." })
				StringAssert.DoesNotContain(forbidden, Read());
			Assert.IsFalse(Regex.IsMatch(Read(), @"\.(?:TimeTicks|Population|Founded)\s*(?:=(?!=)|\+=|-=|\+\+|--)"));
			Contains(Read(), "Explicit synthetic elapsed checkpoint", "not elapsed ordinary play", "never rolled back or retried");
		}

		[Test]
		public void SourceContract_ReproofPreservesWorldResidentsAndExistingNews()
		{
			Contains(Read(), "long now = game?.TimeTicks ?? -1L", "anchor < 0 || now < anchor", "frame.Game.TimeTicks != frame.Now",
				"system.LastSubsidenceTick < 0 || system.LastSubsidenceTick > now",
				"system.MasterAppliedResumeToken != frame.Token", "system.SubsidenceAnnounced",
				"frame.City.TryReadExact(", "frame.Bindings.TryReadExact(", "city.ResidentCount != 50",
				"row.Standing != KingdomResidentStanding.Resident", "KingdomBindingKind.Resident",
				"resident.ObjectId != frame.BodyIds[i]", "resident.ZoneId != frame.Zone.ZoneID",
				"SameReferences(frame.Lists, Lists(system))", "frame.Counts.SequenceEqual(Counts(frame.Ledger))",
				"frame.Lists[i].SequenceEqual(frame.Text[i], StringComparer.Ordinal)");
		}

		private static void Contains(string source, params string[] values)
		{
			foreach (string value in values) StringAssert.Contains(value, source);
		}
		private static void Ordered(string source, params string[] values)
		{
			int position = 0;
			foreach (string value in values)
			{
				int found = source.IndexOf(value, position, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, value); position = found + value.Length;
			}
		}
	}
}
#endif
