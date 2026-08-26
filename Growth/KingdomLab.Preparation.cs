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
		// ==================================================================================
		// Rung 0 — the slab
		// ==================================================================================

		/// <summary>
		/// Reads a carcass, then lets vanilla butcher it.
		/// <para>
		/// The order is the whole point and the precedent wrote the lesson down: the butchering
		/// destroys the source, so <i>nothing useful can be read from the target afterward</i>. The
		/// stamp is taken first, off the whole creature, and travels on whatever comes off.
		/// </para>
		/// </summary>
		internal static void Dress(GameObject Actor)
		{
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
			List<GameObject> carcasses = new List<GameObject>();
			List<string> names = new List<string>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item != null && item.HasPart("Butcherable"))
				{
					carcasses.Add(item);
					names.Add(item.DisplayName);
				}
			}
			if (carcasses.Count == 0)
			{
				// 7b's applicable-but-blocked case: the slab works and there is nothing on it, and
				// nothing else in the game would ever say so.
				Popup.Show("There is nothing on you the slab could open. Bring a carcass home whole.");
				return;
			}
			int picked = Popup.PickOption(Title: "Dress a carcass", Options: names, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject carcass = carcasses[picked];
			string stamp = KingdomProcedures.Stamp(carcass);
			string source = carcass.DisplayNameOnly;
			// Carried on the carcass, so that whatever vanilla's own butchery makes of it inherits
			// the reading through the ordinary property path rather than through a hook of ours.
			carcass.SetStringProperty(KingdomProcedures.StampProperty, stamp);
			carcass.SetStringProperty(KingdomProcedures.SourceProperty, source);
			MessageQueue.AddPlayerMessage(string.IsNullOrEmpty(stamp)
				? ("{{K|There was nothing about " + source + " worth writing down. It is still meat.}}")
				: ("{{G|What " + source + " was carrying is written down. Butcher it, and take what comes off to the vats.}}"));
		}

		// ==================================================================================
		// Rung 1 — the vats
		// ==================================================================================

		/// <summary>
		/// Puts one raw part up to keep.
		/// <para>
		/// The yield is vanilla's own and nothing else: <c>PreservableItem.Number</c> times the
		/// stack (<c>D/XRL/World/Parts/Campfire.cs:543-557</c>). Inventing a multiplier here would
		/// be inventing a second economy on top of one that already works, and the vat-house would
		/// stop being a rendering of the game and start being a machine of ours.
		/// </para>
		/// </summary>
		internal static void Keep(r_KingdomVatHouse Vat, GameObject Actor)
		{
			if (Vat == null || Vat.ParentObject == null || Actor == null)
			{
				return;
			}
			Advance(Vat, The.Game?.TimeTicks ?? Vat.LastWorkedTick);
			GameObject pending = Pending(Vat);
			if (pending != null)
			{
				ManagePending(Vat, Actor, pending);
				return;
			}
			List<GameObject> ready = VatContents(Vat, VatReadyProperty);
			if (ready.Count > 0)
			{
				Collect(Vat, Actor, ready);
				return;
			}
			// The option gates only a new keeping. Existing physical receipts must remain
			// recoverable and collectable after the player turns new lab work off.
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
			List<GameObject> raw = new List<GameObject>();
			List<string> names = new List<string>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item == null || item.GetIntProperty(KeptProperty) == 1
					|| item.GetIntProperty(VatPendingProperty) == 1)
				{
					continue;
				}
				string stamp = item.GetStringProperty(KingdomProcedures.StampProperty);
				if (!string.IsNullOrEmpty(stamp) || item.HasPart("DismemberedProperties"))
				{
					raw.Add(item);
					names.Add(item.DisplayName);
				}
			}
			if (raw.Count == 0)
			{
				Popup.Show("The vats have nothing to work on. Dress a carcass at the slab first — what the vats keep is what the slab took a reading of.");
				return;
			}
			int picked = Popup.PickOption(Title: "Put a part up to keep", Options: names, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject part = raw[picked];
			XRL.World.Parts.PreservableItem preservable = part.GetPart<XRL.World.Parts.PreservableItem>();
			int yield = KingdomProcedureRules.PreservedYield((preservable == null) ? 1 : preservable.Number, part.Count);
			if (yield <= 0)
			{
				Popup.Show("Nothing would come out of the vats for that. It is not the kind of thing that keeps.");
				return;
			}
			if (Popup.ShowYesNoCancel("The vats will keep " + part.DisplayName + " — {{C|" + yield + "}} "
				+ ((yield == 1) ? "part" : "parts") + ", after {{C|" + KingdomProcedureRules.PreserveDays
				+ "}} day of the vat crew's work.\n\nWhat is kept is permanent. It can be stored, traded, or spent at the hall.") != DialogResult.Yes)
			{
				return;
			}
			string stamp2 = part.GetStringProperty(KingdomProcedures.StampProperty);
			string source = part.GetStringProperty(KingdomProcedures.SourceProperty);
			string blueprint = (preservable == null || string.IsNullOrEmpty(preservable.Result)) ? part.Blueprint : preservable.Result;
			if (string.IsNullOrEmpty(blueprint))
			{
				MessageQueue.AddPlayerMessage("{{r|The vats could not make anything of it.}}");
				return;
			}
			string job = string.IsNullOrEmpty(part.ID) ? Guid.NewGuid().ToString("N") : part.ID;
			part.SetIntProperty(VatPendingProperty, 1);
			part.SetIntProperty(VatRemainingProperty,
				KingdomProcedureRules.StaffDayTicks(KingdomProcedureRules.PreserveDays));
			part.SetStringProperty(VatResultProperty, blueprint);
			part.SetIntProperty(VatYieldProperty, yield);
			part.SetStringProperty(VatJobProperty, job);
			part.SetStringProperty(KingdomProcedures.StampProperty, stamp2);
			part.SetStringProperty(KingdomProcedures.SourceProperty, source);
			part.SetIntProperty(VatOutputPhaseProperty, (int)KingdomVatOutputPhase.None);
			part.SetIntProperty(VatRawPhaseProperty, (int)KingdomVatRawPhase.Present);
			part.SetStringProperty(VatRawIdProperty, part.ID ?? "");
			part.SetStringProperty(VatRawBlueprintProperty, part.Blueprint ?? "");
			part.SetIntProperty(VatRawCountProperty, part.Count);
			part.SetStringProperty(VatRawFingerprintProperty,
				KingdomLabRules.VatRawFingerprint(job, part.ID, part.Blueprint, part.Count,
					stamp2, source));
			part.SetStringProperty(VatOwnerIdProperty, Vat.ParentObject.ID ?? "");
			Inventory inventory = Vat.ParentObject.RequirePart<Inventory>();
			inventory.AddObjectToInventory(part, Actor, Silent: true, NoStack: true);
			if (!VatRawReceiptMatches(part, Vat.ParentObject))
			{
				Actor.RequirePart<Inventory>().AddObjectToInventory(part, Actor, Silent: true, NoStack: true);
				ClearPending(part);
				Popup.Show((part.Physics != null && part.Physics.InInventory == Actor)
					? "The vats could not take hold of that part. It is back in your hands; nothing was spent."
					: "The vats could not take hold of that part. Check the ground and your inventory; the raw part was not consumed.");
				return;
			}
			Vat.LastWorkedTick = The.Game?.TimeTicks ?? 0L;
			MessageQueue.AddPlayerMessage("{{G|" + KingdomLabRules.StakedLine("keeping " + source,
				KingdomProcedureRules.PreserveDays) + "}}");
		}

		internal static bool HasPending(r_KingdomVatHouse Vat)
		{
			List<GameObject> contents = Vat?.ParentObject?.Inventory?.Objects;
			for (int i = 0; contents != null && i < contents.Count; i++)
			{
				if (contents[i] != null && contents[i].GetIntProperty(VatPendingProperty) == 1)
				{
					return true;
				}
			}
			return false;
		}

	}
}
