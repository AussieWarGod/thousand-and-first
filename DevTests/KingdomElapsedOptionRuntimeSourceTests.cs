#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomElapsedOptionRuntimeSourceTests
	{
		private static void AssertBefore(string source, string method, string first,
			string second, string message)
		{
			int start = source.IndexOf(method, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, method);
			int firstAt = source.IndexOf(first, start, StringComparison.Ordinal);
			int secondAt = source.IndexOf(second, start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(firstAt, 0, first);
			Assert.GreaterOrEqual(secondAt, 0, second);
			Assert.Less(firstAt, secondAt, message);
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
			StringAssert.Contains("OptionStatePrefix + settlementId", source);
			StringAssert.Contains("KingdomIdentityRules.IsSettlementId(settlementId)", source);
			StringAssert.Contains("System.MasterAppliedResumeToken", source);
			AssertBefore(source, "public static void Reckon(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)",
				"ObserveOption(System, TimeTicks)", "ScopedSupports(System, Z, Survey)",
				"subsidence option transition must precede support scans and due damage");
			AssertBefore(source, "public static void Reckon(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)",
				"System.LastSubsidenceTick = TimeTicks", "KingdomSubsidenceRules.Slide(",
				"subsidence resume must anchor before slide calculation");
			AssertBefore(source, "public static void Reckon(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)",
				"System.LastSubsidenceTick = TimeTicks", "CommitOption(System, option.Record)",
				"subsidence clock must anchor before its option latch commits");
			StringAssert.Contains("disabling is not an earned arrest, reward, chronicle event, or prompt", source);
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
