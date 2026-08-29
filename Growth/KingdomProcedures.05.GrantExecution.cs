using System;
using System.Collections.Generic;
using System.Reflection;

using Qud.API;
using XRL;
using XRL.World;
using XRL.World.Anatomy;

namespace ThousandAndFirst
{
	public static partial class KingdomProcedures
	{
		private static KingdomLabGrantAttempt GrantLimb(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, string JobId, string Manager, string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Who.IDIfAssigned };
			string type = string.IsNullOrEmpty(Detail) ? Slot.Type : Detail;
			BodyPart grown = new BodyPart(type, 0, Slot.ParentBody, Manager: Manager);
			int grownId = grown.ID;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Who, Who, Procedure, grownId, JobId, Manager, Detail,
				Fingerprint, null, -1, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				Slot.AddPart(grown, Slot.Type, DoUpdate: false);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			if (BodyOwnsLivePart(Who, grown)
				&& ReferenceEquals(ExactLiveBodyPart(Who, grownId), grown))
			{
				PublishOwnership(Who, Who, Procedure, type, grownId, JobId, Manager, Detail,
					Fingerprint, null, -1, ledger, attempt);
				attempt.ExactBodyPart = grown;
				attempt.BodyPartId = grownId;
				try
				{
					Who.Body.UpdateBodyParts();
					Who.Body.RecalculateTypeArmor(type);
					Who.WantToReequip();
				}
				catch (Exception ex)
				{
					error = error ?? ex;
				}
				if (error != null) attempt.Failure = "The body update callback threw after the exact limb and ownership receipt were durable: " + error.Message;
				return attempt;
			}
			bool absent = TryRollbackExactBodyPart(Who, grown);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Who, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = (error == null) ? "The exact limb did not enter the patient's body."
					: "The limb insertion threw; the exact partial limb was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Uncertain;
				attempt.Failure = "The limb insertion left uncertain exact topology. Its prepublished intent is quarantined; no same-type limb will be adopted.";
			}
			return attempt;
		}

		private static bool TryRollbackExactBodyPart(GameObject Who, BodyPart Part)
		{
			if (!BodyOwnsPart(Who, Part) && Part?.ParentPart == null) return true;
			try { Who?.Body?.RemovePart(Part); }
			catch { }
			return !BodyOwnsPart(Who, Part) && Part?.ParentPart == null;
		}

		private static KingdomLabGrantAttempt GrantMutation(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, string Stamp, string JobId, string Manager, string Detail,
			string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Who.IDIfAssigned };
			XRL.World.Parts.Mutations mutations = Who.RequirePart<XRL.World.Parts.Mutations>();
			if (Who.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation)
			{
				attempt.Failure = "You already have that, whether native or modifier-backed. The hall will not replace it.";
				return attempt;
			}
			int level;
			int.TryParse(KingdomProcedureRules.StampedField(Stamp, Procedure.Grants, "Level"), out level);
			// NEVER the source's own level. The single most load-bearing balance number in the wave:
			// the mod this whole design learned from is remembered for granting mutations at the
			// source's strength, and its own author wrote down that it ruined the combat design.
			int granted = KingdomProcedureRules.GrantedMutationLevel(level);
			XRL.World.Parts.Mutation.BaseMutation exact =
				XRL.World.Parts.Mutation.BaseMutation.Create(Procedure.Grants);
			if (exact == null)
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "The frozen mutation class could not be constructed.";
				return attempt;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Who, Who, Procedure, Slot.ID, JobId, Manager, Detail,
				Fingerprint, exact, Who.PartsList?.Count ?? 0, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				mutations.AddMutation(exact, granted);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			int ordinal = ReferencePartOrdinal(Who, exact);
			bool listed = MutationListed(mutations, exact);
			if (ordinal >= 0 && ReferenceEquals(exact.ParentObject, Who) && listed)
			{
				PublishOwnership(Who, Who, Procedure, "", Slot.ID, JobId, Manager, Detail,
					Fingerprint, exact, ordinal, ledger, attempt);
				if (error != null) attempt.Failure = "The mutation callback threw after the exact listed mutation and ownership receipt were durable: " + error.Message;
				return attempt;
			}
			bool absent = !listed && TryRollbackExactPart(Who, exact);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Who, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "Mutation publication stopped before MutationList accepted the exact instance; the partial part was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.Failure = "Mutation publication left an uncertain exact instance. It is quarantined; no class replacement will be adopted.";
			}
			return attempt;
		}
	}
}
