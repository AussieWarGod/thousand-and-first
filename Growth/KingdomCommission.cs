using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static class KingdomCommission
	{
		private const uint PlacementEventKind = 5U;
		private const uint PlacementDraw = 0U;

		/// <summary>Founder-facing refusal for the authored stage gate. Commit paths call this
		/// independently of menu visibility: a hidden row is not an authorization boundary.</summary>
		public static string StageRefusal(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null || System.Stage >= Entry.MinStage) return null;
			string seat = string.IsNullOrEmpty(System.SeatName) ? "The settlement" : System.SeatName;
			return seat + " has not yet grown into " + KingdomUpgradeRules.StageWord(Entry.MinStage)
				+ ", when the " + (Entry.Name ?? "work") + " can be raised.";
		}

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
			return Commission(System, Key, SkinKey, Stake, null, out Failure);
		}

		/// <summary>Commits an optional exact plotted quote after the UI has shown it.</summary>
		public static bool Commission(KingdomSystem System, string Key, string SkinKey,
			KingdomPlotRules.PlotSize Stake, KingdomPlotQuote Expected, out string Failure)
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
			Failure = StageRefusal(System, entry);
			if (Failure != null)
			{
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
				return KingdomPlots.Commission(System, zone, entry, SkinKey, Stake,
					Expected, out Failure);
			}
			if (Expected != null)
			{
				Failure = "That work is not an authored plot and cannot consume a plot preview.";
				return false;
			}
			// Frontier works do not count against the plan. A palisade is a LINE, and charging a slot per
			// segment is what made enclosing a settlement impossible - the ring would have eaten
			// the whole allowance before anything civic was built.
			int built = KingdomPlots.CountBuilt(zone);
			if (!KingdomRules.IsFrontierWork(entry.Defence,
				KingdomPlots.IsPlotDesign(entry.Key))
				&& built >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + KingdomPresentation.Rich(System.SeatName) + " is as built-up as this ground allows, until it grows into something larger.";
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
			Cell cell;
			string payload = SkinKey;
			KingdomLayoutRules.LayoutOutcome outcome;
			KingdomGatehousePlan gatePlan = null;
			long started = The.Game.TimeTicks;
			long due = started + CraftBuildTicks(entry.BuildTicks,
				System.ZoneDistricts.Values);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(entry.Key), KingdomMaterials.BitCostFor(entry.Key),
				KingdomMaterials.ExoticCostFor(entry.Key));
			KingdomConstructionJob job = null;
			if (KingdomGatehouseRules.IsGatehouse(entry.Key))
			{
				outcome = KingdomLayoutRules.LayoutOutcome.Grammar;
				// This is the destructive ground read: every one of the nine footprint cells,
				// both road approaches, existing reservations, creatures, and fixtures are proved
				// before either water or materials are reserved. Nothing is cleared or displaced.
				if (!KingdomGatehouse.TryPlan(zone, System, out gatePlan, out Failure)
					|| !KingdomGatehouseRules.TryEncode(gatePlan, out payload))
				{
					Failure = Failure ?? "The exact gatehouse footprint could not be frozen.";
					return false;
				}
				cell = zone.GetCell(gatePlan.GateX, gatePlan.GateY);
				for (int y = gatePlan.Y1; y <= gatePlan.Y2; y++)
				{
					for (int x = gatePlan.X1; x <= gatePlan.X2; x++)
					{
						if (KingdomConstruction.HasActiveAt(System, zone, zone.GetCell(x, y)))
						{
							Failure = "The gatehouse footprint already has a paid construction receipt at "
								+ x + "," + y + ".";
							return false;
						}
					}
				}
			}
			else
			{
				// Mint the durable construction owner before semantic siting. Its exact X/Y are
				// frozen into this same job before publication or any debit.
				job = KingdomConstruction.NewJob(System, zone,
					KingdomConstructionRoute.CommissionScaffold, null, null, entry.Key,
					payload, entry.CostDrams, claim, started, due);
				cell = FindBuildCell(zone, System, entry, job.Id, out outcome);
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
				job.X = cell.X;
				job.Y = cell.Y;
			}
			if (job == null)
				job = KingdomConstruction.NewJob(System, zone,
					KingdomConstructionRoute.CommissionScaffold, cell, null, entry.Key, payload,
					entry.CostDrams, claim, started, due);
			if (!KingdomConstruction.FreezeBuildTruth(job, System, entry.Defence, false))
			{
				Failure = "The commission's exact build effects could not be frozen.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(zone, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(zone, entry.Key);
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
			if (!ProjectScaffold(System, zone, entry, job.Payload, job, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("commission building");
				System.Ledger.Note("{{r|The paid commission could not put its scaffold on the ground. Its durable receipt remains queued for another pass.}} ");
				KingdomLog.Log("construction: commission projection waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("commission building");
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(entry.Name) + " was commissioned at " + KingdomPresentation.Rich(System.KingdomDisplayName));
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
			bool gatehouse = KingdomGatehouseRules.IsGatehouse(Entry?.Key);
			KingdomGatehousePlan gatePlan = null;
			if (gatehouse && !KingdomGatehouseRules.TryDecode(Job.Payload, out gatePlan))
			{
				Failure = "The paid gatehouse receipt has no exact frozen footprint.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (CountExpectedScaffolds(Z, Job, Entry) > 1)
			{
				Failure = "More than one commissioned scaffold carries the exact receipt.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			GameObject existing = FindExpectedScaffold(Z, Job, Entry);
			if (gatehouse && existing != null && !KingdomGatehouse.ScaffoldMatches(existing, gatePlan))
			{
				Failure = "The gatehouse scaffold no longer carries its exact footprint reservation.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (gatehouse && !KingdomGatehouse.TryAudit(Z, gatePlan, null, existing, out Failure))
			{
				return false;
			}
			if (IsExpectedScaffold(existing, cell, Entry, Job))
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
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				Failure = "The unprojected legacy commission predates frozen build effects.";
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
					bool removed = RemoveCreated(scaffold, Z);
					Failure = "The scaffold identity could not be published before AddObject.";
					if (!removed) KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (!KingdomConstruction.Owns(System, Z, Updated)
					|| !KingdomConstruction.IsCurrent(Updated))
				{
					RemoveCreated(scaffold, Z);
					Failure = "Commission authority changed during scaffold creation.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
			scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			if (!KingdomConstruction.ApplyBuildTruth(scaffold, Updated))
			{
				RemoveCreated(scaffold, Z);
				Failure = "The paid commission has no exact frozen build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			KingdomDesign.StageSkin(scaffold, Entry, gatehouse ? null : SkinKey);
			KingdomConstruction.Bind(scaffold, Updated);
			if (gatehouse && !KingdomGatehouse.TryStageScaffold(scaffold, gatePlan))
			{
				bool removed = RemoveCreated(scaffold, Z);
				Failure = "The gatehouse scaffold could not retain its full footprint reservation.";
				if (removed) KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				else KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreated(scaffold, Z);
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
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
				KingdomSurvey.ObserveAddResultInActive(Z, scaffold, accepted);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreated(scaffold, Z);
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
				|| !IsExpectedScaffold(scaffold, cell, Entry, Updated)
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				bool removed = RemoveCreated(scaffold, Z);
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
				if (IsExpectedScaffold(item, cell, Entry, Job)
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

		private static bool RemoveCreated(GameObject Object, Zone Z)
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
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Object);
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
					if (IsExpectedScaffold(item, cell, Entry, Job)
						&& KingdomConstruction.HasReceipt(item, Job))
					{
						if (item.ID != Job.OutputId && item.ID != Job.SubjectId) return 2;
						count++;
					}
			return count;
		}

		private static bool IsExpectedScaffold(GameObject Scaffold, Cell Cell,
			KingdomRules.BuildEntry Entry, KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Scaffold) || Scaffold.CurrentCell != Cell || Entry == null
				|| Scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key)
			{
				return false;
			}
			r_KingdomScaffold part = Scaffold.GetPart<r_KingdomScaffold>();
			return part != null && part.TargetBlueprint == Entry.Blueprint
				&& (KingdomConstruction.BuildTruthMatches(Scaffold, Job)
					|| KingdomConstruction.LegacyProjectedBuildTruthMatches(
						Scaffold, Job, false));
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
			return FindBuildCell(Z, System, Entry, null, out Outcome);
		}

		internal static Cell FindBuildCell(Zone Z, KingdomSystem System,
			KingdomRules.BuildEntry Entry, string PlacementOwnerId,
			out KingdomLayoutRules.LayoutOutcome Outcome)
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
			// A gatehouse with no valid road/frontier footprint is a refusal, never an excuse to
			// drop the network piece onto an ordinary defensive or founder-adjacent cell.
			if (Entry != null && KingdomGatehouseRules.IsGatehouse(Entry.Key))
			{
				Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
				return null;
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
			return FindBuildCell(Z, System, Entry != null
				&& KingdomRules.IsFrontierWork(Entry.Defence,
					KingdomPlots.IsPlotDesign(Entry.Key)),
				PlacementOwnerId);
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
			return FindBuildCell(Z, System, Defensive, null);
		}

		private static Cell FindBuildCell(Zone Z, KingdomSystem System, bool Defensive,
			string PlacementOwnerId)
		{
			if (Z == null)
			{
				return null;
			}
			int start = 0;
			if (PlacementOwnerId != null
				&& !TryPlacementProbe(System, Z, PlacementOwnerId, out start)) return null;
			if (Defensive && System != null)
			{
				KingdomRules.Frontier edges = KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones);
				if (edges != KingdomRules.Frontier.None)
				{
					Cell frontier = ProbeBuildCell(Z, start, edges, true, false);
					if (frontier != null) return frontier;
				}
			}
			Cell founder = ProbeBuildCell(Z, start, KingdomRules.Frontier.None,
				false, true);
			return founder ?? ProbeBuildCell(Z, start, KingdomRules.Frontier.None,
				false, false);
		}

		/// <summary>
		/// The gatehouse's own ground: the exact frontier way out
		/// (<c>KingdomRoadRules.TryGate</c>), which is the cell the settlement's own
		/// <c>HeartToGate</c> route is already walked to. Its full frozen 3x3 topology must pass
		/// the obstruction and passage audit; it never moves sideways to evade a blocker. The brief's ruling &mdash;
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
			if (!KingdomGatehouse.TryPlan(Z, System, out KingdomGatehousePlan plan,
				out string failure))
			{
				KingdomLog.Log("gatehouse: " + failure);
				return null;
			}
			KingdomLog.Log("gatehouse: exact road root=" + plan.GateX + "," + plan.GateY
				+ " footprint=" + plan.X1 + "," + plan.Y1 + ".." + plan.X2 + "," + plan.Y2
				+ " facing=" + plan.Orientation);
			return Z.GetCell(plan.GateX, plan.GateY);
		}

		public static Cell FindBuildCell(Zone Z)
		{
			if (Z == null) return null;
			Cell founder = ProbeBuildCell(Z, 0, KingdomRules.Frontier.None, false, true);
			return founder ?? ProbeBuildCell(Z, 0, KingdomRules.Frontier.None, false, false);
		}

		private static bool TryPlacementProbe(KingdomSystem System, Zone Z,
			string PlacementOwnerId, out int Start)
		{
			Start = -1;
			string streamId;
			SemanticEventKey key;
			KernelFaultCode kernelFault;
			KingdomSemanticSelectionFault semanticFault;
			if (System == null || string.IsNullOrEmpty(System.CurrentSettlementId)
				|| !KingdomSemanticSelectionRules.TryOwnerStreamId("commission-placement",
					PlacementOwnerId, out streamId)
				|| !SemanticEventKey.TryCreate(KingdomSemanticSelectionRules.RulesVersion,
					System.CurrentSettlementId, streamId, PlacementEventKind, 1UL,
					out key, out kernelFault)
				|| !KingdomSemanticSelectionRules.TryProbeStart(System.SimulationSeed, key,
					PlacementDraw, Z.Width, Z.Height, out Start, out semanticFault))
			{
				KingdomLog.Log("commission placement: deterministic probe identity refused");
				return false;
			}
			return true;
		}

		private static Cell ProbeBuildCell(Zone Z, int Start, KingdomRules.Frontier Edges,
			bool FrontierOnly, bool FounderAdjacentOnly)
		{
			if (Z == null || Z.Width <= 0 || Z.Height <= 0) return null;
			Cell player = The.Player?.CurrentCell;
			if (FounderAdjacentOnly && (player == null || player.ParentZone != Z)) return null;
			int count = Z.Width * Z.Height;
			for (int offset = 0; offset < count; offset++)
			{
				int at = KingdomSemanticSelectionRules.ProbeIndex(Start, offset, count);
				Cell cell = Z.GetCell(at % Z.Width, at / Z.Width);
				if (cell == null || !cell.IsEmpty() || !cell.IsPassable()
					|| cell.HasObjectWithPart("LiquidVolume")) continue;
				if (FrontierOnly && !KingdomRules.IsOnFrontier(cell.X, cell.Y,
					Z.Width, Z.Height, Edges)) continue;
				if (FounderAdjacentOnly && KingdomLayoutRules.Chebyshev(cell.X, cell.Y,
					player.X, player.Y) != 1) continue;
				return cell;
			}
			return null;
		}
	}
}
