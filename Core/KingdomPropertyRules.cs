using System;

namespace ThousandAndFirst
{
	/// <summary>Durable phase of one explicitly designated physical object.</summary>
	public enum KingdomPropertyPhase
	{
		None = 0,
		Prepared = 1,
		Designated = 2,
		ReleasePrepared = 3,
		Released = 4,
		Quarantined = 5
	}

	public enum KingdomPropertyVerdict
	{
		Allowed = 0,
		Unfounded = 1,
		UnclaimedGround = 2,
		NotFounder = 3,
		NoPhysicalObject = 4,
		Creature = 5,
		Important = 6,
		Untakeable = 7,
		NotFounderOwned = 8,
		ForeignOwner = 9,
		AlreadyDesignated = 10,
		MalformedReceipt = 11
	}

	public enum KingdomPropertyMutation
	{
		Refuse = 0,
		ApplyRealmOwner = 1,
		ObserveApplied = 2,
		RestorePriorOwner = 3,
		ObserveReleased = 4,
		Quarantine = 5
	}

	/// <summary>
	/// Engine-free law for explicit realm property. Claiming a zone never calls this law: only
	/// one Charter choice over one exact founder-owned object may prepare a receipt.
	/// </summary>
	public static class KingdomPropertyRules
	{
		public const int CurrentReceiptVersion = 1;
		public const int MaxIdentityChars = 512;
		public const int MaxObjectIdChars = 2048;
		public const int MaxFaultChars = 1024;
		public const int MaxNearbyCandidates = 64;

		public static KingdomPropertyVerdict JudgeDesignation(bool Founded,
			bool ClaimedGround, bool FounderActor, bool HasPhysics, bool Creature,
			bool Important, bool Takeable, bool FounderOwned, string CurrentOwner,
			string RealmFaction, bool HasReceipt)
		{
			if (!Founded || !ValidIdentity(RealmFaction)) return KingdomPropertyVerdict.Unfounded;
			if (!ClaimedGround) return KingdomPropertyVerdict.UnclaimedGround;
			if (!FounderActor) return KingdomPropertyVerdict.NotFounder;
			if (!HasPhysics) return KingdomPropertyVerdict.NoPhysicalObject;
			if (Creature) return KingdomPropertyVerdict.Creature;
			if (Important) return KingdomPropertyVerdict.Important;
			if (!Takeable) return KingdomPropertyVerdict.Untakeable;
			if (HasReceipt) return KingdomPropertyVerdict.AlreadyDesignated;
			if (!string.IsNullOrEmpty(CurrentOwner)
				&& !string.Equals(CurrentOwner, RealmFaction, StringComparison.Ordinal))
				return KingdomPropertyVerdict.ForeignOwner;
			if (!FounderOwned) return KingdomPropertyVerdict.NotFounderOwned;
			return KingdomPropertyVerdict.Allowed;
		}

		public static KingdomPropertyMutation JudgeApply(KingdomPropertyPhase Phase,
			string PriorOwner, string RealmFaction, string CurrentOwner)
		{
			if (Phase != KingdomPropertyPhase.Prepared || !ValidIdentity(RealmFaction))
				return KingdomPropertyMutation.Refuse;
			if (SameOwner(CurrentOwner, RealmFaction)) return KingdomPropertyMutation.ObserveApplied;
			if (SameOwner(CurrentOwner, PriorOwner)) return KingdomPropertyMutation.ApplyRealmOwner;
			return KingdomPropertyMutation.Quarantine;
		}

		public static KingdomPropertyMutation JudgeRelease(KingdomPropertyPhase Phase,
			string PriorOwner, string RealmFaction, string CurrentOwner)
		{
			if (Phase != KingdomPropertyPhase.Designated
				&& Phase != KingdomPropertyPhase.ReleasePrepared)
				return KingdomPropertyMutation.Refuse;
			if (SameOwner(CurrentOwner, PriorOwner)) return KingdomPropertyMutation.ObserveReleased;
			if (SameOwner(CurrentOwner, RealmFaction)) return KingdomPropertyMutation.RestorePriorOwner;
			return KingdomPropertyMutation.Quarantine;
		}

		public static bool ValidReceiptShape(int Version, KingdomPropertyPhase Phase,
			string RealmId, string SettlementId, string FactionId, string ObjectId,
			string PriorOwner, long DesignatedTick, long ReleasedTick, string Fault)
		{
			if (Version != CurrentReceiptVersion || Phase == KingdomPropertyPhase.None
				|| !ValidIdentity(RealmId) || !ValidIdentity(SettlementId)
				|| !ValidIdentity(FactionId) || !ValidObjectId(ObjectId)
				|| !ValidOptionalIdentity(PriorOwner) || DesignatedTick < 0L
				|| ReleasedTick < 0L || !ValidFault(Fault)) return false;
			switch (Phase)
			{
			case KingdomPropertyPhase.Prepared:
			case KingdomPropertyPhase.Designated:
			case KingdomPropertyPhase.ReleasePrepared:
				return ReleasedTick == 0L && string.IsNullOrEmpty(Fault);
			case KingdomPropertyPhase.Released:
				return ReleasedTick >= DesignatedTick && string.IsNullOrEmpty(Fault);
			case KingdomPropertyPhase.Quarantined:
				return !string.IsNullOrEmpty(Fault);
			default:
				return false;
			}
		}

		public static string Refusal(KingdomPropertyVerdict Verdict)
		{
			switch (Verdict)
			{
			case KingdomPropertyVerdict.Unfounded: return "No founded realm can own it.";
			case KingdomPropertyVerdict.UnclaimedGround: return "Realm property is designated only on held ground.";
			case KingdomPropertyVerdict.NotFounder: return "Only the Charter bearer may designate realm property.";
			case KingdomPropertyVerdict.NoPhysicalObject: return "That has no physical ownership slot.";
			case KingdomPropertyVerdict.Creature: return "A living creature is not property of the realm.";
			case KingdomPropertyVerdict.Important: return "Important and quest-bound objects cannot be designated.";
			case KingdomPropertyVerdict.Untakeable: return "Only a takeable object can enter the theft law.";
			case KingdomPropertyVerdict.NotFounderOwned: return "That object is not unambiguously yours to give.";
			case KingdomPropertyVerdict.ForeignOwner: return "Another faction already owns that object.";
			case KingdomPropertyVerdict.AlreadyDesignated: return "That object already carries a property receipt.";
			case KingdomPropertyVerdict.MalformedReceipt: return "Its property receipt is malformed and was not rewritten.";
			default: return "That object cannot be designated.";
			}
		}

		private static bool SameOwner(string Left, string Right)
		{
			return string.Equals(Left ?? "", Right ?? "", StringComparison.Ordinal);
		}

		private static bool ValidIdentity(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxIdentityChars
				&& Value.IndexOf('\0') < 0;
		}

		private static bool ValidOptionalIdentity(string Value)
		{
			return string.IsNullOrEmpty(Value) || ValidIdentity(Value);
		}

		private static bool ValidObjectId(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= MaxObjectIdChars
				&& Value.IndexOf('\0') < 0;
		}

		private static bool ValidFault(string Value)
		{
			return (Value ?? "").Length <= MaxFaultChars && (Value ?? "").IndexOf('\0') < 0;
		}
	}
}
