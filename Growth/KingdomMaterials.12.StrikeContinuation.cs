using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{
		private static void ContinueStrike(KingdomSystem System, Zone Z,
			GameObject Building, KingdomConstructionJob Job)
		{
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| !KingdomConstructionRules.TryDecodeStrikeIntent(Job.PhysicalReceipt,
					out KingdomStrikeIntent intent) || intent.Targets == null
				|| intent.Effort <= 0)
			{
				QuarantineStrike(Job, "The strike's frozen physical receipt is absent or stale.");
				return;
			}
			for (int step = 0; step < 512; step++)
			{
				if (Job.PhysicalPhase == KingdomPhysicalPhase.Quarantined
					|| Job.Phase == KingdomConstructionPhase.InspectionRequired) return;
				if (Job.PhysicalPhase == KingdomPhysicalPhase.StrikeWorkComplete)
				{
					if (!KingdomMirrorGate.TryPreflightRemoval(Building, Z, out _)) return;
					if (!ValidateFrozenStrikeTargets(Z, intent, Job.SourceId,
						Job.PhysicalIndex, out GameObject plotPart, out string targetFailure))
					{
						QuarantineStrike(Job, targetFailure);
						return;
					}
					if (Job.PhysicalIndex < intent.Targets.Count)
					{
						RemoveStrikePlotPart(Z, intent, plotPart, ref Job);
						if (Job.PhysicalPhase != KingdomPhysicalPhase.StrikeWorkComplete) return;
						continue;
					}
					if (!KingdomDelveLink.TryFinishStrike(Building, Z,
						out string linkFailure))
					{
						if (!string.IsNullOrEmpty(Building.GetStringProperty(
							KingdomDelveLink.FaultProperty)))
							QuarantineStrike(Job, linkFailure);
						else KingdomLog.Log("delve link: strike waits: " + linkFailure);
						return;
					}
					if (!KingdomMirrorGate.TryPreflightRemoval(Building, Z, out _)) return;
					if (!ValidateFrozenStrikeTargets(Z, intent, Job.SourceId,
						Job.PhysicalIndex, out _, out targetFailure))
					{
						QuarantineStrike(Job, targetFailure);
						return;
					}
					RemoveStrikePredecessor(Z, Building, intent, ref Job);
					if (Job.PhysicalPhase != KingdomPhysicalPhase.PredecessorRemoved) return;
				}
				else if (Job.PhysicalPhase == KingdomPhysicalPhase.PlotPartRemovalPending)
				{
					// No durable callback-success tombstone was written. FindByID null cannot
					// distinguish removal from an unloaded/moved exact target.
					QuarantineStrike(Job,
						"Plot-part removal was interrupted before exact callback-success proof.");
					return;
				}
				else if (Job.PhysicalPhase == KingdomPhysicalPhase.PredecessorRemovalPending)
				{
					QuarantineStrike(Job,
						"Strike predecessor removal was interrupted before exact callback-success proof.");
					return;
				}

				if (Job.PhysicalPhase == KingdomPhysicalPhase.PredecessorRemoved
					|| Job.PhysicalPhase == KingdomPhysicalPhase.SalvageAddPending)
				{
					bool networkStrike = KingdomGatehouseRules.IsNetworkStrike(intent.BuildKey,
						intent.HasPlot, intent.X1, intent.Y1, intent.X2, intent.Y2,
						intent.PlotId, intent.Targets.Count);
					bool sourceAbsent = networkStrike
						? KingdomGatehouse.LoadedIdentityAbsent(Z, Job.SourceId)
						: ExactObject(Job.SourceId) == null;
					if (!sourceAbsent)
					{
						QuarantineStrike(Job, "Salvage was blocked because the exact predecessor still exists.");
						return;
					}
					KingdomDelve.OnStruck(intent.BuildKey, Z.ZoneID);
					if (!ContinueStrikeSalvage(Z, intent, ref Job)) return;
					if (Job.PhysicalPhase != KingdomPhysicalPhase.SalvageSettled) continue;
				}

				if (Job.PhysicalPhase == KingdomPhysicalPhase.SalvageSettled
					|| Job.PhysicalPhase == KingdomPhysicalPhase.SuccessorPending)
				{
					bool fresh = Job.PhysicalPhase == KingdomPhysicalPhase.SalvageSettled;
					if (fresh && !KingdomConstruction.UpdatePhysical(ref Job,
						KingdomPhysicalPhase.SuccessorPending, Job.PhysicalIndex, 0,
						Job.PhysicalSpilled, null, null, Job.PhysicalReceipt)) return;
					if (!KingdomSocket.ResumeStrikeSuccessor(System, Z, intent, fresh,
						ref Job, out _, out string successorFailure))
					{
						QuarantineStrike(Job, successorFailure
							?? "The strike successor is absent or ambiguous.");
						return;
					}
					if (!KingdomConstruction.UpdatePhysical(ref Job,
						KingdomPhysicalPhase.SuccessorSettled, Job.PhysicalIndex, 0,
						Job.PhysicalSpilled, null, null, Job.PhysicalReceipt)) return;
				}

				if (Job.PhysicalPhase == KingdomPhysicalPhase.SuccessorSettled
					|| Job.PhysicalPhase == KingdomPhysicalPhase.TellingsPending)
				{
					SettleStrikeTellings(System, intent, ref Job);
					return;
				}
				if (Job.PhysicalPhase == KingdomPhysicalPhase.Settled) return;
			}
		}

		private static bool ValidateFrozenStrikeTargets(Zone Z, KingdomStrikeIntent Intent,
			string SourceId, int Index, out GameObject Current, out string Failure)
		{
			Current = null;
			Failure = null;
			if (Z == null || Intent == null || Intent.Targets == null || Index < 0
				|| Index > Intent.Targets.Count)
			{
				Failure = "The frozen strike target index is invalid.";
				return false;
			}
			bool networkStrike = KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey,
				Intent.HasPlot, Intent.X1, Intent.Y1, Intent.X2, Intent.Y2,
				Intent.PlotId, Intent.Targets.Count);
			if (networkStrike && !KingdomGatehouse.TryStrikeReceipt(Z, Intent, out _))
			{
				Failure = "The gatehouse strike root or six exact target receipts changed.";
				return false;
			}
			if (!Intent.HasPlot && !networkStrike)
			{
				if (Intent.Targets.Count == 0 && Index == 0) return true;
				Failure = "A non-plot strike carries plot-part targets.";
				return false;
			}
			HashSet<string> remaining = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Intent.Targets.Count; i++)
			{
				KingdomStrikeTarget target = Intent.Targets[i];
				if (i < Index)
				{
					bool reappeared = networkStrike
						? !KingdomGatehouse.LoadedIdentityAbsent(Z, target.Id)
						: GameObject.Validate(ExactObject(target.Id));
					if (reappeared)
					{
						Failure = "A proved-removed strike target reappeared.";
						return false;
					}
					continue;
				}
				GameObject exact;
				bool exactTarget;
				if (networkStrike)
					exactTarget = KingdomGatehouse.TryResolveStrikeSatellite(Z,
						Intent.PlotId, i, target.Id, target.Blueprint,
						target.X, target.Y, out exact);
				else
				{
					exact = ExactObject(target.Id);
					exactTarget = GameObject.Validate(exact) && exact.IDIfAssigned != SourceId
						&& exact.CurrentZone == Z
						&& exact.CurrentCell == Z.GetCell(target.X, target.Y)
						&& exact.Blueprint == target.Blueprint
						&& exact.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
						&& exact.GetStringProperty(KingdomPlots.PlotIdProperty) == Intent.PlotId;
				}
				if (!remaining.Add(target.Id) || !GameObject.Validate(exact)
					|| exact.IDIfAssigned == SourceId || exact.CurrentZone != Z
					|| exact.CurrentCell != Z.GetCell(target.X, target.Y)
					|| exact.Blueprint != target.Blueprint
					|| !exactTarget)
				{
					Failure = "A frozen strike target was removed, moved, replaced, or changed.";
					return false;
				}
				if (i == Index) Current = exact;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			List<GameObject> candidates = networkStrike
				? survey.GatehouseSatellites : survey.PlotParts;
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject item = candidates[i];
				bool owned = networkStrike
					? KingdomGatehouse.IsOwnedSatellite(item, Intent.PlotId)
					: GameObject.Validate(item) && item.IDIfAssigned != SourceId
						&& item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
						&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == Intent.PlotId;
				if (owned && !remaining.Contains(item.IDIfAssigned))
				{
					Failure = "A new or replacement plot part entered the frozen strike footprint.";
					return false;
				}
			}
			return Index == Intent.Targets.Count || GameObject.Validate(Current);
		}

	}
}
