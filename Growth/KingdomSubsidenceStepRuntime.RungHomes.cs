using System;
using System.Collections.Generic;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool CaptureRungRoofs(KingdomSystem system, KingdomCityBook city,
			KingdomSurvey survey, string plot, int homeWorkId, List<KingdomSubsidenceRungRoof> roofs,
			Dictionary<string, GameObject> residents)
		{
			if (survey?.Ground == null || !city.TryReadExact(out KingdomCityState state, out _)
				|| system.Bindings == null || !system.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)) return false;
			var known = new HashSet<int>();
			for (int i = 0; i < state.ResidentCount; i++)
			{
				if (!state.TryResident(i, out KingdomResidentRow row)) return false;
				if (!KingdomResidenceRules.TryDecode(row.Residence, out KingdomResidence home)) continue;
				known.Add(row.ResidentId);
				if (row.Standing != KingdomResidentStanding.Resident || string.IsNullOrEmpty(row.Name)
					|| !KingdomResidenceRules.SameHome(home, survey.Ground.ZoneID, plot)) continue;
				if (row.HomeWorkId != homeWorkId || roofs.Count >= KingdomSubsidenceRungRules.MaxRoofs
					|| !bindings.TryGet(row.ResidentId, KingdomBindingKind.Resident, out KingdomBinding binding)
					|| string.IsNullOrEmpty(binding.ObjectId) || binding.ZoneId != row.BoundZoneId
					|| !city.TryCaptureSubsidenceRoof(row.ResidentId, out var roof)) return false;
				if (row.BoundZoneId == survey.Ground.ZoneID)
				{
					GameObject body = survey.FindBoundBody(binding.ObjectId, KingdomBindingKind.Resident);
					if (!CaptureRoofBody(system, survey, body, row.ResidentId, plot, residents)) return false;
				}
				roofs.Add(new KingdomSubsidenceRungRoof(row.ResidentId, binding.ObjectId, roof.RoofStanding,
					roof.Reached, roof.Warned, KingdomSubsidenceEffectPhase.Prepared, home.ZoneId));
			}
			foreach (GameObject body in survey.CitizenBodies)
			{
				int id = KingdomResidents.IdOf(body);
				if (known.Contains(id) || !GameObject.Validate(body) || !KingdomCitizenship.BelongsTo(system, body)
					|| body.GetStringProperty(KingdomLodging.HomePlotIdProperty) != plot
					|| string.IsNullOrEmpty(body.GetStringProperty("KingdomName"))) continue;
				if (roofs.Count >= KingdomSubsidenceRungRules.MaxRoofs
					|| !city.TryCaptureSubsidenceRoof(id, out var row) || row.HomeWorkId != homeWorkId
					|| !CaptureRoofBody(system, survey, body, id, plot, residents)) return false;
				roofs.Add(new KingdomSubsidenceRungRoof(id, body.IDIfAssigned, row.RoofStanding,
					row.Reached, row.Warned, KingdomSubsidenceEffectPhase.Prepared));
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				state.TryResident(i, out var row);
				if (row.Standing == KingdomResidentStanding.Resident && !string.IsNullOrEmpty(row.Name)
					&& row.HomeWorkId == homeWorkId
					&& !known.Contains(row.ResidentId) && !roofs.Exists(roof => roof.ResidentId == row.ResidentId)) return false;
			}
			roofs.Sort((left, right) => left.ResidentId.CompareTo(right.ResidentId));
			return true;
		}

		private static bool CaptureRoofBody(KingdomSystem system, KingdomSurvey survey, GameObject body,
			int id, string plot, Dictionary<string, GameObject> residents)
		{
			if (!GameObject.Validate(body) || string.IsNullOrEmpty(body.IDIfAssigned) || body.CurrentZone != survey.Ground
				|| KingdomResidents.IdOf(body) != id || !KingdomCitizenship.BelongsTo(system, body)
				|| !RawRungProperty(body, KingdomLodging.HomePlotIdProperty, plot)) return false;
			if (residents.TryGetValue(body.IDIfAssigned, out GameObject prior) && !ReferenceEquals(prior, body)) return false;
			residents[body.IDIfAssigned] = body;
			return true;
		}
	}
}
