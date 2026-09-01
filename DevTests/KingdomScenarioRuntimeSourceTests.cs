#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Ordering and refusal contracts for the engine-touching harness shards.
	/// <para>
	/// These are source contracts, not executed behaviour, and that limitation is deliberate rather
	/// than hidden: the runtime shards call into a live <c>XRLGame</c>, so the pure test assembly
	/// cannot construct them. Every invariant here is one where getting the ORDER wrong is the
	/// defect, so the order is asserted in text until a live-engine pass can execute it.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomScenarioRuntimeSourceTests
	{
		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static void AssertOrder(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		/// <summary>
		/// Native screenshot framing moves only the dev tester after proving one exact gallery lot,
		/// then centres Qud's presentation camera without changing options or world state. It cannot
		/// clear an obstruction or edit terrain to manufacture a prettier result.
		/// </summary>
		[Test]
		public void NativeFrameReprovesTheLotBeforeCenteringQudsCameraAtZoomOne()
		{
			string frame = Read("Harness/KingdomScenarioFrame.cs");
			AssertOrder(frame,
				"TryExactGalleryOwner(zone, out owner, out intent, out lot, out failure)",
				"KingdomArchitectureStamper.TryVerifyComplete(owner, zone, out failure)",
				"KingdomRealizedArchitectureCapture.TryCapture(owner, out beforeDigest",
				"FindTarget(zone, player, owner, intent.Rect, lot)",
				"player.SystemLongDistanceMoveTo(target",
				"KingdomArchitectureStamper.TryReadOwner(owner",
				"KingdomRealizedArchitectureCapture.TryCapture(owner, out afterDigest",
				"string.Equals(beforeDigest, afterDigest, StringComparison.Ordinal)",
				"TryCenterCamera(target, intent.Rect, out failure)",
				"Ok = true");
			AssertOrder(frame,
				"manager.uiQueue.awaitTask",
				"manager.TargetZoomFactor = 1f",
				"manager.SetPlayerCell(new Point2D(Target.X, Target.Y), updateCamera: false)",
				"manager.RefreshLayout(updateForceFullscreenIfSwapped: true)",
				"manager.CenterOnCell(Rect.CenterX, Rect.CenterY)",
				"GameManager.MainCameraLetterbox.OnUpdate()");
			AssertOrder(frame,
				"KingdomScenarioGallerySlice.CarriesGalleryAuthority(candidate)",
				"if (Owner != null)",
				"KingdomArchitectureStamper.TryReadOwner(Owner");
			StringAssert.Contains("Cell.IsPassable(Player, false)", frame);
			StringAssert.Contains("Cell.IsEmptyOfSolid()", frame);
			StringAssert.Contains("Cell.HasOpenLiquidVolume()", frame);
			StringAssert.Contains("BelongsToLot(item, Lot)", frame);
			StringAssert.Contains("blueprint.InheritsFrom(\"Floor\")", frame);
			StringAssert.Contains("KingdomArchitectureStamper.LotIdProperty", frame);
			StringAssert.Contains("KingdomPlots.PlotIdProperty", frame);
			StringAssert.DoesNotContain("Obliterate", frame);
			StringAssert.DoesNotContain("Destroy(", frame);
			StringAssert.DoesNotContain("RemoveObject", frame);
			StringAssert.DoesNotContain("Options.SetOption", frame);
			StringAssert.DoesNotContain("TargetZoomFactor = 1.25", frame);
			StringAssert.DoesNotContain("terrain were not changed", frame);
		}

		/// <summary>
		/// Hosted navigation may realize a lazy vanilla zone, but it proves that target before moving
		/// and may neither manufacture shell/receipts nor use gallery authority.
		/// </summary>
		[Test]
		public void HostedArcologyNavigationProvesAuthorityBeforeLoadingAndNeverForces()
		{
			string source = Read("Harness/KingdomScenarioHostedArcology.cs");
			AssertOrder(source,
				"GalleryInCurrentContext(zone)",
				"TryReadAuthorityIdentityForJointView(system",
				"authority.Phase != KingdomHostedAuthorityPhase.Active",
				"TryLoadedRoot(player, authority",
				"KingdomScenarioGallerySlice.CarriesGalleryAuthority(root)",
				"TryExactAuthority(system, root, authority",
				"KingdomCrown.CrownedOn(system",
				"TryPaidReceipt(hosted, lotKey",
				"TryTargetZoneId(interior, root",
				"interior.CanEnter(player, Action: true, ShowMessage: false)",
				"The.ZoneManager.GetZone(targetZoneId)",
				"TryExactZone(target, root",
				"anchor.FixturesRealized",
				"TryPaidReceipt(hosted, lotKey",
				"TryExactAuthority(system, root, authority",
				"KingdomCrown.CrownedOn(system",
				"ReferenceEquals(player.CurrentZone, originZone)",
				"SystemLongDistanceMoveTo(destination",
				"ReferenceEquals(player.CurrentCell, destination)",
				"TryExactZone(target, root",
				"TryPaidReceipt(hosted, lotKey",
				"TryExactAuthority(system, root, authority");
			StringAssert.Contains("case \"entry\": X = 1; Y = 1; Z = 10;", source);
			StringAssert.Contains("case \"teaching\": X = 1; Y = 0; Z = 10;", source);
			StringAssert.Contains("case \"terrace\": X = 1; Y = 1; Z = 9;", source);
			StringAssert.Contains("case \"ward\": X = 0; Y = 1; Z = 11;", source);
			StringAssert.Contains("receipt.Phase != KingdomHostedLotPhase.Active", source);
			StringAssert.Contains("forced: false, ignoreCombat: false", source);
			StringAssert.Contains("anchor.FixturesRealized", source);
			StringAssert.DoesNotContain("interior.TryEnter(", source);
			StringAssert.DoesNotContain("player.DirectMoveTo(", source);
			int move = source.IndexOf("player.SystemLongDistanceMoveTo(", StringComparison.Ordinal);
			Assert.Greater(move, -1);
			Assert.AreEqual(move, source.LastIndexOf(
				"player.SystemLongDistanceMoveTo(", StringComparison.Ordinal));
			StringAssert.DoesNotContain("KingdomHostedArcology.TryReserve", source);
			StringAssert.DoesNotContain("KingdomHostedArcology.BindAuthority", source);
			StringAssert.DoesNotContain("KingdomHostedArcology.BeginLot", source);
			StringAssert.DoesNotContain("KingdomHostedArcology.SetReceipt", source);
			StringAssert.DoesNotContain("SetStringGameState", source);
			StringAssert.DoesNotContain("RequireSystem<KingdomSystem>", source);
			StringAssert.DoesNotContain("GameObject.Create", source);
			StringAssert.DoesNotContain("KingdomHostedArcologyBuilder", source);
			StringAssert.DoesNotContain("BuildZone(", source);
			StringAssert.DoesNotContain("new InteriorZone", source);
			StringAssert.DoesNotContain("forced: true", source);
		}

		/// <summary>
		/// A terminal row precedes vanilla OpeningStory's later same-cycle event. The runner restores
		/// its broad boot bracket; an exact Harmony patch then brackets only the untriggered story in
		/// a sealed profile and restores only the false value it changed, including on exception.
		/// </summary>
		[Test]
		public void OpeningStoryPopupHasANarrowExceptionSafeScenarioBracket()
		{
			string runner = Read("Harness/KingdomScenarioAutoRunner.cs");
			int finish = runner.IndexOf("private void Finish(", StringComparison.Ordinal);
			int release = runner.IndexOf("private void Release()", finish, StringComparison.Ordinal);
			Assert.Greater(finish, -1);
			Assert.Greater(release, finish);
			string finishBody = runner.Substring(finish, release - finish);
			AssertOrder(finishBody, "KingdomScenarioJournal.Append(Row, Ok, Message)", "Release();");

			string entry = Read("Harness/KingdomScenarioTestGameEntry.cs");
			AssertOrder(entry,
				"[HarmonyPatch(typeof(OpeningStory), \"HandleEvent\"",
				"typeof(BeforeTakeActionEvent)",
				"__instance.Triggered || Popup.Suppress",
				"!KingdomScenarioScript.Present()",
				"Popup.Suppress = true",
				"if (__state) Popup.Suppress = false",
				"return __exception");
			StringAssert.Contains("[HarmonyFinalizer]", entry);
		}

		/// <summary>
		/// The attempt marker is durable BEFORE the sole mutating call, and the commit lands before
		/// any reporting. Gallery staging does not journal ground cells, so a cut between mutation
		/// and owner creation must leave a permanently non-retryable profile.
		/// </summary>
		[Test]
		public void AttemptIsRecordedBeforeMutationAndCommitBeforeReporting()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			AssertOrder(run,
				"TryProvePreconditions",
				"KingdomScenarioTransactionMarker.TryBegin(out Failure)",
				"KingdomScenarioGallerySlice.TryStage",
				"KingdomScenarioTransactionMarker.TryCommit(out commitFailure)",
				"Report = Conclude(");
		}

		[Test]
		public void GalleryPreflightUsesTheExactRequestedCaseBeforeTheAttemptMarker()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			StringAssert.Contains("TryProvePreconditions(zone, expected, out Failure)", run);
			string slice = Read("Harness/KingdomScenarioGallerySlice.cs");
			AssertOrder(slice,
				"KingdomArchitecture.TryResolveVariant(Expected.BuildKey",
				"KingdomArchitectureRules.TryWorldDimensions(snapshot.Width",
				"KingdomArchitectureGalleryWishes.TryFindCanvas(Zone, width, height");
			StringAssert.DoesNotContain("TryFindCanvas(Zone, 10, 8", slice);
		}

		[Test]
		public void GroundPreparationPlansTheExactPoseWithExteriorParkingAndReproof()
		{
			string ground = Read("Harness/KingdomScenarioGround.cs");
			AssertOrder(ground,
				"KingdomScenarioRealizer.TryBindStampedPlan",
				"KingdomScenarioRun.TryExpectedGalleryCase",
				"KingdomArchitecture.TryResolveVariant",
				"KingdomArchitectureRules.TryWorldDimensions");
			StringAssert.DoesNotContain("ProbeWidth", ground);
			StringAssert.Contains("clearance.Contains(x, y)", ground);
			StringAssert.Contains("FindParkingCell(zone, rect, connections)", ground);
			StringAssert.Contains("SafeCanvas(zone, rect, connections", ground);

			string flatten = Read("Harness/KingdomScenarioFlatten.cs");
			AssertOrder(flatten,
				"KingdomScenarioGround.TryExactDimensions",
				"KingdomPlotRules.TryInsetOriginBounds",
				"TryProveClearable(zone, candidate",
				"KingdomScenarioGround.FindParkingCell",
				"player.SystemLongDistanceMoveTo(parking",
				"connections = KingdomArchitectureGalleryWishes.ConnectionCells(zone)",
				"TryProveClearable(zone, chosen",
				"TryClear(zone, clearance",
				"KingdomArchitectureGalleryWishes.SafeCanvas(zone, chosen",
				"KingdomArchitectureGalleryWishes.TryFindCanvas(zone, width, height");
			StringAssert.Contains("ReferenceEquals(item, Player)", flatten);
			StringAssert.Contains("item.Physics.Solid", flatten);
			StringAssert.Contains("KingdomPlots.ReadObject(item)", flatten);
			StringAssert.DoesNotContain("ProbeWidth", flatten);
			StringAssert.DoesNotContain("RequireSystem<KingdomSystem>", flatten);
		}

		/// <summary>Every non-None marker state refuses, and refuses permanently.</summary>
		[Test]
		public void EveryPriorMarkerStateRefusesTheRun()
		{
			string marker = Read("Harness/KingdomScenarioTransaction.cs");
			StringAssert.Contains("already attempted its production transaction", marker);
			StringAssert.Contains("retried here", marker);
			StringAssert.Contains("already committed its production transaction", marker);
			StringAssert.Contains("is torn", marker);
			string run = Read("Harness/KingdomScenarioRun.cs");
			AssertOrder(run,
				"KingdomScenarioTransactionMarker.Observe(out transactionDetail)",
				"observed != KingdomScenarioTransactionShape.None",
				"KingdomScenarioGallerySlice.TryStage");
		}

		/// <summary>A refused mutation leaves the attempt standing; the profile is spent.</summary>
		[Test]
		public void ARefusedMutationLeavesTheProfileSpent()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			StringAssert.Contains("The attempt marker stands, so this profile is spent", run);
			int refusal = run.IndexOf("the production transaction refused", StringComparison.Ordinal);
			Assert.Greater(refusal, -1);
			StringAssert.DoesNotContain("SetIntGameState", run);
		}

		/// <summary>Capture and evidence run after the commit and never undo or repeat it.</summary>
		[Test]
		public void CaptureFailureAfterCommitReportsRatherThanRestaging()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			StringAssert.Contains("The transaction committed; the differential comparison did not run.",
				run);
			int conclude = run.IndexOf("private static string Conclude(", StringComparison.Ordinal);
			Assert.Greater(conclude, -1);
			string body = run.Substring(conclude);
			StringAssert.DoesNotContain("TryStage", body);
			StringAssert.DoesNotContain("TryBegin", body);
			StringAssert.DoesNotContain("TryCommit", body);
		}

		/// <summary>A replay is refused before anything is planned or staged.</summary>
		[Test]
		public void ReplayIsRefusedBeforeTheTransaction()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			AssertOrder(run,
				"KingdomScenarioTransactionMarker.Observe(out transactionDetail)",
				"KingdomScenarioGallerySlice.TryStage");
		}

		/// <summary>Read-only observations all prove out before the single transaction.</summary>
		[Test]
		public void ObservationsProveOutBeforeTheMutation()
		{
			string run = Read("Harness/KingdomScenarioRun.cs");
			AssertOrder(run,
				"if (KingdomScenarioVerbSchema.Mutates(step.Verb)) { mutation = step; continue; }",
				"observation refused before any mutation",
				"TryProvePreconditions",
				"KingdomScenarioGallerySlice.TryStage");
		}

		/// <summary>
		/// The presence marker is written after the stamp and read before it, and presence is judged
		/// from raw key presence before any decode is attempted.
		/// </summary>
		[Test]
		public void PresenceMarkerIsWrittenLastAndReadFirst()
		{
			// The provenance write and the presence reader moved to the authority shard when the
			// two copies were merged into one write path; the ordering claim follows them there.
			string authority = Read("Harness/KingdomScenarioStampAuthority.cs");
			AssertOrder(authority,
				"SetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, wire)",
				"the scenario stamp did not read back exactly");
			// The realizer keeps only the marker write, and it still comes AFTER the shared write.
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			AssertOrder(realizer,
				"TryWriteProvenance(Record, out Failure)",
				"The.Game.SetIntGameState(StampedState, KingdomScenarioStateShape.MarkerValue);");
			int presence = authority.IndexOf(
				"internal static KingdomScenarioStampShape Presence(", StringComparison.Ordinal);
			Assert.Greater(presence, -1);
			string body = authority.Substring(presence);
			AssertOrder(body,
				"KingdomScenarioStampShape shape = Shape(out Failure);",
				"if (shape != KingdomScenarioStampShape.Readable) return shape;",
				"TryDecode(raw, out Record, out Failure)");
		}

		/// <summary>An already-stamped or torn game is refused rather than overwritten.</summary>
		[Test]
		public void OpeningRefusesAnAlreadyStampedGame()
		{
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			AssertOrder(realizer,
				"already carries a scenario stamp; refusing to overwrite it",
				"already carries scenario provenance in a shape no gate",
				"already carries a scenario transaction marker",
				"KingdomScenarioRequest.TryPlan(Request, out plan, out Failure)");
		}

		/// <summary>The engine's own seed evidence is proved before anything is stamped.</summary>
		[Test]
		public void SeedEvidenceIsProvedBeforeStamping()
		{
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			AssertOrder(realizer,
				"if (!TryProveSeed(plan.Seed, out actualSeed, out Failure)) return false;",
				"Seed = actualSeed,",
				"return TryStamp(record, out Failure);");
			StringAssert.Contains("The.Game.GetWorldSeed() != declaredInt", realizer);
			StringAssert.Contains("EngineSeedState = \"OriginalWorldSeed\"", realizer);
		}

		/// <summary>The attended run re-proves the whole stamped tuple, seed included.</summary>
		[Test]
		public void AttendedRunRebindsTheWholeStampedTuple()
		{
			string realizer = Read("Harness/KingdomScenarioRealizer.cs");
			int bind = realizer.IndexOf("internal static bool TryBindStampedPlan(",
				StringComparison.Ordinal);
			Assert.Greater(bind, -1);
			string body = realizer.Substring(bind);
			StringAssert.Contains("KingdomScenarioProvenanceRules.TryValidateStampShape", body);
			StringAssert.Contains("plan.PlanDigest, Record.PlanDigest", body);
			StringAssert.Contains("plan.AnchorId ?? \"\", Record.AnchorId ?? \"\"", body);
			StringAssert.Contains("TryProveSeed(Record.Seed", body);
			StringAssert.Contains("TryReadRequest(out request, out present, out detail)", body);
		}

		/// <summary>
		/// The ordinary-play capture emits a row for human curation and never writes the store.
		/// Eligibility is the ONE fail-closed proof, shared with the anchor status, so a torn or
		/// deleted stamp can never launder a prepared scenario profile into an ordinary anchor.
		/// </summary>
		[Test]
		public void OrdinaryPlayCaptureNeverInsertsAnAnchor()
		{
			string report = Read("Harness/KingdomScenarioCaptureReport.cs");
			StringAssert.Contains("KingdomScenarioDurableState.OrdinaryAnchorEligible(out failure)",
				report);
			StringAssert.Contains("this game is not ordinary play", report);
			StringAssert.Contains("Nothing was written to the anchor store", report);
			StringAssert.DoesNotContain("Anchors.Add", report);
			string verbs = Read("Harness/KingdomScenarioVerbs.cs");
			StringAssert.Contains("KingdomScenarioDurableState.OrdinaryAnchorEligible(out failure)",
				verbs, "the status must not claim more than the capture would allow");
			string store = Read("Harness/KingdomScenarioAnchorStore.cs");
			StringAssert.DoesNotContain("internal static void Insert", store);
			StringAssert.DoesNotContain("SetStringGameState", store);
		}

		/// <summary>
		/// The building is chosen by the plan's own frozen case, then by an explicit selector.
		/// Enumeration order never decides which building a curated anchor was measured from.
		/// </summary>
		[Test]
		public void OrdinaryPlayCaptureSelectsTheFrozenCaseDeterministically()
		{
			string report = Read("Harness/KingdomScenarioCaptureReport.cs");
			AssertOrder(report,
				"KingdomScenarioRun.TryExpectedGalleryCase(Plan, out expected, out Failure)",
				"KingdomScenarioSelectorRules.TryParse(",
				"KingdomScenarioGallerySlice.TryProveExactCase(candidate, expected, out ignored)",
				"KingdomScenarioSelectorRules.Resolve(",
				"the selected building is no longer the frozen case");
			StringAssert.Contains("moved or changed identity after selection", report);
			StringAssert.DoesNotContain("owners.Count == 1", report);
			StringAssert.DoesNotContain("owners[0]", report);
		}

		/// <summary>
		/// Both paths measure the SAME production capture. If the harness held its own copy, a
		/// scenario could self-sign its expected output.
		/// </summary>
		[Test]
		public void BothPathsCallTheOneProductionRealizedCapture()
		{
			string capture = Read("Harness/KingdomScenarioCapture.cs");
			StringAssert.Contains("KingdomRealizedArchitectureCapture.TryCapture", capture);
			StringAssert.Contains("architecture.realized.digest", capture);
			string report = Read("Harness/KingdomScenarioCaptureReport.cs");
			StringAssert.Contains("KingdomScenarioCapture.TryMeasure", report);
			string run = Read("Harness/KingdomScenarioRun.cs");
			StringAssert.Contains("KingdomScenarioCapture.TryMeasure", run);
			string keys = Read("Harness/KingdomScenarioAnchorRules.cs");
			StringAssert.Contains("architecture.realized.digest", keys);
		}

		/// <summary>The staged case is proved field by field, never by a substring on a label.</summary>
		[Test]
		public void StagedCaseIsProvedFieldByField()
		{
			string slice = Read("Harness/KingdomScenarioGallerySlice.cs");
			StringAssert.Contains("TryProveExactCase", slice);
			foreach (string field in new string[]
				{ "intent.BuildKey", "intent.VariantKey", "intent.LotType", "intent.LotSize",
					"intent.Facing" })
				StringAssert.Contains(field, slice);
			StringAssert.DoesNotContain("IndexOf(ExpectedFacing", slice);
		}
	}
}
#endif
