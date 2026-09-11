#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartSourceTests
	{
		// Source contracts only; native callback, custody, and save/load behavior need separate evidence.
		[Test]
		public void ReservationsUseTheSameExecutableCodecForPublicationAndColdRead()
		{
			string source = Source("Growth/KingdomPlot2.07l.FoundingHeartReservations.cs");
			StringAssert.Contains("FoundingHeartReservationPrefix = KingdomFoundingHeartReservationRules.Prefix", source);
			StringAssert.Contains("return KingdomFoundingHeartReservationRules.Encode(Plan, Id, Role)", source);
			StringAssert.Contains("return KingdomFoundingHeartReservationRules.TryRead(Key, Raw, out Transaction, out ZoneId, out Id)", source);
			StringAssert.DoesNotContain("p[5].Length != 64", source);
		}

		private static string Source(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static string Slice(string SourceText, string Start, string End)
		{
			int first = SourceText.IndexOf(Start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, Start);
			int last = SourceText.IndexOf(End, first + Start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, End);
			return SourceText.Substring(first, last - first);
		}

		private static void Ordered(string SourceText, params string[] Terms)
		{
			int cursor = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = SourceText.IndexOf(Terms[i], cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, Terms[i]);
				cursor = found + Terms[i].Length;
			}
		}

		[Test]
		public void EveryFoundingEntryUsesOneReceiptAuthority()
		{
			string geometry = Source("Growth/KingdomPlot2.07.HeartGeometry.cs");
			string survey = Slice(geometry, "public static bool SurveyHeart(", "\n\t\t}\n\n\t}");
			StringAssert.Contains("return EnsureFoundingHeartProjection(System, Z, RiteX, RiteY);",
				survey);
			StringAssert.DoesNotContain("GameObject.Create", survey);
			StringAssert.DoesNotContain("SetZoneProperty", survey);

			string engine = Source("Core/KingdomFoundingTransaction.21EngineProjection.cs");
			string placement = Slice(engine, "private static bool EnsurePlacement(",
				"internal static string FoundingEventID(");
			StringAssert.Contains("EnsureFoundingHeartProjection(System, Site, RiteX, RiteY)",
				placement);
			StringAssert.DoesNotContain("SetZoneProperty", placement);
			StringAssert.DoesNotContain("GameObject.Create", placement);
		}

		[Test]
		public void FrozenReceiptPrecedesEveryWorldObjectAndCompletesAfterExactProof()
		{
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			string ensure = Slice(authority, "public static bool EnsureFoundingHeartProjection(",
				"internal static bool RecoverFoundingHeart(");
			Ordered(ensure, "TryDraftFoundingHeart(", "ClassifyLegacyHeart(",
				"NewHeartIdentitiesAreEmpty", "PreflightFoundingHeartWorld(",
				"PublishFoundingHeartPlan(",
				"EnsureFoundingHeartZoneTruth(", "DriveFoundingHeartMark(",
				"DriveFoundingHeartWorks(", "ExactFoundingHeartWorld(", "SealFoundingHeart(");
			string beforePublish = ensure.Substring(0,
				ensure.IndexOf("PublishFoundingHeartPlan(", StringComparison.Ordinal));
			StringAssert.DoesNotContain("EnsureFoundingRiteGround", beforePublish);
			StringAssert.DoesNotContain("SetZoneProperty", beforePublish);
			StringAssert.DoesNotContain("GameObject.Create", ensure);
			StringAssert.DoesNotContain("AddObject", ensure);
			string preflight = Slice(
				Source("Growth/KingdomPlot2.07d.FoundingHeartWorks.cs"),
				"private static bool PreflightFoundingHeartWorld(", "\n\t}\n}");
			foreach (string proof in new[] { "ExactFoundingHeartMarkerRoster",
				"ExactFoundingHeartWorksRoster", "FindGlobalFoundingHeartId",
				"TryFoundingHeartRoot", "FoundingHeartRootAbsent", "firstOpen",
				"PreparedFoundingHeartMark", "ExactFoundingHeartMark",
				"PreparedFoundingHeartWorks", "ExactFoundingHeartWorks" })
				StringAssert.Contains(proof, preflight);
			foreach (string mutation in new[] { "SetZoneProperty", "SetObjectGameState",
				"ObjectGameState.Remove", "GameObject.Create", "AddObject(" })
				StringAssert.DoesNotContain(mutation, preflight);
			string draft = Slice(authority, "private static bool TryDraftFoundingHeart(",
				"private static bool TryReadFoundingHeartContext(");
			Ordered(draft, "KingdomArchitectureRuntime.TryPrepareFoundingHeart(", "TryEncodePlotPayload(",
				"KingdomFoundingHeartRules.TryCreate(");
			StringAssert.DoesNotContain("TryPreparePlotPayload", draft);
		}

		[Test]
		public void EachSlotRootsDeterministicIdentityBeforeCallbackAndSettlesAfterObservation()
		{
			string marks = Source("Growth/KingdomPlot2.07c.FoundingHeartMarks.cs");
			string drive = Slice(marks, "private static bool DriveFoundingHeartMark(",
				"private static bool PlaceOrSettleFoundingHeartMark(");
			Ordered(drive, "new FoundingHeartAllocationFence(Z, Context)", "if (!fence.Current)",
				"fence.Create(", "UnplacedFoundingHeartOutput(created", "|| !fence.Current)",
				"SetIntProperty(FoundingHeartSlotMark",
				"PreparedFoundingHeartMarkShape(", "StageFoundingHeartIdentity(",
				"RootFoundingHeartOutput(", "AdvanceFoundingHeart(Z, Context, Slot, 0, 1)");
			StringAssert.DoesNotContain("GameObject.Create(", drive);
			string place = Slice(marks, "private static bool PlaceOrSettleFoundingHeartMark(",
				"private static bool SettleFoundingHeartMark(");
			Ordered(place, "cell.AddObject(output, NoStack: true)", "ObserveAddResultInActive",
				"ExactFoundingHeartMark(", "SettleFoundingHeartMark(");

			string stake = Source("Growth/KingdomPlot2.11.Stake.cs") + "\n"
				+ Source("Growth/KingdomPlot2.11a.StakeAdd.cs");
			Ordered(stake, "HeartPlotProperty, 1",
				"TryFreeze(\n\t\t\t\tworks, Architecture", "TryInitializeOwner(",
				"PreparedFoundingHeartWorksShape(works", "StageFoundingHeartIdentity(works",
				"PrepareFoundingHeartWorksAdd(Heart, works)",
				"cell.AddObject(works, NoStack: Heart != null)",
				"ObserveAddResultInActive", "SettleFoundingHeartWorksAdd(");
			string published = Slice(stake, "StageFoundingHeartIdentity(works",
				"GameObject accepted = null;");
			StringAssert.DoesNotContain("RemoveCreatedWorks(works", published);
			string works = Source("Growth/KingdomPlot2.07d.FoundingHeartWorks.cs");
			string shape = Slice(works, "private static bool PreparedFoundingHeartWorksShape(",
				"private static bool ExactFoundingHeartWorks(");
			StringAssert.Contains("Works.Physics.InInventory != null", shape);
		}

		[Test]
		public void GlobalUniquenessAndColdLoadRecoveryStayFailClosed()
		{
			string identity = Source("Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs");
			foreach (string evidence in new[] { "ActiveZone", "CachedZones", "Graveyard",
				"ObjectGameState", "GetInventoryDirectAndEquipment",
				"MaximumFoundingHeartCustodyObjects",
				"IDIfAssigned = KingdomFoundingHeartRules.SlotId" })
				StringAssert.Contains(evidence, identity);
			string reservations = Source("Growth/KingdomPlot2.07l.FoundingHeartReservations.cs");
			foreach (string evidence in new[] { "Store.Ensure(key, expected)", "SlotCount",
				"FoundingHeartFinalId", "AuditFoundingHeartReservations",
				"ZoneActivated audits every", "never asks ZoneManager" })
				StringAssert.Contains(evidence, reservations);
			StringAssert.Contains("Strings.Add(Key, Expected)",
				Source("Growth/KingdomPlot2.07o.FoundingHeartReservationStore.cs"));
			StringAssert.DoesNotContain("SetStringGameState", reservations);
			foreach (string thaw in new[] { "GetZone(", "LoadZone", "ZoneManager.Get",
				"CachedZones[" }) StringAssert.DoesNotContain(thaw, reservations);
			string activation = Source("Core/KingdomSystem.z20.Events.cs");
			Ordered(activation, "KingdomPlots.AuditFoundingHeartReservations(this, E.Zone)",
				"KingdomPlots.RecoverLegacyPlotFinalEffects(this, E.Zone)",
				"KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks)");
			StringAssert.Contains("FoundingHeartReservationPrefix",
				Source("Growth/KingdomPlot2.07g.FoundingHeartCustody.cs"));
			string settlement = Source("Growth/KingdomConstruction.Settlement.cs");
			Ordered(settlement, "KingdomPlots.RecoverFoundingHeart(System, Z)",
				"KingdomConstructionPresence.Assign(System, Survey)");
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			string recovery = Slice(authority, "internal static bool RecoverFoundingHeart(",
				"private static bool TryFoundingHeartTransaction(");
			Ordered(recovery, "TryRiteGround(Z, out int riteX, out int riteY)",
				"EnsureFoundingHeartProjection(System, Z, riteX, riteY)",
				"KingdomFoundingHeartRules.TryDecode(raw",
				"EnsureFoundingHeartProjection(System, Z, plan.RiteX, plan.RiteY)");
			StringAssert.DoesNotContain("KingdomFoundingHeartRules.Complete", recovery);
			StringAssert.DoesNotContain("FoundingHeartRootAbsent", recovery);
		}

		[Test]
		public void FoundingPoseAndExistingBasinAreFrozenBeforeReceiptPublication()
		{
			string facing = Source("Growth/KingdomArchitectureRuntime.HeartAndFacing.cs");
			string founding = Source("Growth/KingdomArchitectureRuntime.FoundingHeart.cs");
			Ordered(founding, "ArchitectureFacing.North, ArchitectureFacing.East",
				"ArchitectureFacing.South, ArchitectureFacing.West",
				"TryHeartBasinCoordinate(", "basinX != RiteX || basinY != RiteY",
				"TryFoundingHeartBasinInvariant(");
			StringAssert.DoesNotContain("KingdomPlots.HeartFor", founding);
			string invariant = Slice(facing,
				"internal static bool TryFoundingHeartBasinInvariant(",
				"private static bool SameRect(");
			foreach (string proof in new[] { "TryDecode(Intent", "ExistingAuthority",
				"r_KingdomFirstBasin", "fixture:first-basin", "basinX != RiteX",
				"basinY != RiteY" }) StringAssert.Contains(proof, invariant);
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			string draft = Slice(authority, "private static bool TryDraftFoundingHeart(",
				"private static bool TryReadFoundingHeartContext(");
			Ordered(draft, "KingdomArchitectureRuntime.TryPrepareFoundingHeart(",
				"KingdomFoundingHeartRules.TryCreate(");
		}

		[Test]
		public void ColdRecoveryUsesOnlyAuthenticatedStakeTruth()
		{
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			string read = Slice(authority, "private static bool TryReadFoundingHeartContext(",
				"private static bool FoundingHeartGroundAllows(");
			StringAssert.Contains("KingdomFoundingHeartStakeRules.TryDecode(Plan.StakeTruth", read);
			StringAssert.DoesNotContain("KingdomData.TryGetBuilding", read);
			StringAssert.DoesNotContain("TryGetSpec", read);
			string stake = Source("Growth/KingdomPlot2.11.Stake.cs");
			foreach (string frozen in new[] { "heartTruth.FootprintX1", "heartTruth.Roof",
				"heartTruth.WallBlueprint", "heartTruth.Staff", "heartTruth.Defence",
				"heartTruth.PurposeLegacy" }) StringAssert.Contains(frozen, stake);
			string exact = Source("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs");
			foreach (string proof in new[] { "part.WallBlueprint != truth.WallBlueprint",
				"part.DefencePending != truth.Defence", "FootX1Property",
				"PlotRoofProperty", "ExactFoundingHeartPurpose", "FoundingHeartWorkSchemaAbsent" })
				StringAssert.Contains(proof, exact);
			string labour = Source("Growth/KingdomPlot2.26.Labour.cs");
			int firstHeartProof = labour.IndexOf(
				"TryReadFoundingHeartWorkAuthority(zone, parent", StringComparison.Ordinal);
			int finalHeartProof = labour.LastIndexOf(
				"TryReadFoundingHeartWorkAuthority(zone, parent", StringComparison.Ordinal);
			ClassicAssert.Greater(finalHeartProof, firstHeartProof,
				"each physical stage must reprove frozen heart authority before cursor commit");
			StringAssert.Contains("Works.StageApplied == priorStage", labour);
			string finish = Source("Growth/KingdomPlot2.30.Finish.cs");
			Ordered(finish, "FoundingHeartWorkIdentityEvidence(parent)",
				"TryReadFoundingHeartWorkAuthority(Z, parent", "founding.Entry",
				"KingdomData.TryGetBuilding(Works.DesignKey, out entry)",
				"return FinishFoundingHeart(", "TryFinishOutput(Works, Z");
			string drive = Source("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs");
			string run = Slice(drive, "private static bool DriveFoundingHeartTerminal(",
				"private static bool RepairFoundingHeartFinalIntent(");
			Ordered(run, "BeginFoundingHeartTerminal(", "RepairFoundingHeartFinalIntent(",
				"ExactPreparedFoundingHeartFinal(", "cell?.AddObject(final, NoStack: true)",
				"ExactSettledFoundingHeartFinal(");
			string begin = Slice(drive, "private static bool BeginFoundingHeartTerminal(",
				"private static bool ExactPreparedFoundingHeartFinal(");
			Ordered(begin, "Final.SetStringProperty(FoundingHeartTerminalProperty",
				"RootFoundingHeartFinal(plan, Final)", "PublishFoundingHeartTerminal(",
				"Predecessor.SetStringProperty(FinalOutputIdProperty");
			StringAssert.Contains("RepairFoundingHeartFinalIntent", drive);
			StringAssert.Contains("if (!published && fence.Current && UnplacedFoundingHeartOutput(Final, Context.Stake.Blueprint)", begin);
			StringAssert.Contains("&& Terminal != null && ExactPreparedFoundingHeartFinal(Final, Z, Context, Terminal)) RemoveCreatedWorks(Final, Z)", begin);
		}

		[Test]
		public void TerminalRemovalUsesExactTombstoneAndReloadNeverRetriesDestroy()
		{
			string settlement = Source(
				"Growth/KingdomPlot2.07k.FoundingHeartTerminalSettlement.cs");
			Ordered(settlement, "FreshAttempt", "predecessor.Destroy(null, Silent: true)",
				"ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)",
				"ExactRemovalTombstone(returned, removed", "ExactGraveyardTombstone(",
				"RemovalProofProperty", "ExactFoundingHeartRetiredAuthority(",
				"KingdomFoundingHeartTerminalPhase.Removed");
			string reload = Slice(settlement, "if (!FreshAttempt)",
				"KingdomPhysicalLookupState before");
			StringAssert.DoesNotContain("Destroy(", reload);
			StringAssert.Contains("HasRemovalProof", reload);
			StringAssert.Contains("QuarantineFoundingHeartTerminal", reload);
			Ordered(settlement, "Callback();",
				"ExactFoundingHeartFinalObjectGameState(Context.Plan, Final, true)",
				"KingdomFoundingHeartSinkDisposition.Settled");
			string custody = Source("Growth/KingdomPlot2.07m.FoundingHeartTombstones.cs");
			foreach (string proof in new[] { "FoundingHeartTombstoneIdentity",
				"ExactFoundingHeartGraveyardTombstone", "ExactFoundingHeartLiveAbsence" })
				StringAssert.Contains(proof, custody);
		}

		[Test]
		public void GenericFinalAndStamperRootBeforeAddAndRetireAfterSettlement()
		{
			string output = Source("Growth/KingdomPlot2.31.FinishOutput.cs");
			Ordered(output, "RootPlotFinalOutput(expectedOutput, building)",
				"UpdateFinalOutput(ref construction",
				"UpdatePhysical(ref construction", "cell.AddObject(building)",
				"ExactFinalBuilding(building", "ExactAddCut(callbackReturned");
			string afterPublication = output.Substring(output.IndexOf(
				"building.SetStringProperty(PlotFinalPredecessorProperty", StringComparison.Ordinal));
			StringAssert.DoesNotContain("RemoveCreatedWorks(building, Z)", afterPublication);
			string root = Source("Growth/KingdomPlot2.31b.FinishOutputCustody.cs");
			foreach (string proof in new[] { "PlotFinalRootPrefix", "TryPlotFinalRoot",
				"ExactPlotFinalRoot", "PreparedPlotFinalOutput",
				"FindPlotFinalRootForPredecessor", "ObjectGameState.Count > 65536" })
				StringAssert.Contains(proof, root);

			string stamper = Source("Growth/KingdomArchitectureStamper.Staging.cs");
			Ordered(stamper, "RootStagingOutput(placed)",
				"Owner.SetIntProperty(stateProperty, 1)", "cell.AddObject(placed",
				"ExactComponent(Owner, placed", "ExactAddCut(callbackReturned",
				"Owner.SetIntProperty(stateProperty, 2)", "RetireStagingRoot(placed)");
			StringAssert.Contains("TryLandStagingRoot", stamper);
			StringAssert.Contains("RetireStagingRoot", stamper);
			StringAssert.Contains("FindStagingRootForPlacement",
				Source("Growth/KingdomArchitectureStamper.StagingCustody.cs"));
			StringAssert.Contains("ExactAddCut(callbackReturned",
				Source("Growth/KingdomPlot2.28.ClearPayout.cs"));
		}

		[Test]
		public void GenericRemovalReloadRequiresAuthenticatedGraveyardTombstone()
		{
			string removal = Source("Growth/KingdomPlot2.32.FinishRemoval.cs");
			Ordered(removal, "parent.Destroy(null, Silent: true)",
				"ExactPlotFinalRootCustody(expectedFinalId, building)",
				"ExactRemovalTombstone(returned, removed", "ExactPlotRemovalTombstone(",
				"RemovalProofProperty", "KingdomPhysicalPhase.FinalRemoved");
			string reload = Source("Growth/KingdomPlot2.32b.FinishRemovalRecovery.cs");
			foreach (string proof in new[] { "HasRemovalProof", "ExactPlotRemovalTombstone",
				"GameObject.Validate(tombstone)", "KingdomConstruction.ReceiptProperty",
				"Job.SubjectId == Id", "Job.PhysicalItemId == Id", "TryReadGraveyardId",
				"Native Destroy promises retained graveyard parts" })
				StringAssert.Contains(proof, reload);
			StringAssert.DoesNotContain("Final.SetStringProperty", reload);
			string pending = Slice(removal,
				"if (construction.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)",
				"else if (construction.PhysicalPhase");
			StringAssert.Contains("RecoverPendingPlotRemoval", pending);
			StringAssert.DoesNotContain("Destroy(", pending);
			string tombstone = Slice(reload, "private static bool ExactPlotRemovalTombstone(",
				"private static bool ExactGraveyardTombstone(string Id, GameObject Expected,");
			StringAssert.Contains("native retained graveyard parts are unreadable", tombstone);
			string graveyard = reload.Substring(reload.IndexOf(
				"private static bool ExactGraveyardTombstone(string Id, GameObject Expected,",
				StringComparison.Ordinal));
			StringAssert.Contains("TryLoadedPlotTombstones(out List<GameObject> rows)", graveyard);
			StringAssert.Contains("try { Id = Item.IDIfAssigned; return true; }", graveyard);
			StringAssert.Contains("native graveyard identity is unreadable", graveyard);
			StringAssert.DoesNotContain("GetInventoryDirectAndEquipment", graveyard);
			// #138: the rung's ceremony moved into the shared settlement helper, and the proof it
			// is re-asked with moved with it as the caller's own delegate. The pin follows the
			// code: the callback still runs before the endpoint is re-proved, and the plot
			// route's proof is still its own plot final-root custody.
			string rungSettle = Source("Growth/KingdomPlotHeartRules.Settle.cs");
			Ordered(rungSettle, "KingdomCeremonyHeart.OnRungRaised(", "if (!Prove()) return false;");
			string jobEffects = Source("Growth/KingdomPlot2.34.EffectsAndFurnishing.cs");
			Ordered(jobEffects, "TrySettleHeartRung(System, Z, Building, Job.TargetKey,",
				"ExactPlotFinalRootCustody(settling.OutputId, Building)");
		}

		[Test]
		public void LegacyEffectsUseAuthenticatedAtMostOnceReceiptAndActiveZoneAudit()
		{
			string effects = Source("Growth/KingdomPlot2.33b.LegacyEffects.cs");
			Ordered(effects, "KingdomFoundingHeartSinkDisposition.Pending",
				"KingdomFoundingHeartSinkDisposition.Attempting", "Callback();",
				"ExactLegacyEffectEndpoint", "KingdomFoundingHeartSinkDisposition.Settled");
			foreach (string proof in new[] { "KingdomPlotLegacyEffectsRules.TryDecode",
				"ExactLegacyPlotRemovalTombstone", "ExactPlotFinalRootCustody",
				"KingdomFoundingHeartSinkDisposition.Lost", "RecoverLegacyPlotFinalEffects" })
				StringAssert.Contains(proof, effects);
			string audit = Slice(effects, "internal static bool RecoverLegacyPlotFinalEffects(",
				"\n\t\t}\n\t}");
			foreach (string thaw in new[] { "GetZone(", "LoadZone", "ZoneManager.Get",
				"CachedZones[" }) StringAssert.DoesNotContain(thaw, audit);
			StringAssert.Contains("RecoverLegacyPlotFinalEffects(this, E.Zone)",
				Source("Core/KingdomSystem.z20.Events.cs"));
		}

		[Test]
		public void ActivationAuditsEveryReservedIdInResidentZoneWithoutThaw()
		{
			string custody = Source("Growth/KingdomPlot2.07g.FoundingHeartCustody.cs");
			Ordered(custody, "HashSet<GameObject> graveyard", "TryLoadedPlotTombstones(",
				"graveyard.Contains(item)");
			string reservations = Source(
				"Growth/KingdomPlot2.07l.FoundingHeartReservations.cs");
			foreach (string proof in new[] { "store.TryAudit(out Dictionary<string, string> reservations)",
				"FoundingHeartReservationPrefix", "TryReadFoundingHeartReservation",
				"Z.GetObjects()", "reservations.TryGetValue(id", "owner != transaction",
				"zone != Z.ZoneID" }) StringAssert.Contains(proof, reservations);
			Ordered(reservations, "store.Retains(reservations, AllowAdditional: false)",
				"RecoverFoundingHeart(System, Z)", "store.Retains(reservations, AllowAdditional: true)");
			foreach (string thaw in new[] { "GetZone(", "LoadZone", "ZoneManager.Get",
				"CachedZones[" }) StringAssert.DoesNotContain(thaw, reservations);
		}

		[Test]
		public void CursorAndFinalProofRefuseCallbackReceiptOrCustodyDrift()
		{
			string identity = Source("Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs");
			string advance = Slice(identity, "private static bool AdvanceFoundingHeart(",
				"private static void FoundingHeartSlotGround(");
			Ordered(advance, "KingdomFoundingHeartRules.Encode(Plan)",
				"Z?.GetZoneProperty(FoundingHeartReceiptProperty", "TryAdvance(Plan",
				"PublishFoundingHeartPlan(Z, receipt, Plan)");
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			string publish = Slice(authority, "private static bool PublishFoundingHeartPlan(",
				"private static bool EnsureFoundingHeartZoneTruth(");
			Ordered(publish, "GetZoneProperty(FoundingHeartReceiptProperty", "SetZoneProperty(",
				"GetZoneProperty(FoundingHeartReceiptProperty");
			string world = Source("Growth/KingdomPlot2.07d.FoundingHeartWorks.cs");
			string final = Slice(world, "private static bool ExactFoundingHeartWorld(",
				"/// <summary>Read-only whole-envelope proof");
			foreach (string proof in new[] { "ExactFoundingHeartReceipt",
				"ExactFoundingHeartZoneTruth", "ExactFoundingHeartFinalCustody" })
				StringAssert.Contains(proof, final);
			string seal = Source("Growth/KingdomPlot2.07h.FoundingHeartSeal.cs");
			Ordered(seal, "ExactFoundingHeartWorld(Z, Context)",
				"SetZoneProperty(FoundingHeartSealProperty", "ExactFoundingHeartSeal(Z, plan)");
			string custody = Source("Growth/KingdomPlot2.07g.FoundingHeartCustody.cs");
			foreach (string proof in new[] { "ExactFoundingHeartObjectGameState",
				"FoundingHeartLoadedReferenceCount", "ExactFoundingHeartOwnedRoster",
				"ExactFoundingHeartRetiredCustody",
				"HasGlobalFoundingHeartTransactionEvidence" }) StringAssert.Contains(proof, custody);
			string sealedAuthority = Source("Growth/KingdomPlot2.07h.FoundingHeartSeal.cs");
			foreach (string proof in new[] { "ExactFoundingHeartFinalCustody(plan)",
				"ExactFoundingHeartMarkerRoster(Z, plan, false)",
				"ExactFoundingHeartRetiredCustody(plan)" }) StringAssert.Contains(proof, sealedAuthority);
		}

		[Test]
		public void LegacyClassifierIsStrictAndReadOnly()
		{
			string legacy = Source("Growth/KingdomPlot2.07e.FoundingHeartLegacy.cs");
			foreach (string proof in new[] { "HasCurrentFoundingHeartEvidence",
				"FoundingHeartOwnerProperty", "FoundingHeartSlotProperty", "present != 4",
				"ExactLegacyHeartMarks", "ExactLegacyHeartRoot", "count != 1",
				"FindGlobalFoundingHeartId" })
				StringAssert.Contains(proof, legacy);
			foreach (string mutation in new[] { "GameObject.Create", "AddObject(",
				"SetZoneProperty", "SetIntProperty", "SetStringProperty", "RequirePart",
				"Destroy(", "Obliterate(", "RemoveZoneProperty" })
				StringAssert.DoesNotContain(mutation, legacy);
		}

		[Test]
		public void FoundingProjectionHasNoEconomicOrJobAuthority()
		{
			string source = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs")
				+ Source("Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs")
				+ Source("Growth/KingdomPlot2.07c.FoundingHeartMarks.cs")
				+ Source("Growth/KingdomPlot2.07d.FoundingHeartWorks.cs");
			foreach (string forbidden in new[] { "KingdomConstruction.NewJob", "TryFundNew(",
				"ReservePayment(", "ReserveExactWater(", "KingdomWaterDebit", ".Debit(" })
				StringAssert.DoesNotContain(forbidden, source);
			string siting = Source("Growth/KingdomPlot2.08.Siting.cs");
			string prepared = Slice(siting, "private static GameObject StakeFirstHeartPrepared(",
				"// --- Siting");
			StringAssert.Contains("KingdomConstructionJob founding = null;", prepared);
			StringAssert.DoesNotContain("KingdomZoning.Permits", prepared);
		}

		[Test]
		public void FoundingHeartProductionFilesRemainBelowPhysicalLineLimit()
		{
			foreach (string path in new[] { "Growth/KingdomFoundingHeartPlan.cs",
				"Growth/KingdomFoundingHeartRules.cs",
				"Growth/KingdomFoundingHeartReservationRules.cs",
				"Growth/KingdomFoundingHeartReservationState.cs",
				"Growth/KingdomFoundingHeartStakeRules.cs",
				"Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs",
				"Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs",
				"Growth/KingdomPlot2.07c.FoundingHeartMarks.cs",
				"Growth/KingdomPlot2.07d.FoundingHeartWorks.cs",
				"Growth/KingdomPlot2.07e.FoundingHeartLegacy.cs",
				"Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs",
				"Growth/KingdomPlot2.07g.FoundingHeartCustody.cs",
				"Growth/KingdomPlot2.07h.FoundingHeartSeal.cs",
				"Growth/KingdomPlot2.07i.FoundingHeartTerminalAuthority.cs",
				"Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs",
				"Growth/KingdomPlot2.07k.FoundingHeartTerminalSettlement.cs",
				"Growth/KingdomPlot2.07l.FoundingHeartReservations.cs",
				"Growth/KingdomPlot2.07m.FoundingHeartTombstones.cs",
				"Growth/KingdomPlot2.07o.FoundingHeartReservationStore.cs",
				"Growth/KingdomPlot2.07p.FoundingHeartAllocationFence.cs",
				"Growth/KingdomFoundingHeartTerminalPlan.cs",
				"Growth/KingdomFoundingHeartTerminalRules.cs",
				"Growth/KingdomPlotLegacyEffectsPlan.cs",
				"Growth/KingdomPlotLegacyEffectsRules.cs",
				"Growth/KingdomPlot2.33b.LegacyEffects.cs" })
			{
				int lines = Source(path).Split('\n').Length;
				ClassicAssert.Less(lines, 300, path + " has " + lines + " physical lines");
			}
		}
		/// <summary>The founding heart binds its immutable basin by anchor ROLE. The draft compiler
		/// composes every stateful anchor as role@x,y, so a literal compare refuses every founding.</summary>
		[Test]
		public void BasinBindingComparesTheAnchorRoleNotTheComposedKey()
		{
			foreach (string path in new[] { "Growth/KingdomArchitectureRuntime.HeartAndFacing.cs",
				"Growth/KingdomArchitectureStamper.Transitions.cs" })
			{
				string source = Source(path);
				StringAssert.Contains(
					"KingdomArchitectureRules.AnchorRole(placement.StatefulAnchor)", source, path);
				StringAssert.DoesNotContain("placement.StatefulAnchor != \"fixture:first-basin\"", source, path);
			}
			StringAssert.Contains("StatefulAnchor = statefulAnchor",
				Source("Growth/KingdomArchitectureDraftCompilerRules.cs"));
		}

		/// <summary>
		/// Every branch that can refuse the founding heart's recovery says which one it was.
		/// Three refusals were silent, and a native run that lost its heart could not tell a
		/// lost works authority from an undecodable receipt from ground that never had a rite.
		/// The answers are unchanged: each of these reads exactly as it read before, and only
		/// the log gained a line.
		/// </summary>
		[Test]
		public void EveryFoundingHeartRecoveryRefusalNamesItsOwnStep()
		{
			string authority = Source("Growth/KingdomPlot2.07a.FoundingHeartAuthority.cs");
			StringAssert.Contains("return !HasReceiptlessFoundingHeartEvidence(System, Z) "
				+ "|| HeartRefused(\"recover: receiptless evidence\");", authority);
			StringAssert.Contains("out KingdomFoundingHeartPlan plan)) "
				+ "return HeartRefused(\"recover: receipt decode\");", authority);

			string drive = Source("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs");
			StringAssert.Contains("return HeartRefused(\"sealed: context or seal\");", drive);
			StringAssert.Contains("return HeartRefused(\"sealed: works slot lookup\");", drive);
			StringAssert.Contains("return TryReadFoundingHeartWorkAuthority(Z, works, out _)\n"
				+ "\t\t\t\t\t|| HeartRefused(\"sealed: work authority\");", drive);

			// The refusal helper still only logs and reads false, so naming a branch cannot
			// change what any of them answers.
			string diagnostics = Source("Growth/KingdomPlot2.07n.FoundingHeartDiagnostics.cs");
			StringAssert.Contains("KingdomLog.Log(\"founding heart refused: \" + Step);",
				diagnostics);
			StringAssert.Contains("private static bool HeartRefused(string Step)", diagnostics);

			// And the recovery these branches answer for still fails the whole settlement pass
			// closed, which is what turned this defect into a silent halt rather than a wrong
			// building.
			string settlement = Source("Growth/KingdomConstruction.Settlement.cs");
			StringAssert.Contains("if (!KingdomPlots.RecoverFoundingHeart(System, Z))", settlement);
			StringAssert.Contains(
				"KingdomLog.Log(\"construction: founding heart recovery requires inspection\");",
				settlement);
		}

		/// <summary>
		/// The retirement authority can now be asked about one NAMED generation, and the works
		/// slot's own question is unchanged. The name is never the caller's to invent: each
		/// generation says which record of the plan's own must name the identity, and the final
		/// generation reads it out of the digest-sealed terminal rather than off a property.
		/// The final generation's own retirement proof is not supplied yet, so it refuses --
		/// fail-closed until the receipt chain that can prove it is wired in.
		/// </summary>
		[Test]
		public void RetirementAuthorityNamesOneGenerationAndStillRefusesTheUnproved()
		{
			string seal = Source("Growth/KingdomPlot2.07h.FoundingHeartSeal.cs");
			// The works case: same clause, reached through the named overload.
			StringAssert.Contains("return ExactFoundingHeartRetiredAuthority(Z, PredecessorId,\n"
				+ "\t\t\t\tKingdomFoundingHeartRetiredGeneration.Works, out Context);", seal);
			StringAssert.Contains("KingdomFoundingHeartRules.SlotId(Plan,\n"
				+ "\t\t\t\t\tKingdomFoundingHeartRules.WorksSlot) == PredecessorId;", seal);
			// The naming clause runs BEFORE seal, reservations, roster, custody and proof, and
			// those five are unchanged.
			foreach (string clause in new[] {
				"NamesRetiredFoundingHeartIdentity(Z, plan, PredecessorId, Generation)",
				"&& ExactFoundingHeartSeal(Z, plan)",
				"&& ExactFoundingHeartReservations(plan)",
				"&& TryReadFoundingHeartContext(Z, plan, out Context)",
				"&& ExactFoundingHeartMarkerRoster(Z, plan, false)",
				"&& ExactFoundingHeartRetiredCustody(plan)",
				"&& ExactFoundingHeartRetirementProof(Z, Context, PredecessorId, Generation)" })
				StringAssert.Contains(clause, seal);
			int names = seal.IndexOf("NamesRetiredFoundingHeartIdentity(Z, plan, PredecessorId",
				StringComparison.Ordinal);
			int proof = seal.IndexOf("ExactFoundingHeartRetirementProof(Z, Context, PredecessorId,",
				StringComparison.Ordinal);
			ClassicAssert.IsTrue(names > -1 && proof > names,
				"the identity must be named before any authority is spent proving it");
			// The final generation is read out of the sealed blob, and may not name the identity
			// the prior record retired.
			StringAssert.Contains("KingdomFoundingHeartTerminalRules.TryDecode(", seal);
			StringAssert.Contains("&& terminal.FinalId == PredecessorId", seal);
			StringAssert.Contains("&& terminal.PredecessorId != PredecessorId;", seal);

			// And the proof itself refuses any generation it cannot prove.
			string removal = Source("Growth/KingdomPlot2.07q.FoundingHeartRecordedRemoval.cs");
			StringAssert.Contains("if (Generation != KingdomFoundingHeartRetiredGeneration.Works)"
				+ " return false;", removal);
		}

		/// <summary>
		/// The chained recovery path: proved from the retired identity outward, never from what
		/// stands on the sealed cell, and it writes nothing. The one write in the chain is the
		/// settle's own reservation, which is issued before the rung is stamped and refuses the
		/// whole settle when it cannot be.
		/// </summary>
		[Test]
		public void TheChainedRecoveryProvesIdentityReadsOnlyAndTheSettleOwnsTheOnlyWrite()
		{
			string chain = Source("Growth/KingdomPlot2.07s.FoundingHeartClimbedChain.cs");
			// Identity, not position: the chain starts at the identity the sealed terminal bound.
			StringAssert.Contains("string retired = prior.FinalId;", chain);
			StringAssert.Contains("TryImprovementSuccessorOf(retired, out job, out successor, "
				+ "out jobs, out objects)", chain);
			// Every hop names itself, so the next native run says which one refused instead of
			// leaving it to be inferred from what did not happen.
			foreach (string hop in new[] { "chain: bound identity", "chain: improvement lookup",
				"chain: receipt", "chain: removal proof", "chain: custody corroboration",
				"chain: binds ground", "chain: retirement authority" })
				StringAssert.Contains("HeartRefused(\"" + hop, chain);
			StringAssert.Contains("row.SubjectId != RetiredId", chain);
			// Exactly one job may name the retired identity: a second one refuses rather than
			// choosing, and the count is carried into the refusal so a native run can read it.
			StringAssert.Contains("Named++;", chain);
			StringAssert.Contains("if (Named != 1 || Job.Phase != KingdomConstructionPhase.Complete",
				chain);
			StringAssert.Contains("if (Named != 1) Job = null;", chain);
			StringAssert.Contains("KingdomConstruction.FindGlobalLiveId(Job.OutputId, out Successor)",
				chain);
			foreach (string fact in new[] { "KingdomConstruction.HasReceipt(successor, job)",
				"r_KingdomScaffold.HasRemovalProof(successor, job.SubjectId)",
				"KingdomFoundingHeartChainRules.CorroboratesRetired(stamp, retired)",
				"Job.Phase != KingdomConstructionPhase.Complete",
				"!string.IsNullOrEmpty(Job.Failure)",
				"KingdomFoundingHeartChainRules.BindsGround(job.OutputId," })
				StringAssert.Contains(fact, chain);
			// The withdrawn clause: the reservation store is keyed by deterministic role
			// identities, so nothing here may ask it about a successor.
			foreach (string withdrawn in new[] { "HasExactFoundingHeartReservation",
				"TryReserveClimbedFoundingHeartRoot", "FoundingHeartReservationPrefix" })
				StringAssert.DoesNotContain(withdrawn, chain);
			StringAssert.DoesNotContain("TryReserveClimbedFoundingHeartRoot",
				Source("Growth/KingdomUpgrade.26.HeartRung.cs"));
			// Nothing about the sealed cell, and no heart-shaped plot admitted by its stamps.
			foreach (string position in new[] { "GetCell(", "CurrentCell", "HeartPlotProperty",
				"MainWorldX" })
				StringAssert.DoesNotContain(position, chain);
			// The retirement authority is asked last, and about the identity the chain named.
			int binds = chain.IndexOf("KingdomFoundingHeartChainRules.BindsGround(",
				StringComparison.Ordinal);
			int authority = chain.IndexOf("ExactFoundingHeartRetiredAuthority(Z, retired,",
				StringComparison.Ordinal);
			ClassicAssert.IsTrue(binds > -1 && authority > binds,
				"the chain must prove itself before it spends the heart's own authority");

			// The proof shard is read-only, whole: the hold that has to write lives in its own
			// shard beside it, so this sweep needs no scoping at all.
			foreach (string write in new[] { "SetStringProperty(", "SetIntProperty(",
				"SetZoneProperty(", "SetObjectGameState(", "Ensure(", "Destroy(", "AddObject(" })
				StringAssert.DoesNotContain(write, chain);

			// The settle writes nothing for this chain: it proves its endpoint and its handover
			// exactly as before, and stamps the rung.
			string rung = Source("Growth/KingdomUpgrade.26.HeartRung.cs");
			int endpoint = rung.IndexOf("ExactImprovementHeartEndpoint(System, Z, Successor, Job)",
				StringComparison.Ordinal);
			int stamp = rung.IndexOf("KingdomPlots.TrySettleHeartRung(", StringComparison.Ordinal);
			ClassicAssert.IsTrue(endpoint > -1 && stamp > endpoint,
				"the endpoint proof must still stand before the rung is stamped");

			// And the final generation's retirement is the receipt chain, never absence alone.
			string removal = Source("Growth/KingdomPlot2.07q.FoundingHeartRecordedRemoval.cs");
			StringAssert.Contains("ExactFoundingHeartImprovementRetirement(PredecessorId)", removal);
			StringAssert.Contains("TryImprovementSuccessorOf(PredecessorId, out var job, "
				+ "out var successor,", removal);
			StringAssert.Contains("&& ExactFoundingHeartLiveAbsence(PredecessorId);", removal);

			// The chained branch sits beside the first-generation drive, after it refuses.
			string drive = Source("Growth/KingdomPlot2.07j.FoundingHeartTerminalDrive.cs");
			StringAssert.Contains("if (DriveFoundingHeartTerminal(System, Z, Context, null, null, "
				+ "0L, null, false))\n\t\t\t\treturn true;", drive);
			StringAssert.Contains("return TryChainedFoundingHeartRoot(Z, Context, out _)\n"
				+ "\t\t\t\t|| HeartRefused(\"sealed: terminal drive and chain\");", drive);
		}

		/// <summary>
		/// A stuck climb is a permanent refusal, so it must not also be permanently silent: the
		/// seal dedupes its own line and the settlement's daily one is suppressed. The founder is
		/// told once, on the ground, when the row is CLASSIFIED as an inspection -- never merely
		/// read -- and the saying is taken back where the climb finishes, so a heart that sticks,
		/// finishes and sticks again is said about twice.
		/// </summary>
		[Test]
		public void AStuckClimbIsSaidOnceAndUnsaidWhereTheClimbFinishes()
		{
			string chain = Source("Growth/KingdomPlot2.07t.FoundingHeartClimbHold.cs");
			string proof = Source("Growth/KingdomPlot2.07s.FoundingHeartClimbedChain.cs");
			// The read says nothing. Announcing is a separate entry point.
			StringAssert.Contains("internal static void NoteClimbUnderInspection(Zone Z, "
				+ "int RowWorkId)", chain);
			StringAssert.DoesNotContain("AnnounceClimbUnderInspection(", chain);
			// The read lives beside the proof and says nothing at all: no writer, no ledger.
			StringAssert.Contains("private static bool HasPendingClimb(Zone Z, int RowWorkId, "
				+ "out string RetiredId,", proof);
			StringAssert.DoesNotContain("SetZoneProperty(", proof);
			StringAssert.DoesNotContain("Ledger", proof);

			// Told only where the classification was actually reached, and never for a
			// duplicated root, which is malformed rather than an inspection.
			string evidence = Source("Core/KingdomInheritanceSpatial.Evidence.cs");
			StringAssert.Contains("count == 0 && KingdomPlots.HasPendingClimb(Zone, Row.WorkId)",
				evidence);
			StringAssert.Contains("if (Pending) KingdomPlots.NoteClimbUnderInspection(Zone, "
				+ "Row.WorkId);", evidence);

			// Once only: decided by the shared rule, written, read back, and nothing said if the
			// write did not take.
			StringAssert.Contains("KingdomFoundingHeartChainRules.SaysClimbHold(held, true)",
				chain);
			StringAssert.Contains("Z.SetZoneProperty(FoundingHeartClimbHeldProperty, job.Id);",
				chain);
			StringAssert.Contains("if (Z.GetZoneProperty(FoundingHeartClimbHeldProperty, null) "
				+ "!= job.Id) return;", chain);
			StringAssert.Contains("system?.Ledger?.Note(", chain);
			StringAssert.Contains("\"founding heart: climb under inspection; retired=\" "
				+ "+ retired", chain);

			// And taken back where the climb finishes -- the completion path the handover takes,
			// because a completed climb never reaches the pending read again.
			StringAssert.Contains("internal static void ClearClimbHold(Zone Z, string RetiredId)",
				chain);
			StringAssert.Contains("prior.FinalId != RetiredId) return;", chain);
			string handover = Source("Growth/KingdomUpgrade.25.HandoverRemoval.cs");
			StringAssert.Contains("KingdomPlots.ClearClimbHold(Z, Job.SubjectId);", handover);
			int complete = handover.IndexOf("if (!KingdomConstruction.Complete(ref Job))",
				StringComparison.Ordinal);
			int cleared = handover.IndexOf("KingdomPlots.ClearClimbHold(Z, Job.SubjectId);",
				StringComparison.Ordinal);
			ClassicAssert.IsTrue(complete > -1 && cleared > complete,
				"the hold is cleared only once the receipt has actually completed");

			// The flag is declared beside the other founding-heart keys and regenerated into
			// removal coverage like every other property.
			StringAssert.Contains("public const string FoundingHeartClimbHeldProperty = "
				+ "\"r_TAF_FoundingHeartClimbHeldAnnounced\";",
				Source("Growth/KingdomPlot2.07i.FoundingHeartTerminalAuthority.cs"));
			StringAssert.Contains("r_TAF_FoundingHeartClimbHeldAnnounced",
				Source("Core/KingdomRemovalCoverage.Generated.cs"));
		}

		/// <summary>
		/// VALUE. The once-only decision itself, executed: production's own rule for when a stuck
		/// climb is SAID and when the saying is TAKEN BACK, driven through the whole life cycle a
		/// heart can have -- stuck, still stuck, finished, stuck again -- and through the other
		/// ending, cancellation, which never reaches the completion path.
		/// </summary>
		[Test]
		public void TheHoldIsSaidOnceAndReleasedByEitherEnding()
		{
			// The four cells of the decision.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.SaysClimbHold(false, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.SaysClimbHold(true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.SaysClimbHold(false, false));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.SaysClimbHold(true, false));
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.ReleasesClimbHold(true, false));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.ReleasesClimbHold(false, false));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.ReleasesClimbHold(true, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.ReleasesClimbHold(false, true));

			// The life cycle, each step decided by the rule rather than by the test.
			bool held = false;
			// Stuck: said, and the ground remembers it.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.SaysClimbHold(held, true));
			held = true;
			// Still stuck, a later poll: nothing said, and nothing released.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.SaysClimbHold(held, true));
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.ReleasesClimbHold(held, true));
			// Finished: the completion path releases it.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.ReleasesClimbHold(held, false));
			held = false;
			// Stuck again on a later climb: said again.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.SaysClimbHold(held, true));
			held = true;
			// CANCELLED, which never reaches the completion path: the witness's own read is not
			// pending, so the saying is taken back there instead.
			ClassicAssert.IsTrue(KingdomFoundingHeartChainRules.ReleasesClimbHold(held, false));
			held = false;
			// And with nothing held, a cancelled or completed climb releases nothing.
			ClassicAssert.IsFalse(KingdomFoundingHeartChainRules.ReleasesClimbHold(held, false));
		}

		/// <summary>Both sides of the hold run that one rule, and the cancelled ending has a
		/// caller of its own.</summary>
		[Test]
		public void BothSidesOfTheHoldRunTheSharedRule()
		{
			string chain = Source("Growth/KingdomPlot2.07t.FoundingHeartClimbHold.cs");
			StringAssert.Contains("KingdomFoundingHeartChainRules.SaysClimbHold(held, true)",
				chain);
			StringAssert.Contains("KingdomFoundingHeartChainRules.ReleasesClimbHold(held, false)",
				chain);
			StringAssert.Contains("KingdomFoundingHeartChainRules.ReleasesClimbHold(held, pending)",
				chain);
			StringAssert.Contains("internal static void ReleaseSettledClimbHold(Zone Z, "
				+ "int RowWorkId)", chain);
			// The witness releases on an absent root whose climb is no longer pending -- which is
			// where a cancelled climb ends up, since it never completes.
			StringAssert.Contains("else if (count == 0) KingdomPlots.ReleaseSettledClimbHold(Zone, "
				+ "Row.WorkId);", Source("Core/KingdomInheritanceSpatial.Evidence.cs"));
			// And the flag's own docstring now names both endings.
			StringAssert.Contains("cleared when that improvement turns terminal EITHER WAY",
				Source("Growth/KingdomPlot2.07i.FoundingHeartTerminalAuthority.cs"));
		}

		/// <summary>
		/// VALUE. One row, one rect. The capture derives a row's rect for road evidence and the
		/// validator derives it again; both must read the same key, or one capture masks road
		/// cells under one footprint and validates them against another. The heart's first two
		/// rungs are 4x4 and 8x6, so a split would have disagreed by four cells across and two
		/// down on the very row this work is about.
		/// </summary>
		[Test]
		public void TheRoadEvidenceRectAndTheValidatorsRectAgreeForAClimbedRow()
		{
			const string retired = "heartbasin";
			const string standing = "heartwaterstone";
			int retiredWidth;
			int retiredHeight;
			int standingWidth;
			int standingHeight;
			ClassicAssert.IsTrue(KingdomInheritRules.TryFootprint(retired, out retiredWidth,
				out retiredHeight));
			ClassicAssert.IsTrue(KingdomInheritRules.TryFootprint(standing, out standingWidth,
				out standingHeight));
			ClassicAssert.AreNotEqual(retiredWidth + "x" + retiredHeight,
				standingWidth + "x" + standingHeight,
				"the two rungs must differ, or this case proves nothing");

			// The capture's fallback rect (built from width/height around the anchor) and the
			// validator's legacy proxy, both taken from the row's own persisted key.
			KingdomInheritanceSpatialRules.Rect validator;
			ClassicAssert.IsTrue(KingdomInheritanceSpatialRules.TryLegacyRect(retired, 40, 12,
				out validator));
			KingdomInheritanceSpatialRules.Rect capture = new KingdomInheritanceSpatialRules.Rect
			{
				X1 = 40 - (retiredWidth - 1) / 2,
				Y1 = 12 - (retiredHeight - 1) / 2,
				X2 = 40 - (retiredWidth - 1) / 2 + retiredWidth - 1,
				Y2 = 12 - (retiredHeight - 1) / 2 + retiredHeight - 1
			};
			ClassicAssert.AreEqual(validator.X1, capture.X1);
			ClassicAssert.AreEqual(validator.Y1, capture.Y1);
			ClassicAssert.AreEqual(validator.X2, capture.X2);
			ClassicAssert.AreEqual(validator.Y2, capture.Y2);

			// And what a split would have produced: the same anchor under the standing key is a
			// different rect, which is the divergence this revert exists to prevent.
			KingdomInheritanceSpatialRules.Rect split;
			ClassicAssert.IsTrue(KingdomInheritanceSpatialRules.TryLegacyRect(standing, 40, 12,
				out split));
			ClassicAssert.IsTrue(split.X2 != validator.X2 || split.Y2 != validator.Y2,
				"if the two keys gave the same rect the divergence would be invisible");
		}

		[Test]
		public void EveryDerivationForARowUsesTheRowsOwnPersistedKey()
		{
			string spatial = Source("Core/KingdomInheritanceSpatial.cs");
			StringAssert.Contains("string key = Record.WorkKeys[i];", spatial);
			StringAssert.Contains("KingdomInheritRules.TryFootprint(key, out width, out height)",
				spatial);
			StringAssert.Contains("KingdomInheritanceSpatialRules.TryLegacyRect(key, row.X,",
				spatial);
			// Nothing re-keys a row off the standing blueprint any more.
			StringAssert.DoesNotContain("TrySemanticKeyForBlueprint(standing", spatial);
			// The validator reads the same persisted keys the derivations above used.
			StringAssert.Contains("KingdomInheritanceSpatialRules.TryValidate(Record.WorkKeys,",
				spatial);
		}

	}
}
#endif
