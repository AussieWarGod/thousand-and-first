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

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		public void PollHandover(long TimeTick)
		{
			if (GameObject.Validate(ref Scaffold))
			{
				return;
			}
			Cell cell = ParentObject?.CurrentCell;
			GameObject successor;
			KingdomPhysicalLookupState successorState = FindSuccessor(cell, out successor);
			if (successorState == KingdomPhysicalLookupState.Ambiguous)
			{
				FailHandover(this, "The improvement successor ID is duplicated or malformed.");
				string duplicateReceipt = ParentObject.GetStringProperty(
					KingdomConstruction.ReceiptProperty);
				if (KingdomConstruction.TryFind(duplicateReceipt, out var duplicate))
					KingdomConstruction.Quarantine(ref duplicate, HandoverFailure);
				return;
			}
			if (successorState == KingdomPhysicalLookupState.Exact)
			{
				string receipt = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
				KingdomConstructionJob job;
				if (!string.IsNullOrEmpty(receipt))
				{
					KingdomSystem system = The.Game == null
						? null : The.Game.RequireSystem<KingdomSystem>();
					if (!KingdomConstruction.TryFind(receipt, out job)
						|| !KingdomConstruction.Owns(system, ParentObject.CurrentZone, job)
						|| job.Route != KingdomConstructionRoute.Improvement
						|| KingdomConstructionRules.IsTerminal(job.Phase)) return;
					KingdomConstruction.Bind(successor, job);
				}
				KingdomUpgrade.HandOver(ParentObject, successor, SuccessorKey);
				return;
			}
			if (TimeTick < WorkCompleteTick + AbandonGraceTicks)
			{
				return;
			}
			// Paid work never evaporates. Publish the missing projection as retryable; the durable
			// construction step can raise the exact same successor without charging again.
			string id = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob outstanding;
			if (!string.IsNullOrEmpty(id) && KingdomConstruction.TryFind(id, out outstanding)
				&& outstanding.Phase != KingdomConstructionPhase.Outstanding)
			{
				KingdomConstruction.FinishProjection(ref outstanding, false, false,
					"The paid improvement scaffold is absent; projection remains outstanding.");
				MessageQueue.AddPlayerMessage("{{r|The improvement scaffold is gone, but its paid receipt remains queued.}}");
				KingdomLog.Log("improvement projection outstanding: " + ParentObject.Blueprint);
			}
		}

		/// <summary>
		/// The finished successor, once it is standing in the same cell. Matched on blueprint and
		/// on the settlement's own build mark, so an unrelated object dropped in the cell mid-
		/// build can never be mistaken for the new work.
		/// </summary>
		/// <param name="Where">Cell this work stands in. Null finds nothing.</param>
		public KingdomPhysicalLookupState FindSuccessor(Cell Where, out GameObject Successor)
		{
			Successor = null;
			if (Where == null || string.IsNullOrEmpty(SuccessorBlueprint))
			{
				return KingdomPhysicalLookupState.Absent;
			}
			string receipt = ParentObject.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (!string.IsNullOrEmpty(receipt))
			{
				if (!KingdomConstruction.TryFind(receipt, out var exactJob)
					|| string.IsNullOrEmpty(exactJob.OutputId))
					return KingdomPhysicalLookupState.Ambiguous;
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(
					Where.ParentZone, exactJob.OutputId, out var candidate);
				if (state != KingdomPhysicalLookupState.Exact) return state;
				if (candidate == ParentObject || candidate.CurrentCell != Where
					|| candidate.Blueprint != SuccessorBlueprint
					|| candidate.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| candidate.GetStringProperty(KingdomUpgrade.BuildKeyProperty) != SuccessorKey
					|| candidate.GetStringProperty(KingdomConstruction.ReceiptProperty) != receipt)
					return KingdomPhysicalLookupState.Ambiguous;
				Successor = candidate;
				return KingdomPhysicalLookupState.Exact;
			}
			List<GameObject> objects = Where.GetObjects();
			int count = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (candidate != ParentObject && candidate.Blueprint == SuccessorBlueprint
					&& candidate.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1
					&& candidate.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == SuccessorKey
					&& string.IsNullOrEmpty(candidate.GetStringProperty(
						KingdomConstruction.ReceiptProperty)))
				{
					count++;
					if (count == 1) Successor = candidate;
				}
			}
			if (count == 0) return KingdomPhysicalLookupState.Absent;
			if (count == 1)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(Where.ParentZone, Successor.ID,
					out global) == KingdomPhysicalLookupState.Exact
					&& ReferenceEquals(global, Successor)) return KingdomPhysicalLookupState.Exact;
			}
			Successor = null;
			return KingdomPhysicalLookupState.Ambiguous;
		}
	}
}
