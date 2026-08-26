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
	/// <summary>
	/// Engine edge for the locked salvage commission. The durable realm job is the itinerary;
	/// the resident row is labour/standing; the resident binding names the one body; journal
	/// visitation is destination authority; dedicated stores pay physical costs; Chronicle and
	/// the existing ledger tell the one result. No proxy or second mission save object exists.
	/// </summary>
	public static class KingdomExpeditions
	{
		public const string ResidentJobProperty = "r_TAF_ExpeditionJob";
		public const string ProvisionJobProperty = "r_TAF_ExpeditionProvisionJob";
		public const string RewardJobProperty = "r_TAF_ExpeditionRewardJob";
		public const string DebitReceiptProperty = "r_TAF_ExpeditionDebitReceipt";
		public const string WaterJobProperty = "r_TAF_ExpeditionWaterJob";
		public const string WaterAfterProperty = "r_TAF_ExpeditionWaterAfter";

		private sealed class ResidentChoice
		{
			internal KingdomResidentRow Row;
			internal string ZoneId;
		}

		private sealed class TargetChoice
		{
			internal JournalMapNote Note;
			internal string ZoneId;
			internal string Name;
			internal KingdomExpeditionQuote Quote;
		}

		private enum BoundBodyState : byte
		{
			Unreachable = 0,
			Alive = 1,
			Led = 2,
			Dead = 3,
			Missing = 4,
			Ambiguous = 5
		}

		/// <summary>Charter route: inspect/recall open jobs or begin a new commission.</summary>
		public static void Open(KingdomSystem System, GameObject Actor)
		{
			if (System == null || !System.Founded || !GameObject.Validate(Actor)
				|| Actor.CurrentZone == null || System.Jobs == null)
			{
				Popup.Show("The Charter cannot find a founded city, its job book, and your present ground.");
				return;
			}
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault))
			{
				Popup.Show("The realm's job book could not be read. No resident or goods were moved.");
				return;
			}
			List<KingdomJobRow> open = ExpeditionRows(table);
			if (open.Count == 0)
			{
				OpenDispatch(System, Actor, table);
				return;
			}
			string[] options = new string[open.Count + 1];
			char[] hotkeys = new char[open.Count + 1];
			options[0] = "Commission another salvage expedition";
			hotkeys[0] = 'n';
			long now = (The.Game == null) ? 0L : The.Game.TimeTicks;
			for (int i = 0; i < open.Count; i++)
			{
				KingdomJobRow row = open[i];
				bool terminal = KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode);
				options[i + 1] = (terminal
					? "Finish recording " : "Recall ") + ShownName(row.SubjectName,
					"resident " + row.SubjectId) + (terminal ? "'s result from " : " from ")
					+ ShownName(row.TargetName, row.DestZoneId) + " — "
					+ KingdomCharterMenuRules.DueWhen(row.DueTick, now, KingdomRules.TicksPerDay);
				hotkeys[i + 1] = (char)('a' + (i % 26));
			}
			int pick = Popup.PickOption(Title: "Salvage expeditions of " + KingdomPresentation.Rich(System.SeatName),
				Intro: "Each line is one named resident, one real body, and one dated realm job.",
				Options: options, Hotkeys: hotkeys, AllowEscape: true);
			if (pick < 0) return;
			if (pick == 0)
			{
				OpenDispatch(System, Actor, table);
				return;
			}
			KingdomJobRow chosen = open[pick - 1];
			if (KingdomExpeditionRules.IsResolutionPrepared(chosen.OriginCode))
			{
				if (!TryResumeTerminalResolution(System, chosen, out string terminalFailure))
				{
					Popup.Show(terminalFailure);
					return;
				}
				KingdomGovernanceScope.Commit("finish salvage expedition result");
				Popup.Show("The dated expedition result is in the Chronicle and homecoming ledger.");
				return;
			}
			if (Popup.ShowYesNo("Recall {{W|" + ShownName(chosen.SubjectName, "this resident")
				+ "}} from {{C|" + ShownName(chosen.TargetName, chosen.DestZoneId)
				+ "}}?\n\nWater and provisions were committed to the route at dispatch. Recall returns the exact "
				+ "resident body; it neither refunds nor spends those goods a second time.") != DialogResult.Yes) return;
			if (!TryResolve(System, chosen, KingdomExpeditionOutcome.Cancelled,
				(The.Game == null) ? 0L : The.Game.TimeTicks, Award: false, LoadZone: true,
				SourceSurvey: null, out string failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomGovernanceScope.Commit("recall salvage expedition");
			Popup.Show(ShownName(chosen.SubjectName, "The resident")
				+ " has returned. The dated recall is in the homecoming ledger.");
		}

		private static void OpenDispatch(KingdomSystem System, GameObject Actor,
			KingdomJobTable Snapshot)
		{
			if (Snapshot.Count >= KingdomJobRules.MaxOpenJobs)
			{
				Popup.Show("The realm already has sixteen open jobs. Close or finish one before sending another resident.");
				return;
			}
			if (!LedgerHasRoom(System, Actor.CurrentZone.ZoneID, Snapshot))
			{
				Popup.Show("This city's named-expedition ledger is full. Read its homecoming report before commissioning another.");
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (System.City == null || !System.City.TryRead(out state, out fault))
			{
				Popup.Show("The seated city's resident roll could not be read. No one was sent.");
				return;
			}
			List<ResidentChoice> residents = EligibleResidents(System, state, Snapshot,
				Actor.CurrentZone.ZoneID);
			if (residents.Count == 0)
			{
				Popup.Show("No eligible named resident is available. A candidate must be living here, bound to one exact body, not following you, and not already on an expedition.");
				return;
			}
			string[] residentOptions = new string[residents.Count];
			for (int i = 0; i < residents.Count; i++)
				residentOptions[i] = ShownName(residents[i].Row.Name,
					"resident " + residents[i].Row.ResidentId) + " {{K|— bound in "
					+ KingdomPresentation.Rich(residents[i].ZoneId) + "}}";
			int residentPick = Popup.PickOption(Title: "Who will go?", Options: residentOptions,
				AllowEscape: true);
			if (residentPick < 0) return;
			ResidentChoice resident = residents[residentPick];
			GameObject body;
			string sourceZoneId;
			if (!KingdomResidents.TryResolveBoundBody(System, resident.Row.ResidentId,
				LoadZone: true, out body, out sourceZoneId))
			{
				Popup.Show(ShownName(resident.Row.Name, "The resident")
					+ "'s exact bound body could not be reached. No substitute was minted and no stores were spent.");
				return;
			}
			List<TargetChoice> targets = VisitedTargets(System, sourceZoneId,
				(The.Game == null) ? 0L : The.Game.TimeTicks);
			if (targets.Count == 0)
			{
				Popup.Show("Your journal holds no eligible destination you personally visited. Revealed hearsay is not enough, and realm ground is not a salvage destination.");
				return;
			}
			string[] targetOptions = new string[targets.Count];
			for (int i = 0; i < targets.Count; i++)
			{
				KingdomExpeditionQuote q = targets[i].Quote;
				targetOptions[i] = ShownName(targets[i].Name, targets[i].ZoneId)
					+ " {{K|— " + q.DurationDays + " world days; "
					+ q.WaterDrams + " drams, " + q.Provisions + " provisions}}";
			}
			int targetPick = Popup.PickOption(Title: "Where will "
				+ ShownName(resident.Row.Name, "the resident") + " search?",
				Options: targetOptions, AllowEscape: true);
			if (targetPick < 0) return;
			TargetChoice target = targets[targetPick];
			KingdomSurvey survey = KingdomSurvey.Take(Actor.CurrentZone, System);
			if (survey == null || survey.StoredWater < target.Quote.WaterDrams
				|| survey.FoodStored < target.Quote.Provisions)
			{
				Popup.Show("The dedicated stores on this ground hold "
					+ ((survey == null) ? 0 : survey.StoredWater) + " drams and "
					+ ((survey == null) ? 0 : survey.FoodStored) + " provisions; this commission needs exactly "
					+ target.Quote.WaterDrams + " and " + target.Quote.Provisions + ".");
				return;
			}
			string preview = "Send {{W|" + ShownName(resident.Row.Name, "the resident")
				+ "}} to {{C|" + ShownName(target.Name, target.ZoneId)
				+ "}}?\n\nExact dispatch: {{C|" + target.Quote.WaterDrams + " drams of fresh water}} and {{C|"
				+ target.Quote.Provisions + " physical provisions}} from this ground's dedicated stores.\n"
				+ "World time: {{C|" + target.Quote.DurationDays + " days}}, due "
				+ DateAt(target.Quote.DueTick) + ".\n\nThe exact resident body travels. The result is frozen now, resolves once, and returns through the homecoming ledger.";
			if (Popup.ShowYesNo(preview) != DialogResult.Yes) return;
			if (!TryDispatch(System, Actor, body, resident.Row, sourceZoneId, target, survey,
				out bool departed, out string failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomGovernanceScope.Commit("commission salvage expedition");
			Popup.Show(departed
				? ShownName(resident.Row.Name, "The resident") + " set out for "
					+ ShownName(target.Name, target.ZoneId) + ". Their return is due "
					+ DateAt(target.Quote.DueTick) + "."
				: "The commission is durably recorded, but its exact dispatch receipt still needs "
					+ "reconciliation. No second job can be opened for "
					+ ShownName(resident.Row.Name, "the resident")
					+ "; revisit this ground or inspect the Charter after the next city pass.");
		}

		private static bool TryDispatch(KingdomSystem System, GameObject Actor, GameObject Body,
			KingdomResidentRow Resident, string SourceZoneId, TargetChoice Target,
			KingdomSurvey Survey, out bool Departed, out string Failure)
		{
			Departed = false;
			Failure = null;
			long now = (The.Game == null) ? -1L : The.Game.TimeTicks;
			if (now < 0L || !GameObject.Validate(Body) || !Body.IsAlive || Body.IsPlayer()
				|| Body.IsPlayerLed() || KingdomPhysicalHappenings.IsStaged(Body)
				|| Body.CurrentCell == null
				|| KingdomResidents.IdOf(Body) != Resident.ResidentId
				|| Target == null || Target.Note == null || !Target.Note.Revealed
				|| !Target.Note.Visited || Target.Note.LastVisit < 0L
				|| !string.Equals(Target.Note.ZoneID, Target.ZoneId, StringComparison.Ordinal))
				return Refuse("The resident or personally visited journal destination changed before confirmation. Nothing was spent.", out Failure);
			KingdomExpeditionQuote requoted;
			if (!KingdomExpeditionRules.TryQuote(SourceZoneId, Target.ZoneId, now, out requoted)
				|| requoted.DueTick != Target.Quote.DueTick
				|| requoted.WaterDrams != Target.Quote.WaterDrams
				|| requoted.Provisions != Target.Quote.Provisions)
				return Refuse("World time or the route changed before confirmation. Reopen the commission for a fresh exact quote.", out Failure);
			Zone destination;
			try { destination = The.ZoneManager.GetZone(Target.ZoneId); }
			catch (Exception ex)
			{
				KingdomLog.Log("expedition: destination thaw refused (" + ex.GetType().Name + ")");
				return Refuse("That destination could not be reached by the world map. No stores were spent.", out Failure);
			}
			Cell destinationCell = SafeCell(destination);
			if (destinationCell == null)
				return Refuse("No safe standing cell could be found at that destination. No stores were spent.", out Failure);
			KingdomJobTable table;
			KingdomCityFault fault;
			if (!System.Jobs.TryRead(out table, out fault) || table.Count >= KingdomJobRules.MaxOpenJobs
				|| HasExpedition(table, Resident.ResidentId))
				return Refuse("The realm job book changed before confirmation. No stores were spent.", out Failure);
			// A closed job may have cut after row eviction but before body-marker cleanup. No open
			// expedition owns these feature-private properties now, so clear them before the new
			// prepared authority is published; this moves no body and spends no goods.
			try
			{
				Body.RemoveIntProperty(ResidentJobProperty);
				Body.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
			}
			catch
			{
				return Refuse("The resident's closed-expedition markers could not be cleared. No stores were spent.", out Failure);
			}
			int jobId = System.Jobs.MintJobId();
			KingdomExpeditionOutcome outcome;
			int scrap;
			string settlementId = (System.City == null) ? null : System.City.SettlementId;
			if (string.IsNullOrEmpty(settlementId)) settlementId = KingdomChronicle.SettlementId(System);
			if (!KingdomExpeditionRules.TryDrawOutcome(System.SimulationSeed, settlementId, jobId,
				SkillBonus(Body), out outcome, out scrap))
				return Refuse("The commission could not freeze a deterministic result. No stores were spent.", out Failure);
			KingdomWaterDebit water = Survey.ReserveExactWater(requoted.WaterDrams);
			KingdomExpeditionDebitReceipt receipt;
			string encodedReceipt;
			if (water.State != KingdomWaterDebitState.Reserved
				|| !TryPrepareDebitReceipt(Survey, water, jobId, SourceZoneId,
					requoted.WaterDrams, requoted.Provisions, out receipt,
					out encodedReceipt, out Failure))
				return Refuse(string.IsNullOrEmpty(Failure)
					? "The exact dedicated water and provisions changed before a bounded receipt could be prepared. Nothing was moved."
					: Failure, out Failure);
			KingdomJobRow row = new KingdomJobRow(jobId, KingdomJobKind.Expedition,
				KingdomStockKind.Materials, scrap, SourceZoneId, Target.ZoneId, now,
				KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open,
				(int)KingdomExpeditionPhase.Prepared, 0,
				new KingdomLeg[0], 0, Resident.ResidentId, Resident.Name, Target.Name,
				requoted.DueTick, requoted.WaterDrams, requoted.Provisions, (int)outcome);
			KingdomJobTable opened;
			if (!table.TryOpen(row, out opened, out fault))
				return Refuse("The realm job book has no safe slot. No stores were spent.", out Failure);
			// P0 ordering: prepared authority publishes before receipt attachment and before every
			// physical drain/destroy callback. From this cut onward, forward recovery owns the exact
			// object identities and CAS ranges; retry can never open a second commission.
			if (!System.Jobs.TryPublish(opened, out fault))
				return Refuse("The prepared job book changed before any physical callback. No stores were spent.", out Failure);
			if (!TryAdvanceDispatch(System, row, Body, encodedReceipt, water, now,
				LoadZone: true, SourceSurvey: Survey,
				out KingdomJobRow advanced, out Failure))
			{
				KingdomLog.Log("expedition: job " + jobId
					+ " is durable; exact dispatch reconciliation waits - " + Failure);
				Failure = null;
				return true;
			}
			Departed = KingdomExpeditionRules.IsDispatched(advanced.OriginCode);
			return true;
		}

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
				ResolutionZoneId);
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

		private static List<ResidentChoice> EligibleResidents(KingdomSystem System,
			KingdomCityState State, KingdomJobTable Jobs, string Here)
		{
			List<ResidentChoice> choices = new List<ResidentChoice>();
			for (int i = 0; i < State.ResidentCount; i++)
			{
				KingdomResidentRow row;
				string zoneId;
				if (!State.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Resident
					|| row.ResidentId <= 0 || string.IsNullOrEmpty(row.Name)
					|| HasExpedition(Jobs, row.ResidentId)
					|| !KingdomResidents.TryBoundZone(System, row.ResidentId,
						KingdomBindingKind.Resident, out zoneId)
					|| !string.Equals(zoneId, Here, StringComparison.Ordinal)) continue;
				KingdomBodyPresence presence = KingdomResidents.PresenceOfKey(System, row.ResidentId,
					KingdomBindingKind.Resident, zoneId);
				if (presence == KingdomBodyPresence.None) continue;
				choices.Add(new ResidentChoice { Row = row, ZoneId = zoneId });
			}
			choices.Sort(delegate(ResidentChoice a, ResidentChoice b)
			{
				int byName = string.Compare(a.Row.Name, b.Row.Name, StringComparison.Ordinal);
				return (byName != 0) ? byName : a.Row.ResidentId.CompareTo(b.Row.ResidentId);
			});
			return choices;
		}

		private static List<TargetChoice> VisitedTargets(KingdomSystem System, string SourceZoneId,
			long StartTick)
		{
			List<TargetChoice> choices = new List<TargetChoice>();
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; JournalAPI.MapNotes != null && i < JournalAPI.MapNotes.Count; i++)
			{
				JournalMapNote note = JournalAPI.MapNotes[i];
				string zoneId = (note == null) ? null : note.ZoneID;
				KingdomExpeditionQuote quote;
				if (note == null || !note.Revealed || !note.Visited || note.LastVisit < 0L
					|| string.IsNullOrEmpty(zoneId) || !seen.Add(zoneId)
					|| RealmHolds(System, zoneId)
					|| !KingdomExpeditionRules.TryQuote(SourceZoneId, zoneId, StartTick, out quote)) continue;
				choices.Add(new TargetChoice
				{
					Note = note,
					ZoneId = zoneId,
					Name = SafeName(ConsoleLib.Console.ColorUtility.StripFormatting(note.Text), zoneId),
					Quote = quote
				});
			}
			choices.Sort(delegate(TargetChoice a, TargetChoice b)
			{
				int byName = string.Compare(a.Name, b.Name, StringComparison.Ordinal);
				return (byName != 0) ? byName : string.CompareOrdinal(a.ZoneId, b.ZoneId);
			});
			return choices;
		}

		private static List<KingdomJobRow> ExpeditionRows(KingdomJobTable Table)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; i < Table.Count; i++)
			{
				KingdomJobRow row;
				if (Table.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition) rows.Add(row);
			}
			rows.Sort((a, b) => a.JobId.CompareTo(b.JobId));
			return rows;
		}

		private static bool HasExpedition(KingdomJobTable Table, int ResidentId)
		{
			if (Table == null || ResidentId <= 0) return false;
			for (int i = 0; i < Table.Count; i++)
			{
				KingdomJobRow row;
				if (Table.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition
					&& row.SubjectId == ResidentId) return true;
			}
			return false;
		}

		private static bool LedgerHasRoom(KingdomSystem System, string ZoneId,
			KingdomJobTable Jobs)
		{
			KingdomLedger ledger = LedgerFor(System, ZoneId);
			if (ledger == null) return false;
			ledger.Normalize();
			int promised = 0;
			for (int i = 0; Jobs != null && i < Jobs.Count; i++)
			{
				KingdomJobRow row;
				if (Jobs.TryAt(i, out row) && row.Kind == KingdomJobKind.Expedition
					&& LedgerFor(System, row.SourceZoneId) == ledger) promised++;
			}
			return ledger.ExpeditionLines.Count + promised < KingdomJobRules.MaxOpenJobs;
		}

		private static KingdomLedger LedgerFor(KingdomSystem System, string ZoneId)
		{
			if (System == null || string.IsNullOrEmpty(ZoneId)) return null;
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(ZoneId))
				return System.Ledger;
			if (System.Away != null && System.Away.ClaimedZones != null
				&& System.Away.ClaimedZones.Contains(ZoneId)) return System.Away.Ledger;
			return null;
		}

		private static bool RealmHolds(KingdomSystem System, string ZoneId)
		{
			return System != null && ((System.ClaimedZones != null
				&& System.ClaimedZones.Contains(ZoneId)) || (System.Away != null
				&& System.Away.ClaimedZones != null && System.Away.ClaimedZones.Contains(ZoneId)));
		}

		private static bool TrySetResident(KingdomSystem System, int ResidentId,
			KingdomResidentStanding Standing, KingdomStandingCause Cause, string ZoneId)
		{
			KingdomCityBook[] books = new KingdomCityBook[2]
			{
				(System == null) ? null : System.City,
				(System == null || System.Away == null) ? null : System.Away.City
			};
			for (int b = 0; b < books.Length; b++)
			{
				KingdomCityBook book = books[b];
				KingdomCityState state;
				KingdomCityFault fault;
				int index;
				if (book == null || !book.TryRead(out state, out fault)
					|| !state.TryResidentIndex(ResidentId, out index)) continue;
				KingdomResidentRow[] rows = new KingdomResidentRow[state.ResidentCount];
				for (int i = 0; i < rows.Length; i++)
					if (!state.TryResident(i, out rows[i])) return false;
				KingdomResidentRow before = rows[index];
				KingdomResidentRow after = before.WithStanding(Standing, Cause).WithBoundZone(ZoneId);
				if (before.Standing == after.Standing && before.Cause == after.Cause
					&& string.Equals(before.BoundZoneId, after.BoundZoneId, StringComparison.Ordinal)) return true;
				rows[index] = after;
				KingdomCityState written;
				return state.TryWithResidents(rows, out written, out fault)
					&& book.TryPublish(written, out fault);
			}
			return false;
		}

		private static bool TryReadResident(KingdomSystem System, int ResidentId,
			out KingdomResidentRow Resident)
		{
			Resident = default(KingdomResidentRow);
			KingdomCityBook[] books = new KingdomCityBook[2]
			{
				(System == null) ? null : System.City,
				(System == null || System.Away == null) ? null : System.Away.City
			};
			for (int b = 0; b < books.Length; b++)
			{
				KingdomCityState state;
				KingdomCityFault fault;
				int index;
				if (books[b] != null && books[b].TryRead(out state, out fault)
					&& state.TryResidentIndex(ResidentId, out index)
					&& state.TryResident(index, out Resident)) return true;
			}
			return false;
		}

		private static bool EnsureResidentUnbound(KingdomSystem System, int ResidentId,
			KingdomUnbindCause Cause)
		{
			if (System == null || System.Bindings == null || ResidentId <= 0) return false;
			KingdomBindingTable table;
			KingdomBinding binding;
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault)) return false;
			if (!table.TryGet(ResidentId, KingdomBindingKind.Resident, out binding)) return true;
			GameObject exact = KingdomResidents.FindExactBindingObject(binding);
			if (GameObject.Validate(exact))
			{
				try
				{
					exact.RemoveIntProperty(ResidentJobProperty);
					exact.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);
				}
				catch { return false; }
			}
			KingdomResidents.Unbind(System, ResidentId, KingdomBindingKind.Resident, Cause);
			if (!System.Bindings.TryRead(out table, out fault)) return false;
			return !table.TryGet(ResidentId, KingdomBindingKind.Resident, out binding);
		}

		private static BoundBodyState FindBoundBody(KingdomSystem System, KingdomJobRow Row,
			bool LoadZone, out GameObject Body, out string ZoneId)
		{
			Body = null;
			ZoneId = null;
			if (System == null || System.Bindings == null || The.ZoneManager == null
				|| Row.Kind != KingdomJobKind.Expedition || Row.JobId <= 0
				|| Row.SubjectId <= 0) return BoundBodyState.Unreachable;
			KingdomBindingTable table;
			KingdomBinding binding;
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault)
				|| !table.TryGet(Row.SubjectId, KingdomBindingKind.Resident, out binding)
				|| binding.BindingKey != Row.SubjectId
				|| binding.Kind != KingdomBindingKind.Resident
				|| string.IsNullOrEmpty(binding.ZoneId) || string.IsNullOrEmpty(binding.ObjectId))
				return BoundBodyState.Unreachable;

			// Exact engine id is the physical authority. Explicit Charter actions may thaw only the
			// three transaction grounds; settlement passes pass LoadZone=false and therefore defer.
			GameObject exact = KingdomResidents.FindExactBindingObject(binding);
			if (!GameObject.Validate(exact) && LoadZone)
			{
				TryExactZone(binding.ZoneId, true, out Zone ignoredBindingZone);
				exact = KingdomResidents.FindExactBindingObject(binding);
				if (!GameObject.Validate(exact))
				{
					TryExactZone(Row.SourceZoneId, true, out Zone ignoredSourceZone);
					exact = KingdomResidents.FindExactBindingObject(binding);
				}
				if (!GameObject.Validate(exact))
				{
					TryExactZone(Row.DestZoneId, true, out Zone ignoredDestZone);
					exact = KingdomResidents.FindExactBindingObject(binding);
				}
			}
			if (!GameObject.Validate(exact)
				&& GameObject.Validate(GameObject.FindByID(binding.ObjectId)))
				return BoundBodyState.Ambiguous;
			if (!GameObject.Validate(exact))
			{
				ZoneId = binding.ZoneId;
				return CandidateZonesAvailable(binding, Row)
					? BoundBodyState.Missing : BoundBodyState.Unreachable;
			}
			if (!string.Equals(exact.IDIfAssigned, binding.ObjectId, StringComparison.Ordinal)
				|| KingdomResidents.IdOf(exact) != Row.SubjectId || exact.CurrentCell == null
				|| exact.CurrentZone == null || !ReferenceEquals(exact.CurrentCell.ParentZone,
					exact.CurrentZone))
				return BoundBodyState.Ambiguous;

			string actualZone = exact.CurrentZone.ZoneID;
			bool transactionZone = string.Equals(actualZone, binding.ZoneId, StringComparison.Ordinal)
				|| string.Equals(actualZone, Row.SourceZoneId, StringComparison.Ordinal)
				|| string.Equals(actualZone, Row.DestZoneId, StringComparison.Ordinal);
			bool ledHere = (exact.IsPlayer() || exact.IsPlayerLed())
				&& ReferenceEquals(exact.CurrentZone, The.Player?.CurrentZone);
			if (!transactionZone && !ledHere) return BoundBodyState.Ambiguous;

			KingdomCityBook book;
			int locatedId;
			KingdomCityState state;
			KingdomResidentRow resident;
			int residentIndex;
			if (!KingdomResidents.TryLocate(System, exact, out book, out locatedId)
				|| locatedId != Row.SubjectId || book == null || !book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(Row.SubjectId, out residentIndex)
				|| !state.TryResident(residentIndex, out resident)
				|| (resident.Standing != KingdomResidentStanding.Resident
					&& resident.Standing != KingdomResidentStanding.Expedition)
				|| (!string.IsNullOrEmpty(resident.BoundZoneId)
					&& !string.Equals(resident.BoundZoneId, binding.ZoneId, StringComparison.Ordinal)
					&& !string.Equals(resident.BoundZoneId, Row.SourceZoneId, StringComparison.Ordinal)
					&& !string.Equals(resident.BoundZoneId, Row.DestZoneId, StringComparison.Ordinal)))
				return BoundBodyState.Ambiguous;

			int jobMarker = exact.GetIntProperty(ResidentJobProperty);
			if (jobMarker != 0 && jobMarker != Row.JobId) return BoundBodyState.Ambiguous;
			if (KingdomExpeditionRules.IsDispatched(Row.OriginCode) && jobMarker != Row.JobId)
			{
				// Return recovery may have moved/rebound the exact body home and cleared its marker
				// before the resident-row/close publishes. That monotone state is the sole zero-marker
				// exception for a dispatched row.
				bool returningHome = string.Equals(actualZone, Row.SourceZoneId,
					StringComparison.Ordinal)
					&& (string.Equals(binding.ZoneId, Row.SourceZoneId, StringComparison.Ordinal)
						|| string.Equals(resident.BoundZoneId, Row.SourceZoneId,
							StringComparison.Ordinal));
				if (!returningHome) return BoundBodyState.Ambiguous;
			}
			Body = exact;
			ZoneId = actualZone;
			if (!exact.IsAlive) return BoundBodyState.Dead;
			if (exact.IsPlayer() || exact.IsPlayerLed()) return BoundBodyState.Led;
			return BoundBodyState.Alive;
		}

		private static bool CandidateZonesAvailable(KingdomBinding Binding, KingdomJobRow Row)
		{
			return ExactZoneAvailable(Binding.ZoneId)
				&& ExactZoneAvailable(Row.SourceZoneId)
				&& ExactZoneAvailable(Row.DestZoneId);
		}

		private static bool ExactZoneAvailable(string ZoneId)
		{
			if (string.IsNullOrEmpty(ZoneId) || The.ZoneManager?.CachedZones == null) return false;
			Zone zone;
			return The.ZoneManager.CachedZones.TryGetValue(ZoneId, out zone) && zone != null;
		}

		private static bool TryExactZone(string ZoneId, bool LoadZone, out Zone Zone)
		{
			Zone = null;
			if (string.IsNullOrEmpty(ZoneId) || The.ZoneManager == null) return false;
			if (The.ZoneManager.CachedZones != null
				&& The.ZoneManager.CachedZones.TryGetValue(ZoneId, out Zone) && Zone != null)
				return true;
			if (!LoadZone) return false;
			try { Zone = The.ZoneManager.GetZone(ZoneId); }
			catch { Zone = null; }
			return Zone != null;
		}

		private static Cell SafeCell(Zone Zone)
		{
			if (Zone == null) return null;
			Cell best = null;
			int bestScore = int.MaxValue;
			int cx = Zone.Width / 2;
			int cy = Zone.Height / 2;
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !cell.IsPassable() || !cell.IsEmptyOfSolid()
						|| cell.HasOpenLiquidVolume()) continue;
					int score = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
					if (score < bestScore)
					{
						best = cell;
						bestScore = score;
					}
				}
			}
			return best;
		}

		private static bool MoveExact(GameObject Body, Cell Target)
		{
			if (!GameObject.Validate(Body) || !Body.IsAlive || Target == null) return false;
			if (ReferenceEquals(Body.CurrentCell, Target)) return true;
			Zone before = Body.CurrentZone;
			try
			{
				bool moved = Body.SystemLongDistanceMoveTo(Target, 0, forced: true, ignoreCombat: true)
					&& ReferenceEquals(Body.CurrentCell, Target);
				if (moved && !ReferenceEquals(before, Body.CurrentZone))
				{
					KingdomSurvey.ObserveRemovedFromActive(before, Body);
					KingdomSurvey.ObserveAddedToActive(Body.CurrentZone, Body);
				}
				return moved;
			}
			catch
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(before, Body);
				return false;
			}
		}

		private static bool EnsureReward(GameObject Body, KingdomJobRow Row)
		{
			if (Row.CargoAmount <= 0) return true;
			if (!GameObject.Validate(Body) || Body.Inventory == null) return false;
			GameObject found = null;
			foreach (GameObject item in Body.Inventory.GetObjects())
			{
				if (item.GetIntProperty(RewardJobProperty) != Row.JobId) continue;
				if (found != null) return false;
				found = item;
			}
			if (found != null) return found.Count == Row.CargoAmount;
			GameObject reward = GameObject.Create(
				KingdomMaterials.MaterialBlueprints[(int)KingdomMaterial.Scrap]);
			if (!GameObject.Validate(reward)) return false;
			reward.Count = Row.CargoAmount;
			reward.SetIntProperty(RewardJobProperty, Row.JobId);
			try
			{
				Body.Inventory.AddObject(reward, null, Silent: true, NoStack: true);
			}
			catch { }
			if (reward.InInventory == Body && reward.Count == Row.CargoAmount)
			{
				KingdomSurvey.ObserveChangedInActive(Body.CurrentZone, Body);
				return true;
			}
			if (GameObject.Validate(reward) && reward.InInventory == null && reward.CurrentCell == null)
				reward.Obliterate(null, Silent: true);
			return false;
		}

		private static void ConsumeRemainingProvisions(GameObject Body, int JobId)
		{
			if (!GameObject.Validate(Body) || Body.Inventory == null) return;
			List<GameObject> items = new List<GameObject>(Body.Inventory.GetObjects());
			for (int i = 0; i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.GetIntProperty(ProvisionJobProperty) != JobId)
					continue;
				while (GameObject.Validate(item) && item.Count > 0)
				{
					int before = item.Count;
					try { item.Destroy(null, Silent: true); }
					catch { break; }
					if (GameObject.Validate(item) && item.Count >= before) break;
				}
			}
			KingdomSurvey.ObserveChangedInActive(Body.CurrentZone, Body);
		}

		private static int SkillBonus(GameObject Body)
		{
			int bonus = 0;
			if (Body.HasSkill("Survival_RuinsSurvival")) bonus += 10;
			if (Body.HasSkill("Survival_Trailblazer")) bonus += 5;
			if (Body.HasSkill("Tinkering") || Body.HasSkill("Tinkering_Tinker1")
				|| Body.HasSkill("Tinkering_Tinker2")) bonus += 10;
			return (bonus > KingdomExpeditionRules.MaxSkillBonus)
				? KingdomExpeditionRules.MaxSkillBonus : bonus;
		}

		private static string ResultLine(KingdomJobRow Row, KingdomExpeditionOutcome Outcome,
			long Tick)
		{
			string who = ShownName(Row.SubjectName, "Resident " + Row.SubjectId);
			string where = ShownName(Row.TargetName, Row.DestZoneId);
			string date = DateAt(Tick);
			switch (Outcome)
			{
			case KingdomExpeditionOutcome.RichFind:
			case KingdomExpeditionOutcome.ModestFind:
				return "{{G|On " + date + ", " + who + " returned from " + where + " with "
					+ Row.CargoAmount + ((Row.CargoAmount == 1) ? " piece" : " pieces") + " of scrap.}}";
			case KingdomExpeditionOutcome.PickedClean:
				return "{{K|On " + date + ", " + who + " returned from " + where
					+ "; the site had already been picked clean.}}";
			case KingdomExpeditionOutcome.Cancelled:
				return "{{K|On " + date + ", " + who + " was recalled from " + where
					+ "; the dated dispatch receipt remained the only charge.}}";
			case KingdomExpeditionOutcome.ResidentDiedOnGround:
				return "{{r|On " + date + ", " + who + " was found dead at " + where
					+ "; the commission ended there.}}";
			case KingdomExpeditionOutcome.ResidentMissingFromBoundGround:
				return "{{r|On " + date + ", " + who + " was not found on the ground their binding named at "
					+ where + "; the roll records them astray, not dead.}}";
			default:
				return "{{K|On " + date + ", " + who + " joined the founder before the commission from "
					+ where + " could be completed.}}";
			}
		}

		private static string ChronicleLine(KingdomJobRow Row, KingdomExpeditionOutcome Outcome,
			long Tick)
		{
			string plain = ResultLine(Row, Outcome, Tick).Replace("{{G|", "")
				.Replace("{{K|", "").Replace("{{r|", "").Replace("}}", "");
			if (plain.EndsWith(".", StringComparison.Ordinal))
				plain = plain.Substring(0, plain.Length - 1);
			return plain;
		}

		private static string DateAt(long Tick)
		{
			long safe = (Tick < 0L) ? 0L : Tick;
			return XRL.World.Calendar.GetDay(safe) + " of " + XRL.World.Calendar.GetMonth(safe)
				+ ", " + XRL.World.Calendar.GetYear(safe) + " AR";
		}

		private static string SafeName(string Value, string Fallback)
		{
			string value = string.IsNullOrEmpty(Value) ? Fallback : Value;
			if (string.IsNullOrEmpty(value)) value = "unnamed ground";
			value = value.Replace('\r', ' ').Replace('\n', ' ');
			return (value.Length <= 160) ? value : value.Substring(0, 160);
		}

		/// <summary>Persisted job/journal names are plain; only this sink projection is rich.</summary>
		private static string ShownName(string Value, string Fallback)
		{
			return KingdomPresentation.Rich(SafeName(Value, Fallback));
		}

		private static bool Refuse(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

		/// <summary>Builds a read-only physical receipt. IDs may be assigned here, but no water or
		/// food moves until the prepared realm row and this encoded receipt are both durable.</summary>
		private static bool TryPrepareDebitReceipt(KingdomSurvey Survey, KingdomWaterDebit Water,
			int JobId, string SourceZoneId, int WaterCost, int ProvisionCost,
			out KingdomExpeditionDebitReceipt Receipt, out string Encoded, out string Failure)
		{
			Receipt = null;
			Encoded = null;
			Failure = null;
			if (Survey == null || Water == null || JobId <= 0 || WaterCost <= 0
				|| ProvisionCost <= 0 || Survey.FoodStored < ProvisionCost)
				return Refuse("Dedicated stores no longer cover the exact quote.", out Failure);
			KingdomWaterDebitLeg[] described;
			if (!Water.TryDescribe(out described) || described.Length <= 0)
				return Refuse("The exact water reservation could not expose a bounded receipt.", out Failure);
			KingdomExpeditionWaterLeg[] water = new KingdomExpeditionWaterLeg[described.Length];
			for (int i = 0; i < described.Length; i++)
			{
				GameObject owner = described[i].Owner;
				string ownerId = GameObject.Validate(owner) ? owner.ID : null;
				if (string.IsNullOrEmpty(ownerId)
					|| ownerId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars)
					return Refuse("A dedicated water vessel lacks a bounded persistent identity.", out Failure);
				water[i] = new KingdomExpeditionWaterLeg(ownerId, described[i].BeforeVolume,
					described[i].AfterVolume, described[i].MaxVolume);
			}

			List<KingdomExpeditionProvisionLeg> provisions =
				new List<KingdomExpeditionProvisionLeg>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
			int remaining = ProvisionCost;
			for (int i = 0; i < Survey.Larders.Count && remaining > 0; i++)
			{
				GameObject larder = Survey.Larders[i];
				if (!GameObject.Validate(larder) || larder.Inventory == null
					|| larder.GetIntProperty("KingdomLarder") != 1) continue;
				string larderId = larder.ID;
				if (string.IsNullOrEmpty(larderId)
					|| larderId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars) continue;
				List<GameObject> items = new List<GameObject>(larder.Inventory.GetObjects());
				for (int j = 0; j < items.Count && remaining > 0; j++)
				{
					GameObject item = items[j];
					if (!GameObject.Validate(item) || !seen.Add(item) || item.InInventory != larder
						|| item.Count <= 0 || item.GetIntProperty(ProvisionJobProperty) != 0
						|| (!item.HasPart("Food")
							&& !item.HasPart("PreparedCookingIngredient"))) continue;
					string itemId = item.ID;
					if (string.IsNullOrEmpty(itemId)
						|| itemId.Length > KingdomExpeditionDebitReceipt.MaxIdentityChars
						|| !seenIds.Add(itemId)) continue;
					int take = (item.Count < remaining) ? item.Count : remaining;
					provisions.Add(new KingdomExpeditionProvisionLeg(larderId, itemId,
						item.Count, item.Count - take));
					remaining -= take;
				}
			}
			if (remaining != 0)
				return Refuse("The larders cannot bind every quoted provision to one exact stack.", out Failure);
			if (!KingdomExpeditionDebitReceipt.TryCreate(JobId, SourceZoneId, WaterCost,
				ProvisionCost, water, provisions.ToArray(), out Receipt)
				|| !Receipt.TryEncode(out Encoded))
				return Refuse("The exact debit receipt exceeds its fixed identity or size bounds.", out Failure);
			return true;
		}

		private static bool TryApplyPreparedDebit(KingdomSystem System, KingdomJobRow Row,
			GameObject Body, KingdomExpeditionDebitReceipt Receipt, KingdomWaterDebit ReservedWater,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Body)
				|| !KingdomExpeditionDebitReceipt.TryDecode(
					Body.GetStringProperty(DebitReceiptProperty), out KingdomExpeditionDebitReceipt held)
				|| held.JobId != Row.JobId)
				return Refuse("The exact body no longer holds this job's debit receipt.", out Failure);
			Zone source;
			try { source = The.ZoneManager.GetZone(Receipt.SourceZoneId); }
			catch { return Refuse("The debit receipt's source ground cannot be thawed.", out Failure); }
			if (!TryApplyProvisionReceipt(source, Row.JobId, Receipt, out Failure)) return false;
			if (ReservedWater != null && WaterAllBefore(source, Receipt))
			{
				if (!MarkWaterReceipt(source, Row.JobId, Receipt, out Failure)) return false;
				if (!ReservedWater.Commit())
					return Refuse("The exact water callback did not complete; its durable receipt remains open for CAS recovery.", out Failure);
			}
			if (!TryApplyWaterReceipt(source, Row.JobId, Receipt, out Failure)) return false;
			return true;
		}

		private static bool TryApplyProvisionReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Receipt.ProvisionLegCount; i++)
			{
				KingdomExpeditionProvisionLeg leg;
				if (!Receipt.TryProvisionLeg(i, out leg))
					return Refuse("A provision receipt leg is absent.", out Failure);
				GameObject larder = FindZoneObject(Source, leg.LarderId);
				if (!GameObject.Validate(larder) || larder.Inventory == null
					|| larder.GetIntProperty("KingdomLarder") != 1)
					return Refuse("A receipt-bound larder is missing; no replacement stack was charged.", out Failure);
				GameObject item = FindInventoryObject(larder, leg.ItemId);
				bool present = GameObject.Validate(item) && item.InInventory == larder;
				int current = present ? item.Count : 0;
				int remaining;
				if (!KingdomExpeditionRules.TryDebitProgress(leg.BeforeCount, leg.AfterCount,
					present, current, out remaining))
					return Refuse("A receipt-bound provision stack left its exact before/after range; it was not charged again.", out Failure);
				if (!present) continue;
				int marker = item.GetIntProperty(ProvisionJobProperty);
				if (marker != 0 && marker != JobId)
					return Refuse("A provision stack belongs to another durable receipt.", out Failure);
				if (remaining > 0)
				{
					item.SetIntProperty(ProvisionJobProperty, JobId);
					while (remaining > 0)
					{
						int before = item.Count;
						try { item.Destroy(null, Silent: true); }
						catch
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(Source, larder);
							return Refuse("A provision callback stopped; the exact partial count remains recoverable.", out Failure);
						}
						KingdomSurvey.ObserveChangedInActive(Source, larder);
						present = GameObject.Validate(item) && item.InInventory == larder;
						current = present ? item.Count : 0;
						if (current != before - 1
							|| !KingdomExpeditionRules.TryDebitProgress(leg.BeforeCount,
								leg.AfterCount, present, current, out remaining))
							return Refuse("A provision callback left an unexpected count; no second stack was touched.", out Failure);
					}
				}
			}
			return true;
		}

		private static bool MarkWaterReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				if (!Receipt.TryWaterLeg(i, out leg)) return false;
				GameObject owner = FindZoneObject(Source, leg.OwnerId);
				LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
				if (!GameObject.Validate(owner) || vessel == null
					|| owner.GetIntProperty("KingdomStores") != 1
					|| vessel.MaxVolume != leg.MaxVolume)
					return Refuse("A receipt-bound water vessel is missing or changed.", out Failure);
				int marker = owner.GetIntProperty(WaterJobProperty);
				if (marker != 0 && marker != JobId)
					return Refuse("A water vessel belongs to another durable receipt.", out Failure);
				owner.SetIntProperty(WaterJobProperty, JobId);
				owner.SetIntProperty(WaterAfterProperty, leg.AfterVolume);
			}
			return true;
		}

		private static bool WaterAllBefore(Zone Source, KingdomExpeditionDebitReceipt Receipt)
		{
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				if (!Receipt.TryWaterLeg(i, out leg)) return false;
				LiquidVolume vessel = FindZoneObject(Source, leg.OwnerId)?.GetPart<LiquidVolume>();
				if (vessel == null || vessel.Volume != leg.BeforeVolume) return false;
			}
			return true;
		}

		private static bool TryApplyWaterReceipt(Zone Source, int JobId,
			KingdomExpeditionDebitReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!MarkWaterReceipt(Source, JobId, Receipt, out Failure)) return false;
			for (int i = 0; i < Receipt.WaterLegCount; i++)
			{
				KingdomExpeditionWaterLeg leg;
				Receipt.TryWaterLeg(i, out leg);
				GameObject owner = FindZoneObject(Source, leg.OwnerId);
				LiquidVolume vessel = owner.GetPart<LiquidVolume>();
				bool present = vessel != null;
				int current = present ? vessel.Volume : 0;
				int remaining;
				if (!KingdomExpeditionRules.TryDebitProgress(leg.BeforeVolume, leg.AfterVolume,
					present, current, out remaining))
					return Refuse("A receipt-bound water volume left its exact before/after range; it was not charged again.", out Failure);
				while (remaining > 0)
				{
					if (!KingdomLiquids.HasFreshWater(vessel))
						return Refuse("A receipt-bound vessel no longer contains pure fresh water.", out Failure);
					int before = vessel.Volume;
					try { KingdomLiquids.Drain(vessel, remaining); }
					catch
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Source, owner);
						return Refuse("A water callback stopped; the exact partial volume remains recoverable.", out Failure);
					}
					KingdomSurvey.ObserveChangedInActive(Source, owner);
					current = vessel.Volume;
					if (current >= before
						|| !KingdomExpeditionRules.TryDebitProgress(leg.BeforeVolume,
							leg.AfterVolume, true, current, out remaining))
						return Refuse("A water callback left an unexpected volume; no second vessel was touched.", out Failure);
				}
			}
			return true;
		}

		private static GameObject FindZoneObject(Zone Zone, string ObjectId)
		{
			if (Zone == null || string.IsNullOrEmpty(ObjectId)) return null;
			GameObject found = null;
			foreach (GameObject candidate in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!string.Equals(candidate.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = candidate;
			}
			return found;
		}

		private static GameObject FindInventoryObject(GameObject Owner, string ObjectId)
		{
			if (!GameObject.Validate(Owner) || Owner.Inventory == null
				|| string.IsNullOrEmpty(ObjectId)) return null;
			GameObject found = null;
			foreach (GameObject item in Owner.Inventory.GetObjects())
			{
				if (!string.Equals(item.IDIfAssigned, ObjectId, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = item;
			}
			return found;
		}

		private static bool HasDebitMarker(Zone Zone, int JobId)
		{
			if (Zone == null || JobId <= 0) return false;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (item.GetIntProperty(WaterJobProperty) == JobId) return true;
				if (item.Inventory == null) continue;
				foreach (GameObject held in item.Inventory.GetObjects())
					if (held.GetIntProperty(ProvisionJobProperty) == JobId) return true;
			}
			return false;
		}

		private static void ClearDebitMarkers(KingdomJobRow Row)
		{
			Zone zone;
			try { zone = The.ZoneManager.GetZone(Row.SourceZoneId); }
			catch { return; }
			foreach (GameObject item in KingdomSurvey.ObjectsFor(zone))
			{
				if (item.GetIntProperty(WaterJobProperty) == Row.JobId)
				{
					item.RemoveIntProperty(WaterJobProperty);
					item.RemoveIntProperty(WaterAfterProperty);
				}
				if (item.Inventory == null) continue;
				foreach (GameObject held in item.Inventory.GetObjects())
					if (held.GetIntProperty(ProvisionJobProperty) == Row.JobId)
						held.RemoveIntProperty(ProvisionJobProperty);
			}
		}
	}
}
