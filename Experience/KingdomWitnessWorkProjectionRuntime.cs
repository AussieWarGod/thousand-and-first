using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal enum KingdomWitnessCarrierObservation : byte
	{
		OutsideRecordedZone = 0,
		Missing = 1,
		Present = 2,
		Ambiguous = 3,
		Diverged = 4
	}

	/// <summary>Physical adapter for an already-prepared witness-work receipt. It neither captures
	/// source events nor commits semantic state. Qud 2.0.211.51 evidence: GameObject.cs 424-451
	/// (assigned exact ID), 517-532 (cell/zone), 565-592 (intrinsic then extrinsic value), and
	/// 8945-8952 (takeability); Physics.cs 136-146 (base takeable flag); base
	/// ObjectBlueprints/PhysicalPhenomena.xml 20-25 gives PhysicalObject Commerce 0.01.</summary>
	internal static partial class KingdomWitnessWorkProjectionRuntime
	{
		private const string ObjectPrefix = "taf:object:";
		private const string ZonePrefix = "taf:zone:";

		internal static bool TryCarrierIdentity(GameObject Carrier, KingdomSurvey Survey,
			out string ObjectId, out string ZoneId, out string Failure)
		{
			ObjectId = null;
			ZoneId = null;
			if (!EligibleSurface(Carrier, Survey, false, out Failure)) return false;
			if (Carrier.GetPart<r_KingdomWitnessWorkProjection>() != null)
			{
				Failure = "That surface already carries an exact witness work.";
				return false;
			}
			string engineId = Carrier.IDIfAssigned;
			if (string.IsNullOrEmpty(engineId) || engineId.IndexOf('\0') >= 0)
			{
				Failure = "The completed witness surface has no pre-existing exact identity.";
				return false;
			}
			ObjectId = ObjectPrefix + engineId;
			ZoneId = ZonePrefix + Carrier.CurrentZone.ZoneID;
			if (!TypedId(ObjectId) || !TypedId(ZoneId))
			{
				ObjectId = null;
				ZoneId = null;
				Failure = "The witness surface's exact identity exceeds the bounded receipt.";
				return false;
			}
			return true;
		}

		internal static bool TryAttachPrepared(string RealmId,
			KingdomWitnessWorkReceipt Receipt, GameObject Carrier, KingdomSurvey Survey,
			out r_KingdomWitnessWorkProjection Marker, out string Failure)
		{
			Marker = null;
			if (!ValidRealm(RealmId) || !ValidReceipt(Receipt)
				|| Receipt.Phase != KingdomWitnessWorkPhase.CarrierPrepared)
			{
				Failure = "Prepared witness-work authority is invalid.";
				return false;
			}
			if (!EligibleSurface(Carrier, Survey, true, out Failure)
				|| !CarrierMatchesReceipt(Carrier, Receipt, out Failure)) return false;
			Marker = Carrier.GetPart<r_KingdomWitnessWorkProjection>();
			if (Marker != null)
				return MarkerMatches(RealmId, Receipt, Carrier, Marker, out Failure)
					&& PhysicalContract(Carrier, out Failure);
			Marker = new r_KingdomWitnessWorkProjection
			{
				RealmId = RealmId,
				SettlementId = Receipt.Source.SettlementId,
				WorkId = Receipt.WorkId,
				SourceSnapshotDigest = Receipt.Source.SnapshotDigest,
				CarrierReceiptId = Receipt.CarrierReceiptId,
				CarrierObjectId = Receipt.CarrierObjectId,
				CarrierEngineId = Carrier.IDIfAssigned,
				CarrierZoneId = Receipt.CarrierZoneId,
				CarrierConstructionReceiptId = Receipt.CarrierConstructionReceiptId,
				CarrierX = Receipt.CarrierX,
				CarrierY = Receipt.CarrierY,
				ProjectedDescription = Receipt.Description
			};
			Marker.ProjectionProof = KingdomWitnessWorkRules.ProjectionProof(
				Marker.ProjectionVersion, Marker.RealmId, Marker.SettlementId, Marker.WorkId,
				Marker.SourceSnapshotDigest, Marker.CarrierReceiptId, Marker.CarrierObjectId,
				Marker.CarrierEngineId, Marker.CarrierZoneId,
				Marker.CarrierConstructionReceiptId, Marker.CarrierX, Marker.CarrierY,
				Marker.ProjectedDescription);
			try { Carrier.AddPart(Marker); }
			catch (Exception error)
			{
				Failure = "The witness projection could not be attached ("
					+ error.GetType().Name + ").";
				Marker = null;
				return false;
			}
			if (MarkerMatches(RealmId, Receipt, Carrier, Marker, out Failure)
				&& PhysicalContract(Carrier, out Failure)) return true;
			if (ReferenceEquals(Carrier.GetPart<r_KingdomWitnessWorkProjection>(), Marker))
			{
				try { Carrier.RemovePart(Marker); }
				catch (Exception error)
				{
					Failure = (Failure ?? "Witness projection readback failed.")
						+ " Exact marker cleanup threw " + error.GetType().Name + ".";
				}
			}
			Marker = null;
			return false;
		}

		private static bool EligibleSurface(GameObject Carrier, KingdomSurvey Survey,
			bool RequireAssignedId, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Carrier) || Survey == null || Carrier.CurrentCell == null
				|| Carrier.CurrentZone == null
				|| !ReferenceEquals(Survey.Ground, Carrier.CurrentZone)
				|| !KingdomUpgrade.IsFunctionallyBuilt(Carrier)
				|| !SupportsFixture(Carrier.Blueprint) || Carrier.Physics == null
				|| Carrier.Physics.Takeable || Carrier.Count != 1
				|| Carrier.GetPart<Description>() == null || !NativeCommerceSurface(Carrier)
				|| Carrier.Inventory != null)
			{
				Failure = "Only one completed, fixed, empty civic witness surface is eligible.";
				return false;
			}
			if (RequireAssignedId && string.IsNullOrEmpty(Carrier.IDIfAssigned))
			{
				Failure = "The witness surface has no prepared physical identity.";
				return false;
			}
			int references = 0;
			for (int i = 0; i < Survey.Cairns.Count; i++)
				if (ReferenceEquals(Carrier, Survey.Cairns[i])) references++;
			if (references != 1 || Carrier.HasStringProperty(
				KingdomRemembranceRuntime.MemorialForProperty)
				|| Carrier.GetPart<r_KingdomRemembranceProjection>() != null
				|| Carrier.GetPart<r_KingdomOfficeProjection>() != null)
			{
				Failure = "The surface is not independently owned and unlinked.";
				return false;
			}
			return true;
		}

		private static bool CarrierMatchesReceipt(GameObject Carrier,
			KingdomWitnessWorkReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!TryRawObjectId(Receipt.CarrierObjectId, out string raw)
				|| Carrier.IDIfAssigned != raw
				|| Receipt.CarrierZoneId != ZonePrefix + Carrier.CurrentZone.ZoneID
				|| Receipt.CarrierX != Carrier.CurrentCell.X
				|| Receipt.CarrierY != Carrier.CurrentCell.Y
				|| Receipt.CarrierConstructionReceiptId != "taf:construction:"
					+ (Carrier.GetStringProperty(KingdomConstruction.ReceiptProperty) ?? ""))
			{
				Failure = "The prepared receipt names another physical carrier or zone.";
				return false;
			}
			return true;
		}

		private static bool MarkerMatches(string RealmId, KingdomWitnessWorkReceipt Receipt,
			GameObject Carrier, r_KingdomWitnessWorkProjection Marker, out string Failure)
		{
			Failure = null;
			if (Marker == null || !ReferenceEquals(Marker.ParentObject, Carrier)
				|| Marker.RealmId != RealmId || Marker.SettlementId != Receipt.Source.SettlementId
				|| Marker.WorkId != Receipt.WorkId
				|| Marker.SourceSnapshotDigest != Receipt.Source.SnapshotDigest
				|| Marker.CarrierReceiptId != Receipt.CarrierReceiptId
				|| Marker.CarrierObjectId != Receipt.CarrierObjectId
				|| Marker.CarrierZoneId != Receipt.CarrierZoneId
				|| Marker.CarrierConstructionReceiptId
					!= Receipt.CarrierConstructionReceiptId
				|| Marker.CarrierX != Receipt.CarrierX || Marker.CarrierY != Receipt.CarrierY
				|| Marker.ProjectedDescription != Receipt.Description || !Marker.ShapeMatchesParent())
			{
				Failure = "The object-local witness proof diverged from semantic authority.";
				return false;
			}
			return true;
		}

		private static bool PhysicalContract(GameObject Carrier, out string Failure)
		{
			Failure = null;
			try
			{
				if (Carrier == null || Carrier.IsTakeable() || Carrier.ValueEach != 0.0)
				{
					Failure = "The witness surface is portable or has nonzero commerce value.";
					return false;
				}
			}
			catch (Exception error)
			{
				Failure = "Witness surface readback threw " + error.GetType().Name + ".";
				return false;
			}
			return true;
		}

		private static bool NativeCommerceSurface(GameObject Carrier)
		{
			Commerce commerce = Carrier?.GetPart<Commerce>();
			return commerce == null || commerce.Value == 0.0 || commerce.Value == 0.01;
		}

		private static bool ValidReceipt(KingdomWitnessWorkReceipt Receipt)
		{
			if (Receipt == null || Receipt.Phase < KingdomWitnessWorkPhase.CarrierPrepared
				|| Receipt.Phase > KingdomWitnessWorkPhase.Lost) return false;
			KingdomWitnessWorkBook probe = new KingdomWitnessWorkBook
				{ Rows = new List<KingdomWitnessWorkReceipt> { Receipt } };
			return KingdomWitnessWorkRules.TryValidate(probe, out string _);
		}

		private static bool ValidRealm(string RealmId)
		{
			return TypedId(RealmId);
		}

		private static bool TryRawObjectId(string ObjectId, out string Raw)
		{
			Raw = null;
			if (!TypedId(ObjectId) || !ObjectId.StartsWith(ObjectPrefix,
				StringComparison.Ordinal) || ObjectId.Length == ObjectPrefix.Length) return false;
			Raw = ObjectId.Substring(ObjectPrefix.Length);
			return Raw.IndexOf('\0') < 0;
		}

		internal static bool SupportsFixture(string Blueprint)
		{
			return Blueprint == "r_KingdomCairn" || Blueprint == "r_KingdomGraveGrove"
				|| Blueprint == "r_KingdomNicheTomb";
		}
	}
}
