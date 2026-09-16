using System;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool RungRoofExact(RungFrame frame, KingdomSubsidenceRungWork work,
			KingdomSubsidenceRungRoof roof, out KingdomCityBook.SubsidenceRoofRow row)
		{
			row = null;
			KingdomCityBook city = frame.Owner.City;
			if (!RungOwnerExact(frame)
				|| frame.System.Bindings == null || !frame.System.Bindings.TryReadExact(out KingdomBindingTable bindings, out _)
				|| !bindings.TryGet(roof.ResidentId, KingdomBindingKind.Resident, out KingdomBinding binding)
				|| binding.ObjectId != roof.BodyObjectId
				|| !city.TryCaptureSubsidenceRoof(roof.ResidentId, out row) || binding.ZoneId != row.ZoneId
				|| row.Standing != (int)KingdomResidentStanding.Resident
				|| !KingdomSubsidenceRungRules.HomeOwnerMatches(frame.Plan, work, roof,
					row.HomeWorkId, row.ZoneId, row.Residence)) return false;
			bool present = frame.Subjects.TryGetValue(roof.BodyObjectId, out GameObject body);
			if (roof.HomeZoneId != null && !present && row.ZoneId != frame.Plan.ZoneId) return true;
			if (!present || !GameObject.Validate(body) || body.IDIfAssigned != roof.BodyObjectId
				|| body.CurrentZone?.ZoneID != binding.ZoneId || KingdomResidents.IdOf(body) != roof.ResidentId
				|| !KingdomCitizenship.BelongsTo(frame.System, body)
				|| !RawRungProperty(body, KingdomLodging.HomePlotIdProperty, work.PlotId)) return false;
			return body.CurrentZone != frame.Zone ? roof.HomeZoneId != null
				: ReferenceEquals(frame.Survey.FindBoundBody(roof.BodyObjectId, KingdomBindingKind.Resident), body);
		}

		private static bool ApplyRungRoof(RungFrame frame, int workIndex, int roofIndex)
		{
			KingdomSubsidenceRungWork work = frame.Plan.Works[workIndex];
			KingdomSubsidenceRungRoof roof = work.Roofs[roofIndex];
			if (!RungRoofExact(frame, work, roof, out KingdomCityBook.SubsidenceRoofRow row)) return false;
			KingdomCityBook city = frame.Owner.City;
			KingdomSubsidenceEffectAction action = KingdomSubsidenceRungRules.RoofAction(frame.Plan,
				workIndex, roofIndex, true, row.RoofStanding, row.Reached, row.Warned);
			if (action == KingdomSubsidenceEffectAction.Refuse) return false;
			if (action == KingdomSubsidenceEffectAction.Apply)
			{
				if (!city.TryPublishSubsidenceRoof(frame.Owner.Wire, row, true,
					roof.BeforeStanding ? roof.BeforeReached : frame.Plan.DueTick,
					roof.BeforeStanding ? roof.BeforeWarned : KingdomBrinkRules.Unwarned, out _)) return false;
			}
			if (!RungRoofExact(frame, work, roof, out KingdomCityBook.SubsidenceRoofRow after)
				|| !row.SameCarriers(after)) return false;
			return KingdomSubsidenceStepRules.TryProveRungRoof(frame.Owner.Step, workIndex, roofIndex,
				true, after.RoofStanding, after.Reached, after.Warned,
				out KingdomSubsidenceStepBook next) && SaveRung(frame, next);
		}
	}
}
