using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidenceRules
	{
		// ==================================================================================
		// 1b. A city is every zone it holds, as each was last seen.
		// ==================================================================================

		/// <summary>
		/// What one claimed zone was holding the last time somebody stood in it: its binding
		/// carries, its dedicated storage, and the day that was.
		/// <para>
		/// <b>Knowledge, not truth</b> (the same doctrine <c>KingdomSystem.SupportedLevel</c>
		/// carries, and the same shape <c>KingdomReach.CityShadeExcept</c> already uses for the
		/// lifting half). Nothing here is simulated forward: a granary zone the founder has not
		/// walked into since spring goes on reporting spring's granary until they walk back in.
		/// That is honest in both directions &mdash; the city is never credited with works
		/// nobody has seen raised, and never docked for ruin nobody has seen fall.
		/// </para>
		/// <para>
		/// A zone that has never been visited has no sighting at all and contributes nothing,
		/// which is the only correct answer: the alternative is inventing state for ground the
		/// game has never looked at.
		/// </para>
		/// </summary>
		public struct ZoneSighting
		{
			/// <summary>Summed <c>water</c> carry seen in that zone.</summary>
			public int Water;

			/// <summary>Summed <c>food</c> carry seen there.</summary>
			public int Food;

			/// <summary>Summed <c>roof</c> carry seen there.</summary>
			public int Roof;

			/// <summary>Dedicated storage capacity seen there, as <c>KingdomRules.StageFor</c>
			/// reads it.</summary>
			public int StorageCapacity;

			/// <summary>The tick the sighting was taken. Zero means never seen, and a sighting
			/// that was never taken is not folded in.</summary>
			public long SeenTick;

			public ZoneSighting(int Water, int Food, int Roof, int StorageCapacity, long SeenTick)
			{
				this.Water = Water;
				this.Food = Food;
				this.Roof = Roof;
				this.StorageCapacity = StorageCapacity;
				this.SeenTick = SeenTick;
			}

			/// <summary>Whether anybody has ever stood in this zone and counted it.</summary>
			public bool Seen => SeenTick > 0L;
		}

		/// <summary>
		/// The whole city's carries: what the pass just counted from the ground under its feet,
		/// plus what every OTHER claimed zone was last seen holding.
		/// <para>
		/// <b>Only the binding half is summed here.</b> Water, food and roofs are physically
		/// drawn from citywide pools, so a two-zone city drinks from both zones' cisterns and
		/// sleeps under both zones' roofs, and measuring it by whichever zone the founder
		/// happened to walk in through made its level swing with the founder's footsteps.
		/// <c>SupportTally.Lift</c> is left exactly as handed in, because
		/// <c>KingdomSubsidence.ScopedSupports</c> has ALREADY summed the lifting half across the
		/// city through <c>KingdomReach.CityShadeExcept</c> (Addendum 6). Adding the other zones'
		/// lifts again here would count every shrine in the realm twice.
		/// </para>
		/// </summary>
		/// <param name="Here">The tally for the zone the pass is in, lifts already scoped.</param>
		/// <param name="Others">Sightings of every other claimed zone. Null or empty leaves the
		/// tally exactly as it was, which is what a one-zone city has always got.</param>
		public static KingdomCatalogueRules.SupportTally CityTally(KingdomCatalogueRules.SupportTally Here,
			IList<ZoneSighting> Others)
		{
			KingdomCatalogueRules.SupportTally tally = Here;
			tally.Water = KingdomCatalogueRules.SaturatingCounterAdd(tally.Water, 0);
			tally.Food = KingdomCatalogueRules.SaturatingCounterAdd(tally.Food, 0);
			tally.Roof = KingdomCatalogueRules.SaturatingCounterAdd(tally.Roof, 0);
			tally.Lift = KingdomCatalogueRules.SaturatingCounterAdd(tally.Lift, 0);
			tally.Works = KingdomCatalogueRules.SaturatingCounterAdd(tally.Works, 0);
			for (int i = 0; (Others != null) && i < Others.Count; i++)
			{
				if (!Others[i].Seen)
				{
					continue;
				}
				tally.Water = KingdomCatalogueRules.SaturatingCounterAdd(
					tally.Water, Others[i].Water);
				tally.Food = KingdomCatalogueRules.SaturatingCounterAdd(
					tally.Food, Others[i].Food);
				tally.Roof = KingdomCatalogueRules.SaturatingCounterAdd(
					tally.Roof, Others[i].Roof);
			}
			return tally;
		}

		/// <summary>
		/// The city's dedicated storage: this zone's, counted now, plus every other claimed
		/// zone's as last seen. The stage ladder reads storage
		/// (<see cref="StageWithHysteresis"/>), and a city whose casks are in the zone next door
		/// is not a camp because the founder walked in the other way.
		/// </summary>
		public static int CityStorage(int Here, IList<ZoneSighting> Others)
		{
			int total = (Here > 0) ? Here : 0;
			for (int i = 0; (Others != null) && i < Others.Count; i++)
			{
				if (Others[i].Seen && Others[i].StorageCapacity > 0)
				{
					total = KingdomCatalogueRules.SaturatingCounterAdd(
						total, Others[i].StorageCapacity);
				}
			}
			return total;
		}

		/// <summary>
		/// The oldest sighting folded into a city reading, or zero when every claimed zone was
		/// counted on this pass. This is what dates the knowledge: a number summed out of a
		/// sighting from forty days ago is forty days old, and the founder is owed that fact
		/// rather than a figure presented as today's.
		/// </summary>
		public static long OldestSighting(IList<ZoneSighting> Others)
		{
			long oldest = 0L;
			for (int i = 0; (Others != null) && i < Others.Count; i++)
			{
				if (!Others[i].Seen)
				{
					continue;
				}
				if (oldest <= 0L || Others[i].SeenTick < oldest)
				{
					oldest = Others[i].SeenTick;
				}
			}
			return oldest;
		}

		/// <summary>How many claimed zones are folded in out of an old sighting rather than
		/// counted from the ground this pass.</summary>
		public static int SightedZones(IList<ZoneSighting> Others)
		{
			int seen = 0;
			for (int i = 0; (Others != null) && i < Others.Count; i++)
			{
				if (Others[i].Seen)
				{
					seen++;
				}
			}
			return seen;
		}

		/// <summary>
		/// The clause that dates a city reading, or null when there is nothing to date &mdash; a
		/// one-zone city, or one whose every zone was walked today. STANDARDS 7b's other half:
		/// a number the founder cannot tell the age of is a number that will be believed at the
		/// wrong time.
		/// </summary>
		/// <param name="Zones">Claimed zones folded in out of a sighting.</param>
		/// <param name="Days">World days since the oldest of those sightings.</param>
		public static string SightingClause(int Zones, int Days)
		{
			if (Zones <= 0)
			{
				return null;
			}
			string ground = (Zones == 1) ? "one parasang" : (Zones + " parasangs");
			if (Days <= 0)
			{
				return "counting " + ground + " you walked today";
			}
			return "counting " + ground + " as " + ((Days == 1) ? "you last saw it a day ago" : ("you last saw " + ((Zones == 1) ? "it " : "them ") + Days + " days ago"));
		}

	}
}
