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

	/// <summary>
	/// The engine-coupled half of the plot as a socket: leaving a re-buildable slot behind a
	/// strike, changing what a standing plot is as one strike-and-rebuild ceremony, building fresh
	/// on ground a strike already cleared, and re-dressing a standing building in any registered
	/// skin. Every founder-facing entry point here does its own eligibility check and its own
	/// messaging, surfacing only a decline through <c>Failure</c>, matching the
	/// <c>KingdomMaterials</c>/<c>KingdomAdopt</c> idiom throughout this mod. Every decision that
	/// does not need a real object or a real cell &mdash; the (type, size) classification, the
	/// footprint check, the combined cost, every refusal's wording &mdash; is delegated to the
	/// engine-free <see cref="KingdomSocketRules"/>.
	/// <para>
	/// This never builds a second demolition or a second construction pipeline. Striking still
	/// runs through <see cref="KingdomMaterials.OrderStrike"/> unmodified, on its own crew-days
	/// schedule with its own salvage math; raising a design onto a rect still runs through
	/// <see cref="KingdomPlots.Stake"/> unmodified, staged exactly as an ordinary commission
	/// stages. The only things this file adds are: the one combined figure disclosed before either
	/// of those is asked to do anything (<see cref="KingdomSocketRules.DescribeConversion"/>); the
	/// hook a strike's own completion calls so a plot's ground survives it (<see cref="OnCleared"/>,
	/// wired into <c>KingdomMaterials.WorkStrike</c>); and the ordinary re-stake of ground a strike
	/// already vacated, onto the exact rect it vacated, bypassing the fresh-ground search a
	/// first-time commission runs because there is nothing left here to search for.
	/// </para>
	/// </summary>
	public static partial class KingdomSocket
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null)
			{
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketBuild)
			{
				ContinueSocketBuild(System, Z, Job, true);
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert
				&& Job.PhysicalPhase != KingdomPhysicalPhase.None
				&& !(Job.Phase == KingdomConstructionPhase.Complete
					&& Job.PhysicalPhase == KingdomPhysicalPhase.Settled))
			{
				KingdomMaterials.RetryConstruction(System, Z, Job);
				return;
			}
			GameObject predecessor;
			KingdomPhysicalLookupState predecessorState = KingdomConstruction.FindSubject(
				Z, Job, out predecessor);
			if (predecessorState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The socket predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketRedress)
			{
				GameObject building = predecessor;
				if (building != null && KingdomData.TryGetBuilding(Job.TargetKey, out var redressEntry))
				{
					KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(
						redressEntry.Skins, Job.Payload);
					if (skin != null)
					{
						KingdomConstructionJob redress = Job;
						if (!KingdomCeremony.PrepareSocketRedressed(System,
							building.ShortDisplayName, Job.Payload, ref redress)) return;
						if (ProjectRedress(building, skin, redress, out redress, out _)
							&& redress.Phase == KingdomConstructionPhase.Complete)
						{
							if (KingdomCeremony.DispatchPending(System, ref redress))
								KingdomConstruction.UpdatePhysical(ref redress,
									KingdomPhysicalPhase.Settled, redress.PhysicalIndex,
									redress.PhysicalAmount, redress.PhysicalSpilled,
									redress.PhysicalItemId, redress.PhysicalDestinationId,
									redress.PhysicalReceipt);
						}
					}
				}
				return;
			}
			if ((Job.Route != KingdomConstructionRoute.SocketBuild
					&& Job.Route != KingdomConstructionRoute.SocketConvert)
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)
				|| !KingdomPlots.TryGetSpec(Job.TargetKey, out var spec)
				|| !KingdomPlots.TryDecodePlotPayload(Job.Payload, out var rect, out var skinKey,
					out KingdomArchitectureIntent architecture, out bool legacyArchitecture,
					out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY)))
			{
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert)
			{
				if (GameObject.Validate(predecessor) && predecessor.CurrentZone == Z
					&& predecessor.ID == Job.SourceId
					&& predecessor.GetIntProperty("KingdomBuilt") == 1)
				{
					ProjectConvertOrder(System, Z, predecessor, Job.TargetKey, skinKey,
						Job, out _, out _);
				}
				else
				{
					KingdomConstructionJob quarantined = Job;
					KingdomConstruction.Quarantine(ref quarantined,
						"A legacy conversion lacks exact predecessor-removal proof.");
				}
				return;
			}
			GameObject final;
			KingdomPhysicalLookupState finalState = FindSocketResult(Z, Job, true, out final);
			if (finalState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The socket final ID is duplicated or malformed.");
				return;
			}
			if (finalState == KingdomPhysicalLookupState.Exact)
			{
				RemoveSocketPredecessor(predecessor, final, Job, false, out _);
				return;
			}
			GameObject existingWorks;
			KingdomPhysicalLookupState worksState = FindSocketResult(Z, Job, false,
				out existingWorks);
			if (worksState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The socket works ID is duplicated or malformed.");
				return;
			}
			if (worksState == KingdomPhysicalLookupState.Exact)
			{
				RemoveSocketPredecessor(predecessor, existingWorks, Job, true, out _);
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert && predecessor != null
				&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0
				&& predecessor.GetStringProperty(PendingConvertKeyProperty) == Job.TargetKey)
			{
				KingdomConstructionJob working = Job;
				if (Job.Phase != KingdomConstructionPhase.Working)
					KingdomConstruction.FinishProjection(ref working, true, true);
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert && predecessor != null
				&& predecessor.GetIntProperty("KingdomBuilt") == 1
				&& predecessor.GetIntProperty(KingdomMaterials.StrikeEffortProperty) <= 0
				&& predecessor.GetStringProperty(PendingConvertKeyProperty) == Job.TargetKey)
			{
				// The strike crossed its completion boundary before a reload. Never order it again:
				// that could salvage the same predecessor twice. Resume only the paid rebuild.
				if (!TrySweepLegacyPlotParts(Z, rect,
					predecessor.GetStringProperty(KingdomPlots.PlotIdProperty), predecessor))
				{
					KingdomConstructionJob unsafeLegacy = Job;
					KingdomConstruction.Quarantine(ref unsafeLegacy,
						"Legacy plot-part sweep found current authored or protected state.");
					return;
				}
				GameObject resumedWorks;
				KingdomConstructionJob resumed;
				if (KingdomPlots.ProjectOnRect(System, Z, rect, entry, spec, skinKey, Job,
					out resumedWorks, out resumed, out _))
				{
					RemoveSocketPredecessor(predecessor, resumedWorks, resumed, true, out _);
				}
				return;
			}
			if (Job.Route == KingdomConstructionRoute.SocketConvert && predecessor != null
				&& predecessor.GetIntProperty("KingdomBuilt") == 1)
			{
				ProjectConvertOrder(System, Z, predecessor, Job.TargetKey, skinKey,
					Job, out _, out _);
				return;
			}
			GameObject works;
			KingdomConstructionJob updated;
			if (KingdomPlots.ProjectOnRect(System, Z, rect, entry, spec, skinKey, Job,
				out works, out updated, out _))
			{
				RemoveSocketPredecessor(predecessor, works, updated, true, out _);
			}
		}
	}
}
