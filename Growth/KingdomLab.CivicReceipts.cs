using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomLabCivicRuntime
	{
		private static bool CanStampMarker(GameObject Resident,
			KingdomLabCivicReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Resident) || Receipt == null)
				return Fail("The exact resident marker carrier is missing.", out Failure);
			string eventId = Resident.GetStringProperty(RefusalEventProperty);
			string owner = Resident.GetStringProperty(RefusalOwnerProperty);
			string digest = Resident.GetStringProperty(RefusalDigestProperty);
			bool empty = string.IsNullOrEmpty(eventId) && string.IsNullOrEmpty(owner)
				&& string.IsNullOrEmpty(digest);
			return empty || eventId == Receipt.EventId && owner == Receipt.OwnerObjectId
				&& digest == Receipt.CauseDigest
				|| Fail("A different or partial laboratory cause marker is already present.", out Failure);
		}

		private static void StampMarker(GameObject Resident, KingdomLabCivicReceipt Receipt)
		{
			Resident.SetStringProperty(RefusalEventProperty, Receipt.EventId);
			Resident.SetStringProperty(RefusalOwnerProperty, Receipt.OwnerObjectId);
			Resident.SetStringProperty(RefusalDigestProperty, Receipt.CauseDigest);
		}

		private static bool MarkerMatches(GameObject Resident, KingdomLabCivicReceipt Receipt)
		{
			return Resident != null && Receipt != null
				&& Resident.GetStringProperty(RefusalEventProperty) == Receipt.EventId
				&& Resident.GetStringProperty(RefusalOwnerProperty) == Receipt.OwnerObjectId
				&& Resident.GetStringProperty(RefusalDigestProperty) == Receipt.CauseDigest;
		}

		private static void Close(KingdomSystem System, Zone Z,
			r_KingdomLabCivicFriction Part, KingdomLabCivicReceipt Before,
			KingdomLabCivicClosure Closure, GameObject Resident)
		{
			if (!KingdomLabCivicRules.TryClose(Before, Closure, Now(),
				out KingdomLabCivicReceipt after, out string failure))
			{
				Part.Stamp(KingdomLabCivicRules.Quarantine(Before, failure)); return;
			}
			Part.Stamp(after);
			if (after.Kind == KingdomLabCivicKind.RefusalDeparture
				&& !TryCompleteClosedDeparture(System, Z, after, Resident, out failure))
			{
				KingdomLog.Log("lab civic terminal cleanup remains retryable: " + failure);
				return;
			}
			RecordClose(System, Part, after.Kind);
		}

		private static void RecordOpen(KingdomSystem System,
			r_KingdomLabCivicFriction Part, KingdomLabCivicKind Kind)
		{
			KingdomLabCivicReceipt receipt = Part?.Receipt(Kind);
			if (receipt == null || receipt.OpenRecorded || receipt.Kind == KingdomLabCivicKind.None)
				return;
			if (KingdomChronicle.RecordOnce(System, receipt.EventId,
				KingdomLabCivicRules.CauseLine(receipt) + " "
					+ KingdomLabCivicRules.RequestLine(receipt)))
			{
				KingdomLabCivicReceipt next = receipt.Copy(); next.OpenRecorded = true;
				Part.Stamp(next);
			}
		}

		private static void RecordClose(KingdomSystem System,
			r_KingdomLabCivicFriction Part, KingdomLabCivicKind Kind)
		{
			KingdomLabCivicReceipt receipt = Part?.Receipt(Kind);
			if (receipt == null || receipt.Phase != KingdomLabCivicPhase.Closed
				|| receipt.CloseRecorded) return;
			if (KingdomChronicle.RecordOnce(System, receipt.EventId + ":closed",
				KingdomLabCivicRules.ClosureLine(receipt)))
			{
				KingdomLabCivicReceipt next = receipt.Copy(); next.CloseRecorded = true;
				Part.Stamp(next);
			}
		}

		internal static void OnOwnerRemoving(r_KingdomLabCivicFriction Part, string Reason)
		{
			GameObject owner = Part?.ParentObject;
			Zone zone = owner?.CurrentZone;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (owner == null || zone == null || system == null) return;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone) ?? KingdomSurvey.Take(zone);
			CloseForOwnerRemoval(system, zone, survey, Part, Part.SavantPrice);
			CloseForOwnerRemoval(system, zone, survey, Part, Part.RefusalDeparture);
			KingdomLabCivicOwnerRow expected = new KingdomLabCivicOwnerRow
			{
				RealmId = system.CurrentRealmId,
				SettlementId = system.SettlementIdForOwnedZone(zone.ZoneID),
				ZoneId = zone.ZoneID, OwnerObjectId = owner.ID
			};
			if (!ReleaseExact(expected, out string failure))
				KingdomLog.Log("lab civic owner removal: " + (failure ?? Reason));
		}

		private static void CloseForOwnerRemoval(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, r_KingdomLabCivicFriction Part,
			KingdomLabCivicReceipt Receipt)
		{
			if (!KingdomLabCivicRules.Valid(Receipt, out _)
				|| Receipt.Phase == KingdomLabCivicPhase.Closed
				|| Receipt.Phase == KingdomLabCivicPhase.Quarantined) return;
			GameObject resident = Receipt.Kind == KingdomLabCivicKind.RefusalDeparture
				? Survey.FindCitizen(Receipt.SubjectResidentId) : null;
			Close(System, Z, Part, Receipt, KingdomLabCivicClosure.OwnerGone, resident);
		}

		private static void ObserveMissingOwner(KingdomSystem System, Zone Z,
			KingdomLabCivicOwnerRow Owner)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			for (int i = 0; survey != null && i < survey.CitizenBodies.Count; i++)
			{
				GameObject resident = survey.CitizenBodies[i];
				if (resident.GetStringProperty(RefusalOwnerProperty) != Owner.OwnerObjectId) continue;
				bool warned = KingdomBrink.Of(resident, BrinkKind.Roof).Warned;
				if (KingdomBrink.Lift(resident, BrinkKind.Roof) && warned)
					KingdomBrink.Unsay(System, BrinkKind.Roof,
						resident.GetStringProperty("KingdomName"), KingdomWord.StandsIn(Z),
						System.SeatName);
				string eventId = resident.GetStringProperty(RefusalEventProperty);
				if (!string.IsNullOrEmpty(eventId)) KingdomChronicle.RecordOnce(System,
					eventId + ":closed", "The exact laboratory owner was gone on visited ground; "
						+ "its warned roof cause was arrested.");
				resident.SetStringProperty(RefusalEventProperty, null, RemoveIfNull: true);
				resident.SetStringProperty(RefusalOwnerProperty, null, RemoveIfNull: true);
				resident.SetStringProperty(RefusalDigestProperty, null, RemoveIfNull: true);
			}
		}
	}
}
