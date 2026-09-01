using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		/// What this ground's works make in a day, as the model's rate.
		/// <para>
		/// W6, LIVING-CITY-ARCHITECTURE &sect;7.4. The figure is <c>KingdomSubsidence.Supports</c>'s
		/// own — the same tally the level is derived from and the same one the settlement pass used
		/// to credit off its settlement-wide stamp — so the model and the ladder can never disagree
		/// about what a reservoir is worth. Measured at the pass that reads the ground and stamped
		/// on the row, because a rate is a fact about a zone's works and a zone's works are only
		/// legible while somebody is standing on them.
		/// </para>
		/// </summary>
		private static int WaterMadePerDay(KingdomSurvey Survey)
		{
			return (Survey == null) ? 0 : KingdomSubsidence.OrdinarySupports(Survey).Water;
		}

		/// <summary>
		/// Food has no city-rate credit. Fields harvest physical crops and mills transform physical
		/// inputs, so away-time model production must remain zero.
		/// </summary>
		private static int FoodMadePerDay(KingdomSurvey Survey)
		{
			return 0;
		}

		/// <summary>
		/// The work rows, rebuilt from the ground under the founder's feet. A work's row carries
		/// state the engine cannot carry for it and nothing else (&sect;1.2(c)); appearance, name,
		/// tile and contents stay on the object.
		/// </summary>
		private static KingdomCityState ReadWorks(KingdomCityState state, Zone Z, KingdomSurvey Survey)
		{
			List<KingdomWorkRow> kept = new List<KingdomWorkRow>();
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (state.TryWork(i, out row) && !string.Equals(row.ZoneId, Z.ZoneID, StringComparison.Ordinal))
				{
					kept.Add(row);
				}
			}
			for (int i = 0; i < Survey.Built.Count && kept.Count < KingdomCityState.MaxWorks; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				Cell at = work.CurrentCell;
				int workId = KingdomCityRules.StableId(work.IDIfAssigned);
				kept.Add(new KingdomWorkRow(
					// The object's own persistent id, folded by a written-out hash rather than the
					// runtime's: a runtime hash is not stable across processes, and a work id that
					// changes when the game restarts is not an id.
					workId,
					Z.ZoneID,
					(short)((at != null) ? at.X : 0),
					(short)((at != null) ? at.Y : 0),
					work.Blueprint ?? "",
					100 - KingdomWear.WearOf(work),
					// Exact resident rows, refreshed before this method, are the only live crew
					// authority. Bound ground is part of the match so stable-id collisions between
					// zones cannot lend a work somebody else's hands.
					KingdomResidentRules.CrewAssigned(state, Z.ZoneID, workId),
					(The.Game != null) ? The.Game.TimeTicks : 0L,
					RunStateOf(work)));
			}
			KingdomCityState rebuilt;
			KingdomCityFault fault;
			if (!Rebuild(state, kept, out rebuilt, out fault))
			{
				Refuse("works", fault);
				return state;
			}
			return rebuilt;
		}

		/// <summary>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9's audit, in both directions: after an attended pass
		/// of a fully-visited city, model total == ground total, per stock kind. A mismatch is
		/// named rather than repaired.
		/// </summary>
		public static string AuditLine(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null || Z == null || Survey == null)
			{
				return null;
			}
			book.Normalize();
			int index;
			if (!book.TryZoneRow(Z.ZoneID, out index))
			{
				return null;
			}
			KingdomCatchUpCounter counter = CityCounter(book);
			return KingdomCityRules.AuditNote(
				book.ZoneWaterLevels[index], book.ZoneOwedWater[index], Survey.StoredWater,
				book.ZoneFoodLevels[index], book.ZoneOwedFood[index], Survey.FoodAvailable,
				counter.OwedThirds);
		}

		/// <summary>
		/// The same audit asked of a model that has NOT been trued against this ground yet.
		/// <para>
		/// The published-book reader above is used at the foot of a pass, where the reconcile has
		/// already re-derived the debt from the reading and <c>level - owed == ground</c> holds by
		/// construction. That is a proof the reconcile ran. This one is the proof the ground and
		/// the book agreed in the first place, which is the only version of the line a founder or a
		/// tester learns anything from.
		/// </para>
		/// </summary>
		private static string AuditLine(KingdomCityState state, Zone Z, KingdomSurvey Survey)
		{
			int index;
			KingdomZoneRow row;
			if (state == null || Z == null || Survey == null || !IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row))
			{
				return null;
			}
			return KingdomCityRules.AuditNote(
				row.Stocks.Water.Level, row.OwedWater, Survey.StoredWater,
				row.Stocks.Food.Level, row.OwedFood, Survey.FoodAvailable,
				KingdomCityRules.CityCounter(state).OwedThirds);
		}

		private static void Audit(KingdomSystem System, Zone Z, KingdomSurvey Survey, string step)
		{
			string line = AuditLine(System, Z, Survey);
			if (line != null)
			{
				KingdomLog.Log("city: " + step + " " + line);
			}
			// Invariant I3 beside invariant I1, and asserted rather than inferred: a registry that
			// has started answering one key with two bodies says so on the pass it happens rather
			// than on the pass a settler is finally seen twice.
			string bindings = KingdomResidents.AuditLine(System);
			if (bindings != null)
			{
				KingdomLog.Log("city: " + step + " " + bindings);
			}
		}

		/// <summary>
		/// Cheap city-wide debt-presence marker for thaw/prefetch. Numeric performance receipts use
		/// <see cref="GroundDemandThirds"/> after a real survey; this model-only figure cannot know
		/// how many physical containers a quantity spans.
		/// </summary>
		public static int OwedThirds(KingdomSystem System)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			return (book == null) ? 0 : CityCounter(book).OwedThirds;
		}

	}
}
