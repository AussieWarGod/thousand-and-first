using System;

namespace ThousandAndFirst
{
	/// <summary>Stable names for the five Thousand and First game-system types whose
	/// presence may be carried by a save. Values are identities, not wire bits.</summary>
	public enum KingdomSaveSystemRosterSystem : byte
	{
		None = 0,
		Realm = 1,
		Seal = 2,
		CivicMemory = 3,
		Succession = 4,
		Inheritance = 5
	}

	/// <summary>Independent proof for why an absent roster marker may be interpreted.</summary>
	public enum KingdomSaveSystemRosterContext : byte
	{
		Unknown = 0,
		ExplicitNewGame = 1,
		LegacyDecodedRealm = 2,
		PreparedRemoval = 3,
		UnprovenAbsence = 4
	}

	public enum KingdomSaveSystemRosterDisposition : byte
	{
		Refused = 0,
		Verified = 1,
		Bootstrap = 2,
		LeaveAbsent = 3,
		ClearForPreparedRemoval = 4,
		RecoveryRequired = 5
	}

	/// <summary>Stable first-cause result. Runtime may turn this into UI without
	/// inferring meaning from prose.</summary>
	public enum KingdomSaveSystemRosterFault : byte
	{
		None = 0,
		InvalidContext = 1,
		InvalidObservation = 2,
		NonPositiveMarker = 3,
		UnsupportedMarkerVersion = 4,
		FutureMarkerVersion = 5,
		UnknownSystemBits = 6,
		MarkerMissingMandatorySystem = 7,
		UnexpectedMultiplicity = 8,
		MarkerExpectedSystemMissing = 9,
		LegacyRealmMissing = 10,
		LegacySealMissing = 11,
		MissingMarkerUnproven = 12,
		CasChanged = 13,
		DecisionNotCommittable = 14
	}

	/// <summary>Detached counts read from XRLGame.Systems. Mutable only so an engine
	/// adapter can fill it without depending on any engine type here.</summary>
	public sealed class KingdomSaveSystemRosterCounts
	{
		public int Realm;
		public int Seal;
		public int CivicMemory;
		public int Succession;
		public int Inheritance;

		public KingdomSaveSystemRosterCounts Clone()
		{
			return new KingdomSaveSystemRosterCounts
			{
				Realm = Realm,
				Seal = Seal,
				CivicMemory = CivicMemory,
				Succession = Succession,
				Inheritance = Inheritance
			};
		}

		public int Count(KingdomSaveSystemRosterSystem System)
		{
			switch (System)
			{
				case KingdomSaveSystemRosterSystem.Realm: return Realm;
				case KingdomSaveSystemRosterSystem.Seal: return Seal;
				case KingdomSaveSystemRosterSystem.CivicMemory: return CivicMemory;
				case KingdomSaveSystemRosterSystem.Succession: return Succession;
				case KingdomSaveSystemRosterSystem.Inheritance: return Inheritance;
				default: return -1;
			}
		}
	}

	/// <summary>Immutable compare-and-swap plan. Expected marker fields are the read
	/// side; next fields are the proposed write side.</summary>
	public sealed class KingdomSaveSystemRosterDecision
	{
		public KingdomSaveSystemRosterDisposition Disposition { get; private set; }
		public KingdomSaveSystemRosterFault Fault { get; private set; }
		public KingdomSaveSystemRosterSystem System { get; private set; }
		public int ExpectedCount { get; private set; }
		public int ActualCount { get; private set; }
		public bool ExpectedMarkerPresent { get; private set; }
		public int ExpectedMarkerRaw { get; private set; }
		public bool NextMarkerPresent { get; private set; }
		public int NextMarkerRaw { get; private set; }
		public string Failure { get; private set; }

		public bool Committable
		{
			get
			{
				return Disposition == KingdomSaveSystemRosterDisposition.Verified
					|| Disposition == KingdomSaveSystemRosterDisposition.Bootstrap
					|| Disposition == KingdomSaveSystemRosterDisposition.LeaveAbsent
					|| Disposition == KingdomSaveSystemRosterDisposition.ClearForPreparedRemoval;
			}
		}

		internal KingdomSaveSystemRosterDecision(KingdomSaveSystemRosterDisposition disposition,
			KingdomSaveSystemRosterFault fault, KingdomSaveSystemRosterSystem system,
			int expectedCount, int actualCount, bool expectedMarkerPresent,
			int expectedMarkerRaw, bool nextMarkerPresent, int nextMarkerRaw, string failure)
		{
			Disposition = disposition;
			Fault = fault;
			System = system;
			ExpectedCount = expectedCount;
			ActualCount = actualCount;
			ExpectedMarkerPresent = expectedMarkerPresent;
			ExpectedMarkerRaw = expectedMarkerRaw;
			NextMarkerPresent = nextMarkerPresent;
			NextMarkerRaw = nextMarkerRaw;
			Failure = failure ?? "";
		}

