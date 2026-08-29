using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Construction-registry ownership of the routed-input child receipt.</summary>
	public static partial class KingdomConstructionRules
	{
		public static bool TryGetInputReceipt(KingdomConstructionJob Job,
			out KingdomConstructionInputReceipt Receipt)
		{
			Receipt = null;
			KingdomConstructionInputFault fault;
			return Job != null && !string.IsNullOrEmpty(Job.InputReceipt)
				&& KingdomConstructionInputRules.TryDecode(Job.InputReceipt,
					out Receipt, out fault);
		}

		/// <summary>Copy-on-write adoption. The caller owns the enclosing job revision/phase.</summary>
		public static bool UpdateInputReceipt(ref KingdomConstructionJob Job,
			KingdomConstructionInputReceipt Receipt)
		{
			if (Job == null || Receipt == null) return false;
			string encoded;
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryEncode(Receipt, out encoded, out fault)
				|| encoded.Length > MaxInputReceiptChars) return false;
			KingdomConstructionJob next = Job.Copy();
			next.InputReceipt = encoded;
			next.InputReceiptHash = Sha256(encoded);
			Job = next;
			return true;
		}

		private static bool ValidInputReceipt(KingdomConstructionJob Job,
			KingdomConstructionInputReceipt Receipt)
		{
			long ownerEpoch;
			if (!TryOwnerEpoch(Job.OwnerKey, out ownerEpoch)
				|| Receipt.OwnerEpoch != ownerEpoch
				|| Receipt.ConstructionJobId != Job.Id || Receipt.OwnerKey != Job.OwnerKey
				|| Receipt.TargetZoneId != Job.ZoneId)
				return false;
			if (Receipt.TxPhase == KingdomConstructionInputTxPhase.Committed)
				return Job.Phase >= KingdomConstructionPhase.Funded
					&& KingdomConstructionInputRules.CommittedClaimsExact(Receipt,
						Job.Claims.WaterSpent, Job.Claims.WaterOutstanding,
						Job.Claims.WaterLost, Job.Claims.MaterialSpent,
						Job.Claims.MaterialOutstanding, Job.Claims.MaterialLost);
			KingdomConstructionInputIntent intent;
			string digest;
			if (!TryInputIntent(Job, Receipt.WaterRequested,
				Receipt.MaterialRequestedClaim, out intent, out digest)
				|| !KingdomConstructionInputRules.ExactIntentBinding(Receipt, intent, ownerEpoch))
				return false;
			if (!KingdomConstructionInputRules.ClaimsBeforeExact(Receipt,
				Job.Claims.WaterSpent, Job.Claims.WaterOutstanding,
				Job.Claims.WaterLost, Job.Claims.MaterialSpent,
				Job.Claims.MaterialOutstanding, Job.Claims.MaterialLost)) return false;
			switch (Receipt.TxPhase)
			{
			case KingdomConstructionInputTxPhase.RolledBack:
			case KingdomConstructionInputTxPhase.Cancelled:
				return Job.Phase == KingdomConstructionPhase.Cancelled;
			case KingdomConstructionInputTxPhase.Compensated:
				return Job.Phase == KingdomConstructionPhase.Compensated;
			case KingdomConstructionInputTxPhase.Quarantined:
				return Job.Phase == KingdomConstructionPhase.InspectionRequired;
			default:
				return Job.Phase == KingdomConstructionPhase.Published
					|| Job.Phase == KingdomConstructionPhase.Outstanding;
			}
		}

		private static bool ValidInputRegistryUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			bool currentHas = !string.IsNullOrEmpty(Current.InputReceipt);
			bool nextHas = !string.IsNullOrEmpty(Next.InputReceipt);
			if (!currentHas && !nextHas) return string.IsNullOrEmpty(Current.InputReceiptHash)
				&& string.IsNullOrEmpty(Next.InputReceiptHash);
			if (!currentHas)
			{
				KingdomConstructionInputReceipt attached;
				return string.IsNullOrEmpty(Current.InputReceiptHash)
					&& SameInputIntentFacts(Current, Next)
					&& (Current.Phase == KingdomConstructionPhase.Published
						|| Current.Phase == KingdomConstructionPhase.Outstanding)
					&& Next.Phase == Current.Phase && TryGetInputReceipt(Next, out attached)
					&& attached.TxPhase == KingdomConstructionInputTxPhase.ReservationPrepared;
			}
			if (!nextHas)
				return Next.Compacted && IsTerminal(Next.Phase)
					&& Current.InputReceiptHash == Next.InputReceiptHash;

			KingdomConstructionInputReceipt current;
			KingdomConstructionInputReceipt next;
			if (!TryGetInputReceipt(Current, out current) || !TryGetInputReceipt(Next, out next)
				|| current.ConstructionJobId != next.ConstructionJobId
				|| current.ReceiptId != next.ReceiptId || current.OwnerKey != next.OwnerKey
				|| current.OwnerEpoch != next.OwnerEpoch || current.PlanDigest != next.PlanDigest)
				return false;
			if (Current.InputReceipt == Next.InputReceipt)
				return InputTerminal(current.TxPhase);
			return SameInputIntentFacts(Current, Next)
				&& KingdomConstructionInputRules.ValidReceiptUpdate(current, next);
		}

		private static bool SameInputIntentFacts(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			return Current.Id == Next.Id && Current.OwnerKey == Next.OwnerKey
				&& Current.ZoneId == Next.ZoneId && Current.Route == Next.Route
				&& Current.Projection == Next.Projection && Current.X == Next.X
				&& Current.Y == Next.Y && Current.SubjectId == Next.SubjectId
				&& Current.SourceId == Next.SourceId && Current.OutputId == Next.OutputId
				&& Current.TargetKey == Next.TargetKey && Current.Payload == Next.Payload
				&& Current.PhysicalPhase == Next.PhysicalPhase
				&& Current.PhysicalIndex == Next.PhysicalIndex
				&& Current.PhysicalAmount == Next.PhysicalAmount
				&& Current.PhysicalSpilled == Next.PhysicalSpilled
				&& Current.PhysicalItemId == Next.PhysicalItemId
				&& Current.PhysicalDestinationId == Next.PhysicalDestinationId
				&& Current.PhysicalReceipt == Next.PhysicalReceipt
				&& Current.BuildTruthSchema == Next.BuildTruthSchema
				&& Current.BuildHasPlot == Next.BuildHasPlot
				&& Current.BuildFrontier == Next.BuildFrontier
				&& Current.BuildDefence == Next.BuildDefence
				&& Current.CreatedTick == Next.CreatedTick
				&& Current.StartedTick == Next.StartedTick && Current.DueTick == Next.DueTick;
		}

		private static bool IsRoutedCommitUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			KingdomConstructionInputReceipt before;
			KingdomConstructionInputReceipt after;
			return (Current.Phase == KingdomConstructionPhase.Published
					|| Current.Phase == KingdomConstructionPhase.Outstanding)
				&& Next.Phase == KingdomConstructionPhase.Funded
				&& TryGetInputReceipt(Current, out before)
				&& TryGetInputReceipt(Next, out after)
				&& before.TxPhase == KingdomConstructionInputTxPhase.Closing
				&& after.TxPhase == KingdomConstructionInputTxPhase.Committed;
		}

		private static bool IsRoutedCompensationUpdate(KingdomConstructionJob Current,
			KingdomConstructionJob Next)
		{
			KingdomConstructionInputReceipt before;
			KingdomConstructionInputReceipt after;
			return (Current.Phase == KingdomConstructionPhase.Published
					|| Current.Phase == KingdomConstructionPhase.Outstanding)
				&& Next.Phase == KingdomConstructionPhase.Compensated
				&& TryGetInputReceipt(Current, out before)
				&& TryGetInputReceipt(Next, out after)
				&& before.TxPhase == KingdomConstructionInputTxPhase.CompensationPending
				&& after.TxPhase == KingdomConstructionInputTxPhase.Compensated;
		}

		private static bool InputTerminal(KingdomConstructionInputTxPhase Phase)
		{
			return Phase == KingdomConstructionInputTxPhase.Committed
				|| Phase == KingdomConstructionInputTxPhase.RolledBack
				|| Phase == KingdomConstructionInputTxPhase.Compensated
				|| Phase == KingdomConstructionInputTxPhase.Quarantined
				|| Phase == KingdomConstructionInputTxPhase.Cancelled;
		}

		private static bool TryOwnerEpoch(string OwnerKey, out long Epoch)
		{
			Epoch = -1L;
			if (string.IsNullOrEmpty(OwnerKey) || !OwnerKey.StartsWith("v1:",
				StringComparison.Ordinal)) return false;
			int end = OwnerKey.IndexOf(':', 3);
			if (end <= 3) return false;
			string text = OwnerKey.Substring(3, end - 3);
			return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
				out Epoch) && Epoch >= 0L
				&& Epoch.ToString(CultureInfo.InvariantCulture) == text;
		}
	}
}
