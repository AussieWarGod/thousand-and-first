using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomRaids
	{
		private static void MigrateLegacyEvidence(KingdomSystem system)
		{
			KingdomLifecycleBook book = system.LifecycleBook;
			if (book == null || !KingdomLifecycleRules.CanOwnAuthority(book)
				|| book.Raid != null || book.RaidLedger.LegacyEvidenceArchived
				|| KingdomRaidIncidentRules.Active(book.RaidLedger) != null) return;
			if (system.RaidState == 0 && string.IsNullOrEmpty(system.RaidFactionName)
				&& system.RaidDueTick == 0L && system.LastRaidTick == 0L
				&& system.RaidTimesDeferred == 0) return;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Raid, KingdomLifecycleAction.RaidCancel, The.Game.TimeTicks);
			if (op == null) return;
			op.Kind = (int)KingdomRaidResolution.LegacyWarningDispersed;
			op.Target = Math.Max(0, system.RaidState);
			op.Count = Math.Max(0, system.RaidTimesDeferred);
			op.Faction = system.RaidFactionName;
			op.DepartTick = Math.Max(0L, system.RaidDueTick);
			op.Origin = Math.Max(0L, system.LastRaidTick).ToString(CultureInfo.InvariantCulture);
			op.Outbox = KingdomLifecycleRules.PrepareOutbox(op,
				"an old standing-derived raid warning was archived and dispersed without causal reinterpretation",
				"Legacy raid evidence retained: state " + op.Target + ", faction "
					+ (op.Faction ?? "none") + ", due " + op.DepartTick + ", last " + op.Origin
					+ ", deferrals " + op.Count + ".",
				"{{W|An old standing-derived warning is archived. It causes no raid and takes nothing.}}",
				null, null);
			if (!PublishSimple(system, op) || !book.RaidLedger.LegacyEvidenceArchived) return;
			system.RaidState = 0;
			system.RaidFactionName = null;
			system.RaidDueTick = 0L;
			system.LastRaidTick = 0L;
			system.RaidTimesDeferred = 0;
		}

		private static string ExactTargetZone(KingdomSystem system, string preferred)
		{
			if (system?.ClaimedZones == null || system.ClaimedZones.Count == 0) return null;
			if (!string.IsNullOrEmpty(preferred) && system.ClaimedZones.Contains(preferred)) return preferred;
			string best = null;
			for (int i = 0; i < system.ClaimedZones.Count; i++)
				if (!string.IsNullOrEmpty(system.ClaimedZones[i]) && (best == null
					|| string.CompareOrdinal(system.ClaimedZones[i], best) < 0)) best = system.ClaimedZones[i];
			return best;
		}

		private static bool IncidentSourceStillValid(KingdomSystem system,
			KingdomRaidIncident incident)
		{
			if (system == null || incident == null || system.ClaimedZones == null
				|| !system.ClaimedZones.Contains(incident.TargetZoneId)
				|| Factions.GetIfExists(incident.AttackerFactionId) == null) return false;
			KingdomRaidProfile profile;
			GrowthStage frozenStage;
			return KingdomRaidProfiles.TryResolveFrozen(incident.AttackerFactionId,
				incident.ForceProfileId, incident.Seed, incident.PlannedPartySize,
				out profile, out frozenStage);
		}

		private static bool FreezeDefence(KingdomSystem system, KingdomSurvey survey,
			out string commitment,
			out int total)
		{
			commitment = null; total = 0;
			if (system == null || survey?.Ground == null || survey.Defences == null
				|| survey.Settlers == null) return false;
			Dictionary<int, KingdomResidentRow> residentRows;
			Dictionary<int, GameObject> residentBodies;
			if (!TryDefenceResidents(system, survey, out residentRows, out residentBodies))
				return false;
			if (!survey.TryBenefits(out KingdomBenefitIndex benefits,
				out string benefitFailure))
			{
				if (!string.IsNullOrEmpty(benefitFailure))
					KingdomLog.Log("raid defence: " + benefitFailure);
				return false;
			}
			List<KingdomRaidDefenceReservation> rows =
				new List<KingdomRaidDefenceReservation>();
			HashSet<int> works = new HashSet<int>();
			HashSet<int> reservedCrew = new HashSet<int>();
			for (int i = 0; i < survey.Defences.Count; i++)
			{
				GameObject work = survey.Defences[i];
				int score = DefenceOf(work, benefits);
				if (!GameObject.Validate(work) || score <= 0) continue;
				int workId = KingdomCityRules.StableId(work.ID);
				if (workId <= 0 || !works.Add(workId)
					|| rows.Count >= KingdomRaidIncidentRules.MaxDefenceWorks) return false;
				List<int> crew;
				if (!TryExactDefenceCrew(system, survey, work, workId, residentRows,
					residentBodies, reservedCrew, out crew)) return false;
				rows.Add(new KingdomRaidDefenceReservation
				{
					WorkId = workId,
					FrozenScore = score,
					CrewSemanticIds = crew
				});
			}
			return KingdomRaidIncidentRules.TryEncodeDefenceReservations(rows,
				out commitment, out total);
		}

		private static int RevalidateDefence(KingdomSystem system, KingdomSurvey survey,
			KingdomRaidIncident incident)
		{
			if (system == null || survey?.Ground == null || survey.Defences == null
				|| incident == null
				|| incident.DefenceReservationVersion
					!= KingdomRaidIncidentRules.CurrentDefenceReservationVersion) return 0;
			List<KingdomRaidDefenceReservation> decoded;
			int frozenTotal;
			if (!KingdomRaidIncidentRules.TryDecodeDefenceReservations(
				incident.DefenceCommitment, out decoded, out frozenTotal)
				|| frozenTotal != incident.DefenceEstimate
				|| !SameDefenceReservations(decoded, incident.DefenceReservations)) return 0;
			Dictionary<int, KingdomResidentRow> residentRows;
			Dictionary<int, GameObject> residentBodies;
			if (!TryDefenceResidents(system, survey, out residentRows, out residentBodies)
				|| !survey.TryBenefits(out KingdomBenefitIndex benefits, out _)) return 0;
			Dictionary<int, GameObject> current = new Dictionary<int, GameObject>();
			for (int i = 0; i < survey.Defences.Count; i++)
			{
				GameObject work = survey.Defences[i];
				int score = DefenceOf(work, benefits);
				if (!GameObject.Validate(work) || score <= 0) continue;
				int workId = KingdomCityRules.StableId(work.ID);
				if (workId <= 0 || current.ContainsKey(workId)) return 0;
				current.Add(workId, work);
			}
			HashSet<int> reservedCrew = new HashSet<int>();
			long total = 0L;
			for (int i = 0; i < decoded.Count; i++)
			{
				KingdomRaidDefenceReservation frozen = decoded[i];
				GameObject work;
				if (!current.TryGetValue(frozen.WorkId, out work)
					|| DefenceOf(work, benefits) != frozen.FrozenScore) return 0;
				List<int> liveCrew;
				if (!TryExactDefenceCrew(system, survey, work, frozen.WorkId, residentRows,
					residentBodies, reservedCrew, out liveCrew)
					|| !SameIds(liveCrew, frozen.CrewSemanticIds)) return 0;
				total += frozen.FrozenScore;
				if (total > KingdomLifecycleRules.MaxPhysicalCount) return 0;
			}
			return (int)total;
		}

		private static bool TryDefenceResidents(KingdomSystem system, KingdomSurvey survey,
			out Dictionary<int, KingdomResidentRow> rows,
			out Dictionary<int, GameObject> bodies)
		{
			rows = new Dictionary<int, KingdomResidentRow>();
			bodies = new Dictionary<int, GameObject>();
			List<KingdomResidentRow> roll = KingdomResidents.RollRows(system, true);
			for (int i = 0; i < roll.Count; i++)
			{
				KingdomResidentRow row = roll[i];
				if (row.ResidentId <= 0 || rows.ContainsKey(row.ResidentId)) return false;
				rows.Add(row.ResidentId, row);
			}
			for (int i = 0; i < survey.Settlers.Count; i++)
			{
				GameObject body = survey.Settlers[i];
				int residentId = KingdomResidents.IdOf(body);
				if (residentId <= 0) continue;
				if (bodies.ContainsKey(residentId)) return false;
				bodies.Add(residentId, body);
			}
			return true;
		}

		private static bool TryExactDefenceCrew(KingdomSystem system, KingdomSurvey survey,
			GameObject work, int workId, Dictionary<int, KingdomResidentRow> rows,
			Dictionary<int, GameObject> bodies, HashSet<int> reserved, out List<int> crew)
		{
			crew = new List<int>();
			if (!GameObject.Validate(work) || workId <= 0 || rows == null || bodies == null
				|| reserved == null) return false;
			foreach (KeyValuePair<int, GameObject> pair in bodies)
			{
				GameObject body = pair.Value;
				if (KingdomStations.PostOf(body) != workId) continue;
				KingdomResidentRow row;
				GameObject exact;
				string zoneId;
				if (!rows.TryGetValue(pair.Key, out row) || row.JobWorkId != workId
					|| !string.Equals(row.BoundZoneId, survey.Ground.ZoneID,
						StringComparison.Ordinal)
					|| !KingdomResidents.TryResolveBoundBody(system, pair.Key, false,
						out exact, out zoneId)
					|| !ReferenceEquals(exact, body)
					|| !string.Equals(zoneId, survey.Ground.ZoneID, StringComparison.Ordinal)
					|| !reserved.Add(pair.Key)) return false;
				crew.Add(pair.Key);
			}
			crew.Sort();
			int need = work.GetIntProperty("KingdomStaffNeeded");
			return need > 0 ? crew.Count > 0 && crew.Count <= need : crew.Count == 0;
		}

		private static bool SameDefenceReservations(IList<KingdomRaidDefenceReservation> a,
			IList<KingdomRaidDefenceReservation> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++)
				if (a[i] == null || b[i] == null || a[i].WorkId != b[i].WorkId
					|| a[i].FrozenScore != b[i].FrozenScore
					|| !SameIds(a[i].CrewSemanticIds, b[i].CrewSemanticIds)) return false;
			return true;
		}

		private static bool SameIds(IList<int> a, IList<int> b)
		{
			if (a == null || b == null || a.Count != b.Count) return false;
			for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
			return true;
		}

		private static int DefenceOf(GameObject work, KingdomBenefitIndex Benefits)
		{
			if (!GameObject.Validate(work) || Benefits == null
				|| string.IsNullOrEmpty(work.IDIfAssigned)) return 0;
			return Math.Min(KingdomLifecycleRules.MaxPhysicalCount,
				Benefits.AmountForRoot(work.IDIfAssigned, "defence"));
		}

		private static GameObject ExactStore(KingdomSurvey survey, long seed)
		{
			List<GameObject> stores = new List<GameObject>();
			if (survey?.Stores == null) return null;
			for (int i = 0; i < survey.Stores.Count; i++)
			{
				LiquidVolume liquid = survey.Stores[i];
				GameObject owner = liquid?.ParentObject;
				if (GameObject.Validate(owner) && owner.CurrentCell != null
					&& owner.GetIntProperty("KingdomStores") == 1
					&& liquid.Volume > 0 && KingdomLiquids.HasFreshWater(liquid)) stores.Add(owner);
			}
			stores.Sort(delegate(GameObject a, GameObject b) { return string.CompareOrdinal(a.ID, b.ID); });
			return stores.Count == 0 ? null : stores[(int)(seed % stores.Count)];
		}

	}
}
