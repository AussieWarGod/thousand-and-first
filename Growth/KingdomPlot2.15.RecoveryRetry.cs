using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| (Job.Route != KingdomConstructionRoute.PlotCommission
					&& Job.Route != KingdomConstructionRoute.PlotPlan))
			{
				return;
			}
			if (!TryDecodePlotPayload(Job.Payload, out var rect, out var skin,
				out KingdomArchitectureIntent architecture, out bool legacy, out string payloadFailure)
				|| (!legacy && (architecture == null || architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacy && (Job.X != rect.CenterX || Job.Y != rect.CenterY)))
			{
				KingdomConstructionJob malformed = Job;
				KingdomConstruction.Quarantine(ref malformed, payloadFailure
					?? "The plot job no longer matches its frozen authored payload.");
				return;
			}
			if (!KingdomData.TryGetBuilding(Job.TargetKey, out var entry)
				|| !TryGetSpec(Job.TargetKey, out var spec)) return;
			if (Job.Route == KingdomConstructionRoute.PlotPlan)
			{
				GameObject marker;
				KingdomPhysicalLookupState markerState = KingdomConstruction.FindSubject(
					Z, Job, out marker);
				GameObject final;
				KingdomPhysicalLookupState finalState = FindConstructionResult(
					Z, Job, true, out final);
				if (markerState == KingdomPhysicalLookupState.Ambiguous
					|| finalState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstructionJob duplicate = Job;
					KingdomConstruction.Quarantine(ref duplicate,
						"A plot plan-marker or final ID is duplicated or malformed.");
					return;
				}
					if (finalState == KingdomPhysicalLookupState.Exact)
					{
						KingdomConstructionJob recovered = Job;
						if (recovered.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
						{
							RecoverPendingPlotRemoval(System, Z, final, ref recovered);
							return;
						}
					if (marker != null && marker != final
						&& marker.GetPart<r_KingdomPlanMarker>() != null)
					{
						if (!KingdomConstruction.BeginProjection(ref recovered, out _)) return;
						string markerId = marker.IDIfAssigned;
						bool removed;
						try { removed = marker.Destroy(null, Silent: true); }
						catch (System.Exception ex)
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
							KingdomConstruction.Quarantine(ref recovered,
								"Plot plan-marker removal threw: " + ex.Message);
							return;
						}
						if (removed && !GameObject.Validate(marker))
							KingdomSurvey.ObserveRemovedFromActive(Z, marker);
						// Destroy moves the exact object to the graveyard with all parts retained.
						// Callback success plus invalidity is the engine's exact tombstone.
					if (KingdomConstructionRules.ExactRemovalAction(true, removed,
						GameObject.Validate(marker), KingdomConstruction.FindExactId(
							Z, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
						!= KingdomExactRemovalAction.ProvedAbsent)
					{
						KingdomConstruction.Quarantine(ref recovered,
							"Completed-plot plan-marker removal was vetoed or remained valid.");
							return;
						}
						if (!TryProvePlotPlanMarkerRemoval(System, Z, final, true, markerId,
							ref recovered, out string markerFailure))
						{
							KingdomConstruction.Quarantine(ref recovered, markerFailure);
							return;
						}
					}
					string removedWorks = final.GetStringProperty(
						r_KingdomScaffold.RemovalProofProperty);
					if (!string.IsNullOrEmpty(removedWorks)
						&& recovered.SubjectId != removedWorks)
					{
						if (!HasPlotPlanMarkerRemovalProof(final, recovered.SubjectId))
						{
							KingdomConstruction.Quarantine(ref recovered,
								"Completed plot lacks exact plan-marker removal proof.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref recovered, removedWorks)) return;
					}
					if (!r_KingdomScaffold.HasRemovalProof(final, recovered.SubjectId))
					{
						KingdomConstruction.Quarantine(ref recovered,
							"Completed plot lacks exact works-removal proof.");
						return;
					}
					FinishPlotEffects(System, Z, final, ref recovered);
					return;
				}
				GameObject works;
				KingdomPhysicalLookupState worksState = FindConstructionResult(
					Z, Job, false, out works);
				if (worksState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstructionJob duplicate = Job;
					KingdomConstruction.Quarantine(ref duplicate,
						"The plot-works ID is duplicated or malformed.");
					return;
				}
				if (worksState == KingdomPhysicalLookupState.Exact
					&& works.GetPart<r_KingdomPlotWorks>() != null)
				{
					KingdomConstructionJob recovered = Job;
					if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
					{
						if (!KingdomConstruction.BeginProjection(ref recovered, out _)) return;
						string markerId = marker.IDIfAssigned;
						bool removed;
						try { removed = marker.Destroy(null, Silent: true); }
						catch (System.Exception ex)
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
							KingdomConstruction.Quarantine(ref recovered,
								"Plot plan-marker removal threw: " + ex.Message);
							return;
						}
						if (removed && !GameObject.Validate(marker))
							KingdomSurvey.ObserveRemovedFromActive(Z, marker);
					if (KingdomConstructionRules.ExactRemovalAction(true, removed,
						GameObject.Validate(marker), KingdomConstruction.FindExactId(
							Z, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
						!= KingdomExactRemovalAction.ProvedAbsent)
					{
						KingdomConstruction.Quarantine(ref recovered,
							"Plot-works plan-marker removal was vetoed or remained valid.");
							return;
						}
						if (!TryProvePlotPlanMarkerRemoval(System, Z, works, false, markerId,
							ref recovered, out string markerFailure))
						{
							KingdomConstruction.Quarantine(ref recovered, markerFailure);
							return;
						}
					}
					if (recovered.SubjectId != works.IDIfAssigned)
					{
						if (!HasPlotPlanMarkerRemovalProof(works, recovered.SubjectId))
						{
							KingdomConstruction.Quarantine(ref recovered,
								"Plot works lack exact plan-marker removal proof.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref recovered, works.IDIfAssigned)) return;
					}
					KingdomConstruction.FinishProjection(ref recovered, true, true);
					return;
				}
				if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
				{
					KingdomConstructionJob pending = Job;
					if (!KingdomConstructionRules.TryReadBuildTruth(pending,
						out _, out _, out _))
					{
						KingdomConstruction.Quarantine(ref pending,
							"The unprojected legacy plot plan predates frozen build effects.");
						return;
					}
					if (KingdomConstruction.BeginProjection(ref pending, out _))
					{
						StakeFromPlan(System, marker, entry, pending, out _);
					}
				}
				return;
			}
			ProjectPlot(System, Z, rect, entry, spec, new GroundGrid(Z), skin,
				KingdomPlotRules.IsUnderground(Z.Z), Job, out _, out _, out _);
		}

	}
}
