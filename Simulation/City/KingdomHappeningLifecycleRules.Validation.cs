using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		private static bool Exact(KingdomHappeningLifecycleBook book, string eventId,
			out KingdomHappeningOperation operation)
		{
			operation = book == null ? null : book.Active;
			return operation != null && string.Equals(operation.EventId, eventId,
				StringComparison.Ordinal);
		}

		private static KingdomHappeningSinkState RecoverUninspectable(
			KingdomHappeningSinkState state)
		{
			return state == KingdomHappeningSinkState.Attempting
				? KingdomHappeningSinkState.Lost : state;
		}

		private static bool Terminal(KingdomHappeningSinkState state)
		{
			return state == KingdomHappeningSinkState.Delivered
				|| state == KingdomHappeningSinkState.Skipped
				|| state == KingdomHappeningSinkState.Lost;
		}

		private static bool SinkTransition(KingdomHappeningSinkState before,
			KingdomHappeningSinkState after)
		{
			if (before == after) return true;
			if (before == KingdomHappeningSinkState.Pending)
				return after == KingdomHappeningSinkState.Attempting
					|| Terminal(after);
			return before == KingdomHappeningSinkState.Attempting
				&& (after == KingdomHappeningSinkState.Delivered
					|| after == KingdomHappeningSinkState.Lost);
		}

		private static bool PhaseTransition(KingdomHappeningLifecyclePhase before,
			KingdomHappeningLifecyclePhase after)
		{
			if (after == KingdomHappeningLifecyclePhase.Restoring)
				return before != KingdomHappeningLifecyclePhase.Restoring;
			return (before == KingdomHappeningLifecyclePhase.Prepared
					&& after == KingdomHappeningLifecyclePhase.Walking)
				|| (before == KingdomHappeningLifecyclePhase.Walking
					&& after == KingdomHappeningLifecyclePhase.Holding)
				|| (before == KingdomHappeningLifecyclePhase.Holding
					&& after == KingdomHappeningLifecyclePhase.Ready);
		}

		private static bool PermanentSemantic(KingdomPhysicalHappeningKind kind)
		{
			return kind == KingdomPhysicalHappeningKind.Wedding
				|| kind == KingdomPhysicalHappeningKind.Funeral
				|| kind == KingdomPhysicalHappeningKind.Raising;
		}

		private const string CommunalRiteProofPrefix = "taf:communal-rite-lease:v1:";

		private static bool ValidCommunalRiteProof(string value, int subject)
		{
			if (value == null || !value.StartsWith(CommunalRiteProofPrefix,
				StringComparison.Ordinal)) return false;
			int separator = value.IndexOf(':', CommunalRiteProofPrefix.Length);
			if (separator <= CommunalRiteProofPrefix.Length
				|| !long.TryParse(value.Substring(CommunalRiteProofPrefix.Length,
					separator - CommunalRiteProofPrefix.Length), NumberStyles.None,
					CultureInfo.InvariantCulture, out long epoch) || epoch <= 0L) return false;
			string practiceId = value.Substring(separator + 1);
			return KingdomCommunalRiteRules.TryPracticeSubject(practiceId,
				out int exactSubject) && exactSubject == subject;
		}

		private static bool ValidBook(KingdomHappeningLifecycleBook book)
		{
			if (book == null || book.Sequence < 0 || book.SemanticReceipts == null
				|| book.SemanticReceipts.Length > MaxSemanticReceipts
				|| (book.Active != null && (book.Active.Sequence != book.Sequence
					|| !ValidOperation(book.Active)))) return false;
			for (int i = 0; i < book.SemanticReceipts.Length; i++)
			{
				KingdomHappeningSemanticReceipt row = book.SemanticReceipts[i];
				if (!PermanentSemantic(row.Kind) || row.SubjectA <= 0
					|| (row.Kind == KingdomPhysicalHappeningKind.Wedding
						&& row.SubjectB <= row.SubjectA)
					|| ((row.Kind == KingdomPhysicalHappeningKind.Funeral
						|| row.Kind == KingdomPhysicalHappeningKind.Raising)
						&& row.SubjectB != 0)) return false;
				for (int j = 0; j < i; j++)
				{
					KingdomHappeningSemanticReceipt earlier = book.SemanticReceipts[j];
					if (earlier.Kind == row.Kind && earlier.SubjectA == row.SubjectA
						&& earlier.SubjectB == row.SubjectB) return false;
				}
			}
			return true;
		}

		private static bool ValidProposal(KingdomHappeningProposal proposal)
		{
			if (proposal == null || proposal.Kind == KingdomPhysicalHappeningKind.None
				|| !Enum.IsDefined(typeof(KingdomPhysicalHappeningKind), proposal.Kind)
				|| proposal.EventTick <= 0L || !Text(proposal.EventId, true)
				|| !Text(proposal.SettlementId, true)
				|| !SemanticIdentity(proposal.Kind, proposal.SubjectA, proposal.SubjectB)
				|| proposal.EventId != CanonicalEventId(proposal.SettlementId, proposal.Kind,
					proposal.EventTick, proposal.SubjectA, proposal.SubjectB, proposal.Outcome)
				|| (proposal.ExternalSemantic
					&& proposal.Kind != KingdomPhysicalHappeningKind.Raising
					&& proposal.Kind != KingdomPhysicalHappeningKind.CommunalRite)
				|| (proposal.Kind == KingdomPhysicalHappeningKind.CommunalRite
					&& (!proposal.ExternalSemantic || !ValidCommunalRiteProof(
						proposal.PlanQuote, proposal.SubjectA)))
				|| proposal.Participants.Length > MaxParticipants
				|| !Text(proposal.ChronicleAttended, proposal.ExternalSemantic ? false : true)
				|| !Text(proposal.ChronicleUnattended, proposal.ExternalSemantic ? false : true)
				|| !Text(proposal.LedgerAttended, false)
				|| !Text(proposal.LedgerUnattended, false)
				|| !Text(proposal.MessageAttended, false)
				|| !Text(proposal.MessageUnattended, false)
				|| !Text(proposal.Effect, false) || !Text(proposal.DisplayName, false)
				|| !Text(proposal.PlanQuote, false)
				|| (proposal.Physical && (!Text(proposal.ZoneId, true)
					|| !Text(proposal.FixtureObjectId, true)
					|| !Text(proposal.FixtureBlueprint, true)
					|| proposal.FixtureX < 0 || proposal.FixtureY < 0
					|| proposal.Participants.Length == 0))
				|| (!proposal.Physical && (proposal.ExternalSemantic
					|| !string.IsNullOrEmpty(proposal.ZoneId)
					|| !string.IsNullOrEmpty(proposal.FixtureObjectId)
					|| !string.IsNullOrEmpty(proposal.FixtureBlueprint)
					|| proposal.FixtureX != 0 || proposal.FixtureY != 0
					|| proposal.Participants.Length != 0))) return false;
			for (int i = 0; i < proposal.Participants.Length; i++)
				if (!ValidParticipant(proposal.Participants[i])) return false;
			return UniqueParticipants(proposal.Participants);
		}

		private static bool ValidOperation(KingdomHappeningOperation operation)
		{
			if (operation == null || operation.Sequence <= 0
				|| !Enum.IsDefined(typeof(KingdomPhysicalHappeningKind), operation.Kind)
				|| operation.Kind == KingdomPhysicalHappeningKind.None
				|| !Enum.IsDefined(typeof(KingdomHappeningLifecyclePhase), operation.Phase)
				|| operation.Phase == KingdomHappeningLifecyclePhase.None
				|| operation.EventTick <= 0L || operation.StartedTick <= 0L
				|| operation.UpdatedTick < operation.StartedTick
				|| !Text(operation.EventId, true) || !Text(operation.SettlementId, true)
				|| !SemanticIdentity(operation.Kind, operation.SubjectA, operation.SubjectB)
				|| operation.EventId != CanonicalEventId(operation.SettlementId, operation.Kind,
					operation.EventTick, operation.SubjectA, operation.SubjectB, operation.Outcome)
				|| (operation.ExternalSemantic
					&& operation.Kind != KingdomPhysicalHappeningKind.Raising
					&& operation.Kind != KingdomPhysicalHappeningKind.CommunalRite)
				|| (operation.Kind == KingdomPhysicalHappeningKind.CommunalRite
					&& (!operation.ExternalSemantic || !ValidCommunalRiteProof(
						operation.PlanQuote, operation.SubjectA)))
				|| (operation.ExternalSemantic
					&& (!string.IsNullOrEmpty(operation.ChronicleAttended)
						|| !string.IsNullOrEmpty(operation.ChronicleUnattended)
						|| !string.IsNullOrEmpty(operation.LedgerAttended)
						|| !string.IsNullOrEmpty(operation.LedgerUnattended)
						|| !string.IsNullOrEmpty(operation.MessageAttended)
						|| !string.IsNullOrEmpty(operation.MessageUnattended)
						|| !string.IsNullOrEmpty(operation.Effect)))
				|| !Text(operation.ZoneId, operation.Physical)
				|| !Text(operation.FixtureObjectId, operation.Physical)
				|| !Text(operation.FixtureBlueprint, operation.Physical)
				|| operation.Participants.Length > MaxParticipants
				|| !Text(operation.ChronicleAttended, operation.ExternalSemantic ? false : true)
				|| !Text(operation.ChronicleUnattended, operation.ExternalSemantic ? false : true)
				|| !Text(operation.LedgerAttended, false)
				|| !Text(operation.LedgerUnattended, false)
				|| !Text(operation.MessageAttended, false)
				|| !Text(operation.MessageUnattended, false)
				|| !Text(operation.Effect, false) || !Text(operation.DisplayName, false)
				|| !Text(operation.PlanQuote, false)
				|| !Sink(operation.ChronicleState) || !Sink(operation.ToldState)
				|| !Sink(operation.EffectState) || !Sink(operation.LedgerState)
				|| !Sink(operation.MessageState)
				|| (operation.Physical && (operation.FixtureX < 0 || operation.FixtureY < 0
					|| operation.Participants.Length == 0))
				|| (operation.Physical
					&& operation.Phase != KingdomHappeningLifecyclePhase.Restoring
					&& operation.Attended
						!= (operation.Phase == KingdomHappeningLifecyclePhase.Ready))
				|| (operation.Phase == KingdomHappeningLifecyclePhase.Holding
					? operation.HoldUntilTick <= operation.UpdatedTick
					: operation.HoldUntilTick != 0L)
				|| (!operation.Physical && (operation.ExternalSemantic || operation.Attended
					|| operation.Phase == KingdomHappeningLifecyclePhase.Prepared
					|| operation.Phase == KingdomHappeningLifecyclePhase.Walking
					|| operation.Phase == KingdomHappeningLifecyclePhase.Holding
					|| !string.IsNullOrEmpty(operation.ZoneId)
					|| !string.IsNullOrEmpty(operation.FixtureObjectId)
					|| !string.IsNullOrEmpty(operation.FixtureBlueprint)
					|| operation.FixtureX != 0 || operation.FixtureY != 0
					|| operation.Participants.Length != 0 || !operation.FixtureRestored))
				|| (operation.Physical
					&& operation.Phase != KingdomHappeningLifecyclePhase.Restoring
					&& operation.FixtureRestored)) return false;
			for (int i = 0; i < operation.Participants.Length; i++)
				if (!ValidParticipant(operation.Participants[i])
					|| (operation.Phase != KingdomHappeningLifecyclePhase.Restoring
						&& operation.Participants[i].Restored)) return false;
			return UniqueParticipants(operation.Participants);
		}

		private static bool ValidParticipant(KingdomHappeningParticipant participant)
		{
			return participant.ResidentId > 0 && Text(participant.ObjectId, true)
				&& Text(participant.Name, true) && Text(participant.Home, false)
				&& Text(participant.Anchor, false) && participant.OriginalX >= 0
				&& participant.OriginalY >= 0 && participant.TargetX >= 0
				&& participant.TargetY >= 0 && participant.PostWorkId >= 0
				&& participant.PostKind >= byte.MinValue && participant.PostKind <= byte.MaxValue
				&& Enum.IsDefined(typeof(KingdomWorkKind),
					(KingdomWorkKind)participant.PostKind);
		}

		private static bool UniqueParticipants(KingdomHappeningParticipant[] people)
		{
			for (int i = 0; i < people.Length; i++)
				for (int j = 0; j < i; j++)
					if (people[i].ResidentId == people[j].ResidentId
						|| string.Equals(people[i].ObjectId, people[j].ObjectId,
							StringComparison.Ordinal)
						|| (people[i].TargetX == people[j].TargetX
							&& people[i].TargetY == people[j].TargetY)) return false;
			return true;
		}

		private static bool SemanticIdentity(KingdomPhysicalHappeningKind kind, int subjectA,
			int subjectB)
		{
			switch (kind)
			{
			case KingdomPhysicalHappeningKind.Wedding:
				return subjectA > 0 && subjectB > subjectA;
			case KingdomPhysicalHappeningKind.Funeral:
			case KingdomPhysicalHappeningKind.Raising:
			case KingdomPhysicalHappeningKind.CommunalRite:
				return subjectA > 0 && subjectB == 0;
			case KingdomPhysicalHappeningKind.Feast:
				return subjectA == 0 && subjectB == 0;
			default:
				return false;
			}
		}

		private static string CanonicalEventId(string settlementId,
			KingdomPhysicalHappeningKind kind, long eventTick, int subjectA, int subjectB,
			int outcome)
		{
			return "taf:happening:" + (settlementId ?? "") + ":"
				+ ((int)kind).ToString(CultureInfo.InvariantCulture) + ":"
				+ eventTick.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectA.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectB.ToString(CultureInfo.InvariantCulture) + ":"
				+ outcome.ToString(CultureInfo.InvariantCulture);
		}

		private static bool Text(string value, bool required)
		{
			if (value == null || (required && value.Length == 0)) return false;
			return StrictUtf8.GetByteCount(value) <= MaxStringBytes;
		}

		private static bool Sink(KingdomHappeningSinkState state)
		{
			return Enum.IsDefined(typeof(KingdomHappeningSinkState), state);
		}

	}
}
