using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>Disposition of the schema-1 projection that entered vanilla global pools.</summary>
	public enum KingdomFounderHistoryLegacyCleanupState : byte
	{
		None = 0,
		Required = 1,
		Complete = 2
	}

	/// <summary>
	/// Exact, one-per-world owner for the TAF-local founder-memory projection. Schema 2 reconstructs
	/// its read-only view from this record and never inserts it into Qud's HistoryKit or journal
	/// pools. The legacy fields retain only bounded schema-1 cleanup evidence.
	/// </summary>
	[Serializable]
	public sealed class KingdomFounderHistoryReceipt
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int Version = KingdomFounderHistoryRules.CurrentVersion;
		public KingdomFounderHistoryPhase Phase;
		public bool PublicationEnabled;
		public string RealmId;
		public string DeathToken;
		public long DeathTick;
		public long PreparedTick;
		public long HistoricYear = long.MinValue;
		public long CommittedTick;
		public string FounderName;
		public string CityName;
		public string RegionName;
		public string Cause;
		public string Gospel;
		public string ProjectionId;
		public string ProjectionProofId;
		public KingdomFounderHistoryLegacyCleanupState LegacyCleanupState;
		public KingdomFounderHistoryPhase LegacyPhase;
		/// <summary>Schema-1 vanilla HistoryKit entity id; empty for native schema-2 records.</summary>
		public string EntityId;
		/// <summary>Schema-1 vanilla Sultan-journal note id; empty for native schema-2 records.</summary>
		public string NoteId;
		/// <summary>Schema-1 ownership marker; empty for native schema-2 records.</summary>
		public string ProofId;
		/// <summary>Schema-1 vanilla HistoryKit event id; zero for native schema-2 records.</summary>
		public long EventId;
		public string Fault;

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomFounderHistoryReceipt));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomFounderHistoryReceipt));
			Normalize();
		}
#endif

		public void Normalize()
		{
			RealmId = RealmId ?? "";
			DeathToken = DeathToken ?? "";
			FounderName = FounderName ?? "";
			CityName = CityName ?? "";
			RegionName = RegionName ?? "";
			Cause = Cause ?? "";
			Gospel = Gospel ?? "";
			ProjectionId = ProjectionId ?? "";
			ProjectionProofId = ProjectionProofId ?? "";
			EntityId = EntityId ?? "";
			NoteId = NoteId ?? "";
			ProofId = ProofId ?? "";
			Fault = Fault ?? "";
			if (Version == 1)
			{
				string migrationFailure;
				if (!KingdomFounderHistoryRules.TryMigrateV1(this, out migrationFailure))
					QuarantineUnmigratable(migrationFailure);
			}
			if (Phase == KingdomFounderHistoryPhase.None)
			{
				PublicationEnabled = false;
				DeathTick = 0L;
				PreparedTick = 0L;
				HistoricYear = long.MinValue;
				CommittedTick = 0L;
				EventId = 0L;
				RealmId = DeathToken = FounderName = CityName = RegionName = Cause = "";
				Gospel = ProjectionId = ProjectionProofId = EntityId = NoteId = ProofId = Fault = "";
				LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.None;
				LegacyPhase = KingdomFounderHistoryPhase.None;
			}
		}

		private void QuarantineUnmigratable(string Failure)
		{
			Version = KingdomFounderHistoryRules.CurrentVersion;
			Phase = KingdomFounderHistoryPhase.Quarantined;
			PublicationEnabled = true;
			RealmId = DeathToken = FounderName = CityName = RegionName = Cause = Gospel = "";
			ProjectionId = ProjectionProofId = EntityId = NoteId = ProofId = "";
			DeathTick = PreparedTick = CommittedTick = EventId = 0L;
			HistoricYear = long.MinValue;
			LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.None;
			LegacyPhase = KingdomFounderHistoryPhase.None;
			Fault = KingdomFounderHistoryRules.QuarantineReason(
				"schema-1 founder-memory receipt could not migrate: " + (Failure ?? "unknown fault"));
		}

		public KingdomFounderHistoryReceipt Copy()
		{
			return (KingdomFounderHistoryReceipt)MemberwiseClone();
		}
	}
}
