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
		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| (Job.Route != KingdomConstructionRoute.PlotCommission
					&& Job.Route != KingdomConstructionRoute.PlotPlan)) return;
			KingdomConstructionJob inspected = Job;
			GameObject result;
			KingdomPhysicalLookupState resultState = FindConstructionResult(
				Z, Job, true, out result);
			if (resultState == KingdomPhysicalLookupState.Absent)
				resultState = FindConstructionResult(Z, Job, false, out result);
			if (resultState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The frozen plot output ID is duplicated or malformed.");
				return;
			}
			GameObject receiptSubject = null;
			KingdomPhysicalLookupState subjectState = Job.Route == KingdomConstructionRoute.PlotPlan
				? KingdomConstruction.FindSubject(Z, Job, out receiptSubject)
				: KingdomPhysicalLookupState.Absent;
			if (subjectState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The plot plan subject ID is duplicated in its loaded owner zone.");
				return;
			}
			if (GameObject.Validate(result) && result.CurrentZone == Z
				&& result.GetPart<r_KingdomPlotWorks>() != null
				&& result.GetPart<r_KingdomPlotWorks>().DesignKey == Job.TargetKey)
			{
				GameObject worksMarker = receiptSubject;
				if (worksMarker != null && worksMarker != result
					&& worksMarker.GetPart<r_KingdomPlanMarker>() != null)
				{
					if (Job.Phase == KingdomConstructionPhase.ProjectionPending)
					{
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The plot works are verified and their surviving plan marker is retryable.");
					}
				}
				else if (Job.Phase != KingdomConstructionPhase.Working)
				{
					if (inspected.SubjectId != result.IDIfAssigned)
					{
						if (!HasPlotPlanMarkerRemovalProof(result, inspected.SubjectId))
						{
							KingdomConstruction.Quarantine(ref inspected,
								"Plot works lack exact plan-marker removal proof after reload.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref inspected, result.IDIfAssigned)) return;
					}
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if (GameObject.Validate(result) && result.CurrentZone == Z
				&& result.GetIntProperty("KingdomBuilt") == 1
				&& result.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.TargetKey)
			{
				GameObject finalMarker = receiptSubject;
				if (finalMarker != null && finalMarker != result
					&& finalMarker.GetPart<r_KingdomPlanMarker>() != null)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"A completed plot and its receipt-bound plan marker coexist.");
				}
				else
				{
					if (inspected.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
					{
						RecoverPendingPlotRemoval(System, Z, result, ref inspected);
						return;
					}
					string removedWorks = result.GetStringProperty(
						r_KingdomScaffold.RemovalProofProperty);
					if (!string.IsNullOrEmpty(removedWorks)
						&& inspected.SubjectId != removedWorks)
					{
						if (!HasPlotPlanMarkerRemovalProof(result, inspected.SubjectId))
						{
							KingdomConstruction.Quarantine(ref inspected,
								"Completed plot lacks exact plan-marker removal proof after reload.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref inspected, removedWorks)) return;
					}
					if (!r_KingdomScaffold.HasRemovalProof(result, inspected.SubjectId))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The completed plot lacks exact works-removal proof.");
					}
					else FinishPlotEffects(System, Z, result, ref inspected);
				}
				return;
			}
			if (Job.Phase != KingdomConstructionPhase.ProjectionPending) return;
			GameObject marker = receiptSubject;
			if (Job.Route == KingdomConstructionRoute.PlotCommission
				|| (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null))
			{
				KingdomConstruction.FinishProjection(ref inspected, false, false,
					"No plot works crossed the interrupted projection boundary.");
			}
		}

		private static KingdomPhysicalLookupState FindConstructionResult(Zone Z,
			KingdomConstructionJob Job, bool Final, out GameObject Result)
		{
			Result = null;
			if (Z == null || Job == null || !TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacy, out _)
				|| (!legacy && (architecture == null || architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacy && (Job.X != rect.CenterX || Job.Y != rect.CenterY)))
				return KingdomPhysicalLookupState.Ambiguous;
			Cell expectedCell = legacy ? Z.GetCell(rect.CenterX, rect.CenterY)
				: Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			// A paid plan starts with its plan marker in SubjectId and has no output yet. The
			// marker is authority to project, not plot works; never inspect it as though the
			// authored works receipt had already crossed the projection boundary.
			if (!Final && Job.Route == KingdomConstructionRoute.PlotPlan
				&& string.IsNullOrEmpty(Job.OutputId))
				return KingdomPhysicalLookupState.Absent;
			string expectedId = Final ? Job?.OutputId
				: (!string.IsNullOrEmpty(Job?.OutputId) ? Job.OutputId : Job?.SubjectId);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(
				Z, expectedId, out var item);
			if (state != KingdomPhysicalLookupState.Exact) return state;
			if (!KingdomConstruction.HasReceipt(item, Job))
				return KingdomPhysicalLookupState.Ambiguous;
			if (Final)
			{
				if (item.GetIntProperty("KingdomBuilt") != 1)
				{
					// OutputId names works until the final root is published. Valid works are
					// absence of a final, not a malformed final, so callers can inspect works next.
					if (ExpectedWorks(item, expectedCell, Job.TargetKey, architecture, legacy, Job))
						return KingdomPhysicalLookupState.Absent;
					return KingdomPhysicalLookupState.Ambiguous;
				}
				if (!ExpectedArchitectureReceipt(item, expectedCell, Job.TargetKey,
					architecture, legacy)
					|| !KingdomConstruction.FinalBuildTruthMatches(item, Job))
					return KingdomPhysicalLookupState.Ambiguous;
			}
			else
			{
				if (!ExpectedWorks(item, expectedCell, Job.TargetKey, architecture, legacy, Job))
					return KingdomPhysicalLookupState.Ambiguous;
			}
			Result = item;
			return KingdomPhysicalLookupState.Exact;
		}

		/// <summary>
		/// Buildings and scaffolds this zone already carries, by the exact rule
		/// <c>KingdomCommission.Commission</c> uses for its own cap check: frontier works are exempt, work
		/// in progress already counts. Plot walls, floors, and furnishings are not counted &mdash;
		/// the cap counts plots, not the hundred objects one plot is made of.
		/// </summary>
		public static int CountBuilt(Zone Z)
		{
			return Z == null ? 0 : CountBuilt(Z.GetObjects());
		}

		/// <summary>Shared cap census over one already-frozen object sequence. A defensive plotted
		/// building counts once; its scenery never counts; a free-standing frontier segment never
		/// counts, including while its scaffold is still rising.</summary>
		public static int CountBuilt(IEnumerable<GameObject> Objects)
		{
			int built = 0;
			if (Objects == null) return built;
			foreach (GameObject item in Objects)
			{
				if (item == null || item.GetIntProperty(PlotPartProperty) == 1
					|| IsFrontierWork(item))
				{
					continue;
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold") || item.HasPart("r_KingdomPlotWorks"))
				{
					built++;
				}
			}
			return built;
		}

		/// <summary>Classifies live and legacy objects by the same defence/plot separation used by
		/// the pure rules. Registry truth wins; plot receipts preserve the distinction if a design
		/// was later removed; old unreceipted defensive objects remain frontier works.</summary>
		public static bool IsFrontierWork(GameObject Object)
		{
			if (Object == null) return false;
			if (!string.IsNullOrEmpty(Object.GetStringProperty(PlotIdProperty))
				|| Object.HasPart("r_KingdomPlotWorks")
				|| Object.GetIntProperty(AdoptedPlotProperty) == 1) return false;
			if (Object.GetIntProperty(FrontierWorkProperty) == 1) return true;
			string key = Object.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
				key = Object.GetStringProperty(KingdomLayout.AdoptedKeyProperty);
			if (Object.HasPart("r_KingdomScaffold") && !string.IsNullOrEmpty(key)
				&& KingdomData.TryGetBuilding(key, out var entry))
				return KingdomRules.IsFrontierWork(entry.Defence, IsPlotDesign(key));
			return Object.GetIntProperty("KingdomDefence") > 0
				|| Object.GetIntProperty("KingdomDefencePending") > 0;
		}

		// --- The plan path ----------------------------------------------------------------

		/// <summary>
		/// Resolves a new surveyor's plan at the founder's exact stake. The stake itself remains
		/// outside the lot, so it can stand visibly without becoming an obstruction the eventual
		/// authored stamp must clear. The returned quote freezes the whole lot/map/price/labour; no
		/// resource or world object is changed here.
		/// </summary>
	}
}
