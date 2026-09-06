using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomWear
	{
		private const string SubsidenceRecoveryWait = "The fall waits for the exact saved wear and repair receipts of its works.";
		private const string SubsidenceConstructionWait = "The fall waits for an existing construction reservation; no new funding or construction was started.";
		private sealed class SubsidenceRecoveryWork
		{
			internal GameObject Body;
			internal string Id, Blueprint;
			internal Cell Cell;
			internal int X, Y;
			internal bool Selected;
		}
		private sealed class SubsidenceRecoveryFrame
		{
			internal XRLGame Game;
			internal KingdomSystem System;
			internal KingdomCityBook City;
			internal Zone Zone;
			internal KingdomSurvey Survey;
			internal string Wire, Realm, Settlement, ZoneId;
			internal long Token;
			internal readonly List<SubsidenceRecoveryWork> Works = new List<SubsidenceRecoveryWork>();
		}

		// Independent receipt owners finish their own calls. Only helper boundaries are reproved here;
		// success permits a fresh rung capture, never proves custody inside an owner's callbacks.
		internal static bool TryRecoverBeforeSubsidenceRung(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomSubsidenceStepBook expected, out string refusal)
		{
			refusal = SubsidenceRecoveryWait;
			try
			{
				if (!TrySubsidenceRecoveryFrame(system, zone, survey, expected, out SubsidenceRecoveryFrame frame)) return false;
				SubsidenceRecoveryWork gang = null;
				foreach (SubsidenceRecoveryWork work in frame.Works)
				{
					if (!ReadSubsidenceRecoveryJob(frame, work, out KingdomConstructionJob job)) return false;
					r_KingdomWear wear = work.Body.GetPart<r_KingdomWear>();
					if (gang == null && wear != null && wear.Wear > 0 && (wear.RepairEffortLeft > 0
						|| job != null && job.Route == KingdomConstructionRoute.WearRepair
							&& !KingdomConstructionRules.IsTerminal(job.Phase)
							&& KingdomConstructionRules.FullyFundedExact(job))) gang = work;
				}
				foreach (SubsidenceRecoveryWork work in frame.Works)
				{
					if (!work.Selected) continue;
					if (!SubsidenceRecoveryExact(frame, work) || !ReadSubsidenceRecoveryWear(work, out r_KingdomWear wear)) return false;
					if (wear != null)
					{
						if (!InvokeSubsidenceRecovery(frame, work, () => ResolveSafeReceipts(system, survey, work.Body))) return false;
						if (wear.IncidentPhase == (int)KingdomWearIncidentPhase.Complete
							&& !InvokeSubsidenceRecovery(frame, work, () => ApplyDamageIncident(system,
								work.Body, (KingdomWearRules.WearCause)wear.IncidentCause, wear.IncidentId))) return false;
						if (wear.LeakPhase == (int)KingdomWearLeakPhase.Bound
							&& !InvokeSubsidenceRecovery(frame, work, () => ContinueBoundLeak(system, survey, work.Body, wear))) return false;
					}
					if (!RecoverSubsidenceRepair(frame, work, out refusal)) return false;
				}
				if (gang != null)
				{
					if (!gang.Selected && !RecoverSubsidenceRepair(frame, gang, out refusal)) return false;
					if (!ReadSubsidenceRecoveryWear(gang, out r_KingdomWear wear)) return false;
					if (wear != null && wear.RepairEffortLeft > 0)
					{
						if (!Enabled) { refusal = "The fall waits for its paid repair; wear and repair are currently disabled."; return false; }
						if (!ReadSubsidenceRecoveryJob(frame, gang, out KingdomConstructionJob job)
							|| !SubsidencePaidRepair(frame, gang, job) || !SubsidenceReceiptsClear(wear)) return false;
						int hands = KingdomMaterialRules.FreeHands(system.Population, system.AssignedCrew);
						if (!InvokeSubsidenceRecovery(frame, gang,
							() => AdvanceRepair(system, gang.Body, wear, hands, The.Game.TimeTicks), true)) return false;
					}
				}
				foreach (SubsidenceRecoveryWork work in frame.Works)
				{
					if (!work.Selected) continue;
					if (!SubsidenceRecoveryExact(frame, work) || !ReadSubsidenceRecoveryWear(work, out r_KingdomWear wear)
						|| wear != null && (!SubsidenceReceiptsClear(wear) || wear.RepairEffortLeft != 0)) return false;
					if (!KingdomSubsidenceRungRuntime.ConstructionAvailable(work.Body))
					{ refusal = SubsidenceConstructionWait; return false; }
				}
				if (!SubsidenceRecoveryOwnerExact(frame)) return false;
				refusal = null; return true;
			}
			catch (Exception) { refusal = SubsidenceRecoveryWait; return false; }
		}

		private static bool RecoverSubsidenceRepair(SubsidenceRecoveryFrame frame,
			SubsidenceRecoveryWork work, out string refusal)
		{
			refusal = SubsidenceRecoveryWait;
			if (!ReadSubsidenceRecoveryJob(frame, work, out KingdomConstructionJob job)
				|| !ReadSubsidenceRecoveryWear(work, out r_KingdomWear wear)
				|| wear != null && !SubsidenceReceiptsClear(wear)) return false;
			if (job == null || KingdomConstructionRules.IsTerminal(job.Phase))
				return wear == null || wear.RepairEffortLeft == 0;
			if (!SubsidencePaidRepair(frame, work, job))
			{ refusal = SubsidenceConstructionWait; return false; }
			KingdomConstructionResumeAction action = KingdomConstructionRules.ResumeAction(job);
			if (action == KingdomConstructionResumeAction.Inspect || action == KingdomConstructionResumeAction.AdvanceWork)
			{
				if (!InvokeSubsidenceRecovery(frame, work, () => InspectConstruction(frame.System, frame.Zone, job), true)
					|| !ReadSubsidenceRecoveryJob(frame, work, out job)) return false;
			}
			if (job != null && KingdomConstructionRules.ResumeAction(job) == KingdomConstructionResumeAction.RetryProjection)
			{
				if (!SubsidencePaidRepair(frame, work, job)
					|| !InvokeSubsidenceRecovery(frame, work, () => RetryConstruction(frame.System, frame.Zone, job), true)) return false;
			}
			return SubsidenceRecoveryExact(frame, work);
		}

		private static bool SubsidencePaidRepair(SubsidenceRecoveryFrame frame,
			SubsidenceRecoveryWork work, KingdomConstructionJob job)
		{
			return job != null && job.Route == KingdomConstructionRoute.WearRepair
				&& !KingdomConstructionRules.IsTerminal(job.Phase) && KingdomConstructionRules.FullyFundedExact(job)
				&& job.X == work.X && job.Y == work.Y && RepairSubjectExact(frame.System, frame.Zone, work.Body, job);
		}

		private static bool InvokeSubsidenceRecovery(SubsidenceRecoveryFrame frame,
			SubsidenceRecoveryWork work, Action callback, bool allowRemovedWear = false)
		{
			if (!SubsidenceRecoveryExact(frame, work) || !ReadSubsidenceRecoveryWear(work, out r_KingdomWear before)) return false;
			bool exact = false;
			try { callback(); }
			finally
			{
				exact = SubsidenceRecoveryExact(frame, work) && ReadSubsidenceRecoveryWear(work, out r_KingdomWear after)
					&& (ReferenceEquals(before, after) || allowRemovedWear && before != null && after == null);
			}
			return exact;
		}

		private static bool ReadSubsidenceRecoveryWear(SubsidenceRecoveryWork work, out r_KingdomWear wear)
		{
			wear = work.Body.GetPart<r_KingdomWear>();
			int copies = 0;
			if (work.Body.PartsList != null)
				foreach (IPart part in work.Body.PartsList) if (part is r_KingdomWear) copies++;
			return copies == (wear == null ? 0 : 1) && (wear == null || wear.ParentObject == work.Body
				&& !wear.LifecycleQuarantined && wear.Wear >= 0 && wear.Wear <= KingdomMaterialRules.MaxWearPercent
				&& wear.RepairEffortLeft >= 0 && wear.IncidentPhase >= 0 && wear.IncidentPhase < (int)KingdomWearIncidentPhase.Quarantined
				&& wear.LeakPhase >= 0 && wear.LeakPhase < (int)KingdomWearLeakPhase.Quarantined);
		}

		private static bool SubsidenceReceiptsClear(r_KingdomWear wear)
		{
			return wear.IncidentPhase == (int)KingdomWearIncidentPhase.None && wear.LeakPhase == (int)KingdomWearLeakPhase.None;
		}

		private static bool ReadSubsidenceRecoveryJob(SubsidenceRecoveryFrame frame,
			SubsidenceRecoveryWork work, out KingdomConstructionJob job)
		{
			job = null;
			if (!SubsidenceRecoveryExact(frame, work)) return false;
			XRLGame game = frame.Game;
			if (game.StringGameState == null || game.IntGameState == null || game.Int64GameState == null
				|| game.ObjectGameState == null || game.BooleanGameState == null
				|| work.Body.HasIntProperty(KingdomConstruction.ReceiptProperty)) return false;
			string key = KingdomConstruction.RegistryStateKey;
			bool present = game.HasStringGameState(key);
			KingdomDurableKeyObservation row = new KingdomDurableKeyObservation {
				HasString = present, HasInt = game.HasIntGameState(key), HasInt64 = game.HasInt64GameState(key),
				HasObject = game.HasObjectGameState(key), HasBoolean = game.HasBooleanGameState(key),
				String = present ? game.GetStringGameState(key, null) : null };
			if (!KingdomScenarioStateShape.TryAuthorityText(row, out _, out _, out _)) return false;
			string receipt = work.Body.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (!string.IsNullOrEmpty(receipt)) KingdomConstruction.TryFind(receipt, out job);
			return SubsidenceRecoveryExact(frame, work);
		}

		private static bool TrySubsidenceRecoveryFrame(KingdomSystem system, Zone zone, KingdomSurvey survey,
			KingdomSubsidenceStepBook expected, out SubsidenceRecoveryFrame frame)
		{
			frame = null;
			XRLGame game = The.Game;
			if (game == null || system == null || system.City == null || zone == null || survey == null
				|| survey.Built == null || !KingdomSubsidenceStepCodec.TryEncode(expected, out string wire)
				|| expected.Active == null || expected.Active.Phase != KingdomSubsidenceStepPhase.Settling
				|| expected.Active.RungModel != KingdomSubsidenceStepRules.UnplannedRungs) return false;
			SubsidenceRecoveryFrame value = new SubsidenceRecoveryFrame { Game = game, System = system,
				City = system.City, Zone = zone, Survey = survey, Wire = wire, Realm = expected.RealmId,
				Settlement = expected.SettlementId, ZoneId = zone.ZoneID, Token = system.MasterAppliedResumeToken };
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (GameObject work in survey.Built)
			{
				if (!GameObject.Validate(work) || string.IsNullOrEmpty(work.IDIfAssigned)
					|| !ids.Add(work.IDIfAssigned) || work.CurrentCell == null) return false;
				value.Works.Add(new SubsidenceRecoveryWork { Body = work, Id = work.IDIfAssigned,
					Blueprint = work.Blueprint, Cell = work.CurrentCell, X = work.CurrentCell.X, Y = work.CurrentCell.Y,
					Selected = KingdomSubsidenceRules.RollRuin(expected.SettlementId, work.IDIfAssigned,
						(ulong)expected.Active.DueTick, expected.Active.FromStage) });
			}
			if (!SubsidenceRecoveryOwnerExact(value)) return false;
			frame = value; return true;
		}

		private static bool SubsidenceRecoveryOwnerExact(SubsidenceRecoveryFrame frame)
		{
			if (!ReferenceEquals(The.Game, frame.Game) || !ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)
				|| !frame.System.Founded || !ReferenceEquals(frame.System.City, frame.City)
				|| frame.System.CurrentRealmId != frame.Realm || frame.City.SettlementId != frame.Settlement
				|| KingdomChronicle.SettlementId(frame.System) != frame.Settlement
				|| frame.System.MasterAppliedResumeToken != frame.Token || frame.City.SubsidenceModel != frame.Wire
				|| !frame.City.HasValidSubsidenceStorage() || frame.Zone.ZoneID != frame.ZoneId
				|| frame.System.ClaimedZones == null || !frame.System.ClaimedZones.Contains(frame.ZoneId)
				|| frame.Survey.Ground != frame.Zone || KingdomSurvey.ActiveFor(frame.Zone) != frame.Survey
				|| frame.Survey.Built.Count != frame.Works.Count) return false;
			for (int i = 0; i < frame.Works.Count; i++)
				if (!ReferenceEquals(frame.Works[i].Body, frame.Survey.Built[i])) return false;
			return true;
		}

		private static bool SubsidenceRecoveryExact(SubsidenceRecoveryFrame frame, SubsidenceRecoveryWork work)
		{
			return SubsidenceRecoveryOwnerExact(frame) && GameObject.Validate(work.Body)
				&& work.Body.IDIfAssigned == work.Id && work.Body.Blueprint == work.Blueprint
				&& work.Body.CurrentZone == frame.Zone && work.Body.CurrentCell == work.Cell
				&& work.Cell.ParentZone == frame.Zone && work.Cell.X == work.X && work.Cell.Y == work.Y
				&& work.Body.InInventory == null && !KingdomSubsidenceRungRuntime.BlocksWork(work.Body);
		}
	}
}
