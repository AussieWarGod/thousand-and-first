using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the mirror-gate: anchoring an arch on real ground, the dedication
	/// rite, the standing draw settled against real charge, and the crossing itself.
	/// <para>
	/// <b>The register is the pairing.</b> An arch never writes on its twin, because its twin is
	/// almost always standing in a zone nobody has loaded. Both ends write only their own key; one
	/// string in the game's own state says who answers whom, exactly as one string carries the
	/// keepers' knowledge roster (<c>KingdomZoning.Roster</c>). That is what makes capital-hub
	/// reconciliation and the founder's explicit destination choice rewrites of one column rather
	/// than walks over dormant cities, and why <c>KingdomMirrorGateRules.TryPair</c> exists.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; who may answer whom, what a day
	/// costs, whether the works paid it, every refusal's wording &mdash; is delegated to the
	/// engine-free <see cref="KingdomMirrorGateRules"/>.
	/// </para>
	/// </summary>
	internal static partial class KingdomMirrorGate
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
			if (!KingdomUpgrade.IsFunctionallyBuilt(Gate.ParentObject)
				&& (Gate.ParentObject.GetIntProperty("KingdomGrid") != 1
					|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(Gate.ParentObject)))
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
			if (KingdomMaterials.HasActiveStrikeReceipt(system, zone, Gate.ParentObject))
			{
				Popup.Show("This arch is already condemned. Call off the strike before keying it, or leave it unkeyed for the crew.");
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

	}
}
