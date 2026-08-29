using System;
using System.Collections.Generic;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// XRL.World.Parts, for the reason r_KingdomPlot and r_KingdomLiquidConduit both state:
// GamePartBlueprint resolves a part named in XML as exactly "XRL.World.Parts.<Name>" and tries no
// other name (GamePartBlueprint.cs:178, :240). Only the part moves; the settlement-side resolver
// below stays where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// An arch keyed onto another of the founder's cities: step into it there, step out of it here.
	/// <para>
	/// <b>Inherit and extend, per Addendum 11(c)'s preference order.</b> The crossing itself is
	/// vanilla's and is not reimplemented: <c>TeleporterPair</c> already carries
	/// <c>LocationKey</c>/<c>DestinationKey</c> as game-state keys, publishes a cell address under
	/// the first and reads a destination out of the second
	/// (<c>D/XRL/World/Parts/TeleporterPair.cs:213-222</c>, <c>:166-175</c>), refuses to move
	/// anyone with hostiles nearby (<c>:177-184</c>), and ends in <c>GameObject.ZoneTeleport</c>
	/// (<c>:205</c>). All of that is inherited whole. What is added here is the part vanilla has no
	/// opinion about, because vanilla's pair is a trinket in a satchel and this is a building on a
	/// plot: an anchor that does not wait for the object to be picked up, a dedication rite, a
	/// standing draw on the settlement's 12(g) power lane, and a brownout that closes it.
	/// </para>
	/// <para>
	/// <b>Two knobs are turned off in the constructor and both are rulings rather than tuning.</b>
	/// <c>ChargeUse</c> is zero because Addendum 22 A2 rules the standing draw the whole price of
	/// the crossing &mdash; a per-step charge would be the second toll that addendum forbids.
	/// <c>Cooldown</c> is zero because Addendum 8 forbids a timer of our own; what rations the
	/// crossing is what the works can pay, which is a thing in the world.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomMirrorGate : TeleporterPair
	{
		/// <summary>
		/// Set while the works could not pay the standing draw. The arch is inert and says so
		/// &mdash; STANDARDS 7b's applicable-but-blocked case, and the one stall this building can
		/// have.
		/// <para>
		/// This is the state AND the once-flag, exactly as <c>KingdomSettlement.SubsidenceAnnounced</c>
		/// is both: the founder is told on the crossing from open to dark, and recovery is unsaid in
		/// silence, which is the discipline <c>KingdomPower.Brownouts</c> already keeps one lane
		/// over.
		/// </para>
		/// </summary>
		public bool Dark;

		/// <summary>
		/// Tick this arch last settled its draw to. Zero until the first day boundary plants it,
		/// which is why an arch never pays for the day it was raised &mdash; the same discipline
		/// <c>r_KingdomPowerWork.LastResolvedTick</c> keeps, and for the same reason.
		/// </summary>
		public long LastDrawTick;

		public r_KingdomMirrorGate()
		{
			ChargeUse = 0;
			Cooldown = 0;
			// Vanilla's default key is shared by every teleporter orb in the game. Ours is its own
			// even though nothing reads it at a zero cooldown, because a knob left pointing at
			// somebody else's state is a bug waiting for the day the cooldown is not zero.
			CooldownKey = "r_TAF_MirrorGateLastUsed";
			WorksOnCellContents = true;
			WorksOnAdjacentCellContents = true;
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		/// <summary>
		/// A dark arch is a part with its own reason for not working, and vanilla has a word for
		/// that (<c>D/XRL/World/Parts/IActivePart.cs:1941</c>, <c>:2010-2012</c>). Declaring it here
		/// rather than only in our own refusal is what makes every other reader honest at once: the
		/// tech scan says why, an <c>AnimatedMaterialGeneric</c> keyed to this part stops running
		/// the lit frames, and vanilla's own <c>AttemptTeleport</c> refuses a crossing the works did
		/// not pay for even if some future caller reaches it without going through
		/// <c>KingdomMirrorGate.Cross</c>.
		/// </summary>
		public override bool GetActivePartLocallyDefinedFailure()
		{
			return Dark;
		}

		/// <summary>Vanilla's own vocabulary for these &mdash; <c>WindTurbine</c> answers
		/// <c>WindSpeedInsufficient</c> and <c>HydroTurbine</c> <c>HydrodynamicForceInsufficient</c>
		/// &mdash; so a founder reading a status list sees one kind of word, not two.</summary>
		public override string GetActivePartLocallyDefinedFailureDescription()
		{
			return "ChargeSupplyInsufficient";
		}

		/// <summary>
		/// A day also turns over while the founder is standing there watching it, so the arch keeps
		/// its own absolute stamp rather than a countdown that must be delivered every turn to stay
		/// correct. <c>r_KingdomPowerWork</c>, <c>r_KingdomPlot</c> and <c>r_KingdomScaffold</c> all
		/// keep time this way; missing ticks costs nothing.
		/// </summary>
		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			KingdomSystem master = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMaster.AutomaticWorkAllowed(master)) return;
			if (LastDrawTick <= master.MasterOptionTick)
			{
				LastDrawTick = TimeTick;
				return;
			}
			if (LastDrawTick <= 0L)
			{
				// Planted at now rather than at tick zero: an arch raised this afternoon is not
				// billed for every day since the world began.
				LastDrawTick = TimeTick;
				return;
			}
			if (TimeTick < LastDrawTick + KingdomRules.TicksPerDay)
			{
				return;
			}
			Zone zone = ParentObject?.CurrentZone;
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (zone == null || system == null || !system.Founded || system.ClaimedZones == null || !system.ClaimedZones.Contains(zone.ZoneID))
			{
				return;
			}
			KingdomSystem.Guard("mirror-gate day", delegate
			{
				KingdomMirrorGate.Settle(this, system, zone, TimeTick);
			});
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append(KingdomMirrorGate.DescriptionLine(this));
			return base.HandleEvent(E);
		}

		/// <summary>
		/// Deliberately not <c>base.HandleEvent</c>: the base offers "Activate" and writes its own
		/// cooldown into the label, and neither sentence can describe this thing. An arch that is
		/// dark is not an arch on cooldown, and an arch that has never been keyed is not an arch at
		/// all yet.
		/// </summary>
		public override bool HandleEvent(GetInventoryActionsEvent E)
		{
			E.AddAction("Dedicate", KingdomMirrorGate.DedicateLabel(this), "r_DedicateMirrorGate", null, 'd', FireOnActor: false, 5);
			E.AddAction("Cross", KingdomMirrorGate.CrossLabel(this), "r_CrossMirrorGate", null, 'c', FireOnActor: false, 100);
			if (KingdomMirrorGate.CanChooseDestination(this))
				E.AddAction("Re-key", "choose this capital arch's destination",
					"r_RekeyMirrorGate", null, 'k', FireOnActor: false, 90);
			E.AddAction("Dispatch", "dispatch a purpose consignment", "r_DispatchPurposeCargo",
				null, 'p', FireOnActor: false, 80);
			return true;
		}

		public override bool HandleEvent(InventoryActionEvent E)
		{
			if (E.Command == "r_DedicateMirrorGate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomMirrorGate.Dedicate(this, E.Actor);
				return true;
			}
			if (E.Command == "r_CrossMirrorGate" && E.Actor != null && E.Actor.IsPlayer())
			{
				if (KingdomMirrorGate.Cross(this, E.Actor, E))
				{
					E.Actor.UseEnergy(KingdomGovernanceRules.NominalEnergyCost,
						KingdomGovernanceRules.EnergyReason("cross mirror gate"));
					E.RequestInterfaceExit();
				}
				return true;
			}
			if (E.Command == "r_RekeyMirrorGate" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomMirrorGate.ChooseDestination(this, E.Actor);
				return true;
			}
			if (E.Command == "r_DispatchPurposeCargo" && E.Actor != null && E.Actor.IsPlayer())
			{
				KingdomPurpose.Dispatch(this, E.Actor);
				return true;
			}
			return base.HandleEvent(E);
		}

		/// <summary>
		/// The base's own location sync is written for a trinket that moves &mdash; it fires on
		/// Equipped, EnteredCell and AddedToInventory, and writes under whatever
		/// <c>LocationKey</c> happens to hold. A building fires exactly one of those, once, at
		/// placement, when the key is still empty; so the key is composed from the ground first and
		/// vanilla's sync then does what it was always going to do.
		/// </summary>
		public override bool FireEvent(Event E)
		{
			if (E.ID == "EnteredCell")
			{
				KingdomSystem.Guard("mirror-gate anchor", delegate
				{
					KingdomMirrorGate.Anchor(this);
				});
			}
			return base.FireEvent(E);
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

	/// <summary>Two physically loaded, reciprocal, powered arches proved usable at one instant.</summary>
	internal sealed class KingdomPurposeConnection
	{
		internal r_KingdomMirrorGate SourceGate;
		internal r_KingdomMirrorGate DestinationGate;
		internal Zone SourceZone;
		internal Zone DestinationZone;
		internal string SourceKey;
		internal string DestinationKey;
		internal string SourceCity;
		internal string DestinationCity;
	}

}
