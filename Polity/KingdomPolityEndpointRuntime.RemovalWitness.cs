using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		// Zone.RemoveZoneProperty(key) cannot preserve a replacement racing an exact-value clear.
		private const int MaximumCleanupPropertyZones = 65536;
		private const int MaximumCleanupIntentCells = 16384;

		private static bool TryPrepareRemovalWitness(Cell Cell, string Kind, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int Ordinal,
			out string Failure)
		{
			Failure = null; Zone zone = Cell?.ParentZone;
			if (zone == null) return FailPhysical("removal witness lacks exact loaded ground", out Failure);
			KingdomPolityCleanupEvidenceProof proof = TryProveRemovalWitness(zone, Kind, RealmId,
				CohortId, ProjectionId, ObjectId, Ordinal, out string _, out Failure);
			return proof == KingdomPolityCleanupEvidenceProof.Absent ||
				proof == KingdomPolityCleanupEvidenceProof.Exact || FailPhysical(Failure ??
					"removal witness slot contains foreign or ambiguous authority", out Failure);
		}

		private static bool TryWriteRemovalWitness(Cell Cell, string Kind, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int Ordinal,
			out string Failure)
		{
			if (!TryPrepareRemovalWitness(Cell, Kind, RealmId, CohortId, ProjectionId,
				ObjectId, Ordinal, out Failure)) return false;
			Zone zone = Cell.ParentZone;
			string key = KingdomPolityPhysicalCustodyRules.RemovalWitnessKey(ProjectionId, ObjectId);
			string expected = KingdomPolityPhysicalCustodyRules.RemovalWitness(Kind, RealmId,
				CohortId, ProjectionId, zone.ZoneID, ObjectId, Ordinal);
			try { zone.SetZoneProperty(key, expected); }
			catch (Exception ex)
			{
				if (TryProveRemovalWitness(zone, Kind, RealmId, CohortId, ProjectionId,
					ObjectId, Ordinal, out string _, out string _) ==
					KingdomPolityCleanupEvidenceProof.Exact) { Failure = null; return true; }
				return FailPhysical("removal witness write failed: " + ex.Message, out Failure);
			}
			return TryProveRemovalWitness(zone, Kind, RealmId, CohortId, ProjectionId,
				ObjectId, Ordinal, out string _, out Failure) ==
				KingdomPolityCleanupEvidenceProof.Exact || FailPhysical(Failure ??
					"removal witness did not survive exact writeback", out Failure);
		}

		private static bool HasRemovalWitness(Zone Zone, string Kind, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int Ordinal)
		{
			return TryProveRemovalWitness(Zone, Kind, RealmId, CohortId, ProjectionId,
				ObjectId, Ordinal, out string _, out string _) ==
				KingdomPolityCleanupEvidenceProof.Exact;
		}

		private static KingdomPolityCleanupEvidenceProof TryProveRemovalWitness(Zone Zone,
			string Kind, string RealmId, string CohortId, string ProjectionId, string ObjectId,
			int Ordinal, out string FrozenValue, out string Failure)
		{
			FrozenValue = KingdomPolityPhysicalCustodyRules.RemovalWitness(Kind, RealmId,
				CohortId, ProjectionId, Zone?.ZoneID, ObjectId, Ordinal);
			return InspectUniqueRawZoneSlot(Zone,
				KingdomPolityPhysicalCustodyRules.RemovalWitnessKey(ProjectionId, ObjectId),
				FrozenValue, out string _, out Failure);
		}

		private static bool TryWriteCleanupIntent(Cell Cell, string RealmId, string CohortId,
			string ProjectionId, string ObjectId, int Ordinal, byte CohortPhase,
			byte ProjectionPhase, out string FrozenKey, out string FrozenValue,
			out string Failure)
		{
			Failure = null; Zone zone = Cell?.ParentZone;
			FrozenKey = KingdomPolityPhysicalCustodyRules.CleanupIntentKey(ProjectionId, ObjectId);
			FrozenValue = zone == null ? null : KingdomPolityPhysicalCustodyRules.PreparedCleanupIntent(
				RealmId, CohortId, ProjectionId, zone.ZoneID, ObjectId, Ordinal, Cell.X, Cell.Y,
				CohortPhase, ProjectionPhase);
			if (zone == null) return FailPhysical("cleanup intent lacks loaded ground", out Failure);
			KingdomPolityCleanupEvidenceProof prior = InspectUniqueRawZoneSlot(zone, FrozenKey,
				FrozenValue, out string _, out Failure);
			if (prior == KingdomPolityCleanupEvidenceProof.Exact) return true;
			if (prior != KingdomPolityCleanupEvidenceProof.Absent) return FailPhysical(Failure ??
				"cleanup intent slot contains foreign authority", out Failure);
			try { zone.SetZoneProperty(FrozenKey, FrozenValue); }
			catch (Exception ex)
			{
				if (InspectUniqueRawZoneSlot(zone, FrozenKey, FrozenValue, out string _,
					out string _) == KingdomPolityCleanupEvidenceProof.Exact)
					{ Failure = null; return true; }
				return FailPhysical("cleanup intent write failed: " + ex.Message, out Failure);
			}
			return InspectUniqueRawZoneSlot(zone, FrozenKey, FrozenValue, out string _,
				out Failure) == KingdomPolityCleanupEvidenceProof.Exact || FailPhysical(Failure ??
					"cleanup intent did not survive exact writeback", out Failure);
		}

		private static KingdomPolityCleanupEvidenceProof TryProveCleanupIntent(Zone Zone,
			string RealmId, string CohortId, string ProjectionId, string ObjectId, int Ordinal,
			byte CohortPhase, byte ProjectionPhase, out Cell FrozenCell, out string FrozenKey,
			out string FrozenValue, out string Failure)
		{
			FrozenCell = null; FrozenValue = null; Failure = null;
			FrozenKey = KingdomPolityPhysicalCustodyRules.CleanupIntentKey(ProjectionId, ObjectId);
			KingdomPolityCleanupEvidenceProof raw = InspectUniqueRawZoneSlot(Zone, FrozenKey,
				null, out string actual, out Failure);
			if (raw != KingdomPolityCleanupEvidenceProof.Exact) return raw;
			if (Zone.Width <= 0 || Zone.Height <= 0 ||
				(long)Zone.Width * Zone.Height > MaximumCleanupIntentCells)
			{
				Failure = "cleanup intent ground is unscannable";
				return KingdomPolityCleanupEvidenceProof.Unscannable;
			}
			int matches = 0;
			for (int y = 0; y < Zone.Height; y++) for (int x = 0; x < Zone.Width; x++)
			{
				string expected = KingdomPolityPhysicalCustodyRules.PreparedCleanupIntent(RealmId,
					CohortId, ProjectionId, Zone.ZoneID, ObjectId, Ordinal, x, y, CohortPhase,
					ProjectionPhase);
				if (!string.Equals(actual, expected, StringComparison.Ordinal)) continue;
				matches++; FrozenCell = Zone.GetCell(x, y); FrozenValue = expected;
			}
			if (matches == 1 && FrozenCell != null) return KingdomPolityCleanupEvidenceProof.Exact;
			FrozenCell = null; FrozenValue = null;
			Failure = "cleanup intent slot contains foreign or ambiguous authority";
			return KingdomPolityCleanupEvidenceProof.Ambiguous;
		}

		private static KingdomPolityCleanupEvidenceProof TryProveExactCleanupIntent(Zone Zone,
			Cell Cell, string RealmId, string CohortId, string ProjectionId, string ObjectId,
			int Ordinal, byte CohortPhase, byte ProjectionPhase, string FrozenKey,
			string FrozenValue, out string Failure)
		{
			Failure = null;
			string key = KingdomPolityPhysicalCustodyRules.CleanupIntentKey(ProjectionId, ObjectId);
			string expected = Zone == null || Cell == null ? null :
				KingdomPolityPhysicalCustodyRules.PreparedCleanupIntent(RealmId, CohortId,
					ProjectionId, Zone.ZoneID, ObjectId, Ordinal, Cell.X, Cell.Y, CohortPhase,
					ProjectionPhase);
			if (Zone == null || !ReferenceEquals(Cell.ParentZone, Zone) || FrozenKey != key ||
				FrozenValue != expected) return KingdomPolityCleanupEvidenceProof.Ambiguous;
			return InspectUniqueRawZoneSlot(Zone, FrozenKey, FrozenValue, out string _, out Failure);
		}

		private static bool TryClearCleanupIntent(Zone Zone, Cell Cell, string RealmId,
			string CohortId, string ProjectionId, string ObjectId, int Ordinal, byte CohortPhase,
			byte ProjectionPhase, string FrozenKey, string FrozenValue, out string Failure)
		{
			Failure = null;
			KingdomPolityCleanupEvidenceProof intent = TryProveExactCleanupIntent(Zone, Cell,
				RealmId, CohortId, ProjectionId, ObjectId, Ordinal, CohortPhase, ProjectionPhase,
				FrozenKey, FrozenValue, out Failure);
			KingdomPolityCleanupEvidenceProof witness = TryProveRemovalWitness(Zone,
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, CohortId,
				ProjectionId, ObjectId, Ordinal, out string _, out Failure);
			if (!KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(intent, witness))
				return FailPhysical(Failure ??
					"cleanup intent lacks exact final removal evidence", out Failure);
			if (!TryRemoveExactRawZoneSlot(Zone, FrozenKey, FrozenValue, out Failure)) return false;
			intent = InspectUniqueRawZoneSlot(Zone, FrozenKey, null, out string _, out Failure);
			witness = TryProveRemovalWitness(Zone,
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, CohortId,
				ProjectionId, ObjectId, Ordinal, out string _, out Failure);
			return KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(intent,
				witness) || FailPhysical(Failure ??
					"cleanup intent clear lost exact final removal evidence", out Failure);
		}

		private static KingdomPolityCleanupEvidenceProof InspectUniqueRawZoneSlot(Zone Zone,
			string Key, string Expected, out string Actual, out string Failure)
		{
			Actual = null; Failure = null;
			try
			{
				Dictionary<string, Dictionary<string, object>> all = The.ZoneManager?.ZoneProperties;
				if (Zone == null || string.IsNullOrEmpty(Zone.ZoneID) ||
					string.IsNullOrEmpty(Key) || all == null ||
					all.Count > MaximumCleanupPropertyZones)
				{
					Failure = "zone property authority is unscannable";
					return KingdomPolityCleanupEvidenceProof.Unscannable;
				}
				int matches = 0; bool ExactString = false, exactValue = false;
				foreach (KeyValuePair<string, Dictionary<string, object>> row in all)
				{
					if (row.Value == null)
					{
						Failure = "zone property authority is unscannable";
						return KingdomPolityCleanupEvidenceProof.Unscannable;
					}
					if (!row.Value.TryGetValue(Key, out object raw)) continue;
					matches++; Actual = raw as string; ExactString = raw is string;
					exactValue = row.Key == Zone.ZoneID && ExactString &&
						(Expected == null || string.Equals(Actual, Expected, StringComparison.Ordinal));
				}
				KingdomPolityCleanupEvidenceProof proof =
					KingdomPolityPhysicalCustodyRules.ClassifyCleanupEvidence(true, matches,
						ExactString, exactValue);
				if (proof == KingdomPolityCleanupEvidenceProof.Ambiguous)
					Failure = "zone property slot contains foreign or ambiguous authority";
				return proof;
			}
			catch (Exception ex)
			{
				Failure = "zone property inspection failed: " + ex.Message;
				return KingdomPolityCleanupEvidenceProof.Unscannable;
			}
		}

		private static bool TryRemoveExactRawZoneSlot(Zone Zone, string Key, string Expected,
			out string Failure)
		{
			Failure = null;
			try
			{
				if (Zone == null || string.IsNullOrEmpty(Zone.ZoneID) ||
					string.IsNullOrEmpty(Key) || Expected == null ||
					The.ZoneManager?.ZoneProperties == null ||
					!The.ZoneManager.ZoneProperties.TryGetValue(Zone.ZoneID,
					out Dictionary<string, object> properties) || properties == null ||
					!((ICollection<KeyValuePair<string, object>>)properties).Remove(
						new KeyValuePair<string, object>(Key, Expected)))
					return FailPhysical("exact zone property changed before conditional clear", out Failure);
				return true;
			}
			catch (Exception ex) { return FailPhysical("exact zone property clear failed: " +
				ex.Message, out Failure); }
		}

		private static bool HasBodyRemovalWitness(Zone Zone, string RealmId, string CohortId,
			string ProjectionId, string ObjectId, int Ordinal)
		{
			return HasRemovalWitness(Zone, KingdomPolityPhysicalCustodyRules.DeathRemovalKind,
				RealmId, CohortId, ProjectionId, ObjectId, Ordinal) ||
				HasRemovalWitness(Zone, KingdomPolityPhysicalCustodyRules.CleanupRemovalKind,
					RealmId, CohortId, ProjectionId, ObjectId, Ordinal);
		}

		private static bool TryClearRemovalWitness(Zone Zone, string RealmId, string CohortId,
			string ProjectionId, string ObjectId, int Ordinal, bool Gear, out string Failure)
		{
			Failure = null;
			if (Zone == null) return FailPhysical("removal witness ground disappeared", out Failure);
			string key = KingdomPolityPhysicalCustodyRules.RemovalWitnessKey(ProjectionId, ObjectId);
			KingdomPolityCleanupEvidenceProof proof = InspectUniqueRawZoneSlot(Zone, key, null,
				out string actual, out Failure);
			if (proof == KingdomPolityCleanupEvidenceProof.Absent) return true;
			string cleanup = KingdomPolityPhysicalCustodyRules.RemovalWitness(Gear ?
				KingdomPolityPhysicalCustodyRules.GearRemovalKind :
				KingdomPolityPhysicalCustodyRules.CleanupRemovalKind, RealmId, CohortId,
				ProjectionId, Zone.ZoneID, ObjectId, Ordinal);
			string death = Gear ? null : KingdomPolityPhysicalCustodyRules.RemovalWitness(
				KingdomPolityPhysicalCustodyRules.DeathRemovalKind, RealmId, CohortId,
				ProjectionId, Zone.ZoneID, ObjectId, Ordinal);
			if (proof != KingdomPolityCleanupEvidenceProof.Exact ||
				actual != cleanup && actual != death) return FailPhysical(Failure ??
					"foreign removal witness was preserved after cohort cleanup", out Failure);
			if (!TryRemoveExactRawZoneSlot(Zone, key, actual, out Failure)) return false;
			return InspectUniqueRawZoneSlot(Zone, key, null, out string _, out Failure) ==
				KingdomPolityCleanupEvidenceProof.Absent || FailPhysical(Failure ??
					"exact removal witness survived cleanup", out Failure);
		}

		private static bool FailPhysical(string Reason, out string Failure)
		{
			Failure = Reason; return false;
		}
	}
}
