using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		// ==================================================================================
		// The rows
		// ==================================================================================

		/// <summary>
		/// The roster, rebuilt from the ground under the founder's feet, and the bindings that go
		/// with it.
		/// <para>
		/// Every settler standing in this zone gets an id and a row; every row the book already had
		/// bound to this zone and NOT found on the ground is witnessed and transitioned, except an
		/// expedition row whose realm job owns that named absence and exact binding. Rows bound
		/// to the city's other zones are carried untouched, because this pass has no honest word
		/// about ground it is not standing in — that is the sighting doctrine, unchanged.
		/// </para>
		/// <para>The book is the roll authority. Legacy parallel lists are rewritten from the rows
		/// after publication and are never consulted here except at the one load migration boundary.</para>
		/// </summary>
		internal static KingdomCityState ReadRoster(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, long TimeTicks)
		{
			if (System == null || Z == null || Survey == null || state == null)
			{
				return state;
			}
			Dictionary<string, int> homes = HomeWorkIds(Survey);
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			HashSet<int> onTheGround = new HashSet<int>();
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				int id = IdOf(settler);
				if (id == 0)
				{
					id = ClaimIdFor(state, settler, onTheGround);
					if (id > 0)
					{
						settler.SetIntProperty(ResidentIdProperty, id);
						if (id > System.ResidentCounter) System.ResidentCounter = id;
					}
					else id = EnsureId(System, settler);
				}
				if (id == 0 || onTheGround.Contains(id))
				{
					continue;
				}
				// A Resident row without its matching binding violates the city model. Refuse
				// this reading when the registry cannot accept it; never publish half of the pair.
				if (!Bind(System, id, KingdomBindingKind.Resident, Z.ZoneID, settler, TimeTicks))
				{
					continue;
				}
				onTheGround.Add(id);
				rows.Add(RowFor(state, id, settler, Z.ZoneID, homes, TimeTicks));
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || onTheGround.Contains(row.ResidentId))
				{
					continue;
				}
				if (!string.Equals(row.BoundZoneId, Z.ZoneID, StringComparison.Ordinal))
				{
					rows.Add(row);
					continue;
				}
				rows.Add(Witnessed(System, Z, Survey, row, TimeTicks));
			}
			if (rows.Count > KingdomCityState.MaxResidents)
			{
				// The cap is KingdomRules.MaxPopulation and the ground cannot hold more people than
				// the settlement is allowed; a book that came back over it is trimmed by Normalize
				// rather than by inventing a rule here about who is dropped.
				rows.RemoveRange(KingdomCityState.MaxResidents, rows.Count - KingdomCityState.MaxResidents);
			}
			KingdomCityState written;
			KingdomCityFault fault;
			if (!state.TryWithResidents(rows.ToArray(), out written, out fault))
			{
				Refuse("roster", fault);
				return state;
			}
			return written;
		}

		/// <summary>Adopts one body into an unresolved migrated claim without minting a second
		/// resident. Exact name and origin disambiguate same-name claims; a bound or already-seen id
		/// cannot be adopted twice.</summary>
		private static int ClaimIdFor(KingdomCityState State, GameObject Body,
			HashSet<int> AlreadySeen)
		{
			if (State == null || !GameObject.Validate(Body)) return 0;
			string name = NameOf(Body, null);
			string origin = Body.GetStringProperty("KingdomOrigin") ?? "";
			for (int i = 0; i < State.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!State.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Abroad
					|| row.ArrivedTick != 0L || AlreadySeen.Contains(row.ResidentId)
					|| !string.Equals(row.Name, name, StringComparison.Ordinal)
					|| !string.Equals(row.Origin ?? "", origin, StringComparison.Ordinal)) continue;
				return row.ResidentId;
			}
			return 0;
		}

		/// <summary>
		/// The book that holds this body's row, and where in it. The realm is walked seat first,
		/// because the founder is standing in the seated city and that is where nearly every
		/// question about a settler is asked from.
		/// </summary>
		public static bool TryLocate(KingdomSystem System, GameObject Body, out KingdomCityBook book, out int residentId)
		{
			book = null;
			residentId = IdOf(Body);
			if (System == null || residentId == 0)
			{
				return false;
			}
			foreach (KingdomCityBook candidate in Books(System))
			{
				int index;
				if (candidate.TryResidentRow(residentId, out index))
				{
					book = candidate;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The book that holds this body's row, minting the id and the row if it has none.
		/// <para>
		/// The lazy half of the roster read, and it earns its place: a settler who arrives during
		/// the growth step is enrolled, housed and can reach a brink several steps before the next
		/// check-in would have given them a row. Without this their first warning would have
		/// nowhere to live, and the brink storage swap would have silently changed behaviour on the
		/// one settler most likely to have a brink.
		/// </para>
		/// </summary>
		public static bool TryEnsureRow(KingdomSystem System, GameObject Body, out KingdomCityBook book, out int residentId)
		{
			long tick = (The.Game != null) ? The.Game.TimeTicks : 0L;
			return TryEnsureRow(System, Body, Body?.GetStringProperty("KingdomOrigin"), null,
				tick, out book, out residentId);
		}

		/// <summary>Enrols one accepted body with the exact provenance/date frozen by its owning
		/// transaction. The tick is the sole clock; <paramref name="Arrived"/> is presentation
		/// evidence and may be a legacy label.</summary>
		internal static bool TryEnsureRow(KingdomSystem System, GameObject Body, string Origin,
			string Arrived, long ArrivedTick, out KingdomCityBook book, out int residentId)
		{
			if (TryLocate(System, Body, out book, out residentId))
			{
				return true;
			}
			book = null;
			residentId = 0;
			if (System == null || System.City == null || !Enrollable(System, Body))
			{
				return false;
			}
			int id = EnsureId(System, Body);
			if (id == 0)
			{
				return false;
			}
			Zone zone = Body.CurrentZone;
			string zoneId = (zone != null) ? zone.ZoneID : null;
			KingdomCityBook seated = BookFor(System, zoneId) ?? System.City;
			KingdomCityState state;
			KingdomCityFault fault;
			if (!seated.TryRead(out state, out fault))
			{
				Refuse("enrol", fault);
				return false;
			}
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow existing;
				if (state.TryResident(i, out existing))
				{
					rows.Add(existing);
				}
			}
			if (rows.Count >= KingdomCityState.MaxResidents)
			{
				Refuse("enrol", KingdomCityFault.RowCapExceeded);
				return false;
			}
			long tick = ArrivedTick > 0L ? ArrivedTick
				: ((The.Game != null) ? The.Game.TimeTicks : 0L);
			rows.Add(RowFor(state, id, Body, zoneId,
				HomeWorkIds(zone == null ? null : KingdomSurvey.Take(zone, System)),
				tick, Origin, Arrived));
			KingdomCityState written;
			if (!state.TryWithResidents(rows.ToArray(), out written, out fault) || !seated.TryPublish(written, out fault))
			{
				Refuse("enrol", fault);
				return false;
			}
			if (!Bind(System, id, KingdomBindingKind.Resident, zoneId, Body, tick))
			{
				SafePublish(seated, state, "enrol rollback");
				Body.RemoveIntProperty(ResidentIdProperty);
				return false;
			}
			book = seated;
			residentId = id;
			ProjectCompatibility(System);
			return true;
		}
	}
}
