using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed class KingdomHappeningOperation
	{
		internal readonly int Sequence;
		internal readonly string EventId;
		internal readonly KingdomPhysicalHappeningKind Kind;
		internal readonly KingdomHappeningLifecyclePhase Phase;
		internal readonly long EventTick;
		internal readonly long StartedTick;
		internal readonly long UpdatedTick;
		internal readonly long HoldUntilTick;
		internal readonly int SubjectA;
		internal readonly int SubjectB;
		internal readonly int Outcome;
		internal readonly string SettlementId;
		internal readonly string ZoneId;
		internal readonly string FixtureObjectId;
		internal readonly string FixtureBlueprint;
		internal readonly int FixtureX;
		internal readonly int FixtureY;
		internal readonly bool Physical;
		internal readonly bool ExternalSemantic;
		internal readonly bool Attended;
		internal readonly bool FixtureRestored;
		internal readonly string ChronicleAttended;
		internal readonly string ChronicleUnattended;
		internal readonly string LedgerAttended;
		internal readonly string LedgerUnattended;
		internal readonly string MessageAttended;
		internal readonly string MessageUnattended;
		internal readonly string Effect;
		internal readonly string DisplayName;
		internal readonly string PlanQuote;
		internal readonly KingdomHappeningParticipant[] Participants;
		internal readonly KingdomHappeningSinkState ChronicleState;
		internal readonly KingdomHappeningSinkState ToldState;
		internal readonly KingdomHappeningSinkState EffectState;
		internal readonly KingdomHappeningSinkState LedgerState;
		internal readonly KingdomHappeningSinkState MessageState;

		internal KingdomHappeningOperation(int sequence, string eventId,
			KingdomPhysicalHappeningKind kind, KingdomHappeningLifecyclePhase phase,
			long eventTick, long startedTick, long updatedTick, long holdUntilTick,
			int subjectA, int subjectB, int outcome, string settlementId, string zoneId,
			string fixtureObjectId, string fixtureBlueprint, int fixtureX, int fixtureY,
			bool physical, bool externalSemantic, bool attended, bool fixtureRestored,
			string chronicleAttended,
			string chronicleUnattended, string ledgerAttended, string ledgerUnattended,
			string messageAttended, string messageUnattended, string effect,
			string displayName, string planQuote, KingdomHappeningParticipant[] participants,
			KingdomHappeningSinkState chronicleState, KingdomHappeningSinkState toldState,
			KingdomHappeningSinkState effectState, KingdomHappeningSinkState ledgerState,
			KingdomHappeningSinkState messageState)
		{
			Sequence = sequence;
			EventId = eventId ?? "";
			Kind = kind;
			Phase = phase;
			EventTick = eventTick;
			StartedTick = startedTick;
			UpdatedTick = updatedTick;
			HoldUntilTick = holdUntilTick;
			SubjectA = subjectA;
			SubjectB = subjectB;
			Outcome = outcome;
			SettlementId = settlementId ?? "";
			ZoneId = zoneId ?? "";
			FixtureObjectId = fixtureObjectId ?? "";
			FixtureBlueprint = fixtureBlueprint ?? "";
			FixtureX = fixtureX;
			FixtureY = fixtureY;
			Physical = physical;
			ExternalSemantic = externalSemantic;
			Attended = attended;
			FixtureRestored = fixtureRestored;
			ChronicleAttended = chronicleAttended ?? "";
			ChronicleUnattended = chronicleUnattended ?? "";
			LedgerAttended = ledgerAttended ?? "";
			LedgerUnattended = ledgerUnattended ?? "";
			MessageAttended = messageAttended ?? "";
			MessageUnattended = messageUnattended ?? "";
			Effect = effect ?? "";
			DisplayName = displayName ?? "";
			PlanQuote = planQuote ?? "";
			Participants = participants == null
				? new KingdomHappeningParticipant[0]
				: (KingdomHappeningParticipant[])participants.Clone();
			ChronicleState = chronicleState;
			ToldState = toldState;
			EffectState = effectState;
			LedgerState = ledgerState;
			MessageState = messageState;
		}

		internal KingdomHappeningParticipant[] CopyParticipants()
		{
			return (KingdomHappeningParticipant[])Participants.Clone();
		}

		internal KingdomHappeningOperation WithPhase(KingdomHappeningLifecyclePhase phase,
			bool attended, long holdUntilTick, long updatedTick)
		{
			return Copy(phase, attended, holdUntilTick, updatedTick, ChronicleState, ToldState,
				EffectState, LedgerState, MessageState);
		}

		internal KingdomHappeningOperation WithSinks(KingdomHappeningSinkState chronicle,
			KingdomHappeningSinkState told, KingdomHappeningSinkState effect,
			KingdomHappeningSinkState ledger, KingdomHappeningSinkState message, long updatedTick)
		{
			return Copy(Phase, Attended, HoldUntilTick, updatedTick, chronicle, told, effect,
				ledger, message);
		}

		internal KingdomHappeningOperation WithRestoration(
			KingdomHappeningParticipant[] participants, bool fixtureRestored, long updatedTick)
		{
			return Copy(Phase, Attended, HoldUntilTick, updatedTick, ChronicleState, ToldState,
				EffectState, LedgerState, MessageState, participants, fixtureRestored);
		}

		private KingdomHappeningOperation Copy(KingdomHappeningLifecyclePhase phase,
			bool attended, long holdUntilTick, long updatedTick,
			KingdomHappeningSinkState chronicle, KingdomHappeningSinkState told,
			KingdomHappeningSinkState effect, KingdomHappeningSinkState ledger,
			KingdomHappeningSinkState message,
			KingdomHappeningParticipant[] participants = null, bool? fixtureRestored = null)
		{
			return new KingdomHappeningOperation(Sequence, EventId, Kind, phase, EventTick,
				StartedTick, updatedTick, holdUntilTick, SubjectA, SubjectB, Outcome,
				SettlementId, ZoneId, FixtureObjectId, FixtureBlueprint, FixtureX, FixtureY,
				Physical, ExternalSemantic, attended, fixtureRestored ?? FixtureRestored,
				ChronicleAttended, ChronicleUnattended,
				LedgerAttended, LedgerUnattended, MessageAttended, MessageUnattended, Effect,
				DisplayName, PlanQuote, participants ?? Participants, chronicle, told, effect,
				ledger, message);
		}
	}
}
