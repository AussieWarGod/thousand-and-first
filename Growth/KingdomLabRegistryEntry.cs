using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free row written to game state before a hall publishes its physical job part. The row
	/// is deliberately small: the hall owns detailed progress, while this proves which hall, patient,
	/// realm incarnation and immutable effect contract may ever act on the job.
	/// </summary>
	internal sealed class KingdomLabRegistryEntry
	{
		public string JobId = "";
		public string BuildingId = "";
		public string PatientId = "";
		public string GameId = "";
		public string RealmId = "";
		public long RealmFoundedTick;
		public int RulerSuccessionOrdinal = -1;
		public string RulerLifeId = "";
		public int ContractVersion;
		public string ProcedureKey = "";
		public string Grants = "";
		public int Source = -1;
		public int Attach = -1;
		public string Manager = "";
		public string Detail = "";
		public string Fingerprint = "";
		public KingdomLabRegistryStatus Status;
		public long UpdatedTick;

		public KingdomLabRegistryEntry Copy()
		{
			return new KingdomLabRegistryEntry
			{
				JobId = JobId ?? "",
				BuildingId = BuildingId ?? "",
				PatientId = PatientId ?? "",
				GameId = GameId ?? "",
				RealmId = RealmId ?? "",
				RealmFoundedTick = RealmFoundedTick,
				RulerSuccessionOrdinal = RulerSuccessionOrdinal,
				RulerLifeId = RulerLifeId ?? "",
				ContractVersion = ContractVersion,
				ProcedureKey = ProcedureKey ?? "",
				Grants = Grants ?? "",
				Source = Source,
				Attach = Attach,
				Manager = Manager ?? "",
				Detail = Detail ?? "",
				Fingerprint = Fingerprint ?? "",
				Status = Status,
				UpdatedTick = UpdatedTick
			};
		}
	}

}
