using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransactionRules
	{
		/// <summary>
		/// Before publication, every changed fact is compensable and the same-vessel snapshot must
		/// be restored. Once publication crossed an irreversible engine boundary, retaining the
		/// exact paid receipt for an idempotent retry is the only honest failure result.
		/// </summary>
		public static KingdomFoundingOutcome FailureOutcome(bool PublicationCommitted,
			bool WaterChanged, bool RestorationExact)
		{
			if (PublicationCommitted)
			{
				return KingdomFoundingOutcome.RecoverableFailure;
			}
			if (!WaterChanged)
			{
				return KingdomFoundingOutcome.Refused;
			}
			return RestorationExact
				? KingdomFoundingOutcome.CompensatedFailure
				: KingdomFoundingOutcome.RecoverableFailure;
		}

		public static KingdomFoundingWaterDisposition WaterDisposition(
			KingdomFoundingOutcome Outcome, bool RestorationExact)
		{
			switch (Outcome)
			{
			case KingdomFoundingOutcome.Refused:
				return KingdomFoundingWaterDisposition.Untouched;
			case KingdomFoundingOutcome.CompensatedFailure:
				return RestorationExact
					? KingdomFoundingWaterDisposition.RestoredExactly
					: KingdomFoundingWaterDisposition.RestorationFailed;
			case KingdomFoundingOutcome.RecoverableFailure:
				return RestorationExact
					? KingdomFoundingWaterDisposition.HeldForRecovery
					: KingdomFoundingWaterDisposition.RestorationFailed;
			case KingdomFoundingOutcome.Committed:
				return KingdomFoundingWaterDisposition.Spent;
			default:
				return KingdomFoundingWaterDisposition.RestorationFailed;
			}
		}

		public static bool ChargesEnergy(KingdomFoundingOutcome Outcome)
		{
			return Outcome == KingdomFoundingOutcome.Committed;
		}

		public static bool RequestsInventoryExit(KingdomFoundingOutcome Outcome)
		{
			return Outcome == KingdomFoundingOutcome.Committed;
		}

		/// <summary>Every projection through <paramref name="Through"/> must have succeeded.</summary>
		public static bool ProjectionSequenceComplete(bool[] Succeeded,
			KingdomFoundingProjection Through)
		{
			int last = (int)Through;
			if (Succeeded == null || last < (int)KingdomFoundingProjection.Water ||
				Succeeded.Length <= last)
			{
				return false;
			}
			for (int i = (int)KingdomFoundingProjection.Water; i <= last; i++)
			{
				if (!Succeeded[i])
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Checked integer subtraction used by the live same-vessel receipt.</summary>
		public static bool TryCommittedVolume(int OriginalVolume, int Cost,
			out int CommittedVolume)
		{
			CommittedVolume = OriginalVolume;
			if (OriginalVolume < 0 || Cost <= 0 || OriginalVolume < Cost)
			{
				return false;
			}
			CommittedVolume = OriginalVolume - Cost;
			return true;
		}
	}
}
