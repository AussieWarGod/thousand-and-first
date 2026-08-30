#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomRealmRetirementSourceTests
	{
		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		[Test]
		public void CentralWorkFenceBlocksNewAutomaticAndWakeWork()
		{
			string master = Read("Core/KingdomMaster.cs");
			StringAssert.Contains(
				"return system != null && !system.LoadFailed && !system.RealmRetirementBlocksWork;",
				master, "the centralized root gate must carry the removal fence");
			string[] methods = { "ObserveAutomaticWake", "NewWorkAllowed", "AutomaticWorkAllowed" };
			for (int i = 0; i < methods.Length; i++)
			{
				int start = master.IndexOf(methods[i], StringComparison.Ordinal);
				int next = i + 1 < methods.Length
					? master.IndexOf(methods[i + 1], start, StringComparison.Ordinal)
					: master.IndexOf("private static bool RootAuthorityAvailable", start,
						StringComparison.Ordinal);
				string body = master.Substring(start, next - start);
				int root = body.IndexOf("RootAuthorityAvailable(system)",
					StringComparison.Ordinal);
				int options = body.IndexOf("ConfiguredEnabled", StringComparison.Ordinal);
				Assert.That(root, Is.GreaterThanOrEqualTo(0), methods[i]);
				Assert.That(options, Is.GreaterThan(root),
					"removal root gate must precede option/latch observation in " + methods[i]);
			}
		}

		[Test]
		public void CleanupUsesOnlyExactActiveVisitedGround()
		{
			string family = Family("Core/KingdomRealmRetirementGround.");
			StringAssert.Contains("ReferenceEquals(Zone, The.Player?.CurrentZone)", family);
			StringAssert.Contains("ReferenceEquals(Plan.Zone, The.Player?.CurrentZone)", family);
			StringAssert.DoesNotContain("GetZone(", family);
			StringAssert.DoesNotContain("RequireZone", family);
			StringAssert.DoesNotContain("LoadZone", family);
			StringAssert.DoesNotContain("ZoneManager.GetZone", family);
		}

		[Test]
		public void DestructiveGroundStepHasFrozenCapacityAndOwnershipPreflight()
		{
			string drive = Read("Core/KingdomRealmRetirementGround.Drive.cs");
			AssertBefore(drive, "TryAuthorizeRecords(State, plan", "TryApply(System, plan");
			AssertBefore(drive, "PublishPreMutationDisclosures", "TryApply(System, plan");
			string mutation = Read("Core/KingdomRealmRetirementGround.Mutation.cs");
			AssertBefore(mutation, "TryRevalidate(System, Plan", "TryRetireForRealmRemoval");
			AssertBefore(mutation, "TryRevalidate(System, Plan", "SetBlueprint");
			AssertBefore(mutation, "TryRemoveExperienceProjections(item", "SetBlueprint");
			StringAssert.DoesNotContain("RemovePart(", mutation,
				"callback-bearing carriers are residue, never generic pre-fence cuts");
			StringAssert.Contains("TryRemoveExperienceProjections", mutation);
			string preflight = Read("Core/KingdomRealmRetirementGround.Preflight.cs");
			StringAssert.Contains("CarrierDisposition(name)", preflight);
			StringAssert.Contains("TryInspectPlayer", preflight);
			StringAssert.Contains("CanRemoveExperienceProjections(item", preflight);
			string authorization = Read("Core/KingdomRealmRetirementGround.Authorization.cs");
			StringAssert.Contains("SameExternalOwnership(Expected.ExternalOwnership,",
				authorization);
			StringAssert.Contains("TryRecord(preview, preview.Revision", authorization);
			StringAssert.Contains("taf:ground-preview:", authorization);
			StringAssert.DoesNotContain("ground-object-preview:", authorization);
			StringAssert.Contains("ObjectRosterRow(Item, blueprint, true, true)", authorization);
			string experience = Read("Core/KingdomRealmRetirementGround.Experience.cs");
			StringAssert.Contains("KingdomOfficeRuntime.CanRemoveForRealmRemoval", experience);
			StringAssert.Contains("KingdomOfficeRuntime.TryRemoveForRealmRemoval", experience);
			StringAssert.Contains("KingdomRemembranceRuntime.CanRemoveForRealmRemoval", experience);
			StringAssert.Contains("KingdomRemembranceRuntime.TryRemoveForRealmRemoval", experience);
			string witness = Read("Core/KingdomRealmRetirementGround.WitnessWork.cs");
			StringAssert.Contains("KingdomWitnessWorkLease.TryReadAuthority", witness);
			StringAssert.Contains("KingdomWitnessWorkProjectionRuntime.TryObserve", witness);
			StringAssert.Contains("ConstructionOwned.Contains(carrier)", witness);
			StringAssert.Contains("ReferenceEquals(Plan.WitnessWorks[j].Carrier, carrier)", witness);
			StringAssert.Contains("ReferenceEquals(Plan.WitnessWorks[j].Marker, marker)", witness);
			AssertBefore(witness, "KingdomWitnessWorkCommit.TryReconcile",
				"KingdomWitnessWorkProjectionRuntime.TryDetach");
			StringAssert.Contains("current.Phase != KingdomWitnessWorkPhase.Removed", witness);
			StringAssert.Contains("action.Carrier.GetPart<r_KingdomWitnessWorkProjection>() != null",
				witness);
			StringAssert.DoesNotContain("KingdomSurvey.Take", witness,
				"retirement preflight builds a local read-only witness index");
			string reset = Read("Core/KingdomExternalOwnership.Reset.cs");
			AssertBefore(reset, "ResetSnapshotExact(Site, Plan)",
				"ClearPreparedRealmReset(Site");
		}

		[Test]
		public void FinalCleanupCommitsPreviewBeforeProjectionMutationAndFenceLast()
		{
			string finalize = Read("Core/KingdomRealmRetirementAuthority.Finalize.cs");
			AssertBefore(finalize, "plan.PreviewRecords", "TryRetireRecoveryQuests");
			AssertBefore(finalize, "TryCloseKnownProjections", "TryCommitRemovalFence");
			AssertBefore(finalize, "TryCommitRemovalFence", "TryCutTerminalProjections");
			AssertBefore(finalize, "KingdomRealmRetirementPhase.PreparedForRemoval",
				"TryCutTerminalProjections");
			AssertBefore(finalize, "TryPublish(System, state, prepared",
				"TryVerifyPreparedRemoval(System, state");
			AssertBefore(finalize, "TryVerifyPreparedRemoval(System, state",
				"TryCutTerminalProjections(System, state");
			AssertBefore(finalize, "TryVerifyPreparedRemoval(System", "RemoveSystem(System)");
			StringAssert.DoesNotContain("TryReconcile(System", finalize);
			AssertBefore(finalize, "TryCutTerminalProjections", "TryRemovePlayerProjection");
			AssertBefore(finalize, "TryClearForPreparedRemoval", "TryRemoveAuxiliarySystems");
			StringAssert.Contains("no mod-absent cleanup or clean uninstall is promised",
				finalize);
			string player = Read("Core/KingdomRemovalProjectionRuntime.Player.cs");
			StringAssert.DoesNotContain("RestoreCharterProjection", player);
			StringAssert.Contains("PlayerProjectionAbsent", player);
			StringAssert.Contains("TryAuthenticatePlayerCutProgress", player);
			StringAssert.Contains("ActivatedAbilityID", player);
			StringAssert.DoesNotContain("charter.RemoveAbility()", player);
			StringAssert.Contains("TryInspectPlayerObjectGraph", player);
			StringAssert.Contains("GetInventoryAndEquipment", player);
			StringAssert.Contains("row.Value.ID != row.Key", player);
			StringAssert.Contains(
				"!ReferenceEquals(row.Value.Abilities, Player.ActivatedAbilities)", player);
			int completeCut = player.IndexOf("charters.Count != 1 || ids.Count != 1",
				StringComparison.Ordinal);
			int indexedCut = player.IndexOf("charters[0].ActivatedAbilityID",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(completeCut, 0);
			Assert.Greater(indexedCut, completeCut,
				"typed Charter and command cardinality must be proved before indexing");
			string proof = Read("Core/KingdomIdentityFenceRuntime.RemovalProof.cs");
			StringAssert.DoesNotContain("TryWriteRaw", proof);
			StringAssert.DoesNotContain("Initialize(", proof);
			StringAssert.Contains("fence.NextRealmIncarnation != incarnation", proof);
			StringAssert.Contains("PreparedProofMatches(fence, State", proof);
			StringAssert.Contains("Tombstone(Fence.PreparedFromDigest",
				Read("Core/KingdomIdentityFenceReceiptRules.cs"));
		}

		[Test]
		public void CallbackAndTerminalCutsUseFrozenRowReceiptsAcrossReload()
		{
			string receipts = Read(
				"Core/KingdomRealmRetirementAuthority.CallbackReceipts.cs");
			foreach (string family in new[] { "quests", "recipes", "journal",
				"civic-semantics", "factions", "systems", "global-state" })
				StringAssert.Contains("Slug == \"" + family + "\"", receipts);
			StringAssert.Contains("CallbackAttemptPrefix", receipts);
			StringAssert.Contains("CallbackRowPrefix", receipts);
			StringAssert.Contains("TryFrozenCutRows", receipts);
			StringAssert.Contains("CutProgress", receipts);
			string finalize = Read("Core/KingdomRealmRetirementAuthority.Finalize.cs");
			foreach (string family in new[] { "quests", "recipes", "journal",
				"civic-semantics", "factions" })
				StringAssert.Contains("CallbackFamilySettled(System, State, \"" + family + "\"",
					finalize);
			StringAssert.Contains("frozenSystems", finalize);
			StringAssert.Contains("frozenGlobals", finalize);
		}

		[Test]
		public void GroundRosterExcludesPlayerCustodyAndPublishesEveryIdentityFirst()
		{
			string preflight = Read("Core/KingdomRealmRetirementGround.Preflight.cs");
			StringAssert.Contains("PlayerCustody(out Failure)", preflight);
			int graph = preflight.IndexOf("private static bool TryObjectGraph",
				StringComparison.Ordinal);
			int custody = preflight.IndexOf("private static HashSet<GameObject> PlayerCustody",
				StringComparison.Ordinal);
			StringAssert.DoesNotContain("pending.Enqueue(The.Player)",
				preflight.Substring(graph, custody - graph));
			string ownership = Read("Core/KingdomRealmRetirementGround.Ownership.cs");
			StringAssert.Contains("TryClassifyOwnedObject", ownership);
			string construction = Read(
				"Core/KingdomRealmRetirementGround.Ownership.Construction.cs");
			StringAssert.Contains("KingdomConstruction.ReceiptProperty", construction);
			StringAssert.Contains("job.OutputId == Item.IDIfAssigned", construction);
			StringAssert.Contains("TryReadOwner(root", construction);
			StringAssert.Contains("TryVerifyArchitectureReadOnly", construction);
			StringAssert.DoesNotContain("TryVerifyComplete(", construction);
			StringAssert.DoesNotContain("Quarantine(", construction);
			StringAssert.DoesNotContain("SetIntProperty(", construction);
			StringAssert.DoesNotContain("SetStringProperty(", construction);
			StringAssert.DoesNotContain("Repair", construction);
			string drive = Read("Core/KingdomRealmRetirementGround.Drive.cs");
			AssertBefore(drive, "PublishPreMutationDisclosures", "TryApply(System, plan");
			StringAssert.Contains("Plan.ObjectPreviewRecords", drive);
			AssertBefore(drive, "TryApply(System, plan", "PublishObjectCompletions");
		}

		[Test]
		public void TerminalCallbacksTreatNativeAbsenceAsTheOnlyCommittedOutcome()
		{
			string player = Read("Core/KingdomRemovalProjectionRuntime.Player.cs");
			StringAssert.Contains("catch (Exception ex)", player);
			StringAssert.DoesNotContain("Player.AddPart(charter)", player);
			StringAssert.DoesNotContain("abilitySnapshot", player);
			string global = Read("Core/KingdomRemovalProjectionRuntime.Global.cs");
			StringAssert.DoesNotContain("registrySnapshot", global);
			StringAssert.DoesNotContain("candidate.Removed = false", global);
			StringAssert.Contains("The.Game.Systems.Contains(candidate)", global);
			string final = Read("Core/KingdomRealmRetirementAuthority.Finalize.cs");
			AssertBefore(final, "catch (Exception ex)", "The.Game.Systems.Contains(System)");
			StringAssert.Contains("registry absence is the terminal result", final);
		}

		[Test]
		public void CivicSemanticsAndHolyPlacesHaveExplicitTerminalEvidence()
		{
			string civic = Read("Core/KingdomRemovalProjectionRuntime.CivicSemantics.cs");
			StringAssert.Contains("TryRetireCivicVoices", civic);
			StringAssert.Contains("TryRetireFirstFeasts", civic);
			AssertBefore(civic, "TryPrepareCivicMemoryNotes(System, Tick, notes",
				"TryRetireCivicVoices(System.Experience");
			AssertBefore(civic, "TryPrepareCivicMemoryNotes(System, Tick, notes",
				"TryPublishNativeCivicNotes(notes");
			StringAssert.Contains("AddToList = listed == null", civic,
				"an exact list-only or index-only prefix must be repairable on retry");
			StringAssert.Contains("AddToIndex = !indexed", civic);
			StringAssert.Contains("if (plan.AddToList) JournalAPI.Observations.Add", civic);
			StringAssert.Contains("if (plan.AddToIndex) JournalAPI.NotesByID.Add", civic);
			AssertBefore(civic, "JournalAPI.NotesByID.Add(plan.Note.ID, plan.Note)",
				"TryPlanNativeCivicNote(plan.Note");
			StringAssert.Contains("publication stopped after an exact prefix", civic);
			StringAssert.Contains("A.Time == B.Time", civic);
			StringAssert.Contains("A.Category == B.Category", civic);
			StringAssert.Contains("A.RevealText == B.RevealText", civic);
			StringAssert.Contains("A.Rumor == B.Rumor", civic);
			StringAssert.Contains("A.ID == B.ID", civic);
			StringAssert.Contains("A.History == B.History", civic);
			StringAssert.Contains("A.Text == B.Text", civic);
			StringAssert.Contains("A.LearnedFrom == B.LearnedFrom", civic);
			StringAssert.Contains("A.Weight == B.Weight", civic);
			StringAssert.Contains("A.Revealed == B.Revealed", civic);
			StringAssert.Contains("A.Tradable == B.Tradable", civic);
			StringAssert.Contains("SameAttributes(A.Attributes, B.Attributes)", civic);
			StringAssert.Contains("JournalObservation", civic);
			StringAssert.DoesNotContain("JournalSultanNote", civic);
			StringAssert.DoesNotContain("JournalAPI.SultanNotes", civic);
			StringAssert.DoesNotContain("JournalGeneralNote", civic);
			StringAssert.DoesNotContain("JournalAPI.GeneralNotes", civic);
			StringAssert.Contains("LearnedFrom = \"Retired realms\"", civic);
			StringAssert.Contains("History = \"\"", civic,
				"native History is displayed verbatim and must not carry archive bytes");
			StringAssert.Contains("NativeArchiveAttribute(wire)", civic,
				"the exact row stays serialized outside the player-facing display text");
			StringAssert.Contains("NativeArchiveAttributePrefix", civic);
			string readable = Read("Core/KingdomRemovalProjectionRuntime.CivicNoteText.cs");
			StringAssert.Contains("TryPrepareExperienceNotes", readable);
			StringAssert.Contains("KingdomPresentation.Rich", readable);
			StringAssert.Contains("named memorial", readable);
			StringAssert.Contains("creed declaration", readable);
			StringAssert.Contains("adopted as a private practice", readable);
			StringAssert.DoesNotContain("Append(Row.Replace", readable);
			string c18 = Read("Core/KingdomRemovalProjectionRuntime.CivicMemory.cs");
			StringAssert.Contains("FirstKnownSection", c18);
			StringAssert.Contains("LastKnownSection", c18);
			StringAssert.Contains("Convert.ToBase64String(section.Payload())", c18);
			StringAssert.Contains("status = present ? \"present\" : \"absent\"", c18);
			StringAssert.Contains("TryReconcileCarrier(next.WitnessWorks", c18);
			StringAssert.Contains("if (PendingWitnessRows > 0)", c18);
			StringAssert.Contains("SectionCivicArtifacts", c18);
			StringAssert.Contains("TryValidateWitnessRetirementLocators", c18);
			StringAssert.Contains("live fixed-witness row lies outside attended retirement ground",
				c18);
			StringAssert.Contains("state.IsFutureOuter", c18);
			StringAssert.Contains("state.HasFutureSections", c18);
			StringAssert.Contains("MaxCivicMemorySectionArchiveChars", c18);
			StringAssert.Contains("digest, 0, 1, payload", c18,
				"each numbered section gets one visible native note, not hundreds of chunks");
			StringAssert.Contains("History = \"\"", c18,
				"the exact base64 archive must not be rendered in the General journal");
			StringAssert.Contains("NativeArchiveAttribute(\"taf-c18-v1|\"", c18);
			StringAssert.DoesNotContain("CivicMemoryChunkChars", c18);
			string factions = Read("Core/KingdomRemovalProjectionRuntime.Factions.cs");
			StringAssert.Contains("FactionPreviewRow", factions);
			StringAssert.Contains("Faction.HolyPlaces", factions);
		}

		[Test]
		public void CharterExposesExplicitAttendedRemovalWithoutRemoteCleanupPromise()
		{
			string chapter = Read("Core/KingdomCharterPart.Chapters.cs");
			StringAssert.Contains("Prepare this save for mod removal", chapter);
			StringAssert.Contains("OpenRealmRemoval(System, The.Player)", chapter);
			string ui = Read("Core/KingdomCharterPart.RealmRemoval.cs");
			StringAssert.Contains("TryInspect(System", ui);
			StringAssert.Contains("TryBegin(System", ui);
			StringAssert.Contains("TryCleanActiveGround(System, Zone", ui);
			StringAssert.Contains("TryFinalizeForRemoval(System", ui);
			StringAssert.Contains("KingdomRealmRetirementPhase.PreparedForRemoval", ui,
				"a failed terminal cut must remain Charter-reachable");
			StringAssert.Contains("There is no remote cleanup", ui);
			StringAssert.Contains("not a clean-uninstall promise", ui);
			StringAssert.Contains("SAVE IMMEDIATELY, QUIT", ui);
			StringAssert.DoesNotContain("GetZone(", ui);
			StringAssert.DoesNotContain("RequireZone", ui);
			StringAssert.DoesNotContain("LoadZone", ui);
		}

		[Test]
		public void TerminalRetryProofsBindReceiptRowsAndReserveFenceCapacity()
		{
			string fence = Read("Core/KingdomIdentityFenceRuntime.cs");
			StringAssert.Contains("fence.PreparedReceiptDigest != receiptDigest", fence);
			int prepared = fence.IndexOf(
				"else if (fence.Disposition == KingdomIdentityFenceDisposition.PreparedForRemoval)",
				StringComparison.Ordinal);
			string branch = fence.Substring(prepared, fence.IndexOf("else return Fail", prepared,
				StringComparison.Ordinal) - prepared);
			StringAssert.DoesNotContain("Tombstone(", branch,
				"retry must not feed the committed tombstone back as its own predecessor");
			string plan = Read("Core/KingdomRealmRetirementAuthority.FinalPlan.cs");
			StringAssert.Contains("KingdomRemovalDisposition.TerminalIntent", plan);
			StringAssert.DoesNotContain("TryBindFamilyRows(State", plan);
			StringAssert.Contains("preview.BeforeDigest != liveDigest", plan);
			StringAssert.Contains("preview.Amount != Rows.Count", plan);
			StringAssert.Contains("FenceCapacityReserved", plan);
			string begin = Read("Core/KingdomRealmRetirementAuthority.Plan.cs");
			AssertBefore(begin, "TryBuildFinalPlan(System, disclosed",
				"KingdomRealmRetirementCodec.Encode(State)");
			AssertBefore(begin, "KingdomRealmRetirementCodec.Encode(State)",
				"seal.TryPrepareRealmRemoval");
			StringAssert.Contains("the profile seal was made quiescent before publication", begin);
		}

		[Test]
		public void RemovalInspectionUsesReadOnlyFenceProof()
		{
			string inspection = Read("Core/KingdomRealmRetirementAuthority.Inspection.cs");
			StringAssert.Contains("KingdomIdentityFenceRuntime.TryVerify", inspection);
			StringAssert.DoesNotContain("KingdomIdentityFenceRuntime.TryReconcile", inspection);
			string fence = Read("Core/KingdomIdentityFenceRuntime.cs");
			int verify = fence.IndexOf("public static bool TryVerify", StringComparison.Ordinal);
			int reconcile = fence.IndexOf("public static bool TryReconcile", StringComparison.Ordinal);
			Assert.That(verify, Is.GreaterThanOrEqualTo(0));
			Assert.That(reconcile, Is.GreaterThan(verify));
			string body = fence.Substring(verify, reconcile - verify);
			StringAssert.DoesNotContain("TryWriteRaw", body);
			StringAssert.DoesNotContain("Initialize(", body);
			StringAssert.DoesNotContain("RealmIdentityFenceFault =", body);
		}

		[Test]
		public void PlanningFreezesZeroFamiliesAndRejectsValueOrPlayerCustody()
		{
			string inspection = Read("Core/KingdomRealmRetirementAuthority.Inspection.cs");
			StringAssert.Contains("state.Phase != KingdomInheritancePhase.Empty", inspection);
			AssertBefore(inspection, "TryInspectPlayer", "TryBuildLocators");
			string plan = Read("Core/KingdomRealmRetirementAuthority.Plan.cs");
			AssertBefore(plan, "TryBuildFinalPlan(System, disclosed", "TrySetPhase(frozen");
			string finalPlan = Read("Core/KingdomRealmRetirementAuthority.FinalPlan.cs");
			StringAssert.Contains("TryInspectRoster", finalPlan);
			StringAssert.Contains("State.Phase == KingdomRealmRetirementPhase.Planning",
				finalPlan);
			StringAssert.Contains("for (int row = 0; row < 4; row++)", finalPlan);
			string globals = Read("Core/KingdomRemovalProjectionRuntime.Global.cs");
			StringAssert.Contains("GlobalDisposition(row.Key)", globals);
			StringAssert.DoesNotContain("CollectKeys(The.Game.IntGameState", globals);
			string dispositions = Read("Core/KingdomRemovalCoverage.Dispositions.cs");
			StringAssert.Contains("KingdomRemovalGlobalDisposition.Preserve", dispositions);
			StringAssert.Contains("KingdomRemovalCarrierDisposition.PreserveResidue", dispositions);
		}

		[Test]
		public void SuccessionAndCivicProjectionsKeepTheirExactRemovalOwners()
		{
			string rootInspection = Read(
				"Core/KingdomRealmRetirementAuthority.Inspection.cs");
				StringAssert.Contains("InspectPolityAndExperience(System, Report)", rootInspection);
			StringAssert.Contains("TryDescribeRealmRemovalBlocker(System?.Experience",
				rootInspection);
			string inspection = Read(
				"Core/KingdomRealmRetirementAuthority.Inspection.Projections.cs");
			StringAssert.Contains("succession.TryDescribeRealmRemovalBlocker", inspection);
			StringAssert.Contains("Succession authority is not quiescent", inspection);
			string coverage = Read("Core/KingdomRemovalCoverage.cs");
			StringAssert.Contains("\"r_KingdomLocusAmbient\"", coverage);
			StringAssert.Contains("\"r_KingdomOfficeProjection\"", coverage);
			StringAssert.Contains("\"r_KingdomRemembranceProjection\"", coverage);
		}

		[Test]
		public void SourceInventoryExactlyMatchesSerializableCoverageRegistry()
		{
			Dictionary<string, HashSet<string>> bases = ProductionClassBases();
			HashSet<string> parts = Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"IPart", "TeleporterPair"
			}, name => name == "KingdomCharterPart" || name.StartsWith("r_Kingdom",
				StringComparison.Ordinal) || name.StartsWith("r_Founder", StringComparison.Ordinal));
			AssertSet(parts, Registry("CustomParts"), "custom object parts");
			AssertSet(Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"IGameSystem", "IPlayerSystem"
			}, name => name.StartsWith("Kingdom", StringComparison.Ordinal)),
				Registry("CustomSystems"), "custom game systems");
			AssertSet(Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"IZonePart"
			}, name => name.StartsWith("Kingdom", StringComparison.Ordinal)),
				Registry("CustomZoneParts"), "custom zone parts");
			AssertSet(Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"IGameStateSingleton"
			}, name => name.StartsWith("Kingdom", StringComparison.Ordinal)),
				Registry("CustomGameStateSingletons"), "game-state singletons");
			AssertSet(Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"CookingRecipe"
			}, name => name.StartsWith("r_Kingdom", StringComparison.Ordinal)),
				Registry("CustomCookingRecipes"), "cooking recipes");
			AssertSet(Derived(bases, new HashSet<string>(StringComparer.Ordinal)
			{
				"JournalSultanNote"
			}, name => name.StartsWith("r_Kingdom", StringComparison.Ordinal)),
				Registry("CustomJournalNotes"), "journal notes");
		}

		[Test]
		public void GeneratedCoverageAndGateAreMandatoryAndCollisionSafe()
		{
			string generator = Read("Tools/generate-removal-coverage.py");
			StringAssert.Contains("MANUAL_BLUEPRINTS", generator);
			StringAssert.Contains("MANUAL_OBJECT_PROPERTIES", generator);
			string gate = Read("Tools/gate.sh");
			StringAssert.Contains(
				"python3 \"$REPO/Tools/generate-removal-coverage.py\" --check", gate);
			string coverage = Read("Core/KingdomRemovalCoverage.cs");
			StringAssert.DoesNotContain("\"Kingdom\"", coverage);
			StringAssert.DoesNotContain("\"TAF\"", coverage);
			StringAssert.DoesNotContain("\"r_TAF_\"", coverage);
			StringAssert.DoesNotContain("StartsWith(\"r_Kingdom\"", coverage);
			string generated = Read("Core/KingdomRemovalCoverage.Generated.cs");
			StringAssert.DoesNotContain("\"r_KingdomCropBlueprint\"", generated,
				"a TAF-owned tag key is not an object blueprint and cannot authorize deletion");
		}

		[Test]
		public void RemovalProductionShardsStayBelowThreeHundredLines()
		{
			string root = TestMain.RepositoryRoot;
			string[] files = Directory.EnumerateFiles(Path.Combine(root, "Core"),
				"*RealmRetirement*.cs", SearchOption.TopDirectoryOnly)
				.Concat(Directory.EnumerateFiles(Path.Combine(root, "Core"),
					"*Removal*.cs", SearchOption.TopDirectoryOnly))
				.Concat(Directory.EnumerateFiles(Path.Combine(root, "Raids"),
					"*Removal*.cs", SearchOption.TopDirectoryOnly))
				.Distinct(StringComparer.Ordinal).ToArray();
			Assert.That(files.Length, Is.GreaterThan(10));
			for (int i = 0; i < files.Length; i++)
			{
				int lines = File.ReadAllLines(files[i]).Length;
				Assert.That(lines, Is.LessThan(300), Path.GetRelativePath(root, files[i]));
			}
		}

		private static Dictionary<string, HashSet<string>> ProductionClassBases()
		{
			Dictionary<string, HashSet<string>> result =
				new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
			Regex declaration = new Regex(@"\bclass\s+(?<name>[A-Za-z_]\w*)\s*:\s*(?<bases>[^\{\r\n]+)");
			foreach (string file in Directory.EnumerateFiles(TestMain.RepositoryRoot, "*.cs",
				SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(TestMain.RepositoryRoot, file);
				if (relative.StartsWith("DevTests" + Path.DirectorySeparatorChar,
					StringComparison.Ordinal) || relative.StartsWith("Tools" +
					Path.DirectorySeparatorChar, StringComparison.Ordinal)
					// Harness/ never ships (absent from manifest Directories, excluded from
					// staging), so its systems can never appear in a shipped save and must not
					// be demanded of the serializable-coverage registry.
					|| relative.StartsWith("Harness" + Path.DirectorySeparatorChar,
						StringComparison.Ordinal)
					|| relative.StartsWith("Integrations" + Path.DirectorySeparatorChar,
						StringComparison.Ordinal)) continue;
				string source = File.ReadAllText(file);
				foreach (Match match in declaration.Matches(source))
				{
					string name = match.Groups["name"].Value;
					if (!result.TryGetValue(name, out HashSet<string> values))
						result[name] = values = new HashSet<string>(StringComparer.Ordinal);
					foreach (string value in match.Groups["bases"].Value.Split(','))
					{
						string clean = value.Trim().Split('<')[0].Trim();
						int dot = clean.LastIndexOf('.');
						if (dot >= 0) clean = clean.Substring(dot + 1);
						if (clean.Length > 0) values.Add(clean);
					}
				}
			}
			return result;
		}

		private static HashSet<string> Derived(Dictionary<string, HashSet<string>> Bases,
			HashSet<string> Roots, Func<string, bool> Include)
		{
			HashSet<string> derived = new HashSet<string>(Roots, StringComparer.Ordinal);
			bool changed;
			do
			{
				changed = false;
				foreach (KeyValuePair<string, HashSet<string>> row in Bases)
					if (!derived.Contains(row.Key) && row.Value.Any(derived.Contains))
						changed |= derived.Add(row.Key);
			}
			while (changed);
			derived.RemoveWhere(name => Roots.Contains(name) || !Include(name));
			return derived;
		}

		private static HashSet<string> Registry(string Name)
		{
			string source = Read("Core/KingdomRemovalCoverage.cs");
			Match array = Regex.Match(source, @"\b" + Regex.Escape(Name)
				+ @"\s*=\s*new\s+string\[\]\s*\{(?<body>.*?)\};",
				RegexOptions.Singleline);
			Assert.That(array.Success, Is.True, Name);
			return new HashSet<string>(Regex.Matches(array.Groups["body"].Value,
				"\"(?<value>[^\"]+)\"").Cast<Match>().Select(match =>
					match.Groups["value"].Value), StringComparer.Ordinal);
		}

		private static void AssertSet(HashSet<string> Expected, HashSet<string> Actual,
			string Label)
		{
			Assert.That(Actual.OrderBy(value => value, StringComparer.Ordinal),
				Is.EqualTo(Expected.OrderBy(value => value, StringComparer.Ordinal)), Label);
		}

		private static string Family(string Prefix)
		{
			string root = TestMain.RepositoryRoot;
			string directory = Path.GetDirectoryName(Prefix).Replace('/',
				Path.DirectorySeparatorChar);
			string filePrefix = Path.GetFileName(Prefix);
			return string.Join("\n", Directory.EnumerateFiles(Path.Combine(root, directory),
				filePrefix + "*.cs").OrderBy(path => path, StringComparer.Ordinal)
				.Select(File.ReadAllText));
		}

		private static void AssertBefore(string Source, string Before, string After)
		{
			int first = Source.IndexOf(Before, StringComparison.Ordinal);
			int second = Source.IndexOf(After, StringComparison.Ordinal);
			Assert.That(first, Is.GreaterThanOrEqualTo(0), Before);
			Assert.That(second, Is.GreaterThan(first), After);
		}

		private static int Occurrences(string Source, string Value)
		{
			int count = 0; int at = 0;
			while ((at = Source.IndexOf(Value, at, StringComparison.Ordinal)) >= 0)
			{
				count++; at += Value.Length;
			}
			return count;
		}
	}
}
#endif
