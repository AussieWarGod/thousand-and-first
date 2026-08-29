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
	public static partial class KingdomPlanMarker
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
				if (!EnsureLegacyProvenance(System, Z, item)) continue;
				KingdomPlanReceiptShape receiptShape = ReceiptShape(item, out _);
				if (receiptShape == KingdomPlanReceiptShape.Corrupt) continue;
				if (receiptShape == KingdomPlanReceiptShape.Exact
					&& !TryReleaseCleanReceipt(System, Z, item, out _)) continue;
				if (!PublicationAllowed(System, Z, item, out _, out _)) continue;
				// A design an outside mod withdrew (or one that never shipped) leaves its marker
				// waiting forever rather than throwing or silently vanishing -- the same
				// "waiting is not failing" contract as a plan that simply cannot afford its cost
				// yet.
				if (!KingdomData.TryGetBuilding(marker.DesignKey, out var entry))
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
				if (!PublicationAllowed(System, Z, markers[index], out _, out _)) continue;
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
				if (!PublicationAllowed(System, Z, markerObject,
						out KingdomPlanMarkerProof publicationProof, out _)
					|| !PreparedJobMatches(publicationProof, job))
				{
					water.Rollback();
					materials.Cancel();
					continue;
				}
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

	}
}
