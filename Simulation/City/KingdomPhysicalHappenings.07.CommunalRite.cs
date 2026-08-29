using System;
using System.Globalization;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomCommunalRitePhysicalState : byte
	{
		Missing = 0,
		Pending = 1,
		Ready = 2,
		Restoring = 3,
		Completed = 4
	}

	internal static partial class KingdomPhysicalHappenings
	{
		private const string CommunalRiteLeasePrefix =
			"taf:communal-rite-lease:v1:";

		internal static bool CommunalRiteActive(KingdomCityBook book)
		{
			return TryReadRaw(book, out KingdomHappeningLifecycleBook lifecycle)
				&& lifecycle.Active != null
				&& lifecycle.Active.Kind == KingdomPhysicalHappeningKind.CommunalRite
				&& lifecycle.Active.ExternalSemantic;
		}

		internal static KingdomPhysicalQueueResult QueueCommunalRite(KingdomSystem system,
			KingdomCityBook book, string practiceId, int practiceSubject, long eventTick,
			long enableEpoch,
			XRL.World.Zone zone, long nowTick, out string eventId, out string[] names)
		{
			eventId = EventId(book == null ? "" : book.SettlementId,
				KingdomPhysicalHappeningKind.CommunalRite, eventTick, practiceSubject, 0, 0);
			if (!KingdomCommunalRiteRules.TryPracticeSubject(practiceId, out int exactSubject)
				|| practiceSubject != exactSubject || enableEpoch <= 0L)
			{
				names = new string[0];
				return KingdomPhysicalQueueResult.Refused;
			}
			return Queue(system, book, KingdomPhysicalHappeningKind.CommunalRite, eventTick,
				practiceSubject, 0, 0, zone, null, true, false, "", "", "", "", "",
				"", "", "First Feast practice", CommunalRiteLeasePrefix
					+ enableEpoch.ToString(CultureInfo.InvariantCulture) + ":" + practiceId,
				nowTick, out names,
				eventId);
		}

		internal static bool TryReadCommunalRite(KingdomCityBook book, int practiceSubject,
			long nowTick, out KingdomCommunalRitePhysicalState state, out string eventId,
			out string practiceId, out long eventTick, out long enableEpoch)
		{
			state = KingdomCommunalRitePhysicalState.Missing;
			eventId = null; practiceId = null; eventTick = 0L; enableEpoch = 0L;
			if (practiceSubject <= 0 || !TryRead(book, nowTick,
				out KingdomHappeningLifecycleBook lifecycle)) return false;
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation == null || operation.Kind != KingdomPhysicalHappeningKind.CommunalRite
				|| operation.SubjectA != practiceSubject || operation.SubjectB != 0
				|| !operation.ExternalSemantic) return true;
			if (!TryReadCommunalRiteProof(operation.PlanQuote, out enableEpoch,
				out practiceId) || !KingdomCommunalRiteRules.TryPracticeSubject(practiceId,
					out int exactSubject) || exactSubject != operation.SubjectA) return false;
			eventId = operation.EventId; eventTick = operation.EventTick;
			state = operation.Phase == KingdomHappeningLifecyclePhase.Ready
				? KingdomCommunalRitePhysicalState.Ready
				: operation.Phase == KingdomHappeningLifecyclePhase.Restoring
					? KingdomCommunalRitePhysicalState.Restoring
					: KingdomCommunalRitePhysicalState.Pending;
			return true;
		}

		internal static bool AcknowledgeCommunalRite(KingdomSystem system,
			KingdomCityBook book, string eventId, int practiceSubject, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			if (!ExactCommunalRite(lifecycle.Active, eventId, practiceSubject)) return false;
			if (!TryCommunalRiteSettlementName(system, book, out string settlementName))
				return false;
			if (lifecycle.Active.Phase == KingdomHappeningLifecyclePhase.Restoring)
			{
				DriveCore(system, book, settlementName, StandsIn(lifecycle.Active.ZoneId),
					nowTick, 0, out int ignoredRestore);
				return TryRead(book, nowTick, out lifecycle) && lifecycle.Active == null;
			}
			if (lifecycle.Active.Phase != KingdomHappeningLifecyclePhase.Ready
				|| !lifecycle.Active.Attended) return false;
			if (!SetPhase(book, lifecycle, KingdomHappeningLifecyclePhase.Ready,
				KingdomHappeningLifecyclePhase.Restoring, true, 0L, nowTick)) return false;
			DriveCore(system, book, settlementName, StandsIn(lifecycle.Active.ZoneId),
				nowTick, 0, out int ignored);
			return TryRead(book, nowTick, out lifecycle) && lifecycle.Active == null;
		}

		internal static bool CancelCommunalRite(KingdomSystem system, KingdomCityBook book,
			string eventId, int practiceSubject, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			if (!ExactCommunalRite(lifecycle.Active, eventId, practiceSubject)) return false;
			if (!TryCommunalRiteSettlementName(system, book, out string settlementName))
				return false;
			if (lifecycle.Active.Phase == KingdomHappeningLifecyclePhase.Ready
				&& lifecycle.Active.Attended)
				return AcknowledgeCommunalRite(system, book, eventId, practiceSubject, nowTick);
			if (lifecycle.Active.Phase != KingdomHappeningLifecyclePhase.Restoring
				&& !SetPhase(book, lifecycle, lifecycle.Active.Phase,
					KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick)) return false;
			DriveCore(system, book, settlementName, StandsIn(lifecycle.Active.ZoneId),
				nowTick, 0, out int ignored);
			return TryRead(book, nowTick, out lifecycle) && lifecycle.Active == null;
		}

		private static bool TryCommunalRiteSettlementName(KingdomSystem system,
			KingdomCityBook book, out string settlementName)
		{
			settlementName = null;
			if (system == null || book == null || !system.TryFindSettlement(book,
				out bool seated, out KingdomSettlement settlement)) return false;
			settlementName = seated ? system.SeatName : settlement?.SettlementName;
			return !string.IsNullOrEmpty(settlementName);
		}

		private static bool ExactCommunalRite(KingdomHappeningOperation operation,
			string eventId, int practiceSubject)
		{
			return operation != null && operation.Kind == KingdomPhysicalHappeningKind.CommunalRite
				&& operation.SubjectA == practiceSubject && operation.SubjectB == 0
				&& operation.ExternalSemantic && string.Equals(operation.EventId, eventId,
					StringComparison.Ordinal)
				&& TryReadCommunalRiteProof(operation.PlanQuote, out long _,
					out string practiceId)
				&& KingdomCommunalRiteRules.TryPracticeSubject(practiceId,
					out int exactSubject) && exactSubject == practiceSubject;
		}

		private static bool TryReadCommunalRiteProof(string value, out long epoch,
			out string practiceId)
		{
			epoch = 0L; practiceId = null;
			if (value == null || !value.StartsWith(CommunalRiteLeasePrefix,
				StringComparison.Ordinal)) return false;
			int separator = value.IndexOf(':', CommunalRiteLeasePrefix.Length);
			if (separator <= CommunalRiteLeasePrefix.Length
				|| !long.TryParse(value.Substring(CommunalRiteLeasePrefix.Length,
					separator - CommunalRiteLeasePrefix.Length), NumberStyles.None,
					CultureInfo.InvariantCulture, out epoch) || epoch <= 0L) return false;
			practiceId = value.Substring(separator + 1);
			return ThousandAndFirst.Simulation.Kernel.KernelSemanticId.IsValid(practiceId);
		}
	}
}
