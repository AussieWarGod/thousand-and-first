using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		private static bool RefuseResidenceObservation(Zone Ground,
			out KingdomLodgingRules.UnhousedReason Reason, out string Hash)
		{
			Reason = KingdomLodgingRules.UnhousedReason.NoRoofAtAll;
			Hash = ArrivalObservationHash(writer =>
			{
				WriteObservationString(writer, "resident-home-authority-unavailable");
				WriteObservationString(writer, Ground.ZoneID);
			});
			return false;
		}

		internal static string LocalHomePlot(Zone Ground, GameObject Body)
		{
			if (Ground == null || !GameObject.Validate(Body)) return null;
			if (KingdomResidents.TryResidence(The.Game?.GetSystem<KingdomSystem>(), Body,
				out KingdomResidentRow row, out KingdomResidence home))
				return home.HasHome && home.ZoneId == Ground.ZoneID ? home.PlotId : null;
			if (row.HomeWorkId != 0)
			{
				KingdomCityBook book = KingdomResidents.ResidenceBook(The.Game?.GetSystem<KingdomSystem>(), Ground);
				if (book == null || !book.TryReadExact(out KingdomCityState state, out _)) return null;
				for (int i = 0; i < state.WorkCount; i++)
					if (state.TryWork(i, out KingdomWorkRow work) && work.WorkId == row.HomeWorkId)
						return work.ZoneId == Ground.ZoneID ? Body.GetStringProperty(HomePlotIdProperty) : null;
				return null;
			}
			return Body.GetStringProperty(HomePlotIdProperty);
		}

		private static bool HasRemoteHome(Zone Ground, GameObject Body)
			=> Ground != null && KingdomResidents.TryResidence(The.Game?.GetSystem<KingdomSystem>(), Body,
				out _, out KingdomResidence home) && home.HasHome && home.ZoneId != Ground.ZoneID;

		private readonly struct HouseholdMember
		{
			internal readonly GameObject Body;
			internal readonly int ResidentId;
			internal readonly string BodyId;
			internal readonly bool Known;
			internal readonly KingdomResidence Facts;
			internal HouseholdMember(int id, GameObject body, bool known, KingdomResidence facts)
			{ ResidentId = id; Body = body; BodyId = body?.IDIfAssigned; Known = known; Facts = facts; }
			internal bool Is(GameObject body) => ResidentId > 0
				? ResidentId == KingdomResidents.IdOf(body)
				: !string.IsNullOrEmpty(BodyId) && BodyId == body?.IDIfAssigned;
		}

		private static HouseholdMember Member(GameObject Body, string Plot, string Bed = "")
		{
			bool known = KingdomResidents.TryObserveHousehold(Body, Body.CurrentZone.ZoneID, Plot, Bed, out string wire)
				&& KingdomResidenceRules.TryDecode(wire, out _);
			KingdomResidence facts = default;
			if (known) KingdomResidenceRules.TryDecode(wire, out facts);
			return new HouseholdMember(KingdomResidents.IdOf(Body), Body, known, facts);
		}

		// A reservation is a resident identity plus observed household facts. An unloaded or
		// absent owner never becomes a fabricated actor or a free bed.
		private static Dictionary<string, List<HouseholdMember>> ReadHouseholds(Zone Ground,
			List<GameObject> Homes, out List<GameObject> Unassigned)
		{
			Unassigned = new List<GameObject>();
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomCityBook book = KingdomResidents.ResidenceBook(system, Ground);
			if (book == null || !book.HasValidSubsidenceStorage()
				|| !book.TryReadExact(out KingdomCityState state, out _)) return null;
			var standing = new Dictionary<string, GameObject>(StringComparer.Ordinal);
			foreach (GameObject home in Homes)
			{
				string plot = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (string.IsNullOrEmpty(plot) || IsCondemned(home)) continue;
				if (standing.ContainsKey(plot)) return null;
				standing.Add(plot, home);
			}
			List<GameObject> locals = ResidentsIn(Ground);
			var bodies = new Dictionary<int, GameObject>();
			foreach (GameObject body in locals)
			{
				int id = KingdomResidents.IdOf(body);
				if (id <= 0) continue;
				if (bodies.ContainsKey(id)) return null;
				bodies.Add(id, body);
			}
			var result = new Dictionary<string, List<HouseholdMember>>(StringComparer.Ordinal);
			var unavailable = new HashSet<int>();
			var observed = new HashSet<int>();
			for (int i = 0; i < state.ResidentCount; i++)
			{
				state.TryResident(i, out KingdomResidentRow row);
				if (!KingdomResidentRules.OnTheRoll(row)) continue;
				bool known = KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence facts);
				string plot = null;
				if (known)
				{
					observed.Add(row.ResidentId);
					if (facts.HasHome && facts.ZoneId != Ground.ZoneID) unavailable.Add(row.ResidentId);
					if (facts.HasHome && facts.ZoneId == Ground.ZoneID && standing.ContainsKey(facts.PlotId))
						plot = facts.PlotId;
				}
				else if (row.HomeWorkId != 0)
				{
					foreach (var home in standing)
						if (KingdomCityRules.StableId(home.Value.IDIfAssigned) == row.HomeWorkId)
						{
							if (plot != null) return null;
							plot = home.Key;
						}
					if (plot == null) unavailable.Add(row.ResidentId);
				}
				if (plot == null) continue;
				HouseholdMember member = bodies.TryGetValue(row.ResidentId, out GameObject body)
					? Member(body, plot, known ? facts.BedId : "") : new HouseholdMember(row.ResidentId, null, known, facts);
				AddMember(result, plot, member);
			}
			foreach (GameObject body in locals)
			{
				int id = KingdomResidents.IdOf(body);
				if (unavailable.Contains(id) || AssignedPlot(result, body) != null) continue;
				string plot = body.GetStringProperty(HomePlotIdProperty);
				if (!observed.Contains(id) && !string.IsNullOrEmpty(plot) && standing.ContainsKey(plot))
					AddOccupant(result, plot, body);
				else Unassigned.Add(body);
			}
			return result;
		}

		private static string AssignedPlot(Dictionary<string, List<HouseholdMember>> Occupancy, GameObject Body)
		{
			foreach (var home in Occupancy)
				foreach (HouseholdMember member in home.Value) if (member.Is(Body)) return home.Key;
			return null;
		}

		private static void AddMember(Dictionary<string, List<HouseholdMember>> Occupancy,
			string Plot, HouseholdMember Member)
		{
			if (!Occupancy.TryGetValue(Plot, out List<HouseholdMember> members))
			{ members = new List<HouseholdMember>(); Occupancy.Add(Plot, members); }
			members.Add(Member);
		}
	}
}
