using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Only production authority for petition publication, recovery, and transitions.</summary>
	internal static class KingdomPetitionLifecycle
	{
		internal static bool OnSettlementPass(KingdomSystem system, Zone zone,
			KingdomSurvey survey, bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !SeatGround(system, zone, survey) || now < 0L) return false;
			if (!AdoptLegacy(system, zone, survey, book, now)) return false;
			if (!Drive(system, book, now)) return false;
			KingdomLifecycleOptionState prior = book.PetitionOption;
			if (!ObserveOption(book, enabled, now)) return false;
			if (!enabled)
			{
				if (!ReconcileDisabled(system, book, now)) return false;
				Project(system, book.Petition);
				return true;
			}
			if (prior == KingdomLifecycleOptionState.Disabled
				&& !ResumeAccepted(system, book, now)) return false;
			if (!Drive(system, book, now)) return false;
			Check(system, zone, survey, now);
			if (KingdomPetitionRules.IsActive(Status(system))) return true;
			return Issue(system, zone, survey, enabled, now) || Status(system) != PetitionLifecycle.None;
		}

		internal static bool Issue(KingdomSystem system, Zone zone, KingdomSurvey survey,
			bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !enabled || !SeatGround(system, zone, survey) || now < 0L)
				return false;
			if (!AdoptLegacy(system, zone, survey, book, now) || !Drive(system, book, now)
				|| !ObserveOption(book, true, now) || !CanStart(system, book, now)) return false;
			string faction = null;
			int worstStanding = 0;
			if (system.Standings != null)
				foreach (KeyValuePair<string, int> row in system.Standings)
					if (row.Value < worstStanding)
					{
						worstStanding = row.Value;
						faction = row.Key;
					}
			KingdomRules.PetitionKind kind = KingdomRules.ChoosePetition(survey.StoredWater,
				system.Population, survey.Beds, system.IdleWorks, worstStanding,
				HasShrine(survey), system.Dead);
			return kind != KingdomRules.PetitionKind.None
				&& PublishOffer(system, zone, survey, book, kind, faction, null, now, false);
		}

		internal static bool Raise(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomRules.PetitionKind kind, string faction, string eventId, bool enabled, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !enabled
				|| !Enum.IsDefined(typeof(KingdomRules.PetitionKind), kind)
				|| kind == KingdomRules.PetitionKind.None
				|| !SeatGround(system, zone, survey) || now < 0L
				|| (eventId != null && !KingdomPetitionRules.EventIdValid(eventId))
				|| !KingdomPetitionRules.SnapshotTextValid(faction,
					KingdomLifecycleRules.MaxNameChars, true)) return false;
			if (!AdoptLegacy(system, zone, survey, book, now) || !Drive(system, book, now)
				|| !ObserveOption(book, true, now)) return false;
			KingdomLifecycleOperation current = book.Petition;
			if (eventId != null && KingdomPetitionRules.FrozenSnapshotValid(current)
				&& string.Equals(current.ObjectMarker, eventId, StringComparison.Ordinal))
				return (KingdomRules.PetitionKind)current.Kind == kind
					&& string.Equals(current.Faction, faction, StringComparison.Ordinal)
					&& KingdomPetitionRules.IsActive(KingdomPetitionRules.LifecycleOf(current));
			return CanStart(system, book, now)
				&& PublishOffer(system, zone, survey, book, kind, faction, eventId, now, false);
		}

		internal static bool Accept(KingdomSystem system, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !Drive(system, book, now)) return false;
			KingdomLifecycleOperation source = book.Petition;
			if (!KingdomPetitionRules.FrozenSnapshotValid(source)
				|| KingdomPetitionRules.LifecycleOf(source) != PetitionLifecycle.Offered) return false;
			bool result = PublishTransition(system, book, source,
				KingdomLifecycleAction.PetitionAccept, KingdomPetitionRules.ActiveClock,
				source.DepartTick, now, "accepted");
			if (result) KingdomGovernanceScope.Commit("accept petition");
			return result;
		}

		internal static bool Decline(KingdomSystem system, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !Drive(system, book, now)) return false;
			KingdomLifecycleOperation source = book.Petition;
			return KingdomPetitionRules.LifecycleOf(source) == PetitionLifecycle.Offered
				&& PublishTransition(system, book, source,
					KingdomLifecycleAction.PetitionDecline, KingdomPetitionRules.ActiveClock,
					source.DepartTick, now, "declined");
		}

		internal static void Check(KingdomSystem system, Zone zone, KingdomSurvey survey, long now)
		{
			KingdomLifecycleBook book = Authority(system);
			if (book == null || !SeatGround(system, zone, survey) || !Drive(system, book, now)) return;
			KingdomLifecycleOperation op = book.Petition;
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)
				|| !KingdomPetitionRules.OriginMatches(op.Origin, book.SettlementId)
				|| string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return;
			PetitionLifecycle state = KingdomPetitionRules.LifecycleOf(op);
			if (state == PetitionLifecycle.Accepted)
			{
				int standing = string.IsNullOrEmpty(op.Faction) ? 0 : system.GetStanding(op.Faction);
				if (KingdomPetitionRules.CanResolve(state, (KingdomRules.PetitionKind)op.Kind,
					op.Target, survey.StoredWater, survey.Beds, system.IdleWorks, standing,
					HasShrine(survey)))
				{
					PublishTransition(system, book, op, KingdomLifecycleAction.PetitionResolve,
						KingdomPetitionRules.ActiveClock, op.DepartTick, now, "resolved");
					return;
				}
			}
			if ((state == PetitionLifecycle.Offered || state == PetitionLifecycle.Accepted)
				&& KingdomPetitionRules.IsExpired(now, op.DepartTick))
				PublishTransition(system, book, op, KingdomLifecycleAction.PetitionExpire,
					KingdomPetitionRules.ActiveClock, op.DepartTick, now, "expired");
		}

		internal static PetitionLifecycle Status(KingdomSystem system)
		{
			KingdomLifecycleOperation op = Open(system);
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)) return PetitionLifecycle.None;
			Project(system, op);
			return KingdomPetitionRules.LifecycleOf(op);
		}

		internal static KingdomLifecycleOperation Open(KingdomSystem system)
		{
			KingdomLifecycleBook book = Authority(system);
			return book?.Petition;
		}

		private static bool PublishOffer(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomLifecycleBook book, KingdomRules.PetitionKind kind, string faction,
			string eventId, long now, bool legacy)
		{
			KingdomLifecycleOperation prior = book.Petition;
			if (prior != null && (!KingdomPetitionRules.FrozenSnapshotValid(prior)
				|| !KingdomPetitionRules.CanFollow(prior.Action,
					KingdomLifecycleAction.PetitionOffer))) return false;
			if (!TryRequester(system, survey, null, out GameObject body, out string name))
				return false;
			int target = KingdomPetitionRules.SnapshotTarget(kind, system.Population);
			if (!KingdomPetitionRules.TargetValid(kind, target)
				|| (eventId != null && !KingdomPetitionRules.EventIdValid(eventId))
				|| !KingdomPetitionRules.SnapshotTextValid(faction,
					KingdomLifecycleRules.MaxNameChars, true)) return false;
			if (!KingdomPetitionRules.TryDeadline(now, KingdomRules.PetitionLifetimeTicks,
				out long deadline)) return false;
			if (prior != null && !KingdomLifecycleRules.Retire(book, prior, now)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, KingdomLifecycleAction.PetitionOffer, now);
			if (op == null) return QuarantineAfterRetirement(book, prior,
				"petition offer could not reserve its lane");
			FreezeOffer(op, body, name, book.SettlementId, zone.ZoneID, kind, faction,
				target, eventId, now, deadline);
			op.Outbox = Outbox(system, op, legacy ? "adopted" : "offered");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool PublishTransition(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation source, KingdomLifecycleAction action, string clock,
			long deadline, long now, string reason)
		{
			if (!KingdomPetitionRules.FrozenSnapshotValid(source)
				|| source.Phase != KingdomLifecyclePhase.Terminal
				|| !KingdomPetitionRules.CanFollow(source.Action, action)
				|| deadline <= 0L || now < source.UpdatedTick) return false;
			if (!KingdomLifecycleRules.Retire(book, source, now)) return false;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, action, now);
			if (op == null)
				return QuarantineAfterRetirement(book, source,
					"petition transition could not reserve its lane");
			CopySnapshot(source, op);
			op.Creed = clock;
			op.DepartTick = deadline;
			if (action == KingdomLifecycleAction.PetitionResolve) op.Count = system.PetitionsMet;
			op.Outbox = Outbox(system, op, reason);
			if (!KingdomPetitionRules.SameFrozenSnapshot(source, op))
				return QuarantineAfterRetirement(book, source,
					"petition transition changed frozen offer semantics");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool PublishAndDrive(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long now)
		{
			if (op.Outbox == null || !KingdomLifecycleRules.PetitionRuntimeAdapter.PrepareLeases(
				book, op) || !KingdomLifecycleRules.TryPublish(book, op))
			{
				book.Quarantined = true;
				book.Fault = "petition plan publication failed without clearing its legacy projection";
				return false;
			}
			Project(system, op);
			if (KingdomLog.Enabled)
				KingdomLog.Log("petition action: " + op.Action + " id=" + op.ObjectMarker
					+ " operation=" + op.Id);
			return Drive(system, book, now);
		}

		private static bool Drive(KingdomSystem system, KingdomLifecycleBook book, long now)
		{
			KingdomLifecycleOperation op = book?.Petition;
			if (op == null) return true;
			if (!KingdomPetitionRules.FrozenSnapshotValid(op)) return false;
			for (int guard = 0; guard < 12; guard++)
			{
				long tick = Math.Max(now, op.UpdatedTick);
				switch (op.Phase)
				{
				case KingdomLifecyclePhase.Prepared:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.DomainIntent, tick)) return false;
					break;
				case KingdomLifecyclePhase.DomainIntent:
					if (!SettleDomain(system, book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.DomainSettled, tick)) return false;
					break;
				case KingdomLifecyclePhase.DomainSettled:
					if (!KingdomLifecycleRules.AdvancePhase(book, op,
						KingdomLifecyclePhase.Sinks, tick)) return false;
					break;
				case KingdomLifecyclePhase.Sinks:
					if (!DispatchOutbox(system, book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.ScheduleIntent, tick)) return false;
					break;
				case KingdomLifecyclePhase.ScheduleIntent:
					if (!KingdomLifecycleRules.PetitionRuntimeAdapter.ProveSchedule(book, op)
						|| !KingdomLifecycleRules.AdvancePhase(book, op,
							KingdomLifecyclePhase.Terminal, tick)) return false;
					break;
				case KingdomLifecyclePhase.Terminal:
					Project(system, op);
					return true;
				case KingdomLifecyclePhase.Quarantined:
					return false;
				default:
					KingdomLifecycleRules.Quarantine(op,
						"petition entered a phase outside its bounded action graph");
					return false;
				}
			}
			KingdomLifecycleRules.Quarantine(op, "petition exceeded its bounded phase budget");
			return false;
		}

		private static bool SettleDomain(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (op.Action != KingdomLifecycleAction.PetitionResolve)
				return KingdomLifecycleRules.PetitionRuntimeAdapter.ProveDomain(book, op);
			KingdomLifecycleLeaseState state =
				KingdomLifecycleRules.PetitionRuntimeAdapter.DomainState(book, op);
			if (state == KingdomLifecycleLeaseState.Proved)
				return system.PetitionsMet == op.Count + 1;
			if (op.Count < 0 || op.Count == int.MaxValue
				|| (system.PetitionsMet != op.Count && system.PetitionsMet != op.Count + 1))
			{
				KingdomLifecycleRules.Quarantine(op,
					"petition completion count disagreed with its exact intent");
				return false;
			}
			if (state == KingdomLifecycleLeaseState.Prepared
				&& !KingdomLifecycleRules.PetitionRuntimeAdapter.BeginDomain(book, op)) return false;
			if (system.PetitionsMet == op.Count) system.PetitionsMet++;
			return KingdomLifecycleRules.PetitionRuntimeAdapter.CommitDomain(book, op);
		}

		private static bool DispatchOutbox(KingdomSystem system, KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (!KingdomLifecycleRules.RecoverOutbox(book, op)) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Chronicle, delegate
			{
				return KingdomChronicle.RecordOnce(system, op.Outbox.ChronicleReceiptId,
					op.Outbox.Chronicle, op.Outbox.ChronicleAccomplishment);
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Ledger, delegate
			{
				system.Ledger.Note(op.Outbox.Ledger); return true;
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Message, delegate
			{
				MessageQueue.AddPlayerMessage(op.Outbox.Message); return true;
			})) return false;
			if (!Deliver(book, op, KingdomLifecycleSinkMask.Deed, delegate
			{
				system.RecordDeed(op.Outbox.Deed); return true;
			})) return false;
			return Settled(op.Outbox);
		}

		private static bool Deliver(KingdomLifecycleBook book, KingdomLifecycleOperation op,
			KingdomLifecycleSinkMask sink, Func<bool> callback)
		{
			KingdomLifecycleSinkState state = SinkState(op.Outbox, sink);
			if (KingdomLifecycleRules.SinkSettled(state)) return true;
			if (!KingdomLifecycleRules.PetitionRuntimeAdapter.BeginSink(book, op, sink)) return false;
			bool delivered = false;
			try { delivered = callback(); }
			catch (Exception error)
			{
				MetricsManager.LogError("ThousandAndFirst petition outbox", error);
			}
			return delivered && KingdomLifecycleRules.PetitionRuntimeAdapter.CommitSink(book, op, sink);
		}

		private static bool ReconcileDisabled(KingdomSystem system, KingdomLifecycleBook book,
			long now)
		{
			KingdomLifecycleOperation op = book.Petition;
			PetitionLifecycle state = KingdomPetitionRules.LifecycleOf(op);
			if (state == PetitionLifecycle.Offered)
				return PublishTransition(system, book, op, KingdomLifecycleAction.PetitionExpire,
					KingdomPetitionRules.OptionClosedClock, op.DepartTick, now, "option-closed");
			if (state != PetitionLifecycle.Accepted
				|| string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return true;
			long remaining = KingdomPetitionRules.PauseRemaining(now, op.DepartTick);
			return remaining > 0L && PublishTransition(system, book, op,
				KingdomLifecycleAction.PetitionAccept, KingdomPetitionRules.PausedClock,
				remaining, now, "paused");
		}

		private static bool ResumeAccepted(KingdomSystem system, KingdomLifecycleBook book,
			long now)
		{
			KingdomLifecycleOperation op = book.Petition;
			if (KingdomPetitionRules.LifecycleOf(op) != PetitionLifecycle.Accepted
				|| !string.Equals(op.Creed, KingdomPetitionRules.PausedClock,
					StringComparison.Ordinal)) return true;
			if (!KingdomPetitionRules.TryResumeDeadline(now, op.DepartTick, out long deadline))
				return false;
			return PublishTransition(system, book, op, KingdomLifecycleAction.PetitionAccept,
				KingdomPetitionRules.ActiveClock, deadline, now, "resumed");
		}

		private static bool ObserveOption(KingdomLifecycleBook book, bool enabled, long now)
		{
			KingdomLifecycleOptionDecision decision = KingdomLifecycleRules.ObserveOption(
				book.PetitionOption, book.PetitionOptionTick, enabled, now, book.Petition != null);
			if (!decision.Valid)
			{
				book.Quarantined = true;
				book.Fault = "petition option evidence moved backwards or was malformed";
				return false;
			}
			book.PetitionOption = decision.State;
			book.PetitionOptionTick = decision.Tick;
			return true;
		}

		private static bool CanStart(KingdomSystem system, KingdomLifecycleBook book, long now)
		{
			if (book.PetitionOption != KingdomLifecycleOptionState.Enabled) return false;
			KingdomLifecycleOperation op = book.Petition;
			if (op != null && (!KingdomPetitionRules.FrozenSnapshotValid(op)
				|| !KingdomPetitionRules.IsTerminal(KingdomPetitionRules.LifecycleOf(op)))) return false;
			long last = 0L;
			if (op != null && !KingdomPetitionRules.TryIssuedTick(op, out last)) return false;
			int percent = KingdomRules.DistrictsPetitionIntervalPercent(
				system.ZoneDistricts == null ? null : system.ZoneDistricts.Values);
			long interval = KingdomPetitionRules.ScaledInterval(
				KingdomRules.PetitionCooldownTicks, percent);
			return KingdomPetitionRules.CanOfferAt(now, last, book.PetitionOptionTick, interval);
		}

		private static bool AdoptLegacy(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomLifecycleBook book, long now)
		{
			if (book.Petition != null) return true;
			PetitionLifecycle state = KingdomPetitionRules.NormalizeLegacy(system.PetitionState,
				system.PetitionKind);
			if (!KingdomPetitionRules.IsActive(state)) return true;
			if (!LegacyShape(system, book)
				|| !TryRequester(system, survey, system.PetitionPetitioner,
					out GameObject body, out string name)
				|| !KingdomPetitionRules.TryDeadline(system.PetitionIssuedTick,
					KingdomRules.PetitionLifetimeTicks, out long deadline))
			{
				book.Quarantined = true;
				book.Fault = "malformed legacy petition evidence was retained without reinterpretation";
				return false;
			}
			KingdomLifecycleAction action = state == PetitionLifecycle.Accepted
				? KingdomLifecycleAction.PetitionAccept : KingdomLifecycleAction.PetitionOffer;
			KingdomLifecycleOperation op = KingdomLifecycleRules.PrepareOperation(book,
				KingdomLifecycleLane.Petition, action, now);
			if (op == null) return false;
			FreezeOffer(op, body, name, book.SettlementId, zone.ZoneID, system.PetitionKind,
				system.PetitionFaction, system.PetitionTarget, system.PetitionEventId,
				system.PetitionIssuedTick, deadline);
			op.Detail = system.PetitionCauseSnapshot;
			op.Outbox = Outbox(system, op, state == PetitionLifecycle.Accepted
				? "legacy-accepted" : "legacy-offered");
			return PublishAndDrive(system, book, op, now);
		}

		private static bool LegacyShape(KingdomSystem system, KingdomLifecycleBook book)
		{
			return system != null && book != null
				&& Enum.IsDefined(typeof(KingdomRules.PetitionKind), system.PetitionKind)
				&& system.PetitionKind != KingdomRules.PetitionKind.None
				&& !string.IsNullOrEmpty(system.PetitionPetitioner)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionPetitioner,
					KingdomLifecycleRules.MaxNameChars, false)
				&& !string.IsNullOrEmpty(system.PetitionOriginSettlementId)
				&& string.Equals(system.PetitionOriginSettlementId, book.SettlementId,
					StringComparison.Ordinal)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionCauseSnapshot,
					KingdomLifecycleRules.MaxTextChars, false)
				&& KingdomPetitionRules.EventIdValid(system.PetitionEventId)
				&& KingdomPetitionRules.SnapshotTextValid(system.PetitionFaction,
					KingdomLifecycleRules.MaxNameChars, true)
				&& system.PetitionIssuedTick >= 0L
				&& KingdomPetitionRules.TargetValid(system.PetitionKind,
					system.PetitionTarget);
		}

		private static bool TryRequester(KingdomSystem system, KingdomSurvey survey,
			string exactName, out GameObject body, out string name)
		{
			body = null;
			name = null;
			if (system == null || survey?.Settlers == null) return false;
			for (int i = 0; i < survey.Settlers.Count; i++)
			{
				GameObject candidate = survey.Settlers[i];
				if (!GameObject.Validate(candidate) || candidate.CurrentZone != survey.Ground
					|| candidate.GetIntProperty("KingdomCitizen") != 1
					|| candidate.IsPlayer() || candidate.IsPlayerLed()
					|| string.IsNullOrEmpty(candidate.ID) || string.IsNullOrEmpty(candidate.Blueprint))
					continue;
				string semantic = candidate.GetStringProperty("KingdomName");
				if (string.IsNullOrEmpty(semantic)) semantic = candidate.BaseDisplayNameStripped;
				if (!KingdomPetitionRules.SnapshotTextValid(candidate.ID,
						KingdomLifecycleRules.MaxIdChars, false)
					|| !KingdomPetitionRules.SnapshotTextValid(candidate.Blueprint,
						KingdomLifecycleRules.MaxNameChars, false)
					|| !KingdomPetitionRules.SnapshotTextValid(semantic,
						KingdomLifecycleRules.MaxNameChars, false)
					|| (exactName != null && !string.Equals(semantic, exactName,
						StringComparison.Ordinal))) continue;
				if (body != null && exactName != null) return false;
				if (body == null || string.CompareOrdinal(candidate.ID, body.ID) < 0)
				{
					body = candidate;
					name = semantic;
				}
			}
			return body != null;
		}

		private static void FreezeOffer(KingdomLifecycleOperation op, GameObject body,
			string name, string settlementId, string zoneId, KingdomRules.PetitionKind kind,
			string faction, int target, string eventId, long issuedTick, long deadline)
		{
			op.ZoneId = zoneId;
			op.ObjectId = body.ID;
			op.Blueprint = body.Blueprint;
			op.ObjectName = name;
			op.Origin = settlementId;
			op.Faction = faction;
			op.DisplayFaction = DisplayFaction(faction);
			op.Detail = string.IsNullOrEmpty(op.DisplayFaction)
				? KingdomPetitions.Subject(kind) : op.DisplayFaction;
			op.Kind = (int)kind;
			op.Target = target;
			op.ObjectMarker = string.IsNullOrEmpty(eventId)
				? KingdomLifecycleRules.ChildId(op.Id, "petition-event", 0) : eventId;
			op.ArrivalText = issuedTick.ToString(CultureInfo.InvariantCulture);
			op.DepartTick = deadline;
			op.Creed = KingdomPetitionRules.ActiveClock;
		}

		private static void CopySnapshot(KingdomLifecycleOperation source,
			KingdomLifecycleOperation target)
		{
			target.ZoneId = source.ZoneId;
			target.ObjectId = source.ObjectId;
			target.Blueprint = source.Blueprint;
			target.ObjectName = source.ObjectName;
			target.Origin = source.Origin;
			target.Faction = source.Faction;
			target.DisplayFaction = source.DisplayFaction;
			target.Detail = source.Detail;
			target.Kind = source.Kind;
			target.Target = source.Target;
			target.ObjectMarker = source.ObjectMarker;
			target.ArrivalText = source.ArrivalText;
		}

		private static KingdomLifecycleOutbox Outbox(KingdomSystem system,
			KingdomLifecycleOperation op, string reason)
		{
			string petitioner = KingdomPresentation.Rich(op.ObjectName);
			string subject = KingdomPetitions.Subject((KingdomRules.PetitionKind)op.Kind);
			string chronicle;
			string ledger;
			string message;
			string deed = null;
			switch (reason)
			{
			case "accepted":
				chronicle = "the founder accepted " + petitioner + "'s petition about " + subject;
				ledger = "{{W|The founder accepted " + petitioner + "'s petition.}}";
				message = "{{G|Your word to " + petitioner + " stands.}}";
				break;
			case "declined":
				chronicle = petitioner + " was told the matter must wait";
				ledger = "{{K|" + petitioner + " returned to work. The matter was not pressed.}}";
				message = "{{K|The petition was declined without penalty.}}";
				break;
			case "resolved":
				deed = KingdomPetitions.Deed((KingdomRules.PetitionKind)op.Kind,
					KingdomPresentation.Rich(system.KingdomDisplayName));
				chronicle = petitioner + " asked, and " + deed;
				ledger = "{{G|" + petitioner + " has what they asked for. Word of it will travel.}}";
				message = "{{G|" + petitioner + " thanks you. "
					+ XRL.Language.Grammar.InitCap(deed) + ".}}";
				break;
			case "paused":
				chronicle = petitioner + "'s accepted petition was held while petitions were disabled";
				ledger = "{{K|The accepted petition is paused; your word is not erased.}}";
				message = "{{K|Petitions are disabled. The accepted promise is paused.}}";
				break;
			case "resumed":
				chronicle = petitioner + "'s accepted petition resumed from its saved time";
				ledger = "{{W|The accepted petition resumes with its remaining time.}}";
				message = "{{W|The accepted promise is active again.}}";
				break;
			case "option-closed":
				chronicle = petitioner + " stopped asking when petitions were disabled";
				ledger = "{{K|The unanswered petition closed without penalty.}}";
				message = "{{K|Petitions are disabled. The unanswered request is closed.}}";
				break;
			case "legacy-accepted":
				chronicle = petitioner + "'s older accepted petition was adopted by the petition book";
				ledger = "{{W|An accepted petition from an older save remains in force.}}";
				message = "{{W|An older accepted petition remains in force.}}";
				break;
			case "legacy-offered":
				chronicle = petitioner + "'s older unanswered petition was adopted by the petition book";
				ledger = "{{W|An unanswered petition from an older save still waits.}}";
				message = "{{W|An older petition still waits at the Charter.}}";
				break;
			case "expired":
				chronicle = petitioner + " stopped asking; the matter was not pressed";
				ledger = "{{K|" + petitioner + " stopped asking. The matter was not pressed.}}";
				message = "{{K|A petition expired without penalty.}}";
				break;
			default:
				chronicle = petitioner + " brought a petition about " + subject;
				ledger = "{{W|" + petitioner + " is waiting to speak with you.}}";
				message = "{{W|" + petitioner + " would have a word with you about " + subject + ".}}";
				break;
			}
			KingdomLifecycleOutbox box = KingdomLifecycleRules.PrepareOutbox(op, chronicle,
				ledger, message, deed, null);
			if (box != null && reason == "resolved") box.ChronicleAccomplishment = true;
			return box;
		}

		private static void Project(KingdomSystem system, KingdomLifecycleOperation op)
		{
			if (system == null || !KingdomPetitionRules.FrozenSnapshotValid(op)) return;
			system.PetitionState = KingdomPetitionRules.LifecycleOf(op);
			system.PetitionKind = (KingdomRules.PetitionKind)op.Kind;
			system.PetitionEventId = op.ObjectMarker;
			system.PetitionOriginSettlementId = op.Origin;
			system.PetitionCauseSnapshot = op.Detail;
			system.PetitionPetitioner = op.ObjectName;
			system.PetitionFaction = op.Faction;
			system.PetitionTarget = op.Target;
			if (KingdomPetitionRules.TryIssuedTick(op, out long issued))
			{
				system.PetitionIssuedTick = issued;
				system.LastPetitionMonthOrdinal = KingdomPetitionRules.CanonicalMonthOrdinal(issued);
				system.LastPetitionTick = issued;
			}
		}

		private static KingdomLifecycleBook Authority(KingdomSystem system)
		{
			if (system == null || !system.Founded || system.LifecycleBook == null
				|| system.City == null || string.IsNullOrEmpty(system.CurrentSettlementId)
				|| !string.Equals(system.LifecycleBook.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal)
				|| !string.Equals(system.City.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal)) return null;
			KingdomLifecycleRules.Normalize(system.LifecycleBook);
			if (system.LifecycleBook.Petition != null
				&& !KingdomPetitionRules.FrozenSnapshotValid(system.LifecycleBook.Petition))
			{
				if (system.LifecycleBook.Petition.Phase != KingdomLifecyclePhase.Quarantined)
					KingdomLifecycleRules.Quarantine(system.LifecycleBook.Petition,
						"malformed petition snapshot was retained without reinterpretation");
				system.LifecycleBook.Quarantined = true;
				if (string.IsNullOrEmpty(system.LifecycleBook.Fault))
					system.LifecycleBook.Fault =
						"malformed petition authority was quarantined without clearing evidence";
				return null;
			}
			return KingdomLifecycleRules.CanOwnAuthority(system.LifecycleBook)
				? system.LifecycleBook : null;
		}

		private static bool SeatGround(KingdomSystem system, Zone zone, KingdomSurvey survey)
		{
			return system != null && zone != null && survey != null
				&& ReferenceEquals(survey.Ground, zone) && system.ClaimedZones != null
				&& system.ClaimedZones.Contains(zone.ZoneID)
				&& string.Equals(system.LifecycleBook?.SettlementId,
					system.CurrentSettlementId, StringComparison.Ordinal);
		}

		private static bool HasShrine(KingdomSurvey survey)
		{
			return survey != null && survey.Shrines.Count > 0;
		}

		private static string DisplayFaction(string faction)
		{
			if (string.IsNullOrEmpty(faction)) return null;
			try
			{
				return ConsoleLib.Console.ColorUtility.StripFormatting(
					XRL.World.Faction.GetFormattedName(faction));
			}
			catch { return faction; }
		}

		private static bool QuarantineAfterRetirement(KingdomLifecycleBook book,
			KingdomLifecycleOperation evidence, string reason)
		{
			book.Quarantined = true;
			book.Fault = reason;
			return false;
		}

		private static KingdomLifecycleSinkState SinkState(KingdomLifecycleOutbox box,
			KingdomLifecycleSinkMask sink)
		{
			if (box == null) return KingdomLifecycleSinkState.None;
			switch (sink)
			{
			case KingdomLifecycleSinkMask.Chronicle: return box.ChronicleState;
			case KingdomLifecycleSinkMask.Ledger: return box.LedgerState;
			case KingdomLifecycleSinkMask.Message: return box.MessageState;
			case KingdomLifecycleSinkMask.Deed: return box.DeedState;
			default: return box.GuestbookState;
			}
		}

		private static bool Settled(KingdomLifecycleOutbox box)
		{
			return box != null
				&& KingdomLifecycleRules.SinkSettled(box.ChronicleState)
				&& KingdomLifecycleRules.SinkSettled(box.LedgerState)
				&& KingdomLifecycleRules.SinkSettled(box.MessageState)
				&& KingdomLifecycleRules.SinkSettled(box.DeedState)
				&& KingdomLifecycleRules.SinkSettled(box.GuestbookState);
		}
	}
}
