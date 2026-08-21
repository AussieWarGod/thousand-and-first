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
	/// or moved until <see cref="ThousandAndFirst.KingdomPlanMarker.OnSettlementPass"/> replaces
	/// this exact object, in this exact cell, with an <c>r_KingdomScaffold</c>.
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
			if (!KingdomGrowth.Enabled || System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			List<GameObject> markers = new List<GameObject>();
			List<KingdomRules.BuildEntry> entries = new List<KingdomRules.BuildEntry>();
			List<KingdomPendingPlan> pending = new List<KingdomPendingPlan>();
			foreach (GameObject item in Z.GetObjects())
			{
				r_KingdomPlanMarker marker = item.GetPart<r_KingdomPlanMarker>();
				if (marker == null || string.IsNullOrEmpty(marker.DesignKey))
				{
					continue;
				}
				// A design an outside mod withdrew (or one that never shipped) leaves its marker
				// waiting forever rather than throwing or silently vanishing -- the same
				// "waiting is not failing" contract as a plan that simply cannot afford its cost
				// yet.
				if (!KingdomData.TryGetBuilding(marker.DesignKey, out var entry))
				{
					continue;
				}
				markers.Add(item);
				entries.Add(entry);
				pending.Add(new KingdomPendingPlan(marker.PlacedTick, marker.PlacedOrder, entry.CostDrams, entry.Defence > 0));
			}
			if (pending.Count == 0)
			{
				return;
			}
			int built = CountBuilt(Z);
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
				// Measured, not trusted (STANDARDS.md): PlansToRealize decided this plan was
				// affordable from the survey's own snapshot, but Consume can still return less
				// than asked if what that snapshot counted cannot actually be drained in one
				// draw. A short draw is left exactly where it was found -- the marker still
				// stands, and it simply tries again next pass.
				int drawn = Survey.Consume(entries[index].CostDrams);
				if (drawn < entries[index].CostDrams)
				{
					continue;
				}
				Realize(System, markers[index], entries[index]);
			}
		}

		/// <summary>
		/// Counts buildings this zone already has standing or scaffolded, by the exact rule
		/// <c>KingdomCommission.Commission</c> uses for its own cap check: walls are exempt, a
		/// scaffold in progress already counts. Kept in step with that rule by hand, since a plan
		/// and a founder-issued commission must compete for one shared allowance rather than each
		/// getting their own.
		/// </summary>
		private static int CountBuilt(Zone Z)
		{
			int built = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomDefence") > 0 || item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1)
				{
					continue;
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold") || item.HasPart("r_KingdomPlotWorks"))
				{
					built++;
				}
			}
			return built;
		}

		/// <summary>
		/// Turns a realised plan into a scaffold, in place: the same <c>r_KingdomScaffold</c>
		/// pipeline <c>KingdomCommission.Commission</c> hands a founder-issued commission to, just
		/// sited at the marker's own cell instead of a freshly chosen one, because the founder
		/// already chose it when they staked the plan.
		/// </summary>
		private static void Realize(KingdomSystem System, GameObject MarkerObject, KingdomRules.BuildEntry Entry)
		{
			Cell cell = MarkerObject.CurrentCell;
			if (cell == null)
			{
				return;
			}
			// A plot-sized design measures out a rect from this stake rather than raising a single
			// scaffold on it. It chronicles and announces itself; everything below is the
			// single-cell path, unchanged.
			if (KingdomPlots.StakeFromPlan(System, MarkerObject, Entry))
			{
				return;
			}
			GameObject scaffold = GameObject.Create("r_KingdomScaffold");
			if (scaffold == null)
			{
				return;
			}
			// Read off the marker before it is taken down: the look the founder chose when they
			// staked the plan rides on the marker exactly as it rides on a scaffold.
			scaffold.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			KingdomDesign.StageSkin(scaffold, Entry, MarkerObject.GetStringProperty(KingdomDesign.PlannedSkinProperty));
			KingdomCeremony.TransferPlanQuote(MarkerObject, scaffold);
			MarkerObject.Destroy(null, Silent: true);
			r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
			if (part != null)
			{
				part.TargetBlueprint = Entry.Blueprint;
				part.TargetDisplayName = Entry.Name;
				part.CompleteTick = The.Game.TimeTicks + KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values);
				part.StaffNeeded = Entry.Staff;
				part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
				if (Entry.Defence > 0)
				{
					// The founder need not be standing at this exact cell for a plan to resolve
					// (only in the same active zone), but the skill the wall math reads is the
					// founder's own, not a property of where they are standing -- so it is read
					// exactly as KingdomCommission.Commission reads it.
					bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
					bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
					int defence = KingdomRules.WallDefence(Entry.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName, hasTinkering, hasAdvancedTinkering);
					scaffold.SetIntProperty("KingdomDefencePending", defence);
				}
			}
			cell.AddObject(scaffold);
			KingdomChronicle.Record(System, XRL.Language.Grammar.A(Entry.Name) + " began to rise at " + System.KingdomDisplayName + ", true to the plan staked there");
			System.Ledger.Note("{{G|The plan staked at " + System.KingdomDisplayName + " is under way: the " + Entry.Name + " rises.}}");
			MessageQueue.AddPlayerMessage("{{G|The plan staked at " + System.KingdomDisplayName + " is under way. The " + Entry.Name + " rises.}}");
		}

		/// <summary>Every plan currently staked and waiting in Z, oldest first.</summary>
		public static List<GameObject> FindPending(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in Z.GetObjects())
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
			Marker?.Destroy(null, Silent: true);
		}
	}
}
