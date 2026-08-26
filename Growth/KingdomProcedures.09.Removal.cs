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
		/// <summary>Calls the engine only with the exact tracked instance or exact body-part ID.</summary>
		internal static KingdomLabOwnedTargetState RemoveExact(GameObject Who,
			LabProcedure Procedure, KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget target;
			KingdomLabOwnedTargetState before = ClassifyOwned(Who, Snapshot, out target);
			if (before != KingdomLabOwnedTargetState.Present || target == null)
			{
				return before;
			}
			int tracked = target.Ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
			if (target.Ledger.BindingStateAt(tracked) == 3
				&& !target.Ledger.RearmPresent(tracked, target.ExactPart))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (!target.Ledger.BeginRemoval(Snapshot.ProcedureKey, Snapshot.JobId))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			try
			{
				if (Snapshot.Source == (int)LabSource.Limb)
				{
					Who.Body.RemovePartByID(Snapshot.BodyPartId);
				}
				else if (Snapshot.Source == (int)LabSource.Mutation)
				{
					Who.GetPart<XRL.World.Parts.Mutations>()?.RemoveMutation(
						target.ExactPart as XRL.World.Parts.Mutation.BaseMutation);
				}
				else
				{
					target.Bearer.RemovePart(target.ExactPart);
				}
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact removal callback threw (" + ex.Message + ")");
			}
			return SettleRemovalIntent(Who, Snapshot, target);
		}

		private static KingdomLabOwnedTargetState SettleRemovalIntent(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot, KingdomLabOwnedTarget Target)
		{
			int entry = Target.Ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
			if (entry < 0) return KingdomLabOwnedTargetState.Uncertain;
			if (Target.Ledger.BindingStateAt(entry) == 3)
			{
				KingdomLabOwnedTarget ignored;
				return ClassifyOwned(Who, Snapshot, out ignored);
			}
			if (Target.Ledger.BindingStateAt(entry) != 4)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb)
			{
				BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
				if (limb == null)
				{
					Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
					return KingdomLabOwnedTargetState.Absent;
				}
				if (BodyOwnsPart(Who, limb) && string.Equals(limb.Manager,
					Snapshot.Manager, StringComparison.Ordinal))
				{
					Target.Ledger.CancelRemoval(Snapshot.ProcedureKey, Snapshot.JobId);
					return KingdomLabOwnedTargetState.Present;
				}
				Target.Ledger.Quarantine(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Uncertain;
			}
			IPart exact = Target.ExactPart;
			int ordinal = ReferencePartOrdinal(Target.Bearer, exact);
			if (exact == null || exact.ParentObject == null || ordinal < 0)
			{
				Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Absent;
			}
			if (!ReferenceEquals(exact.ParentObject, Target.Bearer)
				|| ordinal != Snapshot.PartOrdinal
				|| !string.Equals(exact.Name, Snapshot.Grants, StringComparison.Ordinal))
			{
				Target.Ledger.Quarantine(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Mutation
				&& !MutationListed(Who.GetPart<XRL.World.Parts.Mutations>(),
					exact as XRL.World.Parts.Mutation.BaseMutation))
			{
				Target.Ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
				return KingdomLabOwnedTargetState.Absent;
			}
			Target.Ledger.CancelRemoval(Snapshot.ProcedureKey, Snapshot.JobId);
			return KingdomLabOwnedTargetState.Present;
		}

		internal static bool CleanupOwned(GameObject Who, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget ignored;
			if (ClassifyOwned(Who, Snapshot, out ignored) != KingdomLabOwnedTargetState.Absent)
				return false;
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer)) return false;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer?.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0) return false;
			ledger.MarkRemoved(Snapshot.ProcedureKey, Snapshot.JobId);
			IPart tombstonePart;
			if (ledger.ClassifyTombstone(entry, out tombstonePart)
				!= KingdomLabOwnedTargetState.Absent) return false;
			string marker = bearer.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey));
			string nonceMarker = bearer.GetStringProperty(
				OwnerNonceProperty(Snapshot.ProcedureKey));
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal)) return false;
			if (!string.IsNullOrEmpty(nonceMarker)
				&& !string.Equals(nonceMarker, Snapshot.EffectNonce,
					StringComparison.Ordinal)) return false;
			if (string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal))
			{
				try { bearer.RemoveStringProperty(OwnerProperty(Snapshot.ProcedureKey)); }
				catch { return false; }
				if (!string.IsNullOrEmpty(bearer.GetStringProperty(
					OwnerProperty(Snapshot.ProcedureKey)))) return false;
			}
			if (string.Equals(nonceMarker, Snapshot.EffectNonce, StringComparison.Ordinal))
			{
				try { bearer.RemoveStringProperty(OwnerNonceProperty(Snapshot.ProcedureKey)); }
				catch { return false; }
			}
			XRL.World.Parts.r_KingdomLabRecord record =
				Who?.GetPart<XRL.World.Parts.r_KingdomLabRecord>();
			record?.ForgetOwned(Snapshot.ProcedureKey, Snapshot.JobId);
			return ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) == entry
				&& ledger.BindingStateAt(entry) == 3
				&& !string.Equals(bearer.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey)),
					Snapshot.JobId, StringComparison.Ordinal)
				&& !string.Equals(bearer.GetStringProperty(
					OwnerNonceProperty(Snapshot.ProcedureKey)), Snapshot.EffectNonce,
					StringComparison.Ordinal)
				&& !RecordContains(record, Snapshot.ProcedureKey, Snapshot.JobId);
		}

		internal static bool PurgeOwnedTombstone(GameObject Who,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			GameObject bearer;
			if (!ResolveExactBearer(Who, Snapshot, out bearer)) return false;
			XRL.World.Parts.r_KingdomLabEffectLedger ledger =
				bearer.GetPart<XRL.World.Parts.r_KingdomLabEffectLedger>();
			int entry = ledger?.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) ?? -1;
			if (entry < 0) return true;
			IPart exact;
			if (ledger.BindingStateAt(entry) != 3
				|| ledger.ClassifyTombstone(entry, out exact)
					!= KingdomLabOwnedTargetState.Absent) return false;
			ledger.Forget(Snapshot.ProcedureKey, Snapshot.JobId, CleanupPatient: false);
			return ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId) < 0;
		}

		private static bool RecordContains(XRL.World.Parts.r_KingdomLabRecord Record,
			string Key, string JobId)
		{
			Record?.Normalize();
			for (int i = 0; Record != null && i < Record.Keys.Count; i++)
			{
				if (string.Equals(Record.Keys[i], Key, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(Record.JobIds[i], JobId, StringComparison.Ordinal)) return true;
			}
			return false;
		}

		/// <summary>Compatibility entrypoint, now using the exact ownership protocol.</summary>
		public static bool Remove(GameObject Who, string Key)
		{
			KingdomLabOwnershipSnapshot snapshot;
			if (SnapshotOwned(Who, Key, out snapshot) != KingdomLabOwnedTargetState.Present)
			{
				return false;
			}
			LabProcedure procedure;
			if (!TryGet(Key, out procedure)
				|| RemoveExact(Who, procedure, snapshot) != KingdomLabOwnedTargetState.Absent)
				return false;
			try { Who.WantToReequip(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: compatibility removal reequip threw (" + ex.Message + ")");
			}
			KingdomLabOwnedTarget ignored;
			return ClassifyOwned(Who, snapshot, out ignored) == KingdomLabOwnedTargetState.Absent
				&& CleanupOwned(Who, procedure, snapshot)
				&& PurgeOwnedTombstone(Who, snapshot);
		}

		/// <summary>The founder's own record of what has been done to them, minted on first use.
		/// A part rather than game state, because Addendum 22 C11 rules the named procedures reset
		/// for an heir, and an heir is a different person carrying nothing of this.</summary>
		public static XRL.World.Parts.r_KingdomLabRecord Record(GameObject Who)
		{
			return Who.RequirePart<XRL.World.Parts.r_KingdomLabRecord>();
		}
	}
}
