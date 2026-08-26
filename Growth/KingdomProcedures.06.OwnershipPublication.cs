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
		internal static string ContractDetail(LabProcedure Procedure)
		{
			if (Procedure?.Source != LabSource.Limb) return "";
			List<string> wanted = KingdomProcedureRules.SlotTypes(Procedure);
			return (wanted.Count > 0) ? wanted[0] : "";
		}

		internal static string ExecutionDetail(LabProcedure Procedure, string Stamp)
		{
			string catalog = ContractDetail(Procedure);
			if (Procedure?.Source == LabSource.Limb) return catalog;
			return "stamp:" + KingdomLabRules.ExecutionStampFingerprint(Stamp);
		}

		internal static bool CatalogMatchesExecutionDetail(LabProcedure Procedure, string Detail)
		{
			if (Procedure == null || Detail == null) return false;
			return Procedure.Source == LabSource.Limb
				? string.Equals(Detail, ContractDetail(Procedure), StringComparison.Ordinal)
				: Detail.StartsWith("stamp:", StringComparison.Ordinal)
					&& Detail.Length == "stamp:".Length + 16;
		}

		private static bool PrepareOwnershipIntent(GameObject Bearer, GameObject Who,
			LabProcedure Procedure, int BodyPartId, string JobId, string Manager, string Detail,
			string Fingerprint, IPart RuntimePart, int PartOrdinal,
			out XRL.World.Parts.r_KingdomLabEffectLedger Ledger, out string Failure)
		{
			Ledger = null;
			Failure = null;
			try
			{
				Ledger = Bearer.RequirePart<XRL.World.Parts.r_KingdomLabEffectLedger>();
				if (Ledger == null || CountPartClass(Bearer, nameof(XRL.World.Parts.r_KingdomLabEffectLedger)) != 1)
				{
					Failure = "The bearer has an ambiguous ownership ledger.";
					return false;
				}
				int prior = Ledger.IndexOf(Procedure.Key, JobId);
				Ledger.TrackIntent(Procedure.Key, JobId, Who.ID, BodyPartId,
					(int)Procedure.Source, (int)Procedure.Attach, Procedure.Grants, Manager,
					Detail, Fingerprint, PartOrdinal, RuntimePart);
				int ledgerAt = Ledger.IndexOf(Procedure.Key, JobId);
				string nonce = Ledger.NonceAt(ledgerAt);
				string priorOwner = Bearer.GetStringProperty(OwnerProperty(Procedure.Key));
				string priorNonce = Bearer.GetStringProperty(OwnerNonceProperty(Procedure.Key));
				if ((!string.IsNullOrEmpty(priorOwner)
						&& !string.Equals(priorOwner, JobId, StringComparison.Ordinal))
					|| (!string.IsNullOrEmpty(priorNonce)
						&& !string.Equals(priorNonce, nonce, StringComparison.Ordinal)))
				{
					Failure = "A foreign ownership marker already occupies this procedure key.";
					if (prior < 0) Ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
					return false;
				}
				Bearer.SetStringProperty(OwnerProperty(Procedure.Key), JobId ?? "");
				Bearer.SetStringProperty(OwnerNonceProperty(Procedure.Key), nonce);
				if (Ledger.IndexOf(Procedure.Key, JobId) < 0
					|| !string.Equals(Bearer.GetStringProperty(OwnerProperty(Procedure.Key)),
						JobId, StringComparison.Ordinal)
					|| !string.Equals(Bearer.GetStringProperty(
						OwnerNonceProperty(Procedure.Key)),
						Ledger.NonceAt(Ledger.IndexOf(Procedure.Key, JobId)), StringComparison.Ordinal))
				{
					Failure = "The exact ownership intent could not be published before body mutation.";
					Ledger.Forget(Procedure.Key, JobId, CleanupPatient: false);
					ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "Ownership intent publication threw before body mutation: " + ex.Message;
				try { Ledger?.Forget(Procedure.Key, JobId, CleanupPatient: false); } catch { }
				ClearOwnerIfExact(Bearer, Procedure.Key, JobId);
				return false;
			}
		}

		private static void PublishOwnership(GameObject Who, GameObject Bearer,
			LabProcedure Procedure, string Place, int BodyPartId, string JobId, string Manager,
			string Detail, string Fingerprint, IPart RuntimePart, int PartOrdinal,
			XRL.World.Parts.r_KingdomLabEffectLedger Ledger, KingdomLabGrantAttempt Attempt)
		{
			Attempt.State = KingdomLabOwnedTargetState.Present;
			Attempt.ExactPart = RuntimePart;
			Attempt.BodyPartId = BodyPartId;
			Attempt.PartOrdinal = PartOrdinal;
			Attempt.BearerId = Bearer.ID;
			try
			{
				Ledger.CommitBinding(Procedure.Key, JobId, PartOrdinal, RuntimePart);
				Bearer.SetStringProperty(OwnerProperty(Procedure.Key), JobId ?? "");
				int ledgerAt = Ledger.IndexOf(Procedure.Key, JobId);
				Bearer.SetStringProperty(OwnerNonceProperty(Procedure.Key), Ledger.NonceAt(ledgerAt));
				Record(Who).Note(Procedure.Key, Place,
					Procedure.Attach == LabAttach.Weapon, BodyPartId, Bearer.ID, JobId,
					Procedure.Named, Procedure.Grants, (int)Procedure.Source,
					(int)Procedure.Attach, Manager, Detail, Fingerprint, PartOrdinal,
					Ledger.NonceAt(ledgerAt));
			}
			catch (Exception ex)
			{
				Attempt.Failure = "The exact effect is present; post-effect ownership publication needs repair: " + ex.Message;
			}
		}

		private static void ClearOwnerIfExact(GameObject Bearer, string Key, string JobId)
		{
			try
			{
				if (GameObject.Validate(Bearer) && string.Equals(Bearer.GetStringProperty(
					OwnerProperty(Key)), JobId, StringComparison.Ordinal))
				{
					Bearer.RemoveStringProperty(OwnerProperty(Key));
					Bearer.RemoveStringProperty(OwnerNonceProperty(Key));
				}
			}
			catch { }
		}

		internal static int ReferencePartOrdinal(GameObject Bearer, IPart Part)
		{
			for (int i = 0; Bearer?.PartsList != null && i < Bearer.PartsList.Count; i++)
			{
				if (ReferenceEquals(Bearer.PartsList[i], Part)) return i;
			}
			return -1;
		}

		private static int CountPartClass(GameObject Bearer, string ClassName)
		{
			int count = 0;
			for (int i = 0; Bearer?.PartsList != null && i < Bearer.PartsList.Count; i++)
			{
				if (string.Equals(Bearer.PartsList[i]?.Name, ClassName,
					StringComparison.Ordinal)) count++;
			}
			return count;
		}

		private static bool TryRollbackExactPart(GameObject Bearer, IPart Part)
		{
			if (ReferencePartOrdinal(Bearer, Part) < 0
				&& (Part?.ParentObject == null || ReferenceEquals(Part.ParentObject, Bearer)))
			{
				return true;
			}
			try
			{
				Bearer.RemovePart(Part);
			}
			catch { }
			return ReferencePartOrdinal(Bearer, Part) < 0
				&& (Part?.ParentObject == null || ReferenceEquals(Part.ParentObject, Bearer));
		}

		internal static bool MutationListed(XRL.World.Parts.Mutations Mutations,
			XRL.World.Parts.Mutation.BaseMutation Mutation)
		{
			for (int i = 0; Mutations?.MutationList != null && i < Mutations.MutationList.Count; i++)
			{
				if (ReferenceEquals(Mutations.MutationList[i], Mutation)) return true;
			}
			return false;
		}
	}
}
