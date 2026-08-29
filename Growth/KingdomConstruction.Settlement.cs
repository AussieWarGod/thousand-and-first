using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (Resolving || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID)
				|| !KingdomMaster.AutomaticWorkAllowed(System))
			{
				return;
			}
			Resolving = true;
			try
			{
				if (!KingdomPlots.RecoverFoundingHeart(System, Z))
				{
					KingdomLog.Log("construction: founding heart recovery requires inspection");
					return;
				}
				ReleaseTerminalInputRemaindersOnActiveZone(System, Z);
				if (!CaptureInputObservation(System, Z, Survey, out string observationFailure))
					KingdomLog.Log("construction input observation: " + observationFailure);
				// Freeze one real, named raising gang before any labour clock is read. Every
				// active root below consumes its own stamp; only the oldest selected root can
				// therefore spend these bodies in this pass.
				KingdomConstructionPresence.Assign(System, Survey);
				List<KingdomMaterialRules.KingdomYardStanding> yards =
					KingdomMaterials.YardsStanding(Z);
				List<KingdomConstructionJob> jobs;
				string fault;
				if (!TryRead(out jobs, out fault))
				{
					KingdomLog.Log("construction: " + fault);
					return;
				}
				string owner = OwnerOf(System);
				List<GameObject> plots = new List<GameObject>(Survey.PlotWorks);
				for (int i = 0; i < plots.Count; i++)
				{
					GameObject plot = plots[i];
					r_KingdomPlotWorks works = plot.GetPart<r_KingdomPlotWorks>();
					if (works == null) continue;
					string receipt = plot.GetStringProperty(ReceiptProperty);
					if (string.IsNullOrEmpty(receipt))
					{
						if (plot.GetIntProperty(KingdomPlots.PlotWorkSchemaProperty)
							== KingdomPlotLabourRules.LegacySchema)
							KingdomPlots.Advance(works, System, The.Game.TimeTicks,
								KingdomPlotLabourWindowRules.InfrastructureReady, null);
						else KingdomPlots.ConsumePlotLabourAtZero(works, System,
							The.Game.TimeTicks, "Current plot labour has no paid receipt authority.");
						continue;
					}
					KingdomConstructionJob labourJob = null;
					for (int j = 0; j < jobs.Count; j++)
					{
						KingdomConstructionJob carried = jobs[j];
						if (carried.Id == receipt && carried.OwnerKey == owner
							&& carried.ZoneId == Z.ZoneID) labourJob = carried;
					}
					string authorityFailure = null;
					if (labourJob == null || !TryPlotLabourAuthority(System, Z, plot,
						works, labourJob, out authorityFailure))
					{
						KingdomPlots.ConsumePlotLabourAtZero(works, System, The.Game.TimeTicks,
							authorityFailure ?? "Plot labour authority could not be proved.");
					}
					else
					{
						int infrastructure = PlotInfrastructurePercent(plot, works, labourJob,
							yards, out string infrastructureFailure);
						KingdomPlots.Advance(works, System, The.Game.TimeTicks,
							infrastructure, infrastructureFailure);
					}
				}

				// Plot completion may have updated a row. Never dispatch the stale pre-advance copy.
				if (!TryRead(out jobs, out fault))
				{
					KingdomLog.Log("construction: " + fault);
					return;
				}
				for (int i = 0; i < jobs.Count; i++)
				{
					KingdomConstructionJob job = jobs[i];
					if (job.OwnerKey != owner)
					{
						continue;
					}
					bool targetHere = job.ZoneId == Z.ZoneID;
					bool inputDriven = false;
					if (!string.IsNullOrEmpty(job.InputReceipt)
						&& KingdomConstructionRules.TryGetInputReceipt(job,
							out KingdomConstructionInputReceipt routed)
						&& InputReceiptTouchesZone(routed, Z.ZoneID))
					{
						inputDriven = true;
						DriveRoutedInput(System, Z, ref job, out fault);
						if (!TryFind(job.Id, out job) || !targetHere
							|| job.Phase != KingdomConstructionPhase.Funded) continue;
					}
					else if (!targetHere) continue;
					if (KingdomConstructionRules.IsTerminal(job.Phase))
					{
						if (job.Compacted) continue;
						// Every complete route gets one physical-only inspection so a save between
						// Complete and outbox publication can reconstruct route-owned frozen content.
						if (job.Phase == KingdomConstructionPhase.Complete)
							InspectProjection(System, Z, job);
						if (!TryFind(job.Id, out job) || job.Compacted) continue;
						if (job.Phase == KingdomConstructionPhase.Complete && job.Outbox == null
							&& job.Route == KingdomConstructionRoute.RoadPaving)
							KingdomCeremony.EnsureRoadPavedFromReceipt(System, ref job);
						else if (job.Phase != KingdomConstructionPhase.Complete && job.Outbox == null)
							KingdomCeremony.EnsureTerminalClosed(System, ref job);
						if (TryFind(job.Id, out job) && !job.Compacted && job.Outbox != null
							&& !KingdomConstructionRules.OutboxSettled(job.Outbox))
							KingdomCeremony.DispatchPending(System, ref job);
						continue;
					}
					if (!inputDriven && !string.IsNullOrEmpty(job.InputReceipt))
					{
						DriveRoutedInput(System, Z, ref job, out fault);
						if (!TryFind(job.Id, out job)
							|| job.Phase != KingdomConstructionPhase.Funded) continue;
					}
					KingdomConstructionResumeAction action = KingdomConstructionRules.ResumeAction(job);
					if (action == KingdomConstructionResumeAction.ResumeFunding)
					{
						if (KingdomConstructionRules.RequiresBuildTruth(job.Route)
							&& !KingdomConstructionRules.TryReadBuildTruth(job,
								out _, out _, out _))
						{
							Quarantine(ref job,
								"This legacy construction receipt predates frozen build effects; its original catalogue and founder-skill truth cannot be reconstructed.");
							continue;
						}
						KingdomConstructionStartResult resumed;
						if (KingdomPurpose.RequiresExactFunding(job))
						{
							if (!KingdomPurpose.TryRequiredFundingItems(Z, job,
								out List<GameObject> requiredItems, out fault)
								|| !KingdomPurpose.TryRequiredFundingObjectIds(job,
									out List<string> requiredIds, out fault))
							{
								Quarantine(ref job, fault
									?? "The exact city-purpose cargo set cannot be reproved for funding retry.");
								continue;
							}
							resumed = TryResumeRoutedFunding(job, requiredIds,
								out job, out fault);
						}
						else resumed = TryResumeFunding(job, Z, Survey,
							null, out job, out fault);
						if (resumed != KingdomConstructionStartResult.Funded) continue;
						action = KingdomConstructionResumeAction.RetryProjection;
					}
					if (action == KingdomConstructionResumeAction.RetryProjection)
					{
						RetryProjection(System, Z, job);
					}
					else if (action == KingdomConstructionResumeAction.Inspect
						&& (job.Phase == KingdomConstructionPhase.WaterPending
							|| job.Phase == KingdomConstructionPhase.MaterialPending))
					{
						string diagnostic = KingdomConstructionRules.InterruptedFundingDiagnostic(job.Phase);
						TransitionAndPublish(ref job, KingdomConstructionPhase.InspectionRequired,
							diagnostic, out fault);
					}
					else if (action == KingdomConstructionResumeAction.AdvanceWork
						|| (action == KingdomConstructionResumeAction.Inspect
							&& job.Phase == KingdomConstructionPhase.ProjectionPending))
					{
						InspectProjection(System, Z, job);
					}
				}
			}
			finally
			{
				if (!CaptureInputObservation(System, Z, Survey, out string observationFailure))
					KingdomLog.Log("construction input observation refresh: " + observationFailure);
				Resolving = false;
				KingdomConstructionPresence.ReleaseFinished(Z, Survey);
				KingdomVisualState.Refresh(System, Z, Survey);
			}
		}

		private static void RetryProjection(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			switch (Job.Route)
			{
			case KingdomConstructionRoute.CommissionScaffold:
				KingdomCommission.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlanScaffold:
				KingdomPlanMarker.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlotCommission:
			case KingdomConstructionRoute.PlotPlan:
				KingdomPlots.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.SocketBuild:
			case KingdomConstructionRoute.SocketConvert:
			case KingdomConstructionRoute.SocketRedress:
				KingdomSocket.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Improvement:
				KingdomUpgrade.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.RoadPaving:
				KingdomRoads.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.WearRepair:
				KingdomWear.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Strike:
				KingdomMaterials.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PurposeConsignment:
				KingdomPurpose.RetryConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.HostedArcology:
				KingdomHostedArcology.RetryConstruction(System, Z, Job);
				break;
			}
		}

		private static void InspectProjection(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			switch (Job.Route)
			{
			case KingdomConstructionRoute.CommissionScaffold:
				KingdomCommission.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlanScaffold:
				KingdomPlanMarker.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PlotCommission:
			case KingdomConstructionRoute.PlotPlan:
				KingdomPlots.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.SocketBuild:
			case KingdomConstructionRoute.SocketConvert:
			case KingdomConstructionRoute.SocketRedress:
				KingdomSocket.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Improvement:
				KingdomUpgrade.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.RoadPaving:
				KingdomRoads.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.WearRepair:
				KingdomWear.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.Strike:
				KingdomMaterials.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.PurposeConsignment:
				KingdomPurpose.InspectConstruction(System, Z, Job);
				break;
			case KingdomConstructionRoute.HostedArcology:
				KingdomHostedArcology.InspectConstruction(System, Z, Job);
				break;
			}
		}
	}
}
