using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPower
	{
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
			Gather(KingdomSurvey.ObjectsFor(Z), works, stores, sinks);
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

		private static void Gather(IEnumerable<GameObject> Objects, List<GameObject> Works,
			List<GameObject> Stores, List<GameObject> Sinks)
		{
			foreach (GameObject item in Objects)
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
		/// Says once why a work is standing still, and unsays it when it turns again.
		/// <para>
		/// This used to live inside the per-work credit loop, which is where power's second
		/// accounting lived. The TELLING survives the migration unchanged &mdash; STANDARDS 7b's
		/// applicable-but-blocked rule is about the founder, not about the arithmetic &mdash; and
		/// what left with the loop is the day counting and the summing.
		/// </para>
		/// </summary>
		private static void Dry(KingdomSystem System, GameObject Work, int Output, KingdomPowerRules.PowerSource Source)
		{
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null)
			{
				return;
			}
			if (Output > 0)
			{
				part.DryAnnounced = false;
				return;
			}
			if (part.DryAnnounced)
			{
				return;
			}
			part.DryAnnounced = true;
			System.Ledger.Note("{{r|" + KingdomPowerRules.IdleReason(Source) + "}}");
		}

		/// <summary>
		/// What one work makes in a day right now: its rating, cut by the crew the settlement
		/// gave it, by how worn it is, and by what the ground or the sky is giving it.
		/// </summary>
		private static int DailyOutput(GameObject Work, Zone Z, int Days, out KingdomPowerRules.PowerSource Source)
		{
			Source = KingdomPowerRules.PowerSource.Hands;
			r_KingdomPowerWork part = Work.GetPart<r_KingdomPowerWork>();
			if (part == null || !KingdomPowerRules.TryParseSource(part.Source, out Source))
			{
				return 0;
			}
			// What the work is actually managing: the fraction of the hands it asked for that it
			// got, or - for a work that asked for nobody - its own condition. Damage dims a power
			// work in proportion, staffed or not (Addendum 10(b): "solar panels reduce power
			// output"), and it reaches the staffed ones too. It did not before: this file runs
			// inside the growth pass and KingdomEffectiveness is the staffing pass's crew stretch
			// at that point, so a half-wrecked mill made a whole mill's charge.
			int crew = KingdomWear.EffectivenessOf(Work);
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

	}
}
