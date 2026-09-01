using System;

namespace ThousandAndFirst
{
	public enum KingdomCivicOfficePhase : byte
	{
		None = 0,
		Vacant = 1,
		AppointmentPrepared = 2,
		Held = 3,
		VacancyPrepared = 4,
		Quarantined = 5
	}

	public enum KingdomCivicOfficeVacancyCause : byte
	{
		None = 0,
		Released = 1,
		Death = 2,
		Departure = 3,
		AuthorityLost = 4
	}

	/// <summary>One explicit civic office. The exact role remains title-only; a separate optional
	/// market projection may use this same holder after physical growth prerequisites are met.
	/// Neither the title nor market service grants succession authority.</summary>
	[Serializable]
	public sealed class KingdomCivicOfficeReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomCivicOfficePhase Phase;
		public KingdomCivicOfficeVacancyCause VacancyCause;
		public int Generation;
		public string SettlementId;
		public string SettlementName;
		public int WorkId;
		public int HolderResidentId;
		public string HolderName;
		public string HolderObjectId;
		public bool OwnsRole;
		public int PredecessorResidentId;
		public string PredecessorName;
		public long ChangedTick;
		public string Fault;
	}

	public enum KingdomRemembrancePhase : byte
	{
		None = 0,
		Declined = 1,
		ProjectionPrepared = 2,
		Projected = 3,
		Lost = 4,
		Quarantined = 5,

		/// <summary>An exact active-zone death callback created the one non-expiring semantic
		/// opportunity. No mourner, carrier, audience, or commission is implied yet.</summary>
		Eligible = 6
	}

	/// <summary>One optional bodyless remembrance fixture per owned settlement. The terminal
	/// resident row remains death authority; this row owns only the disclosed choice and carrier.</summary>
	[Serializable]
	public sealed class KingdomRemembranceReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public KingdomRemembrancePhase Phase;
		public int Generation;
		public string SettlementId;
		public string SettlementName;
		public int SubjectResidentId;
		public string SubjectName;
		public int MournerResidentId;
		public string MournerName;
		public string CarrierObjectId;
		public string CarrierZoneId;
		/// <summary>Tick of the directly witnessed terminal-row callback. Retained through later
		/// decline/projection so an explicit Charter action cannot rewrite source provenance.</summary>
		public long DecidedTick;
		public string Fault;
	}
}
