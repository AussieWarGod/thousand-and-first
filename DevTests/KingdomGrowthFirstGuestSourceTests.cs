#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGrowthFirstGuestSourceTests
	{
		[Test]
		public void GrowthOwnsCauseCandidateChoiceAndAdmission()
		{
			string growth = KingdomGrowthLogicalSource.Read();
			string start = Source("Growth/KingdomGrowth.FirstGuestStart.cs");
			StringAssert.Contains("TryLocateGrowthArrival(system, zone", start);
			StringAssert.Contains("PrepareGrowthFirstGuestCandidate(growth", start);
			StringAssert.Contains("TryPublishGrowthArrivalCandidate", start);
			StringAssert.Contains("return ArrivalResult.Deferred;", start);
			StringAssert.DoesNotContain("GameObject.Create", start);
			StringAssert.DoesNotContain("ReconcileArrival(", start);

			int waiting = growth.IndexOf(
				"candidate.Phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice",
				StringComparison.Ordinal);
			int create = growth.IndexOf("GameObject.Create(candidate.Blueprint)",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(waiting, 0);
			Assert.Greater(create, waiting,
				"AwaitingChoice must return before any physical candidate callback");
			StringAssert.Contains("TryInterposeLegacyFirstGuest", growth);
			StringAssert.Contains("DecodedLegacyCandidateHasNoMaterialDebit", growth);
			StringAssert.Contains("DecodedLegacyCandidateHasNoBodyCallback", growth);
			StringAssert.Contains("LegacyCandidateDomainReceiptExists", growth);
		}

		[Test]
		public void AdmissionReservesOnePhysicalBodyBeforeGrowthOwnsTheChoice()
		{
			string runtime = KingdomFirstGuestRuntimeLogicalSource.Read();
			string admission = Source("Growth/KingdomFirstGuestRuntime.Admission.cs");
			int reserve = admission.IndexOf("TryReserveBodies(system, lease",
				StringComparison.Ordinal);
			int admit = admission.IndexOf("TryAdmitGrowthFirstGuest(growth, candidate, lease, now",
				StringComparison.Ordinal);
			int continueGrowth = admission.IndexOf("TryContinueFirstGuestDecision",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(reserve, 0);
			Assert.Greater(admit, reserve);
			Assert.GreaterOrEqual(admit, 0);
			Assert.Greater(continueGrowth, admit,
				"physical guest recovery must begin only after Growth owns the choice");
			StringAssert.Contains("NewFirstGuestBodyRequest", admission);
			StringAssert.Contains("TryReleaseBodies", admission);
			StringAssert.Contains("TryReserveAudience(system, audience", runtime);
			StringAssert.Contains("Direct Growth record: optional presentation unavailable", runtime);
			StringAssert.Contains("Choice authority is unchanged", runtime);
			StringAssert.Contains("TryReleaseAudience", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
			StringAssert.DoesNotContain("ZoneManager", runtime);
			StringAssert.DoesNotContain("GameObject.Create", runtime);
			StringAssert.DoesNotContain("ObjectGameState.Add", runtime);
		}

		[Test]
		public void FirstGuestBranchDoesNotReplaceLaterOrdinaryArrivalEngine()
		{
			string source = Source("Growth/KingdomGrowth.ArrivalStart.cs");
			int first = source.IndexOf("growth.ArrivalOpportunity.FirstGuest",
				StringComparison.Ordinal);
			int correspondence = source.IndexOf("StartFirstGuestOpportunity(system, zone, survey",
				first, StringComparison.Ordinal);
			int ordinary = source.IndexOf("PrepareGrowthArrivalCandidate(growth, marker, blueprint",
				correspondence, StringComparison.Ordinal);
			int publish = source.IndexOf("TryPublishGrowthArrivalCandidate", ordinary,
				StringComparison.Ordinal);
			int reconcile = source.IndexOf("ReconcileArrival(system, zone, survey", publish,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0);
			Assert.Greater(correspondence, first);
			Assert.Greater(ordinary, correspondence);
			Assert.Greater(publish, ordinary);
			Assert.Greater(reconcile, publish);
		}

		[Test]
		public void StoryOffBypassesNewAndLegacyCorrespondenceWithoutBacklog()
		{
			string start = Source("Growth/KingdomGrowth.FirstGuestStart.cs");
			StringAssert.Contains("TryCivicStoryAllowsFirstGuest", start);
			StringAssert.Contains("KingdomExperienceRules.CanEmit", start);
			StringAssert.Contains("KingdomExperienceObservationKind.Closed, 0", start);
			string arrival = Source("Growth/KingdomGrowth.ArrivalCadence.cs");
			int gate = arrival.IndexOf("TryCivicStoryAllowsFirstGuest(system, tick",
				StringComparison.Ordinal);
			int special = arrival.IndexOf("TryPrepareGrowthArrivalPayload", gate,
				StringComparison.Ordinal);
			int ordinary = arrival.IndexOf("TryFreezeGrowthArrivalOpportunity", special,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(gate, 0); Assert.Greater(special, gate);
			Assert.Greater(ordinary, special);
			string recovery = Source("Growth/KingdomGrowth.FirstGuestRecovery.cs");
			AssertOrdered(recovery, "candidate.LegacyAutomaticRecovery",
				"!TryCivicStoryAllowsFirstGuest(system, tick",
				"TryInterposeLegacyPreparedFirstGuest");
		}

		[Test]
		public void ZoneAndReleaseGatesPrecedeArtifactReadsAndLeaseRecovery()
		{
			string reconcile = Source("Growth/KingdomGrowth.z05.ArrivalStartAndReconcile.cs");
			int bind = reconcile.IndexOf("BindLegacyGrowthArrivalCandidateZone",
				StringComparison.Ordinal);
			int zone = reconcile.IndexOf("GrowthArrivalCandidateBoundToZone", bind,
				StringComparison.Ordinal);
			int interpose = reconcile.IndexOf("TryInterposeLegacyFirstGuest", zone,
				StringComparison.Ordinal);
			int migrate = reconcile.IndexOf("TryMigrateArrivalSemanticPlan", interpose,
				StringComparison.Ordinal);
			int firstGuestGate = reconcile.IndexOf("candidate.FirstGuest != null", migrate,
				StringComparison.Ordinal);
			int recover = reconcile.IndexOf("EnsureFirstGuestBodyLeaseForRecovery", firstGuestGate,
				StringComparison.Ordinal);
			Assert.Greater(zone, bind);
			Assert.Greater(interpose, zone,
				"foreign-zone activation returns before five physical proof reads");
			Assert.Greater(migrate, interpose,
				"foreign-zone activation returns before semantic coordinate migration");
			Assert.Greater(firstGuestGate, migrate);
			Assert.Greater(recover, firstGuestGate);
			string recovery = Source("Growth/KingdomGrowth.FirstGuestRecovery.cs");
			AssertOrdered(recovery, "SameFirstGuestBodyRequest(expected, actual)",
				"GrowthFirstGuestBodyLeaseRecoveryRequired", "TryRecoverDurableBodies");

			string completion = Source("Growth/KingdomGrowth.FirstGuestCompletion.cs");
			AssertOrdered(completion, "TryReleaseBodies(system, x.BodyReservationId",
				"TryMarkGrowthFirstGuestBodyReleased");
			string domain = Source("Growth/KingdomGrowth.z07.ArrivalCompletion.cs");
			AssertOrdered(domain, "KingdomResidents.TryEnsureRow",
				"ReleaseFirstGuestBodyAfterCitizenship");
		}

		[Test]
		public void CurrentGrowthApplicabilityPrecedesChoiceWithoutExperienceMutation()
		{
			string admission = Source("Growth/KingdomFirstGuestRuntime.Admission.cs");
			int current = admission.IndexOf("TryCheckGrowthFirstGuestCurrentApplicability",
				StringComparison.Ordinal);
			int observe = admission.IndexOf("TryObserveConfiguredOptions", StringComparison.Ordinal);
			int reserve = admission.IndexOf("TryReserveBodies", StringComparison.Ordinal);
			int admit = admission.IndexOf("TryAdmitGrowthFirstGuest", StringComparison.Ordinal);
			Assert.GreaterOrEqual(current, 0);
			Assert.Greater(observe, current);
			Assert.Greater(reserve, observe);
			Assert.Greater(admit, current);
		}

		[Test]
		public void OnlyAffirmativeAdmissionCommitsGovernanceEnergy()
		{
			string admission = Source("Growth/KingdomFirstGuestRuntime.Admission.cs");
			AssertOrdered(admission,
				"TryAdmitGrowthFirstGuest(growth, candidate, lease, now",
				"KingdomGovernanceScope.Commit(\"admit first guest\")",
				"TryContinueFirstGuestDecision(system, founder");

			string runtime = Source("Growth/KingdomFirstGuestRuntime.cs");
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit(\"defer first guest\")",
				runtime);
			StringAssert.DoesNotContain("KingdomGovernanceScope.Commit(\"decline first guest\")",
				runtime);
		}

		[Test]
		public void PausedCorrespondenceReadsFrozenFactsBeforeAnyLeaseOrChoice()
		{
			Assert.IsTrue(KingdomCharterMenuRules.AvailableWhileSimulationPaused(
				KingdomCharterAction.FirstGuestCorrespondence));
			string source = Source("Growth/KingdomFirstGuestRuntime.cs");
			int open = source.IndexOf("public static void Open", StringComparison.Ordinal);
			Assert.GreaterOrEqual(open, 0);
			string body = source.Substring(open);
			int pause = body.IndexOf("if (!KingdomMaster.NewWorkAllowed(system))",
				StringComparison.Ordinal);
			int directFacts = body.IndexOf("ComposeFacts(candidate, false",
				StringComparison.Ordinal);
			int presentation = body.IndexOf("TryOpenPresentationLease(system, candidate",
				StringComparison.Ordinal);
			int choice = body.IndexOf("Popup.PickOption", StringComparison.Ordinal);
			int defer = body.IndexOf("Defer(system, growth, candidate", StringComparison.Ordinal);
			int decline = body.IndexOf("Decline(system, founder", StringComparison.Ordinal);
			Assert.GreaterOrEqual(pause, 0);
			Assert.Greater(directFacts, pause);
			Assert.Greater(presentation, directFacts);
			Assert.Greater(choice, presentation);
			Assert.Greater(defer, choice);
			Assert.Greater(decline, choice);
			string readOnlyBranch = body.Substring(pause, presentation - pause);
			StringAssert.Contains("simulation is paused", readOnlyBranch);
			StringAssert.Contains("read-only", readOnlyBranch);
			StringAssert.Contains("return;", readOnlyBranch);
			StringAssert.DoesNotContain("TryReserveAudience", readOnlyBranch);
			StringAssert.DoesNotContain("TryDeferGrowthFirstGuest", readOnlyBranch);
			StringAssert.DoesNotContain("TryDeclineGrowthFirstGuest", readOnlyBranch);
		}

		[Test]
		public void CandidateFactsComeFromOneOwnedSemanticCatalogue()
		{
			string semantic = Source("Core/KingdomSemanticSelection.FirstGuest.cs");
			StringAssert.Contains("private static List<KingdomSemanticWeightedEntry> "
				+ "FirstGuestCatalogue()", semantic);
			StringAssert.Contains("GrowthFirstGuestBlueprintAllowed(blueprint)", semantic);
			StringAssert.Contains("cohort exactly 1", Source(
				"Growth/KingdomFirstGuestRuntime.Facts.cs"));
			StringAssert.DoesNotContain("PopulationManager", semantic);
			StringAssert.DoesNotContain("GetPopulationTable", semantic);
			StringAssert.DoesNotContain("PopulationTable", semantic);
		}

		[Test]
		public void ChoiceHasNoExpiryRewardServiceOrParallelCapacityCounter()
		{
			string models = Source("Experience/KingdomGrowthFirstGuestOpportunity.cs")
				+ Source("Experience/KingdomGrowthFirstGuestEnums.cs");
			string rules = FirstGuestRules();
			string runtime = KingdomFirstGuestRuntimeLogicalSource.Read();
			StringAssert.Contains("DeferredTick", models);
			StringAssert.DoesNotContain("Expiry", models);
			StringAssert.DoesNotContain("Reward", models);
			StringAssert.DoesNotContain("Labor", models);
			StringAssert.DoesNotContain("List<", models);
			StringAssert.DoesNotContain("Dictionary<", models);
			StringAssert.DoesNotContain("CapacityCount", models);
			StringAssert.Contains("Exact W0 request proof only", models);
			StringAssert.Contains("sole capacity", models);
			StringAssert.Contains("TryDeferGrowthFirstGuest", rules);
			StringAssert.Contains("TryDeclineGrowthFirstGuest", rules);
			StringAssert.Contains("TryAdmitGrowthFirstGuest", rules);
			StringAssert.Contains("TryBeginGrowthFirstGuestCitizenship", rules);
			StringAssert.Contains("TryBeginGrowthFirstGuestDeparture", rules);
			StringAssert.Contains("Deferral has no expiry, charge, labor, service, reward",
				runtime);
		}

		[Test]
		public void GrowthV6WireAndV5LosslessGateAreExplicit()
		{
			string lifecycle = Source("Experience/KingdomLifecycleRules.cs");
			StringAssert.Contains("public const int SemanticGrowthFormatVersion = 3;", lifecycle);
			StringAssert.Contains("public const int FirstGuestGrowthFormatVersion = 4;", lifecycle);
			StringAssert.Contains("public const int TerminalReceiptGrowthFormatVersion = 5;",
				lifecycle);
			StringAssert.Contains("public const int FirstGuestPhysicalGrowthFormatVersion = 6;",
				lifecycle);
			StringAssert.Contains("public const int CadenceGrowthFormatVersion = 7;", lifecycle);
			StringAssert.Contains("GrowthV3PayloadFixture", Source(
				"Experience/KingdomLifecycleWireCodec.GrowthEnvelope.cs"));
			string envelope = Source("Experience/KingdomLifecycleWireCodec.GrowthEnvelope.cs");
			StringAssert.Contains("GrowthV5PayloadFixture", envelope);
			StringAssert.Contains("GrowthV6PayloadFixture", envelope);
			StringAssert.Contains("growth v5 cannot encode physical guest evidence", envelope);
			StringAssert.Contains("DowngradePhysicalFirstGuestForLegacyFixture", envelope);
			string codec = Source("Experience/KingdomLifecycleWireCodec.GrowthFirstGuest.cs");
			StringAssert.Contains("wireVersion >= KingdomLifecycleRules."
				+ "FirstGuestPhysicalGrowthFormatVersion", codec);
			StringAssert.Contains("historical growth carried physical first-guest rules", codec);
			StringAssert.Contains("GuestTerminalReceiptId", codec);
			StringAssert.Contains("result.Version = "
				+ "KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion", codec);
		}

		[Test]
		public void ArchiveV17AppendsCadenceAndKeepsV16PhysicalDomainFrozen()
		{
			string codec = KingdomArchivedSettlementCodecLogicalSource.Read();
			StringAssert.Contains("public const int FirstGuestVersion = 15;", codec);
			StringAssert.Contains("public const int PhysicalFirstGuestVersion = 16;", codec);
			StringAssert.Contains("public const int ArrivalCadenceVersion = 17;", codec);
			StringAssert.Contains("public const int CurrentVersion = ArrivalCadenceVersion;",
				codec);
			StringAssert.Contains("TryEncodeFirstGuestV15ForTests", codec);
			StringAssert.Contains("TryEncodePhysicalFirstGuestV16ForTests", codec);
			StringAssert.Contains("SchemaVersion < PhysicalFirstGuestVersion", codec);
			StringAssert.Contains("GuestActionReceiptId", codec);
			StringAssert.Contains("GuestTerminalReceiptId", codec);
			StringAssert.Contains("KingdomGrowthArrivalCandidatePhase.Declined", codec);
			StringAssert.Contains("KingdomGrowthArrivalDisposition.Declined", codec);
			StringAssert.Contains("TerminalReceiptGrowthFormatVersion", codec);
			StringAssert.Contains("StageHistoricalPhysicalFirstGuestAuthority", codec);
			StringAssert.Contains("UpgradeHistoricalGrowthArrivalCadence", codec);
			StringAssert.Contains("SchemaVersion < ArrivalCadenceVersion", codec);
			StringAssert.Contains("growth.FirstGuestTerminal.Version =", codec);
			StringAssert.Contains("KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion", codec);
			StringAssert.Contains("version != FirstGuestVersion", codec);
		}

		[Test]
		public void PhysicalGuestOffersOnlyThreeExplicitPlayerChoices()
		{
			string interaction = Source(
				"Growth/KingdomGrowth.PhysicalFirstGuest.Interaction.cs");
			StringAssert.Contains("\"Welcome as citizen\", \"Ask to depart\", "
				+ "\"Remain our guest\"", interaction);
			AssertOrdered(interaction,
				"TryCheckGrowthFirstGuestCurrentApplicability",
				"TryBeginGrowthFirstGuestCitizenship");
			AssertOrdered(interaction,
				"TryBeginGrowthFirstGuestCitizenship",
				"KingdomGovernanceScope.Commit(\"welcome first guest as citizen\")",
				"ContinueCommittedPhysicalFirstGuestAction");
			AssertOrdered(interaction,
				"TryBeginGrowthFirstGuestDeparture",
				"KingdomGovernanceScope.Commit(\"ask first guest to depart\")",
				"ContinueCommittedPhysicalFirstGuestAction");
			StringAssert.Contains("remains your guest without deadline, cost, work, or hidden "
				+ "consequence", interaction);
			StringAssert.DoesNotContain("EndTurnEvent", interaction);
			StringAssert.DoesNotContain("Schedule", interaction);
			StringAssert.DoesNotContain("Automatic", interaction);
		}

		[Test]
		public void GuestBodyHasNoLootXpTradeLaborOrCombatContribution()
		{
			string hardening = Source(
				"Growth/KingdomGrowth.PhysicalFirstGuest.Hardening.cs");
			string part = Source("Growth/r_KingdomFirstGuestBody.cs");
			StringAssert.Contains("GetInventoryDirectAndEquipment", hardening);
			StringAssert.Contains("ForceUnequipAndRemove", hardening);
			StringAssert.Contains("item.Obliterate", hardening);
			StringAssert.Contains("body.SetIntProperty(\"NoXP\", 1)", hardening);
			StringAssert.DoesNotContain("NoXPGain", hardening + part);
			StringAssert.Contains("body.SetIntProperty(\"SuppressCorpseDrops\", 1)", hardening);
			StringAssert.Contains("corpse.CorpseChance = 0", hardening);
			StringAssert.Contains("brain.Allegiance = new AllegianceSet { Calm = true }", hardening);
			StringAssert.Contains("brain.Passive = true", hardening);
			StringAssert.Contains("brain.Mobile = false", hardening);
			StringAssert.Contains("brain.Staying = true", hardening);
			StringAssert.Contains("brain.DoReequip = false", hardening);
			AssertOrdered(hardening, "body.RemovePart(part)",
				"part.Inert = true", "brain.Flags = part.OriginalBrainFlags");
			StringAssert.Contains("CanBeTradedEvent", part);
			StringAssert.Contains("CanBeReplicatedEvent", part);
			StringAssert.Contains("CanBeDismemberedEvent", part);
			StringAssert.Contains("CanJoinPartyLeaderEvent", part);
			StringAssert.Contains("E.AddAction(\"Chat\", \"speak with the first guest\"", part);
			StringAssert.Contains("Override: true", part);
			StringAssert.Contains("CanHaveSmartUseConversation", part);
			StringAssert.Contains("IsConversationallyResponsive", part);
			StringAssert.Contains("HandleEvent(CommandSmartUseEvent E)", part);
			StringAssert.Contains("E.Value = 0.0", part);
			StringAssert.Contains("ApplyProselytize", part);
			StringAssert.Contains("CanApplyBeguile", part);
			StringAssert.Contains("CanApplyDomination", part);
			StringAssert.DoesNotContain("KingdomCitizen", hardening + part);
			StringAssert.DoesNotContain("KingdomResidents", hardening + part);
			StringAssert.DoesNotContain("KingdomStations", hardening + part);
			StringAssert.DoesNotContain("AwardXP", hardening + part);
			StringAssert.DoesNotContain("AddXP", hardening + part);
		}

		[Test]
		public void CustodyRejectsDecoysDuplicatesMovesAndAddObjectRefusal()
		{
			string custody = Source("Growth/KingdomGrowth.PhysicalFirstGuest.Custody.cs");
			StringAssert.Contains("CountArrivalMarker(zone, candidate.Marker) != 1", custody);
			StringAssert.Contains("zone.FindObjectByID(candidate.ObjectId)", custody);
			StringAssert.Contains("body.IDIfAssigned == candidate.ObjectId", custody);
			StringAssert.Contains("ReferenceEquals(body.CurrentZone, zone)", custody);
			StringAssert.Contains("TryGetValue(candidate.EscrowKey, out rooted)", custody);
			StringAssert.Contains("cell.AddObject(body, NoStack: true, Silent: true)", custody);
			StringAssert.Contains("if (!ReferenceEquals(accepted, body))", custody);
			StringAssert.Contains("zone refused the exact guest body", custody);
			StringAssert.Contains("guest placement endpoint did not prove", custody);
			StringAssert.Contains("exact loaded guest body is absent or ambiguous", custody);
			StringAssert.Contains("loaded marker count is ambiguous", custody);
			StringAssert.DoesNotContain("body.ID ==", custody);
			StringAssert.DoesNotContain("GetID()", custody);
		}

		[Test]
		public void ProjectionRootAndSuspensionOrderingProtectFreezeZoneCuts()
		{
			string custody = Source("Growth/KingdomGrowth.PhysicalFirstGuest.Custody.cs");
			int project = custody.IndexOf("private static bool TryProjectPhysicalFirstGuest",
				StringComparison.Ordinal);
			int retract = custody.IndexOf("private static bool TryRetractPhysicalFirstGuest",
				project, StringComparison.Ordinal);
			string projection = custody.Substring(project, retract - project);
			AssertOrdered(projection,
				"cell.AddObject(body, NoStack: true, Silent: true)",
				"TryExactLoadedPhysicalFirstGuest(candidate, zone, out GameObject loaded, true)",
				"ObjectGameState.TryGetValue(candidate.EscrowKey, out rooted)",
				"ObjectGameState.Remove(candidate.EscrowKey)");
			string retraction = custody.Substring(retract);
			AssertOrdered(retraction,
				"RootArrivalCandidate(candidate, body)",
				"body.TryRemoveFromContext()",
				"TryExactPhysicalFirstGuestEscrow(candidate, out GameObject escrowed)");
			AssertOrdered(retraction,
				"if (!body.TryRemoveFromContext())",
				"TryExactLoadedPhysicalFirstGuest(candidate, zone",
				"ObjectGameState.Remove(candidate.EscrowKey)",
				"guest body refused context removal");

			string events = Source("Core/KingdomSystem.z20.Events.cs");
			int thaw = events.IndexOf("HandleEvent(ZoneThawedEvent", StringComparison.Ordinal);
			int suspend = events.IndexOf("HandleEvent(SuspendingEvent", StringComparison.Ordinal);
			int activated = events.IndexOf("HandleEvent(ZoneActivatedEvent", StringComparison.Ordinal);
			Assert.GreaterOrEqual(thaw, 0); Assert.Greater(suspend, thaw);
			Assert.Greater(activated, suspend);
			AssertOrdered(events.Substring(thaw, suspend - thaw),
				"OnPhysicalFirstGuestZoneActivated", "ObserveAutomaticWake");
			AssertOrdered(events.Substring(suspend, activated - suspend),
				"OnPhysicalFirstGuestSuspending", "ObserveAutomaticWake");
			AssertOrdered(events.Substring(activated),
				"OnPhysicalFirstGuestZoneActivated", "ObserveAutomaticWake");
		}

		[Test]
		public void RemovalIsObservedOnlyFromTheExactLoadedBody()
		{
			string part = Source("Growth/r_KingdomFirstGuestBody.cs");
			StringAssert.Contains("BeforeDeathRemovalEvent", part);
			StringAssert.Contains("OnDestroyObjectEvent", part);
			StringAssert.Contains("ObservePhysicalFirstGuestRemoval", part);
			string reconcile = Source("Growth/KingdomGrowth.PhysicalFirstGuest.Reconcile.cs");
			StringAssert.Contains("TryExactLoadedPhysicalFirstGuest(candidate, zone", reconcile);
			StringAssert.Contains("ReferenceEquals(exact, body)", reconcile);
			StringAssert.Contains("body.IDIfAssigned", reconcile);
			StringAssert.Contains("TryObserveGrowthFirstGuestTerminal", reconcile);
			StringAssert.DoesNotContain("GetZone(", reconcile);
			StringAssert.DoesNotContain("ZoneManager", reconcile);
			StringAssert.DoesNotContain("TimeOut", reconcile);
			StringAssert.DoesNotContain("Expiry", reconcile);
		}

		[Test]
		public void FirstGuestProductionShardsStayBelowThreeHundredLines()
		{
			string[] files =
			{
				"Experience/KingdomGrowthFirstGuestEnums.cs",
				"Experience/KingdomGrowthFirstGuestOpportunity.cs",
				"Experience/KingdomGrowthFirstGuestRules.Choices.cs",
				"Experience/KingdomGrowthFirstGuestRules.Completion.cs",
				"Experience/KingdomGrowthFirstGuestRules.Hash.cs",
				"Experience/KingdomGrowthFirstGuestRules.Identity.cs",
				"Experience/KingdomGrowthFirstGuestRules.LeasePhase.cs",
				"Experience/KingdomGrowthFirstGuestRules.Migration.cs",
				"Experience/KingdomGrowthFirstGuestRules.Preparation.cs",
				"Experience/KingdomGrowthFirstGuestRules.Validation.cs",
				"Experience/KingdomLifecycleWireCodec.GrowthFirstGuest.cs",
				"Growth/KingdomGrowth.FirstGuestCapacity.cs",
				"Growth/KingdomGrowth.FirstGuestCompletion.cs",
				"Growth/KingdomGrowth.FirstGuestInteraction.cs",
				"Growth/KingdomGrowth.FirstGuestRecovery.cs",
				"Growth/KingdomGrowth.FirstGuestStart.cs",
				"Growth/KingdomFirstGuestRuntime.cs",
				"Growth/KingdomFirstGuestRuntime.Admission.cs",
				"Growth/KingdomFirstGuestRuntime.Capacity.cs",
				"Growth/KingdomFirstGuestRuntime.Facts.cs",
				"Growth/KingdomGrowth.PhysicalFirstGuest.Custody.cs",
				"Growth/KingdomGrowth.PhysicalFirstGuest.Hardening.cs",
				"Growth/KingdomGrowth.PhysicalFirstGuest.Interaction.cs",
				"Growth/KingdomGrowth.PhysicalFirstGuest.Reconcile.cs",
				"Growth/r_KingdomFirstGuestBody.cs",
				"Core/KingdomSemanticSelection.FirstGuest.cs"
			};
			for (int i = 0; i < files.Length; i++)
				Assert.Less(File.ReadAllLines(Path.Combine(TestMain.RepositoryRoot,
					files[i])).Length, 300,
					files[i]);
		}

		private static string FirstGuestRules()
		{
			string[] files =
			{
				"Experience/KingdomGrowthFirstGuestRules.Choices.cs",
				"Experience/KingdomGrowthFirstGuestRules.Completion.cs",
				"Experience/KingdomGrowthFirstGuestRules.Identity.cs",
				"Experience/KingdomGrowthFirstGuestRules.Migration.cs",
				"Experience/KingdomGrowthFirstGuestRules.Preparation.cs",
				"Experience/KingdomGrowthFirstGuestRules.Validation.cs"
			};
			string source = "";
			for (int i = 0; i < files.Length; i++) source += Source(files[i]);
			return source;
		}

		private static string Source(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static void AssertOrdered(string source, params string[] needles)
		{
			int previous = -1;
			for (int i = 0; i < needles.Length; i++)
			{
				int current = source.IndexOf(needles[i], previous + 1,
					StringComparison.Ordinal);
				Assert.Greater(current, previous, needles[i]);
				previous = current;
			}
		}
	}
}
#endif
