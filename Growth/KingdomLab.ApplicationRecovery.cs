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
		private static bool ExactJobEffectPresent(GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job)
		{
			KingdomLabOwnershipSnapshot snapshot;
			return Actor != null && Procedure != null && Job != null
				&& string.Equals(Actor.ID, Job.PatientId, StringComparison.Ordinal)
				&& SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					== KingdomLabOwnedTargetState.Present;
		}

		private static void QuarantineApplicationTelling(r_KingdomLabJob Job, string Fault)
		{
			if (Job == null) return;
			bool recorded = false;
			try { recorded = WriteCanonical(Job, KingdomLabRegistryStatus.Quarantined); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: canonical telling quarantine threw (" + ex.Message + ")");
			}
			Job.RegistryFinalized = recorded;
			Job.SchemaQuarantined = true;
			Job.State = KingdomLabJobPhase.ApplicationRecovery;
			Job.Fault = Fault ?? "The exact effect changed during terminal publication.";
		}

		private static string PendingProperty(string Key)
		{
			return "r_TAF_LabPending::" + (Key ?? "").Trim().ToLowerInvariant();
		}

		private static XRL.World.Anatomy.BodyPart SelectedPart(GameObject Actor, int CensusIndex)
		{
			List<XRL.World.Anatomy.BodyPart> parts = Actor?.Body?.GetParts();
			int seen = 0;
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				if (parts[i] != null && !parts[i].Abstract && seen++ == CensusIndex)
				{
					return parts[i];
				}
			}
			return null;
		}

		private static bool ContainsBodyReference(IList<XRL.World.Anatomy.BodyPart> Parts,
			XRL.World.Anatomy.BodyPart Candidate)
		{
			for (int i = 0; Parts != null && i < Parts.Count; i++)
				if (ReferenceEquals(Parts[i], Candidate)) return true;
			return false;
		}

		private static int KeptSpent(KeptSpendPreparation Preparation)
		{
			int spent = 0;
			for (int i = 0; Preparation != null && i < Preparation.Plan.Steps.Count; i++)
			{
				KingdomKeptSpendStep step = Preparation.Plan.Steps[i];
				GameObject source = Preparation.Sources[step.Source];
				if (!GameObject.Validate(source))
				{
					spent += step.Taken;
				}
				else if (source.Count < step.Original)
				{
					spent += Math.Min(step.Taken, step.Original - Math.Max(0, source.Count));
				}
			}
			return spent;
		}

		private static KingdomLabOwnedTargetState SnapshotJobEffect(GameObject Actor,
			LabProcedure Procedure, r_KingdomLabJob Job,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			KingdomLabOwnershipSnapshot found;
			KingdomLabOwnedTargetState state = KingdomProcedures.SnapshotTracked(Actor,
				Procedure, Job.JobId, Job.BearerId, out found);
			if (!string.Equals(found.ProcedureKey, Job.ProcedureKey, StringComparison.OrdinalIgnoreCase)
				|| !string.Equals(found.PatientId, Job.PatientId, StringComparison.Ordinal)
				|| !string.Equals(found.Grants, Job.FrozenGrants, StringComparison.Ordinal)
				|| found.Source != Job.FrozenSource || found.Attach != Job.FrozenAttach
				|| !string.Equals(found.Manager, Job.FrozenManager, StringComparison.Ordinal)
				|| !string.Equals(found.Detail, Job.FrozenDetail, StringComparison.Ordinal)
				|| !string.Equals(found.Fingerprint, Job.FrozenFingerprint, StringComparison.Ordinal)
				|| (Job.EffectBodyPartId > 0 && found.BodyPartId != Job.EffectBodyPartId)
				|| (Job.EffectCommitted && found.PartOrdinal != Job.EffectPartOrdinal))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			Snapshot = found;
			return state;
		}

		private static bool RepairProcedureOwnership(GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job, KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnershipSnapshot observed;
			if (SnapshotJobEffect(Actor, Procedure, Job, out observed)
				!= KingdomLabOwnedTargetState.Present
				|| !string.Equals(observed.Fingerprint, Snapshot.Fingerprint,
					StringComparison.Ordinal))
			{
				return false;
			}
			GameObject bearer = Actor;
			if (Snapshot.Source == (int)LabSource.Part
				&& Snapshot.Attach == (int)LabAttach.Weapon)
			{
				bearer = KingdomProcedures.ExactBodyPart(Actor,
					Snapshot.BodyPartId)?.DefaultBehavior;
			}
			if (!GameObject.Validate(bearer)
				|| !string.Equals(bearer.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			string marker = bearer.GetStringProperty(
				KingdomProcedures.OwnerProperty(Snapshot.ProcedureKey));
			if (!string.IsNullOrEmpty(marker)
				&& !string.Equals(marker, Snapshot.JobId, StringComparison.Ordinal)) return false;
			try
			{
				bearer.SetStringProperty(KingdomProcedures.OwnerProperty(Snapshot.ProcedureKey),
					Snapshot.JobId);
			}
			catch { return false; }
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			record.Normalize();
			for (int i = 0; i < record.Keys.Count; i++)
			{
				if (string.Equals(record.Keys[i], Snapshot.ProcedureKey,
					StringComparison.OrdinalIgnoreCase)
					&& !string.Equals(record.JobIds[i], Snapshot.JobId,
						StringComparison.Ordinal)) return false;
			}
			XRL.World.Anatomy.BodyPart part = KingdomProcedures.ExactBodyPart(Actor,
				Snapshot.BodyPartId);
			try
			{
				record.Note(Snapshot.ProcedureKey,
					(Snapshot.Source == (int)LabSource.Mutation) ? "" : (part?.Type ?? ""),
					Snapshot.Attach == (int)LabAttach.Weapon, Snapshot.BodyPartId,
					Snapshot.BearerId, Snapshot.JobId, Job.FrozenName, Snapshot.Grants,
					Snapshot.Source, Snapshot.Attach, Snapshot.Manager, Snapshot.Detail,
					Snapshot.Fingerprint, Snapshot.PartOrdinal, Snapshot.EffectNonce);
			}
			catch { return false; }
			int at = record.IndexOf(Snapshot.ProcedureKey);
			KingdomLabOwnershipSnapshot receipt;
			return record.ContractAt(at, out receipt, Actor.ID)
				&& string.Equals(receipt.JobId, Snapshot.JobId, StringComparison.Ordinal)
				&& string.Equals(receipt.Fingerprint, Snapshot.Fingerprint,
					StringComparison.Ordinal)
				&& string.Equals(receipt.EffectNonce, Snapshot.EffectNonce,
					StringComparison.Ordinal);
		}

		/// <summary>
		/// The first of &sect;3.6's three authored happenings: the hall is spoken against.
		/// <para>
		/// It rides the petitions surface that already ships and builds nothing parallel &mdash; a
		/// named person, waiting to speak, about a thing they actually mind. There is no correct
		/// answer to it, which is the point: friction is placement constraints and named people, and
		/// never a meter (Addendum 4's pillar guard, DIVERSITY &sect;3.6's own closing rule).
		/// </para>
		/// </summary>
		private static bool Speak(KingdomSystem System, GameObject Actor, LabProcedure Procedure,
			r_KingdomLabJob Job)
		{
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			if (record.SpokenAgainst || Procedure.Class == LabClass.Rider)
			{
				return true;
			}
			List<KeyValuePair<string, int>> offended = KingdomLabRules.StandingCost(Procedure.Creeds, 1);
			int holding = 0;
			string creed = null;
			for (int i = 0; i < offended.Count; i++)
			{
				int here = CreedCount(System, offended[i].Key);
				if (here > holding)
				{
					holding = here;
					creed = offended[i].Key;
				}
			}
			if (!KingdomLabRules.SpeaksAgainstHall(holding, System.Population, record.SpokenAgainst))
			{
				return true;
			}
			// Through the shipped petitions machinery, which builds nothing parallel: a named person,
			// waiting at the Charter, about a thing they actually mind (DIVERSITY §3.6's mesh
			// condition). The latch is set only when a petition was really raised, so a founder who
			// happened to be carrying another petition still gets this one the next time.
			string petitionFaction = Job.PetitionAttemptTick >= 0L
				? Job.PetitionFaction : (creed ?? "");
			if (ExactLabPetition(System, Job, petitionFaction))
			{
				record.SpokenAgainst = true;
				return true;
			}
			if (Job.PetitionAttemptTick < 0L)
			{
				Job.PetitionAttemptTick = Math.Max(0L, The.Game?.TimeTicks ?? 0L);
				Job.PetitionFaction = petitionFaction;
			}
			bool raised = false;
			try
			{
				raised = KingdomPetitions.RaiseOnce(System,
					KingdomRules.PetitionKind.Flesh, Job.PetitionFaction,
					Job.PetitionEventId);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: petition intent " + Job.PetitionEventId
					+ " threw (" + ex.Message + ")");
			}
			if (!raised && !ExactLabPetition(System, Job, Job.PetitionFaction)) return false;
			if (!ExactLabPetition(System, Job, Job.PetitionFaction)) return false;
			record.SpokenAgainst = true;
			KingdomLog.Log("lab: hall spoken against (" + creed + " x" + holding
				+ ", event " + Job.PetitionEventId + ")");
			return true;
		}

		private static bool ExactLabPetition(KingdomSystem System, r_KingdomLabJob Job,
			string Faction)
		{
			return System != null && Job != null
				&& string.Equals(System.PetitionEventId, Job.PetitionEventId,
					StringComparison.Ordinal)
				&& System.PetitionKind == KingdomRules.PetitionKind.Flesh
				&& string.Equals(System.PetitionFaction, Faction, StringComparison.Ordinal)
				&& KingdomPetitionRules.IsActive(System.PetitionState);
		}

		private static int CreedCount(KingdomSystem System, string Creed)
		{
			int count;
			return (System.CreedCounts != null && Creed != null && System.CreedCounts.TryGetValue(Creed, out count)) ? count : 0;
		}

	}
}
