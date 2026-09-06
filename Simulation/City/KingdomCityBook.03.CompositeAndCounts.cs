#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	public partial class KingdomCityBook
	{
#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			if (SubsidenceReadFailed)
				throw new System.IO.InvalidDataException("City subsidence storage did not finish loading.");
			Writer.WriteNamedFields(this, typeof(KingdomCityBook));
		}

		public void Read(SerializationReader Reader)
		{
			ReadNamedState(() => Reader.ReadNamedFields(this, typeof(KingdomCityBook)));
		}
#endif

		internal void ReadNamedState(System.Action ReadFields)
		{
			SchemaVersion = 0;
			SubsidenceModel = null;
			SubsidenceReadFailed = true;
			try
			{
				ReadFields();
				if (!TryMigrateSubsidenceStorage())
					throw new System.IO.InvalidDataException("City subsidence storage has no valid versioned authority.");
				SubsidenceReadFailed = false;
				Normalize();
				if (SubsidenceReadFailed)
					throw new System.IO.InvalidDataException("City subsidence carriers failed validated load normalization.");
			}
			catch
			{
				SubsidenceReadFailed = true;
				throw;
			}
		}

		internal bool TryMigrateSubsidenceStorage()
		{
			if (SchemaVersion < 1 || SchemaVersion > KingdomCityRules.SchemaVersion) return false;
			string candidate = SubsidenceModel;
			if (SchemaVersion < 4)
			{
				if (candidate != null) return false;
				candidate = ThousandAndFirst.KingdomSubsidenceStepCodec.LegacyWire;
			}
			ThousandAndFirst.KingdomSubsidenceStepBook decoded;
			if (!ThousandAndFirst.KingdomSubsidenceStepCodec.TryDecode(candidate, out decoded)) return false;
			if (HasFrozenRung(decoded) && (decoded.SettlementId != SettlementId
				|| !TryReadExact(out _, out _))) return false;
			SubsidenceModel = candidate;
			return true;
		}

		internal bool HasValidSubsidenceStorage()
		{
			ThousandAndFirst.KingdomSubsidenceStepBook decoded;
			return !SubsidenceReadFailed && SchemaVersion == KingdomCityRules.SchemaVersion
				&& ThousandAndFirst.KingdomSubsidenceStepCodec.TryDecode(SubsidenceModel, out decoded)
				&& (!HasFrozenRung(decoded) || decoded.SettlementId == SettlementId && TryReadExact(out _, out _));
		}

		private static bool HasFrozenRung(ThousandAndFirst.KingdomSubsidenceStepBook book)
		{
			return book?.Active != null && book.Active.RungModel != ThousandAndFirst.KingdomSubsidenceStepRules.NoRungs
				&& book.Active.RungModel != ThousandAndFirst.KingdomSubsidenceStepRules.UnplannedRungs;
		}

		/// <summary>How many zone rows the book holds after normalization.</summary>
		public int ZoneCount => ZoneIds.Count;

		public int WorkCount => WorkIds.Count;

		public int ResidentCount => ResidentIds.Count;

		public int ToldCount => ToldKinds.Count;
	}
}
