using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A founder's stake in the ground: names a design from <c>KingdomData.Buildings</c> and
	/// waits, doing nothing on its own, for <see cref="ThousandAndFirst.KingdomPlanMarker.OnSettlementPass"/>
	/// to decide it can be afforded. Carries no <c>WantTurnTick</c> and never will &mdash; a plan
	/// is realised only from the settlement's ordinary <c>ZoneActivatedEvent</c> pass, the same
	/// clock every other absence-resolving system in this mod reads from. Nothing here is spent
	/// or moved until <see cref="ThousandAndFirst.KingdomPlanMarker.OnSettlementPass"/> proves its
	/// frozen receipt. Legacy single-cell plans replace this object with a scaffold; current plotted
	/// plans reserve an exact authored lot beside the survey stake and raise their works at the
	/// frozen main anchor.
	/// </summary>
	[Serializable]
	public class r_KingdomPlanMarker : IPart
	{
		/// <summary>Key into <c>KingdomData.Buildings</c> naming the design staked here.</summary>
		public string DesignKey;

		/// <summary>Tick this plan was staked at. First place in the queue, all else equal.</summary>
		public long PlacedTick;

		/// <summary>
		/// Tie-breaker for <see cref="PlacedTick"/>, assigned once from the game's own generic
		/// counter store (<c>XRLGame.ModIntGameState</c>) at the moment the plan is staked. Two
		/// plans staked in the same charter session spend no game time between them, so the tick
		/// alone cannot always tell them apart; this can.
		/// </summary>
		public long PlacedOrder;

		/// <summary>
		/// Key under which the monotonic plan-ordering counter lives in
		/// <c>XRLGame.IntGameState</c>. A generic, already-serialized game-state slot rather than
		/// a new field on <c>KingdomSystem</c>, so staking a plan never touches that system's own
		/// positionally-reflected field layout.
		/// </summary>
		public const string PlanOrderCounterKey = "r_TAF_NextPlanOrder";

		/// <summary>
		/// Names this marker after Entry and records what it is waiting to become. Called once,
		/// at the moment the founder stakes the plan; nothing here is engine-observable beyond
		/// the marker's own fields and display name, and nothing is spent.
		/// </summary>
		public void ApplyDesign(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return;
			}
			DesignKey = Entry.Key;
			PlacedTick = The.Game.TimeTicks;
			PlacedOrder = The.Game.ModIntGameState(PlanOrderCounterKey, 1);
			if (ParentObject != null)
			{
				ParentObject.DisplayName = "plan: " + Entry.Name;
			}
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// Resolves every staked plan in a zone on the settlement's own clock, and carries out the
	/// founder-facing actions (staking, cancelling, listing) that <c>KingdomCharterPart</c> calls
	/// into. The eligibility and ordering arithmetic itself lives in the engine-free
	/// <see cref="KingdomPlanRules"/>; everything here is the thin, engine-coupled shell around
	/// it &mdash; reading real markers into <see cref="KingdomPendingPlan"/> values, spending real
	/// water through the same measured-delta <c>KingdomSurvey.Consume</c> path every other
	/// automatic drawer in this mod uses, and handing a realised plan off to
	/// <c>r_KingdomScaffold</c> exactly the way <c>KingdomCommission.Commission</c> does for a
	/// founder-issued commission. A plan is not a second way to build; it is a way to queue a
	/// commission for later.
	/// </summary>
	public static class KingdomPlanMarker
	{
		/// <summary>
		/// Resolves every plan staked in Z. Called from <see cref="KingdomGrowth.OnZoneActivated"/>
		/// after <c>KingdomPlot</c> and before <c>KingdomPower</c>, so a plan spends only what the
		/// day's upkeep, arrivals, and crop have left in the stores &mdash; it can never be the
		/// reason the thirst ladder fires, the same guarantee the plot already holds.
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			// Resume existing durable receipts before considering any unbound marker. The root
			// semantic dispatcher also calls this independently of settler arrivals; this local call
			// keeps direct/test callers under the same no-second-job law.
			KingdomConstruction.OnSettlementPass(System, Z, Survey);
			List<GameObject> markers = new List<GameObject>();
			List<KingdomRules.BuildEntry> entries = new List<KingdomRules.BuildEntry>();
			List<KingdomPendingPlan> pending = new List<KingdomPendingPlan>();
			List<int> waterPrices = new List<int>();
			List<KingdomMaterialDebitCost> materialClaims = new List<KingdomMaterialDebitCost>();
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				r_KingdomPlanMarker marker = item.GetPart<r_KingdomPlanMarker>();
				if (marker == null || string.IsNullOrEmpty(marker.DesignKey))
				{
					continue;
				}
				string receipt = item.GetStringProperty(KingdomConstruction.ReceiptProperty);
				if (!string.IsNullOrEmpty(receipt))
				{
					KingdomConstructionJob existing;
					if (!KingdomConstruction.TryFind(receipt, out existing))
					{
						// Missing/unreadable registry is ambiguous. Never publish a second job against
						// a marker that may already have paid.
						continue;
					}
					if (!KingdomConstructionRules.IsTerminal(existing.Phase)
						|| existing.Phase == KingdomConstructionPhase.Complete)
					{
						continue;
					}
					// Clean compensation/cancellation paid nothing and may try again normally.
					item.RemoveStringProperty(KingdomConstruction.ReceiptProperty);
				}
				// A design an outside mod withdrew (or one that never shipped) leaves its marker
				// waiting forever rather than throwing or silently vanishing -- the same
				// "waiting is not failing" contract as a plan that simply cannot afford its cost
				// yet.
				if (!KingdomData.TryGetBuilding(marker.DesignKey, out var entry))
				{
					continue;
				}
				if (KingdomConstruction.HasActiveSubject(System, Z,
						KingdomConstructionRoute.PlanScaffold, item)
					|| KingdomConstruction.HasActiveSubject(System, Z,
						KingdomConstructionRoute.PlotPlan, item))
				{
					continue;
				}
				if (!KingdomPlots.TryPlanPrice(item, entry,
					out int waterPrice, out KingdomMaterialDebitCost materialClaim))
				{
					// A current frozen receipt which cannot name its exact price is a real blocker,
					// not an affordability miss. Announce it through the same once-only path before
					// leaving the plan untouched; malformed plans must never fail silently.
					KingdomPlots.PlanBlocked(System, item, entry);
					continue;
				}
				markers.Add(item);
				entries.Add(entry);
				waterPrices.Add(waterPrice);
				materialClaims.Add(materialClaim);
				pending.Add(new KingdomPendingPlan(marker.PlacedTick, marker.PlacedOrder,
					waterPrice, KingdomRules.IsFrontierWork(entry.Defence,
						KingdomPlots.IsPlotDesign(entry.Key))));
			}
			if (pending.Count == 0)
			{
				return;
			}
			int built = CountBuilt(Survey);
			int cap = KingdomRules.MaxBuildingsForStage(System.Stage);
			foreach (int index in KingdomPlanRules.PlansToRealize(pending, Survey.StoredWater, built, cap))
			{
				// Checked before the water is drawn: a plot whose ground is blocked must never spend
				// anything, and it says why once (STANDARDS 7b). Not a plot design: says nothing and
				// changes nothing.
				if (KingdomPlots.PlanBlocked(System, markers[index], entries[index]))
				{
					continue;
				}
				GameObject markerObject = markers[index];
				KingdomRules.BuildEntry entry = entries[index];
				Cell cell = markerObject.CurrentCell;
				if (cell == null)
				{
					continue;
				}
				if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out _))
				{
					continue;
				}
				string materialRefusal;
				if (!KingdomMaterials.AllowsInfrastructure(Z, entry.Key, out materialRefusal))
				{
					continue;
				}
				bool frozenPlot = markerObject.HasIntProperty(KingdomPlots.PlanSchemaProperty);
				KingdomConstructionRoute route = frozenPlot || KingdomPlots.IsPlotDesign(entry.Key)
					? KingdomConstructionRoute.PlotPlan : KingdomConstructionRoute.PlanScaffold;
				string payload = markerObject.GetStringProperty(KingdomDesign.PlannedSkinProperty);
				long duration = KingdomCommission.CraftBuildTicks(entry.BuildTicks,
					System.ZoneDistricts.Values);
				if (route == KingdomConstructionRoute.PlotPlan)
				{
					KingdomPlotRules.PlotRect plannedRect;
					if (!KingdomPlots.TryPreparePlan(System, markerObject, entry,
						out plannedRect, out payload, out duration,
						out int mainX, out int mainY))
					{
						continue;
					}
					cell = Z.GetCell(mainX, mainY);
					if (cell == null) continue;
				}
				int waterPrice = waterPrices[index];
				KingdomMaterialDebitCost claim = materialClaims[index];
				long due = The.Game.TimeTicks + duration;
				KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z, route, cell,
					markerObject, entry.Key, payload,
					waterPrice, claim, The.Game.TimeTicks, due);
				bool hasPlot = route == KingdomConstructionRoute.PlotPlan;
				if (!KingdomConstruction.FreezeBuildTruth(job, System, entry.Defence, hasPlot))
				{
					KingdomLog.Log("construction: plan build effects could not be frozen");
					continue;
				}
				KingdomWaterDebit water = Survey.ReserveExactWater(waterPrice);
				KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, claim);
				KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
					water, materials, out job, out string fundingFailure);
				if (funding == KingdomConstructionStartResult.Refused)
				{
					continue;
				}
				if (funding == KingdomConstructionStartResult.Outstanding)
				{
					KingdomConstruction.Bind(markerObject, job);
					KingdomLog.Log("construction: plan receipt waits: " + (fundingFailure ?? "outstanding claim"));
					continue;
				}
				Realize(System, markerObject, entry, job, out _);
			}
		}

		/// <summary>
		/// Counts buildings this zone already has standing or scaffolded, by the exact rule
		/// <c>KingdomCommission.Commission</c> uses for its own cap check: walls are exempt, a
		/// scaffold in progress already counts. Kept in step with that rule by hand, since a plan
		/// and a founder-issued commission must compete for one shared allowance rather than each
		/// getting their own.
		/// </summary>
		private static int CountBuilt(KingdomSurvey Survey)
		{
			return Survey == null ? 0 : KingdomPlots.CountBuilt(Survey.Objects);
		}

		/// <summary>
		/// Turns a realised plan into a scaffold, in place: the same <c>r_KingdomScaffold</c>
		/// pipeline <c>KingdomCommission.Commission</c> hands a founder-issued commission to, just
		/// sited at the marker's own cell instead of a freshly chosen one, because the founder
		/// already chose it when they staked the plan.
		/// </summary>
		private static bool Realize(KingdomSystem System, GameObject MarkerObject,
			KingdomRules.BuildEntry Entry, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated)
		{
			Updated = Job;
			Cell cell = GameObject.Validate(MarkerObject) ? MarkerObject.CurrentCell : null;
			Zone zone = cell?.ParentZone;
			if (Job != null && Job.Route == KingdomConstructionRoute.PlotPlan)
			{
				r_KingdomPlanMarker plotMarker = GameObject.Validate(MarkerObject)
					? MarkerObject.GetPart<r_KingdomPlanMarker>() : null;
				if (!KingdomConstructionRules.TryReadBuildTruth(Job,
						out bool hasPlot, out bool frontier, out _)
					|| !hasPlot || frontier || Entry == null || cell == null
					|| !KingdomConstruction.Owns(System, zone, Job)
					|| MarkerObject.ID != (Job.SourceId ?? Job.SubjectId) || plotMarker == null
					|| plotMarker.DesignKey != Entry.Key || !KingdomConstruction.IsCurrent(Job))
				{
					KingdomConstruction.Quarantine(ref Updated,
						"The paid plot plan no longer matches its exact marker and design.");
					return false;
				}
				if (!KingdomConstruction.BeginProjection(ref Updated, out _)) return false;
				KingdomConstruction.Bind(MarkerObject, Updated);
				if (!KingdomConstruction.HasReceipt(MarkerObject, Updated)
					|| MarkerObject.CurrentCell != cell || plotMarker.DesignKey != Entry.Key
					|| !KingdomConstruction.IsCurrent(Updated))
				{
					KingdomConstruction.Quarantine(ref Updated,
						"The plot marker changed across its durable projection boundary.");
					return false;
				}
				KingdomConstructionJob plotUpdated;
				bool plotStaked = KingdomPlots.StakeFromPlan(System, MarkerObject, Entry,
					Updated, out plotUpdated);
				Updated = plotUpdated;
				return plotStaked;
			}
			Cell expected = zone == null || Job == null ? null : zone.GetCell(Job.X, Job.Y);
			if (Entry == null || Job == null || Job.Route != KingdomConstructionRoute.PlanScaffold
				|| cell == null || cell != expected || !KingdomConstruction.Owns(System, zone, Job)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Job, Entry, false)
				|| !KingdomConstruction.IsCurrent(Job))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The paid plan marker no longer matches its exact recorded ground and design.");
				return false;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out _, out _, out _))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The unprojected legacy plan predates frozen build effects.");
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out _))
			{
				return false;
			}
			KingdomConstruction.Bind(MarkerObject, Updated);
			if (!IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The plan marker changed across its durable projection boundary.");
				return false;
			}
			GameObject scaffold;
			try
			{
				scaffold = GameObject.Create("r_KingdomScaffold");
			}
			catch (System.Exception ex)
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The plan's scaffold threw during creation: " + ex.Message);
				return false;
			}
			if (scaffold == null)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The plan's scaffold blueprint could not be created.");
				return false;
			}
			if (!KingdomConstruction.Owns(System, zone, Updated)
				|| !KingdomConstruction.IsCurrent(Updated)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true))
			{
				RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated,
					"Plan authority or predecessor changed during scaffold creation.");
				return false;
			}
			// Read off the marker before it is taken down: the look the founder chose when they
			// staked the plan rides on the marker exactly as it rides on a scaffold.
			scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			if (!KingdomConstruction.ApplyBuildTruth(scaffold, Updated))
			{
				RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated,
					"The paid plan has no exact frozen build effects.");
				return false;
			}
			KingdomDesign.StageSkin(scaffold, Entry, MarkerObject.GetStringProperty(KingdomDesign.PlannedSkinProperty));
			KingdomCeremony.TransferPlanQuote(MarkerObject, scaffold);
			KingdomConstruction.Bind(scaffold, Updated);
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part == null)
			{
				bool removed = RemoveCreated(scaffold, zone);
				if (removed)
					KingdomConstruction.FinishProjection(ref Updated, false, false,
						"The plan's scaffold carries no raising capability.");
				else
					KingdomConstruction.Quarantine(ref Updated,
						"The invalid plan scaffold could not be removed exactly.");
				return false;
			}
			else
			{
				part.TargetBlueprint = Entry.Blueprint;
				part.TargetDisplayName = Entry.Name;
				part.CompleteTick = Updated.DueTick;
				part.StaffNeeded = Entry.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
				if (!KingdomConstruction.UpdateOutput(ref Updated, scaffold.ID))
				{
					bool removed = RemoveCreated(scaffold, zone);
					KingdomConstruction.Quarantine(ref Updated, removed
						? "The plan scaffold identity conflicted before AddObject."
						: "The plan scaffold identity conflicted and exact cleanup failed.");
					return false;
				}
			}
			GameObject accepted;
			try
			{
				accepted = cell.AddObject(scaffold);
				KingdomSurvey.ObserveAddResultInActive(zone, scaffold, accepted);
			}
			catch (System.Exception ex)
			{
				bool removed = RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated, (removed
					? "The plan scaffold threw after its identity was published: "
					: "The plan scaffold threw and could not be removed exactly: ") + ex.Message);
				return false;
			}
			GameObject exactScaffold;
			if (!ReferenceEquals(accepted, scaffold)
				|| !KingdomConstruction.Owns(System, zone, Updated)
				|| KingdomConstruction.FindExactId(zone, Updated.OutputId, out exactScaffold)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactScaffold, scaffold)
				|| scaffold.CurrentCell != cell || scaffold.CurrentZone != zone
				|| scaffold.GetPart<r_KingdomScaffold>() == null
				|| scaffold.GetPart<r_KingdomScaffold>().TargetBlueprint != Entry.Blueprint
				|| scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| !KingdomConstruction.HasReceipt(scaffold, Updated)
				|| !KingdomConstruction.IsCurrent(Updated)
				|| !IsExactPlanMarker(MarkerObject, zone, expected, Updated, Entry, true))
			{
				bool removed = RemoveCreated(scaffold, zone);
				KingdomConstruction.Quarantine(ref Updated, removed
					? "The published plan scaffold changed during AddObject."
					: "The published plan scaffold changed and could not be removed exactly.");
				return false;
			}
			string markerId = MarkerObject.ID;
			bool markerRemoved;
			try
			{
				markerRemoved = MarkerObject.Destroy(null, Silent: true);
			}
			catch (System.Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(zone, MarkerObject);
				KingdomConstruction.Quarantine(ref Updated,
					"Plan-marker removal threw after scaffold placement: " + ex.Message);
				return false;
			}
			if (markerRemoved && !GameObject.Validate(MarkerObject))
				KingdomSurvey.ObserveRemovedFromActive(zone, MarkerObject);
			if (KingdomConstructionRules.ExactRemovalAction(true, markerRemoved,
				GameObject.Validate(MarkerObject), KingdomConstruction.FindExactId(
					zone, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
				!= KingdomExactRemovalAction.ProvedAbsent)
			{
				KingdomConstruction.Quarantine(ref Updated,
					"Plan-marker removal was vetoed, moved, replaced, or only partially changed the predecessor.");
				return false;
			}
			GameObject exactAfterRemoval;
			if (!KingdomConstruction.Owns(System, zone, Updated)
				|| KingdomConstruction.FindExactId(zone, scaffold.ID, out exactAfterRemoval)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactAfterRemoval, scaffold)
				|| !IsExactPlanScaffold(scaffold, zone, expected, Updated, Entry)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The planned scaffold changed during marker removal.");
				return false;
			}
			scaffold.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, markerId);
			if (!r_KingdomScaffold.HasRemovalProof(scaffold, markerId))
			{
				KingdomConstruction.Quarantine(ref Updated,
					"The planned scaffold did not retain marker-removal proof.");
				return false;
			}
			if (!KingdomConstruction.UpdateSubject(ref Updated, scaffold.ID))
			{
				return false;
			}
			if (!KingdomConstruction.FinishProjection(ref Updated, true, true)) return false;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(Entry.Name) + " began to rise at " + realm + ", true to the plan staked there");
			System.Ledger.Note("{{G|The plan staked at " + realm + " is under way: the " + Entry.Name + " rises.}}");
			MessageQueue.AddPlayerMessage("{{G|The plan staked at " + realm + " is under way. The " + Entry.Name + " rises.}}");
			return true;
		}

		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null || Job.Route != KingdomConstructionRoute.PlanScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry))
			{
				return;
			}
			if (CountPlanScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"More than one planned scaffold carries the exact receipt.");
				return;
			}
			GameObject existing = FindPlanScaffold(Z, Job, entry);
			GameObject marker = FindExactPlanMarker(System, Z, Job, entry);
			GameObject namedSubject;
			KingdomPhysicalLookupState subjectState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out namedSubject);
			if (subjectState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The planned subject ID resolves to more than one loaded object.");
				return;
			}
			if (GameObject.Validate(namedSubject) && marker == null
				&& (existing == null || namedSubject != existing))
			{
				KingdomConstructionJob moved = Job;
				KingdomConstruction.Quarantine(ref moved,
					"The paid plan predecessor no longer matches its recorded cell or design.");
				return;
			}
			if (existing != null && existing.CurrentCell == Z.GetCell(Job.X, Job.Y)
				&& existing.GetPart<r_KingdomScaffold>() != null
				&& existing.GetPart<r_KingdomScaffold>().TargetBlueprint == entry.Blueprint)
			{
				KingdomConstructionJob complete = Job;
				if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
				{
					if (!KingdomConstruction.BeginProjection(ref complete, out _)) return;
					if (!KingdomConstruction.IsCurrent(complete)
						|| !KingdomConstruction.HasReceipt(marker, complete)
						|| !KingdomConstruction.HasReceipt(existing, complete)) return;
					string markerId = marker.ID;
					bool removed;
					try
					{
						removed = marker.Destroy(null, Silent: true);
					}
					catch (System.Exception ex)
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
						KingdomConstruction.Quarantine(ref complete,
							"Plan-marker retry threw during removal: " + ex.Message);
						return;
					}
					if (removed && !GameObject.Validate(marker))
						KingdomSurvey.ObserveRemovedFromActive(Z, marker);
					if (KingdomConstructionRules.ExactRemovalAction(true, removed,
						GameObject.Validate(marker), KingdomConstruction.FindExactId(
							Z, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
						!= KingdomExactRemovalAction.ProvedAbsent)
					{
						KingdomConstruction.Quarantine(ref complete,
							"Plan-marker retry was vetoed, moved, replaced, or partially changed.");
						return;
					}
					GameObject exactAfterRemoval;
					if (!KingdomConstruction.Owns(System, Z, complete)
						|| KingdomConstruction.FindExactId(Z, existing.ID, out exactAfterRemoval)
							!= KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactAfterRemoval, existing)
						|| !IsExactPlanScaffold(existing, Z, Z.GetCell(Job.X, Job.Y), complete, entry)
						|| !KingdomConstruction.IsCurrent(complete))
					{
						KingdomConstruction.Quarantine(ref complete,
							"The planned scaffold changed during retried marker removal.");
						return;
					}
					existing.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, markerId);
					if (!r_KingdomScaffold.HasRemovalProof(existing, markerId))
					{
						KingdomConstruction.Quarantine(ref complete,
							"The planned scaffold did not retain retried marker-removal proof.");
						return;
					}
				}
				if (!GameObject.Validate(existing) || existing.CurrentCell != Z.GetCell(Job.X, Job.Y)
					|| !KingdomConstruction.HasReceipt(existing, complete)
					|| !KingdomConstruction.IsCurrent(complete)) return;
				if (complete.SubjectId != existing.ID)
				{
					if (!r_KingdomScaffold.HasRemovalProof(existing, complete.SubjectId))
					{
						KingdomConstruction.Quarantine(ref complete,
							"The planned scaffold lacks exact marker-removal proof.");
						return;
					}
					if (!KingdomConstruction.UpdateSubject(ref complete, existing.ID)) return;
				}
				r_KingdomScaffold part = existing.GetPart<r_KingdomScaffold>();
				if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
					part.RetryDurable(System, Z, complete);
				else
					KingdomConstruction.FinishProjection(ref complete, true, true);
				return;
			}
			if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
			{
				Realize(System, marker, entry, Job, out _);
				return;
			}
			KingdomConstructionJob absent = Job;
			KingdomConstruction.Quarantine(ref absent,
				"The planned receipt has no exact marker or scaffold at its recorded cell.");
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.PlanScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)) return;
			GameObject result = FindPlanScaffold(Z, Job, entry);
			Cell cell = Z.GetCell(Job.X, Job.Y);
			KingdomConstructionJob inspected = Job;
			if (CountPlanScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one planned scaffold carries the exact receipt.");
				return;
			}
			GameObject marker = FindExactPlanMarker(System, Z, Job, entry);
			GameObject namedSubject;
			KingdomPhysicalLookupState subjectState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out namedSubject);
			if (subjectState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The planned subject ID resolves to more than one loaded object.");
				return;
			}
			r_KingdomScaffold scaffold = GameObject.Validate(result)
				? result.GetPart<r_KingdomScaffold>() : null;
			GameObject successor;
			int successors = r_KingdomScaffold.FindExactSuccessors(Z, Job,
				entry.Blueprint, result, out successor);
			if (successors > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one exact planned successor carries this receipt.");
				return;
			}
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				if (result != null)
				{
					if (!KingdomConstructionRules.FullyFundedExact(Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"A premature terminal plan does not carry exact paid claims.");
						return;
					}
					if (marker != null)
					{
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal plan still has its exact marker to remove.");
						return;
					}
					// Migration for the first registry wave, which terminalized after marker
					// removal and scaffold placement. That old path carried no removal-proof stamp.
					if (inspected.SubjectId != result.ID
						&& !KingdomConstruction.UpdateSubject(ref inspected, result.ID)) return;
					if (successors == 1)
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal receipt still has an exact scaffold to remove.");
					else
						KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				else if (successors == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The terminal planned successor lacks scaffold-removal proof.");
						return;
					}
					r_KingdomScaffold.TellCompletion(System, successor, Job);
				}
				return;
			}
			if (scaffold != null && result.CurrentCell == cell
				&& scaffold.TargetBlueprint == entry.Blueprint)
			{
				if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
				{
					KingdomConstruction.FinishProjection(ref inspected, false, false,
						"The scaffold is verified and its exact plan marker still needs removal.");
				}
				else
				{
					if (inspected.SubjectId != result.ID)
					{
						if (!r_KingdomScaffold.HasRemovalProof(result, inspected.SubjectId))
						{
							KingdomConstruction.Quarantine(ref inspected,
								"The planned scaffold lacks exact marker-removal proof after reload.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref inspected, result.ID)) return;
					}
				int finalPending = result.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The planned scaffold final flag is not an exact boolean.");
					return;
				}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
					{
						KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
					else if (Job.Phase == KingdomConstructionPhase.Working
						|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
						scaffold.AdvanceDurable(System, Z, inspected, The.Game.TimeTicks);
					else if (Job.Phase == KingdomConstructionPhase.Outstanding)
					{
						if (scaffold.RemainingTicks <= 0 && scaffold.LastWorkedTick > 0)
							scaffold.RetryDurable(System, Z, inspected);
						else
							KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
				}
				return;
			}
			if (successors == 1)
			{
				if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The planned successor lacks exact scaffold-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, successor, inspected);
				return;
			}
			if (GameObject.Validate(namedSubject) && marker == null
				&& (result == null || namedSubject != result))
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The plan predecessor moved or changed outside its exact recorded identity.");
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The interrupted plan projection has no safely identifiable exact endpoint.");
		}

		private static GameObject FindExactPlanMarker(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			GameObject marker;
			if (KingdomConstruction.FindExactId(Z, Job?.SourceId ?? Job?.SubjectId,
				out marker) != KingdomPhysicalLookupState.Exact) return null;
			Cell cell = Z == null || Job == null ? null : Z.GetCell(Job.X, Job.Y);
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| !IsExactPlanMarker(marker, Z, cell, Job, Entry, false)) return null;
			if (string.IsNullOrEmpty(marker.GetStringProperty(KingdomConstruction.ReceiptProperty)))
				KingdomConstruction.Bind(marker, Job);
			return IsExactPlanMarker(marker, Z, cell, Job, Entry, true) ? marker : null;
		}

		private static bool IsExactPlanMarker(GameObject Marker, Zone Z, Cell Cell,
			KingdomConstructionJob Job, KingdomRules.BuildEntry Entry, bool RequireReceipt)
		{
			if (!GameObject.Validate(Marker) || Z == null || Cell == null || Job == null
				|| Entry == null || Marker.ID != (Job.SourceId ?? Job.SubjectId) || Marker.CurrentZone != Z
				|| Marker.CurrentCell != Cell || Cell != Z.GetCell(Job.X, Job.Y)) return false;
			r_KingdomPlanMarker marker = Marker.GetPart<r_KingdomPlanMarker>();
			string receipt = Marker.GetStringProperty(KingdomConstruction.ReceiptProperty);
			return marker != null && marker.DesignKey == Entry.Key
				&& (RequireReceipt ? receipt == Job.Id
					: string.IsNullOrEmpty(receipt) || receipt == Job.Id);
		}

		private static bool IsExactPlanScaffold(GameObject Scaffold, Zone Z, Cell Cell,
			KingdomConstructionJob Job, KingdomRules.BuildEntry Entry)
		{
			if (!GameObject.Validate(Scaffold) || Z == null || Cell == null || Job == null
				|| Entry == null || Scaffold.CurrentZone != Z || Scaffold.CurrentCell != Cell
				|| Cell != Z.GetCell(Job.X, Job.Y)
				|| Scaffold.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != Entry.Key
				|| !KingdomConstruction.HasReceipt(Scaffold, Job)) return false;
			r_KingdomScaffold scaffold = Scaffold.GetPart<r_KingdomScaffold>();
			return scaffold != null && scaffold.TargetBlueprint == Entry.Blueprint
				&& (KingdomConstruction.BuildTruthMatches(Scaffold, Job)
					|| KingdomConstruction.LegacyProjectedBuildTruthMatches(
						Scaffold, Job, false));
		}

		private static GameObject FindPlanScaffold(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return null;
			Cell cell = Z.GetCell(Job.X, Job.Y);
			if (cell == null) return null;
			GameObject found = null;
			GameObject exact = null;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExactPlanScaffold(item, Z, cell, Job, Entry))
				{
					count++;
					if (found == null) found = item;
					if (item.ID == Job.OutputId || item.ID == Job.SubjectId) exact = item;
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

		private static int CountPlanScaffolds(Zone Z, KingdomConstructionJob Job,
			KingdomRules.BuildEntry Entry)
		{
			if (Z == null || Job == null || Entry == null) return 0;
			Cell cell = Z?.GetCell(Job.X, Job.Y);
			if (cell == null) return 0;
			int count = 0;
			foreach (GameObject item in cell.GetObjects())
			{
				if (IsExactPlanScaffold(item, Z, cell, Job, Entry))
					{
						if (item.ID != Job.OutputId && item.ID != Job.SubjectId) return 2;
						count++;
					}
			}
			return count;
		}

		/// <summary>Every plan currently staked and waiting in Z, oldest first.</summary>
		public static List<GameObject> FindPending(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.HasPart("r_KingdomPlanMarker"))
				{
					found.Add(item);
				}
			}
			found.Sort(delegate(GameObject a, GameObject b)
			{
				r_KingdomPlanMarker markerA = a.GetPart<r_KingdomPlanMarker>();
				r_KingdomPlanMarker markerB = b.GetPart<r_KingdomPlanMarker>();
				return KingdomPlanRules.CompareOrder(
					new KingdomPendingPlan(markerA.PlacedTick, markerA.PlacedOrder, 0, false),
					new KingdomPendingPlan(markerB.PlacedTick, markerB.PlacedOrder, 0, false));
			});
			return found;
		}

		/// <summary>What Marker is staked to become, for a menu line or a confirmation prompt.</summary>
		public static string Describe(GameObject Marker)
		{
			if (Marker == null)
			{
				return "a plan";
			}
			r_KingdomPlanMarker part = Marker.GetPart<r_KingdomPlanMarker>();
			if (part != null && KingdomData.TryGetBuilding(part.DesignKey, out var entry))
			{
				return entry.Name;
			}
			return Marker.ShortDisplayName ?? "a plan";
		}

		/// <summary>
		/// Calls off a staked plan. Costs nothing and returns nothing, because a plan never
		/// spends anything until the moment it is realised &mdash; there is nothing here to
		/// refund, the same way nothing is refunded for a commission never issued.
		/// </summary>
		public static void Cancel(GameObject Marker)
		{
			Zone zone = Marker?.CurrentZone;
			bool removed = Marker != null && Marker.Destroy(null, Silent: true);
			if (removed && !GameObject.Validate(Marker))
				KingdomSurvey.ObserveRemovedFromActive(zone, Marker);
		}
	}
}
