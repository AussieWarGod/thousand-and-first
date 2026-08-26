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
		/// <summary>Finds a current commission in the bearer ledger without inventing a legacy
		/// identity from a same-class effect.</summary>
		internal static KingdomLabOwnedTargetState SnapshotTracked(GameObject Who,
			LabProcedure Procedure, string JobId, string BearerId,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || Procedure == null || string.IsNullOrEmpty(JobId)
				|| string.IsNullOrEmpty(BearerId))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			GameObject bearer = string.Equals(BearerId, Who.ID, StringComparison.Ordinal)
				? Who : GameObject.FindByID(BearerId);
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer?.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int at = ledger?.IndexOf(Procedure.Key, JobId) ?? -1;
			if (at < 0 || !string.Equals(ledger.PatientIds[at], Who.ID,
				StringComparison.Ordinal) || ledger.LedgerQuarantined
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					ledger.ProcedureKeys[at], ledger.ClassNames[at], ledger.Sources[at],
					ledger.Attaches[at], ledger.Managers[at], ledger.Fingerprints[at],
					ledger.Details[at]))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Snapshot = new KingdomLabOwnershipSnapshot(Procedure.Key, JobId, Who.ID,
				ledger.BodyPartIds[at], BearerId, ledger.ClassNames[at], ledger.Sources[at],
				ledger.Attaches[at], ledger.Managers[at], ledger.Details[at],
				ledger.Fingerprints[at], ledger.PartOrdinals[at], ledger.NonceAt(at));
			KingdomLabOwnedTarget target;
			return ClassifyOwned(Who, Snapshot, out target);
		}

		/// <summary>Reads one tracked target. Missing physical state proves absence; a same-class
		/// replacement without the original tracker is foreign and therefore uncertain.</summary>
		internal static KingdomLabOwnedTargetState ClassifyOwned(GameObject Who,
			LabProcedure Procedure, KingdomLabOwnershipSnapshot Snapshot,
			out KingdomLabOwnedTarget Target)
		{
			if (Procedure == null || !string.Equals(Procedure.Key, Snapshot.ProcedureKey,
				StringComparison.OrdinalIgnoreCase))
			{
				Target = null;
				return KingdomLabOwnedTargetState.Uncertain;
			}
			return ClassifyOwned(Who, Snapshot, out Target);
		}

		internal static KingdomLabOwnedTargetState ClassifyOwned(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, out KingdomLabOwnedTarget Target)
		{
			Target = null;
			if (Who == null
				|| !string.Equals(Who.ID, Snapshot.PatientId, StringComparison.Ordinal)
				|| string.IsNullOrEmpty(Snapshot.JobId) || Snapshot.BodyPartId <= 0
				|| string.IsNullOrEmpty(Snapshot.BearerId) || Snapshot.EffectNonce.Length != 32
				|| !KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
					Snapshot.ProcedureKey, Snapshot.Grants, Snapshot.Source, Snapshot.Attach,
					Snapshot.Manager, Snapshot.Fingerprint, Snapshot.Detail))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0)
			{
				return UntrackedPhysicalState(Who, bearer, Snapshot);
			}
			if (ledger.LedgerQuarantined || ledger.BindingStates[entry] == 2
				|| !string.Equals(ledger.NonceAt(entry), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				|| !string.Equals(bearer.GetStringProperty(
					OwnerNonceProperty(Snapshot.ProcedureKey)), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				|| !ledger.EntryMatches(entry, Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
					Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
					Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint,
					Snapshot.PartOrdinal))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Target = new KingdomLabOwnedTarget { Bearer = bearer, Ledger = ledger };
			if (ledger.BindingStates[entry] == 4)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
				if (ledger.BindingStates[entry] == 3)
				{
					if (limb == null) return KingdomLabOwnedTargetState.Absent;
					if (!BodyOwnsPart(Who, limb) || !string.Equals(limb.Manager,
						Snapshot.Manager, StringComparison.Ordinal))
					{
						return KingdomLabOwnedTargetState.Uncertain;
					}
					Target.ExactBodyPart = limb;
					return KingdomLabOwnedTargetState.Present;
				}
				if (limb == null)
				{
					return KingdomLabOwnedTargetState.Absent;
				}
				if (!BodyOwnsPart(Who, limb) || !string.Equals(limb.Manager,
					Snapshot.Manager, StringComparison.Ordinal))
				{
					return KingdomLabOwnedTargetState.Uncertain;
				}
				Target.ExactBodyPart = limb;
				return KingdomLabOwnedTargetState.Present;
			}
			if (ledger.BindingStates[entry] == 3)
			{
				IPart tombstonePart;
				KingdomLabOwnedTargetState tombstone = ledger.ClassifyTombstone(entry,
					out tombstonePart);
				Target.ExactPart = tombstonePart;
				return tombstone;
			}
			IPart exact = ledger.ResolvePart(entry);
			if (Snapshot.Source == (int)LabSource.Mutation)
			{
				XRL.World.Parts.Mutations mutations = Who.GetPart<XRL.World.Parts.Mutations>();
				XRL.World.Parts.Mutation.BaseMutation owned =
					exact as XRL.World.Parts.Mutation.BaseMutation;
				if (owned != null && MutationListed(mutations, owned))
				{
					Target.ExactPart = owned;
					return KingdomLabOwnedTargetState.Present;
				}
				// RemoveMutation deliberately leaves modifier-backed mutation parts. The exact
				// runtime instance plus absence from MutationList proves only our contribution gone.
				if (owned != null)
				{
					Target.ExactPart = owned;
					return KingdomLabOwnedTargetState.Absent;
				}
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (exact != null)
			{
				Target.ExactPart = exact;
				return KingdomLabOwnedTargetState.Present;
			}
			return KingdomLabOwnedTargetState.Uncertain;
		}

		private static KingdomLabOwnedTargetState UntrackedPhysicalState(GameObject Who,
			GameObject Bearer, KingdomLabOwnershipSnapshot Snapshot)
		{
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				return ExactBodyPart(Who, Snapshot.BodyPartId) == null
					? KingdomLabOwnedTargetState.Absent : KingdomLabOwnedTargetState.Uncertain;
			}
			return Bearer.GetPart(Snapshot.Grants) == null
				? KingdomLabOwnedTargetState.Absent : KingdomLabOwnedTargetState.Uncertain;
		}

		private static bool ResolveExactBearer(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, out GameObject Bearer)
		{
			Bearer = null;
			if (Snapshot.Source != (int)LabSource.Part
				|| Snapshot.Attach == (int)LabAttach.Body)
			{
				if (!string.Equals(Snapshot.BearerId, Who.ID, StringComparison.Ordinal)) return false;
				Bearer = Who;
				return true;
			}
			if (Snapshot.Attach != (int)LabAttach.Weapon) return false;
			BodyPart slot = ExactBodyPart(Who, Snapshot.BodyPartId);
			GameObject exact = slot?.DefaultBehavior;
			if (slot == null || !BodyOwnsPart(Who, slot) || !GameObject.Validate(exact)
				|| !ReferenceEquals(slot.DefaultBehavior, exact)
				|| !string.Equals(exact.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			Bearer = exact;
			return true;
		}
	}
}
