#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomElapsedOptionRuntimeSourceTests
	{
		private static void AssertBefore(string source, string method, string first,
			string second, string message)
		{
			int start = source.IndexOf(method, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, method);
			int firstAt = source.IndexOf(first, start, StringComparison.Ordinal);
			int secondAt = source.IndexOf(second, start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(firstAt, 0, first);
			ClassicAssert.GreaterOrEqual(secondAt, 0, second);
			ClassicAssert.Less(firstAt, secondAt, message);
		}

		[Test]
		public void RoadOptionObservesAndAnchorsBeforeElapsedWear()
		{
			string source = KingdomRoadsLogicalSource.Read();
			StringAssert.Contains("public const string OptionStateProperty = \"r_TAF_RoadsOption_v1\"", source);
			StringAssert.Contains("public const string OptionOwnerProperty = \"r_TAF_RoadsOptionOwner_v1\"", source);
			StringAssert.Contains("public const string GlobalOptionStatePrefix = \"r_TAF_RoadsGlobalOption_v1:\"", source);
			StringAssert.Contains("KingdomIdentityRules.IsSettlementId(settlementId)", source);
			AssertBefore(source, "private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,",
				"SetZoneProperty(OptionStateProperty, current)",
				"SetZoneProperty(OptionOwnerProperty, settlementId)",
				"road option owner must publish last so interruption fails closed");
			StringAssert.Contains("System.MasterAppliedResumeToken", source);
			AssertBefore(source, "public static void OnSettlementPass(KingdomSystem System, Zone Z)",
				"ObserveOption(System, Z, timeTicks)", "ReadTick(Z, WalkedProperty)",
				"road option transition must precede elapsed road billing");
			AssertBefore(source, "public static void OnSettlementPass(KingdomSystem System, Zone Z)",
				"WriteTick(Z, WalkedProperty, timeTicks)", "KingdomRules.ElapsedDays",
				"road resume must anchor before elapsed road billing");
			AssertBefore(source, "public static void OnSettlementPass(KingdomSystem System, Zone Z)",
				"WriteTick(Z, WalkedProperty, timeTicks)", "CommitOption(System, Z, option.Record)",
				"road clock must anchor before its local option latch commits");
		}

		[Test]
		public void SubsidenceUsesSettlementIdentityAndObservesBeforeSurveyWork()
		{
			string source = KingdomSubsidenceLogicalSource.Read();
			StringAssert.DoesNotContain("private static KingdomElapsedOptionDecision ObserveOption(", source);
			StringAssert.DoesNotContain("private static void CommitOption(", source);
			AssertBefore(source, "internal static bool TryReckon(",
				"KingdomSubsidenceStepRuntime.TryOption(system, Enabled, now,", "ScopedSupports(system, zone, survey)",
				"subsidence option transition must precede support scans and due damage");
			string runtime = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceStepRuntime.Options.cs");
			AssertBefore(runtime, "private static bool TryOptionCore(", "TryFreezeOption(",
				"KingdomSubsidenceOptionRuntime.TryPublish(", "durable intent precedes option publication");
			AssertBefore(runtime, "private static bool TryOptionCore(", "system.LastSubsidenceTick = checkpoint",
				"KingdomSubsidenceStepRules.TryFinishOption(", "intent clears only after exact checkpoint publication");
		}

		[Test]
		public void FaithObservesBeforeBuildingPassAndCancelsOnlyUnpaidShrinePressure()
		{
			string source = KingdomFaithLogicalSource.Read();
			StringAssert.Contains("public const string OptionStateProperty = \"r_TAF_FaithOption_v1\"", source);
			StringAssert.Contains("public const string OptionOwnerProperty = \"r_TAF_FaithOptionOwner_v1\"", source);
			StringAssert.Contains("public const string GlobalOptionStatePrefix = \"r_TAF_FaithGlobalOption_v1:\"", source);
			StringAssert.Contains("KingdomIdentityRules.IsSettlementId(settlementId)", source);
			AssertBefore(source, "private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,",
				"SetZoneProperty(OptionStateProperty, current)",
				"SetZoneProperty(OptionOwnerProperty, settlementId)",
				"faith option owner must publish last so interruption fails closed");
			StringAssert.Contains("public const string ShrineWindowAnchorProperty", source);
			StringAssert.Contains("public const string ShrineDisabledActiveProperty", source);
			StringAssert.Contains("System.MasterAppliedResumeToken", source);
			AssertBefore(source, "public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)",
				"ObserveOption(System, Z, now)", "new HashSet<GameObject>()",
				"faith option transition must precede shrine scans and conversion work");
			StringAssert.Contains("brink.Channel == (int)ConversionChannel.Shrine", source);
			StringAssert.Contains("KingdomBrink.Lift(settler, BrinkKind.Creed)", source);
			StringAssert.Contains("ResumeCanceledFaith(Survey, now)", source);
			AssertBefore(source, "public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)",
				"CancelUncommittedFaith(Survey)", "CommitOption(System, Z, option.Record)",
				"faith cancellation must finish before its local option latch commits");
			StringAssert.Contains("KingdomFaithRules.EffectiveWindowStart", source);
			StringAssert.DoesNotContain("SetStringProperty(ShrineCreedProperty, null)", source);
		}

		[Test]
		public void BountyManningObservesRealmEpochBeforeMasterGuardAndServiceAccrual()
		{
			string events = TestMain.ReadRepositoryText("Core/KingdomSystem.z20.Events.cs");
			AssertBefore(events, "public override bool HandleEvent(EndTurnEvent E)",
				"KingdomBounty.ObserveManningGlobalOption(this, game.TimeTicks)",
				"KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks)",
				"bounty option changes must remain observable while master work is disabled");
			string bounty = KingdomBountyLogicalSource.Read();
			StringAssert.Contains("ManningGlobalOptionPrefix", bounty);
			StringAssert.Contains("System.MasterAppliedResumeToken", bounty);
			AssertBefore(bounty, "internal static ManningPass PrepareManningPass",
				"ObserveManningOption(System, now)",
				"KingdomBountyManningRules.TryAccrue",
				"option transition must reanchor before serviced time can advance");
			StringAssert.Contains("current.ObservedTick == Now", bounty);
			StringAssert.Contains("Data.ManningCheckpointTick = Now", bounty);
		}
	}
}
#endif
