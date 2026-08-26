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
		/// <summary>
		/// Moves everything from the old work into the new one and takes the old work down.
		/// <para>
		/// Carries the contents first &mdash; liquid by its actual mixture, then every held
		/// object &mdash; and only then the settlement's own marks. A dedication is the founder's
		/// decision about a thing, and losing one because the thing improved would be the worst
		/// bug this system could have, so <c>KingdomLarder</c> and <c>KingdomStores</c> are
		/// carried explicitly rather than left to the scaffold's own blueprint-keyed guess.
		/// </para>
		/// <para>
		/// Anything that still will not fit is poured or dropped in the cell rather than
		/// destroyed. That path should be unreachable &mdash;
		/// <see cref="ContentsWouldFit(GameObject, string)"/> refuses the improvement before it
		/// starts &mdash; but water nobody can see is water this mod has quietly invented a
		/// second place to keep.
		/// </para>
		/// </summary>
		/// <param name="Predecessor">The old work. Destroyed once emptied.</param>
		/// <param name="Successor">The new work, already standing.</param>
		/// <param name="SuccessorKey">Registry key to stamp on the new work.</param>
		public static void HandOver(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (!GameObject.Validate(Predecessor) || !GameObject.Validate(Successor))
			{
				return;
			}
			Cell cell = Predecessor.CurrentCell;
			string predecessorId = Predecessor.ID;
			if (cell == null || Successor.CurrentCell != cell
				|| Successor.GetIntProperty(BuiltProperty) != 1)
			{
				return;
			}
			r_KingdomImprovement intent = Predecessor.GetPart<r_KingdomImprovement>();
			if (intent != null && !string.IsNullOrEmpty(intent.SuccessorBlueprint)
				&& Successor.Blueprint != intent.SuccessorBlueprint)
			{
				return;
			}
			if (intent == null || !intent.HandoverFlagsValid()) return;
			if (intent.HandoverSourceId == null && intent.HandoverTargetId == null)
			{
				if (string.IsNullOrEmpty(Predecessor.ID) || Predecessor.ID.Length > 128
					|| string.IsNullOrEmpty(Successor.ID) || Successor.ID.Length > 128) return;
				intent.HandoverSourceId = Predecessor.ID;
				intent.HandoverTargetId = Successor.ID;
			}
			else if (intent.HandoverSourceId != Predecessor.ID
				|| intent.HandoverTargetId != Successor.ID) return;
			string receipt = Predecessor.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt))
			{
				r_KingdomImprovement.FailHandover(intent,
					"Legacy improvement handover lacks a current exact construction receipt.");
				return;
			}
			if (string.IsNullOrEmpty(intent.HandoverConstructionReceipt))
			{
				if (!string.IsNullOrEmpty(receipt))
				{
					if (receipt.Length > 128) return;
					intent.HandoverConstructionReceipt = receipt;
				}
			}
			else if (intent.HandoverConstructionReceipt != receipt) return;
			KingdomConstructionJob job = null;
			KingdomSystem ownerSystem = null;
			KingdomArchitectureIntent authoredSuccessor = null;
			bool authoredUpgrade = false;
			if (!string.IsNullOrEmpty(receipt))
			{
				ownerSystem = The.Game == null
					? null : The.Game.RequireSystem<KingdomSystem>();
				if (!KingdomConstruction.TryFind(receipt, out job)
					|| !KingdomConstruction.Owns(ownerSystem, Predecessor.CurrentZone, job)
					|| job.Route != KingdomConstructionRoute.Improvement
					|| KingdomConstructionRules.IsTerminal(job.Phase)
					|| (job.Phase != KingdomConstructionPhase.Working
						&& job.Phase != KingdomConstructionPhase.ProjectionPending
						&& job.Phase != KingdomConstructionPhase.Outstanding)
					|| job.SubjectId != Predecessor.ID
					|| SuccessorKey != job.TargetKey || intent == null || !intent.Working
					|| intent.Scaffold == null
					|| Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty)
						!= intent.Scaffold.ID
					|| !EnsureExactImprovementPredecessor(ownerSystem, Predecessor.CurrentZone,
						Predecessor, job)
					|| !r_KingdomScaffold.IsExactSuccessor(Successor, Predecessor.CurrentZone,
						cell, job, intent.SuccessorBlueprint)) return;
				string authoredFailure;
				if (!TryReadImprovementArchitecture(Predecessor, job,
					out authoredSuccessor, out authoredUpgrade, out authoredFailure))
				{
					r_KingdomImprovement.FailHandover(intent, authoredFailure
						?? "The frozen authored successor receipt is invalid.");
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return;
				}
				if (!KingdomConstruction.BeginProjection(ref job, out _)) return;
				if (job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
				{
					r_KingdomImprovement.FailHandover(intent,
						"Improvement removal was interrupted before callback-success proof.");
					KingdomConstruction.Quarantine(ref job,
						intent.HandoverFailure);
					return;
				}
				KingdomConstruction.Bind(Successor, job);
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The exact handover endpoints are absent, duplicated, or unauthorized.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return;
			}
			int carriedLiquid;
			int carriedItems;
			if (!TryCarryHandoverContents(Predecessor, Successor, cell, SuccessorKey,
				intent, authoredSuccessor, authoredUpgrade, ref job,
				out carriedLiquid, out carriedItems)) return;
			string predecessorName;
			if (!TryRemoveHandoverPredecessor(Predecessor, Successor, cell, predecessorId,
				SuccessorKey, intent, ownerSystem, ref job, carriedLiquid, carriedItems,
				out predecessorName)) return;
			if (carriedLiquid > 0 || carriedItems > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|Everything the " + predecessorName + " held was moved into " + KingdomDesign.ReferenceFor(Successor, Successor.ShortDisplayName) + ".}}");
			}
			KingdomLog.Log("improvement handover: " + predecessorName + " -> " + Successor.Blueprint + " liquid=" + carriedLiquid + " items=" + carriedItems);
			// A settlement that is standing there watching should be able to watch the next one
			// start, rather than having to walk out and back in. Bounded by the reserve and by
			// free hands exactly as the pass itself is, and it still only ever starts one - and it
			// is a no-op when the handover was itself driven by that pass, which is what keeps
			// "one work per visit" true.
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			Zone zone = Successor.CurrentZone;
			if (system != null && zone != null)
			{
				KingdomSystem.Guard("improvement follow-on", delegate
				{
					OnZoneActivated(system, zone, KingdomSurvey.Take(zone, system));
				});
			}
		}
	}
}
