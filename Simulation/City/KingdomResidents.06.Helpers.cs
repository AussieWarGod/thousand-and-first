using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomResidents
	{
		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		/// <summary>
		/// One settler's row as the ground reads them: their name, where they walked in from, what
		/// they hold with, and the roof over them. An existing row keeps its arrival tick and its
		/// brink windows — those are facts about a person and not readings off a zone.
		/// </summary>
		private static KingdomResidentRow RowFor(KingdomCityState state, int id, GameObject settler,
			string zoneId, Dictionary<string, int> homes, long TimeTicks, string Origin = null,
			string Arrived = null)
		{
			int homeWorkId = 0;
			string plotId = settler.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (!string.IsNullOrEmpty(plotId) && homes != null)
			{
				homes.TryGetValue(plotId, out homeWorkId);
			}
			string origin = Origin ?? settler.GetStringProperty("KingdomOrigin") ?? "";
			int originCode = KingdomResidentRules.OriginCode(origin);
			int creedCode = KingdomCityRules.StableId(settler.GetStringProperty(KingdomCreed.CreedProperty));
			// Addendum 16's recorded fact, read off the person the same way their present creed is.
			// The row keeps the very string the settler carries, so the column costs a reference and
			// the heap nothing.
			string keptCreeds = settler.GetStringProperty(KingdomCreed.CreedPastProperty);
			// W3 stamps the post on the person: KingdomGrowth.AssignWork already knew which
			// settlers it crewed which work with (KingdomCrewRules.CrewOutcome.SettlerIndices) and
			// now writes it down, so the column is a fact rather than a placeholder. A settler the
			// works have no room for still reads zero, and their day shape still derives to the
			// hearth — which is what an unposted settler's day actually is, not a stand-in for one.
			int jobWorkId = KingdomStations.PostOf(settler);
			KingdomWorkKind jobKind = (KingdomWorkKind)settler.GetIntProperty(KingdomStations.PostKindProperty);
			KingdomDayShape dayShape = KingdomResidentRules.DayShapeFor(jobWorkId, jobKind);
			int index;
			KingdomResidentRow existing;
			if (state.TryResidentIndex(id, out index) && state.TryResident(index, out existing))
			{
				return existing
					.WithReading(NameOf(settler, existing.Name), origin, originCode, creedCode,
						homeWorkId, jobWorkId, 0, dayShape)
					.WithKeptCreeds(keptCreeds)
					.WithBoundZone(zoneId)
					.WithStanding(existing.Standing == KingdomResidentStanding.Expedition
						? KingdomResidentStanding.Expedition : KingdomResidentStanding.Resident,
						KingdomStandingCause.None);
			}
			return new KingdomResidentRow(id, NameOf(settler, null), originCode, creedCode,
				(TimeTicks > 0L) ? TimeTicks : 0L, homeWorkId, jobWorkId, 0, dayShape,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, zoneId,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0, keptCreeds, origin,
				string.IsNullOrEmpty(Arrived) ? DateAt(TimeTicks) : Arrived);
		}

		private static string DateAt(long Tick)
		{
			if (Tick <= 0L) return "";
			return Calendar.GetDay(Tick) + " of " + Calendar.GetMonth(Tick) + ", "
				+ Calendar.GetYear(Tick) + " AR";
		}

		/// <summary>
		/// What the pass can honestly say about a row bound to this zone whose body is not among
		/// its settlers.
		/// <para>
		/// The survey excludes a settler the founder has charmed or recruited (<c>IsPlayerLed</c>),
		/// so a body still standing here is exactly &sect;8.3's <c>Abroad</c>: on the roll, doing no
		/// work, and honestly reported. A body that is not in the zone at all has gone somewhere
		/// this pass cannot see, and the honest word for that is also <c>Abroad</c> — never Dead,
		/// which nobody witnessed.
		/// </para>
		/// <para>
		/// <b>The binding goes with the standing.</b> A row that stops being <c>Resident</c> stops
		/// having a bound body in this city's ground, which is the equation &sect;8.3 invariant 3
		/// states and <c>KingdomResidentRules.TryReconcile</c> checks.
		/// </para>
		/// </summary>
		private static KingdomResidentRow Witnessed(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, KingdomResidentRow row, long TimeTicks)
		{
			if (row.Standing == KingdomResidentStanding.Expedition)
			{
				// The expedition job owns this named absence, exact binding, and body marker.
				// Check-in must not release the evidence its later semantic lane needs to tell
				// dead/missing/followed apart or to forward-recover an interrupted dispatch.
				return row;
			}
			KingdomBodyWitness witness;
			if (Survey == null || !Survey.TryWitnessResident(row.ResidentId, out witness))
			{
				KingdomLog.Log("resident: duplicate body evidence for " + row.ResidentId
					+ " in " + (Z?.ZoneID ?? "-") + "; row was not changed");
				return row;
			}
			KingdomResidentRow next;
			KingdomCityFault fault;
			if (!KingdomResidentRules.TryTransition(row, witness, KingdomStandingCause.Unwitnessed, out next, out fault))
			{
				// A dead row is terminal and refusing to move it is the rule working, not a fault
				// worth a line in the founder's log.
				return row;
			}
			if (next.Standing != row.Standing && !KingdomResidentRules.Bindable(next.Standing))
			{
				Unbind(System, row.ResidentId, KingdomBindingKind.Resident, KingdomResidentRules.UnbindFor(next.Standing));
				KingdomLog.Log("resident: " + (row.Name ?? "-") + " (" + row.ResidentId + ") reads " + next.Standing
					+ " (" + next.Cause + ") in " + Z.ZoneID);
			}
			return next;
		}

		/// <summary>Every home plot standing in this zone, by the work row id of the object that
		/// carries it — so a row's home is the same id a work row is keyed on rather than a second
		/// identifier for the same building.</summary>
		private static Dictionary<string, int> HomeWorkIds(KingdomSurvey Survey)
		{
			Dictionary<string, int> homes = new Dictionary<string, int>();
			if (Survey == null)
			{
				return homes;
			}
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
			{
				GameObject item = Survey.PlotRoots[i];
				if (!KingdomUpgrade.IsFunctionallyBuilt(item)) continue;
				string plotId = item.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && !homes.ContainsKey(plotId))
				{
					homes[plotId] = KingdomCityRules.StableId(item.IDIfAssigned);
				}
			}
			return homes;
		}

		/// <summary>
		/// Whether this body is one the city would count as its own settler.
		/// <para>
		/// Exactly <c>KingdomSurvey</c>'s own filter, and that is the point: the lazy enrolment
		/// above and the roster read at check-in must agree about who is on the roll, or a
		/// merchant or a founding citizen would take one of the sixty rows the settlement is
		/// allowed. Both brink paths that can reach the lazy enrolment are already gated on the
		/// settler's roll name, which only an arrival carries, so nothing that used to record a
		/// brink stops being able to.
		/// </para>
		/// </summary>
		private static bool Enrollable(KingdomSystem System, GameObject Body)
		{
			return GameObject.Validate(Body)
				&& KingdomCitizenship.BelongsTo(System, Body)
				&& Body.GetIntProperty("KingdomBorn") == 1
				&& !Body.IsPlayer()
				&& !Body.IsPlayerLed();
		}

		private static string NameOf(GameObject settler, string fallback)
		{
			string named = settler.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(named))
			{
				return named;
			}
			return string.IsNullOrEmpty(fallback) ? (settler.BaseDisplayName ?? "") : fallback;
		}

		/// <summary>Where the bound object is, relative to the ground being asked about. A zone the
		/// manager cannot hand back is a zone on disk, and a body in one is frozen.</summary>
		private static KingdomBodyPresence PresenceOf(KingdomBinding binding, string zoneId)
		{
			if (string.IsNullOrEmpty(binding.ObjectId) || string.IsNullOrEmpty(binding.ZoneId))
			{
				return KingdomBodyPresence.Frozen;
			}
			// FindByID asks Qud's exact object-id index over already resident ground and never thaws a
			// zone. Absence therefore remains the durable Frozen verdict; no remote classification is
			// performed merely to answer check-before-mint.
			GameObject exact = FindExactBindingObject(binding);
			if (!GameObject.Validate(exact) || exact.CurrentZone == null) return KingdomBodyPresence.Frozen;
			return string.Equals(exact.CurrentZone.ZoneID, zoneId, StringComparison.Ordinal)
				? KingdomBodyPresence.Here
				: KingdomBodyPresence.Elsewhere;
		}

		/// <summary>Every book the realm holds, seat first. The registry is realm-scope precisely
		/// because a bound body can be in the other city's ground.</summary>
		private static IEnumerable<KingdomCityBook> Books(KingdomSystem System)
		{
			if (System.City != null)
			{
				yield return System.City;
			}
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				if (nonSeat[i]?.City != null) yield return nonSeat[i].City;
		}

		private static KingdomCityBook BookFor(KingdomSystem System, string zoneId)
		{
			if (string.IsNullOrEmpty(zoneId))
			{
				return null;
			}
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(zoneId))
			{
				return System.City;
			}
			KingdomSettlement nonSeat = System.FindNonSeatSettlementByZone(zoneId);
			if (nonSeat != null) return nonSeat.City;
			return null;
		}

		private static KingdomSettlement SettlementForBook(KingdomSystem System,
			KingdomCityBook Book)
		{
			if (System == null || Book == null || ReferenceEquals(Book, System.City)) return null;
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			KingdomSettlement found = null;
			for (int i = 0; i < nonSeat.Count; i++)
			{
				if (!ReferenceEquals(nonSeat[i]?.City, Book)) continue;
				if (found != null) return null;
				found = nonSeat[i];
			}
			return found;
		}

		private static bool TryTable(KingdomSystem System, out KingdomBindingTable table)
		{
			table = null;
			if (System == null || System.Bindings == null)
			{
				return false;
			}
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault))
			{
				Refuse("registry", fault);
				return false;
			}
			return true;
		}

		private static bool Publish(KingdomSystem System, KingdomBindingTable table, string step)
		{
			KingdomCityFault fault;
			if (!System.Bindings.TryPublish(table, out fault))
			{
				Refuse(step, fault);
				return false;
			}
			return true;
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("binding: " + step + " refused (" + fault + "); the registry is unchanged");
		}
	}
}
