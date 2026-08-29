using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Explicit Charter-facing O5 flow. Charter registration is kept in Core and is
	/// integrated only after the concurrent D6 Charter edit freezes.</summary>
	public static partial class KingdomWitnessWorkCharterRuntime
	{
		private sealed class Ground
		{
			internal KingdomSystem System;
			internal GameObject Founder;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal KingdomCurrentCityEvidenceRuntime.Context City;
			internal IKingdomCivicMemoryAuthority Memory;
			internal string RealmId;
			internal long Tick;
		}

		private sealed class Carrier
		{
			internal GameObject Object;
			internal KingdomSiteBuiltWorkEvidence Evidence;
		}

		private static bool TryGround(KingdomSystem System, GameObject Founder,
			out Ground Result, out string Failure)
		{
			Result = null; Failure = null;
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.Founded || !GameObject.Validate(Founder)
				|| !Founder.IsPlayer() || !ReferenceEquals(Founder, The.Player) || zone == null)
			{
				Failure = "the Charter bearer is not standing on loaded ground"; return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone, System);
			if (!KingdomCurrentCityEvidenceRuntime.TryContext(System, zone, survey, true,
				out KingdomCurrentCityEvidenceRuntime.Context city, out Failure)
				|| !System.TryGetCurrentIdentity(out string realmId, out string settlementId)
				|| settlementId != city.SettlementId || !System.OwnedZone(zone.ZoneID)) return false;
			KingdomCivicMemorySystem memory = The.Game?.GetSystem<KingdomCivicMemorySystem>();
			long tick = The.Game?.TimeTicks ?? -1L;
			if (memory == null || tick < 0L)
			{
				Failure = "C18 civic memory or exact time is unavailable"; return false;
			}
			Result = new Ground { System = System, Founder = Founder, Zone = zone,
				Survey = survey, City = city, Memory = memory, RealmId = realmId, Tick = tick };
			return true;
		}

		private static bool TryCarriers(Ground Ground, KingdomWitnessWorkBook Book,
			out List<Carrier> Result, out string Failure)
		{
			Result = new List<Carrier>();
			if (!KingdomCurrentCityEvidenceRuntime.TryBuiltWorks(Ground.City,
				out List<KingdomCurrentCityEvidenceRuntime.Work> works, out Failure)) return false;
			for (int i = 0; i < works.Count; i++)
			{
				GameObject item = works[i].Object;
				if (!KingdomWitnessWorkProjectionRuntime.SupportsFixture(item.Blueprint)) continue;
				int cairnRefs = 0;
				for (int j = 0; j < Ground.Survey.Cairns.Count; j++)
					if (ReferenceEquals(item, Ground.Survey.Cairns[j])) cairnRefs++;
				if (cairnRefs != 1 || !KingdomWitnessWorkProjectionRuntime.TryCarrierIdentity(item,
					Ground.Survey, out string objectId, out string zoneId, out string _)
					|| works[i].Evidence.ObjectId != objectId
					|| "taf:zone:" + works[i].Evidence.ZoneId != zoneId
					|| !KingdomWitnessWorkProjectionRuntime.TryRequireUnclaimed(Book,
						objectId, out string _)) continue;
				Result.Add(new Carrier { Object = item, Evidence = works[i].Evidence });
			}
			Failure = null; return true;
		}

		private static bool MakerPresent(Ground Ground, KingdomWitnessWorkSource Source)
		{
			int matches = 0;
			List<KingdomResidentRow> rows = KingdomResidents.RollRows(Ground.System);
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].ResidentId == Source.MakerResidentId
					&& rows[i].Name == Source.MakerName) matches++;
			return matches == 1;
		}
	}
}
