using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		private static KingdomLabDepartureProjection DepartureProjection(GameObject Resident,
			KingdomLabCivicReceipt Receipt)
		{
			return KingdomLabCivicRules.ClassifyDepartureProjection(Receipt,
				Resident?.GetStringProperty(KingdomLodging.HomePlotIdProperty),
				Resident?.GetStringProperty(RefusalEventProperty),
				Resident?.GetStringProperty(RefusalOwnerProperty),
				Resident?.GetStringProperty(RefusalDigestProperty));
		}

		private static bool TryCompleteDepartureProjection(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, GameObject Owner, r_KingdomLabCivicFriction Part,
			GameObject Resident, KingdomLabCivicReceipt Receipt, out string Failure)
		{
			Failure = null;
			KingdomLabDepartureProjection state = DepartureProjection(Resident, Receipt);
			if (state == KingdomLabDepartureProjection.Diverged)
				return Fail("The resident projection is neither exact nor safely recoverable.",
					out Failure);
			if (!ExactDepartureCause(System, Z, Survey, Owner, Resident, Receipt,
				out _, out Failure)) return false;
			if (state == KingdomLabDepartureProjection.RecoverableAtSource)
			{
				StampMarker(Resident, Receipt);
				if (!MarkerMatches(Resident, Receipt))
					return Fail("The exact resident marker did not publish completely.", out Failure);
				if (!ExactDepartureCause(System, Z, Survey, Owner, Resident, Receipt,
					out _, out Failure)
					|| !string.Equals(Resident.GetStringProperty(
						KingdomLodging.HomePlotIdProperty), Receipt.SourcePlotId,
						StringComparison.Ordinal)) return false;
				Resident.SetStringProperty(KingdomLodging.HomePlotIdProperty, null);
				if (!string.IsNullOrEmpty(Resident.GetStringProperty(
					KingdomLodging.HomePlotIdProperty)))
					return Fail("The exact old home did not clear; the durable cause remains retryable.",
						out Failure);
			}
			// Idempotent on both sides of the save cut after HomePlotId clears. A reload sees
			// an Active projection and still invalidates the derived cohabitation cache before
			// the existing roof brink is (re)opened.
			KingdomConversion.ForgetCohabitation(Resident);
			if (!KingdomBrink.Stands(Resident, BrinkKind.Roof))
				KingdomLodging.StartLabRoofBrink(System, Z, Resident, Receipt.RefusedTag,
					Owner.ShortDisplayName);
			RecordOpen(System, Part, Receipt.Kind);
			return true;
		}

		private static bool TryCompleteClosedDeparture(KingdomSystem System, Zone Z,
			KingdomLabCivicReceipt Receipt, GameObject Resident, out string Failure)
		{
			Failure = null;
			if (Resident == null) return true;
			if (!GameObject.Validate(Resident) || Resident.ID != Receipt.SubjectObjectId
				|| KingdomResidentsId(Resident) != Receipt.SubjectResidentId)
				return Fail("The terminal receipt cannot prove its resident marker carrier.",
					out Failure);
			string eventId = Resident.GetStringProperty(RefusalEventProperty);
			string ownerId = Resident.GetStringProperty(RefusalOwnerProperty);
			string digest = Resident.GetStringProperty(RefusalDigestProperty);
			if (!KingdomLabCivicRules.ClosedMarkerCleanupAllowed(Receipt,
				eventId, ownerId, digest))
				return Fail("A foreign resident marker blocks terminal cleanup.", out Failure);
			Resident.SetStringProperty(RefusalEventProperty, null, RemoveIfNull: true);
			Resident.SetStringProperty(RefusalOwnerProperty, null, RemoveIfNull: true);
			Resident.SetStringProperty(RefusalDigestProperty, null, RemoveIfNull: true);
			if (!string.IsNullOrEmpty(Resident.GetStringProperty(RefusalEventProperty))
				|| !string.IsNullOrEmpty(Resident.GetStringProperty(RefusalOwnerProperty))
				|| !string.IsNullOrEmpty(Resident.GetStringProperty(RefusalDigestProperty)))
				return Fail("The terminal resident marker cleanup did not read back empty.",
					out Failure);
			bool warned = KingdomBrink.Of(Resident, BrinkKind.Roof).Warned;
			bool lifted = KingdomBrink.Lift(Resident, BrinkKind.Roof);
			if (Receipt.Closure != KingdomLabCivicClosure.Departed && lifted && warned)
				KingdomBrink.Unsay(System, BrinkKind.Roof, Receipt.SubjectName,
					KingdomWord.StandsIn(Z), System.SeatName);
			return true;
		}

		private static void ReconcileClosedDeparture(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, r_KingdomLabCivicFriction Part,
			KingdomLabCivicReceipt Receipt)
		{
			GameObject resident = Survey?.FindCitizen(Receipt.SubjectResidentId);
			if (!GameObject.Validate(resident)) resident = null;
			if (resident == null && Receipt.Closure != KingdomLabCivicClosure.Departed
				&& TryResidentRow(System, Receipt.SubjectResidentId, out _))
			{
				KingdomLog.Log("lab civic: terminal cleanup awaits the exact active-ground resident.");
				return;
			}
			if (!TryCompleteClosedDeparture(System, Z, Receipt, resident,
				out string failure))
			{
				KingdomLog.Log("lab civic: " + failure); return;
			}
			RecordClose(System, Part, Receipt.Kind);
		}
	}
}
