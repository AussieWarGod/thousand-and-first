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
		// ==================================================================================
		// The three write paths
		// ==================================================================================

		/// <summary>
		/// Performs one procedure on one founder.
		/// <para>
		/// Every graft carries <c>Manager = TAF::Lab::&lt;key&gt;</c> whether it is a limb or a
		/// part, so <see cref="Remove"/> undoes any of them in one call and nothing the lab does is
		/// permanent against the founder's will. That is the consent story, and it is also the
		/// escape hatch for the failure mode Playable Golem is remembered for: if a graft is what
		/// stranded you, it can come off (DIVERSITY &sect;3.0c, &sect;3.9 risk 4).
		/// </para>
		/// </summary>
		/// <param name="Who">The founder.</param>
		/// <param name="Procedure">The record.</param>
		/// <param name="SlotIndex">Which place, as an index into <see cref="Census"/>.</param>
		/// <param name="Stamp">The preserved part's stamp, from which the granted part is rebuilt.</param>
		/// <param name="Failure">Why not, when this answers false. Never a bare "that failed".</param>
		/// <returns>True when the founder actually changed. Never throws.</returns>
		public static bool Grant(GameObject Who, LabProcedure Procedure, int SlotIndex, string Stamp, out string Failure)
		{
			Failure = null;
			if (Who == null || Procedure == null)
			{
				Failure = "There is nobody on the table.";
				return false;
			}
			XRL.World.Parts.Body body = Who.Body;
			List<BodyPart> parts = body?.GetParts();
			if (parts == null)
			{
				Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return false;
			}
			// The census skips abstract parts, so the index the slate handed back is an index into
			// the FILTERED list and has to be walked back the same way it was built.
			BodyPart slot = null;
			int seen = 0;
			for (int i = 0; i < parts.Count; i++)
			{
				if (parts[i] == null || parts[i].Abstract)
				{
					continue;
				}
				if (seen++ == SlotIndex)
				{
					slot = parts[i];
					break;
				}
			}
			if (slot == null)
			{
				Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return false;
			}
			return GrantAt(Who, Procedure, slot.ID,
				(Procedure.Attach == LabAttach.Weapon && GameObject.Validate(slot.DefaultBehavior))
					? slot.DefaultBehavior.ID : Who.ID,
				Stamp, Guid.NewGuid().ToString("N"), out Failure);
		}

		/// <summary>Terminal grant against the exact slot and bearer selected at commission.</summary>
		public static bool GrantAt(GameObject Who, LabProcedure Procedure, int BodyPartId,
			string BearerId, string Stamp, string JobId, out string Failure)
		{
			string detail = ExecutionDetail(Procedure, Stamp);
			string manager = ManagerFor(Procedure?.Key);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure?.Key, Procedure?.Grants,
				(int)(Procedure?.Source ?? LabSource.Part),
				(int)(Procedure?.Attach ?? LabAttach.Body), manager, detail);
			KingdomLabGrantAttempt attempt = GrantAtExact(Who, Procedure, BodyPartId, BearerId,
				Stamp, JobId, manager, detail, fingerprint);
			Failure = attempt.Failure;
			return attempt.State == KingdomLabOwnedTargetState.Present;
		}

		internal static KingdomLabGrantAttempt GrantAtExact(GameObject Who, LabProcedure Procedure,
			int BodyPartId, string BearerId, string Stamp, string JobId, string Manager,
			string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = BearerId ?? "" };
			if (Who == null || Procedure == null || Who.Body == null)
			{
				attempt.Failure = "There is nobody on the table.";
				return attempt;
			}
			if (!KingdomLabRules.ValidEffectContract(KingdomLabRules.EffectContractVersion,
				Procedure.Key, Procedure.Grants, (int)Procedure.Source, (int)Procedure.Attach,
				Manager, Fingerprint, Detail))
			{
				attempt.Failure = "The paid job's immutable effect contract is not valid.";
				return attempt;
			}
			BodyPart slot = ExactLiveBodyPart(Who, BodyPartId);
			if (slot == null || slot.Abstract || !BodyOwnsLivePart(Who, slot))
			{
				attempt.Failure = KingdomProcedureRules.RefusalLine(LabVerdict.RefusedNoSlot, Procedure);
				return attempt;
			}
			GameObject expected = (Procedure.Attach == LabAttach.Weapon) ? slot.DefaultBehavior : Who;
			if (!GameObject.Validate(expected) || !string.Equals(expected.ID, BearerId,
				StringComparison.Ordinal) || (Procedure.Attach == LabAttach.Weapon
					&& !ReferenceEquals(slot.DefaultBehavior, expected)))
			{
				attempt.Failure = "The selected body part no longer bears the exact thing the paid contract recorded.";
				return attempt;
			}
			if (HasProcedureClass(Who, Procedure))
			{
				attempt.Failure = "That procedure already exists on live or detached anatomy. The hall will not create a second instance.";
				return attempt;
			}
			switch (Procedure.Source)
			{
			case LabSource.Limb:
				return GrantLimb(Who, Procedure, slot, JobId, Manager, Detail, Fingerprint);
			case LabSource.Mutation:
				return GrantMutation(Who, Procedure, slot, Stamp, JobId, Manager, Detail,
					Fingerprint);
			default:
				return GrantPart(Who, Procedure, slot, expected, Stamp, JobId, Manager, Detail,
					Fingerprint);
			}
		}

		private static KingdomLabGrantAttempt GrantPart(GameObject Who, LabProcedure Procedure,
			BodyPart Slot, GameObject Bearer, string Stamp, string JobId, string Manager,
			string Detail, string Fingerprint)
		{
			KingdomLabGrantAttempt attempt = new KingdomLabGrantAttempt { BearerId = Bearer.ID };
			IPart built;
			if (!TryRebuild(Procedure.Grants, Stamp, out built))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = "The hall could not make sense of what was kept. No body effect was made.";
				return attempt;
			}
			if (Bearer.GetPart(Procedure.Grants) != null)
			{
				attempt.Failure = "You already carry that, and carrying it twice would only make it fire twice.";
				return attempt;
			}
			XRL.World.Parts.r_KingdomLabEffectLedger ledger;
			if (!PrepareOwnershipIntent(Bearer, Who, Procedure, Slot.ID, JobId, Manager, Detail,
				Fingerprint, built, Bearer.PartsList?.Count ?? 0, out ledger, out attempt.Failure))
			{
				attempt.State = KingdomLabOwnedTargetState.Absent;
				return attempt;
			}
			Exception error = null;
			try
			{
				Bearer.AddPart(built);
			}
			catch (Exception ex)
			{
				error = ex;
			}
			int ordinal = ReferencePartOrdinal(Bearer, built);
			if (ordinal >= 0 && ReferenceEquals(built.ParentObject, Bearer)
				&& CountPartClass(Bearer, Procedure.Grants) == 1)
			{
				PublishOwnership(Who, Bearer, Procedure, Slot.Type, Slot.ID, JobId, Manager,
					Detail, Fingerprint, built, ordinal, ledger, attempt);
				if (error != null) attempt.Failure = "The engine callback threw after the exact effect was attached; ownership was recovered: " + error.Message;
				return attempt;
			}
			bool absent = TryRollbackExactPart(Bearer, built);
			if (absent)
			{
				ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
				ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
				attempt.State = KingdomLabOwnedTargetState.Absent;
				attempt.Failure = (error == null) ? "The exact effect did not attach."
					: "The attachment callback threw; the exact attempted part was rolled back.";
			}
			else
			{
				ledger.Quarantine(Procedure.Key, JobId);
				attempt.Failure = "The exact attempted part changed topology during attachment. Its intent is quarantined; no same-class part will be adopted.";
			}
			return attempt;
		}
	}
}
