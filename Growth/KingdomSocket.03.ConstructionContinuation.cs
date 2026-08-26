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
	}
}
