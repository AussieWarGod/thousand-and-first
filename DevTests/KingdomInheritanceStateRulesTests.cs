#if TAF_TESTS
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomInheritanceStateRulesTests
	{
		private static KingdomSealRecord Legacy()
		{
			return new KingdomSealRecord
			{
				Status = KingdomSealStatus.Promoted,
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				InterregnumRoll = 17,
				InheritedState = (int)KingdomRules.InheritedState.Held
			};
		}

		private static KingdomSealReceipt Receipt(KingdomSealRecord Legacy)
		{
			return new KingdomSealReceipt
			{
				LineageId = Legacy.LineageId,
				LegacyId = Legacy.LegacyId,
				TargetGameId = "target-game",
				State = KingdomSealReceiptState.Reserved,
				WrittenTick = 321L
			};
		}

		private static KingdomSealRecord CanonicalLegacy()
		{
			KingdomSealRecord record = new KingdomSealRecord
			{
				WriterVersion = "test",
				EngineVersion = "test",
				Status = KingdomSealStatus.Living,
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				OriginGameId = "origin.game",
				Generation = 1,
				Revision = 7,
				WrittenTick = 100L,
				FounderName = "Abram",
				RealmName = "Old Realm",
				SettlementName = "Old Seat",
				SettlementId = "old-seat",
				Vocation = "holding",
				Style = "common",
				FoundedTick = 10L,
				GroundZoneId = "JoppaWorld.1.1.1.1.10",
				RegionName = "Salt",
				TerrainBlueprint = "TerrainSaltMarsh",
				Depth = 10,
				Stage = (int)GrowthStage.Camp,
				Population = 2,
				Defence = 1,
				StoredWater = 5
			};
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage,
				record.Population, record.Defence, record.StoredWater, record.Withered);
			return KingdomSealRules.PromoteRetirement(KingdomSealRules.WithRetirement(
				KingdomSealTestIdentity.Bind(record)));
		}

		private static KingdomInheritanceSavedShape PendingShape()
		{
			KingdomSealRecord legacy = CanonicalLegacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			Assert.IsTrue(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			return new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.AppliedPendingDurability,
				LegacyText = legacy.Compose(),
				ReceiptText = receipt.Compose(),
				TargetZoneId = "JoppaWorld.4.5.1.2.10",
				TargetTerrainBlueprint = "TerrainSaltMarsh",
				TargetTerrainRank = 0,
				SecretId = "taf.inherit." + legacy.LegacyId,
				SiteName = KingdomInheritanceStateRules.ComposeSiteName(legacy),
				ApplyStatus = (int)KingdomInheritApplyStatus.Applied,
				ApplyFault = (int)KingdomInheritApplyFault.None,
				ApplicationMarker = marker,
				OwnsSkipTerrainBuilders = true,
				OwnsNoBiomes = true,
				OwnsZoneName = true
			};
		}

		[Test]
		public void MarkerFormatHasOneCanonicalImplementation()
		{
			KingdomSealRecord legacy = Legacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			Assert.IsTrue(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			Assert.AreEqual("taf-inherit-v1|lineage-a|legacy-a|target-game|reserved|321|"
				+ "JoppaWorld.4.5.1.2.10", marker);
		}

		[Test]
		public void UnsupportedSerializationHeaderMustTakeThrowAndSkipBlockPath()
		{
			const int magic = 1413568073;
			Assert.IsTrue(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 1, magic, 4));
			Assert.IsTrue(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 4, magic, 4));
			Assert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 0, magic, 4));
			Assert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic, 5, magic, 4));
			Assert.IsFalse(KingdomInheritanceStateRules.IsSupportedSerializationHeader(
				magic + 1, 4, magic, 4));
		}

		[Test]
		public void MarkerRejectsCommittedOrMismatchedReceipt()
		{
			KingdomSealRecord legacy = Legacy();
			KingdomSealReceipt receipt = Receipt(legacy);
			string marker;
			receipt.State = KingdomSealReceiptState.Committed;
			Assert.IsFalse(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
			receipt.State = KingdomSealReceiptState.Reserved;
			receipt.LegacyId = "another-legacy";
			Assert.IsFalse(KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy,
				receipt, "JoppaWorld.4.5.1.2.10", 1, out marker));
		}

		[Test]
		public void LaterLoadDurabilityUsesPhaseAndMarkersNotMutableObjects()
		{
			const string marker = "taf-inherit-v1|lineage-a|legacy-a|target-game|reserved|321|zone";
			Assert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, marker, false));
			Assert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.AlreadyApplied, true, marker, marker, marker, false));
			// There is deliberately no live-object count or object-state argument: initial Apply owns
			// that proof, and later player movement, filling, or destruction cannot revoke durability.
		}

		[Test]
		public void TornOrUnownedMarkerProofFailsClosed()
		{
			const string marker = "expected";
			Assert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, false, marker, marker, marker, false));
			Assert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.AppliedPendingDurability,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, "different", false));
			Assert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.Installed,
				(int)KingdomInheritApplyStatus.Applied, true, "", marker, marker, false));
			Assert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.Installed,
				(int)KingdomInheritApplyStatus.Applied, true, "", marker, marker, true));
			Assert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Failed, true, marker, marker, marker, false));
			Assert.IsTrue(KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
				(int)KingdomInheritApplyStatus.Applied, (int)KingdomInheritApplyFault.None,
				marker));
			Assert.IsFalse(KingdomInheritanceStateRules.RetainsDurableApplicationCandidate(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, marker));
		}

		[Test]
		public void ControlledRetryRequiresFirstTryFailureAndExactCleanup()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 1, true));
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 1, false));
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Failed, 2, true));
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldRetryBuild(
				KingdomInheritApplyStatus.Refused, 1, true));
			Assert.IsTrue(KingdomInheritanceStateRules.CanTransition(
				KingdomInheritancePhase.RepairRequired,
				KingdomInheritancePhase.AppliedPendingDurability));
		}

		[Test]
		public void CleanupDescriptorPreservesForeignSameClassPayloads()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "legacy", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "foreign", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactSiteBuilder(
				"KingdomInheritedSiteBuilder", "legacy", "target", "zone", 2,
				"legacy", "target", "zone", 1));
			Assert.IsTrue(KingdomInheritanceStateRules.IsExactLocationFinder(
				"AddLocationFinder", "secret", 1, "secret"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactLocationFinder(
				"AddLocationFinder", "secret", 0, "secret"));
			Assert.IsTrue(KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
				"KingdomInheritanceLocationFinderBuilder", "legacy", "target", "zone", 1,
				"legacy", "target", "zone", 1));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
				"KingdomInheritanceLocationFinderBuilder", "foreign", "target", "zone", 1,
				"legacy", "target", "zone", 1));
		}

		[Test]
		public void SavedShapeAcceptsExactPendingAndCommittedStates()
		{
			KingdomInheritanceSavedShape pending = PendingShape();
			string failure;
			Assert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(pending,
				"target-game", 1, out failure), failure);

			KingdomSealReceipt committed = new KingdomSealReceipt
			{
				LineageId = "lineage-a",
				LegacyId = "legacy-a",
				TargetGameId = "target-game",
				State = KingdomSealReceiptState.Committed,
				WrittenTick = 400L
			};
			pending.PhaseValue = (int)KingdomInheritancePhase.Committed;
			pending.CommittedReceiptText = committed.Compose();
			Assert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(pending,
				"target-game", 1, out failure), failure);
		}

		[Test]
		public void SavedShapeRejectsCorruptOwnershipStatusAndFault()
		{
			string failure;
			KingdomInheritanceSavedShape shape = PendingShape();
			shape.OwnsNoBiomes = false;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Failed;
			shape.ApplyFault = (int)KingdomInheritApplyFault.PartialApplication;
			shape.ApplicationMarker = "";
			shape.ReleasePending = true;
			shape.SiteName = "Foreign Exact Name";
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure),
				"saved cleanup authority cannot redefine the deterministic owned site name");

			shape = PendingShape();
			shape.OwnsZoneName = false;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.ApplyStatus = 999;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));

			shape = PendingShape();
			shape.ApplyFault = 999;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure));
		}

		[Test]
		public void UnbuiltInstallRepairPersistsCleanupThenReleaseIntent()
		{
			KingdomInheritanceSavedShape shape = PendingShape();
			shape.PhaseValue = (int)KingdomInheritancePhase.RepairRequired;
			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Failed;
			shape.ApplyFault = (int)KingdomInheritApplyFault.PartialApplication;
			shape.ApplicationMarker = "";
			shape.ReleasePending = true;
			string failure;
			Assert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure), failure);

			shape.ApplyStatus = (int)KingdomInheritApplyStatus.Applied;
			shape.ApplyFault = (int)KingdomInheritApplyFault.None;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(shape,
				"target-game", 1, out failure),
				"release intent can never coexist with successful application evidence");
		}

		[Test]
		public void FallbackNeverDropsRepairBuildersBeforeZoneQuarantine()
		{
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				false, false), "unclean application retains its exact repair machinery");
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				true, true), "externally committed application can never enter release cleanup");
			Assert.IsTrue(KingdomInheritanceStateRules.ShouldAttemptFallbackArtifactCleanup(
				true, false));
			Assert.IsTrue(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				true, false, false));
			Assert.IsFalse(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				false, false, false));
			Assert.IsFalse(KingdomInheritanceStateRules.MustPersistFallbackReleaseIntent(
				true, true, false));
		}

		[Test]
		public void InvalidOrTruncatedShapeCanOnlyNormalizeToAuthorityFreeQuarantine()
		{
			string failure;
			KingdomInheritanceSavedShape disabled = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.RepairRequired,
				RecoveryDisabled = true
			};
			Assert.IsTrue(KingdomInheritanceStateRules.TryValidateSavedShape(disabled,
				"target-game", 1, out failure), failure);
			disabled.TargetZoneId = "JoppaWorld.4.5.1.2.10";
			disabled.OwnsSkipTerrainBuilders = true;
			disabled.OwnsNoBiomes = true;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(disabled,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape empty = new KingdomInheritanceSavedShape
			{
				PhaseValue = (int)KingdomInheritancePhase.Empty,
				OwnsSkipTerrainBuilders = true,
				OwnsNoBiomes = true
			};
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(empty,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape committed = PendingShape();
			committed.PhaseValue = (int)KingdomInheritancePhase.Committed;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(committed,
				"target-game", 1, out failure));

			KingdomInheritanceSavedShape refused = PendingShape();
			refused.PhaseValue = (int)KingdomInheritancePhase.Refused;
			Assert.IsFalse(KingdomInheritanceStateRules.TryValidateSavedShape(refused,
				"target-game", 1, out failure));
		}

		[Test]
		public void LoadClassifierAcceptsBothRootsAndTypedRollbackSources()
		{
			string temp = Path.Combine(Path.GetTempPath(), "taf-inherit-source");
			string syncedRoot = Path.Combine(temp, "synced", "Saves");
			string localRoot = Path.Combine(temp, "local", "Saves");
			string synced = Path.Combine(syncedRoot, "target-game", "Primary");
			string local = Path.Combine(localRoot, "target-game", "Primary");
			string failure;
			Assert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L,
				out failure), failure);
			Assert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(local,
				localRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L,
				out failure), failure);
			Assert.AreEqual(KingdomInheritanceLoadKind.Primary,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(local,
				localRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				false, (FileAttributes)0, 0L, true, FileAttributes.Normal, 1L,
				out failure), failure);
			Assert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(syncedRoot, "target-game", "Quick"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(localRoot, "target-game", "Checkpoint"), localRoot,
					"target-game", FileAttributes.Directory, FileAttributes.Directory,
					false, (FileAttributes)0, 0L, true, FileAttributes.Normal, 1L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.SameGameRollback,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(localRoot, "target-game", "Precognition"), localRoot,
					"target-game", FileAttributes.Directory, FileAttributes.Directory,
					true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
				Path.Combine(syncedRoot, "target-game", "primary"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
				Path.Combine(syncedRoot, "TARGET-GAME", "Primary"), syncedRoot, "target-game",
				FileAttributes.Directory, FileAttributes.Directory, true, FileAttributes.Normal,
				1L, false, (FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced + ".sav",
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory, FileAttributes.Directory,
				true, FileAttributes.Normal, 0L, true, FileAttributes.Normal, 1L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(synced,
				syncedRoot, "target-game", FileAttributes.Directory | FileAttributes.ReparsePoint,
				FileAttributes.Directory, true, FileAttributes.Normal, 1L, false,
				(FileAttributes)0, 0L, out failure));
			Assert.AreEqual(KingdomInheritanceLoadKind.Unknown,
				KingdomInheritanceStateRules.ClassifyExactLoadSource(
					Path.Combine(syncedRoot, "target-game", "Coda"), syncedRoot, "target-game",
					FileAttributes.Directory, FileAttributes.Directory, true,
					FileAttributes.Normal, 1L, false, (FileAttributes)0, 0L, out failure));
		}

		[Test]
		public void LoadSourceTrackerIsAsyncLocalAndConsumeOnce()
		{
			KingdomInheritanceLoadSourceFlow.Clear();
			KingdomInheritanceLoadSourceFlow.Record("one");
			string path;
			Assert.IsTrue(KingdomInheritanceLoadSourceFlow.TryConsume(out path));
			Assert.AreEqual("one", path);
			Assert.IsFalse(KingdomInheritanceLoadSourceFlow.TryConsume(out path));

			Task<string> first = Task.Run(async delegate
			{
				KingdomInheritanceLoadSourceFlow.Record("first");
				await Task.Yield();
				string value;
				return KingdomInheritanceLoadSourceFlow.TryConsume(out value) ? value : "missing";
			});
			Task<string> second = Task.Run(async delegate
			{
				KingdomInheritanceLoadSourceFlow.Record("second");
				await Task.Yield();
				string value;
				return KingdomInheritanceLoadSourceFlow.TryConsume(out value) ? value : "missing";
			});
			Task.WaitAll(first, second);
			Assert.AreEqual("first", first.Result);
			Assert.AreEqual("second", second.Result);
		}

		[Test]
		public void ZoneNameAndReachabilityProofsAreExact()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "", true, "", true, "", true, true, "Old Seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "changed", true, "", true, "", true, true, "Old Seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsExactZoneNameFootprint("Old Seat",
				true, "", true, "", true, "", false, true, "Old Seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.MeetsReachability(399));
			Assert.IsTrue(KingdomInheritanceStateRules.MeetsReachability(400));
			Assert.IsFalse(KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(399,
				1200), "an isolated large pocket cannot replace entry-rooted reachability");
			Assert.IsTrue(KingdomInheritanceStateRules.CanTerminalizeHiddenFallback(400, 0));
		}

		[Test]
		public void ZoneNameOwnershipAcceptsEveryExactTornSubsetAndRejectsMismatch()
		{
			for (int mask = 0; mask < 32; mask++)
			{
				bool hasName = (mask & 1) != 0;
				bool hasContext = (mask & 2) != 0;
				bool hasProper = (mask & 4) != 0;
				bool hasIndefinite = (mask & 8) != 0;
				bool hasDefinite = (mask & 16) != 0;
				Assert.IsTrue(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
					hasName, hasName ? "Old Seat" : null,
					hasContext, hasContext ? "" : null,
					hasIndefinite, hasIndefinite ? "" : null,
					hasDefinite, hasDefinite ? "" : null,
					hasProper, hasProper, "Old Seat"), "exact torn subset mask " + mask);
			}
			Assert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				true, "Foreign", false, null, false, null, false, null, false, false,
				"Old Seat"));
			Assert.IsTrue(KingdomInheritanceStateRules.CanClearZoneNameOwnership(false),
				"post-write callback failure cannot outweigh exact five-key absence");
			Assert.IsFalse(KingdomInheritanceStateRules.CanClearZoneNameOwnership(true));
			Assert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				false, null, true, "changed", false, null, false, null, false, false,
				"Old Seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				false, null, false, null, false, null, false, null, true, false,
				"Old Seat"));
		}

		[Test]
		public void FinderRequiresCanonicalNonNullMapNoteCategoryAndText()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Settlements", "the old seat", "Settlements", "the old seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				null, "the old seat", "Settlements", "the old seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Lairs", "the old seat", "Settlements", "the old seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, true,
				"Settlements", null, "Settlements", "the old seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, false, true,
				"Settlements", "the old seat", "Settlements", "the old seat"));
			Assert.IsFalse(KingdomInheritanceStateRules.IsUsableOwnedMapNote(true, true, false,
				"Settlements", "the old seat", "Settlements", "the old seat"));
		}

		[Test]
		public void EmergencyCleanupRequiresExactOwnershipAndPropertiesBeforeBuildersLeave()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, true, true, true));
			Assert.IsTrue(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(3,
				1, 1, true, true, true),
				"unrelated foreign builders do not erase exact ownership");
			Assert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(3,
				2, 1, true, true, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 0, true, true, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, false, true, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanClaimEmergencyOwnership(2,
				1, 1, true, false, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				false, true, true), "builders must remain when cleanup tears");
			Assert.IsFalse(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				true, false, true), "properties must be absent before builder removal completes");
			Assert.IsTrue(KingdomInheritanceStateRules.CanRegenerateAfterEmergencyCleanup(
				true, true, true));
		}

		[Test]
		public void RepairAuthorityRequiresExactPreproof()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 0, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(false, 0, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 1, true));
			Assert.IsFalse(KingdomInheritanceStateRules.CanAuthorizeDirectRepair(true, 0, false));
		}

		[Test]
		public void CommittedReceiptSurvivesOldCheckpointCopyAndReconcilesOnPrimary()
		{
			Assert.IsTrue(KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(
				KingdomSealReceiptState.Committed),
				"Unknown source may defer target mutation but can never release a final receipt");
			Assert.IsFalse(KingdomInheritanceStateRules.ProfileReceiptBlocksRelease(
				KingdomSealReceiptState.Reserved));
			Assert.AreEqual(KingdomCommittedRewindAction.DeferUntilPrimary,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, false, false, false, true,
					true, false), "an uncommitted receipt still requires Primary");
			Assert.AreEqual(KingdomCommittedRewindAction.DeferUntilPrimary,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.Unknown, true, false, true, true, true, true));
			Assert.AreEqual(KingdomCommittedRewindAction.AwaitLazyBuilder,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, false, false, true,
					true, false));
			Assert.AreEqual(KingdomCommittedRewindAction.ReapplyCleanBuiltTarget,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, false, true, true,
					true, true), "the sole rollback event must reconstruct before archive copy");
			Assert.AreEqual(KingdomCommittedRewindAction.AdoptDurable,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.SameGameRollback, true, true, true, false,
					true, false), "external commit makes repeat rollback adoption idempotent");
			Assert.AreEqual(KingdomCommittedRewindAction.RepairRequired,
				KingdomInheritanceStateRules.DecideCommittedRewind(
					KingdomInheritanceLoadKind.Primary, true, false, true, false, true, true));
		}

		[Test]
		public void DiscoveryRepairPreservesSuccessfulDurableMarkerProof()
		{
			const string marker = "exact-marker";
			Assert.IsTrue(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.AppliedPendingDurability,
					(int)KingdomInheritApplyStatus.Applied,
					(int)KingdomInheritApplyFault.None, marker));
			Assert.IsTrue(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.Committed,
					(int)KingdomInheritApplyStatus.AlreadyApplied,
					(int)KingdomInheritApplyFault.None, marker));
			Assert.IsFalse(
				KingdomInheritanceStateRules.PreservesApplicationProofDuringDiscoveryRepair(
					KingdomInheritancePhase.RepairRequired,
					(int)KingdomInheritApplyStatus.Applied,
					(int)KingdomInheritApplyFault.None, marker));
			Assert.IsTrue(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Applied, true, marker, marker, marker, false));
			Assert.IsFalse(KingdomInheritanceStateRules.IsDurableMarkerProof(
				KingdomInheritancePhase.RepairRequired,
				(int)KingdomInheritApplyStatus.Failed, true, marker, marker, marker, false));
		}

		[Test]
		public void ReachabilityThrowAfterApplyRetainsOnlyExactQuarantinableRetryProof()
		{
			const string marker = "exact-marker";
			Assert.IsTrue(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, true, marker, marker, marker));
			Assert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Applied,
				(int)KingdomInheritApplyFault.None, true, marker, marker, marker));
			Assert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, false, marker, marker, marker));
			Assert.IsFalse(KingdomInheritanceStateRules.CanRetryUnvalidatedApplication(
				(int)KingdomInheritApplyStatus.Failed,
				(int)KingdomInheritApplyFault.PartialApplication, true, marker, "other", marker));
		}

		[Test]
		public void GameAndStartGatesFailClosedWithoutRejectingJoppaAlternateVillages()
		{
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Tutorial", false));
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Daily", false));
			Assert.IsFalse(KingdomInheritanceStateRules.ShouldOffer("Classic", true));
			Assert.IsTrue(KingdomInheritanceStateRules.ShouldOffer("Classic", false));
			Assert.AreEqual(KingdomInheritanceStartFault.None,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"JoppaWorld", "JoppaWorld.2.2.1.1.10"));
			Assert.AreEqual(KingdomInheritanceStartFault.AlternateWorld,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"AnotherWorld", "AnotherWorld.2.2.1.1.10"));
			Assert.AreEqual(KingdomInheritanceStartFault.TargetIsStart,
				KingdomInheritanceStateRules.ValidateStart("JoppaWorld.1.1.1.1.10",
					"JoppaWorld", "JoppaWorld.1.1.1.1.10"));
		}
	}
}
#endif
