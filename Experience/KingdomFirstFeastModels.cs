using System;

namespace ThousandAndFirst
{
	/// <summary>Append-only wire vocabulary for one private, deed-sourced civic practice.</summary>
	public enum KingdomFirstFeastPhase : byte
	{
		None = 0,
		Offered = 1,
		Adopted = 2,
		Adapted = 3,
		Refused = 4,
		Quarantined = 5,
		Archived = 6
	}

	/// <summary>Defer is a disclosed command, never persisted state: it leaves Offered unchanged.</summary>
	public enum KingdomFirstFeastChoice : byte
	{
		None = 0,
		Adopt = 1,
		Adapt = 2,
		Refuse = 3,
		Defer = 4
	}

	/// <summary>O9 closes by using the named-cook service, not a second recipe authority.</summary>
	public enum KingdomFirstFeastRecipeDisposition : byte
	{
		None = 0,
		NamedCookServiceSupersedes = 1
	}

	/// <summary>Exact later adventure deed, causally downstream of one joined First Guest.</summary>
	public sealed class KingdomFirstFeastDeed
	{
		public string SettlementId;
		public string SettlementName;
		public string DeedId;
		public string DeedText;
		public long DeedTick;
		public string GuestTerminalReceiptId;
		public string GuestTerminalDigest;
		public long GuestTerminalTick;
		public string AdventureEventId;
		public string AdventureFingerprint;
	}

	/// <summary>One exact standing TAF resident; rules deterministically choose the first two.</summary>
	public readonly struct KingdomFirstFeastCandidate
	{
		public readonly int ResidentId;
		public readonly string Name;

		public KingdomFirstFeastCandidate(int residentId, string name)
		{
			ResidentId = residentId;
			Name = name;
		}
	}

	/// <summary>
	/// One finite proposal and, after an affirmative choice, the sole private practice record.
	/// It owns no meal, ingredient, recipe, Journal note, calendar, creed, reputation, or buff.
	/// </summary>
	[Serializable]
	public sealed class KingdomFirstFeastReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomFirstFeastPhase Phase;
		public KingdomFirstFeastChoice Choice;
		public int Generation;
		public string SettlementId;
		public string SettlementName;
		public string DeedId;
		public string DeedText;
		public long DeedTick;
		public string GuestTerminalReceiptId;
		public string GuestTerminalDigest;
		public long GuestTerminalTick;
		public string AdventureEventId;
		public string AdventureFingerprint;
		public int ProposerResidentId;
		public string ProposerName;
		public int WitnessResidentId;
		public string WitnessName;
		public string DishName;
		public string Ingredients;
		public string OfferedDedication;
		public string AdaptedDedication;
		public string PracticeId;
		public long OfferedTick;
		public long DecidedTick;
		public long EnableEpoch;
		public string Fault;

		public KingdomFirstFeastReceipt Copy()
		{
			return (KingdomFirstFeastReceipt)MemberwiseClone();
		}
	}
}
