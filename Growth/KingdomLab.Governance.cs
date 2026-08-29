using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		private static bool HandleActivePatientRegistry(GameObject Actor, KingdomSystem System)
		{
			if (Actor == null || System == null || The.Game == null) return false;
			bool quarantined;
			List<KingdomLabRegistryEntry> rows = KingdomLabRules.ParseRegistry(
				The.Game.GetStringGameState(LabRegistryState, ""), out quarantined);
			if (quarantined)
			{
				Popup.Show("The canonical lab-job registry is malformed. New commissions are blocked; existing physical receipts are untouched.");
				return true;
			}
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLabRegistryEntry row = rows[i];
				if (row.Status != KingdomLabRegistryStatus.Active
					|| !string.Equals(row.PatientId, Actor.IDIfAssigned, StringComparison.Ordinal)
					|| !string.Equals(row.GameId, The.Game.GameID, StringComparison.Ordinal)
					|| !string.Equals(row.RealmId, RealmIdentity(System), StringComparison.Ordinal)
					|| row.RealmFoundedTick != System.FoundedTick) continue;
				GameObject owner = GameObject.FindByID(row.BuildingId);
				r_KingdomLabJob physical = owner?.GetPart<r_KingdomLabJob>();
				if (GameObject.Validate(owner) && physical != null
					&& string.Equals(physical.JobId, row.JobId, StringComparison.Ordinal))
				{
					Popup.Show("An active commission for this patient belongs to hall {{W|"
						+ row.BuildingId + "}}. Recover or cancel it there; this hall cannot inherit it.");
					return true;
				}
				int choice = Popup.PickOption(Title: "orphaned lab receipt",
					Intro: "The canonical receipt still binds job {{W|" + row.JobId
						+ "}} to missing or unloaded hall {{W|" + row.BuildingId
						+ "}}. No successor hall may assume its payment or body authority.",
					Options: new string[] { "Leave the receipt preserved.",
						"Abandon this receipt; paid costs are not returned." }, AllowEscape: true);
				if (choice != 1) return true;
				string markerKey = PendingProperty(row.ProcedureKey);
				string marker = Actor.GetStringProperty(markerKey);
				if (!string.IsNullOrEmpty(marker)
					&& !string.Equals(marker, row.JobId, StringComparison.Ordinal))
				{
					row.Status = KingdomLabRegistryStatus.Quarantined;
					row.UpdatedTick = Math.Max(0L, The.Game.TimeTicks);
					KingdomLabRules.UpsertRegistry(rows, row);
					The.Game.SetStringGameState(LabRegistryState,
						KingdomLabRules.FormatRegistry(rows));
					Popup.Show("The patient marker belongs to another job. This stale receipt was quarantined and cleared nothing.");
					return true;
				}
				row.Status = KingdomLabRegistryStatus.Abandoned;
				row.UpdatedTick = Math.Max(0L, The.Game.TimeTicks);
				if (!KingdomLabRules.UpsertRegistry(rows, row)) return true;
				string written = KingdomLabRules.FormatRegistry(rows);
				The.Game.SetStringGameState(LabRegistryState, written);
				if (!string.Equals(The.Game.GetStringGameState(LabRegistryState, ""), written,
					StringComparison.Ordinal)) return true;
				if (string.Equals(marker, row.JobId, StringComparison.Ordinal))
					Actor.RemoveStringProperty(markerKey);
				Popup.Show("The orphaned receipt was abandoned. No body effect was applied and no paid cost was returned.");
				return true;
			}
			return false;
		}

		private static LabProcedure FrozenProcedure(r_KingdomLabJob Job)
		{
			if (Job == null || !KingdomLabRules.ValidEffectContract(Job.ContractVersion,
				Job.ProcedureKey, Job.FrozenGrants, Job.FrozenSource, Job.FrozenAttach,
				Job.FrozenManager, Job.FrozenFingerprint, Job.FrozenDetail)) return null;
			return new LabProcedure
			{
				Key = Job.ProcedureKey,
				DisplayName = Job.FrozenName,
				Grants = Job.FrozenGrants,
				Source = (LabSource)Job.FrozenSource,
				Attach = (LabAttach)Job.FrozenAttach,
				Magnitude = Job.FrozenMagnitude,
				Creeds = Job.FrozenCreeds,
				Class = (LabClass)Job.FrozenClass,
				Preserved = Job.KeptOwed,
				StaffDays = Job.FrozenStaffDays
			};
		}

		private static bool ValidApplicationTarget(GameObject Actor, r_KingdomLabJob Job,
			LabProcedure Procedure)
		{
			if (Actor == null || Job == null || Procedure == null
				|| !string.Equals(Actor.IDIfAssigned, Job.PatientId, StringComparison.Ordinal)
				|| !string.Equals(Actor.GetStringProperty(PendingProperty(Job.ProcedureKey)),
					Job.JobId, StringComparison.Ordinal)) return false;
			XRL.World.Anatomy.BodyPart slot = KingdomProcedures.ExactBodyPart(Actor, Job.BodyPartId);
			if (slot == null || slot.Abstract || !KingdomProcedures.BodyOwnsPart(Actor, slot))
				return false;
			GameObject bearer = (Procedure.Attach == LabAttach.Weapon)
				? slot.DefaultBehavior : Actor;
			return GameObject.Validate(bearer)
				&& string.Equals(bearer.IDIfAssigned, Job.BearerId, StringComparison.Ordinal)
				&& (Procedure.Attach != LabAttach.Weapon
					|| ReferenceEquals(slot.DefaultBehavior, bearer))
				&& !KingdomProcedures.HasProcedureClass(Actor, Procedure);
		}

		private static void EnsureJobGovernance(r_KingdomLabJob Job)
		{
			if (Job == null || Job.GovernanceCommitted) return;
			bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
				|| Job.KeptPaid > 0 || Job.KeptLost > 0 || Job.KeptQuarantined
				|| !string.Equals(Job.BitOutstanding, Job.BitClaim, StringComparison.Ordinal)
				|| Job.EffectCommitted || (int)Job.State >= (int)KingdomLabJobPhase.Working;
			if (durable && KingdomGovernanceScope.Commit("commission lab procedure"))
				Job.GovernanceCommitted = true;
		}

		private static bool CleanupApplicationMarker(GameObject Actor, r_KingdomLabJob Job)
		{
			if (Actor == null || Job == null
				|| !string.Equals(Actor.IDIfAssigned, Job.PatientId, StringComparison.Ordinal)) return false;
			Job.MarkerCleanupPending = true;
			string key = PendingProperty(Job.ProcedureKey);
			string marker = Actor.GetStringProperty(key);
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Job.JobId, StringComparison.Ordinal))
			{
				Job.SchemaQuarantined = true;
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The patient marker belongs to a different job. This receipt cleared nothing.";
				return false;
			}
			try
			{
				if (string.Equals(marker, Job.JobId, StringComparison.Ordinal))
					Actor.RemoveStringProperty(key);
			}
			catch (Exception ex)
			{
				Job.Fault = "Patient marker cleanup threw and will retry: " + ex.Message;
				return false;
			}
			if (string.Equals(Actor.GetStringProperty(key), Job.JobId, StringComparison.Ordinal))
				return false;
			Job.MarkerCleaned = true;
			return true;
		}

		private static bool FinalizeApplicationProjection(GameObject Actor, r_KingdomLabJob Job,
			KingdomLabRegistryStatus Status)
		{
			if (!CleanupApplicationMarker(Actor, Job)) return false;
			if (!WriteCanonical(Job, Status))
			{
				Job.Fault = "The canonical job registry did not accept terminal cleanup. The hall projection remains for retry.";
				return false;
			}
			Job.RegistryFinalized = true;
			return true;
		}

		private static KingdomLabOwnershipSnapshot RemovalSnapshot(r_KingdomLabRemovalJob Job)
		{
			return new KingdomLabOwnershipSnapshot(Job.ProcedureKey, Job.OriginalJobId,
				Job.PatientId, Job.BodyPartId, Job.BearerId, Job.FrozenGrants,
				Job.FrozenSource, Job.FrozenAttach, Job.FrozenManager, Job.FrozenDetail,
				Job.FrozenFingerprint, Job.PartOrdinal, Job.EffectNonce);
		}

		private static LabProcedure FrozenRemovalProcedure(r_KingdomLabRemovalJob Job)
		{
			if (Job == null || !KingdomLabRules.ValidEffectContract(Job.ContractVersion,
				Job.ProcedureKey, Job.FrozenGrants, Job.FrozenSource, Job.FrozenAttach,
				Job.FrozenManager, Job.FrozenFingerprint, Job.FrozenDetail)) return null;
			return new LabProcedure { Key = Job.ProcedureKey, DisplayName = Job.FrozenName,
				Grants = Job.FrozenGrants, Source = (LabSource)Job.FrozenSource,
				Attach = (LabAttach)Job.FrozenAttach };
		}

		private static bool CurrentRemovalAuthority(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job)
		{
			return Actor != null && System != null && Job != null && The.Game != null
				&& string.Equals(Actor.IDIfAssigned, Job.PatientId, StringComparison.Ordinal)
				&& string.Equals(The.Game.GameID, Job.GameId, StringComparison.Ordinal)
				&& string.Equals(RealmIdentity(System), Job.RealmId, StringComparison.Ordinal)
				&& System.FoundedTick == Job.RealmFoundedTick && !Job.SchemaQuarantined;
		}

	}
}
