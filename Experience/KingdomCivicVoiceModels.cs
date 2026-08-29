using System;

namespace ThousandAndFirst
{
	/// <summary>The three frozen consequential choices in the first civic-voice proof.</summary>
	public enum KingdomCivicVoiceFixture : byte
	{
		None = 0,
		CreedDeclaration = 1,
		VillageCovenant = 2,
		AssentingMoot = 3
	}

	/// <summary>Owner-authored preview. Facts are copied into the receipt byte-for-byte.</summary>
	public sealed class KingdomCivicDecisionPreview
	{
		public KingdomCivicVoiceFixture Fixture;
		public int SourceVersion;
		public string SourceId;
		public string SettlementId;
		public string Facts;
		public long CauseTick;
		public long EnableEpoch;
	}

	/// <summary>One standing resident offered to the pure deterministic witness selector.</summary>
	public readonly struct KingdomCivicVoiceCandidate
	{
		public readonly int ResidentId;
		public readonly string Name;

		public KingdomCivicVoiceCandidate(int residentId, string name)
		{
			ResidentId = residentId; Name = name;
		}
	}

	/// <summary>Immutable source/facts/witness snapshot. Only callback consumption may advance.</summary>
	[Serializable]
	public sealed class KingdomCivicVoiceReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomCivicVoiceFixture Fixture;
		public int SourceVersion;
		public string SourceId;
		public string SettlementId;
		public string Facts;
		public long CauseTick;
		public long EnableEpoch;
		public int FirstResidentId;
		public string FirstName;
		public int SecondResidentId;
		public string SecondName;
		public bool CallbackConsumed;
		public long CallbackTick;

		public KingdomCivicVoiceReceipt Copy()
		{
			return (KingdomCivicVoiceReceipt)MemberwiseClone();
		}
	}
}
