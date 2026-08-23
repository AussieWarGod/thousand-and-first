using System;
using System.Collections.Generic;

using ThousandAndFirst;

// XRL.World.Parts, for the reason r_KingdomPlot, r_KingdomSeed and r_KingdomMirrorGate all state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name. Only the parts move; everything they do lives in ThousandAndFirst.KingdomLab below.
namespace XRL.World.Parts
{
	/// <summary>
	/// The butcher's slab. Rung 0, and not the lab: the work that turns what the founder drags home
	/// into parts.
	/// <para>
	/// It invents no butchery. Vanilla's <c>Butcherable</c> and <c>Corpse</c> already do the whole
	/// job, gated on the founder's own <c>CookingAndGathering_Butchery</c> skill, and Addendum 11(c)
	/// says inherit rather than reinvent. What the slab adds is the one thing vanilla has no opinion
	/// about: reading what the creature was BEARING before the knife, and stamping it onto what
	/// comes off, so that a part can still be a part a season later.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomButcherSlab : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Dress", "dress a carcass on the slab", "r_DressCarcass", null, 'd', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_DressCarcass" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("butcher slab", delegate
				{
					KingdomLab.Dress(E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The vat-house. Rung 1, and the preservation chain lives here.
	/// <para>
	/// <b>Nothing rots, here or anywhere.</b> Vanilla has no rot at all &mdash;
	/// <c>PreservableItem</c> is two fields and no behaviour
	/// (<c>D/XRL/World/Parts/PreservableItem.cs:8,10</c>) &mdash; and a decay timer would be a rate
	/// running on time alone, which Addendum 8 clause 2 forbids outright. What gates the chain is
	/// LABOUR: a staffed work, real hands, real world-days. An empty vat-house keeps what it holds
	/// forever and preserves nothing new.
	/// </para>
	/// <para>
	/// The point of it is not the gate. A preserved part is a permanent, storable, tradeable item
	/// the day it exists, so the vat-house is worth building for a founder who never raises the hall
	/// at all &mdash; a bonus for engaging, never a penalty for abstaining.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomVatHouse : IPart
	{
		/// <summary>Tick this vat last settled its labour to. Zero until the first day boundary
		/// plants it, so a vat never works for the day it was raised &mdash; the same discipline
		/// <c>r_KingdomMirrorGate.LastDrawTick</c> keeps, and for the same reason.
		/// <para>
		/// This stays the part's only serialized field. Pending work lives on the physical input's
		/// ordinary property dictionaries inside the vat's ordinary inventory, so both halves ride
		/// the engine's existing object serialization and this part's positional save layout
		/// never changes.
		/// </para>
		/// </summary>
		public long LastWorkedTick;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (KingdomLab.HasPending(this))
			{
				KingdomSystem.Guard("vat-house work", delegate
				{
					KingdomLab.Advance(this, TimeTick);
				});
			}
			base.TurnTick(TimeTick, Amount);
		}

		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Keep", "put a part up to keep", "r_KeepPart", null, 'k', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_KeepPart" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("vat house", delegate
				{
					KingdomLab.Keep(this, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The grafting hall. Rung 2, and the lab proper: Class I and Class II.
	/// <para>
	/// <b>The verb is on the building and there is no charter hotkey.</b> The Charter's letters are
	/// full at thirty-six and a new entry there would be a chapter rather than a line, so the slate
	/// opens where the work is done &mdash; which is also where the founder is standing when they
	/// want it, and is the same call the mirror-gate's own dedication made.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomGraftingHall : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Slate", "read the hall's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenLabSlate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("lab slate", delegate
				{
					KingdomLab.OpenSlate(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}

	/// <summary>
	/// The chimeric theatre. Rung 3, Class III, the four named procedures &mdash; and the city's one
	/// purpose (Addendum 22 A1, Design B).
	/// <para>
	/// It carries the same slate as the hall, because it IS the hall with its ceiling raised, and a
	/// second screen for the same act would be a second screen for the same act.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomChimericTheatre : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			if (!base.WantEvent(ID, cascade) && ID != GetInventoryActionsEvent.ID)
			{
				return ID == InventoryActionEvent.ID;
			}
			return true;
		}

		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Slate", "read the theatre's slate", "r_OpenLabSlate", null, 'l', FireOnActor: false, 5);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_OpenLabSlate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomSystem.Guard("lab slate", delegate
				{
					KingdomLab.OpenSlate(ParentObject, E.Actor);
				});
				return true;
			}
			return base.HandleEvent(E);
		}
	}
}

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the lab as a place: dressing a carcass, keeping what comes off,
	/// and the slate the founder reads at the hall.
	/// <para>
	/// <b>One screen, two levels, both <c>Popup.PickOption</c>, and no new screen class.</b> That is
	/// simultaneously the vanilla golem quest's shape, Playable Golem's shape and the precedent's
	/// control-menu shape &mdash; which is not a coincidence; it is Qud's house idiom, and this
	/// system has no reason to be the exception.
	/// </para>
	/// <para>
	/// Two things are inherited on purpose and both were expensive to learn elsewhere. Effects are
	/// shown BEFORE commitment, prefixed <c>{{rules|--}}</c>, because the one documented complaint
	/// about the vanilla picker is that players cannot tell what a choice will do; and the
	/// three-way consent prompt's third answer writes to a permanent exclusion list, because a
	/// founder who says never should be believed.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; what a body will take, where a part
	/// has to sit, what a thing costs, every sentence a refusal is told with &mdash; is delegated to
	/// the engine-free <see cref="KingdomProcedureRules"/> and <see cref="KingdomLabRules"/>.
	/// </para>
	/// </summary>
	internal static class KingdomLab
	{
		/// <summary>The property a preserved part carries to mark it as the vat-house's own work,
		/// so a founder's own jerky is never mistaken for a graftable organ (the protection law: we
		/// count only what we made and marked).</summary>
		internal const string KeptProperty = "r_TAF_LabKept";

