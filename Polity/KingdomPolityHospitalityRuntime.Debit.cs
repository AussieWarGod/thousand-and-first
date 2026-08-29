using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityHospitalityRuntime
	{
		private enum DebitObservation : byte { Before = 1, After = 2, Invalid = 3 }

		private static bool TryDrive(KingdomSystem System,
			KingdomPolityHospitalityTransaction Transaction,
			out KingdomPolityHospitalityProof Proof, out string Failure)
		{
			Proof = null;
			Failure = null;
			Zone zone = The.Player?.CurrentZone;
			if (zone == null || zone.ZoneID != Transaction.ZoneId ||
				System.City?.SettlementId != Transaction.SurfaceRef)
				return Fail("hospitality debit is retained for its exact loaded endpoint", out Failure);
			for (int pass = 0; pass <= Transaction.Lines.Count; pass++)
			{
				Transaction = KingdomPolityHospitalityRules.FindIncident(System.PolityLedger,
					Transaction.TermsPlanId)?.Hospitality;
				if (Transaction == null ||
					Transaction.Phase != KingdomPolityHospitalityPhase.Planned)
					return Fail("hospitality debit changed while it was being reconciled", out Failure);
				int next = -1;
				for (int i = 0; i < Transaction.Lines.Count; i++)
				{
					DebitObservation state = Observe(Transaction, Transaction.Lines[i], zone);
					if (state == DebitObservation.Invalid)
						return Quarantine(System, Transaction,
							"An exact hospitality serving changed outside its frozen debit.",
							out Failure);
					if (state == DebitObservation.Before && next < 0) next = i;
				}
				if (next < 0)
				{
					if (!KingdomPolityHospitalityRules.TryCreateCommittedProof(Transaction,
						Witness(Transaction), Transaction.PlannedTick, out Proof, out Failure))
						return false;
					return KingdomPolityHospitalityRules.TryCommitDebit(System.PolityLedger,
						System.PolityLedger.Revision, Transaction.TermsPlanId, Proof,
						Transaction.PlannedTick, out KingdomPolityPublicationResult _, out Failure);
				}
				if (!Apply(Transaction, Transaction.Lines[next], zone, out Failure))
					return Quarantine(System, Transaction, Failure, out Failure);
			}
			return Quarantine(System, Transaction,
				"Hospitality debit exceeded its two-line work bound.", out Failure);
		}

		private static bool Quarantine(KingdomSystem System,
			KingdomPolityHospitalityTransaction Transaction, string Reason, out string Failure)
		{
			string fault = KingdomPolityRules.Text(Reason, true) ? Reason :
				"Hospitality exact debit requires inspection.";
			if (!KingdomPolityHospitalityRules.TryQuarantineDebit(System.PolityLedger,
				System.PolityLedger.Revision, Transaction.TermsPlanId, fault,
				out KingdomPolityPublicationResult _, out string quarantineFailure))
				return Fail(quarantineFailure ?? fault, out Failure);
			return Fail(fault + " Ordinary diplomacy remains available.", out Failure);
		}

		private static DebitObservation Observe(KingdomPolityHospitalityTransaction T,
			KingdomPolityHospitalityDebitLine Line, Zone Zone)
		{
			if (!TryFindExact(Line.ObjectId, out GameObject item, out bool graveyard) ||
				!GameObject.Validate(item) || item.Blueprint != Line.Blueprint)
				return DebitObservation.Invalid;
			string owner = item.GetStringProperty(OwnerProperty);
			string digest = item.GetStringProperty(DigestProperty);
			bool unbound = string.IsNullOrEmpty(owner) && string.IsNullOrEmpty(digest);
			if (!unbound && !Owned(item, T)) return DebitObservation.Invalid;
			int amount;
			if (Line.Kind == KingdomPolityHospitalityDebitKind.Food)
			{
				if (graveyard)
					return Line.After == 0 && Owned(item, T)
						? DebitObservation.After : DebitObservation.Invalid;
				if (item.InInventory == null || item.InInventory.IDIfAssigned != Line.ContainerId ||
					item.InInventory.CurrentZone != Zone ||
					(!item.HasPart("Food") && !item.HasPart("PreparedCookingIngredient")))
					return DebitObservation.Invalid;
				amount = item.Count;
			}
			else
			{
				LiquidVolume liquid = item.GetPart<LiquidVolume>();
				if (graveyard || item.IDIfAssigned != Line.ContainerId || item.CurrentZone != Zone ||
					item.GetIntProperty("KingdomStores") != 1 || liquid == null ||
					liquid.MaxVolume != Line.Capacity || !KingdomLiquids.HasFreshWater(liquid))
					return DebitObservation.Invalid;
				amount = liquid.Volume;
			}
			if (amount == Line.Before) return DebitObservation.Before;
			if (amount == Line.After && Owned(item, T)) return DebitObservation.After;
			return DebitObservation.Invalid;
		}

		private static bool Apply(KingdomPolityHospitalityTransaction T,
			KingdomPolityHospitalityDebitLine Line, Zone Zone, out string Failure)
		{
			Failure = null;
			if (Observe(T, Line, Zone) != DebitObservation.Before ||
				!TryFindExact(Line.ObjectId, out GameObject item, out bool graveyard) || graveyard)
				return Fail("The next hospitality debit line left its exact before-state.", out Failure);
			if (!Bind(item, T) || Observe(T, Line, Zone) != DebitObservation.Before)
				return Fail("The hospitality serving carries foreign ownership evidence.", out Failure);
			if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
				item, out Failure)) return false;
			try
			{
				if (Line.Kind == KingdomPolityHospitalityDebitKind.Food)
				{
					GameObject container = item.InInventory;
					item.Destroy(null, Silent: true);
					KingdomSurvey.ObserveChangedInActive(Zone, container);
				}
				else
				{
					if (KingdomLiquids.Drain(item.GetPart<LiquidVolume>(), 1) != 1)
						return Fail("The hospitality vessel did not yield one exact dram.", out Failure);
					KingdomSurvey.ObserveChangedInActive(Zone, item);
				}
			}
			catch (Exception ex)
			{
				return Fail("Hospitality debit callback threw: " + ex.Message, out Failure);
			}
			return Observe(T, Line, Zone) == DebitObservation.After ||
				Fail("Hospitality debit reached an ambiguous physical aftermath.", out Failure);
		}

		private static bool Bind(GameObject Item, KingdomPolityHospitalityTransaction T)
		{
			if (!GameObject.Validate(Item)) return false;
			string owner = Item.GetStringProperty(OwnerProperty);
			string digest = Item.GetStringProperty(DigestProperty);
			if (string.IsNullOrEmpty(owner) && string.IsNullOrEmpty(digest))
			{
				Item.SetStringProperty(OwnerProperty, T.TransactionId);
				Item.SetStringProperty(DigestProperty, T.PlanDigest);
			}
			return Owned(Item, T);
		}

		private static bool Owned(GameObject Item, KingdomPolityHospitalityTransaction T)
		{
			return GameObject.Validate(Item) && Item.GetStringProperty(OwnerProperty) ==
				T.TransactionId && Item.GetStringProperty(DigestProperty) == T.PlanDigest;
		}

		private static bool TryFindExact(string Id, out GameObject Object, out bool Graveyard)
		{
			Object = null;
			Graveyard = false;
			if (string.IsNullOrEmpty(Id) || The.ZoneManager == null) return false;
			HashSet<GameObject> found = new HashSet<GameObject>();
			HashSet<Zone> zones = new HashSet<Zone>();
			if (The.ZoneManager.ActiveZone != null) zones.Add(The.ZoneManager.ActiveZone);
			if (The.ZoneManager.CachedZones != null)
				foreach (Zone zone in The.ZoneManager.CachedZones.Values)
					if (zone != null) zones.Add(zone);
			foreach (Zone zone in zones)
			{
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(zone, Id,
					out GameObject candidate);
				if (state == KingdomPhysicalLookupState.Ambiguous) return false;
				if (state == KingdomPhysicalLookupState.Exact) found.Add(candidate);
			}
			if (The.ZoneManager.Graveyard?.Objects != null)
				for (int i = 0; i < The.ZoneManager.Graveyard.Objects.Count; i++)
				{
					GameObject candidate = The.ZoneManager.Graveyard.Objects[i];
					if (candidate != null && candidate.IDIfAssigned == Id) found.Add(candidate);
				}
			if (found.Count != 1) return false;
			foreach (GameObject candidate in found) Object = candidate;
			Graveyard = Object.IsInGraveyard();
			return true;
		}
	}
}
