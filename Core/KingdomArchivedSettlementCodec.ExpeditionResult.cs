using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		private static bool ValidExpeditionResultDomain(
			Simulation.City.KingdomJobRegistry value)
		{
			if (value == null || value.JobIds == null || value.Kinds == null
				|| value.OriginCodes == null || value.OutcomeCodes == null
				|| value.ExpeditionDeedDispositions == null
				|| value.ExpeditionDeedPolityIds == null || value.ExpeditionDeedCauseRefs == null
				|| value.ExpeditionDeedFigureRefs == null) return false;
			int count = value.JobIds.Count;
			if (value.Kinds.Count != count || value.OriginCodes.Count != count
				|| value.OutcomeCodes.Count != count
				|| value.ExpeditionDeedDispositions.Count != count
				|| value.ExpeditionDeedPolityIds.Count != count
				|| value.ExpeditionDeedCauseRefs.Count != count
				|| value.ExpeditionDeedFigureRefs.Count != count) return false;
			for (int i = 0; i < count; i++)
				if (!Simulation.City.KingdomJobRules.ValidExpeditionResultReceipt(
					(Simulation.City.KingdomJobKind)value.Kinds[i], value.OriginCodes[i],
					value.OutcomeCodes[i], (Simulation.City.KingdomExpeditionDeedDisposition)
						value.ExpeditionDeedDispositions[i], value.ExpeditionDeedPolityIds[i],
					value.ExpeditionDeedCauseRefs[i], value.ExpeditionDeedFigureRefs[i])) return false;
			return true;
		}

		private static bool HistoricalExpeditionResultDomain(
			Simulation.City.KingdomJobRegistry value, int schemaVersion)
		{
			if (schemaVersion >= ExpeditionResultVersion) return ValidExpeditionResultDomain(value);
			if (value == null || value.JobIds == null || value.ExpeditionDeedDispositions == null
				|| value.ExpeditionDeedPolityIds == null || value.ExpeditionDeedCauseRefs == null
				|| value.ExpeditionDeedFigureRefs == null) return false;
			int count = value.JobIds.Count;
			bool absent = value.ExpeditionDeedDispositions.Count == 0
				&& value.ExpeditionDeedPolityIds.Count == 0
				&& value.ExpeditionDeedCauseRefs.Count == 0
				&& value.ExpeditionDeedFigureRefs.Count == 0;
			if (absent) return true;
			if (value.ExpeditionDeedDispositions.Count != count
				|| value.ExpeditionDeedPolityIds.Count != count
				|| value.ExpeditionDeedCauseRefs.Count != count
				|| value.ExpeditionDeedFigureRefs.Count != count) return false;
			for (int i = 0; i < count; i++)
				if (value.ExpeditionDeedDispositions[i] != 0
					|| !string.IsNullOrEmpty(value.ExpeditionDeedPolityIds[i])
					|| !string.IsNullOrEmpty(value.ExpeditionDeedCauseRefs[i])
					|| !string.IsNullOrEmpty(value.ExpeditionDeedFigureRefs[i])) return false;
			return true;
		}
	}
}
