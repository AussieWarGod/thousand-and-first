#if !TAF_TESTS
using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomCivicKnowledgeRuntime
	{
		/// <summary>Proves P8 presentation at P4's exact loaded, staffed locus. Curator must be
		/// the frozen proposer, physically beside the one civic bench; one exact posted keeper must
		/// also stand there. This reads local bodies only and creates no projection.</summary>
		internal static bool TryProveCuratorPresentation(KingdomSystem system,
			KingdomCuriosityReceipt receipt, out GameObject curator, out GameObject bench,
			out string failure)
		{
			curator = null; bench = null; failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (system == null || receipt == null || zone == null
				|| receipt.State != KingdomCuriosityState.Available
				|| system.SettlementIdForOwnedZone(zone.ZoneID) != receipt.SettlementId
				|| !system.TryFindSettlement(receipt.SettlementId, out bool seated,
					out KingdomSettlement settlement))
				return FailPresentation("stand on the curator's exact current settlement ground",
					out failure);
			KingdomCityBook city = seated ? system.City : settlement?.City;
			if (city == null)
				return FailPresentation("the current resident roll is unavailable", out failure);
			if (!city.TryRead(out KingdomCityState state, out KingdomCityFault cityFault))
				return FailPresentation("the current resident roll is unavailable (" + cityFault + ")",
					out failure);
			List<GameObject> objects = zone.GetObjects();
			if (objects == null)
				return FailPresentation("the loaded civic-locus objects are unavailable", out failure);
			int workId = KingdomLocusRules.SelectLocusWork(city.WorkIds, city.WorkDesignKeys,
				KingdomLocus.BenchBlueprint);
			int benches = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (GameObject.Validate(item) && ReferenceEquals(item.CurrentZone, zone)
					&& item.CurrentCell != null && item.Blueprint == KingdomLocus.BenchBlueprint
					&& item.GetIntProperty("KingdomBuilt") == 1
					&& KingdomCityRules.StableId(item.IDIfAssigned) == workId)
				{ bench = item; benches++; }
			}
			if (workId == 0 || benches != 1 || bench.GetIntProperty("KingdomStaffNeeded") != 1
				|| bench.GetIntProperty("KingdomStaffed") != 1)
				return Clear("the exact civic locus is absent, ambiguous, or unstaffed",
					ref curator, ref bench, out failure);

			if (!state.TryResidentIndex(receipt.CuratorResidentId, out int at)
				|| !state.TryResident(at, out KingdomResidentRow resident)
				|| resident.Standing != KingdomResidentStanding.Resident
				|| resident.Name != receipt.CuratorName
				|| !KingdomResidents.TryResolveBoundBody(system, resident.ResidentId, false,
					out curator, out string curatorZone)
				|| curatorZone != zone.ZoneID || !ReferenceEquals(curator.CurrentZone, zone)
				|| curator.CurrentCell == null || !curator.IsAlive || curator.Brain == null
				|| receipt.CuratorObjectId != "taf:object:" + curator.IDIfAssigned
				|| curator.DistanceTo(bench) > KingdomLocusRules.AmbientDistance
				|| KingdomPhysicalHappenings.IsStaged(curator)
				|| !KingdomCitizenship.BelongsTo(system, curator))
				return Clear("the named curator is not exactly present beside the civic locus",
					ref curator, ref bench, out failure);

			int keepers = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject body = objects[i];
				if (GameObject.Validate(body) && body.IsAlive && body.Brain != null
					&& ReferenceEquals(body.CurrentZone, zone) && body.CurrentCell != null
					&& body.GetIntProperty("KingdomKeeper") == 1
					&& KingdomResidents.IdOf(body) > 0
					&& KingdomStations.PostOf(body) == workId
					&& body.DistanceTo(bench) <= KingdomLocusRules.AmbientDistance
					&& !KingdomPhysicalHappenings.IsStaged(body)
					&& KingdomCitizenship.BelongsTo(system, body)) keepers++;
			}
			if (keepers == 1) return true;
			return Clear("one exact posted civic keeper is not present at the locus",
				ref curator, ref bench, out failure);
		}

		private static bool Clear(string message, ref GameObject curator, ref GameObject bench,
			out string failure)
		{
			curator = null; bench = null;
			return FailPresentation(message, out failure);
		}

		private static bool FailPresentation(string message, out string failure)
		{
			failure = message;
			return false;
		}
	}
}
#endif
