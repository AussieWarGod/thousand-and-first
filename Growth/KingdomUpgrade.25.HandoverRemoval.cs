using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private static bool TryRemoveHandoverPredecessor(GameObject Predecessor,
			GameObject Successor, Cell cell, string predecessorId, string SuccessorKey,
			r_KingdomImprovement intent, KingdomSystem ownerSystem,
			ref KingdomConstructionJob job, int carriedLiquid, int carriedItems,
			out string predecessorName)
		{
			predecessorName = null;
			// The successor was indexed when the scaffold first landed it. Plot growth and
			// CarryMarks happen later and can add plot, larder, stores, yielding, visual, and other
			// semantic memberships. Refresh the same bound index before any exact removal proof or
			// the immediate improvement follow-on consumes it.
			KingdomSurvey activeSurvey = KingdomSurvey.ActiveFor(Successor.CurrentZone);
			if (activeSurvey != null && !activeSurvey.ObserveChanged(Successor))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The improved successor could not refresh the active settlement survey.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			predecessorName = KingdomDesign.ReferenceFor(Predecessor, Predecessor.ShortDisplayName);
			LiquidVolume remaining = Predecessor.GetPart<LiquidVolume>();
			if (remaining != null && remaining.Volume > 0 && cell != null)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Liquid reappeared after the exact handover receipt settled.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			if (!GameObject.Validate(Predecessor) || Predecessor.CurrentCell != cell
				|| Successor.CurrentCell != cell || Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey
				|| (job != null && (!KingdomConstruction.HasReceipt(Predecessor, job)
					|| !r_KingdomScaffold.IsExactSuccessor(Successor,
						Predecessor.CurrentZone, cell, job, intent.SuccessorBlueprint)
						|| !KingdomConstruction.Owns(ownerSystem, Predecessor.CurrentZone, job)
						|| KingdomConstruction.FindExactId(Predecessor.CurrentZone,
							predecessorId, out exactPredecessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactPredecessor, Predecessor)
						|| KingdomConstruction.FindExactId(Predecessor.CurrentZone,
							Successor.IDIfAssigned, out exactSuccessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactSuccessor, Successor)
						|| Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty)
						!= intent.Scaffold.IDIfAssigned
					|| !KingdomConstruction.IsCurrent(job))))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The improved successor could not be verified before handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (job != null && !KingdomConstruction.UpdatePhysical(ref job,
				KingdomPhysicalPhase.FinalRemovalPending, carriedItems, carriedLiquid, 0,
				predecessorId, Successor.IDIfAssigned, "improvement-handover:v1"))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The final predecessor-removal intent could not be published exactly.");
				if (job != null && KingdomConstruction.IsCurrent(job))
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			bool removed;
			try
			{
				removed = Predecessor.Destroy(null, Silent: true);
			}
			catch (System.Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Successor.CurrentZone, Predecessor);
				r_KingdomImprovement.FailHandover(intent,
					"Improvement predecessor removal threw: " + ex.Message);
				if (job != null)
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (removed && !GameObject.Validate(Predecessor))
				KingdomSurvey.ObserveRemovedFromActive(Successor.CurrentZone, Predecessor);
			KingdomPhysicalLookupState predecessorState = job == null
				? (GameObject.Validate(Predecessor) ? KingdomPhysicalLookupState.Exact
					: KingdomPhysicalLookupState.Absent)
				: KingdomConstruction.FindExactId(Successor.CurrentZone, predecessorId, out _);
			if (!removed || GameObject.Validate(Predecessor)
				|| predecessorState != KingdomPhysicalLookupState.Absent
				|| Successor.CurrentCell != cell)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Improvement removal was vetoed, moved, or partially changed an endpoint.");
				if (job != null)
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (job != null)
			{
					GameObject exactAfter;
					if (!KingdomConstruction.Owns(ownerSystem, Successor.CurrentZone, job)
						|| KingdomConstruction.FindExactId(Successor.CurrentZone,
							Successor.IDIfAssigned, out exactAfter) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactAfter, Successor)
						|| !r_KingdomScaffold.IsExactSuccessor(Successor, Successor.CurrentZone,
						cell, job, intent.SuccessorBlueprint)
						|| !KingdomConstruction.IsCurrent(job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The improvement successor changed during predecessor removal.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return false;
				}
				Successor.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, predecessorId);
				if (!r_KingdomScaffold.HasRemovalProof(Successor, predecessorId))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The improvement successor did not retain predecessor-removal proof.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return false;
				}
				if (!KingdomConstruction.UpdatePhysical(ref job,
					KingdomPhysicalPhase.FinalRemoved, carriedItems, carriedLiquid, 0,
					predecessorId, Successor.IDIfAssigned, "improvement-handover:v1"))
				{
					r_KingdomImprovement.FailHandover(intent,
						"Exact predecessor absence could not be committed to its receipt.");
					if (KingdomConstruction.IsCurrent(job))
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				if (!KingdomConstruction.Complete(ref job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"The physically closed improvement receipt could not complete.");
					if (KingdomConstruction.IsCurrent(job))
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				r_KingdomScaffold.TellCompletion(ownerSystem, Successor, job);
			}
			return true;
		}
	}
}
