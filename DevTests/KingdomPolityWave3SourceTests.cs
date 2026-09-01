#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityWave3SourceTests
	{
		[Test]
		public void LoadZoneAndStationaryCadenceUseOneOrderedActiveAuthority()
		{
			string load = Read("Core/KingdomLoader.cs");
			string events = Read("Core/KingdomSystem.z20.Events.cs");
			string pass = Read("Core/KingdomSystem.z21.SemanticPass.cs");
			string stationary = Read("Core/KingdomSystem.z21b.Polity.cs");
			string active = Read("Polity/KingdomPolityActiveRuntime.cs");
			AssertBefore(load, "KingdomPolityRuntime.TryEnsureFoundation",
				"KingdomPolityActiveRuntime.TryReconcile(kingdomSystem");
			AssertBefore(events, "KingdomSemanticDispatcher.OnZoneActivated",
				"KingdomPolityActiveRuntime.TryReconcile");
			StringAssert.Contains("ReconcileStationaryPolity();", pass);
			StringAssert.Contains("KingdomSemanticDispatcher.IsStationaryDispatch", stationary);
			AssertBefore(active, "KingdomPolityPresentationRuntime.TryObserve",
				"KingdomPolityExperienceRuntime.TryRecover");
			AssertBefore(active, "KingdomPolityExperienceRuntime.TryRecover",
				"KingdomPolityProfileRuntime.TryReconcile");
			AssertBefore(active, "KingdomPolityProfileRuntime.TryReconcile",
				"KingdomPolityResidentRuntime.TryReconcile");
			AssertBefore(active, "KingdomPolityPromotionRuntime.TryReconcile",
				"KingdomPolityVisitRuntime.TryReconcile");
			AssertBefore(active, "KingdomPolityVisitRuntime.TryReconcile",
				"KingdomPolitySchedulerRuntime.TryReconcile");
			StringAssert.Contains("KingdomPolityActiveRuntime.TryReconcileCommittedCapacity", load);
			StringAssert.Contains("if (!KingdomMaster.NewWorkAllowed(System))", active);
			string committed = Slice(active, "public static bool TryReconcileCommittedCapacity",
				"public static void WitnessCohortDeath");
			StringAssert.Contains("KingdomPolityExperienceRuntime.TryRecover(System, Tick, false",
				committed);
			StringAssert.Contains("KingdomPolityCorrespondenceRuntime.TryRecoverTradeReceipts",
				committed);
			foreach (string forbidden in new[] { "TryObserve", "ProfileRuntime", "ResidentRuntime",
				"PromotionRuntime", "VisitRuntime", "SchedulerRuntime", "NonSeatSettlements",
				"System.City", "GetZone", "ZoneManager" })
				StringAssert.DoesNotContain(forbidden, committed);
		}

		[Test]
		public void ProfileProducerUsesDeclaredFactsAndPreservesPhenotypeLineage()
		{
			string models = Read("Polity/KingdomPolityProfileFacts.cs");
			string rules = Read("Polity/KingdomPolityProfileRules.Revisions.cs");
			string runtime = Read("Polity/KingdomPolityProfileRuntime.cs");
			string compact = Read("Polity/KingdomPolityRules.Compaction.cs");
			StringAssert.Contains("Decision", models); StringAssert.Contains("Creed", models);
			StringAssert.Contains("Style", models); StringAssert.Contains("Technology", models);
			StringAssert.Contains("Alliance", models); StringAssert.Contains("Relationship", models);
			StringAssert.Contains("Population", models);
			StringAssert.Contains("Legacy", models);
			StringAssert.DoesNotContain("Species", models);
			StringAssert.DoesNotContain("Culture", models);
			StringAssert.Contains("PopulationBodies(F.Facts, Prior.BodyKeys)", rules);
			StringAssert.Contains("new List<string>(Prior.RoleKeys)", rules);
			StringAssert.Contains("Ledger.Revision != ExpectedLedgerRevision", rules);
			StringAssert.Contains("S.DeclaredCreed", runtime);
			StringAssert.Contains("S.NonSeatSettlements()", runtime);
			StringAssert.Contains("KingdomZoningRules.TechPoints(roster)", runtime);
			StringAssert.Contains("KingdomPolityProfileFactKind.Population", runtime);
			StringAssert.Contains("KingdomPolityProfileRules.CurrentBodyKeys", runtime);
			StringAssert.DoesNotContain("Stage * 2", runtime);
			StringAssert.Contains("AddRelations", runtime);
			StringAssert.Contains("Profile.Revision == 1", compact);
		}

		[Test]
		public void DispatcherNeverLoadsRemoteGroundOrReplaysMissedWindows()
		{
			string facts = Read("Polity/KingdomPolityEndpointFactRuntime.cs");
			string rules = Read("Polity/KingdomPolityDispatchRules.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			string recovery = Read("Polity/KingdomPolityDispatchRules.Recovery.cs");
			StringAssert.Contains("System.NonSeatSettlements()", facts);
			StringAssert.DoesNotContain("GetZone", facts + scheduler);
			StringAssert.DoesNotContain("ZoneManager", facts + scheduler);
			StringAssert.DoesNotContain("The.Player", facts + scheduler);
			StringAssert.Contains("Offer.Tick / PeriodTicks", rules);
			StringAssert.DoesNotContain("for (ulong", rules);
				string planning = Slice(scheduler, "request.PresentationAuthority = authority",
					"return ReconcileLoadedEndpoint");
			AssertBefore(planning, "KingdomPolityCohortRules.TryPlan(System.PolityLedger",
				"KingdomPolityDispatchRules.TryComplete(state, state.Revision");
			StringAssert.Contains("TryCompleteTerminalDue(state, row, existing", scheduler);
			StringAssert.Contains("due polity work lacks exact terminal evidence", scheduler);
			StringAssert.Contains("KingdomPolityLoadedEndpointRuntime.TryObserve", scheduler);
			StringAssert.Contains("SurfaceRef == loadedSettlementId", scheduler);
			StringAssert.DoesNotContain("SurfaceRef == S.City?.SettlementId", scheduler);
			StringAssert.DoesNotContain("Reset(State, RealmId)", recovery);
			StringAssert.Contains("preserved", recovery);
		}

		[Test]
		public void AllFivePurposesUseFiniteLifecycleAndOneManifestationBudget()
		{
			string rules = Read("Polity/KingdomPolityDispatchRules.cs");
			string schedule = Read("Polity/KingdomPolityCohortRules.Schedule.cs");
			string manifestation = Read("Polity/KingdomPolityCohortRules.Manifestation.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			foreach (string purpose in new[] { "Guard", "Patrol", "Courier", "Trader", "Migrant" })
				StringAssert.Contains("KingdomPolityCohortPurpose." + purpose, rules);
			StringAssert.Contains("StayTicks", rules);
			StringAssert.Contains("TryCancelExpiredScheduled", schedule);
			StringAssert.Contains("TryConcludeScheduledStay", schedule);
			StringAssert.Contains("TryPruneScheduledTerminals", schedule);
			StringAssert.Contains("KingdomPolityAttentionRules.TryAdmitManifestation", manifestation);
			StringAssert.Contains("KingdomPolityAttentionRules.TryAdmitManifestation", scheduler);
			StringAssert.Contains("EndpointVerb", scheduler);
		}

		[Test]
		public void TransientBodiesYieldNoRenewableXpOrGearAndNeverWalkUnloadedTiles()
		{
			string npc = Read("Polity/KingdomPolityNpcRuntime.cs") +
				Read("Polity/KingdomPolityNpcRuntime.Gear.cs");
			string body = Read("Polity/r_KingdomPolityCohortBody.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			StringAssert.Contains("RequirePart<NoXPGain>()", npc);
			StringAssert.Contains("created.SetIntProperty(\"NoXP\", 1)", npc);
			StringAssert.Contains("created.SetIntProperty(\"SuppressCorpseDrops\", 1)", npc);
			StringAssert.Contains("commerce.Value = 0.0", npc);
			StringAssert.Contains("item.Physics.Takeable = false", npc);
			StringAssert.DoesNotContain("GetZone", npc + body + scheduler);
			StringAssert.DoesNotContain("ZoneManager", npc + body + scheduler);
			StringAssert.DoesNotContain("ActorId", npc + body + scheduler);
			StringAssert.DoesNotContain("MoveTo", scheduler);
			StringAssert.DoesNotContain("casualt", scheduler.ToLowerInvariant());
			StringAssert.DoesNotContain("conquest", scheduler.ToLowerInvariant());
		}

		[Test]
		public void LoadedDelegationRefusesNewAnswersWhileMasterIsPaused()
		{
			string interaction = Read("Polity/KingdomPolityVisitInteraction.cs");
			string answer = Slice(interaction, "public static void Answer",
				"private static void Welcome");
			AssertBefore(answer, "!KingdomMaster.NewWorkAllowed(system)",
				"AnswerConflict(system, CohortId)");
			AssertBefore(answer, "HasCommittedAnswerRecovery(system, cohort)",
				"Welcome(system, CohortId)");
			StringAssert.Contains("clash?.Conclusion != null || clash?.Intervention != null",
				interaction);
			StringAssert.Contains("return terms?.Conclusion != null;", interaction);
		}

		[Test]
		public void CivicOfficeIsRetirementOnlyAndNeverPolityCapabilityInput()
		{
			string rules = Read("Polity/KingdomPolityFigurePromotion.cs");
			string runtime = Read("Polity/KingdomPolityPromotionRuntime.cs");
			string cohort = Read("Polity/KingdomPolityCohortRules.cs");
			StringAssert.Contains("taf:fact:office-retirement:v1:", runtime);
			StringAssert.Contains("taf:fact:deed:", rules);
			StringAssert.Contains("MaximumActiveNamedFigures", rules);
			StringAssert.Contains("TryRetireAllOfficeFigures", runtime);
			StringAssert.DoesNotContain("TryPromoteNamedFigure", runtime);
			StringAssert.DoesNotContain("OfficeHolderResidentId", runtime);
			StringAssert.DoesNotContain("OfficeHolderName", runtime);
			StringAssert.DoesNotContain("RoleKey = \"officeholder\"", runtime);
			StringAssert.DoesNotContain("RoleKey = \"guard\"", runtime);
			string admission = Slice(rules, "private static bool ValidPromotion",
				"private static bool PromotionRole");
			StringAssert.Contains("F.Origin == KingdomPolityFigureOrigin.PromotedByDeed", admission);
			StringAssert.DoesNotContain("KingdomPolityFigureOrigin.Officeholder", admission);
			StringAssert.Contains("figure.Origin == KingdomPolityFigureOrigin.Officeholder", cohort);
			StringAssert.DoesNotContain("GameObject", rules + runtime);
			StringAssert.DoesNotContain("GetZone", rules + runtime);
		}

		[Test]
		public void SharedExperienceAuthorityPrecedesEveryPlanAndProjection()
		{
			string bridge = Read("Polity/KingdomPolityExperienceRuntime.cs");
			string leases = Read("Polity/KingdomPolityExperienceRuntime.Leases.cs");
			string recovery = Read("Polity/KingdomPolityExperienceRuntime.Recovery.cs");
			string intents = Read("Polity/KingdomPolityExperienceRuntime.Intents.cs");
			string disposition = Read("Polity/KingdomPolityExperienceRecoveryRules.cs");
			string scheduler = Read("Polity/KingdomPolitySchedulerRuntime.cs");
			string visit = Read("Polity/KingdomPolityVisitRuntime.cs");
			string dispute = Read("Polity/KingdomPolityVisitRuntime.Dispute.cs");
			string endpoint = Read("Polity/KingdomPolityEndpointRuntime.cs");
			string withdrawal = Read("Polity/KingdomPolityEndpointRuntime.Withdrawal.cs");
			string active = Read("Polity/KingdomPolityActiveRuntime.cs");
			StringAssert.Contains("TryReserveAmbientPlan", bridge + scheduler);
			StringAssert.Contains("KingdomExperienceOptionKind.AmbientUse", bridge);
			StringAssert.Contains("TryReservePresentation", bridge);
			StringAssert.Contains("TryReserveDirectedPlan", bridge + visit + dispute);
			StringAssert.Contains("KingdomExperienceOptionKind.CivicStory", bridge);
			StringAssert.Contains("TryReserveBodies", bridge);
			StringAssert.DoesNotContain("Option(Purpose)", bridge);
			AssertBefore(scheduler, "TryReserveAmbientPlan",
				"KingdomPolityCohortRules.TryPlan");
			AssertBefore(visit, "TryReserveDirectedPlan",
				"KingdomPolityCohortRules.TryPlan");
			AssertBefore(dispute, "TryReserveDirectedPlan",
				"KingdomPolityDiplomacyRules.TryIngestExactGrievance");
			AssertBefore(dispute, "TryReserveDirectedPlan",
				"KingdomPolityCohortRules.TryPlan");
			AssertBefore(endpoint, "TryReserveAmbientProjection",
				"TryPrepareEndpointManifestation");
			AssertBefore(endpoint, "TryReserveDirectedProjection",
				"TryPrepareEndpointManifestation");
			AssertBefore(endpoint, "TryCommitEndpointCleanup", "TryReleaseForCohort");
			AssertBefore(active, "KingdomPolityExperienceRuntime.TryRecover",
				"KingdomPolityVisitRuntime.TryReconcile");
			StringAssert.Contains("TryEnsureProjectedLease", recovery + leases);
			StringAssert.Contains("bool allowNew = KingdomMaster.NewWorkAllowed(System) && " +
				"PresentationEnabled", recovery);
			StringAssert.Contains("if (allowNew && !KingdomExperienceRuntime." +
				"TryObserveConfiguredOptions", recovery);
			StringAssert.Contains("cannot admit a current plan while new work is off", recovery);
			StringAssert.DoesNotContain("!KingdomMaster.NewWorkAllowed(System)) return true", recovery);
			StringAssert.Contains("EnsureThenRetainFrozen", recovery + disposition);
			StringAssert.Contains("KingdomPolityLoadedEndpointRuntime.TryObserve", recovery);
			StringAssert.Contains("loadedSettlementId, current", recovery);
			StringAssert.Contains("TryRecoverDurablePresentation", leases);
			StringAssert.Contains("TryRecoverDurableBodies", leases);
			string projectedLease = Slice(leases, "private static bool TryEnsureProjectedLease",
				"private static void BuildRequests");
			StringAssert.DoesNotContain("TryReservePresentation(System", projectedLease);
			StringAssert.DoesNotContain("TryReserveBodies(System", projectedLease);
			StringAssert.Contains("out KingdomExperienceLeaseState state", leases);
			StringAssert.Contains("Lease.ReservedTick == Cohort.PresentationReservedTick", leases);
			StringAssert.Contains("Lease.EnableEpoch == Cohort.PresentationEnableEpoch", leases);
			StringAssert.Contains("TryClassifyLeaseProof", recovery);
			StringAssert.Contains("proofState == KingdomExperienceLeaseState.Active", recovery);
			StringAssert.Contains("KingdomExperienceCapacityFault.OptionDisabled", bridge);
			StringAssert.Contains("KingdomExperienceCapacityFault.CauseBeforeEnable", bridge);
			StringAssert.Contains("forbidden audience lease", bridge + leases);
			StringAssert.Contains("TryWithdrawCurrentEndpoint", recovery);
			StringAssert.Contains("TryRollbackPreparedEndpointManifestation", withdrawal);
			string sourceProof = Slice(recovery, "private static bool ValidatePresentationSources",
				"private static bool TryCancelLapsedUnpresented");
			StringAssert.Contains("TryReconcileOrphanSource", sourceProof);
			StringAssert.Contains("lease retained", intents);
			StringAssert.Contains("TryReleaseAmbient", intents);
			StringAssert.Contains("TryReleaseDirected", intents);
			StringAssert.DoesNotContain("TryRelease", sourceProof);
			StringAssert.Contains("existing == null && !KingdomPolityExperienceRuntime." +
				"TryReleaseAmbient", scheduler);
			StringAssert.Contains("existing == null && !KingdomPolityExperienceRuntime." +
				"TryReleaseDirected", visit);
			StringAssert.DoesNotContain("TryReconstructionEpoch", bridge + leases + recovery);
			StringAssert.DoesNotContain("EnableEpoch - 1", bridge + leases + recovery);
			StringAssert.DoesNotContain("AllowCreate", bridge + leases);
			StringAssert.DoesNotContain("GetZone", bridge + leases + recovery + withdrawal);
		}

		[Test]
		public void ExactPresentationTripleRoundTripsAndMasterResumeReanchorsNewCauses()
		{
			string model = Read("Polity/KingdomPolityTrafficState.cs");
			string request = Read("Polity/KingdomPolityJourneyModels.cs");
			string cohort = Read("Polity/KingdomPolityCohortRules.cs");
			string codec = Read("Polity/KingdomPolityCodec.TrafficRows.cs");
			string options = Read("Polity/KingdomPolityRules.Options.cs");
			string dispatch = Read("Polity/KingdomPolityDispatchRules.cs");
			foreach (string field in new[] { "PresentationOptionKind",
				"PresentationEnableEpoch", "PresentationReservedTick" })
			{
				StringAssert.Contains(field, model + cohort + codec);
				StringAssert.Contains(field.Replace("Presentation", string.Empty), request);
			}
			StringAssert.Contains("TryPrepareMasterResume", options);
			StringAssert.Contains("TryPublishMasterResume", options);
			StringAssert.Contains("CanPublishMasterResume", options);
			StringAssert.Contains("PublishMasterResumePrevalidated", options);
			AssertBefore(options, "CanPublishMasterResume(Ledger, Dispatch, Plan",
				"PublishMasterResumePrevalidated(Ledger, Dispatch, Plan)");
			StringAssert.Contains("target.Options.EnableEpoch++", options);
			StringAssert.Contains("target.Options.FutureCauseFloorTick = Tick", options);
			StringAssert.Contains("FutureCauseFloorTick = Tick", options);
			StringAssert.Contains("WindowStart(window) >= State.FutureCauseFloorTick", dispatch);
			StringAssert.DoesNotContain("PresentationEnableEpoch = target.Options", options);
			StringAssert.DoesNotContain("PresentationReservedTick = Tick", options);
		}

		[Test]
		public void DisabledOrUnusedPlansReleaseCapacityWithoutInventingOutcomes()
		{
			string cancellation = Read("Polity/KingdomPolityCohortRules.Cancellation.cs");
			string recovery = Read("Polity/KingdomPolityExperienceRuntime.Recovery.cs");
			string endpoints = Read("Polity/KingdomPolityVisitRuntime.Endpoints.cs");
			StringAssert.Contains("TryCancelUnpresented", cancellation + recovery + endpoints);
			StringAssert.Contains("polity-presentation-lapse-v1", recovery);
			StringAssert.Contains("terms.Conclusion.ConclusionId", endpoints);
			StringAssert.Contains("TryReleaseDirected", endpoints);
			AssertBefore(endpoints, "cohort.Phase == KingdomPolityCohortPhase.Cancelled",
				"return ReconcileReturn(ledger, P, Manifest, Tick, out Failure)");
			StringAssert.DoesNotContain("casualt", cancellation.ToLowerInvariant());
			StringAssert.DoesNotContain("conquest", cancellation.ToLowerInvariant());
			StringAssert.DoesNotContain("death", cancellation.ToLowerInvariant());
		}

		private static string Read(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static void AssertBefore(string source, string first, string second)
		{
			int a = source.IndexOf(first, StringComparison.Ordinal);
			int b = source.IndexOf(second, StringComparison.Ordinal);
			Assert.GreaterOrEqual(a, 0, first); Assert.Greater(b, a, second);
		}

		private static string Slice(string Source, string First, string Last)
		{
			int start = Source.IndexOf(First, StringComparison.Ordinal);
			int end = Source.IndexOf(Last, start < 0 ? 0 : start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, First); Assert.Greater(end, start, Last);
			return Source.Substring(start, end - start);
		}
	}
}
#endif
