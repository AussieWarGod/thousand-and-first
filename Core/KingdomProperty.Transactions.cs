using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomProperty
	{
		public static bool TryDesignate(KingdomSystem System, GameObject Founder,
			GameObject Item, out string Failure)
		{
			Failure = null;
			string realm;
			string settlement;
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.TryGetCurrentIdentity(out realm, out settlement))
			{
				Failure = KingdomPropertyRules.Refusal(KingdomPropertyVerdict.Unfounded);
				return false;
			}
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID)
				|| !StillNearby(Founder, Item, zone))
			{
				Failure = "That exact object is not beside the Charter bearer on held ground.";
				return false;
			}
			r_KingdomProperty existing = Item?.GetPart<r_KingdomProperty>();
			if (existing != null)
			{
				if (existing.Phase != KingdomPropertyPhase.Prepared)
				{
					Failure = KingdomPropertyRules.Refusal(
						KingdomPropertyVerdict.AlreadyDesignated);
					return false;
				}
				if (!ReceiptMatches(existing, System, Item, realm, settlement, out Failure))
					return false;
				return ApplyPrepared(Item, existing, out Failure);
			}
			KingdomPropertyVerdict verdict = KingdomPropertyRules.JudgeDesignation(
				System.Founded, zone != null && System.ClaimedZones.Contains(zone.ZoneID),
				Founder != null && Founder.IsPlayer(), Item?.Physics != null,
				Item != null && Item.IsCreature, Item != null && Item.IsImportant(),
				Item != null && Item.IsTakeable(), FounderOwned(Item), Item?.Physics?.Owner,
				System.KingdomFactionName, false);
			if (verdict != KingdomPropertyVerdict.Allowed)
			{
				Failure = KingdomPropertyRules.Refusal(verdict);
				return false;
			}
			long now = The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks;
			// Open's exact row choice is consent. Only this post-reproof transaction seam may
			// ask Qud to assign identity to an otherwise anonymous, player-owned object.
			string objectId = AssignConfirmedPropertyIdentity(Item);
			if (string.IsNullOrEmpty(objectId)
				|| objectId.Length > KingdomPropertyRules.MaxObjectIdChars)
			{
				Failure = "The selected object's durable identity could not be recorded.";
				return false;
			}
			r_KingdomProperty receipt = Item.RequirePart<r_KingdomProperty>();
			receipt.ReceiptVersion = KingdomPropertyRules.CurrentReceiptVersion;
			receipt.Phase = KingdomPropertyPhase.Prepared;
			receipt.OwnerRealmId = realm;
			receipt.OwnerSettlementId = settlement;
			receipt.FactionId = System.KingdomFactionName;
			receipt.ObjectId = objectId;
			receipt.PriorOwner = Item.Physics.Owner ?? "";
			receipt.DesignatedTick = now;
			receipt.ReleasedTick = 0L;
			receipt.Fault = "";
			if (!ReceiptMatches(receipt, System, Item, realm, settlement, out Failure))
			{
				Quarantine(receipt, "The prepared property receipt failed exact readback.",
					out Failure);
				return false;
			}
			return ApplyPrepared(Item, receipt, out Failure);
		}

		public static bool TryRelease(KingdomSystem System, GameObject Founder,
			GameObject Item, out string Failure)
		{
			Failure = null;
			string realm;
			string settlement;
			Zone zone = Founder?.CurrentZone;
			if (System == null || !System.TryGetCurrentIdentity(out realm, out settlement)
				|| Founder == null || !Founder.IsPlayer() || Item == null || Item.Physics == null
				|| zone == null || !System.ClaimedZones.Contains(zone.ZoneID)
				|| !StillNearby(Founder, Item, zone))
			{
				Failure = "Current realm property authority cannot be proved.";
				return false;
			}
			r_KingdomProperty receipt = Item.GetPart<r_KingdomProperty>();
			if (!ReceiptMatches(receipt, System, Item, realm, settlement, out Failure))
				return false;
			if (receipt.Phase != KingdomPropertyPhase.Designated
				&& receipt.Phase != KingdomPropertyPhase.ReleasePrepared)
			{
				Failure = "That receipt is not active realm property.";
				return false;
			}
			receipt.Phase = KingdomPropertyPhase.ReleasePrepared;
			KingdomPropertyMutation mutation = KingdomPropertyRules.JudgeRelease(
				receipt.Phase, receipt.PriorOwner, receipt.FactionId, Item.Physics?.Owner);
			if (mutation == KingdomPropertyMutation.Quarantine)
				return Quarantine(receipt,
					"Live ownership changed outside the exact property receipt.", out Failure);
			if (mutation == KingdomPropertyMutation.Refuse || Item.Physics == null)
			{
				Failure = "The native ownership slot is unavailable.";
				return false;
			}
			try
			{
				if (mutation == KingdomPropertyMutation.RestorePriorOwner)
					Item.Physics.Owner = string.IsNullOrEmpty(receipt.PriorOwner)
						? null : receipt.PriorOwner;
			}
			catch (Exception ex)
			{
				Failure = "Native ownership restoration threw " + ex.GetType().Name + ".";
				return false;
			}
			if (!SameOwner(Item.Physics.Owner, receipt.PriorOwner))
				return Quarantine(receipt,
					"Native ownership did not accept the exact prior value.", out Failure);
			receipt.ReleasedTick = Math.Max(receipt.DesignatedTick,
				The.Game == null || The.Game.TimeTicks < 0L ? 0L : The.Game.TimeTicks);
			receipt.Phase = KingdomPropertyPhase.Released;
			receipt.Fault = "";
			return true;
		}

		private static bool ApplyPrepared(GameObject Item, r_KingdomProperty Receipt,
			out string Failure)
		{
			Failure = null;
			if (Item?.Physics == null)
			{
				Failure = "The native ownership slot is unavailable.";
				return false;
			}
			KingdomPropertyMutation mutation = KingdomPropertyRules.JudgeApply(Receipt.Phase,
				Receipt.PriorOwner, Receipt.FactionId, Item.Physics.Owner);
			if (mutation == KingdomPropertyMutation.Quarantine)
				return Quarantine(Receipt,
					"Live ownership changed after property preparation.", out Failure);
			if (mutation == KingdomPropertyMutation.Refuse)
			{
				Failure = "The property receipt is not prepared.";
				return false;
			}
			try
			{
				if (mutation == KingdomPropertyMutation.ApplyRealmOwner)
					Item.Physics.Owner = Receipt.FactionId;
			}
			catch (Exception ex)
			{
				Failure = "Native ownership designation threw " + ex.GetType().Name + ".";
				return false;
			}
			if (!SameOwner(Item.Physics.Owner, Receipt.FactionId))
				return Quarantine(Receipt,
					"Native ownership did not accept the exact realm faction.", out Failure);
			Receipt.Phase = KingdomPropertyPhase.Designated;
			Receipt.Fault = "";
			return true;
		}

		private static bool ReceiptMatches(r_KingdomProperty Receipt, KingdomSystem System,
			GameObject Item, string Realm, string Settlement, out string Failure)
		{
			Failure = null;
			if (Receipt == null || System == null || Item == null
				|| !KingdomPropertyRules.ValidReceiptShape(Receipt.ReceiptVersion, Receipt.Phase,
					Receipt.OwnerRealmId, Receipt.OwnerSettlementId, Receipt.FactionId,
					Receipt.ObjectId, Receipt.PriorOwner, Receipt.DesignatedTick,
					Receipt.ReleasedTick, Receipt.Fault))
			{
				Failure = KingdomPropertyRules.Refusal(KingdomPropertyVerdict.MalformedReceipt);
				return false;
			}
			if (!string.Equals(Receipt.OwnerRealmId, Realm, StringComparison.Ordinal)
				|| !string.Equals(Receipt.OwnerSettlementId, Settlement, StringComparison.Ordinal)
				|| !string.Equals(Receipt.FactionId, System.KingdomFactionName,
					StringComparison.Ordinal)
				|| !string.Equals(Receipt.ObjectId, Item.IDIfAssigned, StringComparison.Ordinal))
			{
				Failure = "A different realm, settlement, or physical object owns that receipt.";
				return false;
			}
			return true;
		}

		private static string AssignConfirmedPropertyIdentity(GameObject Item)
		{
			return Item.ID;
		}

		private static bool Quarantine(r_KingdomProperty Receipt, string Reason,
			out string Failure)
		{
			Failure = Reason;
			if (Receipt != null)
			{
				Receipt.Phase = KingdomPropertyPhase.Quarantined;
				Receipt.Fault = (Reason ?? "property receipt diverged").Length
					<= KingdomPropertyRules.MaxFaultChars ? (Reason ?? "property receipt diverged")
					: (Reason ?? "property receipt diverged").Substring(0,
						KingdomPropertyRules.MaxFaultChars);
			}
			KingdomLog.Log("property: " + Failure);
			return false;
		}

		private static bool SameOwner(string Left, string Right)
		{
			return string.Equals(Left ?? "", Right ?? "", StringComparison.Ordinal);
		}
	}
}
