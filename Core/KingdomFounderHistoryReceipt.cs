using System;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst
{
	/// <summary>
	/// Exact, one-per-world receipt for the founder memory projected into Qud's public history.
	/// The history entity and journal note are views; this record owns retry and divergence law.
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
		public string EntityId;
		public string NoteId;
		public string ProofId;
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
			EntityId = EntityId ?? "";
			NoteId = NoteId ?? "";
			ProofId = ProofId ?? "";
			Fault = Fault ?? "";
			if (Phase == KingdomFounderHistoryPhase.None)
			{
				PublicationEnabled = false;
				DeathTick = 0L;
				PreparedTick = 0L;
				HistoricYear = long.MinValue;
				CommittedTick = 0L;
				EventId = 0L;
				RealmId = DeathToken = FounderName = CityName = RegionName = Cause = "";
				Gospel = EntityId = NoteId = ProofId = Fault = "";
			}
		}

		public KingdomFounderHistoryReceipt Copy()
		{
			return (KingdomFounderHistoryReceipt)MemberwiseClone();
		}
	}
}