		private const string VatPendingProperty = "r_TAF_VatPending";
		private const string VatRemainingProperty = "r_TAF_VatRemaining";
		private const string VatResultProperty = "r_TAF_VatResult";
		private const string VatYieldProperty = "r_TAF_VatYield";
		private const string VatJobProperty = "r_TAF_VatJob";
		private const string VatReadyProperty = "r_TAF_VatReady";
		private const string VatOutputJobProperty = "r_TAF_VatOutputJob";
		private const string VatBlockedProperty = "r_TAF_VatBlocked";

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
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
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
			Inventory inventory = Vat.ParentObject.RequirePart<Inventory>();
			inventory.AddObjectToInventory(part, Actor, Silent: true, NoStack: true);
			if (part.Physics == null || part.Physics.InInventory != Vat.ParentObject)
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

		internal static void Advance(r_KingdomVatHouse Vat, long TimeTick)
		{
			GameObject input = Pending(Vat);
			if (Vat == null || Vat.ParentObject == null || input == null)
			{
				return;
			}
			int staffNeeded = Vat.ParentObject.GetIntProperty(KingdomAdopt.StaffNeededProperty);
			int crew = (staffNeeded > 0) ? Vat.ParentObject.GetIntProperty("KingdomEffectiveness") : 100;
			int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Vat.ParentObject));
			KingdomVatAccrual accrual = KingdomLabRules.AccrueVat(Vat.LastWorkedTick, TimeTick,
				input.GetIntProperty(VatRemainingProperty), crew, wear, Settled: false, Cancelled: false);
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
			GameObject output = OutputFor(Vat, job);
			KingdomVatSettlement settlement = KingdomLabRules.VatSettlement(InputPresent: true,
				OutputPresent: output != null, WorkComplete: true, CancelRequested: false);
			if (settlement == KingdomVatSettlement.CreateOutput)
			{
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
			if (!input.Obliterate() || GameObject.Validate(input))
			{
				if (input.GetIntProperty(VatBlockedProperty) == 0)
				{
					input.SetIntProperty(VatBlockedProperty, 1);
					MessageQueue.AddPlayerMessage("{{r|The vats have sealed the result but cannot release the raw part. Both remain in the vat-house; collect nothing until the obstruction is cleared.}}");
				}
				return;
			}
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
			if (kept == null)
			{
				return null;
			}
			kept.Count = yield;
			kept.SetIntProperty(KeptProperty, 1);
			kept.SetIntProperty(VatReadyProperty, 1);
			kept.SetStringProperty(VatOutputJobProperty, Job);
			kept.SetStringProperty(KingdomProcedures.StampProperty,
				Input.GetStringProperty(KingdomProcedures.StampProperty));
			kept.SetStringProperty(KingdomProcedures.SourceProperty,
				Input.GetStringProperty(KingdomProcedures.SourceProperty));
			Inventory inventory = Vat.ParentObject.RequirePart<Inventory>();
			inventory.AddObject(kept, null, Silent: true, NoStack: true);
			if (kept.Physics == null || kept.Physics.InInventory != Vat.ParentObject)
			{
				kept.Obliterate();
				return null;
			}
			return kept;
		}

