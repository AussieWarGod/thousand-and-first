#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomArchitectureLabourRuntimeSourceTests
	{
		[Test]
		public void PlotWakeConsumesPriorWitnessBeforeCapturingCurrentLoadedCrew()
		{
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot,
				"KingdomPlotLabourRules.Assess(receipt, TimeTick)",
				"if (TimeTick < receipt.LastTick) return true",
				"if (TimeTick == receipt.LastTick)",
				"KingdomPlotLabourWindowRules.TryForInterval(",
				"receipt.LastTick, out prior)",
				"KingdomPlotLabourRules.Advance(receipt, TimeTick,",
				"witnessed ? prior.LabourPercent : 0",
				"witnessed ? prior.InfrastructurePercent : 0",
				"SetPlotWorkLong(parent, PlotWorkLastTickProperty, step.NextTick)",
				"SetPlotWorkLong(parent, PlotWorkRemainingProperty, step.RemainingTicks)",
				"if (step.Complete) return true",
				"return CaptureCurrentWitness",
				"? TryCapturePlotLabourWindow(parent, System, TimeTick",
				"KingdomConstructionPresence.EffectivenessOf(Root, System",
				"KingdomPlotLabourWindowRules.TryEncode(current, out string encoded)",
				"Root.SetStringProperty(PlotWorkWindowProperty, encoded)",
				"SayPlotInfrastructure(System, Root",
				"if (selected) SayPlotWorkShortfall");
			StringAssert.Contains("parent.RemoveStringProperty(PlotWorkWindowProperty)", plot);
		}

		[Test]
		public void PlotWitnessRequiresExactLoadedCrewSchema()
		{
			string window = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			AssertOrdered(window,
				"KingdomConstructionPresence.EffectivenessOf(Root, System",
				"KingdomConstructionPresence.SchemaProperty",
				"!= KingdomConstructionPresenceRules.Schema",
				"effectiveness = 0", "freeHands = 0", "selected = false",
				"KingdomPlotLabourWindowRules.TryEncode(current, out string encoded)");
		}

		[Test]
		public void DirectPlotStakeRemainsReceiptlessLegacyCalendar()
		{
			string stake = TestMain.ReadRepositoryText("Growth/KingdomPlot2.11.Stake.cs");
			AssertOrdered(stake, "if (Job != null)",
				"works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema)",
				"SetPlotWorkLong(works, PlotWorkRequiredProperty, part.TotalTicks)");
			ClassicAssert.AreEqual(1, Count(stake,
				"works.SetIntProperty(PlotWorkSchemaProperty, PlotWorkSchema)"));
		}

		[Test]
		public void PlotInfrastructureUsesOnlyFrozenReceiptsAndLoadedBooleanYardLaw()
		{
			string construction = KingdomConstructionLogicalSource.Read();
			AssertOrdered(construction,
				"KingdomConstructionPresence.Assign(System, Survey)",
				"KingdomMaterials.YardsStanding(Z)",
				"PlotInfrastructurePercent(plot, works, labourJob",
				"KingdomPlots.Advance(works, System, The.Game.TimeTicks");
			string authority = TestMain.ReadRepositoryText(
				"Growth/KingdomConstruction.PlotLabour.cs");
			AssertOrdered(authority, "Job.Phase != KingdomConstructionPhase.Working",
				"!Owns(System, Z, Job)", "!IsCurrent(Job)",
				"Job.Route != KingdomConstructionRoute.PlotCommission",
				"Job.Route != KingdomConstructionRoute.PlotPlan",
				"KingdomConstructionRules.TryReadBuildTruth(Job,",
				"out bool hasPlot", "!hasPlot",
				"Root.IDIfAssigned != Job.OutputId", "Root.IDIfAssigned != Job.SubjectId",
				"TryFind(Job.Id, out KingdomConstructionJob current)",
				"current.Revision != Job.Revision", "current.Payload != Job.Payload",
				"current.OutputId != Job.OutputId",
				"FindExactId(Z, Job.OutputId", "FindReceipt(Z, Job",
				"Root.CurrentCell != expected",
				"KingdomConstructionRules.TryPaidBuildReceipt(Job, null",
				"KingdomPlots.TryDecodePlotPayload(Job.Payload",
				"architecture.LotSize",
				"KingdomMaterialRules.AllowsBuild(size, paid.Material.Materials, Yards");
			StringAssert.DoesNotContain("KingdomMaterials.CostFor", authority);
			StringAssert.DoesNotContain("KingdomPlots.TryGetSpec", authority);
			AssertOrdered(construction, "TryPlotLabourAuthority(System, Z, plot",
				"KingdomPlots.ConsumePlotLabourAtZero(works, System",
				"PlotInfrastructurePercent(plot, works, labourJob",
				"KingdomPlots.Advance(works, System, The.Game.TimeTicks");
			string plot = KingdomPlot2LogicalSource.Read();
			AssertOrdered(plot, "bool PricePriorWitness = true",
				"PricePriorWitness && KingdomPlotLabourWindowRules.TryForInterval(",
				"internal static void ConsumePlotLabourAtZero(",
				"out ignored, false, false)");
		}

		[Test]
		public void UnauthorizedEqualAndForwardPassesPublishOnlyTickMatchedZero()
		{
			string labour = TestMain.ReadRepositoryText("Growth/KingdomPlot2.26.Labour.cs");
			AssertOrdered(labour, "bool CaptureCurrentWitness = true",
				"if (TimeTick == receipt.LastTick)", "return CaptureCurrentWitness",
				": TryCaptureZeroPlotLabourWindow(parent, System, TimeTick",
				"SetPlotWorkLong(parent, PlotWorkLastTickProperty, step.NextTick)",
				"return CaptureCurrentWitness",
				": TryCaptureZeroPlotLabourWindow(parent, System, TimeTick",
				"internal static void ConsumePlotLabourAtZero(",
				"out ignored, false, false)");
			string window = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			string zero = window.Substring(window.IndexOf(
				"private static bool TryCaptureZeroPlotLabourWindow", StringComparison.Ordinal));
			AssertOrdered(zero, "Tick = TimeTick", "LabourPercent = 0",
				"InfrastructureUnavailable", "Hands = 0", "Selected = false",
				"TryEncode(zero, out string encoded)",
				"Root.SetStringProperty(PlotWorkWindowProperty, encoded)",
				"Root.GetStringProperty(PlotWorkWindowProperty) != encoded");
			StringAssert.DoesNotContain("EffectivenessOf", zero);
		}

		/// <summary>
		/// A paid raising whose ground layer is refused by a living occupant stands our own
		/// residents off the site and retries in the SAME pass; the player, a stranger, or one of
		/// ours posted into the layout is never moved and is said once. Pinned at the three sites
		/// because the stamper refusal itself is unchanged.
		/// </summary>
		[Test]
		public void AnOccupiedGroundLayerClearsOurOwnAndOtherwiseAnnouncesOnce()
		{
			string labour = TestMain.ReadRepositoryText("Growth/KingdomPlot2.26.Labour.cs");
			AssertOrdered(labour,
				"private static bool Apply(r_KingdomPlotWorks Works, KingdomPlotRules.PlotStage Stage,",
				"KingdomSystem System)",
				"case KingdomPlotRules.PlotStage.Cleared:",
				"if (currentAuthored && !TryGroundStageWithOccupants(System, zone, parent,",
				"Works, managed, authored, plot, out string groundFailure))",
				"KingdomLog.Log(\"architecture: ground layer refused: \" + groundFailure)",
				"return false;");
			string clearance = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26c.OccupantClearance.cs");
			AssertOrdered(clearance,
				"internal static bool TryGroundStageWithOccupants(",
				"KingdomArchitectureStamper.TryStageLayer(Root, Z,",
				"if (!ground && KingdomPlotRules.IsOccupantSlotRefusal(Failure))",
				"bool stoodOff = KingdomArchitectureStamper.TryPlacementPassability(Authored, Z,",
				"out Dictionary<int, ArchitecturePassability> slots,",
				"&& TryClearManagedOccupants(System, Z, Root, Managed, slots, Rect,",
				"ground = KingdomArchitectureStamper.TryStageLayer(Root, Z,",
				"SayPlotWorkCleared(System, Root, name, cleared, ground,",
				"ground ? null : (stoodOff ? Failure : clearanceRefusal))",
				"if (!ground && verdict == KingdomPlotRules.OccupantVerdict.AnchorBound",
				"&& anchor != null)",
				"KingdomPlotRules.RefuseOccupiedAnchor(name, anchor.X, anchor.Y)",
				"SayPlotWorkOccupied(System, Root, slot,",
				"KingdomPlotRules.RefuseOccupiedSlot(name, slot)");
			// Plan before effect: classify, prove every destination, then move. The destination
			// excludes every layout slot, the plot rect, liquid, impassable and occupied ground,
			// and the lawful system move is checked by return value AND by re-reading the cell.
			AssertOrdered(clearance,
				"internal static bool TryClearManagedOccupants(",
				"KingdomSurvey survey = KingdomSurvey.ActiveFor(Z)",
				"foreach (KeyValuePair<int, ArchitecturePassability> slot in Slots)",
				"if (!KingdomPlotRules.SlotBlocksOccupant(slot.Value)) continue;",
				"if (!item.IsCreature && !item.IsPlayer()) continue;",
				"KingdomPlotRules.OccupantReason reason = ReasonFor(System, survey, item)",
				"occupants.Add(item)",
				"if (reason == KingdomPlotRules.OccupantReason.Player) { player = true; continue; }",
				"if (reason != KingdomPlotRules.OccupantReason.Resident) continue;",
				"Cell anchor = PostAnchorInLayout(Z, item, Managed)",
				"Verdict = KingdomPlotRules.JudgeOccupants(occupants.Count, residents, player,",
				"if (Verdict != KingdomPlotRules.OccupantVerdict.Displace)",
				"Cell target = FreeGroundOffLayout(Z, occupants[i], Managed, Rect, taken)",
				"if (target == null)",
				"return ClearanceFault(\"no free ground beside the site to stand them on\",",
				"plan.Add(new KingdomLayoutDisplacement(occupants[i], occupants[i].CurrentCell,",
				"move.Body.SystemLongDistanceMoveTo(move.Target, 0, forced: true,",
				"&& move.Body.CurrentCell == move.Target)",
				"int back = WalkBack(plan, i + 1, out int stranded)",
				"Moved = stranded;",
				"return ClearanceFault(\"a settler would not stand off the site; \" + back",
				"Moved = walked;");
			// A half-cleared site is put back: every body already walked returns to the exact
			// ground it stood on, and the fault names both counts.
			AssertOrdered(clearance,
				"private static int WalkBack(List<KingdomLayoutDisplacement> Plan, int Count,",
				"|| move.Body.CurrentCell == move.Origin) continue;",
				"move.Body.SystemLongDistanceMoveTo(move.Origin, 0, forced: true,",
				"&& move.Body.CurrentCell == move.Origin)",
				"back++;",
				"Stranded++;");
			string labourWindow = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			StringAssert.Contains(
				"KingdomPlotRules.ClearedOccupiedSlots(\n\t\t\t\t\tName ?? \"work\", Moved, Raised, Fault)",
				labourWindow);
			// Every refusing body is named with its reason before the parsed summary sentence.
			string helpers = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26d.OccupantHelpers.cs");
			// The stall line is gated on paid labour AND a changed pair, and the passability it
			// prints is read from the placement lookup, never a literal.
			AssertOrdered(helpers,
				"private static void SayPlotStageWaiting(",
				"TryGetPlotWorkLong(root, PlotWorkRemainingProperty, out long owed)",
				"KingdomPlotRules.StageWaitingPair(Works.StageApplied, (int)Target)",
				"if (!KingdomPlotRules.ShouldSayStageWaiting(remaining, Works.StageApplied,",
				"root.GetStringProperty(PlotStageWaitingLastProperty), pair)) return;",
				"root.SetStringProperty(PlotStageWaitingLastProperty, pair)",
				"KingdomLog.Log(\"plot stage waiting: \"",
				"private static void NameOccupants(",
				"Slots.TryGetValue(index, out passability)",
				"KingdomLog.Log(\"architecture: occupant \" + body.IDIfAssigned",
				"body.Blueprint",
				"passability=\" + (authored ? passability.ToString() : \"none\")",
				"reason=\" + Reasons[i])",
				"private static KingdomPlotRules.OccupantReason ReasonFor(",
				"if (Body.IsPlayer()) return KingdomPlotRules.OccupantReason.Player;",
				"KingdomPlotRules.OccupantReason.PlayerLed",
				"KingdomPlotRules.OccupantReason.NotOurs",
				"Simulation.City.KingdomPhysicalHappenings.IsStaged(Body)",
				"KingdomPlotRules.OccupantReason.Staged",
				"Simulation.City.KingdomResidents.IdOf(Body)",
				"if (id <= 0) return KingdomPlotRules.OccupantReason.NoRoll;",
				"row.Standing == Simulation.City.KingdomResidentStanding.Resident",
				"KingdomPlotRules.OccupantReason.Resident",
				"KingdomPlotRules.OccupantReason.NotResident",
				"private static Cell FreeGroundOffLayout(",
				"Managed.Contains(candidate.Y * Z.Width + candidate.X)",
				"candidate.HasOpenLiquidVolume()",
				"!candidate.IsPassable(Body) || HoldsLivingBody(candidate)");
			StringAssert.DoesNotContain("DirectMoveTo", clearance);
			StringAssert.DoesNotContain("TeleportTo", clearance);
			StringAssert.DoesNotContain("DirectMoveTo", helpers);
			StringAssert.DoesNotContain("TeleportTo", helpers);
			// A body only blocks a slot the map declares Blocked: the stamper gates its refusal on
			// the declared passability, and the clearance walks only the blocking cells.
			string verification = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.Verification.cs");
			AssertOrdered(verification,
				"bool blocks = KingdomPlotRules.SlotBlocksOccupant(PassabilityOf(Snapshot, Placement))",
				"if (item.IsCreature || item.IsPlayer())",
				"if (!blocks) continue;",
				"KingdomLog.Log(\"architecture: blocked slot \" + Placement.Slot + \" holds \"",
				"return Fail(KingdomPlotRules.OccupantSlotRefusalPrefix + Placement.Slot,",
				"internal static ArchitecturePassability PassabilityOf(",
				"return ArchitecturePassability.Blocked;");
			string receipts = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.OwnerReceipts.cs");
			AssertOrdered(receipts,
				"public static bool TryPlacementPassability(",
				"ArchitecturePassability passability = PassabilityOf(snapshot, placement)",
				"|| KingdomPlotRules.SlotBlocksOccupant(passability)) result[key] = passability;");
			StringAssert.DoesNotContain("passability=\" + ArchitecturePassability", helpers);
			AssertOrdered(labour,
				"if ((int)target <= Works.StageApplied)",
				"SayPlotStageWaiting(Works, target)");
			string window = TestMain.ReadRepositoryText(
				"Growth/KingdomPlot2.26b.LabourWindow.cs");
			string said = window.Substring(window.IndexOf(
				"private static void SayPlotWorkOccupied", StringComparison.Ordinal));
			AssertOrdered(said,
				"if (Slot == null)",
				"Works.SetIntProperty(PlotWorkOccupantAnnouncedProperty, 0)",
				"Works.SetStringProperty(PlotWorkOccupantSlotProperty, null, RemoveIfNull: true)",
				"KingdomPlotRules.ShouldAnnounceOccupiedSlot(",
				"Works.GetIntProperty(PlotWorkOccupantAnnouncedProperty)",
				"Works.GetStringProperty(PlotWorkOccupantSlotProperty), Slot)",
				"Works.SetIntProperty(PlotWorkOccupantAnnouncedProperty, 1)",
				"Works.SetStringProperty(PlotWorkOccupantSlotProperty, Slot)",
				"System.Ledger.Note(");
			// The one gang is not held by a paid, physically unbuilt root: apply retries on its
			// own, so labour candidacy stays exactly the spent-clock predicate it was.
			string presence = TestMain.ReadRepositoryText(
				"Growth/KingdomConstructionPresence.Helpers.cs");
			StringAssert.Contains("&& remaining > 0L;", presence);
			string stamper = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.Verification.cs");
			StringAssert.Contains(
				"return Fail(KingdomPlotRules.OccupantSlotRefusalPrefix + Placement.Slot,",
				stamper);
			StringAssert.DoesNotContain("\"a living occupant moved onto layout slot \"", stamper);
		}

		[Test]
		public void UpgradeInfrastructureIsFrozenBeforeRequirementAndDebitAssessment()
		{
			string upgrade = KingdomUpgradeLogicalSource.Read();
			string assessment = Between(upgrade, "public static Assessment Assess(",
				"public static bool ContentsWouldFit(");
			AssertOrdered(assessment,
				"KingdomRules.DistrictsBuildPercent(",
				"KingdomUpgradeRules.BuildTicks(",
				"MeasureRequirements(System, Z, predecessor",
				"KingdomUpgradeRules.Assess(");
		}

		private static string Between(string source, string start, string end)
		{
			int first = source.IndexOf(start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, "missing source boundary: " + start);
			int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, "missing source boundary: " + end);
			return source.Substring(first, last - first);
		}

		private static void AssertOrdered(string source, params string[] terms)
		{
			int offset = 0;
			for (int i = 0; i < terms.Length; i++)
			{
				int found = source.IndexOf(terms[i], offset, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered source term: " + terms[i]);
				offset = found + terms[i].Length;
			}
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int offset = 0;
			while ((offset = source.IndexOf(term, offset, StringComparison.Ordinal)) >= 0)
			{
				count++;
				offset += term.Length;
			}
			return count;
		}
	}
}
#endif
