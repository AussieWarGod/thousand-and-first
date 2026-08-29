using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the parts move; the
// settlement-side resolver below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// A work the settlement raised to make power: a mill someone leans on, a wheel a brook
	/// turns, a vane the wind pushes. Carries its own tick stamp, so a city nobody has stood
	/// in for a week resolves its own absence the moment the founder walks back in, and needs
	/// no clock running behind them.
	/// </summary>
	[Serializable]
	public class r_KingdomPowerWork : IPart
	{
		/// <summary>
		/// What turns this work, named in XML as <c>Hands</c>, <c>Water</c>, or <c>Wind</c>. An
		/// unreadable value disables the work rather than defaulting it, so a third party's
		/// misspelling is inert instead of quietly becoming a mill.
		/// </summary>
		public string Source = "Hands";

		/// <summary>Tick this work was last credited to. Zero until the first settlement pass
		/// stamps it, which is why a work never pays out for the day it was raised.</summary>
		public long LastResolvedTick;

		/// <summary>Set once the founder has been told this work is standing still, so a wheel
		/// beside no water says so once rather than at every homecoming.</summary>
		public bool DryAnnounced;

		/// <summary>
		/// A day also turns over while the founder is standing there watching it. The
		/// settlement pass resolves absence, which is the hard half, but it only runs on zone
		/// activation &mdash; so a founder who commissions a mill and then stays put would see
		/// nothing happen for as long as they stayed. The comparison is against an absolute
		/// tick this part stored, not a countdown that must be delivered every turn to stay
		/// correct, so missing ticks costs nothing; <c>r_KingdomPlot</c> and
		/// <c>r_KingdomScaffold</c> both keep time the same way.
		/// </summary>
		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master)) return;
			if (LastResolvedTick <= master.MasterOptionTick)
			{
				LastResolvedTick = TimeTick;
				return;
			}
			if (LastResolvedTick <= 0 || TimeTick < LastResolvedTick + KingdomRules.TicksPerDay)
			{
				return;
			}
			Zone zone = ParentObject?.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			// Surveying is the expensive part, so it happens only on the tick a day is due -
			// once per day, not once per turn. Crews are assigned first: a work commissioned
			// while the founder stood watching has never been through a growth pass, and
			// would otherwise be credited as though nobody in the settlement had come to it.
			KingdomSystem.Guard("power tick", delegate
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, system);
				KingdomGrowth.AssignWork(system, survey);
				KingdomPower.OnSettlementPass(system, zone, survey);
			});
		}
	}

	/// <summary>
	/// A bed of molten salt the settlement keeps hot: it holds what the day's works made and
	/// gives it back after dark. A marker part, so that any object carrying it and a
	/// <c>Capacitor</c> &mdash; ours, or a third party's own design &mdash; is a store the
	/// settlement will pour into, with no code change and no registry entry.
	/// </summary>
	[Serializable]
	public class r_KingdomPowerStore : IPart
	{
		/// <summary>Set the first time this store was filled to the brim, so the moment is
		/// written down once and never again.</summary>
		public bool EverFilled;

		/// <summary>
		/// Tick this store was last resolved to. Kept even though the works keep their own,
		/// so that a store outliving every work it was built beside still measures a day
		/// honestly and gives its salt back over one, rather than on every arrival.
		/// </summary>
		public long LastResolvedTick;
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The settlement's power, resolved on the settlement's own clock. A working mill is
	/// settlers leaning on a bar and a working post is somebody carrying the charge across to
	/// it, so there is no grid to wire and no machine to manage: the founder commissions the
	/// works, the settlement crews them, and what they make reaches whatever the settlement
	/// built to spend it.
	/// </summary>
	/// <remarks>
	/// Charge is vanilla charge throughout. The works are vanilla blueprints, the stores and
	/// posts are vanilla <c>Capacitor</c>s, and delivery goes through vanilla's own
	/// <c>ChargeAvailableEvent</c>, so anything with a charge-bearing part the settlement
	/// raised is powered for free. What is deliberately <em>not</em> used is vanilla's
	/// <c>IPowerTransmission</c> grid: it is built from cardinal-adjacent runs of matching
	/// conduit (<c>IPowerTransmission.FindGrid</c>), which would make the founder lay gearbox
	/// fence between a windmill and a charging post to get anywhere &mdash; a wiring puzzle,
	/// and one this mod's automatic placement could not guarantee a solution to. It also only
	/// exists in an active zone, so a dormant city would have no grid at all.
	/// </remarks>
	public static partial class KingdomPower
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionPower") != "No";

		/// <summary>
		/// Runs the settlement's power for however many days have passed since each work was
		/// last credited. Called from the growth pass, after crews are assigned and after every
		/// step that draws water, so a work is only ever credited for hands it actually had and
		/// can never be the reason the thirst ladder fires.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">The claimed zone being resolved.</param>
		/// <param name="Survey">The pass's shared survey. Null skips the pass.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || !KingdomGrowth.Enabled || System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			List<GameObject> works = new List<GameObject>();
			List<GameObject> stores = new List<GameObject>();
			List<GameObject> sinks = new List<GameObject>();
			Gather(Survey.Objects, works, stores, sinks);
			if (works.Count == 0 && stores.Count == 0)
			{
				return;
			}
			// ---- One clock. -----------------------------------------------------------------
			// W7. Power used to count its own days, per work, off ElapsedDays and a
			// remainder-keeping checkpoint, and then do its own summing, its own store clamp and
			// its own delivery. That was a second accounting standing beside the model's, which is
			// exactly the thing W6 made unrepresentable for production and this wave makes
			// unrepresentable for charge. Days are now WORLD-DAY BOUNDARIES through the one counter
			// every other lane uses, so Days(a,b) + Days(b,c) == Days(a,c) for any split: a founder
			// who walks in twice in one day is not paid twice, and a horizon that falls mid-day
			// does not drop the remainder.
			long through = Through(works, stores);
			Plant(works, stores, timeTicks);
			long days;
			KingdomCityFault fault;
			if (through <= 0L
				|| !KingdomProductionRules.TryDaysBetween(through, timeTicks, KingdomRules.TicksPerDay, out days, out fault)
				|| days <= 0L)
			{
				return;
			}
			int capacity = Capacity(stores);
			int held = Held(stores);
			// ---- One graph, one solve. ------------------------------------------------------
			KingdomNetworkGraph graph;
			KingdomFlowDemand[] demands;
			int[] order;
			long supplyPerDay;
			if (!TryCompose(System, Z, works, stores, sinks, days, capacity, out graph, out demands, out order, out supplyPerDay))
			{
				return;
			}
			KingdomFlowSolution solution;
			if (!KingdomFlowRules.TrySolve(
					supplyPerDay,
					demands,
					demands.Length,
					order,
					held,
					capacity,
					KingdomPowerRules.ThroughputForDays(capacity, 1),
					days,
					out solution,
					out fault))
			{
				KingdomLog.Log("power: solve refused (" + fault + "); nothing was spent and no stamp moved");
				return;
			}
			// ---- One rendering. -------------------------------------------------------------
			// Every stamp advances to the same tick, and it advances only now that the solve has
			// succeeded: a refused solve leaves every clock where it was, so the day is owed rather
			// than lost.
			Stamp(works, stores, timeTicks);
			int drawn = (solution.Discharged > 0L) ? Withdraw(stores, (int)Clamp(solution.Discharged)) : 0;
			int pool = (int)Clamp(solution.Generated) + drawn;
			int delivered = Deliver(System, sinks, demands, order, solution.Stopped, (int)Clamp(solution.Delivered), pool);
			int spare = pool - delivered;
			if (spare > 0)
			{
				int poured = Deposit(stores, spare);
				if (poured > 0)
				{
					NoteStoreFilled(System, stores, Held(stores), capacity);
				}
				if (poured < spare)
				{
					// Made with nowhere to put it. Loss, never a queue -- the same ruling the
					// larder makes about a harvest it has no room for, and it is said rather than
					// quietly absorbed (STANDARDS 7b).
					KingdomLog.Log("power: " + (spare - poured) + " charge had nowhere to go; a larger salt store would keep it");
				}
			}
			Brownouts(System, Z, sinks, demands, order, solution.Stopped, timeTicks);
			if (delivered > 0)
			{
				System.Ledger.Note("{{c|The works of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " made " + delivered + " charge, and it went where it was wanted.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("power pass " + Z.ZoneID + " days=" + days + " works=" + works.Count
				+ " generated=" + solution.Generated + " demanded=" + solution.Demanded + " delivered=" + delivered
				+ " charged=" + solution.Charged + " discharged=" + solution.Discharged + " spilled=" + solution.Spilled
				+ " stopped=" + solution.Stopped + " held=" + Held(stores) + "/" + capacity);
		}

	}
}
