using System;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomWitnessWorkProjectionRuntime
	{
		internal static bool TryObserve(string RealmId, KingdomWitnessWorkReceipt Receipt,
			Zone LoadedZone, KingdomSurvey Survey, out KingdomWitnessCarrierObservation Observation,
			out GameObject Carrier, out string Failure)
		{
			Observation = KingdomWitnessCarrierObservation.Diverged;
			Carrier = null;
			if (!ValidateReadback(RealmId, Receipt, LoadedZone, Survey,
				out string raw, out bool outside, out Failure)) return false;
			if (outside)
			{
				Observation = KingdomWitnessCarrierObservation.OutsideRecordedZone;
				return true;
			}
			if (!TryFindUnique(Survey, raw, out Carrier, out bool ambiguous))
			{
				if (ambiguous)
				{
					Observation = KingdomWitnessCarrierObservation.Ambiguous;
					Failure = "More than one loaded object claims the witness carrier identity.";
					return false;
				}
				Observation = KingdomWitnessCarrierObservation.Missing;
				return true;
			}
			r_KingdomWitnessWorkProjection marker =
				Carrier.GetPart<r_KingdomWitnessWorkProjection>();
			if (!EligibleSurface(Carrier, Survey, true, out Failure)
				|| !CarrierMatchesReceipt(Carrier, Receipt, out Failure)
				|| !MarkerMatches(RealmId, Receipt, Carrier, marker, out Failure)
				|| !PhysicalContract(Carrier, out Failure))
			{
				Observation = KingdomWitnessCarrierObservation.Diverged;
				return false;
			}
			Observation = KingdomWitnessCarrierObservation.Present;
			return true;
		}

		/// <summary>Removes only this adapter's exact marker. Foreign changes to the carrier do not
		/// become ours to restore or veto because this projection never owned those fields.</summary>
		internal static bool TryDetach(string RealmId, KingdomWitnessWorkReceipt Receipt,
			Zone LoadedZone, KingdomSurvey Survey, out string Failure)
		{
			if (!ValidateReadback(RealmId, Receipt, LoadedZone, Survey,
				out string raw, out bool outside, out Failure)) return false;
			if (outside)
			{
				Failure = "Load the witness work's recorded zone before removing its projection.";
				return false;
			}
			if (!TryFindUnique(Survey, raw, out GameObject carrier, out bool ambiguous))
			{
				if (!ambiguous) return true;
				Failure = "Duplicate physical identity prevents exact witness projection removal.";
				return false;
			}
			r_KingdomWitnessWorkProjection marker =
				carrier.GetPart<r_KingdomWitnessWorkProjection>();
			if (marker == null) return true;
			if (!MarkerOwnsReceipt(RealmId, Receipt, carrier, marker))
			{
				Failure = "No exact witness marker owned by this receipt can be removed.";
				return false;
			}
			try { carrier.RemovePart(marker); }
			catch (Exception error)
			{
				Failure = "The witness projection could not be removed ("
					+ error.GetType().Name + ").";
				return false;
			}
			if (carrier.GetPart<r_KingdomWitnessWorkProjection>() != null)
			{
				Failure = "The witness projection remained after exact removal.";
				return false;
			}
			return true;
		}

		private static bool ValidateReadback(string RealmId, KingdomWitnessWorkReceipt Receipt,
			Zone LoadedZone, KingdomSurvey Survey, out string Raw, out bool Outside,
			out string Failure)
		{
			Raw = null;
			Outside = false;
			Failure = null;
			if (!ValidRealm(RealmId) || !ValidReceipt(Receipt) || LoadedZone == null
				|| Survey == null || !ReferenceEquals(Survey.Ground, LoadedZone)
				|| !TryRawObjectId(Receipt.CarrierObjectId, out Raw))
			{
				Failure = "Witness-work readback authority is invalid.";
				return false;
			}
			Outside = Receipt.CarrierZoneId != ZonePrefix + LoadedZone.ZoneID;
			return true;
		}

		private static bool TryFindUnique(KingdomSurvey Survey, string Raw,
			out GameObject Carrier, out bool Ambiguous)
		{
			Carrier = null;
			Ambiguous = false;
			for (int i = 0; i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				if (!GameObject.Validate(item) || item.IDIfAssigned != Raw) continue;
				if (Carrier != null)
				{
					Carrier = null;
					Ambiguous = true;
					return false;
				}
				Carrier = item;
			}
			return Carrier != null;
		}

		private static bool MarkerOwnsReceipt(string RealmId, KingdomWitnessWorkReceipt Receipt,
			GameObject Carrier, r_KingdomWitnessWorkProjection Marker)
		{
			return Marker != null && ReferenceEquals(Marker.ParentObject, Carrier)
				&& Marker.FieldsAuthenticated()
				&& Marker.ProjectionVersion == r_KingdomWitnessWorkProjection.CurrentVersion
				&& Marker.RealmId == RealmId
				&& Marker.SettlementId == Receipt.Source.SettlementId
				&& Marker.WorkId == Receipt.WorkId
				&& Marker.SourceSnapshotDigest == Receipt.Source.SnapshotDigest
				&& Marker.CarrierReceiptId == Receipt.CarrierReceiptId
				&& Marker.CarrierObjectId == Receipt.CarrierObjectId
				&& Marker.CarrierEngineId == Carrier.IDIfAssigned
				&& Marker.CarrierZoneId == Receipt.CarrierZoneId
				&& Marker.CarrierConstructionReceiptId
					== Receipt.CarrierConstructionReceiptId
				&& Marker.CarrierX == Receipt.CarrierX && Marker.CarrierY == Receipt.CarrierY
				&& Marker.ProjectedDescription == Receipt.Description;
		}
	}
}
