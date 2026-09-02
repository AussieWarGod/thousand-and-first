#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityDeathReplaySourceTests
	{
		[Test]
		public void IntentWritePrecedesEveryFrozenCustodyRelease()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			AssertBefore(death, "TryWriteDeathIntent(zone, intent",
				"TryReleaseFrozenCustody(ledger, RealmId");
			StringAssert.Contains("Intent = intent", death);
			StringAssert.Contains("TryReadDeathIntent(zone", death);
		}

		[Test]
		public void OnDestroyWritesWitnessOnlyAndLoadedReplayCommitsAfterAbsence()
		{
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			StringAssert.Contains("TryCommitVisibleDeathWitness(", body);
			StringAssert.DoesNotContain("TryResolveCommittedDeathIntent(", body);
			// Scoped to the absent-body arm: the live-body arm above it clears the intent
			// without any consequence, so an unscoped search finds that clear first.
			AssertBefore(Between(replay, "if (!witnessed) return FailPhysical(", "return true;"),
				"TryCommitDeathIntentConsequence(System, intent",
				"TryClearDeathIntent(zone, intent");
			StringAssert.Contains("HasRemovalWitness(zone", replay);
		}

		[Test]
		public void CallbackRetainsReplayAuthorityAndNeverPoisonsCorpseSuppression()
		{
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			StringAssert.Contains("intent retained", body);
			StringAssert.DoesNotContain("TryClearVisibleDeathIntent", body);
			StringAssert.Contains("return false", body);
			StringAssert.DoesNotContain("PendingKiller", body);
			StringAssert.DoesNotContain("SetIntProperty(\"SuppressCorpseDrops\", 0)", body);
			StringAssert.DoesNotContain("E.Obliterate", body);
		}

		[Test]
		public void ExactClearPreservesMalformedForeignAndWrongTypedSlots()
		{
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			AssertBefore(replay, "!exactType || !string.Equals(actual, expected",
				"Zone.RemoveZoneProperty(key)");
			StringAssert.Contains("Dictionary<string, object> properties", replay);
			StringAssert.Contains("ExactString = raw is string", replay);
			StringAssert.Contains("AmbiguousDeathIntentFailure", replay);
		}

		[Test]
		public void LegacyWitnessWithoutIntentNeverSynthesizesConclusion()
		{
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			StringAssert.Contains("state == KingdomPolityDeathIntentState.Clear) continue", replay);
			int witness = scheduler.IndexOf("internal static void WitnessDeath", StringComparison.Ordinal);
			int retire = scheduler.IndexOf("private static bool RetireOld", witness,
				StringComparison.Ordinal);
			string method = scheduler.Substring(witness, retire - witness);
			StringAssert.Contains("TryReplayDeathIntents", method);
			StringAssert.DoesNotContain("TryConcludeEndpointCohort", method);
		}

		[Test]
		public void RecoveryReplaysBeforeWithdrawalConclusionAndReturn()
		{
			string capacity = Read("Polity/KingdomPolityExperienceRuntime.Recovery.cs");
			string visit = Read("Polity/KingdomPolityVisitRuntime.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			string withdrawal = Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs");
			AssertBefore(capacity, "TryReplayDeathIntents(",
				"KingdomPolityExperienceRecoveryRules.Decide");
			AssertBefore(visit, "ReplayLoadedDeathIntents(System, plan",
				"RecoverIncidentConclusions(System.PolityLedger");
			AssertBefore(scheduler, "TryReplayDeathIntents(S,",
				"TryConcludeScheduledStay(S.PolityLedger");
			AssertBefore(withdrawal, "TryReplayDeathIntents(System, CohortId",
				"TryConcludeEndpointCohort(ledger");
			AssertBefore(withdrawal, "TryProveMaterializedLifecycleAfterDeathReplay",
				"TryConcludeEndpointCohort(ledger");
			AssertBefore(scheduler, "TryProveMaterializedLifecycleAfterDeathReplay(",
				"TryConcludeScheduledStay(L, L.Revision");
		}

		[Test]
		public void ClashLivenessRelaxesOnlyForExactDeathWitness()
		{
			string clash = Read("Polity/KingdomPolityEndpointRuntime.Clash.cs");
			StringAssert.Contains("KingdomPolityPhysicalCustodyRules.DeathRemovalKind", clash);
			StringAssert.Contains("KingdomPolityCohortRules.PreparedObjectId(cohort, j)", clash);
			StringAssert.Contains("if (!HasRemovalWitness(zone", clash);
			StringAssert.Contains("physically incomplete", clash);
		}

		[Test]
		public void AttributionIsFrozenBeforeWriteAndNeverRereadDuringReplay()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string harm = Read("Polity/KingdomPolityVisitInteraction.Harm.cs");
			AssertBefore(death, "Killer.IsPlayer()", "TryWriteDeathIntent(zone, intent");
			StringAssert.Contains("Body.CurrentCell.IsVisible() && Body.IsVisible()", death);
			StringAssert.Contains("Intent.Attribution", harm);
			int replay = harm.IndexOf("internal static bool TryReplayEnvoyDeath",
				StringComparison.Ordinal);
			string replayBody = harm.Substring(replay);
			StringAssert.DoesNotContain("Killer.IsPlayer", replayBody);
			StringAssert.DoesNotContain("CurrentCell.IsVisible", replayBody);
		}

		[Test]
		public void ExactLoadedOffscreenDeathUsesPhysicalBindingAndAbandonment()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			StringAssert.Contains("ExactPhysicalDeathBinding", death);
			StringAssert.Contains("KingdomPolityDeathVisibility.PhysicalOnly", death);
			StringAssert.Contains("TryAbandonEndpointCohort", replay);
			StringAssert.Contains("ExactDeathRemovalWitness: true", replay);
		}

		[Test]
		public void AbandonedCleanupPreservesTerminalAndNeverReturnsEnvoyRoute()
		{
			string rules = Read("Polity/KingdomPolityCohortRules.Manifestation.cs");
			string endpoints = Read("Polity/KingdomPolityVisitRuntime.Endpoints.cs");
			StringAssert.Contains("cleaned.Phase != KingdomPolityCohortPhase.Abandoned", rules);
			int abandoned = endpoints.IndexOf(
				"cohort.Phase == KingdomPolityCohortPhase.Abandoned) return true",
				StringComparison.Ordinal);
			int returned = endpoints.IndexOf("return ReconcileReturn(ledger, P, Manifest",
				abandoned, StringComparison.Ordinal);
			Assert.GreaterOrEqual(abandoned, 0); Assert.Greater(returned, abandoned);
		}

		[Test]
		public void ConcludedReplayRequiresExactIncidentRebinding()
		{
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			string conflict = Read("Polity/KingdomPolityVisitInteraction.Conflict.cs");
			StringAssert.Contains("ExactConcludedAuthority", replay);
			StringAssert.Contains("lacks the exact intended incident consequence", replay);
			StringAssert.Contains("cohort.RewardEventId != conclusionId", conflict);
			string visit = Read("Polity/KingdomPolityVisitInteraction.cs");
			StringAssert.Contains("clash.Conclusion == null || cohort.RewardEventId !=", visit);
			AssertBefore(visit, "clash.Conclusion == null || cohort.RewardEventId !=",
				"TryConcludeCurrentEndpointClash(System");
			StringAssert.Contains("TryConcludeParticipants(System, clash, out Failure)", visit);
		}

		[Test]
		public void IncidentIsFrozenBeforeReleaseAndReplayNeverUsesFirstMatchLookup()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string incident = Read("Polity/KingdomPolityEndpointRuntime.DeathIncident.cs");
			string incidentRules = Read("Polity/KingdomPolityDeathIncidentRules.cs");
			string envoy = Read("Polity/KingdomPolityVisitInteraction.Harm.cs");
			string warband = Read("Polity/KingdomPolityVisitInteraction.cs");
			AssertBefore(death, "TryFreezeDeathIncident", "TryWriteDeathIntent(zone, intent");
			StringAssert.Contains("multiple open incident authorities", incidentRules);
			StringAssert.Contains("Intent.IncidentPlanId", incident);
			StringAssert.DoesNotContain("TermsFor(ledger, cohort.CohortId)", envoy);
			string replay = warband.Substring(warband.IndexOf(
				"internal static bool TryReplayWarbandDeath", StringComparison.Ordinal));
			StringAssert.DoesNotContain("ClashFor(ledger, cohort.CohortId)", replay);
		}

		[Test]
		public void LiveBodyAfterOnDestroyCutCancelsOnlyBodyWitnessThenIntent()
		{
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			// Anchor inside the replay loop: "if (present)" also names the slot-collision guard in
			// TryWriteDeathIntent, and starting there would swallow the whole file.
			int loop = replay.IndexOf("internal static bool TryReplayDeathIntents",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(loop, 0, "TryReplayDeathIntents");
			int present = replay.IndexOf("if (present)", loop, StringComparison.Ordinal);
			int absent = replay.IndexOf("if (!witnessed)", present, StringComparison.Ordinal);
			Assert.Greater(absent, present, "if (!witnessed)");
			string branch = replay.Substring(present, absent - present);
			AssertBefore(branch, "TryBuildCustodyPlan", "TryClearRemovalWitness");
			AssertBefore(branch, "TryClearRemovalWitness", "TryClearDeathIntent");
			StringAssert.Contains("Gear: false", branch);
			StringAssert.DoesNotContain("TryCommitDeathIntentConsequence", branch);
		}

		[Test]
		public void CleanupBypassesVetoOnlyWithExactPreparedTerminalWitness()
		{
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string custody = Read("Polity/KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			AssertBefore(body, "TryAuthorizePreparedCleanup", "PendingDeath == null");
			AssertBefore(custody, "part.ArmCleanup", "Body.Obliterate");
			StringAssert.Contains("part.ClearCleanup", custody);
			StringAssert.Contains("ExactPreparedBody", death);
			StringAssert.Contains("KingdomPolityCohortPhase.Concluded", death);
			StringAssert.Contains("KingdomPolityCohortPhase.Abandoned", death);
			StringAssert.Contains("return false", body);
		}

		[Test]
		public void DeathWitnessIsReservedBeforeCancellableDestroyAndOnDestroyOnlyConfirms()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			string prepare = death.Substring(death.IndexOf("TryPrepareVisibleDeath",
				StringComparison.Ordinal), death.IndexOf("TryReproveVisibleDeath",
				StringComparison.Ordinal) - death.IndexOf("TryPrepareVisibleDeath",
				StringComparison.Ordinal));
			AssertBefore(prepare, "TryWriteDeathIntent(zone, intent", "TryWriteRemovalWitness(");
			AssertBefore(prepare, "TryWriteRemovalWitness(", "TryReleaseFrozenCustody");
			StringAssert.Contains("DeathCallbackInFlight = true", body);
			StringAssert.Contains("IsDeathCallbackInFlight", body);
			StringAssert.DoesNotContain("TryResolveCommittedDeathIntent", body);
		}

		[Test]
		public void LegacyV1RebindsOrRefusesBeforeV2Rewrite()
		{
			string rules = Read("Polity/KingdomPolityDeathIntentRules.cs");
			string replay = Read("Polity/KingdomPolityEndpointRuntime.DeathIntent.cs");
			StringAssert.Contains("LegacyWirePrefix", rules);
			AssertBefore(replay, "KingdomPolityDeathIncidentRules.TryFreeze",
				"TryRewriteLegacyDeathIntent");
			StringAssert.Contains("actual != LegacyWire", Read(
				"Polity/KingdomPolityEndpointRuntime.DeathLegacy.cs"));
		}

		[Test]
		public void RawBodyWitnessAndLocalCandidateAreReprovedAtEveryCancellableCut()
		{
			string death = Read("Polity/KingdomPolityEndpointRuntime.Death.cs");
			string witness = Read("Polity/KingdomPolityEndpointRuntime.RemovalWitness.cs");
			StringAssert.Contains("Dictionary<string, object>", witness);
			StringAssert.Contains("ExactString = raw is string", witness);
			StringAssert.Contains("HasRemovalWitness(zone", death);
			AssertBefore(death, "DeathWitness candidate", "TryWriteDeathIntent(zone, intent");
			AssertBefore(death, "ReproveVisibleDeath(candidate", "Witness = candidate");
		}

		[Test]
		public void PreparedCleanupIntentPrecedesRemovalAndCanPromoteAfterAbsence()
		{
			string transfer = Read("Polity/KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			string witness = Read("Polity/KingdomPolityEndpointRuntime.RemovalWitness.cs");
			AssertBefore(transfer, "TryWriteCleanupIntent", "part.ArmCleanup");
			AssertBefore(transfer, "part.ArmCleanup", "Body.Obliterate");
			StringAssert.Contains("TryPromotePreparedCleanupIntents", transfer);
			StringAssert.Contains("PreparedCleanupIntent", witness);
			AssertBefore(transfer, "TryWriteRemovalWitness(Cell", "TryClearCleanupIntent");
		}

		[Test]
		public void ResidentIndexAndQuarantineFailuresAreTypedAndFailClosed()
		{
			string locator = Read("Polity/KingdomPolityEndpointRuntime.Locator.cs");
			string gear = Read("Polity/KingdomPolityNpcRuntime.Gear.cs");
			StringAssert.Contains("TryFindResidentObject", locator);
			StringAssert.Contains("resident object lookup failed", locator);
			StringAssert.DoesNotContain("catch (Exception) { return null; }", locator);
			StringAssert.Contains("TryMarkContestedPreparedBody", locator);
			StringAssert.Contains("did not survive exact writeback", locator);
			StringAssert.Contains("TryFindResidentGear", gear);
			StringAssert.Contains("resident gear lookup failed", gear);
			StringAssert.DoesNotContain("catch (Exception) { return null; }", gear);
		}

		[Test]
		public void CleanupTokenFreezesAndRawReprovesExactIntentAtAuthorizationReturn()
		{
			string authorize = Method(Read("Polity/KingdomPolityEndpointRuntime.Death.cs"),
				"internal static bool TryAuthorizePreparedCleanup");
			string arm = Method(Read("Polity/r_KingdomPolityCohortBody.cs"),
				"internal void ArmCleanup");
			string beforeDestroy = Method(Read("Polity/r_KingdomPolityCohortBody.cs"),
				"public override bool HandleEvent(BeforeDestroyObjectEvent E)");
			StringAssert.Contains("TokenIntentKey", authorize);
			StringAssert.Contains("TokenIntentValue", authorize);
			StringAssert.Contains("TryProveExactCleanupIntent", authorize);
			StringAssert.Contains("KingdomPolityCleanupEvidenceProof.Exact", authorize);
			AssertBefore(arm, "CleanupIntentKey = IntentKey", "CleanupIntentValue = IntentValue");
			AssertBefore(beforeDestroy, "TryAuthorizePreparedCleanup", "ClearCleanup(); return");
		}

		[Test]
		public void PreparedAbsenceUsesTotalEvidenceAndExactFinalAftermath()
		{
			string transfer = Read("Polity/KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			string promote = Method(transfer, "private static bool TryPromotePreparedCleanupIntents");
			string withdrawal = Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs");
			string aftermath = Method(withdrawal,
				"private static bool TryProvePreparedRollbackEvidence");
			AssertBefore(promote, "TryFindResidentObject", "TryProveLocalObjectAbsence");
			AssertBefore(promote, "TryProveCleanupIntent", "PreparedAbsenceCanRollback");
			AssertBefore(promote, "TryProveRemovalWitness", "PreparedAbsenceCanRollback");
			StringAssert.Contains("CleanupRemovalKind", aftermath);
			StringAssert.Contains("GearRemovalKind", aftermath);
			StringAssert.Contains("intent != KingdomPolityCleanupEvidenceProof.Absent", aftermath);
			StringAssert.Contains("TryProveLocalObjectAbsence", aftermath);
		}

		[Test]
		public void PreparedRollbackFreezesRevisionAndClearsWitnessesOnlyAfterCas()
		{
			string withdrawal = Method(Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs"),
				"internal static bool TryWithdrawCurrentEndpoint");
			AssertBefore(withdrawal, "long rollbackRevision = ledger.Revision",
				"TryRemovePreparedBodies");
			AssertBefore(withdrawal, "TryRemovePreparedBodies",
				"TryRollbackPreparedEndpointManifestation");
			AssertBefore(withdrawal, "TryRollbackPreparedEndpointManifestation",
				"TryClearPreparedRollbackEvidence");
			string remove = Method(Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs"),
				"private static bool TryRemovePreparedBodies");
			StringAssert.Contains("TryProvePreparedRollbackEvidence", remove);
			StringAssert.DoesNotContain("TryClearCohortRemovalWitnesses", remove);
			string clear = Method(Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs"),
				"private static bool TryClearRolledBackWitness");
			AssertBefore(clear, "TryFindResidentObject", "TryProveRemovalWitness");
			AssertBefore(clear, "TryProveLocalObjectAbsence", "TryProveRemovalWitness");
			StringAssert.Contains("proof == KingdomPolityCleanupEvidenceProof.Absent", clear);
		}

		[Test]
		public void IntentPromotionReprovesEveryFaultBoundary()
		{
			string transfer = Read("Polity/KingdomPolityEndpointRuntime.CustodyTransfer.cs");
			string remove = Method(transfer, "private static bool TryRemoveExactBody");
			string write = Method(Read("Polity/KingdomPolityEndpointRuntime.RemovalWitness.cs"),
				"private static bool TryWriteCleanupIntent");
			AssertBefore(remove, "TryFindResidentObject", "TryProveLocalObjectAbsence");
			AssertBefore(remove, "TryWriteRemovalWitness(Cell", "TryClearCleanupIntent");
			Assert.Greater(remove.LastIndexOf("TryProveLocalObjectAbsence",
				StringComparison.Ordinal), remove.IndexOf("TryClearCleanupIntent",
					StringComparison.Ordinal));
			Assert.Greater(write.LastIndexOf("InspectUniqueRawZoneSlot(zone",
				StringComparison.Ordinal), write.IndexOf("zone.SetZoneProperty",
					StringComparison.Ordinal));
			StringAssert.Contains("KingdomPolityCleanupEvidenceProof.Exact", write);
		}

		[Test]
		public void CleanupIntentClearIsConditionalAndWitnessBracketed()
		{
			string source = Read("Polity/KingdomPolityEndpointRuntime.RemovalWitness.cs");
			string clear = Method(source, "private static bool TryClearCleanupIntent");
			string conditional = Method(source, "private static bool TryRemoveExactRawZoneSlot");
			int remove = clear.IndexOf("TryRemoveExactRawZoneSlot", StringComparison.Ordinal);
			Assert.Greater(remove, clear.IndexOf("CleanupIntentCanClear", StringComparison.Ordinal));
			int witnessBefore = clear.LastIndexOf("TryProveRemovalWitness", remove,
				StringComparison.Ordinal);
			Assert.Greater(witnessBefore, clear.IndexOf("TryProveExactCleanupIntent",
				StringComparison.Ordinal));
			StringAssert.DoesNotContain("TryProveExactCleanupIntent",
				clear.Substring(witnessBefore, remove - witnessBefore));
			Assert.Greater(clear.LastIndexOf("TryProveRemovalWitness", StringComparison.Ordinal), remove);
			StringAssert.Contains("ICollection<KeyValuePair<string, object>>", conditional);
			StringAssert.Contains("new KeyValuePair<string, object>(Key, Expected)", conditional);
			StringAssert.DoesNotContain("InspectUniqueRawZoneSlot", conditional);
			StringAssert.DoesNotContain("RemoveZoneProperty", conditional);
		}

		[Test]
		public void ResidentLookupEnumeratesEveryResidentAuthorityAndChecksNativeCacheLast()
		{
			string source = Read("Polity/KingdomPolityEndpointRuntime.Locator.cs");
			string collect = Method(Read("Polity/KingdomPolityEndpointRuntime.Locator.Roots.cs"),
				"private static bool TryCollectResidentRoots");
			string scan = Method(source, "private static bool TryScanResidentRoots");
			string lookup = Method(source, "private static bool TryFindResidentObject");
			foreach (string authority in new[] { "ActiveZone", "CachedZones", "Graveyard",
				"ObjectGameState", "The.Player" }) StringAssert.Contains(authority, collect);
			StringAssert.Contains("MaximumResidentLookupObjects", scan);
			StringAssert.Contains("HashSet<GameObject> found", scan);
			AssertBefore(lookup, "TryCollectResidentRoots", "GameObject.FindByID");
			StringAssert.Contains("ClassifyResidentEvidence", lookup);
		}

		[Test]
		public void LegacySetterFaultClassifiesExactNewExactOldAndAmbiguousPoststate()
		{
			string rewrite = Method(Read("Polity/KingdomPolityEndpointRuntime.DeathLegacy.cs"),
				"private static bool TryRewriteLegacyDeathIntent");
			AssertBefore(rewrite, "Zone.SetZoneProperty", "ClassifyLegacyRewriteRecovery");
			StringAssert.Contains("KingdomPolityLegacyRewriteRecovery.Applied", rewrite);
			StringAssert.Contains("KingdomPolityLegacyRewriteRecovery.OldBytesPreserved", rewrite);
			StringAssert.Contains("left ambiguous bytes", rewrite);
		}

		[Test]
		public void NewPureRulesAndFocusedTestsAreRegisteredInBothProjects()
		{
			string portable = Read("DevTests/PortableTests.csproj");
			string full = Read("DevTests/TafTests.csproj");
			foreach (string item in new[] { "KingdomPolityDeathIntentRules.cs",
				"KingdomPolityCohortRules.Abandonment.cs",
				"KingdomPolityDeathReplayTests.cs", "KingdomPolityDeathReplaySourceTests.cs" })
			{
				StringAssert.Contains(item, portable); StringAssert.Contains(item, full);
			}
		}

		/// <summary>
		/// Terminal-cleanup deadlock guard. TryRemoveExactBody is the only ArmCleanup caller and
		/// every armed removal is later re-proved by TryAuthorizePreparedCleanup, so any cohort
		/// phase the cleanup branch admits must also be one that authorization admits. Adding a
		/// phase to one side alone re-opens the deadlock this lane closed.
		/// </summary>
		[Test]
		public void EveryCleanupAdmittedCohortPhaseIsAlsoCleanupAuthorized()
		{
			string[] authorized = Phases(Method(
				Read("Polity/KingdomPolityEndpointRuntime.Death.cs"),
				"internal static bool TryAuthorizePreparedCleanup"));
			CollectionAssert.AreEquivalent(new[] { "Abandoned", "Concluded", "Planned" }, authorized,
				"TryAuthorizePreparedCleanup changed its admitted cohort phase set");
			string cleanup = Method(Read("Polity/KingdomPolityEndpointRuntime.cs"),
				"public static bool TryCleanupCurrentEndpoint");
			string[] admitted = Phases(Between(cleanup, "if (alreadyCleaned)",
				"endpoint cohort has not concluded or abandoned for exact cleanup"));
			CollectionAssert.AreEquivalent(new[] { "Abandoned", "Concluded" }, admitted,
				"the cleanup removal branch changed its admitted cohort phase set");
			CollectionAssert.IsSubsetOf(admitted, authorized,
				"a cohort phase can reach body removal that TryAuthorizePreparedCleanup refuses");
			// The withdrawal path removes prepared bodies at Planned, so Planned must stay authorized.
			StringAssert.Contains("if (cohort.Phase != KingdomPolityCohortPhase.Planned) return false;",
				Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs"));
			CollectionAssert.Contains(authorized, "Planned");
		}

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		/// <summary>Distinct KingdomPolityCohortPhase members named in a slice of source.</summary>
		private static string[] Phases(string source)
		{
			const string marker = "KingdomPolityCohortPhase.";
			SortedSet<string> names = new SortedSet<string>(StringComparer.Ordinal);
			for (int i = source.IndexOf(marker, StringComparison.Ordinal); i >= 0;
				i = source.IndexOf(marker, i + marker.Length, StringComparison.Ordinal))
			{
				int start = i + marker.Length, end = start;
				while (end < source.Length && (char.IsLetterOrDigit(source[end]) ||
					source[end] == '_')) end++;
				if (end > start) names.Add(source.Substring(start, end - start));
			}
			string[] result = new string[names.Count]; names.CopyTo(result); return result;
		}

		private static string Between(string source, string opening, string closing)
		{
			int a = source.IndexOf(opening, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, opening);
			int b = source.IndexOf(closing, a, StringComparison.Ordinal);
			Assert.Greater(b, a, closing);
			return source.Substring(a, b - a);
		}

		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start);
			Assert.Greater(open, start, signature + " body");
			int depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0)
					return source.Substring(start, i - start + 1);
			}
			Assert.Fail(signature + " has no balanced body"); return null;
		}

		private static void AssertBefore(string source, string earlier, string later)
		{
			int a = source.IndexOf(earlier, StringComparison.Ordinal);
			int b = source.IndexOf(later, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, earlier); Assert.Greater(b, a, later);
		}
	}
}
#endif
