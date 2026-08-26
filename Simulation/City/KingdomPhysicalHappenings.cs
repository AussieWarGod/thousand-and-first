using System;
using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.AI.Pathfinding;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal enum KingdomPhysicalQueueResult : byte
	{
		Refused = 0,
		Pending = 1,
		Unattended = 2,
		AttendedReady = 3,
		Busy = 4,
		AlreadyCompleted = 5
	}

	[Serializable]
	internal sealed class KingdomHappeningMoveTo : MoveTo
	{
		public string HappeningEventId;

		public KingdomHappeningMoveTo()
		{
		}

		internal KingdomHappeningMoveTo(string eventId, Cell target)
			: base(target, careful: true)
		{
			HappeningEventId = eventId ?? "";
		}
	}

	/// <summary>
	/// Projects one city-owned happening onto exact resident bodies and an authored functional
	/// fixture. The city-book sidecar is authority; body and fixture properties are recoverable
	/// projections. Movement is exclusively vanilla <c>MoveTo</c>/anchor pathing.
	/// </summary>
	internal static class KingdomPhysicalHappenings
	{
		internal const string TokenProperty = "r_TAF_HappeningToken";
		internal const string PostReceiptProperty = "r_TAF_HappeningPostBefore";
		internal const string AnchorReceiptProperty = "r_TAF_HappeningAnchorBefore";
		internal const string HomeReceiptProperty = "r_TAF_HappeningHomeBefore";
		internal const string TargetReceiptProperty = "r_TAF_HappeningTarget";
		internal const string FixtureReceiptProperty = "r_TAF_HappeningFixture";
		internal const string OriginalReceiptProperty = "r_TAF_HappeningOriginal";
		internal const string WandersReceiptProperty = "r_TAF_HappeningWandersBefore";
		internal const string RandomReceiptProperty = "r_TAF_HappeningRandomBefore";
		internal const string StayingReceiptProperty = "r_TAF_HappeningStayingBefore";
		internal const string FixtureTokenProperty = "r_TAF_HappeningLocusToken";
		internal const string FixtureUseProperty = "r_TAF_HappeningUseToken";

		private const int MaxPathSteps = 256;

		internal static bool IsStaged(GameObject body)
		{
			return GameObject.Validate(body)
				&& !string.IsNullOrEmpty(body.GetStringProperty(TokenProperty));
		}

		internal static string EventId(string settlementId, KingdomPhysicalHappeningKind kind,
			long eventTick, int subjectA, int subjectB, int outcome)
		{
			return "taf:happening:" + (settlementId ?? "") + ":"
				+ ((int)kind).ToString(CultureInfo.InvariantCulture) + ":"
				+ eventTick.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectA.ToString(CultureInfo.InvariantCulture) + ":"
				+ subjectB.ToString(CultureInfo.InvariantCulture) + ":"
				+ outcome.ToString(CultureInfo.InvariantCulture);
		}

		internal static bool AlreadyCompleted(KingdomCityBook book,
			KingdomPhysicalHappeningKind kind, int subjectA, int subjectB, long nowTick)
		{
			return TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				&& KingdomHappeningLifecycleRules.AlreadyCompleted(lifecycle, kind, subjectA,
					subjectB);
		}

		internal static KingdomPhysicalQueueResult QueueGeneric(KingdomSystem system,
			KingdomCityBook book, KingdomPhysicalHappeningKind kind, long eventTick,
			int subjectA, int subjectB, int outcome, Zone zone, int[] requiredResidents,
			string chronicleAttended, string chronicleUnattended, string ledgerAttended,
			string ledgerUnattended, string messageAttended, string messageUnattended,
			string effect, string displayName, long nowTick)
		{
			return Queue(system, book, kind, eventTick, subjectA, subjectB, outcome, zone,
				requiredResidents, false, false, chronicleAttended, chronicleUnattended,
				ledgerAttended, ledgerUnattended, messageAttended, messageUnattended, effect,
				displayName, "", nowTick, out string[] ignoredNames);
		}

		internal static KingdomPhysicalQueueResult QueueRaising(KingdomSystem system,
			KingdomCityBook book, string constructionId, long eventTick, Zone zone,
			string displayName, string planQuote, long nowTick, out string eventId,
			out string[] names)
		{
			eventId = EventId(book == null ? "" : book.SettlementId,
				KingdomPhysicalHappeningKind.Raising, eventTick,
				KingdomCityRules.StableId(constructionId ?? ""), 0, 0);
			return Queue(system, book, KingdomPhysicalHappeningKind.Raising, eventTick,
				KingdomCityRules.StableId(constructionId ?? ""), 0, 0, zone, null, true, true,
				"", "", "", "", "", "", "", displayName, planQuote, nowTick,
				out names, eventId);
		}

		internal static int Drive(KingdomSystem system, KingdomCityBook book, string label,
			bool here, long nowTick, int pushBudget)
		{
			KingdomPhysicalQueueResult result = DriveCore(system, book, label, here, nowTick,
				pushBudget < 0 ? 0 : pushBudget, out int pushed);
			return result == KingdomPhysicalQueueResult.Refused ? 0 : pushed;
		}

		internal static bool AcknowledgeRaising(KingdomSystem system, KingdomCityBook book,
			string eventId, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			if (!string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal)
				|| !lifecycle.Active.ExternalSemantic) return false;
			if (lifecycle.Active.Phase == KingdomHappeningLifecyclePhase.Restoring)
			{
				DriveCore(system, book, system.SeatName, StandsIn(lifecycle.Active.ZoneId), nowTick,
					0, out int ignoredRestore);
				return TryRead(book, nowTick, out lifecycle) && lifecycle.Active == null;
			}
			if (lifecycle.Active.Phase != KingdomHappeningLifecyclePhase.Ready
				|| !lifecycle.Active.Attended
				|| nowTick - lifecycle.Active.UpdatedTick
					>= KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks) return false;
			if (!SetPhase(book, lifecycle, KingdomHappeningLifecyclePhase.Ready,
				KingdomHappeningLifecyclePhase.Restoring, true, 0L, nowTick)) return false;
			DriveCore(system, book, system.SeatName, StandsIn(lifecycle.Active.ZoneId), nowTick,
				0, out int ignored);
			if (!TryRead(book, nowTick, out lifecycle)) return false;
			return lifecycle.Active == null;
		}

		internal static bool ReconcileSettledRaising(KingdomSystem system, KingdomCityBook book,
			string constructionId, long nowTick)
		{
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)) return false;
			if (lifecycle.Active == null) return true;
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation.Kind != KingdomPhysicalHappeningKind.Raising
				|| operation.SubjectA != KingdomCityRules.StableId(constructionId ?? "")
				|| !operation.ExternalSemantic) return true;
			return AcknowledgeRaising(system, book, operation.EventId, nowTick);
		}

		internal static bool TryReadyRaising(KingdomCityBook book, string constructionId,
			long nowTick, out string eventId, out string[] names)
		{
			eventId = null;
			names = new string[0];
			if (string.IsNullOrEmpty(constructionId)
				|| !TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null) return false;
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation.Kind != KingdomPhysicalHappeningKind.Raising
				|| operation.SubjectA != KingdomCityRules.StableId(constructionId)
				|| !operation.Physical || !operation.ExternalSemantic || !operation.Attended
				|| operation.Phase != KingdomHappeningLifecyclePhase.Ready
				|| nowTick - operation.UpdatedTick
					>= KingdomHappeningLifecycleRules.ExternalReadyTimeoutTicks) return false;
			eventId = operation.EventId;
			names = Names(operation);
			return true;
		}

		internal static bool ExactTold(KingdomCityState state,
			KingdomPhysicalHappeningKind kind, long tick, int subjectA, int subjectB,
			string zoneId, int outcome)
		{
			KingdomToldKind toldKind = ToldKind(kind);
			if (state == null || toldKind == KingdomToldKind.None) return false;
			for (int i = 0; i < state.ToldCount; i++)
			{
				if (state.TryTold(i, out KingdomToldRow row) && row.Kind == toldKind
					&& row.Tick == tick && row.SubjectA == subjectA && row.SubjectB == subjectB
					&& row.Outcome == outcome && string.Equals(row.PlaceZoneId ?? "",
						zoneId ?? "", StringComparison.Ordinal)) return true;
			}
			return false;
		}

		private static KingdomPhysicalQueueResult Queue(KingdomSystem system,
			KingdomCityBook book, KingdomPhysicalHappeningKind kind, long eventTick,
			int subjectA, int subjectB, int outcome, Zone zone, int[] requiredResidents,
			bool externalSemantic, bool preferConstruction, string chronicleAttended,
			string chronicleUnattended, string ledgerAttended, string ledgerUnattended,
			string messageAttended, string messageUnattended, string effect,
			string displayName, string planQuote, long nowTick, out string[] names,
			string fixedEventId = null)
		{
			names = new string[0];
			if (system == null || !system.Founded || book == null || eventTick <= 0L
				|| nowTick <= 0L || kind == KingdomPhysicalHappeningKind.None)
				return KingdomPhysicalQueueResult.Refused;
			string settlementId = book.SettlementId ?? "";
			string eventId = fixedEventId ?? EventId(settlementId, kind, eventTick,
				subjectA, subjectB, outcome);
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle))
				return KingdomPhysicalQueueResult.Refused;
			if (zone != null) ReconcileZoneProjections(zone, settlementId,
				lifecycle.Active == null ? "" : lifecycle.Active.EventId);
			if (KingdomHappeningLifecycleRules.AlreadyCompleted(lifecycle, kind, subjectA,
				subjectB)) return KingdomPhysicalQueueResult.AlreadyCompleted;
			if (lifecycle.Active != null)
			{
				if (!KingdomHappeningLifecycleRules.Matches(lifecycle.Active, kind, eventTick,
					subjectA, subjectB, outcome)) return KingdomPhysicalQueueResult.Busy;
				KingdomPhysicalQueueResult resumed = DriveCore(system, book, system.SeatName,
					StandsIn(lifecycle.Active.ZoneId), nowTick, 0, out int ignored);
				if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null
					&& string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal))
					names = Names(lifecycle.Active);
				return resumed;
			}
			if (zone == null || !StandsIn(zone.ZoneID) || !OwnedGround(system, zone.ZoneID))
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			GameObject fixture = FindFixture(zone, kind);
			if (!GameObject.Validate(fixture) || fixture.CurrentCell == null)
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			if (!TryParticipants(system, zone, fixture, kind, requiredResidents,
				preferConstruction, out KingdomHappeningParticipant[] participants))
				return externalSemantic ? KingdomPhysicalQueueResult.Unattended
					: OpenReport(system, book, lifecycle, eventId, kind, eventTick, subjectA,
						subjectB, outcome, chronicleAttended, chronicleUnattended,
						ledgerAttended, ledgerUnattended, messageAttended, messageUnattended,
						effect, displayName, planQuote, nowTick);
			if (kind == KingdomPhysicalHappeningKind.Raising && !externalSemantic)
			{
				List<string> present = new List<string>();
				for (int i = 0; i < participants.Length; i++) present.Add(participants[i].Name);
				chronicleAttended = KingdomCeremonyRules.RaisingAttendedChronicle(displayName,
					system.SeatName, present, planQuote);
				messageAttended = KingdomCeremonyRules.RaisingAttendedMessage(displayName,
					present);
			}
			KingdomHappeningProposal proposal = new KingdomHappeningProposal(eventId, kind,
				eventTick, subjectA, subjectB, outcome, settlementId, zone.ZoneID, fixture.ID,
				fixture.Blueprint, fixture.CurrentCell.X, fixture.CurrentCell.Y, true, externalSemantic,
				chronicleAttended, chronicleUnattended, ledgerAttended, ledgerUnattended,
				messageAttended, messageUnattended, effect, displayName, planQuote, participants);
			if (!KingdomHappeningLifecycleRules.TryOpen(lifecycle, proposal, nowTick,
				out KingdomHappeningLifecycleBook opened,
				out KingdomHappeningLifecycleFault fault) || !Write(book, opened))
			{
				KingdomLog.Log("happening physical: open refused (" + fault + ") for " + eventId);
				return KingdomPhysicalQueueResult.Refused;
			}
			names = Names(opened.Active);
			return DriveCore(system, book, system.SeatName, true, nowTick, 0,
				out int ignoredPush);
		}

		private static KingdomPhysicalQueueResult OpenReport(KingdomSystem system,
			KingdomCityBook book, KingdomHappeningLifecycleBook lifecycle, string eventId,
			KingdomPhysicalHappeningKind kind, long eventTick, int subjectA, int subjectB,
			int outcome, string chronicleAttended, string chronicleUnattended,
			string ledgerAttended, string ledgerUnattended, string messageAttended,
			string messageUnattended, string effect, string displayName, string planQuote,
			long nowTick)
		{
			KingdomHappeningProposal report = new KingdomHappeningProposal(eventId, kind,
				eventTick, subjectA, subjectB, outcome, book.SettlementId,
				"", "", "", 0, 0, false, false, chronicleAttended, chronicleUnattended,
				ledgerAttended, ledgerUnattended, messageAttended, messageUnattended, effect,
				displayName, planQuote, null);
			if (!KingdomHappeningLifecycleRules.TryOpen(lifecycle, report, nowTick,
				out KingdomHappeningLifecycleBook opened,
				out KingdomHappeningLifecycleFault fault) || !Write(book, opened))
			{
				KingdomLog.Log("happening report: open refused (" + fault + ") for " + eventId);
				return KingdomPhysicalQueueResult.Refused;
			}
			return DriveCore(system, book, system.SeatName, false, nowTick, 0,
				out int ignoredPush);
		}

		private static KingdomPhysicalQueueResult DriveCore(KingdomSystem system,
			KingdomCityBook book, string label, bool here, long nowTick, int pushBudget,
			out int pushed)
		{
			pushed = 0;
			if (TryReadRaw(book, out KingdomHappeningLifecycleBook standing))
				ReconcileZoneProjections(The.Player?.CurrentZone, book.SettlementId,
					standing.Active == null ? "" : standing.Active.EventId);
			for (int step = 0; step < 8; step++)
			{
				if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle))
					return KingdomPhysicalQueueResult.Refused;
				KingdomHappeningOperation operation = lifecycle.Active;
				if (operation == null) return KingdomPhysicalQueueResult.Unattended;
				bool founderHere = here && StandsIn(operation.ZoneId);
				Evidence evidence = Observe(system, operation);
				KingdomHappeningResumeAction action = KingdomHappeningLifecycleRules.ResumeAction(
					operation, nowTick, founderHere, evidence.FixtureExact,
					evidence.ParticipantsExact, evidence.AllArrived, evidence.UseReceiptExact);
				switch (action)
				{
				case KingdomHappeningResumeAction.PreparePosts:
					if (!Prepare(operation, evidence))
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!SetPhase(book, lifecycle, operation.Phase,
						KingdomHappeningLifecyclePhase.Walking, false, 0L, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.WaitForArrival:
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.BeginHold:
					if (!StampUse(operation, evidence))
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!SetPhase(book, lifecycle, operation.Phase,
						KingdomHappeningLifecyclePhase.Holding, false,
						nowTick + KingdomHappeningLifecycleRules.HoldTicks, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.WaitHold:
					return KingdomPhysicalQueueResult.Pending;

				case KingdomHappeningResumeAction.Publish:
					if (operation.Phase != KingdomHappeningLifecyclePhase.Ready)
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Ready, true, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (operation.ExternalSemantic) return KingdomPhysicalQueueResult.AttendedReady;
					pushed += PublishGeneric(system, book, operation, label,
						pushBudget - pushed, nowTick);
					if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
						return KingdomPhysicalQueueResult.Refused;
					if (!KingdomHappeningLifecycleRules.SinksSettled(lifecycle.Active))
						return KingdomPhysicalQueueResult.Pending;
					if (!SetPhase(book, lifecycle, KingdomHappeningLifecyclePhase.Ready,
						KingdomHappeningLifecyclePhase.Restoring, true, 0L, nowTick))
						return KingdomPhysicalQueueResult.Refused;
					continue;

				case KingdomHappeningResumeAction.WaitExternal:
					return KingdomPhysicalQueueResult.AttendedReady;

				case KingdomHappeningResumeAction.Restore:
					if (operation.Phase != KingdomHappeningLifecyclePhase.Restoring)
					{
						if (!SetPhase(book, lifecycle, operation.Phase,
							KingdomHappeningLifecyclePhase.Restoring, false, 0L, nowTick))
							return KingdomPhysicalQueueResult.Refused;
						continue;
					}
					if (!Restore(system, book, lifecycle, nowTick))
						return KingdomPhysicalQueueResult.Pending;
					if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
						return KingdomPhysicalQueueResult.Refused;
					operation = lifecycle.Active;
					if (!operation.Attended && !operation.ExternalSemantic)
					{
						pushed += PublishGeneric(system, book, operation, label,
							pushBudget - pushed, nowTick);
						if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null)
							return KingdomPhysicalQueueResult.Refused;
						operation = lifecycle.Active;
						if (!KingdomHappeningLifecycleRules.SinksSettled(operation))
							return KingdomPhysicalQueueResult.Pending;
					}
					if (operation.ExternalSemantic && !operation.Attended)
					{
						if (!Clear(book, lifecycle, operation.EventId))
							return KingdomPhysicalQueueResult.Refused;
						return KingdomPhysicalQueueResult.Unattended;
					}
					bool wasAttended = operation.Attended;
					if (!Clear(book, lifecycle, operation.EventId))
						return KingdomPhysicalQueueResult.Refused;
					return wasAttended ? KingdomPhysicalQueueResult.AttendedReady
						: KingdomPhysicalQueueResult.Unattended;

				default:
					KingdomLog.Log("happening physical: lifecycle refused for " + operation.EventId);
					return KingdomPhysicalQueueResult.Refused;
				}
			}
			return KingdomPhysicalQueueResult.Pending;
		}

		private static int PublishGeneric(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningOperation operation, string label, int pushBudget, long nowTick)
		{
			int pushed = 0;
			if (operation.ChronicleState == KingdomHappeningSinkState.Pending)
			{
				string line = operation.Attended ? operation.ChronicleAttended
					: operation.ChronicleUnattended;
				bool delivered = false;
				try { delivered = KingdomChronicle.RecordOnce(system,
					operation.EventId + ":chronicle", line); }
				catch { }
					SetSink(book, operation.EventId, SinkLane.Chronicle,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Pending, nowTick);
					if (!delivered) KingdomLog.Log("happening physical: chronicle deferred for "
						+ operation.EventId);
			}
			if (!TryRead(book, nowTick, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.ToldState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = PublishTold(book, operation);
					SetSink(book, operation.EventId, SinkLane.Told,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Pending, nowTick);
					if (!delivered) KingdomLog.Log("happening physical: told receipt deferred for "
					+ operation.EventId);
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.EffectState == KingdomHappeningSinkState.Attempting
				|| operation.LedgerState == KingdomHappeningSinkState.Attempting
				|| operation.MessageState == KingdomHappeningSinkState.Attempting)
			{
				KingdomHappeningLifecycleBook recovered =
					KingdomHappeningLifecycleRules.RecoverInterruptedSinks(lifecycle, nowTick);
				if (!ReferenceEquals(recovered, lifecycle)) Write(book, recovered);
				lifecycle = recovered;
				operation = recovered.Active;
			}
			if (operation.EffectState == KingdomHappeningSinkState.Pending)
			{
				if (BeginUninspectable(book, operation.EventId, SinkLane.Effect, nowTick))
				{
					bool delivered = ApplyEffect(book, operation);
					SetSink(book, operation.EventId, SinkLane.Effect,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			string ledger = operation.Attended ? operation.LedgerAttended
				: operation.LedgerUnattended;
			string message = operation.Attended ? operation.MessageAttended
				: operation.MessageUnattended;
			if (string.IsNullOrWhiteSpace(ledger)
				&& operation.LedgerState == KingdomHappeningSinkState.Pending)
			{
				SetSink(book, operation.EventId, SinkLane.Ledger,
					KingdomHappeningSinkState.Skipped, nowTick);
			}
			if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null)
				operation = lifecycle.Active;
			if (string.IsNullOrWhiteSpace(message)
				&& operation.MessageState == KingdomHappeningSinkState.Pending)
			{
				SetSink(book, operation.EventId, SinkLane.Message,
					KingdomHappeningSinkState.Skipped, nowTick);
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (pushBudget <= 0)
			{
				if (operation.LedgerState == KingdomHappeningSinkState.Pending)
					SetSink(book, operation.EventId, SinkLane.Ledger,
						KingdomHappeningSinkState.Skipped, nowTick);
				if (TryRead(book, nowTick, out lifecycle) && lifecycle.Active != null
					&& lifecycle.Active.MessageState == KingdomHappeningSinkState.Pending)
					SetSink(book, operation.EventId, SinkLane.Message,
						KingdomHappeningSinkState.Skipped, nowTick);
				return pushed;
			}
			if (operation.LedgerState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = BeginUninspectable(book, operation.EventId, SinkLane.Ledger,
					nowTick);
				if (delivered)
				{
					try { system.Ledger.Note("{{K|" + ledger + "}}"); }
					catch { delivered = false; }
					SetSink(book, operation.EventId, SinkLane.Ledger,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return pushed;
			operation = lifecycle.Active;
			if (operation.MessageState == KingdomHappeningSinkState.Pending)
			{
				bool delivered = BeginUninspectable(book, operation.EventId, SinkLane.Message,
					nowTick);
				if (delivered)
				{
					try
					{
						string said = operation.Attended ? message
							: KingdomBrinkRules.WordFrom(KingdomPresentation.Rich(
								KingdomWord.CityName(system, label)), message);
						MessageQueue.AddPlayerMessage(said);
						pushed = 1;
					}
					catch { delivered = false; }
					SetSink(book, operation.EventId, SinkLane.Message,
						delivered ? KingdomHappeningSinkState.Delivered
							: KingdomHappeningSinkState.Lost, nowTick);
				}
			}
			return pushed;
		}

		private static bool PublishTold(KingdomCityBook book,
			KingdomHappeningOperation operation)
		{
			if (!book.TryRead(out KingdomCityState state, out KingdomCityFault fault)) return false;
			if (ExactTold(state, operation.Kind, operation.EventTick, operation.SubjectA,
				operation.SubjectB, operation.ZoneId, operation.Outcome)) return true;
			KingdomToldKind kind = ToldKind(operation.Kind);
			if (kind == KingdomToldKind.None) return false;
			if (!state.TryTell(new KingdomToldRow(kind, operation.EventTick,
				operation.SubjectA, operation.SubjectB, operation.ZoneId, operation.Outcome),
				out KingdomCityState next, out fault)) return false;
			return book.TryPublish(next, out fault);
		}

		private static KingdomToldKind ToldKind(KingdomPhysicalHappeningKind kind)
		{
			switch (kind)
			{
			case KingdomPhysicalHappeningKind.Wedding: return KingdomToldKind.Wedding;
			case KingdomPhysicalHappeningKind.Funeral: return KingdomToldKind.Funeral;
			case KingdomPhysicalHappeningKind.Feast: return KingdomToldKind.Festival;
			case KingdomPhysicalHappeningKind.Raising: return KingdomToldKind.Raising;
			default: return KingdomToldKind.None;
			}
		}

		private static bool ApplyEffect(KingdomCityBook book,
			KingdomHappeningOperation operation)
		{
			if (string.IsNullOrEmpty(operation.Effect)) return true;
			if (operation.Kind != KingdomPhysicalHappeningKind.Feast) return false;
			int split = operation.Effect.IndexOf('\n');
			if (split <= 0 || split >= operation.Effect.Length - 1) return false;
				return KingdomHappenings.AccruePilgrim(book,
					operation.Effect.Substring(0, split), operation.Effect.Substring(split + 1),
					operation.EventTick);
		}

		private static bool BeginUninspectable(KingdomCityBook book, string eventId,
			SinkLane lane, long nowTick)
		{
			return SetSink(book, eventId, lane, KingdomHappeningSinkState.Attempting, nowTick);
		}

		private static bool SetSink(KingdomCityBook book, string eventId, SinkLane lane,
			KingdomHappeningSinkState value, long nowTick)
		{
			// This read must be raw. Attempting is the durable before-callback receipt; recovering
			// it here would turn the live callback's own receipt into Lost before it can acknowledge
			// Delivered. Recovery belongs only at a later top-level resume boundary.
			if (!TryReadRaw(book, out KingdomHappeningLifecycleBook lifecycle)
				|| lifecycle.Active == null
				|| !string.Equals(lifecycle.Active.EventId, eventId, StringComparison.Ordinal))
				return false;
			KingdomHappeningOperation operation = lifecycle.Active;
			KingdomHappeningSinkState chronicle = operation.ChronicleState;
			KingdomHappeningSinkState told = operation.ToldState;
			KingdomHappeningSinkState effect = operation.EffectState;
			KingdomHappeningSinkState ledger = operation.LedgerState;
			KingdomHappeningSinkState message = operation.MessageState;
			switch (lane)
			{
			case SinkLane.Chronicle: chronicle = value; break;
			case SinkLane.Told: told = value; break;
			case SinkLane.Effect: effect = value; break;
			case SinkLane.Ledger: ledger = value; break;
			case SinkLane.Message: message = value; break;
			}
			return KingdomHappeningLifecycleRules.TrySetSinks(lifecycle, eventId, chronicle,
				told, effect, ledger, message, nowTick,
				out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private static Evidence Observe(KingdomSystem system,
			KingdomHappeningOperation operation)
		{
			Zone zone = ExactLoadedZone(operation.ZoneId);
			GameObject fixture = FindById(zone, operation.FixtureObjectId);
			bool fixtureExact = GameObject.Validate(fixture) && fixture.CurrentCell != null
				&& fixture.Blueprint == operation.FixtureBlueprint
				&& fixture.CurrentCell.X == operation.FixtureX
				&& fixture.CurrentCell.Y == operation.FixtureY
				&& FunctionalFixture(operation.Kind, fixture);
			List<GameObject> bodies = new List<GameObject>(operation.Participants.Length);
			bool participantsExact = zone != null;
			bool allArrived = participantsExact;
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				KingdomHappeningParticipant row = operation.Participants[i];
				GameObject body;
				string bound;
				bool exact = KingdomResidents.TryResolveBoundBody(system, row.ResidentId, false,
					out body, out bound) && string.Equals(bound, operation.ZoneId,
					StringComparison.Ordinal) && string.Equals(body.IDIfAssigned, row.ObjectId,
					StringComparison.Ordinal) && NameOf(body) == row.Name && body.Brain != null
					&& PostReceipt(body) == PostReceipt(row)
					&& (body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "") == row.Home;
				if (exact && operation.Phase != KingdomHappeningLifecyclePhase.Prepared)
				{
					exact = ExactBodyReceipt(body, operation, row);
				}
				participantsExact &= exact;
				bodies.Add(exact ? body : null);
				Cell target = zone == null ? null : zone.GetCell(row.TargetX, row.TargetY);
				allArrived &= exact && target != null && body.CurrentCell == target;
			}
			bool useExact = fixtureExact && operation.Phase != KingdomHappeningLifecyclePhase.Holding
				&& operation.Phase != KingdomHappeningLifecyclePhase.Ready
				? true : fixtureExact && FunctionalUseExact(operation,
					new Evidence(zone, fixture, bodies, fixtureExact, participantsExact,
						allArrived, false));
			return new Evidence(zone, fixture, bodies, fixtureExact, participantsExact,
				allArrived, useExact);
		}

		private static bool Prepare(KingdomHappeningOperation operation, Evidence evidence)
		{
			if (!evidence.FixtureExact || !evidence.ParticipantsExact) return false;
			string standingFixture = evidence.Fixture.GetStringProperty(FixtureTokenProperty);
			if (!string.IsNullOrEmpty(standingFixture)
				&& standingFixture != operation.EventId) return false;
			evidence.Fixture.SetStringProperty(FixtureTokenProperty, operation.EventId);
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				GameObject body = evidence.Bodies[i];
				KingdomHappeningParticipant row = operation.Participants[i];
				string standing = body.GetStringProperty(TokenProperty);
				if (!string.IsNullOrEmpty(standing) && standing != operation.EventId) return false;
				body.SetStringProperty(TokenProperty, operation.EventId);
				body.SetStringProperty(PostReceiptProperty, PostReceipt(row));
				body.SetStringProperty(AnchorReceiptProperty, row.Anchor);
				body.SetStringProperty(HomeReceiptProperty, row.Home);
				body.SetStringProperty(TargetReceiptProperty, row.TargetX.ToString(
					CultureInfo.InvariantCulture) + "," + row.TargetY.ToString(
					CultureInfo.InvariantCulture));
				body.SetStringProperty(FixtureReceiptProperty, operation.FixtureObjectId);
				body.SetStringProperty(OriginalReceiptProperty, row.OriginalX.ToString(
					CultureInfo.InvariantCulture) + "," + row.OriginalY.ToString(
					CultureInfo.InvariantCulture));
				body.SetIntProperty(WandersReceiptProperty, row.Wanders ? 1 : 0);
				body.SetIntProperty(RandomReceiptProperty, row.WandersRandomly ? 1 : 0);
				body.SetIntProperty(StayingReceiptProperty, row.Staying ? 1 : 0);
				Cell target = evidence.Zone.GetCell(row.TargetX, row.TargetY);
				body.Brain.Wanders = false;
				body.Brain.WandersRandomly = false;
				body.Brain.Stay(target);
				if (body.CurrentCell != target && !HasOwnedGoal(body, operation.EventId))
					body.Brain.PushGoal(new KingdomHappeningMoveTo(operation.EventId, target));
				if (!ExactBodyReceipt(body, operation, row)) return false;
			}
			return evidence.Fixture.GetStringProperty(FixtureTokenProperty) == operation.EventId;
		}

		private static bool StampUse(KingdomHappeningOperation operation, Evidence evidence)
		{
			GameObject fixture = evidence.Fixture;
			if (!GameObject.Validate(fixture)
				|| fixture.GetStringProperty(FixtureTokenProperty) != operation.EventId) return false;
			string standing = fixture.GetStringProperty(FixtureUseProperty);
			if (standing == operation.EventId) return FunctionalUseExact(operation, evidence);
			if (standing == operation.EventId + ":attempt")
			{
				if (!FunctionalUseExact(operation, evidence)) return false;
				fixture.SetStringProperty(FixtureUseProperty, operation.EventId);
				return fixture.GetStringProperty(FixtureUseProperty) == operation.EventId;
			}
			if (!string.IsNullOrEmpty(standing)) return false;
			fixture.SetStringProperty(FixtureUseProperty, operation.EventId + ":attempt");
			if (!PerformFunctionalUse(operation, evidence)) return false;
			fixture.SetStringProperty(FixtureUseProperty, operation.EventId);
			return fixture.GetStringProperty(FixtureUseProperty) == operation.EventId
				&& FunctionalUseExact(operation, evidence);
		}

		private static bool PerformFunctionalUse(KingdomHappeningOperation operation,
			Evidence evidence)
		{
			if (!evidence.FixtureExact || evidence.Bodies.Count == 0
				|| !GameObject.Validate(evidence.Bodies[0])) return false;
			GameObject actor = evidence.Bodies[0];
			switch (operation.Kind)
			{
			case KingdomPhysicalHappeningKind.Wedding:
				Chair chair = evidence.Fixture.GetPart<Chair>();
				return chair != null && chair.SitDown(actor)
					&& actor.GetEffect<Sitting>()?.SittingOn == evidence.Fixture;
			case KingdomPhysicalHappeningKind.Funeral:
				Shrine shrine = evidence.Fixture.GetPart<Shrine>();
				return shrine != null && shrine.PrayAtShrine(actor, Silent: true);
			case KingdomPhysicalHappeningKind.Feast:
				Campfire fire = evidence.Fixture.GetPart<Campfire>();
				return fire != null && fire.IsReady(UseCharge: true)
					&& RadiatesHeatEvent.Check(evidence.Fixture);
			case KingdomPhysicalHappeningKind.Raising:
				LiquidVolume basin = evidence.Fixture.GetPart<LiquidVolume>();
				return basin != null && basin.MaxVolume > 0
					&& GetStorableDramsEvent.GetFor(evidence.Fixture, "water",
						LiquidVolume: basin) == basin.MaxVolume - basin.Volume;
			default:
				return false;
			}
		}

		private static bool FunctionalUseExact(KingdomHappeningOperation operation,
			Evidence evidence)
		{
			if (!evidence.FixtureExact
				|| evidence.Fixture.GetStringProperty(FixtureUseProperty)
					!= operation.EventId) return false;
			if (operation.Kind != KingdomPhysicalHappeningKind.Wedding) return true;
			return evidence.Bodies.Count > 0 && GameObject.Validate(evidence.Bodies[0])
				&& evidence.Bodies[0].GetEffect<Sitting>()?.SittingOn == evidence.Fixture;
		}

		private static bool Restore(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, long nowTick)
		{
			KingdomHappeningOperation operation = lifecycle.Active;
			if (operation == null) return false;
			if (!operation.Physical)
				return KingdomHappeningLifecycleRules.RestorationSettled(operation);
			Zone zone = ExactLoadedZone(operation.ZoneId);
			if (zone == null) return false;
			for (int i = 0; i < operation.Participants.Length; i++)
			{
				KingdomHappeningParticipant row = operation.Participants[i];
				if (row.Restored) continue;
				GameObject body;
				string bound;
				bool exact = KingdomResidents.TryResolveBoundBody(system, row.ResidentId, false,
					out body, out bound) && string.Equals(bound, operation.ZoneId,
						StringComparison.Ordinal) && string.Equals(body.IDIfAssigned, row.ObjectId,
						StringComparison.Ordinal);
				if (!exact && !ParticipantGone(system, book, row)) return false;
				if (exact)
				{
					if (body.Brain == null) return false;
					string token = body.GetStringProperty(TokenProperty);
					if (!string.IsNullOrEmpty(token) && token != operation.EventId) return false;
					RemoveOwnedGoal(body, operation.EventId);
					if (!StandFromWeddingFixture(body, operation)) return false;
					KingdomStations.Post(body, row.PostWorkId, (KingdomWorkKind)row.PostKind);
					body.Brain.Wanders = row.Wanders;
					body.Brain.WandersRandomly = row.WandersRandomly;
					body.Brain.Staying = row.Staying;
					if (string.IsNullOrEmpty(row.Anchor)) body.Brain.StartingCell = null;
					else body.Brain.StartingCell = new GlobalLocation(row.Anchor);
					Cell original = zone.GetCell(row.OriginalX, row.OriginalY);
					if (original == null) return false;
					if (body.CurrentCell != original)
					{
						if (!CanWalk(body, original)) return false;
						if (!HasOwnedGoal(body, operation.EventId + ":restore"))
							body.Brain.PushGoal(new KingdomHappeningMoveTo(
								operation.EventId + ":restore", original));
						return false;
					}
					RemoveOwnedGoal(body, operation.EventId + ":restore");
					ClearBodyProjection(body);
					if (!BodyScheduleRestored(body, row)) return false;
				}
				if (!MarkRestored(book, operation.EventId, i, false, nowTick)) return false;
				if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return false;
				operation = lifecycle.Active;
			}
			if (!operation.FixtureRestored)
			{
				if (!TryFindById(zone, operation.FixtureObjectId, out GameObject fixture,
					out bool fixtureAbsent)) return false;
				if (!fixtureAbsent)
				{
					string token = fixture.GetStringProperty(FixtureTokenProperty);
					string used = fixture.GetStringProperty(FixtureUseProperty);
					if ((!string.IsNullOrEmpty(token) && token != operation.EventId)
						|| (!string.IsNullOrEmpty(used)
							&& used != operation.EventId
							&& used != operation.EventId + ":attempt")) return false;
					fixture.RemoveStringProperty(FixtureTokenProperty);
					fixture.RemoveStringProperty(FixtureUseProperty);
					if (!string.IsNullOrEmpty(fixture.GetStringProperty(FixtureTokenProperty))
						|| !string.IsNullOrEmpty(fixture.GetStringProperty(FixtureUseProperty)))
						return false;
				}
				if (!MarkRestored(book, operation.EventId, -1, true, nowTick)) return false;
				if (!TryRead(book, nowTick, out lifecycle) || lifecycle.Active == null) return false;
				operation = lifecycle.Active;
			}
			return KingdomHappeningLifecycleRules.RestorationSettled(operation);
		}

		private static bool TryParticipants(KingdomSystem system, Zone zone,
			GameObject fixture, KingdomPhysicalHappeningKind kind, int[] requiredResidents,
			bool preferConstruction, out KingdomHappeningParticipant[] participants)
		{
			participants = null;
			List<GameObject> candidates = ExactResidents(system, zone);
			if (preferConstruction)
			{
				candidates.Sort(delegate(GameObject a, GameObject b)
				{
					bool ac = a.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
					bool bc = b.GetIntProperty(KingdomStations.PostKindProperty)
						== (int)KingdomWorkKind.Construction;
					if (ac != bc) return ac ? -1 : 1;
					return KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
				});
			}
			List<GameObject> selected = new List<GameObject>();
			for (int i = 0; requiredResidents != null && i < requiredResidents.Length; i++)
			{
				GameObject required = null;
				for (int j = 0; j < candidates.Count; j++)
					if (KingdomResidents.IdOf(candidates[j]) == requiredResidents[i])
						required = candidates[j];
				if (!GameObject.Validate(required) || selected.Contains(required)) return false;
				selected.Add(required);
			}
			for (int i = 0; i < candidates.Count
				&& selected.Count < KingdomHappeningLifecycleRules.MaxParticipants; i++)
				if (!selected.Contains(candidates[i])) selected.Add(candidates[i]);
			if (selected.Count == 0) return false;
			List<Cell> targets = OpenCells(zone, fixture, kind);
			List<KingdomHappeningParticipant> rows = new List<KingdomHappeningParticipant>();
			for (int i = 0; i < selected.Count; i++)
			{
				GameObject body = selected[i];
				Cell target = null;
				for (int j = 0; j < targets.Count; j++)
				{
					if (CanWalk(body, targets[j]))
					{
						target = targets[j];
						targets.RemoveAt(j);
						break;
					}
				}
				if (target == null)
				{
					if (requiredResidents != null && i < requiredResidents.Length) return false;
					continue;
				}
				Cell original = body.CurrentCell;
				string anchor = body.Brain.StartingCell == null ? ""
					: body.Brain.StartingCell.ToString();
				rows.Add(new KingdomHappeningParticipant(KingdomResidents.IdOf(body), body.ID,
					NameOf(body), body.GetStringProperty(KingdomLodging.HomePlotIdProperty) ?? "",
					anchor, original.X, original.Y, target.X, target.Y,
					KingdomStations.PostOf(body),
					body.GetIntProperty(KingdomStations.PostKindProperty), body.Brain.Wanders,
					body.Brain.WandersRandomly, body.Brain.Staying));
			}
			if (rows.Count == 0 || (requiredResidents != null
				&& rows.Count < requiredResidents.Length)) return false;
			participants = rows.ToArray();
			return true;
		}

		private static List<GameObject> ExactResidents(KingdomSystem system, Zone zone)
		{
			List<GameObject> result = new List<GameObject>();
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(zone))
			{
				if (!GameObject.Validate(candidate) || !candidate.IsAlive || candidate.Brain == null
					|| candidate.IsPlayer() || candidate.IsPlayerLed() || IsStaged(candidate)) continue;
				int residentId = KingdomResidents.IdOf(candidate);
				GameObject exact;
				string bound;
				if (residentId > 0 && KingdomResidents.TryResolveBoundBody(system, residentId,
					false, out exact, out bound) && ReferenceEquals(exact, candidate)
					&& bound == zone.ZoneID && !string.IsNullOrEmpty(NameOf(candidate)))
					result.Add(candidate);
			}
			result.Sort(delegate(GameObject a, GameObject b)
			{
				return KingdomResidents.IdOf(a).CompareTo(KingdomResidents.IdOf(b));
			});
			return result;
		}

		private static GameObject FindFixture(Zone zone, KingdomPhysicalHappeningKind kind)
		{
			GameObject best = null;
			int bestPriority = int.MaxValue;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(zone))
			{
				if (!FunctionalFixture(kind, candidate) || candidate.CurrentCell == null) continue;
				int priority = FixturePriority(kind, candidate);
				if (best == null || priority < bestPriority
					|| (priority == bestPriority && (candidate.CurrentCell.Y < best.CurrentCell.Y
						|| (candidate.CurrentCell.Y == best.CurrentCell.Y
							&& candidate.CurrentCell.X < best.CurrentCell.X))))
				{
					best = candidate;
					bestPriority = priority;
				}
			}
			return best;
		}

		private static bool FunctionalFixture(KingdomPhysicalHappeningKind kind,
			GameObject fixture)
		{
			if (!GameObject.Validate(fixture) || fixture.CurrentCell == null) return false;
			bool authored = fixture.GetIntProperty("KingdomBuilt") == 1
				|| (fixture.Blueprint ?? "").StartsWith("r_Kingdom", StringComparison.Ordinal);
			if (!authored) return false;
			switch (kind)
			{
			case KingdomPhysicalHappeningKind.Wedding:
				return fixture.GetPart<Chair>() != null;
			case KingdomPhysicalHappeningKind.Funeral:
				return fixture.HasPart("Shrine");
			case KingdomPhysicalHappeningKind.Feast:
				return fixture.HasPart("Campfire");
			case KingdomPhysicalHappeningKind.Raising:
				return fixture.Blueprint == "r_KingdomFirstBasin"
					&& fixture.HasPart("LiquidVolume");
			default:
				return false;
			}
		}

		private static int FixturePriority(KingdomPhysicalHappeningKind kind, GameObject fixture)
		{
			if (kind == KingdomPhysicalHappeningKind.Feast)
				return fixture.Blueprint == "r_KingdomOven" ? 0 : 1;
			if (kind == KingdomPhysicalHappeningKind.Funeral)
			{
				if (fixture.Blueprint == "r_KingdomShrine") return 0;
				if (fixture.Blueprint == "r_KingdomShrineGarth") return 1;
				if (fixture.Blueprint == "r_KingdomTemple") return 2;
			}
			if (kind == KingdomPhysicalHappeningKind.Wedding)
				return fixture.Blueprint == "r_KingdomBench" ? 0 : 1;
			return 0;
		}

		private static List<Cell> OpenCells(Zone zone, GameObject fixtureObject,
			KingdomPhysicalHappeningKind kind)
		{
			List<Cell> result = new List<Cell>();
			Cell fixture = fixtureObject.CurrentCell;
			if (kind == KingdomPhysicalHappeningKind.Wedding
				&& fixtureObject.GetPart<Chair>() != null
				&& ActivityCell(fixture, fixtureObject)) result.Add(fixture);
			for (int radius = 1; radius <= 4
				&& result.Count < KingdomHappeningLifecycleRules.MaxParticipants * 3; radius++)
			{
				for (int y = fixture.Y - radius; y <= fixture.Y + radius; y++)
				for (int x = fixture.X - radius; x <= fixture.X + radius; x++)
				{
					if (Math.Max(Math.Abs(x - fixture.X), Math.Abs(y - fixture.Y)) != radius)
						continue;
					Cell cell = zone.GetCell(x, y);
					if (ActivityCell(cell, fixtureObject)) result.Add(cell);
				}
			}
			return result;
		}

		private static bool ActivityCell(Cell cell, GameObject fixture)
		{
			if (cell == null || !cell.IsPassable() || !cell.IsEmptyOfSolid()) return false;
			for (int i = 0; i < cell.Objects.Count; i++)
			{
				GameObject item = cell.Objects[i];
				if (ReferenceEquals(item, fixture)) continue;
				if (item.IsCreature || (item.Physics != null && item.Physics.Solid)) return false;
			}
			return true;
		}

		private static bool CanWalk(GameObject body, Cell target)
		{
			if (!GameObject.Validate(body) || body.CurrentCell == null || target == null
				|| body.CurrentZone != target.ParentZone) return false;
			if (body.CurrentCell == target) return true;
			FindPath path = new FindPath(body.CurrentZone.ZoneID, body.CurrentCell.X,
				body.CurrentCell.Y, target.ParentZone.ZoneID, target.X, target.Y,
				PathGlobal: false, PathUnlimited: false, Looker: body, Juggernaut: false,
				IgnoreCreatures: false, IgnoreGases: false, FlexPhase: false, MaxWeight: 95);
			return path.Usable && path.Directions.Count <= MaxPathSteps;
		}

		private static bool ExactBodyReceipt(GameObject body,
			KingdomHappeningOperation operation, KingdomHappeningParticipant row)
		{
			return body.GetStringProperty(TokenProperty) == operation.EventId
				&& body.GetStringProperty(PostReceiptProperty) == PostReceipt(row)
				&& body.GetStringProperty(AnchorReceiptProperty) == row.Anchor
				&& body.GetStringProperty(HomeReceiptProperty) == row.Home
				&& body.GetStringProperty(TargetReceiptProperty) == row.TargetX.ToString(
					CultureInfo.InvariantCulture) + "," + row.TargetY.ToString(
					CultureInfo.InvariantCulture)
				&& body.GetStringProperty(FixtureReceiptProperty) == operation.FixtureObjectId
				&& body.GetStringProperty(OriginalReceiptProperty) == row.OriginalX.ToString(
					CultureInfo.InvariantCulture) + "," + row.OriginalY.ToString(
					CultureInfo.InvariantCulture)
				&& body.GetIntProperty(WandersReceiptProperty) == (row.Wanders ? 1 : 0)
				&& body.GetIntProperty(RandomReceiptProperty) == (row.WandersRandomly ? 1 : 0)
				&& body.GetIntProperty(StayingReceiptProperty) == (row.Staying ? 1 : 0);
		}

		private static bool MarkRestored(KingdomCityBook book, string eventId,
			int participantIndex, bool fixture, long nowTick)
		{
			return TryReadRaw(book, out KingdomHappeningLifecycleBook lifecycle)
				&& KingdomHappeningLifecycleRules.TryMarkRestored(lifecycle, eventId,
					participantIndex, fixture, nowTick,
					out KingdomHappeningLifecycleBook changed,
					out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private static bool BodyScheduleRestored(GameObject body,
			KingdomHappeningParticipant row)
		{
			if (!GameObject.Validate(body) || body.Brain == null || IsStaged(body)
				|| PostReceipt(body) != PostReceipt(row)
				|| body.Brain.Wanders != row.Wanders
				|| body.Brain.WandersRandomly != row.WandersRandomly
				|| body.Brain.Staying != row.Staying) return false;
			string anchor = body.Brain.StartingCell == null ? ""
				: body.Brain.StartingCell.ToString();
			return anchor == row.Anchor;
		}

		private static void ClearBodyProjection(GameObject body)
		{
			body.RemoveStringProperty(TokenProperty);
			body.RemoveStringProperty(PostReceiptProperty);
			body.RemoveStringProperty(AnchorReceiptProperty);
			body.RemoveStringProperty(HomeReceiptProperty);
			body.RemoveStringProperty(TargetReceiptProperty);
			body.RemoveStringProperty(FixtureReceiptProperty);
			body.RemoveStringProperty(OriginalReceiptProperty);
			body.RemoveIntProperty(WandersReceiptProperty);
			body.RemoveIntProperty(RandomReceiptProperty);
			body.RemoveIntProperty(StayingReceiptProperty);
		}

		private static bool StandFromWeddingFixture(GameObject body,
			KingdomHappeningOperation operation)
		{
			Sitting sitting = body.GetEffect<Sitting>();
			if (sitting == null) return true;
			GameObject fixture = sitting.SittingOn;
			if (!GameObject.Validate(fixture)
				|| fixture.IDIfAssigned != operation.FixtureObjectId)
				return body.RemoveEffect(sitting);
			Chair chair = fixture.GetPart<Chair>();
			return chair != null && chair.StandUp(body, S: sitting);
		}

		private static bool HasOwnedGoal(GameObject body, string eventId)
		{
			if (body?.Brain?.Goals?.Items == null) return false;
			for (int i = 0; i < body.Brain.Goals.Items.Count; i++)
				if (body.Brain.Goals.Items[i] is KingdomHappeningMoveTo move
					&& move.HappeningEventId == eventId) return true;
			return false;
		}

		private static bool ParticipantGone(KingdomSystem system, KingdomCityBook book,
			KingdomHappeningParticipant row)
		{
			if (system?.Bindings == null || !system.Bindings.TryRead(
				out KingdomBindingTable bindings, out KingdomCityFault bindingFault)
				|| bindings.TryGet(row.ResidentId, KingdomBindingKind.Resident,
					out KingdomBinding ignoredBinding)
				|| book == null || !book.TryRead(out KingdomCityState state,
					out KingdomCityFault cityFault)) return false;
			if (!state.TryResidentIndex(row.ResidentId, out int index)) return true;
			return state.TryResident(index, out KingdomResidentRow resident)
				&& resident.Standing == KingdomResidentStanding.Dead;
		}

		private static void ReconcileZoneProjections(Zone zone, string settlementId,
			string activeEventId)
		{
			if (zone == null || string.IsNullOrEmpty(settlementId)) return;
			string prefix = "taf:happening:" + settlementId + ":";
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				string token = item.GetStringProperty(TokenProperty);
				if (!string.IsNullOrEmpty(token) && token != activeEventId
					&& token.StartsWith(prefix, StringComparison.Ordinal))
					RestoreStaleBodyProjection(item, token);
				string fixture = item.GetStringProperty(FixtureTokenProperty);
				if (!string.IsNullOrEmpty(fixture) && fixture != activeEventId
					&& fixture.StartsWith(prefix, StringComparison.Ordinal))
				{
					item.RemoveStringProperty(FixtureTokenProperty);
					string use = item.GetStringProperty(FixtureUseProperty);
					if (use == fixture || use == fixture + ":attempt")
						item.RemoveStringProperty(FixtureUseProperty);
				}
			}
		}

		private static void RestoreStaleBodyProjection(GameObject body, string token)
		{
			if (!GameObject.Validate(body) || body.Brain == null) return;
			RemoveOwnedGoal(body, token);
			RemoveOwnedGoal(body, token + ":restore");
			Sitting sitting = body.GetEffect<Sitting>();
			if (sitting != null && GameObject.Validate(sitting.SittingOn)
				&& sitting.SittingOn.GetStringProperty(FixtureTokenProperty) == token)
			{
				Chair chair = sitting.SittingOn.GetPart<Chair>();
				if (chair != null) chair.StandUp(body, S: sitting);
			}
			string post = body.GetStringProperty(PostReceiptProperty);
			int slash = string.IsNullOrEmpty(post) ? -1 : post.IndexOf('/');
			if (slash > 0
				&& int.TryParse(post.Substring(0, slash), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out int workId)
				&& int.TryParse(post.Substring(slash + 1), NumberStyles.Integer,
					CultureInfo.InvariantCulture, out int kind)
				&& workId >= 0 && kind >= byte.MinValue && kind <= byte.MaxValue
				&& Enum.IsDefined(typeof(KingdomWorkKind), (KingdomWorkKind)kind))
				KingdomStations.Post(body, workId, (KingdomWorkKind)kind);
			body.Brain.Wanders = body.GetIntProperty(WandersReceiptProperty) == 1;
			body.Brain.WandersRandomly = body.GetIntProperty(RandomReceiptProperty) == 1;
			body.Brain.Staying = body.GetIntProperty(StayingReceiptProperty) == 1;
			string anchor = body.GetStringProperty(AnchorReceiptProperty) ?? "";
			try
			{
				body.Brain.StartingCell = string.IsNullOrEmpty(anchor)
					? null : new GlobalLocation(anchor);
			}
			catch { }
			ClearBodyProjection(body);
		}

		private static void RemoveOwnedGoal(GameObject body, string eventId)
		{
			if (body?.Brain?.Goals?.Items == null) return;
			for (int i = body.Brain.Goals.Items.Count - 1; i >= 0; i--)
			{
				if (!(body.Brain.Goals.Items[i] is KingdomHappeningMoveTo move)
					|| move.HappeningEventId != eventId) continue;
				for (int j = body.Brain.Goals.Items.Count - 1; j >= i; j--)
					body.Brain.Goals.Items.RemoveAt(j);
				return;
			}
		}

		private static string PostReceipt(GameObject body)
		{
			return KingdomStations.PostOf(body).ToString(CultureInfo.InvariantCulture) + "/"
				+ body.GetIntProperty(KingdomStations.PostKindProperty).ToString(
					CultureInfo.InvariantCulture);
		}

		private static string PostReceipt(KingdomHappeningParticipant row)
		{
			return row.PostWorkId.ToString(CultureInfo.InvariantCulture) + "/"
				+ row.PostKind.ToString(CultureInfo.InvariantCulture);
		}

		private static string NameOf(GameObject body)
		{
			return body.GetStringProperty("KingdomName") ?? body.BaseDisplayNameStripped ?? "";
		}

		private static string[] Names(KingdomHappeningOperation operation)
		{
			if (operation == null) return new string[0];
			string[] names = new string[operation.Participants.Length];
			for (int i = 0; i < names.Length; i++) names[i] = operation.Participants[i].Name;
			return names;
		}

		private static GameObject FindById(Zone zone, string objectId)
		{
			if (zone == null || string.IsNullOrEmpty(objectId)) return null;
			GameObject found = GameObject.FindByID(objectId);
			return GameObject.Validate(found) && found.CurrentCell != null
				&& ReferenceEquals(found.CurrentZone, zone)
				&& string.Equals(found.IDIfAssigned, objectId, StringComparison.Ordinal)
				? found : null;
		}

		private static bool TryFindById(Zone zone, string objectId, out GameObject found,
			out bool absent)
		{
			found = null;
			absent = false;
			if (zone == null || string.IsNullOrEmpty(objectId)) return false;
			GameObject exact = GameObject.FindByID(objectId);
			if (!GameObject.Validate(exact))
			{
				absent = true;
				return true;
			}
			// An exact fixture id resolving elsewhere is conflicting evidence, not absence. Keep the
			// durable restoration receipt open rather than clearing another zone's authority.
			if (exact.CurrentCell == null || !ReferenceEquals(exact.CurrentZone, zone)
				|| !string.Equals(exact.IDIfAssigned, objectId, StringComparison.Ordinal)) return false;
			found = exact;
			return true;
		}

		private static Zone ExactLoadedZone(string zoneId)
		{
			Zone zone = null;
			if (string.IsNullOrEmpty(zoneId) || The.ZoneManager?.CachedZones == null
				|| !The.ZoneManager.CachedZones.TryGetValue(zoneId, out zone)) return null;
			return zone;
		}

		private static bool StandsIn(string zoneId)
		{
			return The.Player?.CurrentZone != null
				&& string.Equals(The.Player.CurrentZone.ZoneID, zoneId, StringComparison.Ordinal);
		}

		private static bool OwnedGround(KingdomSystem system, string zoneId)
		{
			return !string.IsNullOrEmpty(zoneId) && ((system.ClaimedZones != null
				&& system.ClaimedZones.Contains(zoneId)) || (system.Away?.ClaimedZones != null
				&& system.Away.ClaimedZones.Contains(zoneId)));
		}

		private static bool TryRead(KingdomCityBook book, long nowTick,
			out KingdomHappeningLifecycleBook lifecycle)
		{
			if (!TryReadRaw(book, out lifecycle)) return false;
			KingdomHappeningLifecycleBook recovered =
				KingdomHappeningLifecycleRules.RecoverInterruptedSinks(lifecycle, nowTick);
			if (!ReferenceEquals(recovered, lifecycle) && !Write(book, recovered)) return false;
			lifecycle = recovered;
			return true;
		}

		private static bool TryReadRaw(KingdomCityBook book,
			out KingdomHappeningLifecycleBook lifecycle)
		{
			lifecycle = null;
			KingdomHappeningLifecycleFault fault = KingdomHappeningLifecycleFault.Malformed;
			if (book == null || !KingdomHappeningLifecycleRules.TryDecode(
				book.HappeningModel, out lifecycle, out fault))
			{
				KingdomLog.Log("happening physical: sidecar refused (" + fault + ")");
				return false;
			}
			return true;
		}

		private static bool Write(KingdomCityBook book, KingdomHappeningLifecycleBook lifecycle)
		{
			if (book == null || !KingdomHappeningLifecycleRules.TryEncode(lifecycle,
				out string wire)) return false;
			book.HappeningModel = wire;
			return true;
		}

		private static bool SetPhase(KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, KingdomHappeningLifecyclePhase expected,
			KingdomHappeningLifecyclePhase phase, bool attended, long holdUntil, long nowTick)
		{
			return lifecycle.Active != null && KingdomHappeningLifecycleRules.TrySetPhase(
				lifecycle, lifecycle.Active.EventId, expected, phase, attended, holdUntil,
				nowTick, out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private static bool Clear(KingdomCityBook book,
			KingdomHappeningLifecycleBook lifecycle, string eventId)
		{
			return KingdomHappeningLifecycleRules.TryClear(lifecycle, eventId,
				out KingdomHappeningLifecycleBook changed,
				out KingdomHappeningLifecycleFault fault) && Write(book, changed);
		}

		private enum SinkLane : byte
		{
			Chronicle,
			Told,
			Effect,
			Ledger,
			Message
		}

		private sealed class Evidence
		{
			internal readonly Zone Zone;
			internal readonly GameObject Fixture;
			internal readonly List<GameObject> Bodies;
			internal readonly bool FixtureExact;
			internal readonly bool ParticipantsExact;
			internal readonly bool AllArrived;
			internal readonly bool UseReceiptExact;

			internal Evidence(Zone zone, GameObject fixture, List<GameObject> bodies,
				bool fixtureExact, bool participantsExact, bool allArrived, bool useReceiptExact)
			{
				Zone = zone;
				Fixture = fixture;
				Bodies = bodies;
				FixtureExact = fixtureExact;
				ParticipantsExact = participantsExact;
				AllArrived = allArrived;
				UseReceiptExact = useReceiptExact;
			}
		}
	}
}