		private static void ManagePending(r_KingdomVatHouse Vat, GameObject Actor, GameObject Input)
		{
			int remaining = Input.GetIntProperty(VatRemainingProperty);
			GameObject output = OutputFor(Vat, Input.GetStringProperty(VatJobProperty));
			if (output != null)
			{
				Popup.Show("The keeping is finished and its sealed result is already in the vat-house, but the raw part has not released. Nothing can be collected or cancelled until the obstruction is cleared.");
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
			Actor.RequirePart<Inventory>().AddObjectToInventory(Input, Actor, Silent: true, NoStack: true);
			if (Input.Physics == null || Input.Physics.InInventory != Actor)
			{
				Vat.ParentObject.RequirePart<Inventory>().AddObjectToInventory(Input, Actor, Silent: true, NoStack: true);
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
				inventory.AddObjectToInventory(output, Actor, Silent: true, NoStack: true);
				if (output.Physics != null && output.Physics.InInventory == Actor)
				{
					output.RemoveIntProperty(VatReadyProperty);
					output.RemoveStringProperty(VatOutputJobProperty);
					taken += output.Count;
				}
				else
				{
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

		private static GameObject OutputFor(r_KingdomVatHouse Vat, string Job)
		{
			List<GameObject> ready = VatContents(Vat, VatReadyProperty);
			for (int i = 0; i < ready.Count; i++)
			{
				if (string.Equals(ready[i].GetStringProperty(VatOutputJobProperty), Job,
					StringComparison.Ordinal))
				{
					return ready[i];
				}
			}
			return null;
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
			Input.RemoveIntProperty(VatBlockedProperty);
		}

		// ==================================================================================
		// Rungs 2 and 3 — the slate
		// ==================================================================================

		/// <summary>
		/// Level one of the slate: the founder's own body, place by place, and what is on each.
		/// <para>
		/// Straight from the golem mound's own option list &mdash; the marks, the sentinel rows, the
		/// escape &mdash; because that screen is the canon for exactly this act and re-inventing it
		/// would cost the player their familiarity for nothing.
		/// </para>
		/// </summary>
		internal static void OpenSlate(GameObject Building, GameObject Actor)
		{
			if (!KingdomProcedures.Enabled)
			{
				return;
			}
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				return;
			}
			string city = system.SeatName;
			int rung = RungAt(Building);
			List<GameObject> kept = KeptParts(Actor);
			// Read once per slate rather than once per row: the roster is the same for every place
			// on the founder's body, and it is one city's rolls, not a per-procedure question.
			List<string> roster = KingdomZoning.Roster(system);
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			record.Normalize();
			List<string> names = new List<string>();
			List<LabSlot> anatomy = KingdomProcedures.Census(Actor, names);
			while (true)
			{
				List<string> options = new List<string>();
				List<int> slotIndex = new List<int>();
				for (int i = 0; i < anatomy.Count; i++)
				{
					List<LabProcedure> offers = Candidates(anatomy, i, rung, kept, record, roster);
					string grafted = record.GraftedAt(anatomy[i].Type);
					if (offers.Count == 0 && grafted == null)
					{
						// A place the hall could never do anything with is not a row. A slate that
						// listed all 157 body-part types would be a slate nobody reads.
						continue;
					}
					LabProcedure held;
					options.Add(KingdomLabRules.SlotRow(names[i],
						(grafted != null && KingdomProcedures.TryGet(grafted, out held)) ? held.Named : null,
						offers.Count > 0));
					slotIndex.Add(i);
				}
				string gap = KingdomLabRules.LadderGapLine(rung >= KingdomProcedureRules.RungSlab,
					rung >= KingdomProcedureRules.RungVat, rung >= KingdomProcedureRules.RungHall,
					rung >= KingdomProcedureRules.RungTheatre);
				if (options.Count == 0)
				{
					Popup.Show(gap ?? ("The hall has nothing it could do to you today, and it says so rather than "
						+ "showing you an empty list. Bring something to the vats, or raise the hall higher."));
					return;
				}
				int picked = Popup.PickOption(
					Title: KingdomLabRules.SlateTitle(city),
					Intro: KingdomLabRules.SlateIntro(SavantAt(system), null, TotalKept(kept)) + ((gap == null) ? "" : ("\n" + gap)),
					Options: options, AllowEscape: true, RespectOptionNewlines: true);
				if (picked < 0)
				{
					return;
				}
				int at = slotIndex[picked];
				string standing = record.GraftedAt(anatomy[at].Type);
				if (standing != null)
				{
					OfferRemoval(Actor, system, standing, city);
				}
				else
				{
					OfferProcedure(Actor, system, anatomy, at, names[at], rung, kept, record, roster, city);
				}
				// The body may have changed under us, so it is read again rather than patched.
				names = new List<string>();
				anatomy = KingdomProcedures.Census(Actor, names);
				kept = KeptParts(Actor);
			}
		}

		/// <summary>
		/// Level two: what the hall could do at one place, each with its effects and its whole price
		/// stated before anything is committed.
		/// </summary>
		private static void OfferProcedure(GameObject Actor, KingdomSystem System, List<LabSlot> Anatomy, int At,
			string SlotName, int Rung, List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster, string City)
		{
			List<LabProcedure> offers = Candidates(Anatomy, At, Rung, Kept, Record, Roster);
			if (offers.Count == 0)
			{
				Popup.ShowFail(KingdomLabRules.NothingMeetsRequirement(SlotName));
				return;
			}
			List<string> rows = new List<string>();
			for (int i = 0; i < offers.Count; i++)
			{
				rows.Add(KingdomLabRules.CandidateRow(offers[i], CountFor(Kept, offers[i])));
			}
			int picked = Popup.PickOption(Title: "Choose a procedure for " + SlotName,
				Options: rows, AllowEscape: true, RespectOptionNewlines: true);
			if (picked < 0)
			{
				return;
			}
			LabProcedure procedure = offers[picked];
			int consent = Popup.PickOption(
				Title: procedure.Named,
				Intro: KingdomLabRules.PriceLine(procedure) + "\n" + KingdomLabRules.ReversibilityLine(),
				Options: KingdomLabRules.ConsentOptions, AllowEscape: true);
			if (consent == 2)
			{
				Record.Exclude(procedure.Key);
				MessageQueue.AddPlayerMessage("{{K|The hall will not offer that again.}}");
				return;
			}
			if (consent != 0)
			{
				return;
			}
			Commission(Actor, System, procedure, Anatomy, At, Kept, City);
		}

		/// <summary>
		/// Takes the drams, spends the kept parts, pays the standing, and performs the work.
		/// <para>
		/// <b>The whole verdict is asked AGAIN here</b>, and not because the slate might be wrong:
		/// because the founder may have walked away, come back a season later, and had the answer
		/// change under them. A commit that trusts the screen that opened it is a commit that will
		/// one day take a founder's water for a thing it cannot do.
		/// </para>
		/// </summary>
		private static void Commission(GameObject Actor, KingdomSystem System, LabProcedure Procedure,
			List<LabSlot> Anatomy, int At, List<GameObject> Kept, string City)
		{
			List<int> categories = KingdomProcedures.Categories(Procedure);
			LabVerdict verdict = KingdomProcedureRules.JudgeSlot(Procedure, Anatomy[At], categories);
			if (verdict != LabVerdict.Allowed)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(verdict, Procedure));
				return;
			}
			GameObject source = FirstSourceFor(Kept, Procedure);
			if (source == null || CountFor(Kept, Procedure) < Procedure.Preserved)
			{
				Popup.Show(KingdomProcedureRules.RefusalLine(LabVerdict.RefusedUnkept, Procedure));
				return;
			}
			string stamp = source.GetStringProperty(KingdomProcedures.StampProperty);
			// Measured, never trusted: what the hall actually cost the city is the difference the
			// draw reports, not the number we asked for (STANDARDS §1). A short draw is a refusal
			// with the water already back where it was, because nothing is spent on a half-price.
			if (Procedure.Cost > 0 && KingdomGrowth.ConsumeStoredWater(Actor.CurrentZone, Procedure.Cost) < Procedure.Cost)
			{
				Popup.Show("The stores at " + KingdomLabRules.Named(City) + " cannot spare {{C|" + Procedure.Cost
					+ "}} drams. Fill them, and the hall will take the work on.");
				return;
			}
			// Spent before the graft rather than after it: a founder who is told the work happened
			// and finds their vats untouched has been lied to about what a thing cost.
			SpendKept(Kept, Procedure);
			string failure;
			if (!KingdomProcedures.Grant(Actor, Procedure, At, stamp, out failure))
			{
				Popup.Show(failure);
				return;
			}
			List<KeyValuePair<string, int>> standing = KingdomLabRules.StandingCost(Procedure.Creeds, KingdomLabRules.StandingPerCreed);
			for (int i = 0; i < standing.Count; i++)
			{
				System.AdjustStanding(standing[i].Key, standing[i].Value);
			}
			MessageQueue.AddPlayerMessage(KingdomLabRules.DoneLine(Procedure.Named, City));
			KingdomChronicle.Record(System, KingdomLabRules.DoneTelling(Procedure.Named, City));
			Speak(System, Actor, Procedure);
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
		private static void Speak(KingdomSystem System, GameObject Actor, LabProcedure Procedure)
		{
			r_KingdomLabRecord record = KingdomProcedures.Record(Actor);
			if (record.SpokenAgainst || Procedure.Class == LabClass.Rider)
			{
				return;
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
				return;
			}
			// Through the shipped petitions machinery, which builds nothing parallel: a named person,
			// waiting at the Charter, about a thing they actually mind (DIVERSITY §3.6's mesh
			// condition). The latch is set only when a petition was really raised, so a founder who
			// happened to be carrying another petition still gets this one the next time.
			if (KingdomPetitions.Raise(System, KingdomRules.PetitionKind.Flesh, creed))
			{
				record.SpokenAgainst = true;
				KingdomLog.Log("lab: hall spoken against (" + creed + " x" + holding + ")");
			}
		}

		private static int CreedCount(KingdomSystem System, string Creed)
		{
			int count;
			return (System.CreedCounts != null && Creed != null && System.CreedCounts.TryGetValue(Creed, out count)) ? count : 0;
		}

		/// <summary>Offers to take a graft off. Costs less than the graft, returns nothing, and says
		/// so before the founder agrees.</summary>
		private static void OfferRemoval(GameObject Actor, KingdomSystem System, string Key, string City)
		{
			LabProcedure procedure;
			if (!KingdomProcedures.TryGet(Key, out procedure))
			{
				return;
			}
			int price = procedure.Cost / 4;
			if (Popup.ShowYesNoCancel("Have " + procedure.Named + " taken off?\n\n{{rules|--}} It costs {{C|"
				+ price + "}} drams and returns nothing. What was kept for it is spent and stays spent."
				+ (procedure.IsNamed ? "\n{{r|--}} It was a once-ever procedure. Taking it off does not give you the once back."
					: "")) != DialogResult.Yes)
			{
				return;
			}
			if (price > 0 && KingdomGrowth.ConsumeStoredWater(Actor.CurrentZone, price) < price)
			{
				Popup.Show("The stores cannot spare even that.");
				return;
			}
			if (KingdomProcedures.Remove(Actor, procedure.Key))
			{
				MessageQueue.AddPlayerMessage("{{K|It is off. Nothing was given back for it.}}");
				KingdomChronicle.Record(System, KingdomLabRules.RemovedTelling(procedure.Named, City));
			}
		}

		// ==================================================================================
		// Reading the world
		// ==================================================================================

		/// <summary>
		/// Which records this hall could perform at this place, for this founder, today.
		/// <para>
		/// The visibility law is enforced HERE and by the accessor rather than by discipline: a
		/// named procedure the founder has not found is dropped before any row, count or refusal is
		/// computed, so "cannot have it" and "have never heard of it" are the same absence of a row.
		/// </para>
		/// </summary>
		private static List<LabProcedure> Candidates(List<LabSlot> Anatomy, int At, int Rung,
			List<GameObject> Kept, r_KingdomLabRecord Record, List<string> Roster)
		{
			List<LabProcedure> offers = new List<LabProcedure>();
			List<LabProcedure> all = KingdomProcedures.All;
			for (int i = 0; i < all.Count; i++)
			{
				LabProcedure procedure = all[i];
				if (!KingdomProcedures.Discovered(procedure) || Record.Refuses(procedure.Key))
				{
					continue;
				}
				if (procedure.IsNamed && Record.AlreadyHad(procedure.Key))
				{
					continue;
				}
				// The record's own Knowledge gate, read through the SHIPPED roster grammar and
				// nothing of ours: a procedure gates on a research node, a rite, a taught disk or a
				// certified machine with one attribute, exactly as a building does, and a third
				// party's procedure gates on a third party's research with no code at all.
				if (!KingdomProcedureRules.KnowledgeMet(Roster, procedure.Knowledge))
				{
					continue;
				}
				if (Rung < procedure.MinRung || CountFor(Kept, procedure) < procedure.Preserved)
				{
					continue;
				}
				if (KingdomProcedureRules.JudgeSlot(procedure, Anatomy[At], KingdomProcedures.Categories(procedure)) == LabVerdict.Allowed)
				{
					offers.Add(procedure);
				}
			}
			return offers;
		}

		private static List<GameObject> KeptParts(GameObject Actor)
		{
			List<GameObject> kept = new List<GameObject>();
			foreach (GameObject item in Actor.GetInventoryAndEquipment())
			{
				if (item != null && item.GetIntProperty(KeptProperty) == 1)
				{
					kept.Add(item);
				}
			}
			return kept;
		}

		private static int TotalKept(List<GameObject> Kept)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				total += Kept[i].Count;
			}
			return total;
		}

