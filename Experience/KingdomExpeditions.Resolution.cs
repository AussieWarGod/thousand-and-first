using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		private static bool TryResolve(KingdomSystem System, KingdomJobRow Row,
			KingdomExpeditionOutcome Resolution, long DatedTick, bool Award, bool LoadZone,
			KingdomSurvey SourceSurvey, out string Failure)
		{
			Failure = null;
			if (System == null || System.Jobs == null || Row.Kind != KingdomJobKind.Expedition
				|| Row.JobId <= 0 || Row.SubjectId <= 0)
				return Refuse("The expedition row is malformed; no body or goods were guessed.", out Failure);
			if (KingdomExpeditionRules.IsResolutionPrepared(Row.OriginCode))
				return TryResumeTerminalResolution(System, Row, out Failure);
			if (TryInferTerminalResidentEvidence(System, Row,
				out KingdomExpeditionOutcome residentResolution, out string residentZone))
			{
				if (!TryPublishTerminalResolution(System, Row, residentResolution, DatedTick,
					residentZone, out Row, out Failure)) return false;
				return TryResumeTerminalResolution(System, Row, out Failure);
			}
			GameObject body;
			string boundZone;
			BoundBodyState bodyState = FindBoundBody(System, Row, LoadZone,
				out body, out boundZone);
			if (bodyState == BoundBodyState.Unreachable || bodyState == BoundBodyState.Ambiguous)
				return Refuse("The exact bound body cannot be proved yet. The job remains open and no substitute was minted.", out Failure);

			if (bodyState == BoundBodyState.Dead)
				Resolution = KingdomExpeditionOutcome.ResidentDiedOnGround;
			else if (bodyState == BoundBodyState.Missing)
				Resolution = KingdomExpeditionOutcome.ResidentMissingFromBoundGround;
			else if (bodyState == BoundBodyState.Led)
				Resolution = KingdomExpeditionOutcome.ResidentJoinedFounder;

			if (KingdomExpeditionRules.IsTerminalOutcome((int)Resolution))
			{
				if (!TryPublishTerminalResolution(System, Row, Resolution, DatedTick,
					boundZone ?? Row.DestZoneId, out Row, out Failure)) return false;
				return TryResumeTerminalResolution(System, Row, out Failure);
			}

			if (bodyState != BoundBodyState.Alive || !GameObject.Validate(body))
				return Refuse("The exact living body is not available to return. The job remains open.", out Failure);
			if (KingdomPhysicalHappenings.IsStaged(body))
				return Refuse("The exact resident is attending a city occasion; the return waits without moving them.", out Failure);
			if (!KingdomExpeditionRules.IsDispatched(Row.OriginCode)
				&& !TryAdvanceDispatch(System, Row, body, null, null,
					(The.Game == null) ? 0L : The.Game.TimeTicks, LoadZone, SourceSurvey,
					out Row, out Failure))
				return Refuse("The exact dispatch receipt must reconcile before this job can close: "
					+ Failure, out Failure);
			Zone source;
			if (!TryExactZone(Row.SourceZoneId, LoadZone, out source))
				return Refuse("The resident's recorded home ground is unavailable. The job remains open.", out Failure);
			Cell home = SafeCell(source);
			if (home == null || !MoveExact(body, home))
				return Refuse("No safe standing cell could be proved on the resident's home ground. The job remains open.", out Failure);
			if (!KingdomResidents.Bind(System, Row.SubjectId, KingdomBindingKind.Resident,
				Row.SourceZoneId, body, (The.Game == null) ? 0L : The.Game.TimeTicks))
				return Refuse("The exact body reached home, but its binding has not yet caught up. The job remains open for repair.", out Failure);
			body.RemoveIntProperty(ResidentJobProperty);
			body.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
			if (Award && Row.CargoAmount > 0 && !EnsureReward(body, Row))
				return Refuse("The exact body returned, but its frozen salvage could not be placed without duplication. The job remains open.", out Failure);
			if (Award) ConsumeRemainingProvisions(body, Row.JobId);
			if (!TrySetResident(System, Row.SubjectId, KingdomResidentStanding.Resident,
				KingdomStandingCause.None, Row.SourceZoneId))
				return Refuse("The exact body returned, but the resident roll has not yet caught up. The job remains open for repair.", out Failure);
			return TellAndClose(System, Row, Resolution, DatedTick, out Failure);
		}

		/// <summary>Publishes the immutable terminal outcome, date, and last proved ground before
		/// standing, body markers, or binding authority change. That row is the retry receipt when
		/// any later Chronicle, ledger, registry, or job-book publication is interrupted.</summary>
		private static bool TryPublishTerminalResolution(KingdomSystem System,
			KingdomJobRow Requested, KingdomExpeditionOutcome Resolution, long ResolutionTick,
			string ResolutionZoneId, out KingdomJobRow Published, out string Failure)
		{
			Published = Requested;
			Failure = null;
			if (System == null || System.Jobs == null
				|| !KingdomExpeditionRules.IsTerminalOutcome((int)Resolution)
				|| string.IsNullOrEmpty(ResolutionZoneId))
				return Refuse("The terminal expedition receipt is malformed; no resident authority changed.",
					out Failure);
			KingdomJobTable jobs;
			KingdomJobRow current;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out jobs, out fault)
				|| !jobs.TryGet(Requested.JobId, out current))
				return Refuse("The terminal expedition receipt cannot find its open job authority.",
					out Failure);
			if (KingdomExpeditionRules.IsResolutionPrepared(current.OriginCode))
			{
				if (!SameExpeditionIdentity(Requested, current)
					|| current.OutcomeCode != (int)Resolution)
					return Refuse("The open job already carries a different terminal expedition receipt.",
						out Failure);
				Published = current;
				return true;
			}
			if (!SameAuthority(Requested, current))
				return Refuse("The open job no longer matches the terminal expedition authority.",
					out Failure);
			long frozenTick = ResolutionTick;
			if (frozenTick <= current.StartTick)
			{
				if (current.StartTick == long.MaxValue)
					return Refuse("The terminal expedition date cannot be represented safely.", out Failure);
				frozenTick = current.StartTick + 1L;
			}
			KingdomJobRow receipt = current.WithExpeditionResolution((int)Resolution, frozenTick,
				ResolutionZoneId, KingdomExpeditionDeedDisposition.NotApplicable, null, null,
				null);
			KingdomJobTable next;
			if (!jobs.TryReplace(receipt, out next, out fault)
				|| !System.Jobs.TryPublish(next, out fault))
				return Refuse("The terminal expedition receipt could not yet be published; no resident authority changed.",
					out Failure);
			Published = receipt;
			return true;
		}

		/// <summary>Forward-recovers a published no-body result without needing the binding or body
		/// whose removal it authorizes. Every mutation is idempotent; the receipt's stored date and
		/// ground make the Chronicle and ledger telling stable across save/load and retry.</summary>
		private static bool TryResumeTerminalResolution(KingdomSystem System, KingdomJobRow Requested,
			out string Failure)
		{
			Failure = null;
			if (System == null || System.Jobs == null)
				return Refuse("The terminal expedition receipt has no realm job book.", out Failure);
			KingdomJobTable jobs;
			KingdomJobRow row;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out jobs, out fault))
				return Refuse("The terminal expedition receipt cannot read the realm job book.",
					out Failure);
			if (!jobs.TryGet(Requested.JobId, out row)) return true;
			if (!SameExpeditionIdentity(Requested, row)
				|| !KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode)
				|| !KingdomExpeditionRules.IsTerminalOutcome(row.OutcomeCode)
				|| string.IsNullOrEmpty(row.DestZoneId))
				return Refuse("The open job does not match a complete terminal expedition receipt.",
					out Failure);

			KingdomResidentStanding standing;
			KingdomStandingCause standingCause;
			KingdomUnbindCause unbindCause;
			string standingZone = row.DestZoneId;
			switch ((KingdomExpeditionOutcome)row.OutcomeCode)
			{
			case KingdomExpeditionOutcome.ResidentDiedOnGround:
				standing = KingdomResidentStanding.Dead;
				standingCause = KingdomStandingCause.Unwitnessed;
				unbindCause = KingdomUnbindCause.Death;
				break;
			case KingdomExpeditionOutcome.ResidentMissingFromBoundGround:
				standing = KingdomResidentStanding.Abroad;
				standingCause = KingdomStandingCause.Astray;
				unbindCause = KingdomUnbindCause.Abroad;
				break;
			default:
				standing = KingdomResidentStanding.Abroad;
				standingCause = KingdomStandingCause.Followed;
				unbindCause = KingdomUnbindCause.Abroad;
				break;
			}
			KingdomResidentRow existing;
			if (TryReadResident(System, row.SubjectId, out existing)
				&& existing.Standing == KingdomResidentStanding.Dead
				&& KingdomResidentRules.CauseFits(existing.Standing, existing.Cause))
			{
				// A later exact engine death outranks an earlier joined/missing telling, and a death
				// callback's known killer must not be rewritten as unwitnessed during retry.
				standing = KingdomResidentStanding.Dead;
				standingCause = existing.Cause;
				unbindCause = KingdomUnbindCause.Death;
				if (!string.IsNullOrEmpty(existing.BoundZoneId))
					standingZone = existing.BoundZoneId;
			}
			if (!TrySetResident(System, row.SubjectId, standing, standingCause, standingZone))
				return Refuse("The terminal result is durable, but the resident roll has not caught up.",
					out Failure);
			if (!EnsureResidentUnbound(System, row.SubjectId, unbindCause))
				return Refuse("The terminal result is durable, but its exact resident binding has not caught up.",
					out Failure);
			return TellAndClose(System, row, (KingdomExpeditionOutcome)row.OutcomeCode,
				row.DueTick, out Failure);
		}

		private static bool SameExpeditionIdentity(KingdomJobRow A, KingdomJobRow B)
		{
			return A.JobId == B.JobId && A.Kind == KingdomJobKind.Expedition
				&& B.Kind == KingdomJobKind.Expedition && A.SubjectId == B.SubjectId
				&& A.StartTick == B.StartTick && A.WaterCost == B.WaterCost
				&& A.ProvisionCost == B.ProvisionCost && A.CargoAmount == B.CargoAmount
				&& string.Equals(A.SourceZoneId, B.SourceZoneId, StringComparison.Ordinal)
				&& string.Equals(A.SubjectName, B.SubjectName, StringComparison.Ordinal)
				&& string.Equals(A.TargetName, B.TargetName, StringComparison.Ordinal);
		}

		/// <summary>Recovery bridge for an older interruption, or for the resident check-in that
		/// correctly observed a led/missing expedition body before the expedition semantic lane ran.
		/// Binding absence plus the resident row's typed standing/cause is durable terminal evidence;
		/// an ordinary living row or any retained binding is not.</summary>
		private static bool TryInferTerminalResidentEvidence(KingdomSystem System,
			KingdomJobRow Row, out KingdomExpeditionOutcome Resolution, out string ZoneId)
		{
			Resolution = KingdomExpeditionOutcome.None;
			ZoneId = null;
			if (System == null || System.Bindings == null) return false;
			KingdomBindingTable bindings;
			KingdomBinding held;
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out bindings, out fault)
				|| bindings.TryGet(Row.SubjectId, KingdomBindingKind.Resident, out held)) return false;
			KingdomResidentRow resident;
			if (!TryReadResident(System, Row.SubjectId, out resident)
				|| string.IsNullOrEmpty(resident.BoundZoneId)) return false;
			if (resident.Standing == KingdomResidentStanding.Dead
				&& KingdomResidentRules.CauseFits(resident.Standing, resident.Cause))
				Resolution = KingdomExpeditionOutcome.ResidentDiedOnGround;
			else if (resident.Standing == KingdomResidentStanding.Abroad
				&& resident.Cause == KingdomStandingCause.Astray)
				Resolution = KingdomExpeditionOutcome.ResidentMissingFromBoundGround;
			else if (resident.Standing == KingdomResidentStanding.Abroad
				&& resident.Cause == KingdomStandingCause.Followed)
				Resolution = KingdomExpeditionOutcome.ResidentJoinedFounder;
			else return false;
			ZoneId = resident.BoundZoneId;
			return true;
		}

		private static bool TellAndClose(KingdomSystem System, KingdomJobRow Row,
			KingdomExpeditionOutcome Resolution, long Tick, out string Failure)
		{
			Failure = null;
			string line = ResultLine(Row, Resolution, Tick);
			string eventId = "taf:expedition:" + Row.JobId + ":" + (int)Resolution;
			if (!KingdomChronicle.RecordOnce(System, eventId, ChronicleLine(Row, Resolution, Tick)))
				return Refuse("The result is physically settled, but its Chronicle receipt is not. The job remains open for a safe retry.", out Failure);
			if (!TryRecordExpeditionDeed(System, Row, Resolution, eventId, out Failure))
				return false;
			KingdomLedger ledger = LedgerFor(System, Row.SourceZoneId);
			if (ledger == null)
				return Refuse("The result is settled, but its home city's ledger cannot be found. The job remains open.", out Failure);
			ledger.NoteExpedition(line);
			KingdomJobTable current;
			KingdomJobTable next;
			KingdomJobRow closed;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out current, out fault))
				return Refuse("The result is told, but the realm job book cannot yet be read.", out Failure);
			if (!current.TryGet(Row.JobId, out closed)) return true;
			if (!current.TryClose(Row.JobId, out next, out closed, out fault)
				|| !System.Jobs.TryPublish(next, out fault))
				return Refuse("The result is told, but the job row could not yet be evicted.", out Failure);
			KingdomLog.Log("expedition: job " + Row.JobId + " closed once as " + Resolution);
			return true;
		}

	}
}
