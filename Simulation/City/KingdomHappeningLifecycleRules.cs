using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomHappeningLifecycleRules
	{
		internal const int Magic = 0x54414831;
		internal const int PreviousVersion = 1;
		internal const int CurrentVersion = 2;
		internal const int MaxParticipants = 4;
		internal static readonly int MaxSemanticReceipts = KingdomCityState.MaxResidents
			* (KingdomCityState.MaxResidents - 1) / 2 + KingdomCityState.MaxResidents
			+ KingdomCityState.MaxWorks;
		internal const int MaxStringBytes = 2048;
		internal const int MaxPayloadBytes = 24 * 1024;
		internal const int MaxWireChars = ((MaxPayloadBytes + 2) / 3) * 4;
		internal const long WalkTimeoutTicks = KingdomRules.TicksPerDay / 2;
		internal const long HoldTicks = 50L;
		internal const long ExternalReadyTimeoutTicks = KingdomRules.TicksPerDay;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryOpen(KingdomHappeningLifecycleBook book,
			KingdomHappeningProposal proposal, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!ValidBook(book) || !ValidProposal(proposal) || nowTick <= 0L)
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			if (book.Active != null)
			{
				fault = KingdomHappeningLifecycleFault.Busy;
				return false;
			}
			if (AlreadyCompleted(book, proposal.Kind, proposal.SubjectA, proposal.SubjectB))
			{
				fault = KingdomHappeningLifecycleFault.AlreadyCompleted;
				return false;
			}
			// Reserve the permanent receipt before a body or fixture is leased. Otherwise a
			// full book could stage and publish a rite that can never pass its final clear.
			if (PermanentSemantic(proposal.Kind)
				&& book.SemanticReceipts.Length >= MaxSemanticReceipts)
			{
				fault = KingdomHappeningLifecycleFault.OverBudget;
				return false;
			}
			if (book.Sequence == int.MaxValue)
			{
				fault = KingdomHappeningLifecycleFault.SequenceExhausted;
				return false;
			}
			int sequence = book.Sequence + 1;
			KingdomHappeningSinkState skipped = KingdomHappeningSinkState.Skipped;
			KingdomHappeningOperation operation = new KingdomHappeningOperation(sequence,
				proposal.EventId, proposal.Kind, proposal.Physical
					? KingdomHappeningLifecyclePhase.Prepared
					: KingdomHappeningLifecyclePhase.Ready,
				proposal.EventTick, nowTick, nowTick, 0L, proposal.SubjectA, proposal.SubjectB,
				proposal.Outcome, proposal.SettlementId, proposal.ZoneId,
				proposal.FixtureObjectId, proposal.FixtureBlueprint, proposal.FixtureX,
				proposal.FixtureY, proposal.Physical, proposal.ExternalSemantic, false,
				!proposal.Physical,
				proposal.ChronicleAttended, proposal.ChronicleUnattended,
				proposal.LedgerAttended, proposal.LedgerUnattended,
				proposal.MessageAttended, proposal.MessageUnattended, proposal.Effect,
				proposal.DisplayName, proposal.PlanQuote, proposal.Participants,
				proposal.ExternalSemantic ? skipped : KingdomHappeningSinkState.Pending,
				proposal.ExternalSemantic ? skipped : KingdomHappeningSinkState.Pending,
				proposal.ExternalSemantic || string.IsNullOrEmpty(proposal.Effect)
					? skipped : KingdomHappeningSinkState.Pending,
				proposal.ExternalSemantic || (string.IsNullOrEmpty(proposal.LedgerAttended)
					&& string.IsNullOrEmpty(proposal.LedgerUnattended))
					? skipped : KingdomHappeningSinkState.Pending,
				proposal.ExternalSemantic || (string.IsNullOrEmpty(proposal.MessageAttended)
					&& string.IsNullOrEmpty(proposal.MessageUnattended))
					? skipped : KingdomHappeningSinkState.Pending);
			if (!ValidOperation(operation))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(sequence, operation,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

	}
}
