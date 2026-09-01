using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomWitnessWorkRemovalPlan
	{
		internal GameObject Carrier;
		internal r_KingdomWitnessWorkProjection Marker;
		internal KingdomWitnessWorkReceipt Receipt;
	}

	internal static partial class KingdomRealmRetirementGround
	{
		private const string WitnessObjectPrefix = "taf:object:";
		private const string WitnessZonePrefix = "taf:zone:";

		private static bool TryPrepareWitnessWorks(KingdomSystem System, Zone Zone,
			IList<GameObject> Objects, HashSet<GameObject> ConstructionOwned,
			KingdomRealmRemovalGroundPlan Plan, out string Failure)
		{
			Failure = null;
			if (System == null || Zone == null || ConstructionOwned == null || Plan == null
				|| !System.TryReadRealmRetirement(out KingdomRealmRetirementState state,
					out Failure)) return false;
			if (!TryFindWitnessAuthority(out KingdomCivicMemorySystem authority, out Failure)
				|| !KingdomWitnessWorkLease.TryReadAuthority(authority, System.RealmId,
					out _, out KingdomCivicArtifactsEnvelope held, out Failure)) return false;
			KingdomSurvey survey = ReadOnlyWitnessSurvey(Zone, Objects);
			string zoneId = WitnessZonePrefix + Zone.ZoneID;
			string settlementId = System.SettlementIdForOwnedZone(Zone.ZoneID);
			HashSet<string> planned = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < held.WitnessWorks.Rows.Count; i++)
			{
				KingdomWitnessWorkReceipt row = held.WitnessWorks.Rows[i];
				if (row.CarrierZoneId != zoneId) continue;
				if (row.Phase == KingdomWitnessWorkPhase.Removed
					&& row.ChangedTick == state.StartedTick) Plan.WitnessRetryProgress = true;
				if (row.Source.SettlementId != settlementId)
					return Fail("fixed-witness row belongs to another settlement", out Failure);
				GameObject carrier = FindWitnessCarrier(survey, row.CarrierObjectId,
					out bool duplicate);
				if (duplicate)
					return Fail("fixed-witness carrier identity is duplicated", out Failure);
				if (carrier != null && !ConstructionOwned.Contains(carrier))
					return Fail("fixed-witness carrier lacks exact current-realm construction authority",
						out Failure);
				r_KingdomWitnessWorkProjection marker =
					carrier?.GetPart<r_KingdomWitnessWorkProjection>();
				bool live = row.Phase == KingdomWitnessWorkPhase.CarrierPrepared
					|| row.Phase == KingdomWitnessWorkPhase.Projected;
				if (live && marker == null)
					return Fail("live fixed-witness authority lacks its exact loaded marker; recover its source lane before removal",
						out Failure);
				if (marker == null) continue;
				if (!planned.Add(row.WorkId)
					|| !KingdomWitnessWorkProjectionRuntime.TryObserve(System.RealmId, row,
						Zone, survey, out KingdomWitnessCarrierObservation observation,
						out GameObject exact, out Failure)
					|| observation != KingdomWitnessCarrierObservation.Present
					|| !ReferenceEquals(exact, carrier))
					return Fail(Failure ?? "fixed-witness marker is not uniquely authenticated",
						out Failure);
				Plan.WitnessWorks.Add(new KingdomWitnessWorkRemovalPlan
					{ Carrier = carrier, Marker = marker,
						Receipt = KingdomWitnessWorkRules.FindExact(held.WitnessWorks, row.WorkId) });
			}
			for (int i = 0; i < survey.Objects.Count; i++)
			{
				GameObject carrier = survey.Objects[i];
				r_KingdomWitnessWorkProjection marker =
					carrier?.GetPart<r_KingdomWitnessWorkProjection>();
				if (marker == null) continue;
				int matches = 0;
				for (int j = 0; j < Plan.WitnessWorks.Count; j++)
					if (ReferenceEquals(Plan.WitnessWorks[j].Carrier, carrier)
						&& ReferenceEquals(Plan.WitnessWorks[j].Marker, marker)) matches++;
				if (matches != 1)
					return Fail("loaded fixed-witness marker has no unique section-1 authority",
						out Failure);
			}
			Plan.WitnessAuthority = authority; Plan.WitnessSurvey = survey;
			Plan.WitnessRetirementTick = state.StartedTick;
			return true;
		}

		private static bool TryRetireWitnessWorks(KingdomSystem System,
			KingdomRealmRemovalGroundPlan Plan, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Plan.WitnessWorks.Count; i++)
			{
				KingdomWitnessWorkRemovalPlan action = Plan.WitnessWorks[i];
				if (!KingdomWitnessWorkLease.TryReadBackRow(Plan.WitnessAuthority,
					System.RealmId, action.Receipt.WorkId,
					out KingdomWitnessWorkReceipt current, out Failure)
					|| !SameWitnessAuthority(action.Receipt, current))
					return Fail(Failure ?? "fixed-witness authority changed after preflight",
						out Failure);
				if (current.Phase == KingdomWitnessWorkPhase.CarrierPrepared
					|| current.Phase == KingdomWitnessWorkPhase.Projected)
				{
					if (!KingdomWitnessWorkCommit.TryReconcile(Plan.WitnessAuthority,
						System.RealmId, current.WorkId, true, true,
						Plan.WitnessRetirementTick, out Failure)
						|| !KingdomWitnessWorkLease.TryReadBackRow(Plan.WitnessAuthority,
							System.RealmId, current.WorkId, out current, out Failure)
						|| current.Phase != KingdomWitnessWorkPhase.Removed
						|| !SameWitnessAuthority(action.Receipt, current))
						return Fail(Failure ?? "fixed-witness row did not retain terminal retirement",
							out Failure);
				}
				if (current.Phase != KingdomWitnessWorkPhase.Removed
					&& current.Phase != KingdomWitnessWorkPhase.Lost)
					return Fail("fixed-witness marker lacks terminal semantic authority", out Failure);
				if (action.Carrier.GetPart<r_KingdomWitnessWorkProjection>() != null
					&& !KingdomWitnessWorkProjectionRuntime.TryDetach(System.RealmId, current,
						Plan.Zone, Plan.WitnessSurvey, out Failure)
					&& action.Carrier.GetPart<r_KingdomWitnessWorkProjection>() != null)
					return false;
				if (action.Carrier.GetPart<r_KingdomWitnessWorkProjection>() != null)
					return Fail("terminal fixed-witness marker remained attached", out Failure);
			}
			return true;
		}

		private static bool SameWitnessAuthority(KingdomWitnessWorkReceipt Frozen,
			KingdomWitnessWorkReceipt Current)
		{
			return Frozen != null && Current != null && Frozen.WorkId == Current.WorkId
				&& Frozen.Source.SnapshotDigest == Current.Source.SnapshotDigest
				&& Frozen.CarrierReceiptId == Current.CarrierReceiptId
				&& Frozen.CarrierObjectId == Current.CarrierObjectId
				&& Frozen.CarrierZoneId == Current.CarrierZoneId
				&& Frozen.CarrierConstructionReceiptId == Current.CarrierConstructionReceiptId
				&& Frozen.CarrierX == Current.CarrierX && Frozen.CarrierY == Current.CarrierY
				&& Frozen.Description == Current.Description;
		}

		private static KingdomSurvey ReadOnlyWitnessSurvey(Zone Zone,
			IList<GameObject> Objects)
		{
			KingdomSurvey survey = new KingdomSurvey { Ground = Zone };
			HashSet<GameObject> seen = new HashSet<GameObject>();
			for (int i = 0; i < (Objects?.Count ?? 0); i++)
			{
				GameObject item = Objects[i];
				if (!GameObject.Validate(item) || item.CurrentZone != Zone
					|| item.CurrentCell == null || !seen.Add(item)) continue;
				survey.Objects.Add(item);
				if (KingdomUpgrade.IsFunctionallyBuilt(item)
					&& KingdomWitnessWorkProjectionRuntime.SupportsFixture(item.Blueprint))
					survey.Cairns.Add(item);
			}
			return survey;
		}

		private static GameObject FindWitnessCarrier(KingdomSurvey Survey, string TypedId,
			out bool Duplicate)
		{
			Duplicate = false; GameObject found = null;
			if (string.IsNullOrEmpty(TypedId) || !TypedId.StartsWith(WitnessObjectPrefix,
				StringComparison.Ordinal)) return null;
			string raw = TypedId.Substring(WitnessObjectPrefix.Length);
			for (int i = 0; i < Survey.Objects.Count; i++)
				if (Survey.Objects[i].IDIfAssigned == raw)
				{
					if (found != null) { Duplicate = true; return null; }
					found = Survey.Objects[i];
				}
			return found;
		}

		private static bool TryFindWitnessAuthority(out KingdomCivicMemorySystem Authority,
			out string Failure)
		{
			Authority = null; Failure = null;
			for (int i = 0; i < (The.Game?.Systems?.Count ?? 0); i++)
				if (The.Game.Systems[i]?.GetType() == typeof(KingdomCivicMemorySystem))
				{
					if (Authority != null)
						return Fail("civic-memory system is duplicated", out Failure);
					Authority = (KingdomCivicMemorySystem)The.Game.Systems[i];
				}
			return Authority != null || Fail("civic-memory system is absent", out Failure);
		}

		private static bool SameWitnessPlan(KingdomRealmRemovalGroundPlan Expected,
			KingdomRealmRemovalGroundPlan Actual)
		{
			if (Expected == null || Actual == null
				|| !ReferenceEquals(Expected.WitnessAuthority, Actual.WitnessAuthority)
				|| Expected.WitnessRetirementTick != Actual.WitnessRetirementTick
				|| Expected.WitnessRetryProgress != Actual.WitnessRetryProgress
				|| Expected.WitnessWorks.Count != Actual.WitnessWorks.Count) return false;
			for (int i = 0; i < Expected.WitnessWorks.Count; i++)
			{
				KingdomWitnessWorkRemovalPlan left = Expected.WitnessWorks[i];
				KingdomWitnessWorkRemovalPlan right = Actual.WitnessWorks[i];
				if (!ReferenceEquals(left.Carrier, right.Carrier)
					|| !ReferenceEquals(left.Marker, right.Marker)
					|| !SameWitnessAuthority(left.Receipt, right.Receipt)) return false;
			}
			return true;
		}
	}
}
