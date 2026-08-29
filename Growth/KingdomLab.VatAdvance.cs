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
		internal static void Advance(r_KingdomVatHouse Vat, long TimeTick)
		{
			if (Vat == null || Vat.ParentObject == null)
			{
				return;
			}
			RecoverVatReceipts(Vat);
			GameObject input = Pending(Vat);
			if (input == null) return;
			KingdomVatOutputPhase outputPhase = (KingdomVatOutputPhase)
				input.GetIntProperty(VatOutputPhaseProperty);
			KingdomVatRawPhase rawPhaseAtStart = (KingdomVatRawPhase)
				input.GetIntProperty(VatRawPhaseProperty);
			bool frozenOutput = !string.IsNullOrEmpty(
				input.GetStringProperty(VatOutputIdProperty));
			if (!Enum.IsDefined(typeof(KingdomVatOutputPhase), outputPhase)
				|| !Enum.IsDefined(typeof(KingdomVatRawPhase), rawPhaseAtStart)
				|| rawPhaseAtStart != KingdomVatRawPhase.Present
				|| outputPhase == KingdomVatOutputPhase.Quarantined
				|| (outputPhase == KingdomVatOutputPhase.None && frozenOutput)
				|| (outputPhase != KingdomVatOutputPhase.None && !frozenOutput)
				|| !VatRawReceiptMatches(input, Vat.ParentObject))
			{
				QuarantineVatReceipt(input, frozenOutput
					? GameObject.FindByID(input.GetStringProperty(VatOutputIdProperty)) : null);
				return;
			}
			int staffNeeded = Vat.ParentObject.GetIntProperty(KingdomAdopt.StaffNeededProperty);
			int crew = (staffNeeded > 0) ? Vat.ParentObject.GetIntProperty("KingdomEffectiveness") : 100;
			int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Vat.ParentObject));
			KingdomVatAccrual accrual = KingdomLabRules.AccrueVat(Vat.LastWorkedTick, TimeTick,
				input.GetIntProperty(VatRemainingProperty), crew, wear, Settled: false,
				Cancelled: false, IdentityAffinity: KingdomCrews.AffinityOf(Vat.ParentObject));
			Vat.LastWorkedTick = accrual.NextTick;
			input.SetIntProperty(VatRemainingProperty, accrual.RemainingTicks);
			if (crew <= 0 || wear <= 0)
			{
				if (input.GetIntProperty(VatBlockedProperty) == 0)
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					MessageQueue.AddPlayerMessage((crew <= 0)
						? "{{r|The vat-house stands idle. No crew is working the vats; assign hands or free them from other works.}}"
						: "{{r|The vat-house cannot work in its present condition. Mend it, and the crew will take the keeping up again.}}");
				}
				return;
			}
			if (!accrual.Complete)
			{
				input.RemoveIntProperty(VatBlockedProperty);
				return;
			}
			string job = input.GetStringProperty(VatJobProperty);
			GameObject output = OutputFor(Vat, input);
			KingdomVatSettlement settlement = KingdomLabRules.VatSettlement(InputPresent: true,
				OutputPresent: output != null, WorkComplete: true, CancelRequested: false);
			if (settlement == KingdomVatSettlement.CreateOutput)
			{
				if (!string.IsNullOrEmpty(input.GetStringProperty(VatOutputIdProperty)))
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					return;
				}
				output = CreateVatOutput(Vat, input, job);
				if (output == null)
				{
					if (input.GetIntProperty(VatBlockedProperty) == 0)
					{
						input.SetIntProperty(VatBlockedProperty, 1);
						MessageQueue.AddPlayerMessage("{{r|The vats finished their work but could not jar the result. The raw part remains untouched; inspect the vat-house and try again.}}");
					}
					return;
				}
				settlement = KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: true,
					WorkComplete: true, CancelRequested: false);
			}
			if (settlement != KingdomVatSettlement.ConsumeInput)
			{
				return;
			}
			if (!VatRawReceiptMatches(input, Vat.ParentObject)
				|| !VatOutputMatches(output, input, job, VatFingerprint(input, job),
				Vat.ParentObject) || output.GetIntProperty(VatOutputPhaseProperty)
					!= (int)KingdomVatOutputPhase.Added)
			{
				QuarantineVatReceipt(input, output);
				return;
			}
			// Freeze the destructive intent on both objects. If execution stops after this
			// point, recovery observes the exact raw identity once and never calls Obliterate again.
			input.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.DestroyIntent);
			output.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.DestroyIntent);
			string authorityFailure;
			if (!KingdomOrdinaryFoodAuthority.TryObjectNow(input, out authorityFailure))
			{
				QuarantineVatReceipt(input, output);
				return;
			}
			try { input.Obliterate(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: vat raw destruction intent threw (" + ex.Message + ")");
			}
			bool outputExact = VatOutputReceiptMatches(output, Vat.ParentObject);
			KingdomVatRawPhase rawPhase = KingdomLabRules.ResumeVatRaw(
				KingdomVatRawPhase.DestroyIntent, GameObject.Validate(input), outputExact);
			output.SetIntProperty(VatRawPhaseProperty, (int)rawPhase);
			if (GameObject.Validate(input)) input.SetIntProperty(VatRawPhaseProperty, (int)rawPhase);
			if (rawPhase != KingdomVatRawPhase.Destroyed)
			{
				QuarantineVatReceipt(GameObject.Validate(input) ? input : null, output);
				if (GameObject.Validate(input) && input.GetIntProperty(VatBlockedProperty) == 0)
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					MessageQueue.AddPlayerMessage("{{r|The vats have sealed the result but cannot release the raw part. Both remain in the vat-house; collect nothing until the obstruction is cleared.}}");
				}
				return;
			}
			output.SetIntProperty(VatReadyProperty, 1);
			Vat.LastWorkedTick = 0L;
			MessageQueue.AddPlayerMessage("{{G|The vat-house has finished its keeping. The sealed parts wait there for collection.}}");
		}

		private static GameObject CreateVatOutput(r_KingdomVatHouse Vat, GameObject Input, string Job)
		{
			string blueprint = Input.GetStringProperty(VatResultProperty);
			int yield = Input.GetIntProperty(VatYieldProperty);
			if (string.IsNullOrEmpty(blueprint) || yield <= 0)
			{
				return null;
			}
			GameObject kept = GameObject.Create(blueprint);
			if (kept == null || string.IsNullOrEmpty(kept.ID))
			{
				return null;
			}
			string authorityFailure;
			if (!KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure)
				|| !KingdomOrdinaryFoodAuthority.TryObjectNow(kept, out authorityFailure)) return null;
			string fingerprint = VatFingerprint(Input, Job);
			kept.Count = yield;
			kept.SetIntProperty(KeptProperty, 1);
			kept.SetStringProperty(VatOutputJobProperty, Job);
			kept.SetStringProperty(VatOutputFingerprintProperty, fingerprint);
			kept.SetStringProperty(KingdomProcedures.StampProperty,
				Input.GetStringProperty(KingdomProcedures.StampProperty));
			kept.SetStringProperty(KingdomProcedures.SourceProperty,
				Input.GetStringProperty(KingdomProcedures.SourceProperty));
			// Freeze identity before the first transfer callback. From here on, retry may
			// resolve/re-home this object only; it may never create a replacement.
			Input.SetStringProperty(VatOutputIdProperty, kept.ID);
			kept.SetStringProperty(VatOutputIdProperty, kept.ID);
			Input.SetStringProperty(VatOutputFingerprintProperty, fingerprint);
			Input.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.AddIntent);
			kept.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.AddIntent);
			kept.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Present);
			kept.SetStringProperty(VatRawIdProperty, Input.GetStringProperty(VatRawIdProperty));
			kept.SetStringProperty(VatRawBlueprintProperty,
				Input.GetStringProperty(VatRawBlueprintProperty));
			kept.SetIntProperty(VatRawCountProperty, Input.GetIntProperty(VatRawCountProperty));
			kept.SetStringProperty(VatRawFingerprintProperty,
				Input.GetStringProperty(VatRawFingerprintProperty));
			kept.SetStringProperty(VatOwnerIdProperty, Vat.ParentObject.IDIfAssigned ?? "");
			if (!string.Equals(Input.GetStringProperty(VatOutputIdProperty), kept.ID,
				StringComparison.Ordinal)) return null;
			if (!KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure)
				|| !KingdomOrdinaryFoodAuthority.TryObjectNow(kept, out authorityFailure)) return null;
			try
			{
				Vat.ParentObject.RequirePart<Inventory>().AddObject(kept, null,
					Silent: true, NoStack: true);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: vat output add intent threw (" + ex.Message + ")");
			}
			KingdomVatOutputPhase phase = KingdomLabRules.ResumeVatOutput(
				KingdomVatOutputPhase.AddIntent,
				VatOutputMatches(kept, Input, Job, fingerprint, Vat.ParentObject));
			Input.SetIntProperty(VatOutputPhaseProperty, (int)phase);
			kept.SetIntProperty(VatOutputPhaseProperty, (int)phase);
			if (phase != KingdomVatOutputPhase.Added)
			{
				QuarantineVatReceipt(Input, kept);
				return null;
			}
			return kept;
		}

	}
}
