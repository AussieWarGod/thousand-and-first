using System;

using ThousandAndFirst;

namespace XRL.World.Parts
{
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
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master))
			{
				base.TurnTick(TimeTick, Amount);
				return;
			}
			if (LastWorkedTick <= master.MasterOptionTick)
			{
				LastWorkedTick = TimeTick;
				base.TurnTick(TimeTick, Amount);
				return;
			}
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
}
