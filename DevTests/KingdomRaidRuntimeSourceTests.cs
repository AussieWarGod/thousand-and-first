#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomRaidRuntimeSourceTests
	{
		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static string Slice(string source, string start, string end)
		{
			int at = source.IndexOf(start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(at, 0, start);
			int until = source.IndexOf(end, at + start.Length, StringComparison.Ordinal);
			Assert.Greater(until, at, end);
			return source.Substring(at, until - at);
		}

		[Test]
		public void StandingIsOnlyDiplomaticLeverageAndNeverAProvocationEntrance()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string record = Slice(source, "public static bool RecordProvocation(",
				"public static void OnZoneActivated(");
			string finder = Slice(source, "public static string FindProvokedFaction(",
				"public static bool TryThreat(");
			Assert.IsFalse(record.Contains("GetStanding"));
			Assert.IsFalse(record.Contains("RaidStandingThreshold"));
			Assert.IsFalse(finder.Contains("GetStanding"));
			Assert.IsFalse(finder.Contains("Standings"));
			StringAssert.Contains("SourceConsumed(book.RaidLedger, sourceEventId)", record);
			StringAssert.Contains("KingdomLifecycleAction.RaidWarning", record);
			StringAssert.Contains("KingdomRaidIncidentRules.GrievanceId(sourceEventId)", record);
			StringAssert.Contains("op.Kind = KingdomRules.RaidPlunderDrams", record);
		}

		[Test]
		public void RaiderMustReachTheFrozenObjectBeforeAnyPhysicalDebitOrPlunderProof()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string step = Slice(source, "internal static void StepRaider(",
				"private static bool PublishSimple(");
			string launch = Slice(source, "private static void LaunchRaid(",
				"private static void ResumeOpen(");
			string contact = Slice(source, "private static void ProveObjectiveContact(",
				"private static bool ResolveIncident(");
			StringAssert.Contains("if (distance > 1) return;", step);
			StringAssert.Contains("ProveObjectiveContact", step);
			Assert.IsFalse(launch.Contains("ReserveExactWater"));
			Assert.IsFalse(launch.Contains("PlunderProved ="));
			StringAssert.Contains("string.Equals(op.Origin, targetId", contact);
			StringAssert.Contains("target.CurrentCell.X != x", contact);
			StringAssert.Contains("target.GetIntProperty(\"KingdomStores\") != 1", contact);
			int reserve = contact.IndexOf("ReserveExactWater(amount)", StringComparison.Ordinal);
			int proof = contact.IndexOf("RaidRuntimeAdapter.BeginEffect", StringComparison.Ordinal);
			Assert.Greater(reserve, 0);
			Assert.Greater(proof, reserve);
		}

		[Test]
		public void EntryCellsAreReachableFromTheFrozenObjective()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string ingress = Slice(source, "private static List<Cell> DeterministicEntryCells(",
				"private static void CountProjection(");
			StringAssert.Contains("Queue<Cell>", ingress);
			StringAssert.Contains("objective.X + dx[i]", ingress);
			StringAssert.Contains("IsPassable(null, false)", ingress);
			StringAssert.Contains("reachable[c.X, c.Y]", ingress);
			StringAssert.Contains("c.X == 0 || c.X == zone.Width - 1", ingress);
			Assert.IsFalse(ingress.Contains("zone.GetCell(1, 1)"),
				"an unreachable interior fallback would sever physical contact causality");
		}

		[Test]
		public void PhysicalDebitFailuresRestoreExactlyOrQuarantineRaidAuthority()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string tribute = Slice(source, "public static bool TryTribute(",
				"public static bool TryTalkDown(");
			string contact = Slice(source, "private static void ProveObjectiveContact(",
				"private static bool ResolveIncident(");
			string restore = Slice(source, "private static bool RestoreDebitOrQuarantine(",
				"private static string DisplayFaction(");
			StringAssert.Contains("RestoreDebitOrQuarantine(system, op, debit", tribute);
			StringAssert.Contains("RestoreDebitOrQuarantine(system, op, debit", contact);
			StringAssert.Contains("RaidRuntimeAdapter.BeginEffect", contact);
			StringAssert.Contains("RaidRuntimeAdapter.CommitEffect", contact);
			StringAssert.Contains("proved raid plunder could not advance", contact);
			StringAssert.Contains("debit.Rollback() || debit.RestorationExact", restore);
			StringAssert.Contains("book.Quarantined = true", restore);
			StringAssert.Contains("KingdomLifecycleRules.Quarantine(op, fault)", restore);
		}

		[Test]
		public void RaiderBodiesResolveOnExactLastDeathAndCannotFarmExperience()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string launch = Slice(source, "private static void LaunchRaid(",
				"private static void ResumeOpen(");
			string bodies = Slice(source, "private static void PrepareRaiderBody(",
				"private static bool AllProjectionsProved(");
			string death = Slice(source, "internal static void RaiderDying(",
				"private static bool PublishSimple(");
			string result = Slice(source, "private static bool TryDeriveAttackResult(",
				"private static bool ResolveIncident(");
			string count = Slice(source, "private static int CountLiveRaiders(",
				"private static bool RestoreDebitOrQuarantine(");
			string part = Slice(source, "public sealed class r_KingdomRaiderObjective",
				"public override void TurnTick(");
			StringAssert.Contains("RequirePart<NoXPGain>()", bodies);
			StringAssert.Contains("Allegiance[system.KingdomFactionName] = -100", bodies);
			StringAssert.Contains("CountLiveRaiders(zone, op.Id, actor) == 0", death);
			StringAssert.Contains("SkipEffectWithoutContact", death);
			StringAssert.Contains("last recovery-marked raider died", death);
			StringAssert.Contains("ReconcileRecoveryAtSeat(system, zone, actor)", death);
			StringAssert.Contains("KingdomRaidResolution.RaidersDefeated", result);
			StringAssert.Contains("item.IsAlive", count);
			StringAssert.Contains("BeforeDeathRemovalEvent.ID", part);
			StringAssert.Contains("KingdomRaids.RaiderDying", part);
		}

		[Test]
		public void InterruptedProjectionIntentResumesOnlyFromExactPhysicalEvidence()
		{
			string raids = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string lifecycle = Source(Path.Combine("Experience", "KingdomLifecycleRules.cs"));
			string resume = Slice(raids, "private static bool ResumeAttackProjections(",
				"private static void PrepareRaiderBody(");
			string reset = Slice(lifecycle, "internal static bool ResetAbsentProjectionIntent(",
				"internal static bool BeginEffect(");
			StringAssert.Contains("ids == 0 && markers == 0", resume);
			StringAssert.Contains("ResetAbsentProjectionIntent", resume);
			StringAssert.Contains("ExactRaiderBody", resume);
			StringAssert.Contains("CommitProjection", resume);
			StringAssert.Contains("Quarantine(op", resume);
			StringAssert.Contains("lease.State = KingdomLifecycleLeaseState.Prepared", reset);
			StringAssert.Contains("projection.State = KingdomLifecyclePhysicalState.Prepared", reset);
			StringAssert.Contains("row.Revision != lease.BeforeRevision", reset);
		}

		[Test]
		public void SettledAttackResumesToOneResolutionWhileUnprovedContactQuarantines()
		{
			string raids = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string resume = Slice(raids, "private static void ResumeOpen(",
				"private static KingdomLifecyclePhase NextAfterPrepared(");
			string inspect = Slice(raids, "private static void InspectOpenAttack(",
				"private static void ProveObjectiveContact(");
			StringAssert.Contains("op.EffectState == KingdomLifecyclePhysicalState.Intent", inspect);
			StringAssert.Contains("contact intent survived without an exact debit receipt", inspect);
			StringAssert.Contains("KingdomLifecyclePhysicalState.Proved", inspect);
			StringAssert.Contains("KingdomLifecyclePhysicalState.Skipped", inspect);
			StringAssert.Contains("TryDeriveAttackResult", resume);
			int retire = resume.IndexOf("KingdomLifecycleRules.Retire(book, op, now)",
				StringComparison.Ordinal);
			int resolve = resume.IndexOf("ResolveIncident(system, result, plunder, notice)",
				StringComparison.Ordinal);
			Assert.Greater(retire, 0);
			Assert.Greater(resolve, retire);
		}

		[Test]
		public void FortificationProofReservesExactWorkRowsBodiesPostsAndExclusiveCrews()
		{
			string source = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string freeze = Slice(source, "private static bool FreezeDefence(",
				"private static int RevalidateDefence(");
			string revalidate = Slice(source, "private static int RevalidateDefence(",
				"private static bool TryDefenceResidents(");
			StringAssert.Contains("KingdomCityRules.StableId(work.ID)", freeze);
			StringAssert.Contains("TryExactDefenceCrew", freeze);
			StringAssert.Contains("TryEncodeDefenceReservations", freeze);
			StringAssert.Contains("HashSet<int> reservedCrew", freeze);
			StringAssert.Contains("TryDecodeDefenceReservations", revalidate);
			StringAssert.Contains("SameDefenceReservations", revalidate);
			StringAssert.Contains("DefenceOf(work) != frozen.FrozenScore", revalidate);
			StringAssert.Contains("!SameIds(liveCrew, frozen.CrewSemanticIds)", revalidate);

			string crew = Slice(source, "private static bool TryExactDefenceCrew(",
				"private static bool SameDefenceReservations(");
			StringAssert.Contains("row.JobWorkId != workId", crew);
			StringAssert.Contains("KingdomStations.PostOf(body) != workId", crew);
			StringAssert.Contains("KingdomResidents.TryResolveBoundBody", crew);
			StringAssert.Contains("!ReferenceEquals(exact, body)", crew);
			StringAssert.Contains("!reserved.Add(pair.Key)", crew);
		}

		[Test]
		public void ForcePlanFreezesTierReachAndRosterAndLoaderRefusesUnsafeBodies()
		{
			string raids = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string profiles = Source(Path.Combine("Raids", "KingdomRaidProfiles.cs"));
			string record = Slice(raids, "public static bool RecordProvocation(",
				"public static void OnZoneActivated(");
			string launch = Slice(raids, "private static void LaunchRaid(",
				"private static void ResumeOpen(");
			StringAssert.Contains("KingdomRaidProfiles.FreezePlan", record);
			StringAssert.Contains("TryResolveFrozen", launch);
			StringAssert.Contains("Blueprint(profile, frozenStage", launch);
			Assert.IsFalse(launch.Contains("Blueprint(profile, system.Stage"));
			StringAssert.Contains("Factions.GetIfExists(faction)", profiles);
			StringAssert.Contains("GetBlueprintIfExists(value)", profiles);
			StringAssert.Contains("blueprint.HasProperName()", profiles);
			StringAssert.Contains("blueprint.IsExcludedFromDynamicEncounters()", profiles);
			StringAssert.Contains("blueprint.HasPart(\"Brain\")", profiles);
		}

		[Test]
		public void CreedDeclarationAndWishMintExplicitStableSources()
		{
			string creed = Source(Path.Combine("Core", "KingdomCreed.cs"));
			string wishes = Source(Path.Combine("Debug", "KingdomWishes.cs"));
			string declaration = Slice(creed, "public static bool Declare(",
				"public static void EaseForMeal(");
			string raidWish = Slice(wishes, "public static void RaidWish()",
				"[WishCommand(\"kingdom:reset\"");
			StringAssert.Contains("creed-declaration-slight", declaration);
			StringAssert.Contains("KingdomRaids.RecordProvocation", declaration);
			StringAssert.Contains("debug-test-provocation", raidWish);
			StringAssert.Contains("KingdomRaidIncidentRules.Active", raidWish);
			StringAssert.Contains("remains only rumor: no demand has been delivered and no clock is running", raidWish);
			StringAssert.Contains("incident.ChannelState", raidWish);
			Assert.IsFalse(raidWish.Contains("system.RaidState = 1"));
		}

		[Test]
		public void PlaytestProtocolUsesExplicitGrievanceAndPhysicalContactFlow()
		{
			string testing = Source("TESTING.md");
			string raidPass = Slice(testing, "## Pass 6 — Raids and tribute",
				"## Pass 8 — Homes, work, and the first service");
			StringAssert.Contains("Raw negative standing is not a grievance", raidPass);
			StringAssert.Contains("explicitly mints one snapjaw **test grievance**", raidPass);
			StringAssert.Contains("only rumor", raidPass);
			StringAssert.Contains("no delivered demand or due tick", raidPass);
			StringAssert.Contains("Read and acknowledge", raidPass);
			StringAssert.Contains("Channel loss pauses the clock", raidPass);
			StringAssert.Contains("One natural snapjaw grievance", raidPass);
			StringAssert.Contains("No duplicate grievance or demand object exists", raidPass);
			StringAssert.Contains("Spawn itself takes **no water**", raidPass);
			StringAssert.Contains("until physical adjacency", raidPass);
			StringAssert.Contains("plain base-game quest", raidPass);
			StringAssert.Contains("explicit seat turn-in", raidPass);
			StringAssert.Contains("it remains the same incident/source", raidPass);

			string answerPass = Slice(testing, "## Pass 10 — Names, policy, and answering a threat",
				"## Pass 7 — Trade charters and caravans");
			StringAssert.Contains("The explicit wish—not standing—mints the cause", answerPass);
			StringAssert.Contains("Four answers appear", answerPass);
			StringAssert.Contains("up-to-24-dram store stake", answerPass);
			StringAssert.Contains("same incident becomes confrontation-ready", answerPass);
			Assert.IsFalse(answerPass.Contains("Three exits offered"));
			Assert.IsFalse(answerPass.Contains("demand has grown by half"));
		}

		[Test]
		public void RaidRiskUsesTheLockedTwentyFourDramStakeAndDefenceProof()
		{
			string rules = Source(Path.Combine("Core", "KingdomRules.cs"));
			string raids = Source(Path.Combine("Raids", "KingdomRaids.cs"));
			string testing = Source("TESTING.md");
			StringAssert.Contains("public const int RaidPlunderDrams = 24;", rules);
			StringAssert.Contains("incident.MaximumPlunder, defence, outcome", raids);
			StringAssert.Contains("up-to-24-dram raid stake", testing);
			StringAssert.Contains("up to 24 drams, reduced by proved defence", testing);
		}
	}
}
#endif
