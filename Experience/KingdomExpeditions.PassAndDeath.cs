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

		// Witnessed death is not inferred from a missing binding or body. The journal supplies
		// the exact job before/prepared pair; only that pair can authorize this narrow bridge.
		internal static bool TryCaptureWitnessedDeath(KingdomSystem system, int id, long tick,
			string zone, out string before, out string prepared)
		{
			before = ""; prepared = "";
			if (!ReadWitnessJobs(system, id, out var jobs, out var row, out bool found)) return false;
			if (!found) return true;
			if (tick <= row.StartTick || string.IsNullOrEmpty(zone)) return false;
			if (KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode))
			{
				if (!WitnessPrepared(row, tick)) return false;
				// A later death does not replace an already-owned expedition result or its date.
				before = prepared = WitnessJob(row); return true;
			}
			var next = row.WithExpeditionResolution((int)KingdomExpeditionOutcome.ResidentDiedOnGround,
				tick, zone, KingdomExpeditionDeedDisposition.NotApplicable, null, null, null);
			if (!jobs.TryReplace(next, out _, out _)) return false;
			before = WitnessJob(row); prepared = WitnessJob(next); return true;
		}
		internal static bool TryPrepareWitnessedDeath(KingdomSystem system, int id, long tick,
			string zone, string before, string prepared, GameObject body, Func<bool> exact)
		{
			if (exact == null || !exact() || !ReadWitnessJobs(system, id, out _, out var row, out bool found)) return false;
			if (before == "" || prepared == "") return before == "" && prepared == "" && !found && exact();
			if (!found) return false;
			string current = WitnessJob(row);
			if (current != before && current != prepared) return false;
			if (before == prepared && !WitnessPrepared(row, tick)) return false;
			if (current == before && before != prepared)
			{
				var expected = row.WithExpeditionResolution((int)KingdomExpeditionOutcome.ResidentDiedOnGround,
					tick, zone, KingdomExpeditionDeedDisposition.NotApplicable, null, null, null);
				if (tick <= row.StartTick || WitnessJob(expected) != prepared || !exact()
					|| !TryPublishTerminalResolution(system, row, KingdomExpeditionOutcome.ResidentDiedOnGround,
						tick, zone, out _, out _)) return false;
			}
			if (!exact() || !ReadWitnessJobs(system, id, out _, out var after, out found)
				|| !found || WitnessJob(after) != prepared) return false;
			if (body != null)
			{
				if (KingdomResidents.IdOf(body) != id || !exact()) return false;
				body.RemoveIntProperty(ResidentJobProperty);
				if (!exact()) return false;
				body.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
			}
			return exact();
		}
		private static bool WitnessPrepared(KingdomJobRow row, long tick)
		{
			return KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode)
				&& KingdomJobRules.ValidExpeditionOutcomeForPhase(row.OriginCode, row.OutcomeCode)
				&& KingdomJobRules.ValidExpeditionResultReceipt(row) && row.DueTick > row.StartTick
				&& row.DueTick <= tick && !string.IsNullOrEmpty(row.DestZoneId);
		}
		private static bool ReadWitnessJobs(KingdomSystem system, int id,
			out KingdomJobTable table, out KingdomJobRow row, out bool found)
		{
			table = null; row = default(KingdomJobRow); found = false;
			var source = system?.Jobs;
			if (source == null || !source.TryProjectResidentTransition(id, out _)) return false;
			try
			{
				string raw = WitnessJobs(source);
				var copy = KingdomRealmArchive.CloneJobs(source);
				if (!copy.TryRead(out table, out _) || WitnessJobs(copy) != raw
					|| !ReferenceEquals(source, system.Jobs) || WitnessJobs(source) != raw) return false;
				for (int i = 0; i < table.Count; i++)
				{
					if (!table.TryAt(i, out var candidate)) return false;
					if (candidate.Kind != KingdomJobKind.Expedition || candidate.SubjectId != id) continue;
					if (found) return false; row = candidate; found = true;
				}
				return true;
			}
			catch { table = null; return false; }
		}
		private static string WitnessJob(KingdomJobRow row)
		{
			if (!KingdomJobTable.TryCreate(new[] { row }, out var table, out _)) throw new InvalidOperationException();
			var copy = new KingdomJobRegistry();
			if (!copy.TryPublish(table, out _)) throw new InvalidOperationException();
			return WitnessJobs(copy);
		}
		private static string WitnessJobs(KingdomJobRegistry x)
		{
			if (x.JobCounter < 0 || x.JobIds == null || x.JobIds.Count > KingdomJobRules.MaxOpenJobs)
				throw new InvalidOperationException();
			for (int i = 0; i < x.JobIds.Count; i++)
				if (!Enum.IsDefined(typeof(KingdomJobKind), (byte)x.Kinds[i]) || x.Kinds[i] < 0 || x.Kinds[i] > 255
					|| !Enum.IsDefined(typeof(KingdomStockKind), (byte)x.Cargos[i]) || x.Cargos[i] < 0 || x.Cargos[i] > 255
					|| !Enum.IsDefined(typeof(KingdomJobStatus), (byte)x.Statuses[i]) || x.Statuses[i] < 0 || x.Statuses[i] > 255)
					throw new InvalidOperationException();
			object[] columns = { x.JobIds, x.Kinds, x.Cargos, x.CargoAmounts, x.SourceZoneIds, x.DestZoneIds,
				x.StartTicks, x.WalkTicksPerCell, x.Statuses, x.OriginCodes, x.DepositLegIndexes, x.SubjectIds,
				x.SubjectNames, x.TargetNames, x.DueTicks, x.WaterCosts, x.ProvisionCosts, x.OutcomeCodes,
				x.ExpeditionDeedDispositions, x.ExpeditionDeedPolityIds, x.ExpeditionDeedCauseRefs, x.ExpeditionDeedFigureRefs,
				x.DeliverySourceEndpointIds, x.DeliverySourceObjectIds, x.DeliverySourceXs, x.DeliverySourceYs,
				x.DeliveryTargetEndpointIds, x.DeliveryTargetObjectIds, x.DeliveryTargetXs, x.DeliveryTargetYs,
				x.DeliverySourceBeforeAmounts, x.DeliveryTripIds, x.DeliveryStopOrdinals, x.DeliveryPhases,
				x.DeliveryCargoAuthorityKinds, x.DeliveryOwnerOperationIds, x.DeliveryOwnerManifestVersions,
				x.DeliveryOwnerManifestDigests, x.DeliveryOwnerManifestRevisions, x.DeliveryManifestSourceStarts,
				x.DeliveryManifestSourceCounts, x.DeliveryTargetBeforeAmounts, x.DeliveryTargetReceiptStates,
				x.LegCounts, x.LegZoneIds, x.LegEnterX, x.LegEnterY, x.LegExitX, x.LegExitY, x.LegLengths,
				x.LegDepartTicks, x.LegArriveTicks };
			var values = new List<string> { x.JobCounter.ToString(global::System.Globalization.CultureInfo.InvariantCulture) };
			foreach (object column in columns)
			{
				var items = column as global::System.Collections.IList;
				if (items == null || items.Count > KingdomJobRules.MaxOpenJobs * KingdomItineraryRules.MaxLegs)
					throw new InvalidOperationException();
				var fields = new List<string>();
				foreach (object item in items)
				{
					if (item == null) fields.Add(null);
					else if (item is string text && KingdomResidentDeathRules.Text(text, 1024)) fields.Add(text);
					else if (item is int number) fields.Add(number.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
					else if (item is long tick) fields.Add(tick.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
					else throw new InvalidOperationException();
				}
				values.Add(KingdomResidentDeathCodec.Fields(fields.ToArray()));
			}
			return KingdomResidentDeathCodec.Fields(values.ToArray());
		}

	}
}