		public KingdomSaveSystemRosterDecision Clone()
		{
			return new KingdomSaveSystemRosterDecision(Disposition, Fault, System,
				ExpectedCount, ActualCount, ExpectedMarkerPresent, ExpectedMarkerRaw,
				NextMarkerPresent, NextMarkerRaw, Failure);
		}
	}

	public static partial class KingdomSaveSystemRosterRules
	{
		public const string StateKey = "r_TAF_SaveSystemRoster_v1";
		public const int CurrentVersion = 1;
		public const int VersionShift = 16;
		public const int MaskBits = 16;
		public const int MaskField = 65535;

		public const int RealmBit = 1 << 0;
		public const int SealBit = 1 << 1;
		public const int CivicMemoryBit = 1 << 2;
		public const int SuccessionBit = 1 << 3;
		public const int InheritanceBit = 1 << 4;
		public const int MandatoryMask = RealmBit | SealBit | CivicMemoryBit;
		public const int OptionalMask = SuccessionBit | InheritanceBit;
		public const int KnownMask = MandatoryMask | OptionalMask;

		private static readonly KingdomSaveSystemRosterSystem[] OrderedSystems =
		{
			KingdomSaveSystemRosterSystem.Realm,
			KingdomSaveSystemRosterSystem.Seal,
			KingdomSaveSystemRosterSystem.CivicMemory,
			KingdomSaveSystemRosterSystem.Succession,
			KingdomSaveSystemRosterSystem.Inheritance
		};

		public static bool TryEncode(int Mask, out int Raw, out string Failure)
		{
			Raw = 0;
			if (!ValidMask(Mask, out KingdomSaveSystemRosterFault _,
				out KingdomSaveSystemRosterSystem _, out Failure)) return false;
			Raw = (CurrentVersion << VersionShift) | Mask;
			return true;
		}

		public static bool TryDecode(int Raw, out int Version, out int Mask,
			out KingdomSaveSystemRosterFault Fault,
			out KingdomSaveSystemRosterSystem System, out string Failure)
		{
			Version = 0; Mask = 0; Fault = KingdomSaveSystemRosterFault.None;
			System = KingdomSaveSystemRosterSystem.None; Failure = null;
			if (Raw <= 0)
				return DecodeFailure(KingdomSaveSystemRosterFault.NonPositiveMarker,
					"save-system roster marker is zero or negative", out Fault, out Failure);
			Version = (Raw >> VersionShift) & MaskField;
			Mask = Raw & MaskField;
			if (Version == 0)
				return DecodeFailure(KingdomSaveSystemRosterFault.UnsupportedMarkerVersion,
					"save-system roster marker version zero is unsupported", out Fault,
					out Failure);
			if (Version > CurrentVersion)
				return DecodeFailure(KingdomSaveSystemRosterFault.FutureMarkerVersion,
					"save-system roster marker uses a future version", out Fault, out Failure);
			return ValidMask(Mask, out Fault, out System, out Failure);
		}

		private static bool ValidMask(int Mask, out KingdomSaveSystemRosterFault Fault,
			out KingdomSaveSystemRosterSystem System, out string Failure)
		{
			Fault = KingdomSaveSystemRosterFault.None;
			System = KingdomSaveSystemRosterSystem.None;
			Failure = null;
			if ((Mask & ~KnownMask) != 0)
				return DecodeFailure(KingdomSaveSystemRosterFault.UnknownSystemBits,
					"save-system roster marker contains unknown system bits", out Fault,
					out Failure);
			for (int i = 0; i < 3; i++)
			{
				KingdomSaveSystemRosterSystem current = OrderedSystems[i];
				if ((Mask & Bit(current)) != 0) continue;
				System = current;
				return DecodeFailure(KingdomSaveSystemRosterFault.MarkerMissingMandatorySystem,
					"save-system roster marker omits mandatory " + Name(current), out Fault,
					out Failure);
			}
			return true;
		}

		internal static int Bit(KingdomSaveSystemRosterSystem System)
		{
			if (System == KingdomSaveSystemRosterSystem.None) return 0;
			return 1 << ((int)System - 1);
		}

		internal static string Name(KingdomSaveSystemRosterSystem System)
		{
			return System.ToString();
		}

		private static bool DecodeFailure(KingdomSaveSystemRosterFault Value,
			string Message, out KingdomSaveSystemRosterFault Fault, out string Failure)
		{
			Fault = Value; Failure = Message; return false;
		}
	}
}
