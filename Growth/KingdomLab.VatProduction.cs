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
		private static void ManagePending(r_KingdomVatHouse Vat, GameObject Actor, GameObject Input)
		{
			if (!VatRawReceiptMatches(Input, Vat.ParentObject))
			{
				QuarantineVatReceipt(Input, null);
				Popup.Show("The vat's raw-part custody is protected or no longer exact. It was not moved.");
				return;
			}
			int remaining = Input.GetIntProperty(VatRemainingProperty);
			GameObject output = OutputFor(Vat, Input);
			if (output != null)
			{
				Popup.Show("The keeping is finished and its sealed result is already in the vat-house, but the raw part has not released. Nothing can be collected or cancelled until the obstruction is cleared.");
				return;
			}
			if (!string.IsNullOrEmpty(Input.GetStringProperty(VatOutputIdProperty)))
			{
				Popup.Show("The vat has frozen an exact output identity, but that same object is missing or no longer matches its receipt. The raw input and receipt are quarantined; cancellation cannot create or return a duplicate.");
				return;
			}
			int crew = Vat.ParentObject.GetIntProperty("KingdomEffectiveness");
			int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Vat.ParentObject));
			int whole = KingdomProcedureRules.StaffDayTicks(KingdomProcedureRules.PreserveDays);
			long earned = (long)whole - remaining;
			int done = (whole > 0) ? (int)(earned * 100L / whole) : 100;
			if (done < 0)
			{
				done = 0;
			}
			else if (done > 100)
			{
				done = 100;
			}
			string state;
			if (crew <= 0)
			{
				state = "{{r|Nobody is working the vats. No idle time has counted as work.}}";
			}
			else if (wear <= 0)
			{
				state = "{{r|The vat-house needs mending before the crew can continue.}}";
			}
			else
			{
				state = "The keeping is {{C|" + done + "%}} done, and the crew is working.";
			}
			int picked = Popup.PickOption(Title: "The vat-house",
				Intro: Input.DisplayName + " is still in the vats. " + state,
				Options: new string[2] { "Leave it in the crew's hands.", "Cancel the keeping and take the raw part back." },
				AllowEscape: true);
			if (picked != 1 || Popup.ShowYesNo("Cancel this keeping? The raw part returns unchanged, and the work already spent is lost.") != DialogResult.Yes)
			{
				return;
			}
			if (KingdomLabRules.VatSettlement(InputPresent: true, OutputPresent: false,
				WorkComplete: remaining <= 0, CancelRequested: true) != KingdomVatSettlement.ReturnInput)
			{
				return;
			}
			string inputId = Input.IDIfAssigned;
			string inputBlueprint = Input.Blueprint;
			int inputCount = Input.Count;
			string authorityFailure;
			if (!KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure)) return;
			Actor.RequirePart<Inventory>().AddObjectToInventory(Input, Actor, Silent: true, NoStack: true);
			bool landed = LabObjectAt(Input, Actor, null, inputId, inputBlueprint, inputCount)
				&& KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure);
			if (!landed)
			{
				if (KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure)
					&& Input.IDIfAssigned == inputId && Input.Blueprint == inputBlueprint
					&& Input.Count == inputCount)
					Vat.ParentObject.RequirePart<Inventory>().AddObjectToInventory(Input, Actor,
						Silent: true, NoStack: true);
				Popup.Show("The vat-house could not hand the part back. The raw part was not consumed; inspect the vats before trying again.");
				return;
			}
			ClearPending(Input);
			Vat.LastWorkedTick = 0L;
			MessageQueue.AddPlayerMessage("{{K|The keeping was cancelled. The raw part is back in your hands.}}");
		}

		private static void Collect(r_KingdomVatHouse Vat, GameObject Actor, List<GameObject> Ready)
		{
			int taken = 0;
			Inventory inventory = Actor.RequirePart<Inventory>();
			for (int i = 0; i < Ready.Count; i++)
			{
				GameObject output = Ready[i];
				string outputId = output.IDIfAssigned;
				string outputBlueprint = output.Blueprint;
				int outputCount = output.Count;
				if (output.GetIntProperty(VatOutputPhaseProperty)
						!= (int)KingdomVatOutputPhase.Added
					|| output.GetIntProperty(VatRawPhaseProperty)
						!= (int)KingdomVatRawPhase.Destroyed
					|| !VatOutputReceiptMatches(output, Vat.ParentObject))
				{
					QuarantineVatReceipt(null, output);
					continue;
				}
				inventory.AddObjectToInventory(output, Actor, Silent: true, NoStack: true);
				string authorityFailure;
				if (LabObjectAt(output, Actor, null, outputId, outputBlueprint, outputCount)
					&& KingdomOrdinaryFoodAuthority.TryObjectNow(output, out authorityFailure))
				{
					output.RemoveIntProperty(VatReadyProperty);
					output.RemoveStringProperty(VatOutputJobProperty);
					output.RemoveStringProperty(VatOwnerIdProperty);
					taken += output.Count;
				}
				else
				{
					if (KingdomOrdinaryFoodAuthority.TryObjectNow(output, out authorityFailure)
						&& output.IDIfAssigned == outputId && output.Blueprint == outputBlueprint
						&& output.Count == outputCount)
						Vat.ParentObject.RequirePart<Inventory>().AddObjectToInventory(output, Actor,
							Silent: true, NoStack: true);
				}
			}
			if (taken > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|You collect " + taken + " kept "
					+ ((taken == 1) ? "part" : "parts") + " from the vat-house.}}");
			}
			else
			{
				Popup.Show("The sealed parts could not be handed over. They remain in the vat-house.");
			}
		}

		private static GameObject Pending(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(VatPendingProperty) == 1)
				{
					return contents[i];
				}
			}
			return null;
		}

		private static GameObject OutputFor(r_KingdomVatHouse Vat, GameObject Input)
		{
			if (Input == null) return null;
			string job = Input.GetStringProperty(VatJobProperty);
			string expected = VatFingerprint(Input, job);
			string frozenId = Input.GetStringProperty(VatOutputIdProperty);
			if (!string.IsNullOrEmpty(frozenId))
			{
				GameObject exact = GameObject.FindByID(frozenId);
				bool matches = VatOutputMatches(exact, Input, job, expected, Vat?.ParentObject);
				KingdomVatOutputDecision decision = KingdomLabRules.VatOutputIdentity(
					FrozenId: true, Resolved: GameObject.Validate(exact),
					FingerprintMatches: matches);
				if (decision != KingdomVatOutputDecision.UseExact)
				{
					QuarantineVatReceipt(Input, exact);
					return null;
				}
				KingdomVatOutputPhase phase = (KingdomVatOutputPhase)
					Input.GetIntProperty(VatOutputPhaseProperty);
				if (!Enum.IsDefined(typeof(KingdomVatOutputPhase), phase)
					|| phase == KingdomVatOutputPhase.Quarantined)
				{
					QuarantineVatReceipt(Input, exact);
					return null;
				}
				if (phase == KingdomVatOutputPhase.AddIntent)
				{
					phase = KingdomLabRules.ResumeVatOutput(phase, matches);
					Input.SetIntProperty(VatOutputPhaseProperty, (int)phase);
					exact.SetIntProperty(VatOutputPhaseProperty, (int)phase);
				}
				if (phase != KingdomVatOutputPhase.Added) return null;
				return exact;
			}
			// A pre-receipt output may be inspected but is never adopted by job/class/ordinal.
			// Only a new job with no output intent reaches CreateVatOutput.
			return null;
		}

		private static void QuarantineVatReceipt(GameObject Input, GameObject Output)
		{
			string authorityFailure;
			if (GameObject.Validate(Input)
				&& KingdomOrdinaryFoodAuthority.TryObjectNow(Input, out authorityFailure))
			{
				Input.SetIntProperty(VatOutputPhaseProperty,
					(int)KingdomVatOutputPhase.Quarantined);
				Input.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Quarantined);
				Input.SetIntProperty(VatBlockedProperty, 1);
			}
			if (GameObject.Validate(Output)
				&& KingdomOrdinaryFoodAuthority.TryObjectNow(Output, out authorityFailure))
			{
				Output.SetIntProperty(VatOutputPhaseProperty,
					(int)KingdomVatOutputPhase.Quarantined);
				Output.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Quarantined);
				Output.RemoveIntProperty(VatReadyProperty);
				Output.SetIntProperty(VatBlockedProperty, 1);
			}
		}

	}
}
