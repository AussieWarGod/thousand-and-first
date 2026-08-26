using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed class KingdomHappeningProposal
	{
		internal readonly string EventId;
		internal readonly KingdomPhysicalHappeningKind Kind;
		internal readonly long EventTick;
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

		internal KingdomHappeningProposal(string eventId, KingdomPhysicalHappeningKind kind,
			long eventTick, int subjectA, int subjectB, int outcome, string settlementId,
			string zoneId, string fixtureObjectId, string fixtureBlueprint, int fixtureX,
			int fixtureY, bool physical, bool externalSemantic, string chronicleAttended,
			string chronicleUnattended, string ledgerAttended, string ledgerUnattended,
			string messageAttended, string messageUnattended, string effect,
			string displayName, string planQuote, KingdomHappeningParticipant[] participants)
		{
			EventId = eventId ?? "";
			Kind = kind;
			EventTick = eventTick;
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
		}
	}
}
