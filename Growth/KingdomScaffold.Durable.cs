using System;
using System.Globalization;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomScaffold
	{
		/// <summary>Advances one exact receipt-bearing scaffold from the semantic pass.</summary>
		public void AdvanceDurable(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job, long TimeTick)
		{
			if (!ExactPredecessor(System, Z, Job)) return;
			if (!TryInitializeDurableWork(Job, TimeTick, out string workFailure))
			{
				KingdomConstructionJob damaged = Job;
				KingdomConstruction.Quarantine(ref damaged, workFailure);
				return;
			}
			bool ready = RemainingTicks <= 0 && LastWorkedTick > 0;
			if (!ready && Job.Phase == KingdomConstructionPhase.Working)
			{
				ready = AdvanceLabour(TimeTick);
			}
			if (!ready) return;
			ContinueDurable(System, Z, Job);
		}

		/// <summary>Retries a finished scaffold without charging another labour interval.</summary>
		public void RetryDurable(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (!ExactPredecessor(System, Z, Job)) return;
			if (!TryValidateDurableWork(Job, out string workFailure))
			{
				KingdomConstructionJob damaged = Job;
				KingdomConstruction.Quarantine(ref damaged, workFailure);
				return;
			}
			if (RemainingTicks != 0L || LastWorkedTick <= 0L) return;
			long timeTick = The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks;
			if (timeTick < LastWorkedTick) return;
			ContinueDurable(System, Z, Job);
		}

		private void ContinueDurable(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			KingdomConstructionJob current = Job;
			Cell cell = ParentObject.CurrentCell;
			string blueprint = TargetBlueprint;
			string predecessorId = ParentObject.ID;
			if (!ExactPredecessor(System, Z, current) || cell == null || cell.ParentZone != Z
				|| string.IsNullOrEmpty(blueprint)) return;

			GameObject successor;
			int successorCount = FindExactSuccessors(Z, current, blueprint, ParentObject, out successor);
			if (successorCount > 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"More than one exact successor carries the scaffold receipt.");
				return;
			}
			int finalPending = ParentObject.GetIntProperty(FinalPendingProperty);
			if (finalPending != 0 && finalPending != 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold final-projection flag is not an exact boolean.");
				return;
			}
			if (current.Phase == KingdomConstructionPhase.Working
				|| current.Phase == KingdomConstructionPhase.Outstanding)
			{
				if (finalPending != 0)
				{
					KingdomConstruction.Quarantine(ref current,
						"The scaffold final-projection phase and marker disagree.");
					return;
				}
				if (!KingdomConstruction.BeginProjection(ref current, out _)) return;
				ParentObject.SetIntProperty(FinalPendingProperty, 1);
				if (ParentObject.GetIntProperty(FinalPendingProperty) != 1)
				{
					KingdomConstruction.Quarantine(ref current,
						"The scaffold did not retain its final-projection marker.");
					return;
				}
			}
			else if (current.Phase != KingdomConstructionPhase.ProjectionPending
				|| finalPending != 1)
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold final-projection phase and marker disagree.");
				return;
			}
			if (!ExactPredecessor(System, Z, current) || ParentObject.CurrentCell != cell
				|| TargetBlueprint != blueprint || !KingdomConstruction.IsCurrent(current))
			{
				KingdomConstruction.Quarantine(ref current,
					"The scaffold changed across its durable projection boundary.");
				return;
			}

			if (successor == null)
			{
				// A reloaded pending row with no successor is ambiguous. Only the live attempt that
				// just wrote Pending, or a writer-proved Outstanding retry, may create one.
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending)
				{
					KingdomConstruction.Quarantine(ref current,
						"The interrupted final projection has no safely identifiable successor.");
					return;
				}
				try
				{
					successor = GameObject.Create(blueprint);
				}
				catch (Exception ex)
				{
					KingdomConstruction.Quarantine(ref current,
						"The final blueprint threw before creating a successor: " + ex.Message);
					return;
				}
				if (!GameObject.Validate(successor))
				{
					ReturnToOutstanding(ref current,
						"The final blueprint could not create its exact successor.");
					return;
				}
				if (successor.Blueprint != blueprint)
				{
					QuarantineOrRetryAfterAdd(ref current, successor, Z,
						"The final blueprint created an unexpected successor.");
					return;
				}
				if (!KingdomConstruction.UpdateFinalOutput(ref current,
					predecessorId, successor.ID))
				{
						QuarantineOrRetryAfterAdd(ref current, successor, Z,
							"The final successor identity could not be published before AddObject.");
						return;
				}
				try
				{
					PrepareSuccessor(successor, current);
				}
				catch (Exception ex)
				{
					QuarantineOrRetryAfterAdd(ref current, successor, Z,
						"The final successor threw while it was staged: " + ex.Message);
					return;
				}
				GameObject accepted = null;
				try
				{
					accepted = cell.AddObject(successor);
					successor.MakeActive();
				}
				catch (Exception ex)
				{
					QuarantineOrRetryAfterAdd(ref current, successor, Z,
						"The final successor threw while entering its cell: " + ex.Message);
					return;
				}
				finally
				{
					KingdomSurvey.ObserveAddResultInActive(Z, successor, accepted);
				}
				if (!ReferenceEquals(accepted, successor))
				{
					QuarantineOrRetryAfterAdd(ref current, successor, Z,
						"The final successor AddObject replaced its exact return identity.");
					return;
				}
				if (!IsExactSuccessor(successor, Z, cell, current, blueprint,
					current.Route == KingdomConstructionRoute.Improvement ? ParentObject : null))
				{
					QuarantineOrRetryAfterAdd(ref current, successor, Z,
						"The final successor could not be observed exactly after AddObject.");
					return;
				}
			}

			// Gatehouse EnteredCell is its own durable six-output transaction. A callback cut
			// leaves this paid root and predecessor standing; resume it before any scaffold cut.
			bool gatehouse = KingdomGatehouseRules.IsGatehouse(current.TargetKey);
			if (gatehouse && !KingdomGatehouse.TryResumeProjection(successor, cell)) return;

			// AddObject and MakeActive are callbacks. Re-read both endpoints before removal.
			if (!ExactPredecessor(System, Z, current) || ParentObject.CurrentCell != cell
				|| TargetBlueprint != blueprint
				|| !IsExactSuccessor(successor, Z, cell, current, blueprint,
					current.Route == KingdomConstructionRoute.Improvement ? ParentObject : null)
				|| (gatehouse && !KingdomGatehouse.ProjectionComplete(successor, Z))
				|| !KingdomConstruction.IsCurrent(current))
			{
				KingdomConstruction.Quarantine(ref current,
					"A construction endpoint changed before predecessor removal.");
				return;
			}
			if (!TryPublishScaffoldRemovalIntent(successor, predecessorId))
			{
				KingdomConstruction.Quarantine(ref current,
					"The successor could not publish exact scaffold-removal intent.");
				return;
			}
			bool removed = false;
			string removalException = null;
			try
			{
				removed = ParentObject.Destroy(null, Silent: true);
			}
			catch (Exception ex)
			{
				removalException = ex.Message;
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Z, ParentObject);
			KingdomPhysicalLookupState predecessorState = KingdomConstruction.FindGlobalLiveId(
				predecessorId, out GameObject afterPredecessor);
			bool exactReference = ReferenceEquals(afterPredecessor, ParentObject);
			bool originalValid = GameObject.Validate(ParentObject);
			bool exactShape = exactReference && originalValid
				&& ExactPredecessor(System, Z, current) && ParentObject.CurrentCell == cell
				&& TargetBlueprint == blueprint
				&& IsExactSuccessor(successor, Z, cell, current, blueprint,
					current.Route == KingdomConstructionRoute.Improvement ? ParentObject : null)
				&& HasExactScaffoldRemovalIntent(successor, predecessorId)
				&& (!gatehouse || KingdomGatehouse.ProjectionComplete(successor, Z));
			KingdomExactRemovalAction aftermath =
				KingdomConstructionRules.ScaffoldRemovalAftermath(predecessorState,
					exactReference, exactShape, originalValid);
			if (aftermath == KingdomExactRemovalAction.InvokeOnce)
			{
				ReturnToOutstanding(ref current, "The exact successor stands, but scaffold removal "
					+ (removed ? "reported success without an effect"
						: removalException == null ? "was vetoed" : "threw before taking effect") + ".");
				return;
			}
			if (aftermath != KingdomExactRemovalAction.ProvedAbsent)
			{
				KingdomConstruction.Quarantine(ref current,
					"Scaffold removal moved, replaced, duplicated, or ambiguously changed an endpoint."
					+ (removalException == null ? "" : " Callback threw: " + removalException));
				return;
			}
			KingdomSurvey.ObserveRemovedFromActive(Z, ParentObject);
			if (!TryCommitScaffoldRemovalProof(System, Z, successor, ParentObject, blueprint,
				predecessorId, ref current, out string proofFailure))
			{
				KingdomConstruction.Quarantine(ref current,
					proofFailure ?? "The successor could not commit scaffold-removal proof.");
				return;
			}

			if (current.Route == KingdomConstructionRoute.Improvement)
			{
				KingdomConstruction.FinishProjection(ref current, true, true);
				return;
			}
			if (KingdomConstruction.Complete(ref current))
			{
				TellCompletion(System, successor, current);
			}
		}

		private void ReturnToOutstanding(ref KingdomConstructionJob Job, string Failure)
		{
			if (!KingdomConstruction.FinishProjection(ref Job, false, false, Failure)) return;
			ParentObject.SetIntProperty(FinalPendingProperty, 0);
			if (ParentObject.GetIntProperty(FinalPendingProperty) != 0)
			{
				KingdomConstruction.Quarantine(ref Job,
					Failure + " The final-projection marker could not be cleared.");
			}
		}

	}
}
