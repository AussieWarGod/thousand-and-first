using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomPhysicalHappeningKind : byte
	{
		None = 0,
		Wedding = 1,
		Funeral = 2,
		Feast = 3,
		Raising = 4
	}

	internal enum KingdomHappeningLifecyclePhase : byte
	{
		None = 0,
		Prepared = 1,
		Walking = 2,
		Holding = 3,
		Ready = 4,
		Restoring = 5
	}

	internal enum KingdomHappeningSinkState : byte
	{
		Pending = 0,
		Attempting = 1,
		Delivered = 2,
		Skipped = 3,
		Lost = 4
	}

	internal enum KingdomHappeningLifecycleFault : byte
	{
		None = 0,
		Malformed = 1,
		UnsupportedVersion = 2,
		OverBudget = 3,
		Busy = 4,
		WrongOperation = 5,
		WrongPhase = 6,
		SequenceExhausted = 7,
		AlreadyCompleted = 8
	}

	internal enum KingdomHappeningResumeAction : byte
	{
		Refuse = 0,
		PreparePosts = 1,
		WaitForArrival = 2,
		BeginHold = 3,
		WaitHold = 4,
		Publish = 5,
		WaitExternal = 6,
		Restore = 7
	}

	internal readonly struct KingdomHappeningParticipant
	{
		internal readonly int ResidentId;
		internal readonly string ObjectId;
		internal readonly string Name;
		internal readonly string Home;
		internal readonly string Anchor;
		internal readonly int OriginalX;
		internal readonly int OriginalY;
		internal readonly int TargetX;
		internal readonly int TargetY;
		internal readonly int PostWorkId;
		internal readonly int PostKind;
		internal readonly bool Wanders;
		internal readonly bool WandersRandomly;
		internal readonly bool Staying;
		internal readonly bool Restored;

		internal KingdomHappeningParticipant(int residentId, string objectId, string name,
			string home, string anchor, int originalX, int originalY, int targetX, int targetY,
			int postWorkId, int postKind, bool wanders, bool wandersRandomly, bool staying,
			bool restored = false)
		{
			ResidentId = residentId;
			ObjectId = objectId ?? "";
			Name = name ?? "";
			Home = home ?? "";
			Anchor = anchor ?? "";
			OriginalX = originalX;
			OriginalY = originalY;
			TargetX = targetX;
			TargetY = targetY;
			PostWorkId = postWorkId;
			PostKind = postKind;
			Wanders = wanders;
			WandersRandomly = wandersRandomly;
			Staying = staying;
			Restored = restored;
		}

		internal KingdomHappeningParticipant WithRestored()
		{
			return new KingdomHappeningParticipant(ResidentId, ObjectId, Name, Home, Anchor,
				OriginalX, OriginalY, TargetX, TargetY, PostWorkId, PostKind, Wanders,
				WandersRandomly, Staying, true);
		}
	}

	internal readonly struct KingdomHappeningSemanticReceipt
	{
		internal readonly KingdomPhysicalHappeningKind Kind;
		internal readonly int SubjectA;
		internal readonly int SubjectB;

		internal KingdomHappeningSemanticReceipt(KingdomPhysicalHappeningKind kind,
			int subjectA, int subjectB)
		{
			Kind = kind;
			if (kind == KingdomPhysicalHappeningKind.Wedding && subjectB < subjectA)
			{
				SubjectA = subjectB;
				SubjectB = subjectA;
			}
			else
			{
				SubjectA = subjectA;
				SubjectB = subjectB;
			}
		}
	}

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

	internal sealed class KingdomHappeningLifecycleBook
	{
		internal readonly int Sequence;
		internal readonly KingdomHappeningOperation Active;
		internal readonly KingdomHappeningSemanticReceipt[] SemanticReceipts;

		internal KingdomHappeningLifecycleBook(int sequence, KingdomHappeningOperation active)
			: this(sequence, active, null)
		{
		}

		internal KingdomHappeningLifecycleBook(int sequence, KingdomHappeningOperation active,
			KingdomHappeningSemanticReceipt[] semanticReceipts)
		{
			Sequence = sequence;
			Active = active;
			SemanticReceipts = semanticReceipts == null
				? new KingdomHappeningSemanticReceipt[0]
				: (KingdomHappeningSemanticReceipt[])semanticReceipts.Clone();
		}

		internal KingdomHappeningSemanticReceipt[] CopySemanticReceipts()
		{
			return (KingdomHappeningSemanticReceipt[])SemanticReceipts.Clone();
		}

		internal static KingdomHappeningLifecycleBook Empty
		{
			get { return new KingdomHappeningLifecycleBook(0, null); }
		}
	}

	internal static class KingdomHappeningLifecycleRules
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

		internal static bool TrySetPhase(KingdomHappeningLifecycleBook book, string eventId,
			KingdomHappeningLifecyclePhase expected, KingdomHappeningLifecyclePhase phase,
			bool attended, long holdUntilTick, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (operation.Phase != expected)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			if (!PhaseTransition(expected, phase) || nowTick < operation.UpdatedTick)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningOperation changed = operation.WithPhase(phase, attended,
				holdUntilTick, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TrySetSinks(KingdomHappeningLifecycleBook book, string eventId,
			KingdomHappeningSinkState chronicle, KingdomHappeningSinkState told,
			KingdomHappeningSinkState effect, KingdomHappeningSinkState ledger,
			KingdomHappeningSinkState message, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if ((operation.Phase != KingdomHappeningLifecyclePhase.Ready
				&& operation.Phase != KingdomHappeningLifecyclePhase.Restoring)
				|| nowTick < operation.UpdatedTick
				|| !SinkTransition(operation.ChronicleState, chronicle)
				|| !SinkTransition(operation.ToldState, told)
				|| !SinkTransition(operation.EffectState, effect)
				|| !SinkTransition(operation.LedgerState, ledger)
				|| !SinkTransition(operation.MessageState, message))
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningOperation changed = operation.WithSinks(chronicle, told, effect,
				ledger, message, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TryClear(KingdomHappeningLifecycleBook book, string eventId,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation ignored))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (book.Active.Phase != KingdomHappeningLifecyclePhase.Restoring
				|| !RestorationSettled(book.Active))
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningSemanticReceipt[] receipts = book.SemanticReceipts;
			if (PermanentSemantic(book.Active.Kind)
				&& !AlreadyCompleted(book, book.Active.Kind, book.Active.SubjectA,
					book.Active.SubjectB))
			{
				if (receipts.Length >= MaxSemanticReceipts)
				{
					fault = KingdomHappeningLifecycleFault.OverBudget;
					return false;
				}
				KingdomHappeningSemanticReceipt[] grown =
					new KingdomHappeningSemanticReceipt[receipts.Length + 1];
				Array.Copy(receipts, grown, receipts.Length);
				grown[receipts.Length] = new KingdomHappeningSemanticReceipt(book.Active.Kind,
					book.Active.SubjectA, book.Active.SubjectB);
				receipts = grown;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, null, receipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static bool TryMarkRestored(KingdomHappeningLifecycleBook book,
			string eventId, int participantIndex, bool fixture, long nowTick,
			out KingdomHappeningLifecycleBook next, out KingdomHappeningLifecycleFault fault)
		{
			next = book;
			if (!Exact(book, eventId, out KingdomHappeningOperation operation))
			{
				fault = KingdomHappeningLifecycleFault.WrongOperation;
				return false;
			}
			if (operation.Phase != KingdomHappeningLifecyclePhase.Restoring
				|| nowTick < operation.UpdatedTick)
			{
				fault = KingdomHappeningLifecycleFault.WrongPhase;
				return false;
			}
			KingdomHappeningParticipant[] people = operation.CopyParticipants();
			bool fixtureRestored = operation.FixtureRestored;
			if (fixture)
				fixtureRestored = true;
			else
			{
				if (participantIndex < 0 || participantIndex >= people.Length)
				{
					fault = KingdomHappeningLifecycleFault.Malformed;
					return false;
				}
				people[participantIndex] = people[participantIndex].WithRestored();
			}
			KingdomHappeningOperation changed = operation.WithRestoration(people,
				fixtureRestored, nowTick);
			if (!ValidOperation(changed))
			{
				fault = KingdomHappeningLifecycleFault.Malformed;
				return false;
			}
			next = new KingdomHappeningLifecycleBook(book.Sequence, changed,
				book.SemanticReceipts);
			fault = KingdomHappeningLifecycleFault.None;
			return true;
		}

		internal static KingdomHappeningLifecycleBook RecoverInterruptedSinks(
			KingdomHappeningLifecycleBook book, long nowTick)
		{
			KingdomHappeningOperation operation = book == null ? null : book.Active;
			if (operation == null || nowTick < operation.UpdatedTick) return book;
			KingdomHappeningSinkState effect = RecoverUninspectable(operation.EffectState);
			KingdomHappeningSinkState ledger = RecoverUninspectable(operation.LedgerState);
			KingdomHappeningSinkState message = RecoverUninspectable(operation.MessageState);
			if (effect == operation.EffectState && ledger == operation.LedgerState
				&& message == operation.MessageState) return book;
			return new KingdomHappeningLifecycleBook(book.Sequence, operation.WithSinks(
				operation.ChronicleState, operation.ToldState, effect, ledger, message, nowTick),
				book.SemanticReceipts);
		}

		internal static KingdomHappeningResumeAction ResumeAction(
			KingdomHappeningOperation operation, long nowTick, bool founderHere,
			bool fixtureExact, bool participantsExact, bool allArrived, bool useReceiptExact)
		{
			if (!ValidOperation(operation) || nowTick <= 0L)
				return KingdomHappeningResumeAction.Refuse;
			if (operation.Phase == KingdomHappeningLifecyclePhase.Restoring)
				return KingdomHappeningResumeAction.Restore;
			// Ready is durable proof that attendance already completed. Later zone departure,
			// fixture loss, or reload cannot retroactively turn a witnessed rite into a report.
			if (operation.Phase == KingdomHappeningLifecyclePhase.Ready)
			{
				if (operation.ExternalSemantic
					&& nowTick - operation.UpdatedTick >= ExternalReadyTimeoutTicks)
					return KingdomHappeningResumeAction.Restore;
				return operation.ExternalSemantic ? KingdomHappeningResumeAction.WaitExternal
					: KingdomHappeningResumeAction.Publish;
			}
			if (!operation.Physical) return KingdomHappeningResumeAction.Refuse;
			if (!fixtureExact || !participantsExact || !founderHere
				|| (operation.Phase == KingdomHappeningLifecyclePhase.Walking
					&& nowTick - operation.StartedTick >= WalkTimeoutTicks))
				return KingdomHappeningResumeAction.Restore;
			switch (operation.Phase)
			{
			case KingdomHappeningLifecyclePhase.Prepared:
				return KingdomHappeningResumeAction.PreparePosts;
			case KingdomHappeningLifecyclePhase.Walking:
				return allArrived ? KingdomHappeningResumeAction.BeginHold
					: KingdomHappeningResumeAction.WaitForArrival;
			case KingdomHappeningLifecyclePhase.Holding:
				if (!allArrived || !useReceiptExact)
					return KingdomHappeningResumeAction.Restore;
				return nowTick < operation.HoldUntilTick
					? KingdomHappeningResumeAction.WaitHold
					: KingdomHappeningResumeAction.Publish;
			default:
				return KingdomHappeningResumeAction.Refuse;
			}
		}

		internal static bool SinksSettled(KingdomHappeningOperation operation)
		{
			return operation != null && Terminal(operation.ChronicleState)
				&& Terminal(operation.ToldState) && Terminal(operation.EffectState)
				&& Terminal(operation.LedgerState) && Terminal(operation.MessageState);
		}

		internal static bool RestorationSettled(KingdomHappeningOperation operation)
		{
			if (operation == null || !operation.FixtureRestored) return false;
			for (int i = 0; i < operation.Participants.Length; i++)
				if (!operation.Participants[i].Restored) return false;
			return true;
		}

		internal static bool AlreadyCompleted(KingdomHappeningLifecycleBook book,
			KingdomPhysicalHappeningKind kind, int subjectA, int subjectB)
		{
			if (book == null || !PermanentSemantic(kind)) return false;
			KingdomHappeningSemanticReceipt expected = new KingdomHappeningSemanticReceipt(kind,
				subjectA, subjectB);
			for (int i = 0; i < book.SemanticReceipts.Length; i++)
			{
				KingdomHappeningSemanticReceipt row = book.SemanticReceipts[i];
				if (row.Kind == expected.Kind && row.SubjectA == expected.SubjectA
					&& row.SubjectB == expected.SubjectB) return true;
			}
			return false;
		}

		internal static bool Matches(KingdomHappeningOperation operation,
			KingdomPhysicalHappeningKind kind, long eventTick, int subjectA, int subjectB,
			int outcome)
		{
			if (operation == null || operation.Kind != kind || operation.SubjectA != subjectA
				|| operation.SubjectB != subjectB || operation.Outcome != outcome) return false;
			return kind == KingdomPhysicalHappeningKind.Wedding
				|| operation.EventTick == eventTick;
		}

		internal static bool TryEncode(KingdomHappeningLifecycleBook book, out string wire)
		{
			return TryEncodeVersion(book, CurrentVersion, out wire);
		}

		internal static bool TryEncodeV1ForTests(KingdomHappeningLifecycleBook book,
			out string wire)
		{
			return TryEncodeVersion(book, PreviousVersion, out wire);
		}

		private static bool TryEncodeVersion(KingdomHappeningLifecycleBook book, int version,
			out string wire)
		{
			wire = null;
			if (!ValidBook(book) || (version != PreviousVersion && version != CurrentVersion)
				|| (version == PreviousVersion && book.SemanticReceipts.Length != 0)) return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(Magic);
					writer.Write(version);
					writer.Write(book.Sequence);
					writer.Write(book.Active != null ? (byte)1 : (byte)0);
					if (book.Active != null) WriteOperation(writer, book.Active, version);
					if (version >= CurrentVersion)
					{
						writer.Write(book.SemanticReceipts.Length);
						for (int i = 0; i < book.SemanticReceipts.Length; i++)
						{
							writer.Write((byte)book.SemanticReceipts[i].Kind);
							writer.Write(book.SemanticReceipts[i].SubjectA);
							writer.Write(book.SemanticReceipts[i].SubjectB);
						}
					}
					writer.Flush();
					if (stream.Length > MaxPayloadBytes) return false;
					wire = Convert.ToBase64String(stream.ToArray());
					return wire.Length <= MaxWireChars;
				}
			}
			catch { wire = null; return false; }
		}

		internal static bool TryDecode(string wire, out KingdomHappeningLifecycleBook book,
			out KingdomHappeningLifecycleFault fault)
		{
			book = null;
			fault = KingdomHappeningLifecycleFault.Malformed;
			if (string.IsNullOrEmpty(wire))
			{
				book = KingdomHappeningLifecycleBook.Empty;
				fault = KingdomHappeningLifecycleFault.None;
				return true;
			}
			if (wire.Length > MaxWireChars) { fault = KingdomHappeningLifecycleFault.OverBudget; return false; }
			try
			{
				byte[] payload = Convert.FromBase64String(wire);
				if (payload.Length > MaxPayloadBytes)
				{
					fault = KingdomHappeningLifecycleFault.OverBudget;
					return false;
				}
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
				{
					if (reader.ReadInt32() != Magic) return false;
					int version = reader.ReadInt32();
					if (version != PreviousVersion && version != CurrentVersion)
					{
						fault = KingdomHappeningLifecycleFault.UnsupportedVersion;
						return false;
					}
					int sequence = reader.ReadInt32();
					byte present = reader.ReadByte();
					if (present > 1) return false;
					KingdomHappeningOperation active = present == 1
						? ReadOperation(reader, version) : null;
					KingdomHappeningSemanticReceipt[] receipts =
						new KingdomHappeningSemanticReceipt[0];
					if (version >= CurrentVersion)
					{
						int count = reader.ReadInt32();
						if (count < 0 || count > MaxSemanticReceipts)
							throw new InvalidDataException();
						receipts = new KingdomHappeningSemanticReceipt[count];
						for (int i = 0; i < count; i++)
							receipts[i] = new KingdomHappeningSemanticReceipt(
								ReadEnum<KingdomPhysicalHappeningKind>(reader),
								reader.ReadInt32(), reader.ReadInt32());
					}
					if (stream.Position != stream.Length) return false;
					KingdomHappeningLifecycleBook decoded = new KingdomHappeningLifecycleBook(sequence,
						active, receipts);
					if (!ValidBook(decoded)) return false;
					book = decoded;
					fault = KingdomHappeningLifecycleFault.None;
					return true;
				}
			}
			catch { return false; }
		}

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
					&& proposal.Kind != KingdomPhysicalHappeningKind.Raising)
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
					&& operation.Kind != KingdomPhysicalHappeningKind.Raising)
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

		private static void WriteOperation(BinaryWriter writer,
			KingdomHappeningOperation operation, int version)
		{
			writer.Write(operation.Sequence);
			WriteString(writer, operation.EventId);
			writer.Write((byte)operation.Kind);
			writer.Write((byte)operation.Phase);
			writer.Write(operation.EventTick);
			writer.Write(operation.StartedTick);
			writer.Write(operation.UpdatedTick);
			writer.Write(operation.HoldUntilTick);
			writer.Write(operation.SubjectA);
			writer.Write(operation.SubjectB);
			writer.Write(operation.Outcome);
			WriteString(writer, operation.SettlementId);
			WriteString(writer, operation.ZoneId);
			WriteString(writer, operation.FixtureObjectId);
			WriteString(writer, operation.FixtureBlueprint);
			writer.Write(operation.FixtureX);
			writer.Write(operation.FixtureY);
			writer.Write(operation.Physical);
			writer.Write(operation.ExternalSemantic);
			writer.Write(operation.Attended);
			if (version >= CurrentVersion) writer.Write(operation.FixtureRestored);
			WriteString(writer, operation.ChronicleAttended);
			WriteString(writer, operation.ChronicleUnattended);
			WriteString(writer, operation.LedgerAttended);
			WriteString(writer, operation.LedgerUnattended);
			WriteString(writer, operation.MessageAttended);
			WriteString(writer, operation.MessageUnattended);
			WriteString(writer, operation.Effect);
			WriteString(writer, operation.DisplayName);
			WriteString(writer, operation.PlanQuote);
			writer.Write((byte)operation.ChronicleState);
			writer.Write((byte)operation.ToldState);
			writer.Write((byte)operation.EffectState);
			writer.Write((byte)operation.LedgerState);
			writer.Write((byte)operation.MessageState);
			writer.Write(operation.Participants.Length);
			for (int i = 0; i < operation.Participants.Length; i++)
				WriteParticipant(writer, operation.Participants[i], version);
		}

		private static KingdomHappeningOperation ReadOperation(BinaryReader reader, int version)
		{
			int sequence = reader.ReadInt32();
			string eventId = ReadString(reader);
			KingdomPhysicalHappeningKind kind = ReadEnum<KingdomPhysicalHappeningKind>(reader);
			KingdomHappeningLifecyclePhase phase = ReadEnum<KingdomHappeningLifecyclePhase>(reader);
			long eventTick = reader.ReadInt64();
			long started = reader.ReadInt64();
			long updated = reader.ReadInt64();
			long hold = reader.ReadInt64();
			int subjectA = reader.ReadInt32();
			int subjectB = reader.ReadInt32();
			int outcome = reader.ReadInt32();
			string settlementId = ReadString(reader);
			string zoneId = ReadString(reader);
			string fixtureId = ReadString(reader);
			string fixtureBlueprint = ReadString(reader);
			int fixtureX = reader.ReadInt32();
			int fixtureY = reader.ReadInt32();
			bool physical = ReadBool(reader);
			bool external = ReadBool(reader);
			bool attended = ReadBool(reader);
			bool fixtureRestored = version >= CurrentVersion ? ReadBool(reader) : !physical;
			string chronicleAttended = ReadString(reader);
			string chronicleUnattended = ReadString(reader);
			string ledgerAttended = ReadString(reader);
			string ledgerUnattended = ReadString(reader);
			string messageAttended = ReadString(reader);
			string messageUnattended = ReadString(reader);
			string effect = ReadString(reader);
			string display = ReadString(reader);
			string plan = ReadString(reader);
			KingdomHappeningSinkState chronicle = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState told = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState effectState = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState ledger = ReadEnum<KingdomHappeningSinkState>(reader);
			KingdomHappeningSinkState message = ReadEnum<KingdomHappeningSinkState>(reader);
			int count = reader.ReadInt32();
			if (count < 0 || count > MaxParticipants) throw new InvalidDataException();
			KingdomHappeningParticipant[] participants = new KingdomHappeningParticipant[count];
			for (int i = 0; i < count; i++) participants[i] = ReadParticipant(reader, version);
			return new KingdomHappeningOperation(sequence, eventId, kind, phase, eventTick,
				started, updated, hold, subjectA, subjectB, outcome, settlementId, zoneId,
				fixtureId, fixtureBlueprint, fixtureX, fixtureY, physical, external, attended,
				fixtureRestored,
				chronicleAttended, chronicleUnattended, ledgerAttended, ledgerUnattended,
				messageAttended, messageUnattended, effect, display, plan, participants,
				chronicle, told, effectState, ledger, message);
		}

		private static void WriteParticipant(BinaryWriter writer,
			KingdomHappeningParticipant participant, int version)
		{
			writer.Write(participant.ResidentId);
			WriteString(writer, participant.ObjectId);
			WriteString(writer, participant.Name);
			WriteString(writer, participant.Home);
			WriteString(writer, participant.Anchor);
			writer.Write(participant.OriginalX);
			writer.Write(participant.OriginalY);
			writer.Write(participant.TargetX);
			writer.Write(participant.TargetY);
			writer.Write(participant.PostWorkId);
			writer.Write(participant.PostKind);
			writer.Write(participant.Wanders);
			writer.Write(participant.WandersRandomly);
			writer.Write(participant.Staying);
			if (version >= CurrentVersion) writer.Write(participant.Restored);
		}

		private static KingdomHappeningParticipant ReadParticipant(BinaryReader reader, int version)
		{
			return new KingdomHappeningParticipant(reader.ReadInt32(), ReadString(reader),
				ReadString(reader), ReadString(reader), ReadString(reader), reader.ReadInt32(),
				reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
				reader.ReadInt32(), ReadBool(reader), ReadBool(reader), ReadBool(reader),
				version >= CurrentVersion && ReadBool(reader));
		}

		private static bool ReadBool(BinaryReader reader)
		{
			byte value = reader.ReadByte();
			if (value > 1) throw new InvalidDataException();
			return value == 1;
		}

		private static void WriteString(BinaryWriter writer, string value)
		{
			byte[] bytes = StrictUtf8.GetBytes(value ?? "");
			if (bytes.Length > MaxStringBytes) throw new InvalidDataException();
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string ReadString(BinaryReader reader)
		{
			int count = reader.ReadInt32();
			if (count < 0 || count > MaxStringBytes) throw new InvalidDataException();
			byte[] bytes = reader.ReadBytes(count);
			if (bytes.Length != count) throw new EndOfStreamException();
			return StrictUtf8.GetString(bytes);
		}

		private static T ReadEnum<T>(BinaryReader reader) where T : struct
		{
			T value = (T)Enum.ToObject(typeof(T), reader.ReadByte());
			if (!Enum.IsDefined(typeof(T), value)) throw new InvalidDataException();
			return value;
		}
	}
}
