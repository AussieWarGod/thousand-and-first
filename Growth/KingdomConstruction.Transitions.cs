using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		public static bool BeginProjection(ref KingdomConstructionJob Job, out string Failure)
		{
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.ProjectionPending, null, out Failure);
		}

		public static bool FinishProjection(ref KingdomConstructionJob Job, bool Success,
			bool Working, string Failure = null)
		{
			string ignored;
			return TransitionAndPublish(ref Job,
				Success ? (Working ? KingdomConstructionPhase.Working : KingdomConstructionPhase.Complete)
					: KingdomConstructionPhase.Outstanding,
				Failure, out ignored);
		}

		public static bool Complete(ref KingdomConstructionJob Job, string Failure = null)
		{
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.Complete, Failure, out ignored);
		}

		/// <summary>Quarantines an ambiguous external mutation. No automatic retry may cross it.</summary>
		public static bool Quarantine(ref KingdomConstructionJob Job, string Failure)
		{
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.InspectionRequired,
				Failure, out ignored);
		}

		/// <summary>Publishes the exact live predecessor identity before work may advance.</summary>
		public static bool UpdateSubject(ref KingdomConstructionJob Job, string SubjectId)
		{
			if (Job == null || string.IsNullOrEmpty(SubjectId)
				|| SubjectId.Length > KingdomConstructionRules.MaxSubjectChars) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.SubjectId = SubjectId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		/// <summary>Publishes generated identity before first engine insertion callback.</summary>
		public static bool UpdateOutput(ref KingdomConstructionJob Job, string OutputId)
		{
			if (Job == null || (OutputId != null
				&& OutputId.Length > KingdomConstructionRules.MaxSubjectChars)) return false;
			// Generated identity is a write-once receipt boundary. Once published before an
			// engine callback it may neither be replaced nor cleared: doing either would let a
			// retry bless a different object after an ambiguous Add/Destroy cut.
			if (!string.IsNullOrEmpty(Job.OutputId)
				&& !string.Equals(Job.OutputId, OutputId, StringComparison.Ordinal)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.OutputId = OutputId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		/// <summary>Advances the write-once output slot from an exact generated predecessor
		/// (works/scaffold) to its exact generated final successor. SubjectId retains the old
		/// identity as removal proof; no arbitrary overwrite or second advance is permitted.</summary>
		public static bool UpdateFinalOutput(ref KingdomConstructionJob Job,
			string PredecessorId, string OutputId)
		{
			if (Job == null || string.IsNullOrEmpty(PredecessorId)
				|| string.IsNullOrEmpty(OutputId) || OutputId.Length > KingdomConstructionRules.MaxSubjectChars
				|| Job.OutputId != PredecessorId
				|| (Job.Route != KingdomConstructionRoute.Improvement
					&& Job.SubjectId != PredecessorId)
				|| Job.Phase != KingdomConstructionPhase.ProjectionPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalOutputSettled
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.OutputId = OutputId;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdatePhysical(ref KingdomConstructionJob Job,
			KingdomPhysicalPhase Phase, int Index, int Amount, int Spilled,
			string ItemId, string DestinationId, string Receipt, string Failure = null)
		{
			if (Job == null || Index < 0 || Index > 4096 || Amount < 0 || Spilled < 0
				|| (ItemId != null && ItemId.Length > KingdomConstructionRules.MaxSubjectChars)
				|| (DestinationId != null && DestinationId.Length > KingdomConstructionRules.MaxSubjectChars)
				|| (Receipt != null && Receipt.Length > KingdomConstructionRules.MaxPhysicalReceiptChars)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Failure ?? Job.Failure);
			next.PhysicalPhase = Phase;
			next.PhysicalIndex = Index;
			next.PhysicalAmount = Amount;
			next.PhysicalSpilled = Spilled;
			next.PhysicalItemId = ItemId;
			next.PhysicalDestinationId = DestinationId;
			next.PhysicalReceipt = Receipt;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdateOutbox(ref KingdomConstructionJob Job,
			KingdomConstructionOutbox Outbox)
		{
			if (Job == null || !KingdomConstructionRules.ValidOutbox(Outbox)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.Outbox = Outbox == null ? null : Outbox.Copy();
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool Cancel(ref KingdomConstructionJob Job, string Failure = null)
		{
			KingdomConstructionInputReceipt receipt;
			if (KingdomConstructionRules.TryGetInputReceipt(Job, out receipt))
			{
				if (receipt.TxPhase == KingdomConstructionInputTxPhase.CancellationPending)
					return true;
				if (KingdomConstructionInputRules.IsTerminal(receipt))
					return receipt.TxPhase == KingdomConstructionInputTxPhase.Cancelled
						&& Job.Phase == KingdomConstructionPhase.Cancelled;
				KingdomConstructionInputReceipt pending;
				KingdomConstructionInputFault fault;
				if (!KingdomConstructionInputRules.TryTransitionTransaction(receipt,
					receipt.Revision, receipt.TxPhase,
					KingdomConstructionInputTxPhase.CancellationPending,
					out pending, out fault)) return false;
				KingdomConstructionJob published;
				string publishFailure;
				if (!PublishInputReceipt(Job, pending, out published, out publishFailure))
					return false;
				Job = published;
				return true;
			}
			string ignored;
			return TransitionAndPublish(ref Job, KingdomConstructionPhase.Cancelled, Failure,
				out ignored);
		}

		public static bool UpdateTiming(ref KingdomConstructionJob Job, long StartedTick, long DueTick)
		{
			if (Job == null || StartedTick < 0L || DueTick < StartedTick) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.StartedTick = StartedTick;
			next.DueTick = DueTick;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		public static bool UpdatePayload(ref KingdomConstructionJob Job, string Payload)
		{
			if (Job == null || (Payload != null
				&& Payload.Length > KingdomConstructionRules.MaxPayloadChars)) return false;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Job.Phase,
				The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks, Job.Failure);
			next.Payload = Payload;
			string failure;
			if (!TryUpdate(next, out failure)) return false;
			Job = next;
			return true;
		}

		private static bool TransitionAndPublish(ref KingdomConstructionJob Job,
			KingdomConstructionPhase Phase, string Failure, out string PublishFailure)
		{
			long now = The.Game == null ? Job.UpdatedTick : The.Game.TimeTicks;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(Job, Phase, now, Failure);
			if (!TryUpdate(next, out PublishFailure))
			{
				return false;
			}
			Job = next;
			return true;
		}

	}
}