		/// <summary>How many kept parts would answer this record: stamped with the class it grants,
		/// and inside its band if it names one.</summary>
		private static int CountFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			int total = 0;
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					total += Kept[i].Count;
				}
			}
			return total;
		}

		private static GameObject FirstSourceFor(List<GameObject> Kept, LabProcedure Procedure)
		{
			for (int i = 0; i < Kept.Count; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					&& KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					return Kept[i];
				}
			}
			return null;
		}

		private static void SpendKept(List<GameObject> Kept, LabProcedure Procedure)
		{
			int owed = Procedure.Preserved;
			for (int i = 0; i < Kept.Count && owed > 0; i++)
			{
				string stamp = Kept[i].GetStringProperty(KingdomProcedures.StampProperty);
				if (!KingdomProcedureRules.StampCarries(stamp, Procedure.Grants)
					|| !KingdomProcedureRules.MagnitudeAdmits(Procedure, stamp))
				{
					continue;
				}
				int take = (Kept[i].Count < owed) ? Kept[i].Count : owed;
				Kept[i].SplitStack(take)?.Obliterate();
				owed -= take;
			}
		}

		/// <summary>
		/// The rung this building performs at. Read off the building's own part rather than off a
		/// survey, because the founder is standing in front of it and the thing they are standing in
		/// front of is the authority on what it can do.
		/// </summary>
		private static int RungAt(GameObject Building)
		{
			if (Building == null)
			{
				return -1;
			}
			if (Building.HasPart("r_KingdomChimericTheatre"))
			{
				return KingdomProcedureRules.RungTheatre;
			}
			if (Building.HasPart("r_KingdomGraftingHall"))
			{
				return KingdomProcedureRules.RungHall;
			}
			return Building.HasPart("r_KingdomVatHouse") ? KingdomProcedureRules.RungVat : KingdomProcedureRules.RungSlab;
		}

		/// <summary>The lodged savant's name, or null when the hall has nobody who knows the work.
		/// Derived from the crew the lodging machinery already placed &mdash; the hall assigns
		/// nobody, exactly as Addendum 6 says a great work never does.</summary>
		private static string SavantAt(KingdomSystem System)
		{
			return (System.RosterNames != null && System.RosterNames.Count > 0) ? System.RosterNames[0] : null;
		}
	}
}
