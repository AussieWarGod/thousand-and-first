using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomSubsidence
	{
		/// <summary>
		/// The same tally, with its lifting half scoped to what each work actually reaches
		/// (Addendum 6). The <b>only</b> difference from <see cref="Supports"/> is
		/// <c>SupportTally.Lift</c>: water, food and roofs are drawn and carried, so they stay the
		/// citywide pools they have always been, and faith, order, learning, luxury and craft
		/// shade the people in reach of the work giving them.
		/// <para>
		/// Denominated in roofs, which is the level's own currency for a person: a work's lift
		/// lands in proportion to the settlement's housing it covers
		/// (<c>KingdomReachRules.Landed</c>). A shrine standing among the houses is worth its
		/// whole amount; the same shrine out past the fields is worth what it touches; and a
		/// wayside statue that reaches no home lands nothing on the level while still shading the
		/// ground it stands on. That is what makes the temple quarter different ground from the
		/// tanners' rather than a second number nobody can see.
		/// </para>
		/// <para>
		/// The great works of the realm's other claimed zones arrive whole, out of the record
		/// their own attended passes wrote (<c>KingdomReach.CityShadeExcept</c>), because a city
		/// band covers every cell of the city by definition. This zone's own record is deliberately
		/// skipped: what stands here has just been counted from the ground.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null falls back to the unscoped tally rather than
		/// dropping every lift &mdash; a caller with no realm to measure against is asking a
		/// different question, not asking this one badly.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Survey">The pass's survey.</param>
		public static KingdomCatalogueRules.SupportTally ScopedSupports(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomCatalogueRules.SupportTally tally = Supports(Survey);
			if (System == null || Z == null || Survey == null)
			{
				return tally;
			}
			List<Cell> homes = new List<Cell>();
			List<int> housed = new List<int>();
			List<GameObject> lifters = new List<GameObject>();
			List<int> lifted = new List<int>();
			int roofs = 0;
			int trades = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				string key = KingdomUpgrade.DesignKeyOf(work);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				List<KindAmount> carries;
				KingdomCatalogueRules.TryParseTally(entry.Carries, out carries, out _);
				carries = KingdomHostedArcology.HostedCarries(work, carries,
					Survey.StoredWater > 0);
				int effectiveness = KingdomWear.EffectivenessOf(work);
				int lift = 0;
				for (int c = 0; c < carries.Count; c++)
				{
					// The kinds come out of TryParseTally already folded, so the comparison
					// against the catalogue's own constant is the whole test.
					if (carries[c].Kind == KingdomCatalogueRules.SupportRoof)
					{
						int people = KingdomCatalogueRules.Carried(carries[c].Amount, effectiveness);
						Cell cell = work.CurrentCell;
						if (people > 0 && cell != null)
						{
							homes.Add(cell);
							housed.Add(people);
							roofs += people;
						}
						continue;
					}
					if (KingdomCatalogueRules.IsBindingSupport(carries[c].Kind))
					{
						continue;
					}
					lift += KingdomReachRules.Scaled(carries[c].Amount, effectiveness);
				}
				if (lift > 0)
				{
					lifters.Add(work);
					lifted.Add(lift);
				}
				// A household's trade is not a work with ground of its own, so it has no band to
				// be scoped by: what it makes goes to the settlement, and its whole ceiling is
				// KingdomYardRules.MaxShadePerWork. Carried straight across, exactly as Supports
				// folded it, so the scoped tally does not quietly lose the yard.
				trades += LiftOf(YardShadesOf(work), effectiveness);
			}
			int scoped = trades;
			for (int i = 0; i < lifters.Count; i++)
			{
				int reached = 0;
				for (int h = 0; h < homes.Count; h++)
				{
					if (KingdomReach.ReachesCell(System, Z, lifters[i], Z, homes[h].X, homes[h].Y))
					{
						reached += housed[h];
					}
				}
				scoped += KingdomReachRules.Landed(lifted[i], reached, roofs);
			}
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				int city = KingdomReach.CityShadeExcept(System, KingdomReachRules.LiftOrder[i], Z.ZoneID);
				if (city > 0)
				{
					scoped += city;
				}
			}
			tally.Lift = scoped;
			return tally;
		}

		// --- The city's own record, one zone at a time --------------------------------------

		/// <summary>
		/// Writes down what this zone was holding, on the pass that stood in it. Rewritten from
		/// the ground every time, including down to zero: a reservoir that was struck stops
		/// counting toward the city the pass the founder sees the empty plot, and never before.
		/// <para>
		/// The discipline is unchanged; where it is written is not. This used to be five
		/// <c>r_TAF_Supports_&lt;zoneID&gt;_*</c> game-state ints, which were the right answer for
		/// five ints that had to be readable without loading a zone and the wrong answer for a
		/// hundred typed rows (LIVING-CITY-ARCHITECTURE &sect;1.3). It is now one row of the
		/// settlement's own city book, and every number downstream is the same number.
		/// </para>
		/// </summary>
		/// <param name="System">The seated settlement, whose book holds the row.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Supports">What was counted here, lifts ignored &mdash; only the binding
		/// half is a citywide pool.</param>
		/// <param name="StorageCapacity">Dedicated storage counted here.</param>
		/// <param name="TimeTicks">Now, which is what dates the sighting.</param>
		public static void RecordZone(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCatalogueRules.SupportTally Supports, int StorageCapacity, long TimeTicks)
		{
			// W7 repair: the RATES are no longer handed over. `Supports.Food` is the raw tally and
			// the model's food carry is KingdomGrowth.FoodMadePerDay, which subtracts the sown
			// fields and the mills because those two deliver physically; passing the raw figure
			// here was the one writer that disagreed with the other two. The survey goes across
			// instead and KingdomCity reads both rates off it through the same expressions every
			// other writer uses.
			Simulation.City.KingdomCity.RecordSupports(System, Z, Survey, Supports.Roof, StorageCapacity, TimeTicks);
		}

		/// <summary>The stamp a sighting tick is dated in: whole DAYS, not ticks, because a day is
		/// the granularity everything downstream reads (<c>KingdomRules.ElapsedDays</c>) and the
		/// staleness clause is written in days. Clamped: a game that somehow outruns it stops
		/// ageing rather than wrapping negative and reading as the future.</summary>
		public static int SeenStamp(long TimeTicks)
		{
			if (TimeTicks <= 0L)
			{
				return 0;
			}
			long days = TimeTicks / KingdomRules.TicksPerDay;
			return (days >= int.MaxValue) ? int.MaxValue : ((days < 1L) ? 1 : (int)days);
		}

		/// <summary>Every claimed zone of the seated city EXCEPT the one the pass is in, as each
		/// was last seen. The exclusion is the whole point: this zone has just been counted from
		/// the ground, and counting it twice would double its cisterns.</summary>
		public static List<KingdomSubsidenceRules.ZoneSighting> OtherZones(KingdomSystem System, Zone Z)
		{
			return Simulation.City.KingdomCity.OtherZones(System, Z);
		}

		/// <summary>
		/// The whole city's dedicated storage: this zone's, counted now, plus every other claimed
		/// zone's as last seen. The stage ladder is read against storage
		/// (<c>KingdomRules.StageFor</c>), so a city whose casks stand in the zone next door must
		/// be measured against all of them or it demotes itself the moment the founder walks in
		/// through the wrong side.
		/// </summary>
		/// <param name="System">The seated city.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="Here">Storage counted in this zone this pass
		/// (<c>KingdomSurvey.StorageCapacity</c>).</param>
		public static int CityStorageCapacity(KingdomSystem System, Zone Z, int Here)
		{
			return KingdomSubsidenceRules.CityStorage(Here, OtherZones(System, Z));
		}

		/// <summary>
		/// The clause that dates a city reading for the founder, or null when the reading is
		/// wholly this pass's own. The staleness doctrine said out loud: a two-zone city's level
		/// is partly a memory, and the founder is told how old the memory is.
		/// </summary>
		/// <param name="System">The seated city.</param>
		/// <param name="Z">The zone the pass is in.</param>
		/// <param name="TimeTicks">Now.</param>
		public static string SightingClause(KingdomSystem System, Zone Z, long TimeTicks)
		{
			List<KingdomSubsidenceRules.ZoneSighting> others = OtherZones(System, Z);
			long oldest = KingdomSubsidenceRules.OldestSighting(others);
			int days = (oldest > 0L) ? KingdomRules.ElapsedDays(TimeTicks - oldest) : 0;
			return KingdomSubsidenceRules.SightingClause(KingdomSubsidenceRules.SightedZones(others), days);
		}

	}
}
