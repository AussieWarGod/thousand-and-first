using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
		internal sealed class SubsidenceRoofRow
		{
			internal readonly KingdomCityBook City;
			internal readonly int ResidentId, HomeWorkId, Standing;
			internal readonly string ZoneId, SettlementId;
			internal readonly bool RoofStanding;
			internal readonly long Reached, Warned;
			internal readonly List<int> Ids, Homes, Standings, Roofs;
			internal readonly List<string> Zones;
			internal readonly List<long> ReachedTicks, WarnedTicks;
			internal SubsidenceRoofRow(KingdomCityBook city, int index)
			{
				City = city; SettlementId = city.SettlementId;
				ResidentId = city.ResidentIds[index]; HomeWorkId = city.ResidentHomeWorkIds[index];
				Standing = city.ResidentStandings[index]; ZoneId = city.ResidentBoundZoneIds[index];
				RoofStanding = city.ResidentRoofStanding[index] != 0;
				Reached = city.ResidentRoofTicks[index]; Warned = city.ResidentRoofWarnedTicks[index];
				Ids = city.ResidentIds; Homes = city.ResidentHomeWorkIds; Standings = city.ResidentStandings;
				Roofs = city.ResidentRoofStanding; Zones = city.ResidentBoundZoneIds;
				ReachedTicks = city.ResidentRoofTicks; WarnedTicks = city.ResidentRoofWarnedTicks;
			}

			internal bool SameCarriers(SubsidenceRoofRow other)
			{
				return other != null && ReferenceEquals(City, other.City) && SettlementId == other.SettlementId
					&& ResidentId == other.ResidentId
					&& HomeWorkId == other.HomeWorkId && Standing == other.Standing && ZoneId == other.ZoneId
					&& ReferenceEquals(Ids, other.Ids) && ReferenceEquals(Homes, other.Homes)
					&& ReferenceEquals(Standings, other.Standings) && ReferenceEquals(Roofs, other.Roofs)
					&& ReferenceEquals(Zones, other.Zones) && ReferenceEquals(ReachedTicks, other.ReachedTicks)
					&& ReferenceEquals(WarnedTicks, other.WarnedTicks);
			}
		}

		internal bool TryCaptureSubsidenceRoof(int residentId, out SubsidenceRoofRow row)
		{
			row = null;
			if (!HasValidSubsidenceStorage() || !TryReadExact(out _, out _)
				|| !TryResidentRow(residentId, out int index)) return false;
			row = new SubsidenceRoofRow(this, index);
			return true;
		}

		/// <summary>The owned rung bypasses the competing-writer fence only with exact parent bytes
		/// and the same raw row carriers. No enrollment, normalization, or whole-book publication.</summary>
		internal bool TryPublishSubsidenceRoof(string stepWire, SubsidenceRoofRow prior,
			bool stands, long reached, long warned, out SubsidenceRoofRow after)
		{
			after = null;
			if (prior == null || !ReferenceEquals(prior.City, this) || SubsidenceModel != stepWire
				|| !KingdomSubsidenceStepCodec.TryDecode(stepWire, out KingdomSubsidenceStepBook parent)
				|| parent.SettlementId != SettlementId
				|| reached < 0 || warned < 0 || !stands && (reached != 0 || warned != 0)
				|| !TryCaptureSubsidenceRoof(prior.ResidentId, out SubsidenceRoofRow current)
				|| !prior.SameCarriers(current) || prior.RoofStanding != current.RoofStanding
				|| prior.Reached != current.Reached || prior.Warned != current.Warned
				|| !KingdomSubsidenceRungRules.AdmitsRoofWrite(stepWire, prior.ResidentId,
					prior.HomeWorkId, prior.ZoneId, prior.Standing, prior.RoofStanding,
					prior.Reached, prior.Warned, stands, reached, warned)
				|| !TryResidentRow(prior.ResidentId, out int index)) return false;
			ResidentRoofStanding[index] = stands ? 1 : 0;
			ResidentRoofTicks[index] = reached;
			ResidentRoofWarnedTicks[index] = warned;
			return SubsidenceModel == stepWire && TryCaptureSubsidenceRoof(prior.ResidentId, out after)
				&& prior.SameCarriers(after) && after.RoofStanding == stands
				&& after.Reached == reached && after.Warned == warned;
		}
	}
}
