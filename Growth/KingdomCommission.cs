using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomCommission
	{
		public static bool Commission(KingdomSystem System, string Key, out string Failure)
		{
			return Commission(System, Key, null, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on.
		/// </summary>
		/// <param name="System">The realm; must be founded and must hold this ground.</param>
		/// <param name="Key">Registry key of the design.</param>
		/// <param name="SkinKey">Key of the design's own skin the founder chose, or null for the
		/// design's unmodified look. A key the design does not carry is ignored.</param>
		/// <param name="Failure">A founder-facing sentence when this returns false; null otherwise.
		/// Every refusal names what would lift it.</param>
		/// <returns>True once scaffolding is standing and the water is spent.</returns>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, out string Failure)
		{
			return Commission(System, Key, SkinKey, KingdomPlotRules.PlotSize.None, out Failure);
		}

		/// <summary>
		/// Issues one commission on the ground the founder is standing on, staking the tier of plot
		/// they chose. Identical in every check to the overload above: the tier only ever widens the
		/// envelope, never the building, and a design that is not a plot ignores it entirely.
		/// </summary>
		/// <param name="Stake">The tier to lay, from <c>KingdomPlots.StakeableSizes</c>.
		/// <c>PlotSize.None</c> stakes the design's own.</param>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)
		{
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (!System.Founded || zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Failure = "Commissions are issued on the kingdom's own ground.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out var entry) || !KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "No such design.";
				return false;
			}
			// Before the room and the stores are counted, so a founder is never told what a design
			// would cost them on ground that was never going to take it.
			if (!KingdomZoning.Permits(System, zone.ZoneID, entry, out string refusal))
			{
				Failure = refusal;
				return false;
			}
			// And before the paths fork, because the way down is a fact about the GROUND and a wall
			// is as unbuildable in unopened rock as a gallery is. The plot path asks again on its
			// own account, since KingdomPlots.Commission is reachable without coming through here.
			Failure = KingdomDelve.Refusal(System, zone.ZoneID, entry.Key, entry.Name);
			if (Failure != null)
			{
				return false;
			}
			// A plot-sized design is raised over a rect in stages, not as one object in one cell.
			// Every design that declares no Plot size - which is all of them until one does - falls
			// through to the single-cell path below, untouched.
			if (KingdomPlots.IsPlotDesign(entry.Key))
			{
				return KingdomPlots.Commission(System, zone, entry, SkinKey, Stake, out Failure);
			}
			// Walls do not count against the plan. A palisade is a LINE, and charging a slot per
			// segment is what made enclosing a settlement impossible - the ring would have eaten
			// the whole allowance before anything civic was built.
			int built = 0;
			foreach (GameObject item in zone.GetObjects())
			{
				// A plot's walls, floor, and furnishings carry KingdomPlotPart and never count: the
				// cap counts plots, not the hundred objects one plot is made of.
				if (item.GetIntProperty("KingdomDefence") > 0 || item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1)
				{
					continue;
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold") || item.HasPart("r_KingdomPlotWorks"))
				{
					built++;
				}
			}
			if (entry.Defence <= 0 && built >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + System.SeatName + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(zone) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			// After the water and before the ground is chosen: a founder is told the whole price
			// before anything is committed, and a design with no material cost is always affordable,
			// which is every design the catalogue carried before materials existed.
			if (!KingdomMaterials.CanPay(zone, entry.Key, out var materialRefusal))
			{
				Failure = materialRefusal;
				return false;
			}
			Cell cell = FindBuildCell(zone, System, entry, out var outcome);
			if (cell == null)
			{
				Failure = "There is no clear ground for it here.";
				return false;
			}
			if (KingdomConstruction.HasActiveAt(System, zone, cell))
			{
				Failure = "That ground already has a paid construction receipt in hand.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(zone, entry.Key);
			long due = The.Game.TimeTicks + CraftBuildTicks(entry.BuildTicks, System.ZoneDistricts.Values);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(entry.Key), KingdomMaterials.BitCostFor(entry.Key),
				KingdomMaterials.ExoticCostFor(entry.Key));
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, zone,
				KingdomConstructionRoute.CommissionScaffold, cell, null, entry.Key, SkinKey,
				entry.CostDrams, claim, The.Game.TimeTicks, due);
			KingdomConstructionStartResult funded = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funded == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			if (funded == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The commission has a measured receipt still outstanding. It remains queued and will not charge its paid claims twice.}} ");
				return true;
			}
			if (!ProjectScaffold(System, zone, entry, SkinKey, job, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The paid commission could not put its scaffold on the ground. Its durable receipt remains queued for another pass.}} ");
				KingdomLog.Log("construction: commission projection waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("commission building");
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(entry.Name) + " was commissioned at " + System.KingdomDisplayName);
			string clause = KingdomLayoutRules.PlacementClause(KingdomLayout.PurposeOfEntry(entry), outcome);
			MessageQueue.AddPlayerMessage("{{G|The " + entry.Name + " is commissioned. Scaffolding rises"
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			return true;
		}

		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null || Job.Route != KingdomConstructionRoute.CommissionScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry))
			{
				return;
			}
			GameObject scaffold = FindExpectedScaffold(Z, Job, entry);
			if (scaffold != null && scaffold.ID == Job.SubjectId)
			{
				r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
				if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
				{
					part.RetryDurable(System, Z, Job);
					return;
				}
			}
			ProjectScaffold(System, Z, entry, Job.Payload, Job, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.CommissionScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)) return;
			if (CountExpectedScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"More than one commissioned scaffold carries the exact receipt.");
				return;
			}
			GameObject existing = FindExpectedScaffold(Z, Job, entry);
			KingdomConstructionJob inspected = Job;
			GameObject successor;
			int successors = r_KingdomScaffold.FindExactSuccessors(Z, Job,
				entry.Blueprint, existing, out successor);
			if (successors > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one exact commissioned successor carries this receipt.");
				return;
			}
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				if (existing != null)
				{
					if (!KingdomConstructionRules.FullyFundedExact(Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"A premature terminal commission does not carry exact paid claims.");
						return;
					}
					if (inspected.SubjectId != existing.ID
						&& !KingdomConstruction.UpdateSubject(ref inspected, existing.ID)) return;
					if (successors == 1)
					{
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal receipt still has an exact scaffold to remove.");
					}
					else
					{
						KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
				}
				else if (successors == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The terminal commissioned successor lacks scaffold-removal proof.");
						return;
					}
					r_KingdomScaffold.TellCompletion(System, successor, Job);
				}
				return;
			}
			if (existing != null)
			{
				if (inspected.SubjectId != existing.ID)
				{
					if (!KingdomConstruction.UpdateSubject(ref inspected, existing.ID)) return;
					KingdomConstruction.FinishProjection(ref inspected, true, true);
					return;
				}
				r_KingdomScaffold part = existing.GetPart<r_KingdomScaffold>();
				int finalPending = existing.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The commissioned scaffold final flag is not an exact boolean.");
					return;
				}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				else if (Job.Phase == KingdomConstructionPhase.Working
					|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
					part.AdvanceDurable(System, Z, Job, The.Game.TimeTicks);
				else if (Job.Phase == KingdomConstructionPhase.Outstanding)
				{
					if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
						part.RetryDurable(System, Z, Job);
					else
						KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if (successors == 1)
			{
				if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The commissioned successor lacks exact scaffold-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, successor, inspected);
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The commissioned receipt has no exact predecessor or successor at its recorded cell.");
		}

		private static bool ProjectScaffold(KingdomSystem System, Zone Z,
			KingdomRules.BuildEntry Entry, string SkinKey, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (CountExpectedScaffolds(Z, Job, Entry) > 1)
			{
				Failure = "More than one commissioned scaffold carries the exact receipt.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject existing = FindExpectedScaffold(Z, Job, Entry);
			if (IsExpectedScaffold(existing, cell, Entry))
			{
				if (Updated.SubjectId != existing.ID
					&& !KingdomConstruction.UpdateSubject(ref Updated, existing.ID))
				{
					Failure = "The scaffold identity could not be published.";
					return false;
				}
				if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
				{
					Failure = "The scaffold stands, but its Working state did not persist.";
					return false;
				}
				return true;
			}
			GameObject unexpected;
			KingdomPhysicalLookupState receiptState = KingdomConstruction.FindReceipt(
				Z, Job, out unexpected);
			if (receiptState != KingdomPhysicalLookupState.Absent)
			{
				Failure = receiptState == KingdomPhysicalLookupState.Ambiguous
					? "More than one physical object carries the construction receipt."
					: "The construction receipt is attached to an unexpected projection.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (cell == null || !KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			GameObject scaffold;
			try
			{
				scaffold = GameObject.Create("r_KingdomScaffold");
			}
			catch (System.Exception ex)
			{
				Failure = "The scaffold blueprint threw during creation: " + ex.Message;
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
				if (scaffold == null)
			{
				Failure = "The scaffold blueprint could not be created.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
				}
				if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
				{
					bool removed = RemoveCreated(scaffold);
					Failure = "The scaffold identity could not be published before AddObject.";
					if (!removed) KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (!KingdomConstruction.Owns(System, Z, Updated)
					|| !KingdomConstruction.IsCurrent(Updated))
				{
					RemoveCreated(scaffold);
					Failure = "Commission authority changed during scaffold creation.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			KingdomDesign.StageSkin(scaffold, Entry, SkinKey);
			KingdomConstruction.Bind(scaffold, Updated);
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreated(scaffold);
				Failure = "The created scaffold carries no raising capability.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			part.TargetBlueprint = Entry.Blueprint;
			part.TargetDisplayName = Entry.Name;
			part.CompleteTick = Updated.DueTick;
			part.StaffNeeded = Entry.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
			if (Entry.Defence > 0)
			{
				bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
				bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
				scaffold.SetIntProperty("KingdomDefencePending", KingdomRules.WallDefence(
					Entry.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName,
					hasTinkering, hasAdvancedTinkering));
			}
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreated(scaffold);
				Failure = "The scaffold threw while entering its commissioned cell: " + ex.Message;
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject exactScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, Z, Updated)
				|| KingdomConstruction.FindExactId(Z, Updated.OutputId, out exactScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactScaffold, scaffold)
				|| !IsExpectedScaffold(scaffold, cell, Entry)
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				bool removed = RemoveCreated(scaffold);
				Failure = "The scaffold could not be verified in its commissioned cell.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.UpdateSubject(ref Updated, scaffold.ID))
			{
				Failure = "The commissioned scaffold identity could not be published.";
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true))
			{
				Failure = "The commissioned scaffold stands, but its Working state did not persist.";
				return false;
			}
			return true;
		}

		private static GameObject FindExpectedScaffold(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return null;
			GameObject found = null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExpectedScaffold(item, cell, Entry)
					&& KingdomConstruction.HasReceipt(item, Job))
				{
					count++;
					if (item.ID == Job.OutputId || item.ID == Job.SubjectId) exact = item;
					else if (found == null) found = item;
				}
			}
			GameObject global;
			return count == 1 && exact != null
				&& KingdomConstruction.FindExactId(Z, exact.ID, out global)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(global, exact) ? exact : null;
		}

		private static bool RemoveCreated(GameObject Object)
		{
			try
			{
				return !GameObject.Validate(Object)
					|| (Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object));
			}
			catch
			{
				return false;
			}
		}

		private static int CountExpectedScaffolds(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return 0;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
					if (IsExpectedScaffold(item, cell, Entry)
						&& KingdomConstruction.HasReceipt(item, Job))
					{
						if (item.ID != Job.OutputId && item.ID != Job.SubjectId) return 2;
						count++;
					}
			return count;
		}

		private static bool IsExpectedScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Entry)
		{
			if (!GameObject.Validate(Scaffold) || Scaffold.CurrentCell != Cell || Entry == null
				|| Scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key)
			{
				return false;
			}
			r_KingdomScaffold part = Scaffold.GetPart<r_KingdomScaffold>();
			return part != null && part.TargetBlueprint == Entry.Blueprint;
		}

		/// <summary>
		/// Build ticks after a craft district's discount. Floors at one tick and falls back to
		/// the undiscounted time on a hostile or missing percent rather than ever completing a
		/// build instantly or in the past.
		/// </summary>
		public static long CraftBuildTicks(long BaseTicks, IEnumerable<string> Districts)
		{
			int percent = KingdomRules.DistrictsBuildPercent(Districts);
			if (percent <= 0)
			{
				percent = 100;
			}
			long ticks = BaseTicks * percent / 100;
			return (ticks < 1) ? 1 : ticks;
		}

		/// <summary>
		/// Where a commissioned work is raised, by what it is for, read against everything the
		/// settlement already has standing here.
		/// <para>
		/// The settlement's own plan (<c>KingdomLayout</c>) is asked first: casks gather by the
		/// water, houses gather by the houses and stand back from the wall, the civic ground
		/// thickens where the settlement already lives, fields lie out past the last roof, and a
		/// wall extends the wall. It grows with the city because every one of those answers is a
		/// function of what is already built, so the same commission lands somewhere different
		/// in a camp and in a town.
		/// </para>
		/// <para>
		/// The plan is allowed to have nothing to say &mdash; on empty ground it always does,
		/// because there are no neighbours to reason from &mdash; and it never fights the
		/// founder over ground it does not care about. Either way the fallback below is the
		/// placement this mod always had.
		/// </para>
		/// <para>
		/// Failure here degrades to that fallback rather than escaping into the engine: a
		/// commission the plan cannot site is still a commission.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim.</param>
		/// <param name="Entry">The design being raised.</param>
		/// <param name="Outcome">What the plan did, for the message the founder reads. Reports
		/// <c>Defer</c> whenever the fallback placed the work, whatever the reason.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, out KingdomLayoutRules.LayoutOutcome Outcome)
		{
			KingdomLayoutRules.LayoutOutcome planned = KingdomLayoutRules.LayoutOutcome.Defer;
			Cell cell = null;
			// Asked before the plan, because the gatehouse is the one design whose ground is a
			// rule rather than a preference: it belongs where the wall meets the road, and the
			// plan has no way to know that. Everything else, the plan answers for.
			KingdomSystem.Guard("gatehouse siting", delegate
			{
				cell = FindGateCell(Z, System, Entry);
			});
			if (cell != null)
			{
				// Grammar, not Founder: the settlement's own shape chose this ground, and the
				// founder's standing on it had nothing to do with it. The clause the founder
				// reads for a defensive work chosen by the plan is "on the line", which is
				// exactly where a gatehouse went.
				Outcome = KingdomLayoutRules.LayoutOutcome.Grammar;
				return cell;
			}
			KingdomSystem.Guard("settlement layout", delegate
			{
				cell = KingdomLayout.ChooseCell(Z, System, Entry, out planned);
			});
			if (cell != null)
			{
				Outcome = planned;
				return cell;
			}
			Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
			return FindBuildCell(Z, System, Entry != null && Entry.Defence > 0);
		}

		/// <summary>
		/// Where a commissioned work is raised when the settlement's plan has no opinion.
		/// <para>
		/// Defensive works go on the frontier: the edges of this zone that face ground the realm
		/// does not hold. That is what makes a wall a wall rather than a post in a field, and it
		/// is why walls must be sited against the WHOLE claim rather than one zone - a camp
		/// becomes a city across several zones, and the edge that needed a wall yesterday is
		/// interior once the neighbour is claimed. Nothing is moved or torn down when that
		/// happens; the old line simply becomes an inner wall.
		/// </para>
		/// <para>
		/// Everything else is raised where the founder is standing, which is the closest thing to
		/// intent the mod can read without a placement UI.
		/// </para>
		/// </summary>
		/// <param name="Z">Zone to build in.</param>
		/// <param name="System">The realm, for its claim. Null falls back to founder-adjacent.</param>
		/// <param name="Defensive">True to site this on the frontier.</param>
		public static Cell FindBuildCell(Zone Z, KingdomSystem System, bool Defensive)
		{
			if (Z == null)
			{
				return null;
			}
			if (Defensive && System != null)
			{
				KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
				if (edges != KingdomRules.Frontier.None)
				{
					List<Cell> line = new List<Cell>();
					foreach (Cell candidate in Z.GetEmptyCells())
					{
						if (candidate.IsPassable() && !candidate.HasObjectWithPart("LiquidVolume")
							&& KingdomRules.IsOnFrontier(candidate.X, candidate.Y, Z.Width, Z.Height, edges))
						{
							line.Add(candidate);
						}
					}
					if (line.Count > 0)
					{
						return line.GetRandomElement();
					}
				}
			}
			return FindBuildCell(Z);
		}

		/// <summary>
		/// The gatehouse's own ground: the buildable frontier cell nearest the way out
		/// (<c>KingdomRoadRules.TryGate</c>), which is the cell the settlement's own
		/// <c>HeartToGate</c> route is already walked to. The brief's whole ruling on it &mdash;
		/// "a placement rule, not a size: on the frontier wall, astride a road".
		/// <para>
		/// Null for every other design, for a zone with no frontier left to have a way out of,
		/// and for a settlement with no heart yet to aim from. Every one of those falls through
		/// to the ordinary plan, which is what sited a gatehouse before this existed.
		/// </para>
		/// </summary>
		public static Cell FindGateCell(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (Z == null || System == null || Entry == null || !KingdomRoadRules.SitesAtGate(Entry.Key))
			{
				return null;
			}
			KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
			if (edges == KingdomRules.Frontier.None)
			{
				return null;
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out var riteX, out var riteY);
			if (!KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY, out var heartX, out var heartY))
			{
				return null;
			}
			if (!KingdomRoadRules.TryGate(Z.Width, Z.Height, edges, heartX, heartY, out var gateX, out var gateY))
			{
				return null;
			}
			List<Cell> candidates = new List<Cell>();
			List<int> xs = new List<int>();
			List<int> ys = new List<int>();
			foreach (Cell candidate in Z.GetEmptyCells())
			{
				if (!candidate.IsPassable() || candidate.HasObjectWithPart("LiquidVolume")
					|| !KingdomRules.IsOnFrontier(candidate.X, candidate.Y, Z.Width, Z.Height, edges))
				{
					continue;
				}
				candidates.Add(candidate);
				xs.Add(candidate.X);
				ys.Add(candidate.Y);
			}
			int index = KingdomRoadRules.NearestToGate(xs, ys, gateX, gateY);
			if (index < 0 || index >= candidates.Count)
			{
				return null;
			}
			KingdomLog.Log("gatehouse: gate=" + gateX + "," + gateY + " sited=" + candidates[index].X + "," + candidates[index].Y
				+ " from " + candidates.Count + " frontier cells");
			return candidates[index];
		}

		public static Cell FindBuildCell(Zone Z)
		{
			Cell playerCell = The.Player?.CurrentCell;
			if (playerCell != null)
			{
				List<Cell> adjacent = playerCell.GetLocalAdjacentCells();
				for (int i = 0; i < adjacent.Count; i++)
				{
					if (adjacent[i].IsEmpty() && adjacent[i].IsPassable() && !adjacent[i].HasObjectWithPart("LiquidVolume"))
					{
						return adjacent[i];
					}
				}
			}
			List<Cell> emptyCells = Z.GetEmptyCells();
			if (emptyCells != null && emptyCells.Count > 0)
			{
				return emptyCells.GetRandomElement();
			}
			return null;
		}
	}
}
