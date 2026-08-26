using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	internal sealed partial class KingdomSealStore
	{
		internal bool TryAdvanceGeneration(KingdomSealRecord Previous, KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidGenerationHandoff(Previous, Successor, out Failure))
			{
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Previous.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(Previous.OriginGameId, 'a');
				string slotB = StagePath(Previous.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b))
				{
					Failure = "the origin journal contains an unreadable slot";
					return false;
				}
				if (SameRecord(a, Successor) && SameRecord(b, Successor))
				{
					return true;
				}
				if (SameRecord(a, Previous) && SameRecord(b, Successor))
				{
					return TryWriteSeal(slotA, Successor, true, out Failure);
				}
				if (SameRecord(b, Previous) && SameRecord(a, Successor))
				{
					return TryWriteSeal(slotB, Successor, true, out Failure);
				}
				if (!SameStageIdentity(a, b))
				{
					Failure = "the origin journal is not one coherent generation";
					return false;
				}
				KingdomSealRecord current = Best(a, b);
				if (!SameRecord(current, Previous))
				{
					Failure = "the previous generation is not the exact current stage";
					return false;
				}
				string first = object.ReferenceEquals(current, a) ? slotB : slotA;
				string second = object.ReferenceEquals(current, a) ? slotA : slotB;
				if (!TryWriteSeal(first, Successor, true, out Failure))
				{
					return false;
				}
				return TryWriteSeal(second, Successor, true, out Failure);
			}
		}

		internal bool TryCompleteGenerationAdvance(KingdomSealRecord Successor, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(Successor) || Successor.Status != KingdomSealStatus.Living
				|| Successor.IsResolved)
			{
				Failure = "only an exact complete living successor can finish a generation handoff";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(Successor.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(Successor.OriginGameId, 'a');
				string slotB = StagePath(Successor.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b))
				{
					Failure = "the origin journal contains an unreadable slot";
					return false;
				}
				if (SameRecord(a, Successor) && SameRecord(b, Successor))
				{
					return true;
				}
				KingdomSealRecord durableNewer;
				if (!TryRecoverableGenerationPair(a, b, out durableNewer)
					|| !SameRecord(durableNewer, Successor))
				{
					Failure = "the origin journal is not the exact adjacent handoff for that successor";
					return false;
				}
				string olderSlot = object.ReferenceEquals(durableNewer, a) ? slotB : slotA;
				return TryWriteSeal(olderSlot, durableNewer, true, out Failure);
			}
		}

		internal bool TryRestoreLivingGeneration(KingdomSealRecord SavedLiving, out string Failure)
		{
			Failure = "";
			if (!ValidStageRecord(SavedLiving) || SavedLiving.Status != KingdomSealStatus.Living
				|| SavedLiving.IsResolved)
			{
				Failure = "only a complete living primary-save generation can restore a journal";
				return false;
			}
			FileStream gate;
			if (!TryLockStage(SavedLiving.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(SavedLiving.OriginGameId, 'a');
				string slotB = StagePath(SavedLiving.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b)
					|| !MayRestoreOver(a, SavedLiving) || !MayRestoreOver(b, SavedLiving)
					|| !RecoverableRestoreJournal(a, b, SavedLiving))
				{
					Failure = "the journal is not a recoverable living generation for that primary save";
					return false;
				}
				if (!SameRecord(a, SavedLiving)
					&& !TryWriteSeal(slotA, SavedLiving, true, out Failure))
				{
					return false;
				}
				if (!SameRecord(b, SavedLiving)
					&& !TryWriteSeal(slotB, SavedLiving, true, out Failure))
				{
					return false;
				}
				return true;
			}
		}

		internal bool TryRestoreRetiredGeneration(KingdomSealLineage SavedRetirement,
			out string Failure)
		{
			Failure = "";
			if (SavedRetirement == null
				|| !KingdomSealReceipt.ValidId(SavedRetirement.LineageId)
				|| !KingdomSealReceipt.ValidId(SavedRetirement.LegacyId)
				|| !KingdomSealReceipt.ValidId(SavedRetirement.OriginGameId)
				|| SavedRetirement.Generation < 0 || SavedRetirement.Generation > 1024
				|| SavedRetirement.Revision < 0)
			{
				Failure = "the saved retirement identity is incomplete";
				return false;
			}
			KingdomSealRecord proof = ReadSlot(LegacyPath(SavedRetirement.LegacyId));
			if (proof == null || proof.Status != KingdomSealStatus.Promoted || !proof.IsResolved
				|| proof.LineageId != SavedRetirement.LineageId
				|| proof.LegacyId != SavedRetirement.LegacyId
				|| proof.OriginGameId != SavedRetirement.OriginGameId
				|| proof.Generation != SavedRetirement.Generation
				|| proof.Revision != SavedRetirement.Revision)
			{
				Failure = "the saved retirement has no exact immutable legacy proof";
				return false;
			}
			KingdomSealRecord retired = KingdomSealRules.Copy(proof);
			retired.Status = KingdomSealStatus.Retired;
			retired.InterregnumRoll = -1;
			retired.InheritedState = -1;
			KingdomSealRecord promotedEcho;
			try
			{
				promotedEcho = KingdomSealRules.PromoteRetirement(retired);
			}
			catch (Exception ex)
			{
				Failure = "the immutable retirement proof could not be reconstructed: " + ex.Message;
				return false;
			}
			if (!ValidStageRecord(retired) || !SameRecord(promotedEcho, proof))
			{
				Failure = "the immutable legacy is not the exact promotion of one retired stage";
				return false;
			}

			FileStream gate;
			if (!TryLockStage(retired.OriginGameId, out gate, out Failure))
			{
				return false;
			}
			using (gate)
			{
				string slotA = StagePath(retired.OriginGameId, 'a');
				string slotB = StagePath(retired.OriginGameId, 'b');
				KingdomSealRecord a = ReadSlot(slotA);
				KingdomSealRecord b = ReadSlot(slotB);
				if (SlotIsBroken(slotA, a) || SlotIsBroken(slotB, b)
					|| !MayRestoreRetirementOver(a, retired)
					|| !MayRestoreRetirementOver(b, retired)
					|| !RecoverableRetirementJournal(a, b))
				{
					Failure = "the journal is not recoverable from that exact saved retirement";
					return false;
				}
				if (!SameRecord(a, retired)
					&& !TryWriteSeal(slotA, retired, true, out Failure))
				{
					return false;
				}
				if (!SameRecord(b, retired)
					&& !TryWriteSeal(slotB, retired, true, out Failure))
				{
					return false;
				}
				return true;
			}
		}

	}
}
