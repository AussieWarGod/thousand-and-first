using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name. This part is
// only ever added in code (see r_KingdomImprovement's own header for why), but it lives here
// anyway alongside every other part this mod ships: a part whose namespace depends on how it
// happened to be attached is a trap waiting for the first blueprint that names it.
namespace XRL.World.Parts
{
	/// <summary>
	/// A cleared plot: everything the settlement raised on this ground has come down, and the
	/// rect, its lane, and the ground itself stand ready for whatever the founder chooses next.
	/// See BUILDING-CATALOGUE-BRIEF.md's 2026-08-21 addendum, "the plot as socket".
	/// <para>
	/// Carries no geometry of its own. The rect a later stake needs rides on the same
	/// <c>KingdomPlots.PlotX1Property</c> family every laid plot already carries &mdash;
	/// <c>KingdomSocket</c> stamps them with <c>KingdomPlots.StampRect</c> the moment this part is
	/// attached &mdash; so <c>KingdomPlots.ReadPlots</c>, the lane rule, and the road budget all
	/// count a socket exactly as they count a standing plot, with no change to any of the three.
	/// <see cref="LastDesignKey"/> is purely descriptive: nothing anywhere reads it to decide
	/// anything, only to tell the founder what stood here.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomSocket : IPart
	{
		/// <summary>Registry key of the design that last stood here, if it is still known.
		/// Null when nothing was ever recorded, or when the design has since left the catalogue.</summary>
		public string LastDesignKey;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|This ground stands cleared, staked out, and ready to be built on again.}}");
			return base.HandleEvent(E);
		}
	}
}

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
	public static class KingdomSocket
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

		private static void ContinueSocketBuild(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job, bool MayProject)
		{
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| Job.Route != KingdomConstructionRoute.SocketBuild
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)
				|| !KingdomPlots.TryGetSpec(Job.TargetKey, out var spec)
				|| !KingdomPlots.TryDecodePlotPayload(Job.Payload, out var rect, out var skinKey,
					out KingdomArchitectureIntent architecture, out bool legacyArchitecture,
					out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY))
				|| string.IsNullOrEmpty(Job.SourceId)) return;

			KingdomConstructionJob current = Job;
			GameObject source;
			KingdomPhysicalLookupState sourceState = KingdomConstruction.FindExactId(
				Z, current.SourceId, out source);
			if (current.PhysicalPhase == KingdomPhysicalPhase.None)
			{
				Cell center = Z.GetCell(rect.CenterX, rect.CenterY);
				if (!MayProject || sourceState != KingdomPhysicalLookupState.Exact
					|| !GameObject.Validate(source) || source.CurrentZone != Z
					|| source.CurrentCell != center || source.ID != current.SourceId
					|| source.GetPart<r_KingdomSocket>() == null
					|| !KingdomConstruction.HasReceipt(source, current)
					|| !KingdomPlots.TryReadRect(source, out var observed)
					|| observed.X1 != rect.X1 || observed.Y1 != rect.Y1
					|| observed.X2 != rect.X2 || observed.Y2 != rect.Y2)
				{
					KingdomConstruction.Quarantine(ref current,
						"Socket-build predecessor identity or frozen rect changed before removal.");
					return;
				}
				if (!KingdomConstruction.UpdatePhysical(ref current,
					KingdomPhysicalPhase.PredecessorRemovalPending, 0, 0, 0,
					current.SourceId, null, "socket-build:v1")) return;
				GameObject exactSource;
				if (!KingdomConstruction.Owns(System, Z, current)
					|| !KingdomConstruction.IsCurrent(current) || !GameObject.Validate(source)
					|| source.CurrentCell != center || source.ID != current.SourceId
					|| source.GetPart<r_KingdomSocket>() == null
					|| !KingdomConstruction.HasReceipt(source, current)
					|| KingdomConstruction.FindExactId(Z, current.SourceId, out exactSource)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exactSource, source))
				{
					KingdomConstruction.Quarantine(ref current,
						"Socket-build predecessor changed after removal intent publication.");
					return;
				}
				bool removed;
				try { removed = source.Obliterate(null, Silent: true); }
				catch (System.Exception ex)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, source);
					KingdomConstruction.Quarantine(ref current,
						"Socket-build predecessor removal threw: " + ex.Message);
					return;
				}
				if (removed || !GameObject.Validate(source))
					KingdomSurvey.ObserveRemovedFromActive(Z, source);
				sourceState = KingdomConstruction.FindExactId(Z, current.SourceId, out var replacement);
				if (!removed || GameObject.Validate(source)
					|| sourceState != KingdomPhysicalLookupState.Absent
					|| GameObject.Validate(replacement) || !KingdomConstruction.Owns(System, Z, current)
					|| !KingdomConstruction.IsCurrent(current))
				{
					KingdomConstruction.Quarantine(ref current,
						"Socket-build predecessor removal was vetoed, replaced, or re-entered.");
					return;
				}
				if (!KingdomConstruction.UpdatePhysical(ref current,
					KingdomPhysicalPhase.PredecessorRemoved, 0, 0, 0,
					current.SourceId, null, "socket-build:v1")) return;
			}
			else if (current.PhysicalPhase == KingdomPhysicalPhase.PredecessorRemovalPending)
			{
				// FindByID searches loaded zones only. A save before the callback-success
				// tombstone cannot prove whether an unloaded exact predecessor survived.
				KingdomConstruction.Quarantine(ref current,
					"Socket-build removal was interrupted before exact callback-success proof.");
				return;
			}
			else if (current.PhysicalPhase != KingdomPhysicalPhase.PredecessorRemoved)
			{
				KingdomConstruction.Quarantine(ref current,
					"Socket-build physical receipt has an impossible phase.");
				return;
			}

			sourceState = KingdomConstruction.FindExactId(Z, current.SourceId, out source);
			if (sourceState != KingdomPhysicalLookupState.Absent)
			{
				KingdomConstruction.Quarantine(ref current,
					"Socket-build predecessor reappeared after exact removal proof.");
				return;
			}
			GameObject final;
			KingdomPhysicalLookupState finalState = FindSocketResult(Z, current, true, out final);
			if (finalState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref current,
					"The frozen socket-build final ID is duplicated or malformed.");
				return;
			}
			if (finalState == KingdomPhysicalLookupState.Exact)
			{
				if (!r_KingdomScaffold.HasRemovalProof(final, current.SubjectId))
				{
					KingdomConstruction.Quarantine(ref current,
						"Completed socket build lacks exact works-removal proof.");
					return;
				}
				if (current.PhysicalAmount != 1)
				{
					if (current.Outbox != null && current.Outbox.EventId
						!= "construction:" + current.Id + ":socket-staked")
					{
						KingdomConstruction.Quarantine(ref current,
							"Completed socket build lacks durable socket-staked event proof.");
						return;
					}
					if (!KingdomCeremony.EnsureSocketStaked(System, entry.Name, ref current)
						|| !KingdomConstruction.UpdatePhysical(ref current,
							current.PhysicalPhase, current.PhysicalIndex, 1,
							current.PhysicalSpilled, current.PhysicalItemId,
							current.PhysicalDestinationId, current.PhysicalReceipt)) return;
				}
				if (current.Phase != KingdomConstructionPhase.Complete
					&& !KingdomConstruction.Complete(ref current)) return;
				r_KingdomScaffold.TellCompletion(System, final, current);
				return;
			}
			GameObject works;
			KingdomPhysicalLookupState worksState = FindSocketResult(Z, current, false, out works);
			if (worksState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref current,
					"The frozen socket-build works ID is duplicated or malformed.");
				return;
			}
			if (worksState == KingdomPhysicalLookupState.Exact)
			{
				if (current.SubjectId != works.ID
					&& !KingdomConstruction.UpdateSubject(ref current, works.ID)) return;
				if (current.Phase != KingdomConstructionPhase.Working)
					KingdomConstruction.FinishProjection(ref current, true, true);
				if (current.PhysicalAmount != 1
					&& KingdomCeremony.EnsureSocketStaked(System, entry.Name, ref current))
					KingdomConstruction.UpdatePhysical(ref current, current.PhysicalPhase,
						current.PhysicalIndex, 1, current.PhysicalSpilled,
						current.PhysicalItemId, current.PhysicalDestinationId,
						current.PhysicalReceipt);
				return;
			}
			if (!string.IsNullOrEmpty(current.OutputId))
			{
				KingdomConstruction.Quarantine(ref current,
					"Frozen socket-build output is absent or was replaced; no new output was adopted.");
				return;
			}
			if (!MayProject) return;
			if (KingdomPlots.ProjectOnRect(System, Z, rect, entry, spec, skinKey, current,
				out works, out current, out _))
			{
				if (KingdomConstruction.FindExactId(Z, current.SourceId, out _)
					!= KingdomPhysicalLookupState.Absent
					|| !GameObject.Validate(works) || works.ID != current.OutputId
					|| !KingdomConstruction.HasReceipt(works, current)
					|| !KingdomConstruction.Owns(System, Z, current)
					|| FindSocketResult(Z, current, false, out var exactWorks)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exactWorks, works))
				{
					KingdomConstruction.Quarantine(ref current,
						"Socket-build projection changed frozen source or output identity.");
					return;
				}
				if (KingdomCeremony.EnsureSocketStaked(System, entry.Name, ref current))
					KingdomConstruction.UpdatePhysical(ref current, current.PhysicalPhase,
						current.PhysicalIndex, 1, current.PhysicalSpilled,
						current.PhysicalItemId, current.PhysicalDestinationId,
						current.PhysicalReceipt);
			}
		}

		private static bool RemoveSocketPredecessor(GameObject Predecessor,
			GameObject Result, KingdomConstructionJob Job, bool Working,
			out KingdomConstructionJob Updated)
		{
			Updated = Job;
			if (!GameObject.Validate(Result) || Result.CurrentCell == null) return false;
			if (GameObject.Validate(Predecessor) && Predecessor != Result
				&& (Predecessor.GetPart<r_KingdomSocket>() != null
					|| Predecessor.GetIntProperty("KingdomBuilt") == 1))
			{
				if (!KingdomConstruction.BeginProjection(ref Updated, out _)) return false;
				Cell oldCell = Predecessor.CurrentCell;
				bool removed;
				try { removed = Predecessor.Obliterate(null, Silent: true); }
				finally
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Result.CurrentZone, Predecessor);
				}
				if (removed || !GameObject.Validate(Predecessor))
					KingdomSurvey.ObserveRemovedFromActive(Result.CurrentZone, Predecessor);
				if (!removed || GameObject.Validate(Predecessor))
				{
					if (GameObject.Validate(Predecessor) && Predecessor.CurrentCell == oldCell)
						KingdomConstruction.FinishProjection(ref Updated, false, false,
							"The verified socket result still waits on predecessor removal.");
					else
						KingdomConstruction.Quarantine(ref Updated,
							"Socket predecessor removal moved or partially changed the source.");
					return false;
				}
				if (!GameObject.Validate(Result) || Result.CurrentCell == null)
				{
					KingdomConstruction.Quarantine(ref Updated,
						"The socket result changed during predecessor removal.");
					return false;
				}
			}
			if (Working)
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
			}
			else
			{
				KingdomConstruction.Complete(ref Updated);
			}
			return true;
		}

		private static bool HasBlockingReceipt(GameObject Object)
		{
			return KingdomConstruction.ReceiptBlocksCurrent(Object);
		}

		/// <summary>Blueprint the socket marker stands as. Inherits vanilla <c>Sign</c> the same
		/// way every other inert stake in this mod does (<c>r_KingdomClearanceStake</c>,
		/// <c>r_KingdomNotice</c>) and carries only this file's own part &mdash; never
		/// <c>r_KingdomClearance</c>, which would make <c>KingdomMaterials.OnSettlementPass</c>
		/// mistake a socket for a live clearance order. See MODDING.md / ObjectBlueprints.xml for
		/// the declaration; this file only ever creates it, never defines it.</summary>
		public const string SocketBlueprint = "r_KingdomSocket";

		/// <summary>Registry key of the design a condemned building is being converted into,
		/// staged on the building itself the moment the founder orders the ceremony and read back
		/// by <see cref="OnCleared"/> once the crew finishes taking it down &mdash; which may be
		/// days, or a save and reload, later. A property rather than a part field for the reason
		/// every other staged choice in this mod (skins, plans) is one: STANDARDS.md &sect;1
		/// forbids appending a serialized field to a part that already ships, and the building
		/// this rides on ships today with no opinion about being converted.</summary>
		public const string PendingConvertKeyProperty = "KingdomConvertKey";

		/// <summary>The skin key chosen alongside <see cref="PendingConvertKeyProperty"/>, or
		/// absent for the new design's own unmodified look.</summary>
		public const string PendingConvertSkinProperty = "KingdomConvertSkin";

		// ==================================================================================
		// Convert: "change what this plot is" as one ceremony
		// ==================================================================================

		/// <summary>What one <see cref="Validate"/> pass resolved, so <see cref="AssessConvert"/>
		/// and <see cref="ExecuteConvert"/> never have to re-derive it separately and can never
		/// disagree about which design, which spec, or which rect they are talking about.</summary>
		private struct ConvertContext
		{
			public KingdomRules.BuildEntry OldEntry;
			public KingdomPlotRules.PlotSpec OldSpec;
			public KingdomRules.BuildEntry NewEntry;
			public KingdomPlotRules.PlotSpec NewSpec;
			public KingdomPlotRules.PlotRect Rect;
			public KingdomPlotRules.PlotRect TargetRect;
			public KingdomPlotRules.PlotSize ActualSize;
			public KingdomSocketRules.ChangeKind Kind;
			public KingdomSocketTransition Transition;
		}

		/// <summary>Read-only production receipt shared by conversion preview and commit.</summary>
		private sealed class PreparedConvert
		{
			public string BuildingId;
			public string SkinKey;
			public ConvertContext Context;
			public KingdomSocketRules.ConversionQuote Quote;
			public KingdomArchitectureIntent Architecture;
			public string Payload;
			public KingdomUpgrade.Assessment Improvement;
			public KingdomUpgrade.PreparedImprovement PreparedImprovement;
			public ArchitectureLayoutDelta Delta;
			public bool RequiresRestakePreflight;
		}

		/// <summary>
		/// Every eligibility check a conversion has to pass, run in the order a founder should
		/// read the refusals in: whose ground it is, whether the settlement actually raised it,
		/// whether it is free to be touched at all, then what the new design itself asks for.
		/// Read-only &mdash; spends nothing, strikes nothing, stakes nothing.
		/// </summary>
		private static bool Validate(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out ConvertContext Context, out string Failure)
		{
			Context = default(ConvertContext);
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A plot is changed on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to change.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement converts what it raised. That is not one of its buildings.";
				return false;
			}
			if (HasBlockingReceipt(Building))
			{
				Failure = "That building already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketConvert, Building))
			{
				Failure = "That building already has a conversion receipt in hand.";
				return false;
			}
			if (Building.GetIntProperty(KingdomAdopt.AdoptedProperty) == 1)
			{
				Failure = KingdomSocketRules.RefuseAdopted(Building.ShortDisplayName);
				return false;
			}
			if (Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0)
			{
				Failure = KingdomSocketRules.RefuseCondemned(Building.ShortDisplayName);
				return false;
			}
			r_KingdomImprovement improvement = Building.GetPart<r_KingdomImprovement>();
			if (improvement != null && (improvement.Working || improvement.Held))
			{
				Failure = KingdomSocketRules.RefuseImproving(Building.ShortDisplayName);
				return false;
			}
			string oldKey = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (!KingdomData.TryGetBuilding(oldKey, out KingdomRules.BuildEntry oldEntry) || !KingdomPlots.TryGetSpec(oldKey, out KingdomPlotRules.PlotSpec oldSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomPlots.TryReadRect(Building, out KingdomPlotRules.PlotRect rect))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomSocketRules.TryActualSize(rect.Width, rect.Height,
				out KingdomPlotRules.PlotSize actualSize))
			{
				Failure = "The standing plot rectangle has no recognized actual size.";
				return false;
			}
			if (oldKey == NewKey)
			{
				Failure = KingdomSocketRules.RefuseAlreadyThat(Building.ShortDisplayName);
				return false;
			}
			if (!KingdomData.TryGetBuilding(NewKey, out KingdomRules.BuildEntry newEntry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(NewKey, out KingdomPlotRules.PlotSpec newSpec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(newEntry.Name);
				return false;
			}
			if (!KingdomRules.StyleAllows(newEntry.Styles, System.Style))
			{
				Failure = "The " + newEntry.Name + " is not built in this city's own style.";
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, newEntry);
			if (Failure != null)
			{
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, newSpec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(newSpec.Size, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(newSpec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			KingdomSocketRules.ChangeKind kind = KingdomSocketRules.FitsSameSet(
				oldEntry.Category, actualSize, newEntry.Category, newSpec.Size)
				? KingdomSocketRules.ChangeKind.SameSet
				: KingdomSocketRules.ChangeKind.Retype;
			KingdomSocketTransition transition = null;
			KingdomPlotRules.PlotRect targetRect = rect;
			if (kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				if (!KingdomWear.CanCarryStableState(Building, out Failure)) return false;
				KingdomArchitectureIntent standing;
				if (!KingdomArchitectureRuntime.TryRead(Building, out standing, out _)
					|| !KingdomArchitectureRules.IsCurrentSnapshotEncoding(
						standing.EncodedSnapshot))
				{
					Failure = "That save-era plot has no exact authored transition delta. Strike it and commission fresh.";
					return false;
				}
				if (!KingdomSocketTransitions.TryGet(oldKey, NewKey, standing.LotType,
					standing.LotSize, out transition))
				{
					Failure = KingdomSocketTransitionRules.RefuseUndeclared(oldEntry.Name,
						newEntry.Name);
					return false;
				}
			}
			else
			{
				List<KingdomPlotRules.PlotRect> remaining = KingdomPlots.ReadPlots(Z);
				for (int i = remaining.Count - 1; i >= 0; i--)
				{
					KingdomPlotRules.PlotRect laid = remaining[i];
					if (laid.X1 == rect.X1 && laid.Y1 == rect.Y1
						&& laid.X2 == rect.X2 && laid.Y2 == rect.Y2)
					{
						remaining.RemoveAt(i);
						break;
					}
				}
				if (KingdomPlotRules.WouldExceedBudget(remaining, newSpec.Size,
					Z.Width, Z.Height))
				{
					Failure = KingdomPlotRules.RefuseBudget(KingdomPresentation.Rich(System.SeatName));
					return false;
				}
				KingdomLayoutRules.LayoutOutcome outcome;
				if (!KingdomPlots.TryFindRect(Z, System, newEntry, newSpec,
					new KingdomPlots.GroundGrid(Z), null, out targetRect, out outcome, out Failure))
					return false;
			}
			// The way down before the weather: a conversion is still a building raised, and rock
			// whose shaft was struck since this plot went up will not take another one.
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, newEntry.Key, newEntry.Name);
			if (Failure != null)
			{
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && newSpec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(newEntry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, newEntry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			Context.OldEntry = oldEntry;
			Context.OldSpec = oldSpec;
			Context.NewEntry = newEntry;
			Context.NewSpec = newSpec;
			Context.Rect = rect;
			Context.TargetRect = targetRect;
			Context.ActualSize = actualSize;
			Context.Kind = kind;
			Context.Transition = transition;
			return true;
		}

		/// <summary>
		/// Reads what converting <paramref name="Building"/> into <paramref name="NewKey"/> would
		/// take: whether it is even allowed, which of Addendum 2's two verbs it is, and the one
		/// combined figure <see cref="KingdomSocketRules.DescribeConversion"/> composes from it.
		/// Spends nothing. Safe to call for a confirmation popup, and called again by
		/// <see cref="ExecuteConvert"/> itself before anything actually moves, because nothing
		/// here trusts that the world held still between the two calls.
		/// </summary>
		public static bool AssessConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, out KingdomSocketRules.ChangeKind Kind, out KingdomSocketRules.ConversionQuote Quote, out string Failure)
		{
			Kind = default(KingdomSocketRules.ChangeKind);
			Quote = default(KingdomSocketRules.ConversionQuote);
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
			{
				return false;
			}
			Kind = context.Kind;
			Quote = Kind == KingdomSocketRules.ChangeKind.SameSet
				? KingdomSocketRules.AssessPlanChange(context.Transition)
				: KingdomSocketRules.AssessConversion(
					KingdomMaterials.CostFor(context.OldEntry.Key), context.OldEntry.CostDrams,
					KingdomMaterials.CostFor(context.NewEntry.Key), context.NewEntry.CostDrams);
			return true;
		}

		/// <summary>
		/// Resolves and preflights the exact production target before consent. No debit, strike,
		/// marker, or receipt is created here.
		/// </summary>
		private static bool TryPrepareConvert(KingdomSystem System, Zone Z, GameObject Building,
			string NewKey, string NewSkinKey, out PreparedConvert Prepared, out string Failure)
		{
			Prepared = null;
			if (!Validate(System, Z, Building, NewKey, out ConvertContext context, out Failure))
				return false;
			KingdomSocketRules.ConversionQuote quote = context.Kind
				== KingdomSocketRules.ChangeKind.SameSet
				? KingdomSocketRules.AssessPlanChange(context.Transition)
				: KingdomSocketRules.AssessConversion(
					KingdomMaterials.CostFor(context.OldEntry.Key), context.OldEntry.CostDrams,
					KingdomMaterials.CostFor(context.NewEntry.Key), context.NewEntry.CostDrams);
			PreparedConvert prepared = new PreparedConvert
			{
				BuildingId = Building.ID, SkinKey = NewSkinKey, Context = context, Quote = quote
			};
			if (context.Kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				if (!KingdomUpgrade.TryPreparePlanChange(System, Z, Building, context.NewEntry,
					context.Transition, out prepared.Improvement,
					out prepared.PreparedImprovement, out Failure)) return false;
				prepared.Architecture = prepared.PreparedImprovement.Architecture;
				prepared.Payload = prepared.PreparedImprovement.Payload;
				prepared.Delta = prepared.PreparedImprovement.Delta;
			}
			else
			{
				KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
					KingdomMaterials.CostFor(context.NewEntry.Key),
					KingdomMaterials.BitCostFor(context.NewEntry.Key),
					KingdomMaterials.ExoticCostFor(context.NewEntry.Key));
				bool architectureMarker = Building.HasIntProperty(
					KingdomArchitectureRuntime.SchemaProperty)
					|| Building.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
				if (architectureMarker)
				{
					KingdomArchitectureIntent standing;
					ArchitectureLayoutSnapshot standingSnapshot;
					string standingLot;
					if (!KingdomArchitectureStamper.TryReadOwner(Building, out standing,
						out standingSnapshot, out standingLot, out Failure)) return false;
					if (KingdomArchitectureRules.IsCurrentSnapshotEncoding(
						standing.EncodedSnapshot))
					{
						prepared.RequiresRestakePreflight = true;
						if (!KingdomArchitectureRuntime.TryPrepare(System, Z, context.TargetRect,
							context.NewEntry.Key, context.NewEntry.Category,
							out prepared.Architecture, out Failure)
							|| !KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building,
								prepared.Architecture, claim, out Failure)
							|| !KingdomPlots.TryEncodePlotPayload(context.TargetRect, NewSkinKey,
								prepared.Architecture, out prepared.Payload, out Failure)) return false;
					}
					else if (!KingdomPlots.TryPreparePlotPayload(System, Z, context.TargetRect,
						context.NewEntry.Key, context.NewEntry.Category, NewSkinKey,
						out prepared.Architecture, out prepared.Payload, out Failure)) return false;
				}
				else if (!KingdomPlots.TryPreparePlotPayload(System, Z, context.TargetRect,
					context.NewEntry.Key, context.NewEntry.Category, NewSkinKey,
					out prepared.Architecture, out prepared.Payload, out Failure)) return false;
			}
			Prepared = prepared;
			return true;
		}

		/// <summary>
		/// Orders the ceremony: pays the new design's full water and material cost right now,
		/// exactly as an ordinary commission would (<see cref="KingdomMaterials.CanPay"/> /
		/// <c>Pay</c>, <c>KingdomGrowth.ConsumeStoredWater</c>, unmodified), then hands the strike
		/// itself to <see cref="KingdomMaterials.OrderStrike"/>, also unmodified. Nothing is built
		/// yet &mdash; the crew has to take the old work down first, on its own ordinary schedule
		/// &mdash; but the whole price is spent and disclosed before any of that begins, which is
		/// what "before anything moves" means here. <see cref="OnCleared"/> is what actually
		/// raises the new design, once the strike finishes.
		/// </summary>
		public static bool ExecuteConvert(KingdomSystem System, Zone Z, GameObject Building, string NewKey, string NewSkinKey, out string Failure)
		{
			if (!TryPrepareConvert(System, Z, Building, NewKey, NewSkinKey,
				out PreparedConvert prepared, out Failure)) return false;
			return ExecutePreparedConvert(System, Z, Building, prepared, out Failure);
		}

		private static bool ExecutePreparedConvert(KingdomSystem System, Zone Z,
			GameObject Building, PreparedConvert Prepared, out string Failure)
		{
			Failure = null;
			if (Prepared == null || !GameObject.Validate(Building)
				|| Building.ID != Prepared.BuildingId
				|| !Validate(System, Z, Building, Prepared.Context.NewEntry.Key,
					out ConvertContext live, out Failure)
				|| live.Kind != Prepared.Context.Kind
				|| live.OldEntry.Key != Prepared.Context.OldEntry.Key
				|| live.NewEntry.Key != Prepared.Context.NewEntry.Key)
			{
				if (Failure == null) Failure = "The previewed conversion is no longer current.";
				return false;
			}
			ConvertContext context = Prepared.Context;
			if (context.Kind == KingdomSocketRules.ChangeKind.SameSet)
			{
				string currentName = KingdomDesign.ReferenceFor(Building, Building.ShortDisplayName);
				if (!KingdomUpgrade.BeginPreparedPlanChange(System, Z, Building,
					Prepared.Improvement, Prepared.PreparedImprovement, out Failure)) return false;
				KingdomGovernanceScope.Commit("change plot plan");
				KingdomChronicle.Record(System, "the founder ordered the " + currentName + " of "
					+ KingdomPresentation.Rich(System.KingdomDisplayName) + " changed in place into "
					+ XRL.Language.Grammar.A(context.NewEntry.Name));
				System.Ledger.Note("{{G|The " + currentName + " keeps its exact lot while its declared plan change is worked.}}");
				MessageQueue.AddPlayerMessage("{{G|The " + currentName + " is ordered changed in place into "
					+ XRL.Language.Grammar.A(context.NewEntry.Name) + ".}}");
				KingdomLog.Log("socket: declared plan change ordered " + context.OldEntry.Key
					+ " -> " + context.NewEntry.Key + " at " + System.SeatName);
				return true;
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(context.NewEntry.Key),
				KingdomMaterials.BitCostFor(context.NewEntry.Key),
				KingdomMaterials.ExoticCostFor(context.NewEntry.Key));
			KingdomArchitectureIntent architecture = Prepared.Architecture;
			string payload = Prepared.Payload;
			if (architecture == null || string.IsNullOrEmpty(payload)
				|| (Prepared.RequiresRestakePreflight
					&& !KingdomArchitectureStamper.TryPreflightRestake(System, Z, Building,
						architecture, claim, out Failure))) return false;
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null || KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "The authored successor's main ground already has paid construction in hand.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < context.NewEntry.CostDrams)
			{
				Failure = "The work would cost {{C|" + context.NewEntry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, context.NewEntry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(context.NewEntry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(Z, context.NewEntry.Key);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketConvert, mainCell, Building,
				context.NewEntry.Key, payload,
				context.NewEntry.CostDrams, claim);
			if (!KingdomConstruction.FreezeBuildTruth(job, System,
				context.NewEntry.Defence, true))
			{
				water.Rollback();
				materials.Cancel();
				Failure = "The converted plot's exact build effects could not be frozen.";
				return false;
			}
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			KingdomConstruction.Bind(Building, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("convert plot");
				System.Ledger.Note("{{r|The conversion receipt remains outstanding. The old work stands until its exact claim is settled.}}");
				return true;
			}
			if (!ProjectConvertOrder(System, Z, Building, context.NewEntry.Key, Prepared.SkinKey,
				job, out job, out string strikeFailure))
			{
				KingdomGovernanceScope.Commit("convert plot");
				System.Ledger.Note("{{r|The paid conversion could not yet be given to the striking crew. Its receipt remains queued.}}");
				KingdomLog.Log("construction: conversion order waits: " + strikeFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("convert plot");
			KingdomSocketRules.ChangeKind kind = context.Kind;
			string verb = KingdomSocketRules.VerbFor(kind);
			string name = Building.ShortDisplayName;
			KingdomChronicle.Record(System, "the founder ordered the " + name + " of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name));
			System.Ledger.Note("{{G|The " + name + " is to become " + XRL.Language.Grammar.A(context.NewEntry.Name) + ". The crew is set to strike it.}}");
			MessageQueue.AddPlayerMessage("{{G|The " + name + " is ordered " + verb + " into " + XRL.Language.Grammar.A(context.NewEntry.Name) + ".}}");
			KingdomLog.Log("socket: convert ordered " + context.OldEntry.Key + " -> " + context.NewEntry.Key + " at " + System.SeatName);
			return true;
		}

		private static bool ProjectConvertOrder(KingdomSystem System, Zone Z,
			GameObject Building, string NewKey, string NewSkinKey, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Building) || Building.CurrentZone != Z)
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false,
					"The conversion's predecessor is absent.");
				return false;
			}
			if (KingdomConstruction.HasReceipt(Building, Job)
				&& Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) > 0
				&& Building.GetStringProperty(PendingConvertKeyProperty) == NewKey)
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				return true;
			}
			if (!KingdomConstructionRules.TryReadBuildTruth(Job, out bool hasPlot,
				out bool frontier, out _) || !hasPlot || frontier)
			{
				Failure = "The unprojected legacy conversion predates frozen plotted build effects.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			if (!KingdomMaterials.OrderStrikeForConstruction(System, Z, Building, Updated,
				out Updated, out Failure))
			{
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			Building.SetStringProperty(PendingConvertKeyProperty, NewKey);
			Building.SetStringProperty(PendingConvertSkinProperty, NewSkinKey, RemoveIfNull: true);
			KingdomConstruction.Bind(Building, Updated);
			if (Building.CurrentZone != Z
				|| Building.GetIntProperty(KingdomMaterials.StrikeEffortProperty) <= 0
				|| Building.GetStringProperty(PendingConvertKeyProperty) != NewKey)
			{
				Failure = "The conversion strike order could not be verified.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.FinishProjection(ref Updated, true, true);
			return true;
		}

		// ==================================================================================
		// The strike's own completion: leave a socket, or finish a conversion
		// ==================================================================================

		/// <summary>
		/// Projects only after the durable strike receipt proves the exact predecessor and every
		/// receipt-matched plot part absent. A pending reload adopts only the frozen output ID.
		/// </summary>
		internal static bool ResumeStrikeSuccessor(KingdomSystem System, Zone Z,
			KingdomStrikeIntent Intent, bool FreshAttempt, ref KingdomConstructionJob Job,
			out bool Converted, out string Failure)
		{
			Converted = Job != null && Job.Route == KingdomConstructionRoute.SocketConvert;
			Failure = null;
			if (System == null || Z == null || Intent == null || Job == null
				|| !KingdomConstruction.Owns(System, Z, Job)
				|| Job.PhysicalPhase != KingdomPhysicalPhase.SuccessorPending)
			{
				Failure = "The strike successor receipt is not current.";
				return false;
			}
			GameObject source;
			KingdomPhysicalLookupState sourceState = KingdomConstruction.FindExactId(
				Z, Job.SourceId, out source);
			if (sourceState != KingdomPhysicalLookupState.Absent)
			{
				Failure = sourceState == KingdomPhysicalLookupState.Ambiguous
					? "The strike predecessor ID is duplicated in its loaded owner zone."
					: "The strike successor waits on exact predecessor absence.";
				return false;
			}
			if (!Intent.HasPlot)
			{
				if (Converted)
				{
					Failure = "A conversion receipt has no frozen plot rectangle.";
					return false;
				}
				return string.IsNullOrEmpty(Job.OutputId);
			}
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(Intent.X1,
				Intent.Y1, Intent.X2, Intent.Y2);
			if (HasStrikePlotParts(Z, rect, Intent.PlotId))
			{
				Failure = "The strike successor waits on exact plot-part absence.";
				return false;
			}
			GameObject receiptObject;
			KingdomPhysicalLookupState receiptState = KingdomConstruction.FindReceipt(
				Z, Job, out receiptObject);
			if (receiptState == KingdomPhysicalLookupState.Ambiguous)
			{
				Failure = "More than one physical object carries the strike successor receipt.";
				return false;
			}
			if (!string.IsNullOrEmpty(Job.OutputId))
			{
				GameObject exact;
				KingdomPhysicalLookupState exactState = KingdomConstruction.FindExactId(
					Z, Job.OutputId, out exact);
				if (exactState == KingdomPhysicalLookupState.Exact
					&& GameObject.Validate(exact) && exact.ID == Job.OutputId
					&& exact.CurrentZone == Z && KingdomConstruction.HasReceipt(exact, Job))
				{
					if (Converted && ExactConversionOutput(exact, Z, Job)) return true;
					if (!Converted && ExactSocketOutput(exact, Z, rect, Intent, Job)) return true;
				}
				Failure = "The frozen strike successor ID is absent, replaced, or malformed.";
				return false;
			}
			if (!FreshAttempt)
			{
				Failure = "An interrupted strike successor has no frozen generated ID.";
				return false;
			}
			if (receiptState == KingdomPhysicalLookupState.Exact)
			{
				Failure = "A foreign object already carries the strike successor receipt.";
				return false;
			}
			if (Converted)
			{
				if (!KingdomPlots.TryDecodePlotPayload(Job.Payload, out var paidRect,
					out var skinKey, out KingdomArchitectureIntent architecture,
					out bool legacyArchitecture, out _)
					|| (!legacyArchitecture && (architecture == null
						|| architecture.BuildKey != Job.TargetKey
						|| Job.X != architecture.MainWorldX
						|| Job.Y != architecture.MainWorldY))
					|| (legacyArchitecture
					&& (Job.X != paidRect.CenterX || Job.Y != paidRect.CenterY))
					|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)
					|| !KingdomPlots.TryGetSpec(Job.TargetKey, out var spec))
				{
					Failure = "The paid conversion no longer matches its frozen fresh site.";
					return false;
				}
				if (!KingdomPlots.ProjectOnRect(System, Z, paidRect, entry, spec, skinKey, Job,
					out GameObject works, out KingdomConstructionJob updated, out Failure))
				{
					Job = updated;
					return false;
				}
				Job = updated;
				if (!KingdomConstruction.Owns(System, Z, Job)
					|| KingdomConstruction.FindExactId(Z, Job.OutputId, out var exactWorks)
						!= KingdomPhysicalLookupState.Exact
					|| !ReferenceEquals(exactWorks, works)
					|| !ExactConversionOutput(works, Z, Job))
				{
					Failure = "The conversion callback did not retain its exact generated works.";
					return false;
				}
				if (Job.SubjectId != works.ID
					&& !KingdomConstruction.UpdateSubject(ref Job, works.ID))
				{
					Failure = "The conversion works identity could not replace the absent predecessor.";
					return false;
				}
				return true;
			}

			Cell cell = Z.GetCell(rect.CenterX, rect.CenterY);
			GameObject marker = cell == null ? null : GameObject.Create(SocketBlueprint);
			r_KingdomSocket part = marker?.GetPart<r_KingdomSocket>();
			if (!GameObject.Validate(marker) || part == null)
			{
				marker?.Obliterate(null, Silent: true);
				Failure = "The exact cleared-plot marker could not be created.";
				return false;
			}
			if (!KingdomConstruction.Owns(System, Z, Job)
				|| !KingdomConstruction.IsCurrent(Job)
				|| KingdomConstruction.FindExactId(Z, Job.SourceId, out _)
					!= KingdomPhysicalLookupState.Absent
				|| KingdomConstruction.FindReceipt(Z, Job, out _)
					!= KingdomPhysicalLookupState.Absent
				|| HasStrikePlotParts(Z, rect, Intent.PlotId))
			{
				marker.Obliterate(null, Silent: true);
				Failure = "Strike successor authority or frozen topology changed during creation.";
				return false;
			}
			part.LastDesignKey = Intent.BuildKey;
			KingdomPlots.StampRect(marker, rect);
			if (!KingdomConstruction.UpdateOutput(ref Job, marker.ID))
			{
				marker.Obliterate(null, Silent: true);
				Failure = "The cleared-plot marker ID could not be published before insertion.";
				return false;
			}
			KingdomConstruction.Bind(marker, Job);
			GameObject accepted;
			try { accepted = cell.AddObject(marker); }
			catch (Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
				Failure = "Cleared-plot marker insertion threw: " + ex.Message;
				return false;
			}
			KingdomSurvey.ObserveAddResultInActive(Z, marker, accepted);
			if (!ReferenceEquals(accepted, marker)
				|| !KingdomConstruction.IsCurrent(Job) || !KingdomConstruction.Owns(System, Z, Job)
				|| KingdomConstruction.FindExactId(Z, Job.OutputId, out var exactMarker)
					!= KingdomPhysicalLookupState.Exact
				|| !ReferenceEquals(exactMarker, marker)
				|| !ExactSocketOutput(marker, Z, rect, Intent, Job))
			{
				Failure = "The cleared-plot marker was vetoed, replaced, or moved.";
				return false;
			}
			return true;
		}

		private static bool HasStrikePlotParts(Zone Z, KingdomPlotRules.PlotRect Rect,
			string PlotId)
		{
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null)
			{
				for (int i = 0; i < active.PlotParts.Count; i++)
				{
					GameObject item = active.PlotParts[i];
					Cell cell = item?.CurrentCell;
					if (GameObject.Validate(item) && cell != null
						&& cell.X >= Rect.X1 && cell.X <= Rect.X2
						&& cell.Y >= Rect.Y1 && cell.Y <= Rect.Y2
						&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
						return true;
				}
				return false;
			}
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> objects = cell.GetObjects();
					for (int i = 0; i < objects.Count; i++)
					{
						GameObject item = objects[i];
						if (GameObject.Validate(item)
							&& item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
							&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
							return true;
					}
				}
			}
			return false;
		}

		private static bool ExactConversionOutput(GameObject Output, Zone Z,
			KingdomConstructionJob Job)
		{
			if (Z == null || Job == null || !KingdomPlots.TryDecodePlotPayload(Job.Payload,
				out KingdomPlotRules.PlotRect rect, out _,
				out KingdomArchitectureIntent architecture, out bool legacyArchitecture, out _)
				|| (!legacyArchitecture && (architecture == null
					|| architecture.BuildKey != Job.TargetKey
					|| Job.X != architecture.MainWorldX || Job.Y != architecture.MainWorldY))
				|| (legacyArchitecture && (Job.X != rect.CenterX || Job.Y != rect.CenterY))
				|| !GameObject.Validate(Output) || Output.ID != Job.OutputId
				|| Output.CurrentZone != Z || !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.ExpectedArchitectureReceipt(Output, Z.GetCell(Job.X, Job.Y),
					Job.TargetKey, architecture, legacyArchitecture))
				return false;
			r_KingdomPlotWorks works = Output.GetPart<r_KingdomPlotWorks>();
			return (works != null && works.DesignKey == Job.TargetKey)
				|| (Output.GetIntProperty("KingdomBuilt") == 1
					&& Output.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == Job.TargetKey);
		}

		private static bool ExactSocketOutput(GameObject Output, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomStrikeIntent Intent,
			KingdomConstructionJob Job)
		{
			r_KingdomSocket socket = GameObject.Validate(Output)
				? Output.GetPart<r_KingdomSocket>() : null;
			if (socket == null || Output.ID != Job.OutputId || Output.CurrentZone != Z
				|| Output.CurrentCell != Z.GetCell(Rect.CenterX, Rect.CenterY)
				|| socket.LastDesignKey != Intent.BuildKey
				|| !KingdomConstruction.HasReceipt(Output, Job)
				|| !KingdomPlots.TryReadRect(Output, out var observed)) return false;
			return observed.X1 == Rect.X1 && observed.Y1 == Rect.Y1
				&& observed.X2 == Rect.X2 && observed.Y2 == Rect.Y2;
		}

		/// <summary>
		/// Called from <c>KingdomMaterials.WorkStrike</c> the instant a strike finishes, while
		/// <paramref name="Building"/> still stands and still carries its own stamped rect &mdash;
		/// see MODDING.md / the wiring note in <c>KingdomMaterials.cs</c> for exactly where.
		/// Does nothing, and returns false, for a building that never stood on a plot at all: every
		/// single-cell design in this mod is untouched by any of this.
		/// <para>
		/// For a plot design, this always sweeps the plot's own walls, floor, door, and
		/// furnishings off the rect (everything <c>KingdomPlots.Furnish</c> and
		/// <c>KingdomPlots.Apply</c> stamped with this same plot's own id) before doing anything
		/// else, because a struck plot whose shell is left standing is not a re-buildable slot; it
		/// is dead ground the survey will refuse forever. Then:
		/// </para>
		/// <list type="bullet">
		/// <item>if <see cref="ExecuteConvert"/> staged a true retype, the new design is projected
		/// on the distinct fresh site frozen in its paid receipt, with a new LotId. The old rectangle
		/// is left bare rather than renamed into the successor; one combined line is chronicled and
		/// the caller suppresses its ordinary "struck" message (return value <c>true</c>);</item>
		/// <item>otherwise, or if the restake could not land (a design withdrawn mid-strike, a torn
		/// down zone), the rect is left a plain <see cref="r_KingdomSocket"/> marker and the caller
		/// proceeds exactly as an ordinary strike always has (return value <c>false</c>).</item>
		/// </list>
		/// </summary>
		/// <returns>True when this call fully told the conversion's story and the caller's own
		/// "struck" chronicle/message should not also fire; false for every ordinary strike,
		/// where the caller's own messaging still applies unchanged.</returns>
		public static bool OnCleared(KingdomSystem System, Zone Z, GameObject Building)
		{
			if (System == null || Z == null || !GameObject.Validate(Building)
				|| Building.CurrentZone != Z) return false;
			string receipt = Building.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt)
				|| !KingdomConstruction.TryFind(receipt, out var construction)
				|| !KingdomConstruction.Owns(System, Z, construction)
				|| KingdomConstructionRules.IsTerminal(construction.Phase)
				|| (construction.Route != KingdomConstructionRoute.Strike
					&& construction.Route != KingdomConstructionRoute.SocketConvert)
				|| construction.SourceId != Building.ID
				|| construction.PhysicalPhase == KingdomPhysicalPhase.None
				|| !KingdomConstructionRules.TryDecodeStrikeIntent(
					construction.PhysicalReceipt, out var intent)) return false;
			if (intent.HasPlot)
			{
				if (!KingdomPlots.TryReadRect(Building, out var rect)
					|| rect.X1 != intent.X1 || rect.Y1 != intent.Y1
					|| rect.X2 != intent.X2 || rect.Y2 != intent.Y2
					|| Building.GetStringProperty(KingdomPlots.PlotIdProperty) != intent.PlotId)
					return false;
			}
			// Legacy hook owns no destructive mutation. Durable strike inspector alone may advance.
			KingdomMaterials.InspectConstruction(System, Z, construction);
			return false;
		}

		/// <summary>Removes every object this plot raised over its own rect &mdash; walls, floor,
		/// door, contents &mdash; leaving bare cells. Scoped to <paramref name="PlotId"/> so a
		/// neighbouring plot's own lane, which never overlaps this rect by construction (
		/// <c>KingdomPlotRules.CrowdsExisting</c>), is never touched even if IDs collide across
		/// zones somehow. Only ever objects the settlement itself created and marked
		/// (<c>KingdomPlots.PlotPartProperty</c>) &mdash; the protection law's own exemption,
		/// exercised here the same way striking already exercises it on the building itself.</summary>
		private static bool TrySweepLegacyPlotParts(Zone Z,
			KingdomPlotRules.PlotRect Rect, string PlotId, GameObject Owner)
		{
			if (Z == null || !GameObject.Validate(Owner)
				|| Owner.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)) return false;
			List<GameObject> targets = new List<GameObject>();
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (item == null || !GameObject.Validate(item) || item.GetIntProperty(KingdomPlots.PlotPartProperty) != 1)
						{
							continue;
						}
						if (!string.IsNullOrEmpty(PlotId) && item.GetStringProperty(KingdomPlots.PlotIdProperty) != PlotId)
						{
							continue;
						}
						if (item.Inventory != null && item.Inventory.Objects.Count != 0) return false;
						LiquidVolume liquid = item.GetPart<LiquidVolume>();
						if (liquid != null && liquid.Volume > 0) return false;
						if (item.GetIntProperty("KingdomCitizen") == 1
							|| item.GetIntProperty("KingdomStores") == 1
							|| item.GetIntProperty("KingdomLarder") == 1
							|| item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
							return false;
						targets.Add(item);
					}
				}
			}
			for (int i = 0; i < targets.Count; i++)
			{
				GameObject target = targets[i];
				if (!GameObject.Validate(target))
					return false;
				bool removed = target.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(target))
					KingdomSurvey.ObserveRemovedFromActive(Z, target);
				if (!removed || GameObject.Validate(target)) return false;
			}
			return true;
		}

		/// <summary>Leaves a socket marker at the rect's centre, stamped with the rect itself so
		/// every later siting pass counts it exactly as it counts a standing plot.</summary>
		private static void LeaveSocket(Zone Z, KingdomPlotRules.PlotRect Rect, string OldKey,
			KingdomConstructionJob Job = null)
		{
			Cell cell = Z.GetCell(Rect.CenterX, Rect.CenterY);
			if (cell == null)
			{
				return;
			}
			GameObject marker = GameObject.Create(SocketBlueprint);
			if (marker == null)
			{
				return;
			}
			r_KingdomSocket part = marker.GetPart<r_KingdomSocket>();
			if (part == null)
			{
				marker.Obliterate();
				return;
			}
			part.LastDesignKey = OldKey;
			KingdomPlots.StampRect(marker, Rect);
			if (Job != null)
			{
				KingdomConstruction.Bind(marker, Job);
			}
			GameObject accepted;
			try { accepted = cell.AddObject(marker); }
			catch
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
				throw;
			}
			KingdomSurvey.ObserveAddResultInActive(Z, marker, accepted);
			if (marker.CurrentCell != cell)
			{
				bool removed = marker.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(marker))
					KingdomSurvey.ObserveRemovedFromActive(Z, marker);
			}
		}

		// ==================================================================================
		// Building fresh on ground a strike already cleared
		// ==================================================================================

		private sealed class PreparedSocketBuild
		{
			public string MarkerId;
			public string SkinKey;
			public KingdomRules.BuildEntry Entry;
			public KingdomPlotRules.PlotRect Rect;
			public KingdomArchitectureIntent Architecture;
			public string Payload;
			public long LabourTicks;
		}

		/// <summary>
		/// Stakes a design on ground a strike already left a socket on. Runs every check an
		/// ordinary commission runs &mdash; style, stage, footprint, sky, zoning, water, material
		/// &mdash; minus the two that make no sense for ground the settlement already claimed
		/// (the plan cap and the road budget: this is not new plotted area, it is the same rect
		/// coming back into use) and pays the design's own full cost, with no strike effort added
		/// on top &mdash; nothing stands here to take down.
		/// </summary>
		public static bool BuildOnSocket(KingdomSystem System, Zone Z, GameObject Marker, string Key, string SkinKey, out string Failure)
		{
			if (!TryPrepareSocketBuild(System, Z, Marker, Key, SkinKey,
				out PreparedSocketBuild prepared, out Failure)) return false;
			return ExecuteSocketBuild(System, Z, Marker, prepared, out Failure);
		}

		private static bool TryPrepareSocketBuild(KingdomSystem System, Zone Z,
			GameObject Marker, string Key, string SkinKey, out PreparedSocketBuild Prepared,
			out string Failure)
		{
			Prepared = null;
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A plot is raised on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Marker == null || !GameObject.Validate(Marker) || Marker.CurrentZone == null || Marker.CurrentZone.ZoneID != Z.ZoneID || Marker.GetPart<r_KingdomSocket>() == null)
			{
				Failure = "There is no cleared plot there to build on.";
				return false;
			}
			if (HasBlockingReceipt(Marker))
			{
				Failure = "That cleared plot already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketBuild, Marker))
			{
				Failure = "That cleared plot already has a construction receipt in hand.";
				return false;
			}
			if (!KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect rect))
			{
				Failure = "That ground cannot be read.";
				return false;
			}
			if (!KingdomData.TryGetBuilding(Key, out KingdomRules.BuildEntry entry))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlots.TryGetSpec(Key, out KingdomPlotRules.PlotSpec spec))
			{
				Failure = KingdomSocketRules.RefuseNotAPlot(entry.Name);
				return false;
			}
			if (!KingdomRules.StyleAllows(entry.Styles, System.Style))
			{
				Failure = "The " + entry.Name + " is not built in this city's own style.";
				return false;
			}
			Failure = KingdomCommission.StageRefusal(System, entry);
			if (Failure != null)
			{
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(spec.Size, KingdomPresentation.Rich(System.SeatName), System.Stage);
				return false;
			}
			if (!KingdomPlotRules.TryDimensions(spec.Size, out int needWidth, out int needHeight))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomSocketRules.FootprintFits(rect.Width, rect.Height, needWidth, needHeight))
			{
				Failure = KingdomSocketRules.RefuseTooSmall(entry.Name, rect.Width, rect.Height, needWidth, needHeight);
				return false;
			}
			// The way down before the weather, for the same reason the conversion path asks it.
			Failure = KingdomDelve.Refusal(System, Z.ZoneID, entry.Key, entry.Name);
			if (Failure != null)
			{
				return false;
			}
			if (KingdomPlotRules.IsUnderground(Z.Z) && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(entry.Name);
				return false;
			}
			if (!KingdomZoning.Permits(System, Z.ZoneID, entry, out string zoningFailure))
			{
				Failure = zoningFailure;
				return false;
			}
			if (!KingdomPlots.TryPreparePlotPayload(System, Z, rect, entry.Key, entry.Category,
				SkinKey,
				out KingdomArchitectureIntent architecture, out string payload, out Failure))
				return false;
			if (!TrySocketBuildLabour(System, Z, rect, entry, spec,
				out long labourTicks, out Failure)) return false;
			Prepared = new PreparedSocketBuild
			{
				MarkerId = Marker.ID, SkinKey = SkinKey, Entry = entry, Rect = rect,
				Architecture = architecture, Payload = payload, LabourTicks = labourTicks
			};
			return true;
		}

		private static bool TrySocketBuildLabour(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry,
			KingdomPlotRules.PlotSpec Spec, out long LabourTicks, out string Failure)
		{
			LabourTicks = 0L;
			Failure = null;
			if (System == null || Z == null || Entry == null || Spec == null)
			{
				Failure = "The cleared plot has no exact labour context.";
				return false;
			}
			KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Z);
			KingdomPlots.HeartFor(Z, Rect, out int heartX, out int heartY);
			KingdomPlotRules.PlotRect footprint = KingdomPlots.FootprintFor(Rect, Spec,
				heartX, heartY);
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			LabourTicks = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks,
					System.ZoneDistricts.Values), grid.CellsOf(Rect), footprint,
				KingdomPlotRules.RoofOnGround(Spec.Roof, carved), carved);
			if (LabourTicks > 0L) return true;
			Failure = "The cleared plot's exact labour quote is empty.";
			return false;
		}

		private static bool ExecuteSocketBuild(KingdomSystem System, Zone Z, GameObject Marker,
			PreparedSocketBuild Prepared, out string Failure)
		{
			Failure = null;
			if (Prepared == null || !GameObject.Validate(Marker)
				|| Marker.ID != Prepared.MarkerId || Marker.CurrentZone != Z
				|| Marker.GetPart<r_KingdomSocket>() == null || HasBlockingReceipt(Marker)
				|| !KingdomPlots.TryReadRect(Marker, out KingdomPlotRules.PlotRect liveRect)
				|| liveRect.X1 != Prepared.Rect.X1 || liveRect.Y1 != Prepared.Rect.Y1
				|| liveRect.X2 != Prepared.Rect.X2 || liveRect.Y2 != Prepared.Rect.Y2)
			{
				Failure = "The previewed cleared plot changed before consent.";
				return false;
			}
			KingdomRules.BuildEntry entry = Prepared.Entry;
			KingdomArchitectureIntent architecture = Prepared.Architecture;
			string payload = Prepared.Payload;
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(entry.Key), KingdomMaterials.BitCostFor(entry.Key),
				KingdomMaterials.ExoticCostFor(entry.Key));
			if (!KingdomArchitectureStamper.TryPreflight(System, Z, architecture, claim,
				out Failure)) return false;
			if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec liveSpec)
				|| !TrySocketBuildLabour(System, Z, Prepared.Rect, entry, liveSpec,
					out long liveLabour, out Failure)
				|| liveLabour != Prepared.LabourTicks)
			{
				if (Failure == null)
					Failure = "The cleared plot's labour changed after its preview.";
				return false;
			}
			Cell mainCell = Z.GetCell(architecture.MainWorldX, architecture.MainWorldY);
			if (mainCell == null || KingdomConstruction.HasActiveAt(System, Z, mainCell))
			{
				Failure = "The authored building's main ground already has paid construction in hand.";
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			if (!KingdomMaterials.CanPay(Z, entry.Key, out string materialFailure))
			{
				Failure = materialFailure;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(entry.CostDrams);
			KingdomMaterialDebit materials = KingdomMaterials.ReservePayment(Z, entry.Key);
			long start = The.Game.TimeTicks;
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketBuild, mainCell, Marker,
				entry.Key, payload, entry.CostDrams, claim, start,
				start + Prepared.LabourTicks);
			if (!KingdomConstruction.FreezeBuildTruth(job, System, entry.Defence, true))
			{
				water.Rollback();
				materials.Cancel();
				Failure = "The cleared plot's exact build effects could not be frozen.";
				return false;
			}
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stores could not cover the work after all.";
				return false;
			}
			KingdomConstruction.Bind(Marker, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("build on cleared plot");
				System.Ledger.Note("{{r|The cleared plot's construction receipt remains outstanding. It will retry without charging any paid claim twice.}}");
				return true;
			}
			ContinueSocketBuild(System, Z, job, true);
			KingdomGovernanceScope.Commit("build on cleared plot");
			if (KingdomConstruction.TryFind(job.Id, out var observed)
				&& observed.Phase == KingdomConstructionPhase.InspectionRequired)
				System.Ledger.Note("{{r|The cleared plot's exact removal or output receipt needs inspection; it will not retry either callback.}}");
			KingdomLog.Log("socket: ordered " + entry.Key + " on cleared ground at "
				+ Prepared.Rect.X1 + "," + Prepared.Rect.Y1);
			return true;
		}

		// ==================================================================================
		// Re-dress: any registered skin, on any standing building, trivially
		// ==================================================================================

		/// <summary>
		/// Applies a registered skin to a standing building. Reads the building's own design
		/// LIVE from the current catalogue rather than from anything cached at the moment it was
		/// raised, which is what lets a skin a mod added after the building went up be offered
		/// here (Addendum 1: "including one a mod added later"). Structural: never changes what
		/// the building is, what it costs to run, or what it produces &mdash; only
		/// <c>Render</c>, through <c>KingdomDesign.ApplyRenderOverrides</c>, unmodified.
		/// </summary>
		public static bool Redress(KingdomSystem System, Zone Z, GameObject Building, string SkinKey, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is re-dressed on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to re-dress.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement re-dresses what it stands behind. That is not one of its buildings.";
				return false;
			}
			if (HasBlockingReceipt(Building))
			{
				Failure = "That building already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketRedress, Building))
			{
				Failure = "That building already has a re-dressing receipt in hand.";
				return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = Building.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
			{
				Failure = KingdomSocketRules.RefuseUnknownDesign(Building.ShortDisplayName);
				return false;
			}
			if (string.IsNullOrEmpty(SkinKey))
			{
				Failure = "Choose a look to re-dress it in.";
				return false;
			}
			KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(entry.Skins, SkinKey);
			if (skin == null)
			{
				Failure = KingdomSocketRules.RefuseUnknownSkin(SkinKey, Building.ShortDisplayName);
				return false;
			}
			KingdomMaterialTally cost = KingdomSocketRules.RedressCost(KingdomMaterials.CostFor(entry.Key));
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(cost);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, claim);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketRedress, Building.CurrentCell, Building,
				entry.Key, SkinKey, 0, claim);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stockpiles could not cover the re-dressing after all.";
				return false;
			}
			KingdomConstruction.Bind(Building, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The re-dressing receipt remains outstanding and will retry without another charge.}}");
				return true;
			}
			if (!KingdomCeremony.PrepareSocketRedressed(System, Building.ShortDisplayName,
				SkinKey, ref job))
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The paid re-dressing telling could not be frozen safely. Its receipt needs inspection.}}");
				return true;
			}
			if (!ProjectRedress(Building, skin, job, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The paid re-dressing could not yet be verified. Its receipt remains queued.}}");
				KingdomLog.Log("construction: redress waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("redress building");
			KingdomCeremony.DispatchPending(System, ref job);
			KingdomLog.Log("socket: redress " + Building.ShortDisplayName + " (" + entry.Key + ") as " + SkinKey);
			return true;
		}

		private static bool ProjectRedress(GameObject Building,
			KingdomDesignRules.SkinEntry Skin, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Building) || Building.CurrentCell == null
				|| Building.GetIntProperty("KingdomBuilt") != 1 || Skin == null)
			{
				Failure = "The paid building is no longer available to re-dress.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (IsRedressed(Building, Skin))
			{
				KingdomConstruction.Complete(ref Updated);
				return true;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			KingdomConstruction.Bind(Building, Updated);
			KingdomDesign.ApplyRenderOverrides(Building, Skin.ColorString, Skin.DetailColor,
				Skin.RenderString, Skin.Tile);
			if (!IsRedressed(Building, Skin))
			{
				Failure = "The new appearance could not be verified on the paid building.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.Complete(ref Updated);
			return true;
		}

		private static bool IsRedressed(GameObject Building, KingdomDesignRules.SkinEntry Skin)
		{
			Render render = GameObject.Validate(Building) ? Building.GetPart<Render>() : null;
			return render != null && Building.CurrentCell != null
				&& (string.IsNullOrEmpty(Skin.ColorString) || render.ColorString == Skin.ColorString)
				&& (string.IsNullOrEmpty(Skin.DetailColor) || render.DetailColor == Skin.DetailColor)
				&& (string.IsNullOrEmpty(Skin.RenderString) || render.RenderString == Skin.RenderString)
				&& (string.IsNullOrEmpty(Skin.Tile) || render.Tile == Skin.Tile);
		}

		// ==================================================================================
		// Charter entry points
		// ==================================================================================

		private static void CollectNearby(Cell Anchor, List<GameObject> Into, Func<GameObject, bool> Predicate)
		{
			if (Anchor == null)
			{
				return;
			}
			foreach (GameObject item in Anchor.GetObjects())
			{
				if (Predicate(item) && !Into.Contains(item))
				{
					Into.Add(item);
				}
			}
		}

		/// <summary>
		/// The Charter's "change what a plot is" action. Standing beside a work the settlement
		/// raised offers a conversion; standing beside a cleared socket offers to build fresh on
		/// it. Both list only designs a plot may actually be raised as, and a live conversion's
		/// list is annotated with Addendum 2's own verb (<c>change</c>/<c>re-type</c>) for each
		/// choice before the founder commits to one.
		/// </summary>
		public static void OpenConvert(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A plot is changed on the kingdom's own ground.");
				return;
			}
			List<GameObject> buildings = new List<GameObject>();
			List<GameObject> sockets = new List<GameObject>();
			Func<GameObject, bool> isBuilding = o => o.GetIntProperty("KingdomBuilt") == 1 && KingdomPlots.TryReadRect(o, out _);
			Func<GameObject, bool> isSocket = o => o.GetPart<r_KingdomSocket>() != null;
			CollectNearby(cell, buildings, isBuilding);
			CollectNearby(cell, sockets, isSocket);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, buildings, isBuilding);
				CollectNearby(adjacent, sockets, isSocket);
			}
			if (buildings.Count == 0 && sockets.Count == 0)
			{
				Popup.Show("Stand beside a plot " + KingdomPresentation.Rich(System.SeatName) + " raised, or ground it cleared, to change what stands there.");
				return;
			}
			List<string> options = new List<string>();
			List<GameObject> targets = new List<GameObject>();
			for (int i = 0; i < buildings.Count; i++)
			{
				options.Add(buildings[i].ShortDisplayName);
				targets.Add(buildings[i]);
			}
			int socketsStart = targets.Count;
			for (int i = 0; i < sockets.Count; i++)
			{
				options.Add("{{K|a cleared plot}}");
				targets.Add(sockets[i]);
			}
			int picked = Popup.PickOption(Title: "Change what a plot is, at " + KingdomPresentation.Rich(System.SeatName), Options: options.ToArray(), AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = targets[picked];
			bool onSocket = picked >= socketsStart;
			string currentCategory = null;
			string currentKey = null;
			KingdomPlotRules.PlotSize currentSize = KingdomPlotRules.PlotSize.None;
			KingdomArchitectureIntent standingArchitecture = null;
			if (!onSocket)
			{
				currentKey = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				KingdomArchitectureRuntime.TryRead(target, out standingArchitecture, out _);
				if (KingdomData.TryGetBuilding(currentKey, out KingdomRules.BuildEntry oldEntry) && KingdomPlots.TryGetSpec(currentKey, out KingdomPlotRules.PlotSpec oldSpec))
				{
					currentCategory = oldEntry.Category;
					if (!KingdomPlots.TryReadRect(target, out KingdomPlotRules.PlotRect actualRect)
						|| !KingdomSocketRules.TryActualSize(actualRect.Width, actualRect.Height,
							out currentSize)) currentSize = oldSpec.Size;
				}
			}
			List<KingdomRules.BuildEntry> available = new List<KingdomRules.BuildEntry>();
			foreach (KingdomRules.BuildEntry entry in KingdomData.Buildings)
			{
				if (!KingdomPlots.TryGetSpec(entry.Key, out KingdomPlotRules.PlotSpec spec) || spec.Size == KingdomPlotRules.PlotSize.None)
				{
					continue;
				}
				// KingdomZoning.Offered rather than the two checks by hand: a settlement that
				// chooses its own next work must not choose a creed-work it has no way to.
				if (!KingdomZoning.Offered(System, entry))
				{
					continue;
				}
				if (!onSocket)
				{
					if (entry.Key == currentKey) continue;
					KingdomSocketRules.ChangeKind shown = KingdomSocketRules.FitsSameSet(
						currentCategory, currentSize, entry.Category, spec.Size)
						? KingdomSocketRules.ChangeKind.SameSet
						: KingdomSocketRules.ChangeKind.Retype;
					if (shown == KingdomSocketRules.ChangeKind.SameSet
						&& (standingArchitecture == null
							|| !KingdomSocketTransitions.TryGet(currentKey, entry.Key,
								standingArchitecture.LotType, standingArchitecture.LotSize, out _)))
						continue;
				}
				else if (KingdomPlots.TryReadRect(target, out KingdomPlotRules.PlotRect socketRect)
					&& KingdomSocketRules.TryActualSize(socketRect.Width, socketRect.Height,
						out KingdomPlotRules.PlotSize socketSize)
					&& !KingdomArchitecture.TryGetMapping(entry.Key, entry.Category,
						(ArchitectureLotSize)(int)socketSize, out _))
					continue;
				available.Add(entry);
			}
			if (available.Count == 0)
			{
				Popup.Show("No plot design is known here.");
				return;
			}
			string[] designOptions = new string[available.Count];
			for (int i = 0; i < available.Count; i++)
			{
				string tag = "";
				if (!onSocket && KingdomPlots.TryGetSpec(available[i].Key, out KingdomPlotRules.PlotSpec size))
				{
					KingdomSocketRules.ChangeKind shown = KingdomSocketRules.FitsSameSet(
						currentCategory, currentSize, available[i].Category, size.Size)
						? KingdomSocketRules.ChangeKind.SameSet
						: KingdomSocketRules.ChangeKind.Retype;
					if (shown == KingdomSocketRules.ChangeKind.SameSet
						&& KingdomSocketTransitions.TryGet(currentKey, available[i].Key,
							standingArchitecture.LotType, standingArchitecture.LotSize,
							out KingdomSocketTransition transition))
					{
						string material = transition.Materials?.Describe();
						tag = " {{C|[change: " + transition.WaterDrams + " drams"
							+ (material == null ? "" : "; " + material)
							+ "; " + transition.WorkTicks + " ticks]}}";
					}
					else tag = " {{C|[re-type: full build " + available[i].CostDrams
						+ " drams]}}";
				}
				designOptions[i] = available[i].DisplayName
					+ (onSocket ? " {{C|[" + available[i].CostDrams + " drams]}}" : "") + tag;
			}
			int designPicked = Popup.PickOption(Title: onSocket ? "Build on the cleared plot" : ("Change the " + target.ShortDisplayName + " into"), Options: designOptions, AllowEscape: true);
			if (designPicked < 0)
			{
				return;
			}
			KingdomRules.BuildEntry chosen = available[designPicked];
			string skinKey = KingdomDesign.ChooseSkin(chosen, System.Style)?.Key;
			if (onSocket)
			{
				if (!TryPrepareSocketBuild(System, zone, target, chosen.Key, skinKey,
					out PreparedSocketBuild socketBuild, out string socketFailure)
					|| !KingdomArchitecturePreview.TryRender(socketBuild.Architecture, chosen,
						socketBuild.LabourTicks, out string socketPreview, out socketFailure))
				{
					Popup.Show(socketFailure);
					return;
				}
				int socketConfirmed = Popup.PickOption(Title: "Build exact plan: " + chosen.Name,
					Intro: socketPreview, Options: new string[1] { "Build this exact plan" },
					AllowEscape: true);
				if (socketConfirmed < 0) return;
				if (!ExecuteSocketBuild(System, zone, target, socketBuild, out socketFailure))
					Popup.Show(socketFailure);
				return;
			}
			if (!TryPrepareConvert(System, zone, target, chosen.Key, skinKey,
				out PreparedConvert conversion, out string assessFailure))
			{
				Popup.Show(assessFailure);
				return;
			}
			string productionPreview;
			bool rendered = conversion.Context.Kind == KingdomSocketRules.ChangeKind.SameSet
				? KingdomArchitecturePreview.TryRenderTransition(conversion.Architecture, chosen,
					conversion.Context.Transition, conversion.Delta, out productionPreview,
					out assessFailure)
				: KingdomArchitecturePreview.TryRenderRetype(conversion.Architecture, chosen,
					conversion.Quote, out productionPreview, out assessFailure);
			if (!rendered)
			{
				Popup.Show(assessFailure);
				return;
			}
			string question = productionPreview + "\n"
				+ KingdomSocketRules.DescribeConversion(target.ShortDisplayName, chosen.Name,
					conversion.Context.Kind, conversion.Quote);
			int confirmed = Popup.PickOption(Title: "Preview exact change: " + chosen.Name,
				Intro: question, Options: new string[1] { "Order this exact change" },
				AllowEscape: true);
			if (confirmed < 0) return;
			if (!ExecutePreparedConvert(System, zone, target, conversion,
				out string executeFailure))
			{
				Popup.Show(executeFailure);
			}
		}

		/// <summary>The Charter's "give a building a new look" action.</summary>
		public static void OpenRedress(KingdomSystem System, GameObject Founder)
		{
			if (System == null || Founder == null)
			{
				return;
			}
			Zone zone = Founder.CurrentZone;
			Cell cell = Founder.CurrentCell;
			if (zone == null || cell == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A building is re-dressed on the kingdom's own ground.");
				return;
			}
			List<GameObject> candidates = new List<GameObject>();
			Func<GameObject, bool> isBuilding = o => o.GetIntProperty("KingdomBuilt") == 1;
			CollectNearby(cell, candidates, isBuilding);
			foreach (Cell adjacent in cell.GetLocalAdjacentCells())
			{
				CollectNearby(adjacent, candidates, isBuilding);
			}
			if (candidates.Count == 0)
			{
				Popup.Show("Stand beside something " + KingdomPresentation.Rich(System.SeatName) + " stands behind to give it a new look.");
				return;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				options[i] = candidates[i].ShortDisplayName;
			}
			int picked = Popup.PickOption(Title: "Give a building a new look, at " + KingdomPresentation.Rich(System.SeatName), Options: options, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = candidates[picked];
			string key = target.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = target.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry) || entry.Skins == null || entry.Skins.Count == 0)
			{
				Popup.Show("There is no look known for the " + target.ShortDisplayName + " besides its own.");
				return;
			}
			string[] skinOptions = new string[entry.Skins.Count];
			for (int i = 0; i < entry.Skins.Count; i++)
			{
				skinOptions[i] = KingdomDesignRules.DescribeSkinOption(entry.Skins[i], false);
			}
			int skinPicked = Popup.PickOption(Title: "Dress the " + target.ShortDisplayName + " as", Options: skinOptions, AllowEscape: true);
			if (skinPicked < 0)
			{
				return;
			}
			if (!Redress(System, zone, target, entry.Skins[skinPicked].Key, out string failure))
			{
				Popup.Show(failure);
			}
		}
	}
}
