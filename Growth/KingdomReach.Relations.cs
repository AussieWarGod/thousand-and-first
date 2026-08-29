using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomReach
	{
		/// <summary>
		/// How well one settler would head a work of this purpose, read off the attributes the
		/// engine already gives every creature. Nothing is stored on them and nothing is trained:
		/// a settler another mod shipped is scored by exactly the same six numbers.
		/// </summary>
		public static int FitnessOf(string Category, GameObject Settler)
		{
			if (Settler == null)
			{
				return 0;
			}
			return KingdomReachRules.SeatFitness(Category,
				Settler.GetStatValue("Strength"),
				Settler.GetStatValue("Agility"),
				Settler.GetStatValue("Toughness"),
				Settler.GetStatValue("Intelligence"),
				Settler.GetStatValue("Willpower"),
				Settler.GetStatValue("Ego"));
		}

		private static int Tenure(KingdomSystem System, string Name)
		{
			if (System == null || string.IsNullOrEmpty(Name))
			{
				return int.MaxValue;
			}
			List<Simulation.City.KingdomResidentRow> rows =
				Simulation.City.KingdomResidents.RollRows(System);
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].Name, Name, StringComparison.Ordinal)) return i;
			return int.MaxValue;
		}

		private static string NameOf(GameObject Settler)
		{
			return (Settler == null) ? null : Settler.GetStringProperty("KingdomName");
		}

		// --- What reaches a place --------------------------------------------------------------

		/// <summary>
		/// Whether a standing work reaches a resident. The question the shrine, the scriptorium
		/// and every later channel ask; the shrine's "quarter" is this and nothing else.
		/// </summary>
		/// <param name="System">The realm, for what ground it holds.</param>
		/// <param name="WorkZone">The zone the work stands in.</param>
		/// <param name="Work">The standing work. Null reaches nothing.</param>
		/// <param name="At">The resident. Null, or one standing nowhere, is not reached.</param>
		public static bool Reaches(KingdomSystem System, Zone WorkZone, GameObject Work, GameObject At)
		{
			Cell cell = (At == null) ? null : At.CurrentCell;
			return cell != null && ReachesCell(System, WorkZone, Work, cell.ParentZone, cell.X, cell.Y);
		}

		/// <summary>Whether a standing work reaches one cell of one zone.</summary>
		/// <param name="System">The realm, for what ground it holds.</param>
		/// <param name="WorkZone">The zone the work stands in.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="AtZone">The zone the place is in.</param>
		/// <param name="X">The place.</param>
		/// <param name="Y">The place.</param>
		public static bool ReachesCell(KingdomSystem System, Zone WorkZone, GameObject Work, Zone AtZone, int X, int Y)
		{
			if (Work == null || WorkZone == null || AtZone == null)
			{
				return false;
			}
			return KingdomReachRules.Covers(EffectiveBandOf(Work), RelationOf(System, WorkZone, Work, AtZone, X, Y));
		}

		private static ReachRelation RelationOf(KingdomSystem System, Zone WorkZone, GameObject Work, Zone AtZone, int X, int Y)
		{
			bool sameZone = WorkZone.ZoneID == AtZone.ZoneID;
			bool sameRealm = Holds(System, AtZone.ZoneID);
			bool sameCity = SameCity(System, WorkZone.ZoneID, AtZone.ZoneID);
			bool onFootprint = false;
			bool inQuarter = false;
			if (sameZone)
			{
				KingdomPlotRules.PlotRect footprint;
				onFootprint = KingdomPlots.TryReadFootprint(Work, out footprint) && footprint.Contains(X, Y);
				if (!onFootprint)
				{
					Cell cell = Work.CurrentCell;
					inQuarter = cell != null && KingdomReachRules.InQuarter(MarksOf(WorkZone),
						cell.X, cell.Y, X, Y, KingdomReachRules.QuarterLinkCells, QuarterRadiusOf(Work));
				}
			}
			return KingdomReachRules.RelationAt(sameRealm, sameCity, sameZone, inQuarter, onFootprint);
		}

		// The zone's marks, read once per zone per tick rather than once per settler per shrine.
		// A quarter is measured from what is standing, and nothing is raised or struck between two
		// questions asked on the same tick, so the only thing this drops is a repeated full-zone
		// walk in a loop that asks the same question about twenty people.
		private static List<KingdomLayoutRules.LayoutMark> _marks;

		private static string _marksZone;

		private static long _marksTick = -1L;

		private static List<KingdomLayoutRules.LayoutMark> MarksOf(Zone Z)
		{
			long tick = (The.Game == null) ? 0L : The.Game.TimeTicks;
			if (_marks != null && _marksZone == Z.ZoneID && _marksTick == tick)
			{
				return _marks;
			}
			_marks = KingdomLayout.ReadMarks(Z);
			_marksZone = Z.ZoneID;
			_marksTick = tick;
			return _marks;
		}

		/// <summary>Whether the realm holds this ground at all, either city's.</summary>
		public static bool Holds(KingdomSystem System, string ZoneID)
		{
			if (System == null || string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			return System.OwnedZone(ZoneID);
		}

		private static bool SameCity(KingdomSystem System, string WorkZoneID, string AtZoneID)
		{
			if (System == null)
			{
				return false;
			}
			string work = System.SettlementIdForOwnedZone(WorkZoneID);
			string at = System.SettlementIdForOwnedZone(AtZoneID);
			return !string.IsNullOrEmpty(work) && string.Equals(work, at,
				StringComparison.Ordinal);
		}

	}
}
