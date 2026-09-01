#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomFoundingHeartSourceTests
	{
		private static string Source(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static string Slice(string SourceText, string Start, string End)
		{
			int first = SourceText.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, Start);
			int last = SourceText.IndexOf(End, first + Start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, End);
			return SourceText.Substring(first, last - first);
		}

		private static void Ordered(string SourceText, params string[] Terms)
		{
			int cursor = 0;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = SourceText.IndexOf(Terms[i], cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(found, 0, Terms[i]);
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
			Ordered(drive, "GameObject.Create(", "SetIntProperty(FoundingHeartSlotMark",
				"PreparedFoundingHeartMarkShape(", "StageFoundingHeartIdentity(",
				"RootFoundingHeartOutput(", "AdvanceFoundingHeart(Z, Context, Slot, 0, 1)");
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
			foreach (string evidence in new[] { "SetStringGameState", "SlotCount",
				"FoundingHeartFinalId", "AuditFoundingHeartReservations",
				"ZoneActivated audits every", "never asks ZoneManager" })
				StringAssert.Contains(evidence, reservations);
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
			Assert.Greater(finalHeartProof, firstHeartProof,
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
			StringAssert.Contains("if (!published && GameObject.Validate(Final))", begin);
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
			StringAssert.Contains("Graveyard.Objects", graveyard);
			StringAssert.Contains("try { Id = Item.IDIfAssigned; return true; }", graveyard);
			StringAssert.Contains("native graveyard identity is unreadable", graveyard);
			StringAssert.DoesNotContain("GetInventoryDirectAndEquipment", graveyard);
			string jobEffects = Source("Growth/KingdomPlot2.34.EffectsAndFurnishing.cs");
			Ordered(jobEffects, "KingdomCeremonyHeart.OnRungRaised(",
				"ExactPlotFinalRootCustody(Job.OutputId, Building)");
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
			Ordered(custody, "HashSet<GameObject> graveyard", "Graveyard?.Objects",
				"graveyard.Contains(item)");
			string reservations = Source(
				"Growth/KingdomPlot2.07l.FoundingHeartReservations.cs");
			foreach (string proof in new[] { "The.Game.StringGameState",
				"FoundingHeartReservationPrefix", "TryReadFoundingHeartReservation",
				"Z.GetObjects()", "reservations.TryGetValue(id", "owner != transaction",
				"zone != Z.ZoneID" }) StringAssert.Contains(proof, reservations);
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
				"Growth/KingdomFoundingHeartTerminalPlan.cs",
				"Growth/KingdomFoundingHeartTerminalRules.cs",
				"Growth/KingdomPlotLegacyEffectsPlan.cs",
				"Growth/KingdomPlotLegacyEffectsRules.cs",
				"Growth/KingdomPlot2.33b.LegacyEffects.cs" })
			{
				int lines = Source(path).Split('\n').Length;
				Assert.Less(lines, 300, path + " has " + lines + " physical lines");
			}
		}
	}
}
#endif
