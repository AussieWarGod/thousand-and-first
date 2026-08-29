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
				|| survey.FoodAvailable < target.Quote.Provisions)
			{
				Popup.Show("The dedicated stores on this ground hold "
					+ ((survey == null) ? 0 : survey.StoredWater) + " drams and "
					+ ((survey == null) ? 0 : survey.FoodAvailable) + " available provisions; this commission needs exactly "
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

	}
}
