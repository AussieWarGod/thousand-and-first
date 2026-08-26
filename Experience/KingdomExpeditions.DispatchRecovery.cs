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
		/// <summary>Forward-recovers prepared payment, exact body movement, binding, and standing.
		/// Each boundary is monotone: Prepared row, body receipt, paid row, then dispatched row.</summary>
		private static bool TryAdvanceDispatch(KingdomSystem System, KingdomJobRow Requested,
			GameObject PreferredBody, string PreparedReceipt, KingdomWaterDebit ReservedWater,
			long TimeTicks, bool LoadZone, KingdomSurvey SourceSurvey,
			out KingdomJobRow Advanced, out string Failure)
		{
			Advanced = Requested;
			Failure = null;
			if (System == null || System.Jobs == null || Requested.Kind != KingdomJobKind.Expedition
				|| Requested.JobId <= 0 || Requested.SubjectId <= 0)
				return Refuse("The prepared expedition authority is malformed.", out Failure);
			KingdomJobTable jobs;
			KingdomCityFault fault;
			KingdomJobRow row;
			if (!System.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(Requested.JobId, out row)
				|| !SameAuthority(Requested, row))
				return Refuse("The realm job row no longer matches the prepared authority.", out Failure);
			Advanced = row;
			GameObject body;
			string actualZone;
			BoundBodyState bodyState = FindBoundBody(System, row, LoadZone,
				out body, out actualZone);
			if (GameObject.Validate(PreferredBody)
				&& KingdomResidents.IdOf(PreferredBody) == row.SubjectId)
			{
				if (bodyState != BoundBodyState.Alive || !ReferenceEquals(body, PreferredBody))
					return Refuse("The selected body no longer matches its exact resident binding.", out Failure);
				body = PreferredBody;
			}
			if (bodyState != BoundBodyState.Alive || !GameObject.Validate(body))
				return Refuse(bodyState == BoundBodyState.Unreachable
					? "The exact resident body is unreachable; the prepared row remains open and no new debit was attempted."
					: "The exact resident body is no longer dispatchable; its dated cause must settle before any retry.", out Failure);
			if (KingdomPhysicalHappenings.IsStaged(body))
				return Refuse("The exact resident is attending a city occasion; its prepared expedition waits without moving or charging them.", out Failure);

			// Development-wire migration: old code published only after both physical costs had
			// committed. OriginCode zero therefore proves paid, never an invitation to charge again.
			if (row.OriginCode == (int)KingdomExpeditionPhase.LegacyPrepared)
			{
				if (!TryPublishPhase(System, row, KingdomExpeditionPhase.Paid, out row, out Failure))
					return false;
			}

			if (row.OriginCode == (int)KingdomExpeditionPhase.Prepared)
			{
				string encoded = body.GetStringProperty(DebitReceiptProperty);
				if (string.IsNullOrEmpty(encoded))
				{
					Zone source;
					if (!TryExactZone(row.SourceZoneId, LoadZone, out source))
						return Refuse("The receipt's source ground is unavailable; prepared authority remains open.", out Failure);
					if (HasDebitMarker(source, row.JobId))
						return Refuse("Debit markers exist but the body receipt is missing; no amount was charged again.", out Failure);
					if (string.IsNullOrEmpty(PreparedReceipt))
					{
						KingdomSurvey survey = SourceSurvey != null
							&& ReferenceEquals(SourceSurvey.Ground, source)
							? SourceSurvey : (LoadZone ? KingdomSurvey.Take(source, System) : null);
						if (survey == null)
							return Refuse("Prepared debit waits for the source ground's maintained survey.", out Failure);
						KingdomWaterDebit water = survey.ReserveExactWater(row.WaterCost);
						KingdomExpeditionDebitReceipt rebuilt;
						if (water.State != KingdomWaterDebitState.Reserved
							|| !TryPrepareDebitReceipt(survey, water, row.JobId, row.SourceZoneId,
								row.WaterCost, row.ProvisionCost, out rebuilt,
								out PreparedReceipt, out Failure))
							return Refuse("Prepared authority exists, but its untouched stores cannot reconstruct the exact receipt: "
								+ Failure, out Failure);
						ReservedWater = water;
					}
					try { body.SetStringProperty(DebitReceiptProperty, PreparedReceipt); }
					catch { return Refuse("The exact body refused its durable debit receipt.", out Failure); }
					encoded = body.GetStringProperty(DebitReceiptProperty);
				}
				else if (!string.IsNullOrEmpty(PreparedReceipt)
					&& !string.Equals(encoded, PreparedReceipt, StringComparison.Ordinal))
					return Refuse("The exact body already carries a different debit receipt.", out Failure);
				KingdomExpeditionDebitReceipt receipt;
				if (!TryReadReceipt(row, encoded, out receipt, out Failure)) return false;
				if (!TryApplyPreparedDebit(System, row, body, receipt, ReservedWater, out Failure))
					return false;
				if (!TryPublishPhase(System, row, KingdomExpeditionPhase.Paid, out row, out Failure))
					return false;
				ClearDebitMarkers(row);
			}
			if (!KingdomExpeditionRules.IsPaid(row.OriginCode))
				return Refuse("The expedition did not reach a paid dispatch phase.", out Failure);
			ClearDebitMarkers(row);

			Zone destination;
			if (!TryExactZone(row.DestZoneId, LoadZone, out destination))
				return Refuse("The recorded destination is unavailable; paid dispatch remains open.", out Failure);
			Cell destinationCell = SafeCell(destination);
			if (destinationCell == null)
				return Refuse("The recorded destination has no safe standing cell; paid dispatch remains open.", out Failure);
			try { body.SetIntProperty(ResidentJobProperty, row.JobId); }
			catch { return Refuse("The exact body refused its job marker; paid dispatch remains open.", out Failure); }
			if (!string.Equals(body.CurrentZone?.ZoneID, row.DestZoneId, StringComparison.Ordinal)
				&& !MoveExact(body, destinationCell))
				return Refuse("The exact body could not complete its recorded move; paid dispatch remains open.", out Failure);
			if (!KingdomResidents.Bind(System, row.SubjectId, KingdomBindingKind.Resident,
				row.DestZoneId, body, TimeTicks))
				return Refuse("The exact body moved, but its binding has not caught up; paid dispatch remains open.", out Failure);
			if (!TrySetResident(System, row.SubjectId, KingdomResidentStanding.Expedition,
				KingdomStandingCause.None, row.DestZoneId))
				return Refuse("The binding moved, but the resident roll has not caught up; paid dispatch remains open.", out Failure);
			if (!KingdomExpeditionRules.IsDispatched(row.OriginCode)
				&& !TryPublishPhase(System, row, KingdomExpeditionPhase.Dispatched,
					out row, out Failure)) return false;
			Advanced = row;
			return true;
		}

		private static bool TryPublishPhase(KingdomSystem System, KingdomJobRow Expected,
			KingdomExpeditionPhase Phase, out KingdomJobRow Published, out string Failure)
		{
			Published = Expected;
			Failure = null;
			KingdomJobTable table;
			KingdomJobRow current;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault) || !table.TryGet(Expected.JobId, out current)
				|| !SameAuthority(Expected, current))
				return Refuse("The realm job row changed during its phase publication.", out Failure);
			if (current.OriginCode == (int)Phase)
			{
				Published = current;
				return true;
			}
			KingdomJobRow rewritten = current.WithOriginCode((int)Phase);
			KingdomJobTable next;
			if (!table.TryReplace(rewritten, out next, out fault)
				|| !System.Jobs.TryPublish(next, out fault))
				return Refuse("The realm job row refused its next durable phase.", out Failure);
			Published = rewritten;
			return true;
		}

		private static bool SameAuthority(KingdomJobRow A, KingdomJobRow B)
		{
			return A.JobId == B.JobId && A.Kind == B.Kind && A.SubjectId == B.SubjectId
				&& A.StartTick == B.StartTick && A.DueTick == B.DueTick
				&& A.WaterCost == B.WaterCost && A.ProvisionCost == B.ProvisionCost
				&& A.OutcomeCode == B.OutcomeCode && A.CargoAmount == B.CargoAmount
				&& string.Equals(A.SourceZoneId, B.SourceZoneId, StringComparison.Ordinal)
				&& string.Equals(A.DestZoneId, B.DestZoneId, StringComparison.Ordinal);
		}

		private static bool TryReadReceipt(KingdomJobRow Row, string Encoded,
			out KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!KingdomExpeditionDebitReceipt.TryDecode(Encoded, out Receipt)
				|| Receipt.JobId != Row.JobId || Receipt.WaterCost != Row.WaterCost
				|| Receipt.ProvisionCost != Row.ProvisionCost
				|| !string.Equals(Receipt.SourceZoneId, Row.SourceZoneId, StringComparison.Ordinal))
				return Refuse("The body's bounded debit receipt does not match the realm job.", out Failure);
			return true;
		}

	}
}
