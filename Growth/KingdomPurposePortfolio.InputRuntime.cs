using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static bool BeginInputDebit(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || string.IsNullOrEmpty(operation.InputCargoId)
				|| !TryPurposeZone(operation.SourceZoneId, out Zone zone)
				|| FindExactKnown(zone, operation.InputCargoId, out GameObject cargo)
					!= KingdomPhysicalLookupState.Exact
				|| !ExactPortfolioCargo(cargo, operation.InputCargoReceipt,
					operation.SourceInputStoreId))
				return Fail("The exact incoming purpose cargo is not in its frozen input store.",
					out Failure);
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.InputDebitPending;
			next.InputBeforeDigest = PurposeDigest("purpose-input", operation.InputCargoId,
				operation.InputCargoReceipt, "present");
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}

		private static bool DriveInputDebit(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || !TryPurposeZone(operation.SourceZoneId, out Zone zone))
				return Fail("The purpose input ground is unavailable.", out Failure);
			string before = PurposeDigest("purpose-input", operation.InputCargoId,
				operation.InputCargoReceipt, "present");
			string after = PurposeDigest("purpose-input", operation.InputCargoId,
				operation.InputCargoReceipt, "absent");
			if (operation.InputBeforeDigest != before)
				return QuarantinePortfolio(Pair, "The input-cargo intent digest changed.",
					out Published, out Failure);
			KingdomPhysicalLookupState state = FindPortfolioObject(operation.InputCargoId,
				out GameObject cargo, out bool graveyard);
			bool present = state == KingdomPhysicalLookupState.Exact && !graveyard
				&& ExactPortfolioCargo(cargo, operation.InputCargoReceipt,
					operation.SourceInputStoreId);
			bool absent = state == KingdomPhysicalLookupState.Absent || graveyard;
			if (!present && !absent)
				return QuarantinePortfolio(Pair,
					"The exact input cargo is neither present nor proved consumed.",
					out Published, out Failure);
			if (present)
			{
				GameObject holder = cargo.InInventory;
				bool removed = false;
				try { removed = cargo.Obliterate(null, Silent: true); }
				catch (Exception ex)
				{
					return QuarantinePortfolio(Pair,
						"The exact input-cargo callback threw: " + ex.Message,
						out Published, out Failure);
				}
				KingdomSurvey.ObserveChangedInActive(zone, holder);
				state = FindPortfolioObject(operation.InputCargoId, out cargo, out graveyard);
				absent = state == KingdomPhysicalLookupState.Absent || graveyard;
				if (!removed && !absent)
					return QuarantinePortfolio(Pair,
						"The exact input cargo refused consumption.", out Published, out Failure);
			}
			if (!absent)
				return QuarantinePortfolio(Pair, "The input-cargo aftermath is ambiguous.",
					out Published, out Failure);
			// Both roots go, not just the canonical one: a cargo rooted before the canonical key
			// existed would otherwise leave its legacy entry behind forever. Each is checked
			// against the value under it, so a colliding legacy key holding another operation's
			// live cargo is left exactly where it is.
			if (KingdomPurposePortfolioRules.TryDecodeCargo(operation.InputCargoReceipt,
				out KingdomPurposeCargoReceipt consumed)) RemovePurposeCargoRoots(consumed);
			KingdomPurposeOperationReceipt next = operation.Copy();
			next.Phase = KingdomPurposeOperationPhase.InputDebited;
			next.InputAfterDigest = after;
			next.Revision++;
			return TryPublishOperation(Pair, next, Pair.Phase, out Published, out Failure);
		}
	}
}
