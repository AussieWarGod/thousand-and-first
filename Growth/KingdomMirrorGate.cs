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
					E.Actor.UseEnergy(1000, "Mirror Gate");
					E.RequestInterfaceExit();
				}
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

	/// <summary>
	/// The engine-coupled half of the mirror-gate: anchoring an arch on real ground, the dedication
	/// rite, the standing draw settled against real charge, and the crossing itself.
	/// <para>
	/// <b>The register is the pairing.</b> An arch never writes on its twin, because its twin is
	/// almost always standing in a zone nobody has loaded. Both ends write only their own key; one
	/// string in the game's own state says who answers whom, exactly as one string carries the
	/// keepers' knowledge roster (<c>KingdomZoning.Roster</c>). That is what makes the capital-hub
	/// re-keying QUESTION-BACKLOG QB-1 defers a rewrite of one column rather than a walk over
	/// dormant cities, and it is why <c>KingdomMirrorGateRules.TryPair</c> exists as its own rule.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; who may answer whom, what a day
	/// costs, whether the works paid it, every refusal's wording &mdash; is delegated to the
	/// engine-free <see cref="KingdomMirrorGateRules"/>.
	/// </para>
	/// </summary>
	internal static class KingdomMirrorGate
	{
		/// <summary>The standing draw an arch declares to the 12(g) lane, where the lane can read it
		/// off the object without knowing what a mirror-gate is.</summary>
		internal const string DailyDrawProperty = "KingdomDailyDraw";

		/// <summary>
		/// Writes this arch's own cell address under its own key, and reads back out of the register
		/// which arch it answers.
		/// <para>
		/// Cheap and idempotent, so it runs before every act rather than being scheduled: two
		/// dictionary writes and a string split. Nothing here loads a zone.
		/// </para>
		/// </summary>
		internal static void Anchor(r_KingdomMirrorGate Gate)
		{
			if (Gate == null || The.Game == null)
			{
				return;
			}
			Cell cell = Gate.ParentObject?.CurrentCell;
			Zone zone = cell?.ParentZone;
			if (zone == null)
			{
				return;
			}
			string key = KingdomMirrorGateRules.ComposeLocationKey(zone.ZoneID, cell.X, cell.Y);
			if (string.IsNullOrEmpty(key))
			{
				return;
			}
			Gate.LocationKey = key;
			The.Game.SetStringGameState(key, cell.GetAddress());
			string partner = KingdomMirrorGateRules.PartnerOf(Register(null), key);
			Gate.DestinationKey = partner;
			// Declared where the power lane can read it, and only while the arch actually answers
			// something: an unkeyed arch draws nothing, because idleness costs nothing (Addendum 8).
			Gate.ParentObject.SetIntProperty(DailyDrawProperty, (partner.Length == 0) ? 0 : KingdomMirrorGateRules.OpenChargePerDay);
		}

		/// <summary>
		/// One day boundary's worth of the standing draw, settled against the city's real charge.
		/// <para>
		/// <b>The works run first.</b> This calls the ordinary settlement power pass before drawing,
		/// so the day's charge is in the salt before the arch reaches for it; without that, whether
		/// a crossing survived a week away would depend on which part happened to tick first.
		/// The pass stamps its own works to now, so calling it here costs a second visit nothing.
		/// </para>
		/// </summary>
		internal static void Settle(r_KingdomMirrorGate Gate, KingdomSystem System, Zone Z, long TimeTick)
		{
			Anchor(Gate);
			if (string.IsNullOrEmpty(Gate.DestinationKey))
			{
				Gate.LastDrawTick = TimeTick;
				Gate.Dark = false;
				return;
			}
			long days;
			KingdomCityFault fault;
			if (!KingdomProductionRules.TryDaysBetween(Gate.LastDrawTick, TimeTick, KingdomRules.TicksPerDay, out days, out fault) || days <= 0L)
			{
				return;
			}
			string city = CityOf(System, Z.ZoneID);
			if (!KingdomPower.Enabled)
			{
				// The lane the arch draws on has been switched off entirely. It cannot be paid, and
				// a founder who turned power off is owed the sentence rather than a dead arch.
				Gate.LastDrawTick = TimeTick;
				GoDark(Gate, System, city);
				return;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomGrowth.AssignWork(System, survey);
			KingdomPower.OnSettlementPass(System, Z, survey);
			int owed = KingdomMirrorGateRules.DrawForDays(days);
			int before = Gate.ParentObject.QueryCharge();
			KingdomGateHold hold = KingdomMirrorGateRules.JudgeHold(owed, before);
			Gate.LastDrawTick = TimeTick;
			if (hold == KingdomGateHold.Held)
			{
				Gate.ParentObject.UseCharge(owed);
				// Measured, never trusted: UseCharge reports what it found convenient, and what the
				// arch actually cost the city is the difference between two readings (STANDARDS §1).
				int spent = before - Gate.ParentObject.QueryCharge();
				if (spent < owed)
				{
					hold = KingdomGateHold.Lost;
				}
				if (KingdomLog.Enabled) KingdomLog.Log("mirror-gate " + Gate.LocationKey + " days=" + days + " owed=" + owed + " spent=" + spent + " held=" + (hold == KingdomGateHold.Held));
			}
			if (hold == KingdomGateHold.Lost)
			{
				GoDark(Gate, System, city);
				return;
			}
			if (hold == KingdomGateHold.Held)
			{
				// Recovery is unsaid, and unsaid in silence: a settlement that announced every
				// return to normal would be a settlement that talks about itself constantly, which
				// is the thing 7b's own complaint is actually about.
				Gate.Dark = false;
			}
		}

		/// <summary>
		/// The dedication rite, on the arch itself rather than in the Charter: the Charter's letters
		/// are full at thirty-six, and a new entry there would be a chapter rather than a line. The
		/// verb idiom is the Charter's own &mdash; disclose the whole cost, ask, then act, and
		/// surface only a decline (<c>KingdomCharterPart.CertifyMachine</c>).
		/// </summary>
		internal static void Dedicate(r_KingdomMirrorGate Gate, GameObject Actor)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				Popup.Show("You rule nothing yet, and an arch keyed to nowhere is a wall with a hole in it.");
				return;
			}
			Cell cell = Gate.ParentObject?.CurrentCell;
			Zone zone = cell?.ParentZone;
			if (zone == null)
			{
				return;
			}
			string city = CityOf(system, zone.ZoneID);
			if (city == null)
			{
				Popup.Show(KingdomMirrorGateRules.NotOurGroundLine);
				return;
			}
			if (Gate.ParentObject.GetIntProperty("KingdomBuilt") != 1 && Gate.ParentObject.GetIntProperty("KingdomGrid") != 1)
			{
				Popup.Show(KingdomMirrorGateRules.NotOurWorkLine);
				return;
			}
			Anchor(Gate);
			if (string.IsNullOrEmpty(Gate.LocationKey))
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.RefusedNamed, city));
				return;
			}
			KingdomGateRow[] rows = Register(system);
			if (KingdomMirrorGateRules.IndexOfKey(rows, Gate.LocationKey) >= 0)
			{
				Release(Gate, system, rows, city);
				return;
			}
			int held = KingdomMirrorGateRules.IndexOfCity(rows, city);
			if (held >= 0)
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(KingdomGateVerdict.RefusedCityKeyed, rows[held].City));
				return;
			}
			if (Popup.ShowYesNo(KingdomMirrorGateRules.DedicationPrompt(city)) != DialogResult.Yes)
			{
				return;
			}
			KingdomGateRow[] next;
			string partner;
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TryDedicate(rows, Gate.LocationKey, city, out next, out partner);
			if (verdict != KingdomGateVerdict.Offered && verdict != KingdomGateVerdict.Joined)
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(verdict, city));
				return;
			}
			Write(next);
			Anchor(Gate);
			if (verdict == KingdomGateVerdict.Offered)
			{
				system.Ledger.Note("{{C|" + KingdomMirrorGateRules.OfferedLine(city) + "}}");
				MessageQueue.AddPlayerMessage("{{C|" + KingdomMirrorGateRules.OfferedLine(city) + "}}");
				return;
			}
			string there = CityNamed(next, partner);
			system.Ledger.Note("{{G|" + KingdomMirrorGateRules.JoinedLine(city, there) + "}}");
			MessageQueue.AddPlayerMessage("{{G|" + KingdomMirrorGateRules.JoinedLine(city, there) + "}}");
			system.RecordDeed("the arch of " + city + " opened onto " + there);
			KingdomChronicle.Record(system, KingdomMirrorGateRules.JoinedTelling(city, there), Accomplishment: true);
		}

		/// <summary>
		/// One crossing. Every refusal this can give is said out loud, because a founder standing in
		/// front of an arch that does nothing has asked a question and is owed an answer.
		/// </summary>
		/// <returns>True once somebody has actually been moved, so the caller spends the turn.</returns>
		internal static bool Cross(r_KingdomMirrorGate Gate, GameObject Actor, IEvent FromEvent)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system == null || !system.Founded)
			{
				Popup.ShowFail(KingdomMirrorGateRules.UnkeyedLine);
				return false;
			}
			Anchor(Gate);
			if (string.IsNullOrEmpty(Gate.DestinationKey))
			{
				Popup.ShowFail(KingdomMirrorGateRules.UnkeyedLine);
				return false;
			}
			if (!KingdomPower.Enabled)
			{
				Popup.ShowFail(KingdomMirrorGateRules.NoPowerLine);
				return false;
			}
			if (Gate.Dark)
			{
				Popup.ShowFail(KingdomMirrorGateRules.DarkLine);
				return false;
			}
			// Vanilla's own from here down, and deliberately: the hostiles refusal, the destination
			// address, the cooldown check that costs nothing at a zero cooldown, and the zone
			// teleport are all TeleporterPair's and all inherited whole.
			return Gate.AttemptTeleport(Actor, FromEvent);
		}

		/// <summary>The line the arch carries in its own description.</summary>
		internal static string DescriptionLine(r_KingdomMirrorGate Gate)
		{
			KingdomGateRow[] rows = Register(null);
			int at = KingdomMirrorGateRules.IndexOfKey(rows, Gate.LocationKey);
			if (at < 0)
			{
				return KingdomMirrorGateRules.DescriptionLine(false, null, false);
			}
			return KingdomMirrorGateRules.DescriptionLine(true, CityNamed(rows, rows[at].Partner), Gate.Dark);
		}

		/// <summary>What the dedication action reads as in the list.</summary>
		internal static string DedicateLabel(r_KingdomMirrorGate Gate)
		{
			return (KingdomMirrorGateRules.IndexOfKey(Register(null), Gate.LocationKey) >= 0)
				? "unkey this arch"
				: "key this arch to another of your cities";
		}

		/// <summary>What the crossing action reads as in the list. The state is in the label, so a
		/// founder never presses a thing that cannot work and then reads why.</summary>
		internal static string CrossLabel(r_KingdomMirrorGate Gate)
		{
			if (string.IsNullOrEmpty(Gate.DestinationKey))
			{
				return "{{K|cross}} [unkeyed]";
			}
			return Gate.Dark ? "{{K|cross}} [{{r|dark}}]" : "cross";
		}

		/// <summary>
		/// Proves the hard purpose prerequisite against both real arches. This is mutation-free:
		/// stale draw state refuses and names the visit that will settle it rather than charging a
		/// dormant city during a preview.
		/// </summary>
		internal static bool TryPurposeConnection(r_KingdomMirrorGate Gate,
			KingdomSystem System, out KingdomPurposeConnection Connection, out string Failure)
		{
			return TryPurposeConnection(Gate, System, out Connection, out _, out Failure);
		}

		/// <summary>The same live proof, with an explicit signal for duplicate physical endpoints.
		/// A preview can simply refuse either condition; a published consignment must quarantine an
		/// ambiguity because retrying cannot choose which arch the frozen route meant.</summary>
		internal static bool TryPurposeConnection(r_KingdomMirrorGate Gate,
			KingdomSystem System, out KingdomPurposeConnection Connection,
			out bool RequiresInspection, out string Failure)
		{
			Connection = null;
			RequiresInspection = false;
			Failure = null;
			if (Gate == null || System == null || !System.Founded || The.Game == null
				|| The.ZoneManager == null || !KingdomPower.Enabled)
				return PurposeConnectionFailure("Both cities must keep power enabled before a purpose consignment can move.", out Failure);
			GameObject sourceObject = Gate.ParentObject;
			Zone sourceZone = sourceObject?.CurrentZone;
			Cell sourceCell = sourceObject?.CurrentCell;
			string sourceCity = CityOf(System, sourceZone?.ZoneID);
			if (sourceZone == null || sourceCell == null || sourceCity == null
				|| (sourceObject.GetIntProperty("KingdomBuilt") != 1
					&& sourceObject.GetIntProperty("KingdomGrid") != 1))
				return PurposeConnectionFailure("Stand at a finished mirror-gate on this city's own ground.", out Failure);
			Anchor(Gate);
			KingdomGateRow[] rows = Register(null);
			int sourceAt = KingdomMirrorGateRules.IndexOfKey(rows, Gate.LocationKey);
			if (sourceAt < 0 || string.IsNullOrEmpty(rows[sourceAt].Partner))
				return PurposeConnectionFailure("Key this mirror-gate and its twin in another city first.", out Failure);
			string destinationKey = rows[sourceAt].Partner;
			int destinationAt = KingdomMirrorGateRules.IndexOfKey(rows, destinationKey);
			if (destinationAt < 0 || rows[destinationAt].Partner != Gate.LocationKey)
				return PurposeConnectionFailure("The gate register is not reciprocal; release and re-key the two arches.", out Failure);
			if (!KingdomMirrorGateRules.TryParseLocationKey(Gate.LocationKey,
				out string sourceZoneId, out int sourceX, out int sourceY)
				|| sourceZoneId != sourceZone.ZoneID || sourceX != sourceCell.X || sourceY != sourceCell.Y
				|| !KingdomMirrorGateRules.TryParseLocationKey(destinationKey,
					out string destinationZoneId, out int destinationX, out int destinationY))
				return PurposeConnectionFailure("The exact gate addresses are malformed; re-key the arches on their standing cells.", out Failure);
			if (!The.ZoneManager.IsZoneBuilt(destinationZoneId))
				return PurposeConnectionFailure("Visit the other gate's ground once so its real arch and power state can be proved.", out Failure);
			Zone destinationZone;
			try { destinationZone = The.ZoneManager.GetZone(destinationZoneId); }
			catch (Exception ex)
			{
				return PurposeConnectionFailure("The other gate's visited ground could not be loaded: "
					+ ex.Message, out Failure);
			}
			Cell destinationCell = destinationZone?.GetCell(destinationX, destinationY);
			r_KingdomMirrorGate destinationGate = ExactGateAt(destinationCell, destinationKey,
				out bool destinationAmbiguous);
			if (destinationAmbiguous)
			{
				RequiresInspection = true;
				return PurposeConnectionFailure("More than one physical mirror-gate answers the frozen destination address; inspect the route rather than choosing an arch.", out Failure);
			}
			GameObject destinationObject = destinationGate?.ParentObject;
			string destinationCity = CityOf(System, destinationZoneId);
			if (destinationGate == null || destinationObject == null || destinationCity == null
				|| (destinationObject.GetIntProperty("KingdomBuilt") != 1
					&& destinationObject.GetIntProperty("KingdomGrid") != 1))
				return PurposeConnectionFailure("Visit the other city and repair or re-key the exact mirror-gate standing there.", out Failure);
			Anchor(destinationGate);
			if (Gate.DestinationKey != destinationKey
				|| destinationGate.LocationKey != destinationKey
				|| destinationGate.DestinationKey != Gate.LocationKey)
				return PurposeConnectionFailure("The two physical arches no longer answer their frozen register; re-key them.", out Failure);
			if (Gate.Dark || destinationGate.Dark)
				return PurposeConnectionFailure("One of the two arches is dark. Visit that city and restore enough charge for its daily draw.", out Failure);
			long now = The.Game.TimeTicks;
			if (Gate.LastDrawTick <= 0L || destinationGate.LastDrawTick <= 0L
				|| now < Gate.LastDrawTick || now < destinationGate.LastDrawTick
				|| now >= Gate.LastDrawTick + KingdomRules.TicksPerDay
				|| now >= destinationGate.LastDrawTick + KingdomRules.TicksPerDay)
				return PurposeConnectionFailure("A gate's power reading is stale. Visit each arch so its daily draw settles, then dispatch before the next day turns.", out Failure);
			string sourceAddress = The.Game.GetStringGameState(Gate.LocationKey, "");
			string destinationAddress = The.Game.GetStringGameState(destinationKey, "");
			if (sourceAddress != sourceCell.GetAddress()
				|| destinationAddress != destinationCell.GetAddress())
				return PurposeConnectionFailure("A physical gate address is stale; visit and re-key that arch on its standing cell.", out Failure);
			Connection = new KingdomPurposeConnection
			{
				SourceGate = Gate, DestinationGate = destinationGate,
				SourceZone = sourceZone, DestinationZone = destinationZone,
				SourceKey = Gate.LocationKey, DestinationKey = destinationKey,
				SourceCity = sourceCity, DestinationCity = destinationCity
			};
			return true;
		}

		private static r_KingdomMirrorGate ExactGateAt(Cell Cell, string Key,
			out bool Ambiguous)
		{
			Ambiguous = false;
			r_KingdomMirrorGate exact = null;
			int count = 0;
			List<GameObject> objects = Cell?.GetObjects();
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				r_KingdomMirrorGate gate = objects[i]?.GetPart<r_KingdomMirrorGate>();
				if (gate == null) continue;
				Anchor(gate);
				if (gate.LocationKey != Key) continue;
				count++;
				if (count == 1) exact = gate;
			}
			Ambiguous = count > 1;
			return count == 1 ? exact : null;
		}

		private static bool PurposeConnectionFailure(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

		// ==================================================================================
		// The register
		// ==================================================================================

		/// <summary>
		/// The realm's arches, read out of game state and repaired if it needs it.
		/// <para>
		/// A row that cannot be read is dropped, the repaired register is written back, and the
		/// founder is told once &mdash; which is once and only once because the repair makes the
		/// condition non-recurring, so no latch is needed anywhere to hold it to that.
		/// </para>
		/// </summary>
		/// <param name="System">Told when a row had to be dropped. Null asks nothing and says
		/// nothing, which is what the read-only callers want.</param>
		private static KingdomGateRow[] Register(KingdomSystem System)
		{
			if (The.Game == null)
			{
				return new KingdomGateRow[0];
			}
			KingdomGateRow[] rows;
			int dropped;
			KingdomMirrorGateRules.TryParseRegister(The.Game.GetStringGameState(KingdomMirrorGateRules.RegisterStateKey, ""), out rows, out dropped);
			if (dropped <= 0)
			{
				return rows;
			}
			Write(rows);
			KingdomLog.Log("mirror-gate: dropped " + dropped + " unreadable register row(s)");
			if (System != null && System.Founded)
			{
				System.Ledger.Note("{{r|The realm's record of its arches was damaged, and " + dropped + " of them could not be read back. Those arches are standing but unkeyed; key them again.}}");
			}
			return rows;
		}

		private static void Write(KingdomGateRow[] rows)
		{
			The.Game?.SetStringGameState(KingdomMirrorGateRules.RegisterStateKey, KingdomMirrorGateRules.FormatRegister(rows));
		}

		/// <summary>
		/// Points every arch in the realm at the capital's, and says what changed.
		/// <para>
		/// Called by the crown the moment a capital is made or moved (Addendum 22 A2: the network
		/// is hubbed at the capital), and this is the whole of the retrofit QB-1 deferred. Nothing
		/// here loads a zone, visits an arch, or rebuilds anything: the register carries the
		/// pairing, so re-keying the realm is a rewrite of one column and the arches find out the
		/// next time each is anchored &mdash; which is before every crossing, every dedication and
		/// every day's draw, so no arch can act on a stale partner.
		/// </para>
		/// <para>
		/// One live object is the exception worth taking: an arch standing in the zone the founder
		/// is in has a <c>DestinationKey</c> in memory right now and a description they may be
		/// reading, so any loaded arch is re-anchored here rather than at some later event.
		/// </para>
		/// </summary>
		/// <param name="System">The realm, for the telling. Never null in practice.</param>
		/// <param name="Capital">The city keeping the crown.</param>
		internal static void Hub(KingdomSystem System, string Capital)
		{
			if (System == null || !System.Founded || string.IsNullOrEmpty(Capital) || The.Game == null)
			{
				return;
			}
			KingdomGateRow[] rows = Register(System);
			if (rows.Length == 0)
			{
				// Not applicable rather than blocked: a realm that has never keyed an arch is not
				// being stopped from anything, and 7b's first kind says nothing, correctly.
				return;
			}
			KingdomGateRow[] next;
			int rekeyed;
			string hubKey;
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TryHub(rows, Capital, out next, out rekeyed, out hubKey);
			if (verdict == KingdomGateVerdict.RefusedUnkeyed)
			{
				System.Ledger.Note(KingdomMirrorGateRules.NoArchAtCapitalLine(Capital));
				MessageQueue.AddPlayerMessage(KingdomMirrorGateRules.NoArchAtCapitalLine(Capital));
				return;
			}
			if (verdict != KingdomGateVerdict.Joined && verdict != KingdomGateVerdict.Offered)
			{
				KingdomLog.Log("mirror-gate hub refused for " + Capital + ": " + verdict);
				return;
			}
			Write(next);
			ReAnchorHere();
			if (rekeyed <= 0)
			{
				return;
			}
			string line = KingdomMirrorGateRules.HubbedLine(Capital, rekeyed);
			System.Ledger.Note(line);
			MessageQueue.AddPlayerMessage(line);
			KingdomChronicle.Record(System, KingdomMirrorGateRules.HubbedTelling(Capital));
			KingdomLog.Log("mirror-gate hub=" + Capital + " rekeyed=" + rekeyed + " rows=" + next.Length);
		}

		/// <summary>Re-reads the register into whatever arch is standing in the zone the founder is
		/// in. Every other arch reads it for itself the next time it is anchored.</summary>
		private static void ReAnchorHere()
		{
			Zone active = The.ZoneManager?.ActiveZone;
			if (active == null)
			{
				return;
			}
			foreach (GameObject found in active.GetObjects())
			{
				r_KingdomMirrorGate arch = found?.GetPart<r_KingdomMirrorGate>();
				if (arch != null)
				{
					Anchor(arch);
				}
			}
		}

		private static void Release(r_KingdomMirrorGate Gate, KingdomSystem System, KingdomGateRow[] rows, string city)
		{
			if (Popup.ShowYesNo("Unkey the arch at " + city + "?\n\nIt will stand exactly where it stands and cost nothing at all; the crossing simply stops answering.") != DialogResult.Yes)
			{
				return;
			}
			KingdomGateRow[] next;
			string orphan;
			KingdomGateVerdict verdict = KingdomMirrorGateRules.TryRelease(rows, Gate.LocationKey, out next, out orphan);
			if (verdict != KingdomGateVerdict.Released)
			{
				Popup.Show(KingdomMirrorGateRules.RefusalLine(verdict, city));
				return;
			}
			Write(next);
			Anchor(Gate);
			Gate.Dark = false;
			System.Ledger.Note("{{y|" + KingdomMirrorGateRules.ReleasedLine(KingdomPresentation.Rich(city)) + "}}");
			if (orphan.Length > 0)
			{
				// The other end was unkeyed by the same act and its own city is nowhere near: told
				// here, because there is no other moment at which the founder would find out.
				System.Ledger.Note("{{y|" + KingdomMirrorGateRules.OrphanedLine(KingdomPresentation.Rich(CityNamed(next, orphan))) + "}}");
			}
		}

		private static void GoDark(r_KingdomMirrorGate Gate, KingdomSystem System, string city)
		{
			if (Gate.Dark)
			{
				return;
			}
			Gate.Dark = true;
			System.Ledger.Note("{{r|" + KingdomMirrorGateRules.WentDarkLine(KingdomPresentation.Rich(city)) + "}}");
			KingdomChronicle.Record(System, KingdomMirrorGateRules.WentDarkTelling(KingdomPresentation.Rich(city), KingdomPresentation.Rich(System.KingdomDisplayName)));
		}

		/// <summary>The city keeping the arch under this key, or null when nothing does.</summary>
		private static string CityNamed(KingdomGateRow[] rows, string key)
		{
			int at = KingdomMirrorGateRules.IndexOfKey(rows, key);
			return (at < 0) ? null : rows[at].City;
		}

		/// <summary>
		/// Which of the realm's cities holds this ground, or null when the realm does not hold it at
		/// all. Delegated to <c>KingdomCrown.CityOf</c>, which is the one copy: the crown lane needs
		/// exactly this read and two of them would eventually disagree about which city an arch
		/// stands in, which is the one thing the register may never be wrong about.
		/// </summary>
		private static string CityOf(KingdomSystem System, string ZoneId)
		{
			return KingdomCrown.CityOf(System, ZoneId);
		}
	}
}
