using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>Active-ground observation and exact unloaded read boundary for schooling posts.</summary>
	internal static class KingdomEducationPostObservationRuntime
	{
		internal const string PropertyName = "r_TAF_EducationPostObservation_v1";
		private const int MaxRevocationZones = 4096;

		internal static bool Proves(KingdomSystem System, string SettlementId,
			KingdomWorkRow Work, long CurrentTick)
		{
			if (!TryBinding(System, Work.ZoneId, SettlementId,
				out string realm, out string owner)) return false;
			Zone active = The.ZoneManager?.ActiveZone;
			if (active != null && string.Equals(active.ZoneID, Work.ZoneId,
				StringComparison.Ordinal)) return LiveProves(System, SettlementId, Work, active);
			if (!TryRaw(Work.ZoneId, out bool present, out object raw) || !present
				|| !KingdomZoneObservationRules.TryReadExact(raw,
					KingdomEducationPostObservationRules.Purpose, realm, SettlementId,
					Work.ZoneId, owner, KingdomEducationPostObservationRules.SourceRevision,
					CurrentTick, out KingdomZoneObservationReceipt receipt)) return false;
			return KingdomEducationPostObservationRules.TryFindExact(receipt.Payload,
				Work.WorkId, Work.ZoneId, Work.AnchorX, Work.AnchorY, Work.DesignKey, out _);
		}

		internal static void OnSemanticPass(KingdomSystem System, Zone Zone,
			KingdomSurvey Survey)
		{
			string zoneId = Zone?.ZoneID;
			if (!TryRevokeZone(zoneId, out string failure))
			{
				KingdomLog.Log("education observation: revocation refused (" + failure + ")");
				return;
			}
			string settlementId = System?.SettlementIdForOwnedZone(zoneId);
			KingdomCityState state = null;
			KingdomCityFault cityFault = default(KingdomCityFault);
			if (The.Game == null || Zone == null || Survey == null
				|| !ReferenceEquals(Zone, The.ZoneManager?.ActiveZone)
				|| !ReferenceEquals(Survey.Ground, Zone)
				|| !TryBinding(System, zoneId, settlementId, out string realm, out string owner)
				|| System.City == null || !System.City.TryRead(out state, out cityFault)
				|| !string.Equals(state.SettlementId, settlementId, StringComparison.Ordinal))
			{
				KingdomLog.Log("education observation: current city authority unavailable ("
					+ cityFault + ")"); return;
			}
			long tick = The.Game.TimeTicks;
			if (tick < 0L || !Survey.TryBenefits(out KingdomBenefitIndex benefits,
				out failure) || benefits == null
				|| !TryRows(state, Zone, Survey, benefits,
					out List<KingdomEducationPostObservationRow> rows, out failure)
				|| !KingdomEducationPostObservationRules.TryEncode(rows, out string payload)
				|| !KingdomZoneObservationRules.TryCreate(
					KingdomEducationPostObservationRules.Purpose, realm, settlementId,
					zoneId, owner, KingdomEducationPostObservationRules.SourceRevision,
					tick, payload, out KingdomZoneObservationReceipt receipt)
				|| !KingdomZoneObservationCodec.TryEncode(receipt, out string wire))
			{
				KingdomLog.Log("education observation: physical snapshot refused ("
					+ (failure ?? "malformed bounded authority") + ")"); return;
			}
			if (The.Game.TimeTicks != tick || !ReferenceEquals(Zone, The.ZoneManager?.ActiveZone)
				|| !TryBinding(System, zoneId, settlementId, out string reprovedRealm,
					out string reprovedOwner)
				|| reprovedRealm != realm || reprovedOwner != owner)
			{
				KingdomLog.Log("education observation: authority changed before publication"); return;
			}
			try { Zone.SetZoneProperty(PropertyName, wire); }
			catch (Exception) { TryRemoveRaw(zoneId); return; }
			if (!TryRaw(zoneId, out bool present, out object raw) || !present
				|| raw?.GetType() != typeof(string)
				|| !string.Equals((string)raw, wire, StringComparison.Ordinal)
				|| !KingdomZoneObservationRules.TryReadExact(raw,
					KingdomEducationPostObservationRules.Purpose, realm, settlementId,
					zoneId, owner, KingdomEducationPostObservationRules.SourceRevision,
					tick, out KingdomZoneObservationReceipt exact)
				|| !KingdomEducationPostObservationRules.TryDecode(exact.Payload, out _))
				TryRemoveRaw(zoneId);
		}

		internal static bool TryRevokeOwned(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System == null || !System.TryExactSettlementIds(true,
				out List<string> _, out Failure)) return false;
			List<string> zones = new List<string>();
			if (!AddZones(zones, System.ClaimedZones, out Failure)) return false;
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; i < others.Count; i++)
				if (!AddZones(zones, others[i]?.ClaimedZones, out Failure)) return false;
			return TryRevokeZones(zones, out Failure);
		}

		internal static bool TryRevokeZones(IList<string> ZoneIds, out string Failure)
		{
			Failure = null;
			if (The.Game == null || The.ZoneManager?.ZoneProperties == null || ZoneIds == null
				|| ZoneIds.Count > MaxRevocationZones)
				return Fail("education revocation cannot bound zone registry", out Failure);
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				string zone = ZoneIds[i];
				if (!KingdomZoneObservationRules.Text(zone,
					KingdomZoneObservationRules.MaxIdentityChars) || !seen.Add(zone))
					return Fail("education revocation found malformed or duplicate ground",
						out Failure);
				if (!TryRevokeZone(zone, out Failure)) return false;
			}
			return true;
		}

		internal static bool TryRevokeZone(string ZoneId, out string Failure)
		{
			Failure = null;
			if (The.Game == null || !KingdomZoneObservationRules.Text(ZoneId,
				KingdomZoneObservationRules.MaxIdentityChars) || !TryRemoveRaw(ZoneId))
				return Fail("education receipt could not be removed exactly", out Failure);
			return true;
		}

		private static bool LiveProves(KingdomSystem System, string SettlementId,
			KingdomWorkRow Work, Zone Zone)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Zone)
				?? KingdomSurvey.Take(Zone, System);
			if (survey == null || !ReferenceEquals(survey.Ground, Zone)
				|| !TryExactRoot(Zone, survey, Work, out GameObject root, out int matches)
				|| matches != 1 || !survey.TryBenefits(out KingdomBenefitIndex benefits,
					out string _)) return false;
			return TryExactReading(benefits.Readings, Work, root,
				out KingdomBenefitReading reading, out int readings) && readings == 1
				&& KingdomBenefitCapabilities.Has(reading,
					KingdomBenefitCapabilities.Education);
		}

		private static bool TryRows(KingdomCityState State, Zone Zone, KingdomSurvey Survey,
			KingdomBenefitIndex Benefits, out List<KingdomEducationPostObservationRow> Rows,
			out string Failure)
		{
			Rows = new List<KingdomEducationPostObservationRow>(); Failure = null;
			IReadOnlyList<KingdomBenefitReading> readings = Benefits.Readings;
			for (int i = 0; i < State.WorkCount; i++)
			{
				if (!State.TryWork(i, out KingdomWorkRow work)
					|| !string.Equals(work.ZoneId, Zone.ZoneID, StringComparison.Ordinal)) continue;
				if (!UniqueWork(State, work.WorkId)
					|| !TryExactRoot(Zone, Survey, work, out GameObject root, out int matches))
					return Fail("city work identity is malformed", out Failure);
				if (matches == 0) continue;
				if (matches != 1 || !TryExactReading(readings, work, root,
					out KingdomBenefitReading reading, out int readingCount))
					return Fail("city work root or designation is ambiguous", out Failure);
				if (readingCount == 0 || !KingdomBenefitCapabilities.Has(reading,
					KingdomBenefitCapabilities.Education)) continue;
				KingdomBenefitDesignation d = reading.Designation;
				Rows.Add(new KingdomEducationPostObservationRow { WorkId = work.WorkId,
					RootId = root.IDIfAssigned, DesignationIdentity = d.Identity,
					DesignationRevision = d.Revision, ZoneId = work.ZoneId,
					AnchorX = work.AnchorX, AnchorY = work.AnchorY,
					Blueprint = work.DesignKey });
			}
			return Rows.Count <= KingdomEducationPostObservationRules.MaxRows;
		}

		private static bool TryExactRoot(Zone Zone, KingdomSurvey Survey, KingdomWorkRow Work,
			out GameObject Root, out int Matches)
		{
			Root = null; Matches = 0;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject candidate = Survey.Built[i]; Cell cell = candidate?.CurrentCell;
				if (!KingdomUpgrade.IsFunctionallyBuilt(candidate)
					|| !ReferenceEquals(candidate.CurrentZone, Zone)
					|| cell == null || cell.X != Work.AnchorX || cell.Y != Work.AnchorY
					|| candidate.Blueprint != Work.DesignKey
					|| KingdomCityRules.StableId(candidate.IDIfAssigned) != Work.WorkId) continue;
				Matches++; if (Matches == 1) Root = candidate;
			}
			if (Matches != 1) Root = null; return true;
		}

		private static bool TryExactReading(IReadOnlyList<KingdomBenefitReading> Readings,
			KingdomWorkRow Work, GameObject Root, out KingdomBenefitReading Reading, out int Count)
		{
			Reading = null; Count = 0; string rootId = Root?.IDIfAssigned;
			for (int i = 0; Readings != null && i < Readings.Count; i++)
			{
				KingdomBenefitDesignation d = Readings[i]?.Designation;
				if (d == null || d.RootId != rootId) continue;
				Count++; if (Count == 1) Reading = Readings[i];
			}
			if (Count != 1) { Reading = null; return Count == 0; }
			return string.Equals(Reading.Designation.ZoneId, Work.ZoneId,
				StringComparison.Ordinal) && KingdomEducationPostObservationRules.Valid(
				new KingdomEducationPostObservationRow { WorkId = Work.WorkId, RootId = rootId,
					DesignationIdentity = Reading.Designation.Identity,
					DesignationRevision = Reading.Designation.Revision, ZoneId = Work.ZoneId,
					AnchorX = Work.AnchorX, AnchorY = Work.AnchorY,
					Blueprint = Work.DesignKey });
		}

		private static bool UniqueWork(KingdomCityState State, int WorkId)
		{
			if (WorkId <= 0) return false; int count = 0;
			for (int i = 0; i < State.WorkCount; i++)
				if (State.TryWork(i, out KingdomWorkRow row) && row.WorkId == WorkId) count++;
			return count == 1;
		}

		private static bool TryBinding(KingdomSystem System, string ZoneId,
			string SettlementId, out string RealmId, out string OwnerId)
		{
			RealmId = System?.RealmId; OwnerId = System?.KingdomFactionName;
			return System != null && System.Founded && string.IsNullOrEmpty(System.IdentityFault)
				&& KingdomZoneObservationRules.Text(RealmId, 512)
				&& KingdomZoneObservationRules.Text(OwnerId, 512)
				&& KingdomZoneObservationRules.Text(SettlementId, 512)
				&& KingdomZoneObservationRules.Text(ZoneId, 512)
				&& System.TryExactSettlementIds(true, out List<string> settlements, out string _)
				&& settlements.Contains(SettlementId) && System.OwnedZone(ZoneId)
				&& System.SettlementIdForOwnedZone(ZoneId) == SettlementId;
		}

		private static bool AddZones(List<string> Into, IList<string> Zones, out string Failure)
		{
			Failure = null; if (Zones == null) return false;
			for (int i = 0; i < Zones.Count; i++)
			{
				if (Into.Count == MaxRevocationZones)
					return Fail("education revocation exceeds zone bound", out Failure);
				Into.Add(Zones[i]);
			}
			return true;
		}

		private static bool TryRaw(string ZoneId, out bool Present, out object Raw)
		{
			Present = false; Raw = null;
			Dictionary<string, Dictionary<string, object>> all = The.ZoneManager?.ZoneProperties;
			if (all == null) return false;
			if (!all.TryGetValue(ZoneId, out Dictionary<string, object> properties)) return true;
			if (properties == null) return false;
			Present = properties.TryGetValue(PropertyName, out Raw); return true;
		}

		private static bool TryRemoveRaw(string ZoneId)
		{
			Dictionary<string, Dictionary<string, object>> all = The.ZoneManager?.ZoneProperties;
			if (all == null) return false;
			if (!all.TryGetValue(ZoneId, out Dictionary<string, object> properties)) return true;
			if (properties == null) return false;
			properties.Remove(PropertyName); return !properties.ContainsKey(PropertyName);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
