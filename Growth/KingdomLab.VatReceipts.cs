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
		private static string VatFingerprint(GameObject Input, string Job)
		{
			return KingdomLabRules.VatOutputFingerprint(Job,
				Input?.GetStringProperty(VatResultProperty), Input?.GetIntProperty(VatYieldProperty) ?? 0,
				Input?.GetStringProperty(KingdomProcedures.StampProperty),
				Input?.GetStringProperty(KingdomProcedures.SourceProperty));
		}

		private static bool VatOutputMatches(GameObject Output, GameObject Input, string Job,
			string Fingerprint, GameObject VatOwner)
		{
			return GameObject.Validate(Output) && Output.GetIntProperty(KeptProperty) == 1
				&& GameObject.Validate(Input) && GameObject.Validate(VatOwner)
				&& Output.Physics != null && ReferenceEquals(Output.Physics.InInventory, VatOwner)
				&& string.Equals(Output.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)
				&& !string.IsNullOrEmpty(Output.ID)
				&& string.Equals(Output.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				&& string.Equals(Input.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatOutputJobProperty), Job,
					StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatOutputFingerprintProperty),
					Fingerprint, StringComparison.Ordinal)
				&& string.Equals(Input.GetStringProperty(VatOutputFingerprintProperty),
					Fingerprint, StringComparison.Ordinal)
				&& Output.Count == Input.GetIntProperty(VatYieldProperty)
				&& string.Equals(Output.Blueprint, Input.GetStringProperty(VatResultProperty),
					StringComparison.Ordinal);
		}

		private static bool VatOutputReceiptMatches(GameObject Output, GameObject VatOwner)
		{
			if (!GameObject.Validate(Output) || !GameObject.Validate(VatOwner)
				|| Output.Physics == null || !ReferenceEquals(Output.Physics.InInventory, VatOwner)
				|| Output.GetIntProperty(KeptProperty) != 1
				|| Output.GetIntProperty(VatOutputPhaseProperty)
					!= (int)KingdomVatOutputPhase.Added
				|| string.IsNullOrEmpty(Output.ID)
				|| !string.Equals(Output.GetStringProperty(VatOutputIdProperty), Output.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Output.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)) return false;
			string job = Output.GetStringProperty(VatOutputJobProperty);
			string fingerprint = KingdomLabRules.VatOutputFingerprint(job, Output.Blueprint,
				Output.Count, Output.GetStringProperty(KingdomProcedures.StampProperty),
				Output.GetStringProperty(KingdomProcedures.SourceProperty));
			return !string.IsNullOrEmpty(job)
				&& string.Equals(Output.GetStringProperty(VatOutputFingerprintProperty),
					fingerprint, StringComparison.Ordinal)
				&& string.Equals(Output.GetStringProperty(VatRawFingerprintProperty),
					KingdomLabRules.VatRawFingerprint(job,
						Output.GetStringProperty(VatRawIdProperty),
						Output.GetStringProperty(VatRawBlueprintProperty),
						Output.GetIntProperty(VatRawCountProperty),
						Output.GetStringProperty(KingdomProcedures.StampProperty),
						Output.GetStringProperty(KingdomProcedures.SourceProperty)),
					StringComparison.Ordinal);
		}

		private static bool VatRawReceiptMatches(GameObject Raw, GameObject VatOwner)
		{
			if (!GameObject.Validate(Raw) || !GameObject.Validate(VatOwner)
				|| Raw.Physics == null || !ReferenceEquals(Raw.Physics.InInventory, VatOwner)
				|| string.IsNullOrEmpty(Raw.ID)
				|| !string.Equals(Raw.GetStringProperty(VatRawIdProperty), Raw.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Raw.GetStringProperty(VatOwnerIdProperty), VatOwner.ID,
					StringComparison.Ordinal)
				|| !string.Equals(Raw.GetStringProperty(VatRawBlueprintProperty), Raw.Blueprint,
					StringComparison.Ordinal)
				|| Raw.GetIntProperty(VatRawCountProperty) != Raw.Count) return false;
			string job = Raw.GetStringProperty(VatJobProperty);
			return !string.IsNullOrEmpty(job)
				&& string.Equals(Raw.GetStringProperty(VatRawFingerprintProperty),
					KingdomLabRules.VatRawFingerprint(job, Raw.ID, Raw.Blueprint, Raw.Count,
						Raw.GetStringProperty(KingdomProcedures.StampProperty),
						Raw.GetStringProperty(KingdomProcedures.SourceProperty)),
					StringComparison.Ordinal);
		}

		private static void RecoverVatReceipts(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				GameObject output = contents[i];
				if (output == null || string.IsNullOrEmpty(
					output.GetStringProperty(VatOutputJobProperty))) continue;
				KingdomVatRawPhase phase = (KingdomVatRawPhase)
					output.GetIntProperty(VatRawPhaseProperty);
				if (phase != KingdomVatRawPhase.DestroyIntent) continue;
				GameObject raw = GameObject.FindByID(output.GetStringProperty(VatRawIdProperty));
				phase = KingdomLabRules.ResumeVatRaw(phase, GameObject.Validate(raw),
					VatOutputReceiptMatches(output, Vat.ParentObject));
				output.SetIntProperty(VatRawPhaseProperty, (int)phase);
				if (phase == KingdomVatRawPhase.Destroyed)
				{
					output.SetIntProperty(VatReadyProperty, 1);
				}
				else
				{
					QuarantineVatReceipt(raw, output);
				}
			}
		}

		private static List<GameObject> VatContents(r_KingdomVatHouse Vat, string Marker)
		{
			List<GameObject> result = new List<GameObject>();
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(Marker) == 1)
				{
					result.Add(contents[i]);
				}
			}
			return result;
		}

		private static void ClearPending(GameObject Input)
		{
			Input.RemoveIntProperty(VatPendingProperty);
			Input.RemoveIntProperty(VatRemainingProperty);
			Input.RemoveStringProperty(VatResultProperty);
			Input.RemoveIntProperty(VatYieldProperty);
			Input.RemoveStringProperty(VatJobProperty);
			Input.RemoveStringProperty(VatOutputIdProperty);
			Input.RemoveStringProperty(VatOutputFingerprintProperty);
			Input.RemoveIntProperty(VatOutputPhaseProperty);
			Input.RemoveIntProperty(VatRawPhaseProperty);
			Input.RemoveStringProperty(VatRawIdProperty);
			Input.RemoveStringProperty(VatRawBlueprintProperty);
			Input.RemoveIntProperty(VatRawCountProperty);
			Input.RemoveStringProperty(VatRawFingerprintProperty);
			Input.RemoveStringProperty(VatOwnerIdProperty);
			Input.RemoveIntProperty(VatBlockedProperty);
		}
	}
}
