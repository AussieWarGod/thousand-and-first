using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null) return;
			if (Job.Route == KingdomConstructionRoute.SocketBuild)
			{
				ContinueSocketBuild(System, Z, Job, false);
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert
				&& Job.PhysicalPhase != KingdomPhysicalPhase.None
				&& !(Job.Phase == KingdomConstructionPhase.Complete
					&& Job.PhysicalPhase == KingdomPhysicalPhase.Settled))
			{
				KingdomMaterials.InspectConstruction(System, Z, Job);
				return;
			}
			GameObject predecessor;
			KingdomPhysicalLookupState predecessorState = KingdomConstruction.FindSubject(
				Z, Job, out predecessor);
			KingdomConstructionJob inspected = Job;
			if (predecessorState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The socket predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketRedress)
			{
				if (GameObject.Validate(predecessor)
					&& KingdomData.TryGetBuilding(Job.TargetKey, out var entry))
				{
					KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(entry.Skins,
						Job.Payload);
						if (skin != null && IsRedressed(predecessor, skin))
						{
							if (inspected.Phase != KingdomConstructionPhase.Complete
								&& !KingdomConstruction.Complete(ref inspected)) return;
							if (KingdomCeremony.EnsureSocketRedressed(System,
								predecessor.ShortDisplayName, Job.Payload, ref inspected))
								KingdomConstruction.UpdatePhysical(ref inspected,
									KingdomPhysicalPhase.Settled, inspected.PhysicalIndex,
									inspected.PhysicalAmount, inspected.PhysicalSpilled,
									inspected.PhysicalItemId, inspected.PhysicalDestinationId,
									inspected.PhysicalReceipt);
					}
					else if (Job.Phase == KingdomConstructionPhase.ProjectionPending
						&& predecessor.GetIntProperty("KingdomBuilt") == 1)
					{
						// Every render override is assignment to the paid value; repeating it is exact.
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The interrupted re-dressing is safe to reapply idempotently.");
					}
				}
				return;
			}
			GameObject result;
			KingdomPhysicalLookupState resultState = FindSocketResult(Z, Job, true, out result);
			if (resultState == KingdomPhysicalLookupState.Absent)
				resultState = FindSocketResult(Z, Job, false, out result);
			if (resultState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The frozen socket output ID is duplicated or malformed.");
				return;
			}
			if (resultState == KingdomPhysicalLookupState.Exact)
			{
				if (GameObject.Validate(predecessor) && predecessor != result
					&& (predecessor.GetPart<r_KingdomSocket>() != null
						|| predecessor.GetIntProperty("KingdomBuilt") == 1))
				{
					KingdomConstruction.FinishProjection(ref inspected, false, false,
						"The verified socket result still has its predecessor to remove.");
				}
				else if (result.GetIntProperty("KingdomBuilt") == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(result, Job.SubjectId))
						KingdomConstruction.Quarantine(ref inspected,
							"Completed socket conversion lacks exact works-removal proof.");
					else if ((inspected.Phase == KingdomConstructionPhase.Complete
						|| KingdomConstruction.Complete(ref inspected)))
						r_KingdomScaffold.TellCompletion(System, result, inspected);
				}
				else if (Job.Phase != KingdomConstructionPhase.Working)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert
				&& GameObject.Validate(predecessor) && predecessor.CurrentZone == Z
				&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0
				&& predecessor.GetStringProperty(PendingConvertKeyProperty) == Job.TargetKey)
			{
				KingdomConstruction.FinishProjection(ref inspected, true, true);
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert
				&& Job.Phase == KingdomConstructionPhase.ProjectionPending
				&& GameObject.Validate(predecessor) && predecessor.CurrentZone == Z
				&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0
				&& string.IsNullOrEmpty(predecessor.GetStringProperty(PendingConvertKeyProperty))
				&& KingdomPlots.TryDecodePlotPayload(Job.Payload, out _, out var interruptedSkin,
					out _, out _, out _))
			{
				// OrderStrike was accepted; only the idempotent target stamps were interrupted.
				predecessor.SetStringProperty(PendingConvertKeyProperty, Job.TargetKey);
				predecessor.SetStringProperty(PendingConvertSkinProperty, interruptedSkin,
					RemoveIfNull: true);
				KingdomConstruction.Bind(predecessor, inspected);
				if (predecessor.GetStringProperty(PendingConvertKeyProperty) == Job.TargetKey)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert
				&& GameObject.Validate(predecessor) && predecessor.CurrentZone == Z
				&& predecessor.GetIntProperty("KingdomBuilt") == 1
				&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) == 0
				&& predecessor.GetStringProperty(PendingConvertKeyProperty) == Job.TargetKey)
			{
				KingdomConstruction.FinishProjection(ref inspected, false, false,
					"The paid conversion's strike order was interrupted and is safe to restore.");
				return;
			}
			if (Job.Phase != KingdomConstructionPhase.ProjectionPending) return;
			if (GameObject.Validate(predecessor) && predecessor.CurrentZone == Z)
			{
				if (Job.Route == KingdomConstructionRoute.SocketBuild
					&& predecessor.GetPart<r_KingdomSocket>() != null)
				{
					KingdomConstruction.FinishProjection(ref inspected, false, false,
						"The cleared-plot marker survived the interrupted projection.");
				}
				else if (Job.Route == KingdomConstructionRoute.SocketConvert
					&& predecessor.GetIntProperty("KingdomBuilt") == 1
					&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) == 0
					&& string.IsNullOrEmpty(predecessor.GetStringProperty(PendingConvertKeyProperty)))
				{
					KingdomConstruction.FinishProjection(ref inspected, false, false,
						"The predecessor remained unchanged after the interrupted conversion order.");
				}
			}
		}

		private static KingdomPhysicalLookupState FindSocketResult(Zone Z,
			KingdomConstructionJob Job, bool Final, out GameObject Result)
		{
			Result = null;
			if (Z == null || Job == null || !KingdomPlots.TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacyArchitecture, out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY)))
				return KingdomPhysicalLookupState.Ambiguous;
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(
				Z, Job?.OutputId, out var candidate);
			if (state != KingdomPhysicalLookupState.Exact) return state;
			Cell expected = Z.GetCell(Job.X, Job.Y);
			if (candidate.CurrentZone != Z || candidate.CurrentCell != expected
				|| !KingdomConstruction.HasReceipt(candidate, Job)
				|| !KingdomPlots.ExpectedArchitectureReceipt(candidate, expected, Job.TargetKey,
					architecture, legacyArchitecture))
				return KingdomPhysicalLookupState.Ambiguous;
			if (Final)
			{
				if (candidate.GetIntProperty("KingdomBuilt") != 1)
				{
					// OutputId names works until the final root is published. Valid works are
					// absence of a final, not a malformed final, so callers can inspect works next.
					r_KingdomPlotWorks works = candidate.GetPart<r_KingdomPlotWorks>();
					return works != null && works.DesignKey == Job.TargetKey
						? KingdomPhysicalLookupState.Absent
						: KingdomPhysicalLookupState.Ambiguous;
				}
				if (candidate.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
					!= Job.TargetKey) return KingdomPhysicalLookupState.Ambiguous;
			}
			else
			{
				r_KingdomPlotWorks works = candidate.GetPart<r_KingdomPlotWorks>();
				if (works == null || works.DesignKey != Job.TargetKey)
					return KingdomPhysicalLookupState.Ambiguous;
			}
			Result = candidate;
			return KingdomPhysicalLookupState.Exact;
		}
	}
}
