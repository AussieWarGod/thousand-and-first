using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

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
	public static class KingdomPower
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
			Gather(Z, works, stores, sinks);
			if (works.Count == 0 && stores.Count == 0)
			{
				return;
			}
			int generated = 0;
			int days = 0;
			for (int i = 0; i < works.Count; i++)
			{
				generated += Credit(System, works[i], Z, timeTicks, ref days);
			}
			for (int j = 0; j < stores.Count; j++)
			{
				r_KingdomPowerStore store = stores[j].GetPart<r_KingdomPowerStore>();
				if (store == null)
				{
					continue;
				}
				int stored = CreditDays(store.LastResolvedTick, timeTicks, out var next);
				store.LastResolvedTick = next;
				if (stored > days)
				{
					days = stored;
				}
			}
			if (days <= 0)
			{
				return;
			}
			int capacity = Capacity(stores);
			int held = Held(stores);
			int delivered = (generated > 0) ? Deliver(System, sinks, generated) : 0;
			if (delivered < generated)
			{
				int poured = Deposit(stores, KingdomPowerRules.Absorbable(generated - delivered, held, capacity, days));
				if (poured > 0)
				{
					NoteStoreFilled(System, stores, held + poured, capacity);
				}
			}
			else if (held > 0)
			{
				// Either the works gave the posts everything they had and the posts wanted more,
				// or the works made nothing at all - a dead calm, a dry brook, nobody on the bar.
				// Both are the night the salt was banked for. Whatever the posts turn out not to
				// want goes straight back: it never left the settlement, so nothing is lost.
				int drawn = Withdraw(stores, KingdomPowerRules.Releasable(held, capacity, days));
				int used = Deliver(System, sinks, drawn);
				Deposit(stores, drawn - used);
				delivered += used;
			}
			if (delivered > 0)
			{
				System.Ledger.Note("{{c|The works of " + System.KingdomDisplayName + " made " + delivered + " charge, and it went where it was wanted.}}");
			}
			if (KingdomLog.Enabled) KingdomLog.Log("power pass " + Z.ZoneID + " days=" + days + " works=" + works.Count + " generated=" + generated + " delivered=" + delivered + " held=" + Held(stores) + "/" + capacity);
		}

		/// <summary>
		/// The settlement's power as one line for the Charter's status, or empty when this
		/// ground has nothing to say about power.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Ground to report on; must be the kingdom's own claimed zone.</param>
		public static string StatusLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return "";
			}
			List<GameObject> works = new List<GameObject>();
			List<GameObject> stores = new List<GameObject>();
			List<GameObject> sinks = new List<GameObject>();
			Gather(Z, works, stores, sinks);
			int perDay = 0;
			string reason = KingdomPowerRules.IdleNoWorks;
			for (int i = 0; i < works.Count; i++)
			{
				int output = DailyOutput(works[i], Z, 1, out var source);
				if (output > 0)
				{
					perDay += output;
				}
				else if (perDay <= 0)
				{
					reason = KingdomPowerRules.IdleReason(source);
				}
			}
			KingdomPowerRules.SupplyTier tier = KingdomPowerRules.ClassifySupply(perDay, works.Count, sinks.Count);
			return KingdomPowerRules.SupplyLine(tier, perDay, Held(stores), Capacity(stores), reason);
		}

		private static void Gather(Zone Z, List<GameObject> Works, List<GameObject> Stores, List<GameObject> Sinks)
		{
			foreach (GameObject item in Z.GetObjects())
			{
				// The founder's designation is the whole of the grid's membership: what the
				// settlement raised, plus anything explicitly dedicated to it. Nothing the
				// player merely left lying about is ever charged, moved, or read.
				if (item.GetIntProperty("KingdomBuilt") != 1 && item.GetIntProperty("KingdomGrid") != 1)
				{
					continue;
				}
				if (item.GetPart<r_KingdomPowerWork>() != null)
				{
					Works.Add(item);
				}
				else if (item.GetPart<r_KingdomPowerStore>() != null && item.GetPart<Capacitor>() != null)
				{
					Stores.Add(item);
				}
				else if (ChargeAvailableEvent.Wanted(item))
				{
					Sinks.Add(item);
				}
			}
		}

		/// <summary>
		/// Advances one work's own clock and returns what it made in the days that passed.
		/// Reports the largest day count any work credited through <paramref name="Days"/>, so
		/// the store's throughput is measured against the same span the charge was made over.
		/// </summary>
		private static int Credit(KingdomSystem System, GameObject Work, Zone Z, long TimeTicks, ref int Days)
		{
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null)
			{
				return 0;
			}
			int days = CreditDays(part.LastResolvedTick, TimeTicks, out var next);
			part.LastResolvedTick = next;
			if (days <= 0)
			{
				return 0;
			}
			if (days > Days)
			{
				Days = days;
			}
			int output = KingdomPowerRules.ChargeForDays(DailyOutput(Work, Z, days, out var source), days);
			if (output > 0)
			{
				part.DryAnnounced = false;
				return output;
			}
			if (!part.DryAnnounced)
			{
				part.DryAnnounced = true;
				System.Ledger.Note("{{r|" + KingdomPowerRules.IdleReason(source) + "}}");
			}
			return 0;
		}

		/// <summary>
		/// Whole days elapsed on one part's own stamp, and the stamp it should carry away.
		/// A part with no stamp yet takes the current tick and is credited nothing, so a work
		/// never pays out for the day it was raised; time beyond the absence cap is forgiven by
		/// starting a fresh checkpoint, exactly as the water heartbeat does.
		/// </summary>
		private static int CreditDays(long PreviousTick, long TimeTicks, out long NextTick)
		{
			if (PreviousTick <= 0 || TimeTicks <= PreviousTick)
			{
				NextTick = TimeTicks;
				return 0;
			}
			int days = KingdomRules.HeartbeatDays(TimeTicks - PreviousTick);
			NextTick = (days <= 0) ? PreviousTick : KingdomRules.HeartbeatCheckpoint(PreviousTick, TimeTicks);
			return days;
		}

		/// <summary>
		/// What one work makes in a day right now: its rating, cut by the crew the settlement
		/// gave it and by what the ground or the sky is giving it.
		/// </summary>
		private static int DailyOutput(GameObject Work, Zone Z, int Days, out KingdomPowerRules.PowerSource Source)
		{
			Source = KingdomPowerRules.PowerSource.Hands;
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null || !KingdomPowerRules.TryParseSource(part.Source, out Source))
			{
				return 0;
			}
			// A work that asked for nobody is always fully crewed; one that asked for hands is
			// worth exactly the fraction of them it got, which is what the staffing pass wrote.
			int needed = Work.GetIntProperty("KingdomStaffNeeded");
			int crew = (needed > 0) ? Work.GetIntProperty("KingdomEffectiveness") : 100;
			int available;
			switch (Source)
			{
			case KingdomPowerRules.PowerSource.Water:
				available = KingdomPowerRules.WaterAvailabilityPercent(OpenWaterBeside(Work));
				break;
			case KingdomPowerRules.PowerSource.Wind:
				available = KingdomPowerRules.WindAvailabilityPercent(Z.CurrentWindSpeed, Days);
				break;
			default:
				available = 100;
				break;
			}
			return KingdomPowerRules.DailyOutput(Source, crew, available);
		}

		/// <summary>
		/// Open water standing in and beside a wheel's cell. Open pools only &mdash; a wheel is
		/// turned by a brook, never by the settlement's cisterns, which hold what the people
		/// drink and are not a fuel.
		/// </summary>
		private static int OpenWaterBeside(GameObject Work)
		{
			Cell cell = Work.CurrentCell;
			if (cell == null)
			{
				return 0;
			}
			int total = OpenWaterIn(cell);
			List<Cell> adjacent = cell.GetLocalAdjacentCells();
			if (adjacent != null)
			{
				for (int i = 0; i < adjacent.Count; i++)
				{
					total += OpenWaterIn(adjacent[i]);
				}
			}
			return total;
		}

		private static int OpenWaterIn(Cell C)
		{
			int total = 0;
			for (int i = 0; i < C.Objects.Count; i++)
			{
				LiquidVolume volume = C.Objects[i].GetPart<LiquidVolume>();
				if (volume != null && volume.MaxVolume < 0 && volume.Volume > 0)
				{
					total += volume.Volume;
				}
			}
			return total;
		}

		/// <summary>
		/// Offers charge to everything the settlement powers, in the order it stands, and
		/// returns what was actually taken.
		/// </summary>
		private static int Deliver(KingdomSystem System, List<GameObject> Sinks, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Sinks.Count && remaining > 0; i++)
			{
				GameObject sink = Sinks[i];
				if (!GameObject.Validate(sink))
				{
					continue;
				}
				// Forced, because a settlement pass is a day's work arriving at once, not one
				// turn's trickle: without it a Capacitor clamps intake to its per-turn ChargeRate
				// (Capacitor.Process, IInitialChargeProductionEvent) and a day's milling would be
				// throttled to a single turn's worth. The return is the event's own accumulated
				// delta (StartingAmount - Amount), not a convenience flag, but it is clamped
				// here anyway rather than trusted on its face.
				int used = sink.ChargeAvailable(remaining, 0L, 1, Forced: true);
				if (used <= 0)
				{
					continue;
				}
				if (used > remaining)
				{
					used = remaining;
				}
				remaining -= used;
				// Latched on the object rather than the settlement, so each thing the works
				// reach announces its own first charge once and never again - and so a dormant
				// city's posts keep their own memory of it without a field on the system.
				if (sink.GetIntProperty("KingdomPowered") != 1)
				{
					sink.SetIntProperty("KingdomPowered", 1);
					string drawing = KingdomDesign.ReferenceFor(sink, sink.ShortDisplayName);
					System.RecordDeed("the " + drawing + " of " + System.KingdomDisplayName + " drawing its first charge");
					KingdomChronicle.Record(System, "the works turned at " + System.KingdomDisplayName + ", and " + XRL.Language.Grammar.A(drawing) + " drew its first charge from hands and weather alone", Accomplishment: true);
					MessageQueue.AddPlayerMessage("{{G|The works of " + System.KingdomDisplayName + " are turning. The " + drawing + " draws from them.}}");
				}
			}
			return Amount - remaining;
		}

		private static int Capacity(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.MaxCharge > 0)
				{
					total += capacitor.MaxCharge;
				}
			}
			return total;
		}

		private static int Held(List<GameObject> Stores)
		{
			int total = 0;
			for (int i = 0; i < Stores.Count; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor != null && capacitor.Charge > 0)
				{
					total += capacitor.Charge;
				}
			}
			return total;
		}

		/// <summary>
		/// Pours charge into the stores and returns what actually went in, measured from the
		/// capacitors before and after rather than taken on the word of anything that was
		/// called. Never exceeds a store's own MaxCharge.
		/// </summary>
		private static int Deposit(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge >= capacitor.MaxCharge)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.AddCharge(remaining);
				int added = capacitor.Charge - before;
				if (added > 0)
				{
					remaining -= added;
				}
			}
			return Amount - remaining;
		}

		/// <summary>Draws charge back out of the stores, measured the same way.</summary>
		private static int Withdraw(List<GameObject> Stores, int Amount)
		{
			int remaining = Amount;
			for (int i = 0; i < Stores.Count && remaining > 0; i++)
			{
				Capacitor capacitor = Stores[i].GetPart<Capacitor>();
				if (capacitor == null || capacitor.Charge <= 0)
				{
					continue;
				}
				int before = capacitor.Charge;
				capacitor.UseCharge(remaining);
				int taken = before - capacitor.Charge;
				if (taken > 0)
				{
					remaining -= taken;
				}
			}
			return Amount - remaining;
		}

		/// <summary>
		/// Writes the moment a bed of salt first ran full, once, and never again. Named
		/// parameters rather than a re-survey: the caller has just measured both figures.
		/// </summary>
		private static void NoteStoreFilled(KingdomSystem System, List<GameObject> Stores, int HeldCharge, int TotalCapacity)
		{
			if (TotalCapacity <= 0 || HeldCharge < TotalCapacity)
			{
				return;
			}
			for (int i = 0; i < Stores.Count; i++)
			{
				r_KingdomPowerStore store = Stores[i].GetPart<r_KingdomPowerStore>();
				if (store == null || store.EverFilled)
				{
					continue;
				}
				store.EverFilled = true;
				KingdomChronicle.Record(System, "the salt at " + System.KingdomDisplayName + " ran full and bright, and the settlement kept its first whole night of light");
				System.Ledger.Note("{{G|The molten-salt store is full. The settlement keeps the night now.}}");
				// One telling per pass, however many beds of salt the settlement keeps.
				return;
			}
		}
	}
}
