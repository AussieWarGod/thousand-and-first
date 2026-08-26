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
		/// <summary>
		/// Realm semantic step. It runs after check-in/construction and before happenings/digest:
		/// ground authority is established first, then a returning resident and their one result are
		/// visible to every later story/read surface in the same pass. All realm jobs are inspected,
		/// while physical recovery commits only on the job's source-ground survey. A seat exchange
		/// therefore retains the durable job without making this pass classify the other city.
		/// </summary>
		public static bool OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || The.Game == null || System.Jobs == null
				|| System.Jobs.Count == 0) return false;
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault))
			{
				KingdomLog.Log("expedition: semantic job read refused (" + fault + ")");
				return false;
			}
			int[] ids = table.OpenIds();
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomJobRow row;
				if (!table.TryGet(ids[i], out row) || row.Kind != KingdomJobKind.Expedition) continue;
				if (!KingdomExpeditionRules.IsPhase(row.OriginCode))
				{
					KingdomLog.Log("expedition: job " + row.JobId
						+ " has an unknown dispatch phase; no body or debit was guessed");
					continue;
				}
				if (KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode))
				{
					if (!TryResumeTerminalResolution(System, row, out string resolutionFailure))
						KingdomLog.Log("expedition: job " + row.JobId
							+ " terminal telling waits - " + resolutionFailure);
					continue;
				}
				// Physical recovery belongs to its source-ground pass. A realm row remains visible
				// after a seat exchange, but no bound pass thaws or classifies the other city's zone.
				if (!KingdomExpeditionRules.IsDispatched(row.OriginCode)
					&& (Z == null || Survey == null || !ReferenceEquals(Survey.Ground, Z)
						|| !string.Equals(Z.ZoneID, row.SourceZoneId, StringComparison.Ordinal)))
					continue;
				if (!KingdomExpeditionRules.IsDispatched(row.OriginCode)
					&& !TryAdvanceDispatch(System, row, null, null, null, The.Game.TimeTicks,
						LoadZone: false, SourceSurvey: Survey,
						out row, out string dispatchFailure))
				{
					GameObject strandedBody;
					string strandedZone;
					BoundBodyState stranded = FindBoundBody(System, row, LoadZone: false,
						out strandedBody, out strandedZone);
					if (stranded == BoundBodyState.Dead)
						TryResolve(System, row, KingdomExpeditionOutcome.ResidentDiedOnGround,
							The.Game.TimeTicks, Award: false, LoadZone: false,
							SourceSurvey: Survey, out string _);
					else if (stranded == BoundBodyState.Missing)
						TryResolve(System, row, KingdomExpeditionOutcome.ResidentMissingFromBoundGround,
							The.Game.TimeTicks, Award: false, LoadZone: false,
							SourceSurvey: Survey, out string _);
					else if (stranded == BoundBodyState.Led)
						TryResolve(System, row, KingdomExpeditionOutcome.ResidentJoinedFounder,
							The.Game.TimeTicks, Award: false, LoadZone: false,
							SourceSurvey: Survey, out string _);
					KingdomLog.Log("expedition: job " + row.JobId
						+ " dispatch waits - " + dispatchFailure);
					continue;
				}
				KingdomExpeditionOutcome outcome = (KingdomExpeditionOutcome)row.OutcomeCode;
				if (!KingdomExpeditionRules.IsFrozenOutcome(row.OutcomeCode))
				{
					// A malformed current row has no authority to draw. Bring the named body back if
					// possible and close it as a dated, cargo-free recall instead of guessing.
					TryResolve(System, row, KingdomExpeditionOutcome.Cancelled,
						The.Game.TimeTicks, Award: false, LoadZone: false,
						SourceSurvey: Survey, out string _);
					continue;
				}
				if (KingdomExpeditionRules.Due(The.Game.TimeTicks, row.DueTick))
				{
					// Return commits into the owning source ground. A pass elsewhere leaves the dated
					// job open; visiting/loading that source gives it one maintained survey to use.
					if (Z == null || Survey == null || !ReferenceEquals(Survey.Ground, Z)
						|| !string.Equals(Z.ZoneID, row.SourceZoneId, StringComparison.Ordinal)) continue;
					TryResolve(System, row, outcome, row.DueTick, Award: true, LoadZone: false,
						SourceSurvey: Survey, out string failure);
					if (!string.IsNullOrEmpty(failure)) KingdomLog.Log("expedition: job "
						+ row.JobId + " return waits - " + failure);
				}
				// Not due: durable dispatched authority waits without touching remote ground.
			}
			return false;
		}

		/// <summary>Death callbacks run before Qud removes a corpse. Freeze an open expedition's
		/// terminal authority while its exact binding and ground can still be proved, before the
		/// ordinary citizen-death path changes the resident row and releases that binding.</summary>
		internal static bool TryPrepareResidentDeath(KingdomSystem System, GameObject Body,
			long Tick, out string Failure)
		{
			Failure = null;
			if (System == null || System.Jobs == null || System.Bindings == null
				|| System.Jobs.Count == 0) return true;
			int residentId = KingdomResidents.IdOf(Body);
			if (residentId <= 0) return true;
			KingdomJobTable jobs;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out jobs, out fault))
				return Refuse("A resident died while the expedition job book was unreadable; no terminal receipt was guessed.",
					out Failure);
			KingdomJobRow row = default(KingdomJobRow);
			bool found = false;
			int[] ids = jobs.OpenIds();
			for (int i = 0; i < ids.Length; i++)
			{
				KingdomJobRow candidate;
				if (jobs.TryGet(ids[i], out candidate)
					&& candidate.Kind == KingdomJobKind.Expedition
					&& candidate.SubjectId == residentId)
				{
					row = candidate;
					found = true;
					break;
				}
			}
			if (!found) return true;
			if (KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode)) return true;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (!System.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(residentId, KingdomBindingKind.Resident, out binding)
				|| !ReferenceEquals(KingdomResidents.FindExactBindingObject(binding), Body))
				return Refuse("The dying expedition resident no longer matches the exact live binding; no terminal receipt was guessed.",
					out Failure);
			string zoneId = Body.CurrentZone?.ZoneID ?? binding.ZoneId;
			if (!TryPublishTerminalResolution(System, row,
				KingdomExpeditionOutcome.ResidentDiedOnGround, Tick, zoneId,
				out KingdomJobRow _, out Failure)) return false;
			try
			{
				Body.RemoveIntProperty(ResidentJobProperty);
				Body.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
			}
			catch
			{
				return Refuse("The dying resident's terminal receipt is durable, but its body markers could not yet be cleared.",
					out Failure);
			}
			return true;
		}

	}
}
