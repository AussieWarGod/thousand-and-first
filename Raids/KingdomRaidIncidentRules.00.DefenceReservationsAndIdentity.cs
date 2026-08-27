using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		public const int MaxSeverity = 4;
		public const int MaxStake = 24;
		public const int MaxParty = 8;
		public const int MaxDefenceWorks = 64;
		public const int MaxDefenceCrew = 64;
		public const int CurrentDefenceReservationVersion = 1;

		/// <summary>Builds the canonical compact payload carried by the lifecycle operation until
		/// the raid ledger publishes the typed rows. Work and crew order is semantic, not survey
		/// order; duplicate work or resident identities refuse instead of double-counting.</summary>
		public static bool TryEncodeDefenceReservations(
			IList<KingdomRaidDefenceReservation> Reservations, out string Commitment, out int Total)
		{
			Commitment = null;
			Total = 0;
			if (Reservations == null || Reservations.Count == 0
				|| Reservations.Count > MaxDefenceWorks) return false;
			List<KingdomRaidDefenceReservation> rows = CopyDefenceReservations(Reservations);
			rows.Sort(delegate(KingdomRaidDefenceReservation a,
				KingdomRaidDefenceReservation b) { return a.WorkId.CompareTo(b.WorkId); });
			HashSet<int> works = new HashSet<int>();
			HashSet<int> crews = new HashSet<int>();
			long sum = 0L;
			StringBuilder text = new StringBuilder("R1");
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomRaidDefenceReservation row = rows[i];
				if (row == null || row.WorkId <= 0 || !works.Add(row.WorkId)
					|| row.FrozenScore <= 0
					|| row.FrozenScore > KingdomLifecycleRules.MaxPhysicalCount
					|| row.CrewSemanticIds == null
					|| row.CrewSemanticIds.Count > MaxDefenceCrew) return false;
				row.CrewSemanticIds.Sort();
				text.Append(';').Append(row.WorkId.ToString(CultureInfo.InvariantCulture))
					.Append('=').Append(row.FrozenScore.ToString(CultureInfo.InvariantCulture))
					.Append('[');
				for (int j = 0; j < row.CrewSemanticIds.Count; j++)
				{
					int residentId = row.CrewSemanticIds[j];
					if (residentId <= 0 || !crews.Add(residentId)) return false;
					if (j != 0) text.Append(',');
					text.Append(residentId.ToString(CultureInfo.InvariantCulture));
				}
				text.Append(']');
				sum += row.FrozenScore;
				if (sum > KingdomLifecycleRules.MaxPhysicalCount
					|| crews.Count > MaxDefenceCrew
					|| text.Length > KingdomLifecycleRules.MaxTextChars) return false;
			}
			Commitment = text.ToString();
			Total = (int)sum;
			return true;
		}

		/// <summary>Decodes only the canonical payload. A merely parseable alternate spelling is
		/// rejected so reload cannot change row order, plan hash, or exclusivity.</summary>
		public static bool TryDecodeDefenceReservations(string Commitment,
			out List<KingdomRaidDefenceReservation> Reservations, out int Total)
		{
			Reservations = new List<KingdomRaidDefenceReservation>();
			Total = 0;
			if (string.IsNullOrEmpty(Commitment)
				|| Commitment.Length > KingdomLifecycleRules.MaxTextChars
				|| !Commitment.StartsWith("R1;", StringComparison.Ordinal)) return false;
			string[] encoded = Commitment.Substring(3).Split(';');
			if (encoded.Length == 0 || encoded.Length > MaxDefenceWorks) return false;
			for (int i = 0; i < encoded.Length; i++)
			{
				string value = encoded[i];
				int equals = value.IndexOf('=');
				int open = value.IndexOf('[', equals + 1);
				if (equals <= 0 || open <= equals + 1 || value.Length <= open + 1
					|| value[value.Length - 1] != ']') return false;
				int workId;
				int score;
				if (!TryPositive(value.Substring(0, equals), out workId)
					|| !TryPositive(value.Substring(equals + 1, open - equals - 1), out score)
					|| score > KingdomLifecycleRules.MaxPhysicalCount) return false;
				KingdomRaidDefenceReservation row = new KingdomRaidDefenceReservation
				{
					WorkId = workId,
					FrozenScore = score
				};
				string crew = value.Substring(open + 1, value.Length - open - 2);
				if (crew.Length != 0)
				{
					string[] ids = crew.Split(',');
					if (ids.Length > MaxDefenceCrew) return false;
					for (int j = 0; j < ids.Length; j++)
					{
						int residentId;
						if (!TryPositive(ids[j], out residentId)) return false;
						row.CrewSemanticIds.Add(residentId);
					}
				}
				Reservations.Add(row);
			}
			string canonical;
			if (!TryEncodeDefenceReservations(Reservations, out canonical, out Total)
				|| !string.Equals(canonical, Commitment, StringComparison.Ordinal))
			{
				Reservations.Clear();
				Total = 0;
				return false;
			}
			return true;
		}

		private static bool TryPositive(string Text, out int Value)
		{
			Value = 0;
			return !string.IsNullOrEmpty(Text) && Text[0] != '0'
				&& int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value > 0;
		}

		private static List<KingdomRaidDefenceReservation> CopyDefenceReservations(
			IList<KingdomRaidDefenceReservation> Source)
		{
			List<KingdomRaidDefenceReservation> rows =
				new List<KingdomRaidDefenceReservation>(Source == null ? 0 : Source.Count);
			for (int i = 0; Source != null && i < Source.Count; i++)
			{
				KingdomRaidDefenceReservation source = Source[i];
				if (source == null) { rows.Add(null); continue; }
				rows.Add(new KingdomRaidDefenceReservation
				{
					WorkId = source.WorkId,
					FrozenScore = source.FrozenScore,
					CrewSemanticIds = source.CrewSemanticIds == null ? null
						: new List<int>(source.CrewSemanticIds)
				});
			}
			return rows;
		}

		public static string GrievanceId(string SourceEventId)
		{
			return ValidId(SourceEventId)
				? KingdomLifecycleRules.ChildId(SourceEventId, "grievance", 0) : null;
		}

		public static string IncidentId(string GrievanceId)
		{
			return ValidId(GrievanceId)
				? KingdomLifecycleRules.ChildId(GrievanceId, "incident", 0) : null;
		}

		public static string DemandChannelId(string IncidentId)
		{
			return ValidId(IncidentId)
				? KingdomLifecycleRules.ChildId(IncidentId, "demand-channel", 0) : null;
		}

		public static string DemandObjectId(string ChannelId, int Revision)
		{
			return ValidId(ChannelId) && Revision > 0
				? KingdomLifecycleRules.ChildId(ChannelId, "witness", Revision) : null;
		}

		public static string RecoveryQuestId(string IncidentId)
		{
			return ValidId(IncidentId) ? "TAF:Recovery:" + IncidentId : null;
		}

		public static string RecoveryStepId(string IncidentId)
		{
			return ValidId(IncidentId)
				? KingdomLifecycleRules.ChildId(IncidentId, "recovery-step", 0) : null;
		}

		public static long SeedFor(string IncidentId)
		{
			if (!ValidId(IncidentId)) return 0L;
			unchecked
			{
				ulong hash = 1469598103934665603UL;
				for (int i = 0; i < IncidentId.Length; i++)
				{
					hash ^= IncidentId[i];
					hash *= 1099511628211UL;
				}
				return (long)(hash & 0x7fffffffffffffffUL);
			}
		}

		public static KingdomRaidIncident Active(KingdomRaidLedger Ledger)
		{
			if (!CurrentLedger(Ledger) || string.IsNullOrEmpty(Ledger.ActiveIncidentId)
				|| Ledger.Incidents == null) return null;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i] != null && string.Equals(Ledger.Incidents[i].Id,
					Ledger.ActiveIncidentId, StringComparison.Ordinal)) return Ledger.Incidents[i];
			return null;
		}

		public static KingdomRaidGrievance Grievance(KingdomRaidLedger Ledger, string Id)
		{
			if (!CurrentLedger(Ledger) || Ledger.Grievances == null || string.IsNullOrEmpty(Id)) return null;
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i] != null && string.Equals(Ledger.Grievances[i].Id,
					Id, StringComparison.Ordinal)) return Ledger.Grievances[i];
			return null;
		}

		public static KingdomRaidIncident Incident(KingdomRaidLedger Ledger, string Id)
		{
			if (!CurrentLedger(Ledger) || Ledger.Incidents == null || string.IsNullOrEmpty(Id)) return null;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i] != null && string.Equals(Ledger.Incidents[i].Id,
					Id, StringComparison.Ordinal)) return Ledger.Incidents[i];
			return null;
		}

		public static bool SourceConsumed(KingdomRaidLedger Ledger, string SourceEventId)
		{
			if (!CurrentLedger(Ledger) || Ledger.Grievances == null || string.IsNullOrEmpty(SourceEventId))
				return false;
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i] != null && string.Equals(
					Ledger.Grievances[i].SourceEventId, SourceEventId,
					StringComparison.Ordinal)) return true;
			return false;
		}

		public static bool HasTalkObligation(KingdomRaidLedger ledger, string faction)
		{
			if (!CurrentLedger(ledger) || ledger.Incidents == null || string.IsNullOrEmpty(faction)) return false;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = ledger.Incidents[i];
				if (q != null && q.TalkObligation && q.TalkObligationDischargedBy == null
					&& string.Equals(q.AttackerFactionId, faction, StringComparison.Ordinal)) return true;
			}
			return false;
		}
	}
}
