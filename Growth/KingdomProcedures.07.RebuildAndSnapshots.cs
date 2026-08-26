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
		/// <summary>Actual global class presence across founder and every natural-weapon bearer.</summary>
		public static bool HasProcedureClass(GameObject Who, LabProcedure Procedure)
		{
			if (Who == null || Procedure == null)
			{
				return false;
			}
			if (Procedure.Source == LabSource.Mutation)
			{
				// Modifier-backed mutations live as BaseMutation parts but are deliberately absent
				// from Mutations.MutationList. AddMutation removes such a part before adding its own;
				// checking the live part is therefore the non-destructive global collision test.
				return Who.GetPart(Procedure.Grants) is XRL.World.Parts.Mutation.BaseMutation;
			}
			if (Procedure.Source == LabSource.Limb)
			{
				List<BodyPart> held = AllBodyParts(Who);
				for (int i = 0; held != null && i < held.Count; i++)
				{
					if (string.Equals(held[i]?.Manager, ManagerFor(Procedure.Key), StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}
			if (Who.GetPart(Procedure.Grants) != null)
			{
				return true;
			}
			List<BodyPart> parts = AllBodyParts(Who);
			List<GameObject> seen = new List<GameObject>();
			for (int i = 0; parts != null && i < parts.Count; i++)
			{
				GameObject bearer = parts[i]?.DefaultBehavior;
				if (GameObject.Validate(bearer) && !seen.Contains(bearer))
				{
					seen.Add(bearer);
					if (bearer.GetPart(Procedure.Grants) != null)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Rebuilds one part from a stamp: instantiate the type, then set its fields from strings
		/// &mdash; which is exactly what the engine does with every part in every blueprint it
		/// loads, and what the precedent's own repertoire says it is mirroring.
		/// </summary>
		private static bool TryRebuild(string ClassName, string Stamp, out IPart Part)
		{
			Part = null;
			if (string.IsNullOrEmpty(ClassName) || KingdomProcedureRules.Blocked(ClassName))
			{
				return false;
			}
			Type type = ModManager.ResolveType("XRL.World.Parts." + ClassName)
				?? ModManager.ResolveType("XRL.World.Parts.Mutation." + ClassName);
			if (type == null || !typeof(IPart).IsAssignableFrom(type))
			{
				return false;
			}
			IPart built = Activator.CreateInstance(type) as IPart;
			if (built == null)
			{
				return false;
			}
			FieldInfo[] declared = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
			for (int i = 0; i < declared.Length; i++)
			{
				string raw = KingdomProcedureRules.StampedField(Stamp, ClassName, declared[i].Name);
				if (raw == null || declared[i].IsLiteral || !IsPlain(declared[i].FieldType))
				{
					continue;
				}
				try
				{
					declared[i].SetValue(built, declared[i].FieldType.IsEnum
						? Enum.Parse(declared[i].FieldType, raw, ignoreCase: true)
						: Convert.ChangeType(raw, declared[i].FieldType, System.Globalization.CultureInfo.InvariantCulture));
				}
				catch (Exception e)
				{
					// One unreadable field costs its own field and nothing else: the part still
					// rebuilds, at that field's own default, and the log says which one went.
					KingdomLog.Log("KingdomProcedures: " + ClassName + "." + declared[i].Name + " would not read back (" + e.Message + ").");
				}
			}
			Part = built;
			return true;
		}

		/// <summary>Freezes the exact ownership identity before a removal receipt can spend water.
		/// A pre-ledger record is deliberately not upgraded by guessing.</summary>
		internal static KingdomLabOwnedTargetState SnapshotOwned(GameObject Who, string Key,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || string.IsNullOrEmpty(Key))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			XRL.World.Parts.r_KingdomLabRecord record = Record(Who);
			record.Normalize();
			int at = record.IndexOf(Key);
			if (at < 0)
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (!record.ContractAt(at, out Snapshot, Who.ID))
			{
				// Legacy type/manager/ordinal rows remain visible to the slate, but are
				// read-only quarantine. They cannot mint mutation authority by inference.
				return KingdomLabOwnedTargetState.Uncertain;
			}
			if (Snapshot.Source == (int)LabSource.Limb && !EnsureLimbLedger(Who, Snapshot))
			{
				return KingdomLabOwnedTargetState.Uncertain;
			}
			KingdomLabOwnedTarget target;
			return ClassifyOwned(Who, Snapshot, out target);
		}

		private static bool TryMigrateLegacyLimb(GameObject Who, LabProcedure Procedure,
			XRL.World.Parts.r_KingdomLabRecord Record, int At,
			out KingdomLabOwnershipSnapshot Snapshot)
		{
			Snapshot = default(KingdomLabOwnershipSnapshot);
			if (Who == null || Procedure?.Source != LabSource.Limb || Record.RegistryQuarantined
				|| At < 0 || At >= Record.Keys.Count || !string.IsNullOrEmpty(Record.Fingerprints[At])
				|| At >= Record.EffectNonces.Count || Record.EffectNonces[At].Length != 32)
			{
				return false;
			}
			string manager = ManagerFor(Procedure.Key);
			BodyPart exact = null;
			List<BodyPart> all = AllBodyParts(Who);
			for (int i = 0; i < all.Count; i++)
			{
				if (!string.Equals(all[i]?.Manager, manager, StringComparison.Ordinal)) continue;
				if (exact != null) return false;
				exact = all[i];
			}
			if (exact == null || !BodyOwnsPart(Who, exact)
				|| (Record.BodyPartIds[At] > 0 && Record.BodyPartIds[At] != exact.ID)) return false;
			string detail = ContractDetail(Procedure);
			string fingerprint = KingdomLabRules.EffectFingerprint(
				KingdomLabRules.EffectContractVersion, Procedure.Key, Procedure.Grants,
				(int)Procedure.Source, (int)Procedure.Attach, manager, detail);
			string job = string.IsNullOrEmpty(Record.JobIds[At])
				? Guid.NewGuid().ToString("N") : Record.JobIds[At];
			if (!Record.UpgradeLegacyLimbAt(At, exact.ID, Who.ID, job, Procedure.Named,
				Procedure.Grants, (int)Procedure.Attach, manager, detail, fingerprint)) return false;
			if (!Record.ContractAt(At, out Snapshot, Who.ID)) return false;
			return EnsureLimbLedger(Who, Snapshot);
		}

		private static bool EnsureLimbLedger(GameObject Who, KingdomLabOwnershipSnapshot Snapshot)
		{
			if (Who == null || Snapshot.Source != (int)LabSource.Limb
				|| !string.Equals(Who.ID, Snapshot.PatientId, StringComparison.Ordinal)
				|| !string.Equals(Who.ID, Snapshot.BearerId, StringComparison.Ordinal)) return false;
			BodyPart limb = ExactBodyPart(Who, Snapshot.BodyPartId);
			if (limb == null || !BodyOwnsPart(Who, limb)
				|| !string.Equals(limb.Manager, Snapshot.Manager, StringComparison.Ordinal)) return false;
			try
			{
				XRL.World.Parts.r_KingdomLabEffectLedger ledger =
					Who.RequirePart<XRL.World.Parts.r_KingdomLabEffectLedger>();
				int at = ledger.IndexOf(Snapshot.ProcedureKey, Snapshot.JobId);
				if (at < 0)
				{
					ledger.TrackIntent(Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
						Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
						Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint, -1, null,
						Snapshot.EffectNonce);
				}
				else if (!ledger.EntryMatches(at, Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
					Snapshot.BodyPartId, Snapshot.Source, Snapshot.Attach, Snapshot.Grants,
					Snapshot.Manager, Snapshot.Detail, Snapshot.Fingerprint, -1))
				{
					if (!ledger.UpgradeLegacyLimb(Snapshot.ProcedureKey, Snapshot.JobId, Who.ID,
						Snapshot.BodyPartId, Snapshot.Attach, Snapshot.Grants, Snapshot.Manager,
						Snapshot.Detail, Snapshot.Fingerprint)) return false;
				}
				string marker = Who.GetStringProperty(OwnerProperty(Snapshot.ProcedureKey));
				if (!string.IsNullOrEmpty(marker) && !string.Equals(marker, Snapshot.JobId,
					StringComparison.Ordinal)) return false;
				Who.SetStringProperty(OwnerProperty(Snapshot.ProcedureKey), Snapshot.JobId);
				Who.SetStringProperty(OwnerNonceProperty(Snapshot.ProcedureKey),
					Snapshot.EffectNonce);
				ledger.CommitBinding(Snapshot.ProcedureKey, Snapshot.JobId, -1, null);
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact legacy limb migration stopped (" + ex.Message + ")");
				return false;
			}
		}
	}
}
