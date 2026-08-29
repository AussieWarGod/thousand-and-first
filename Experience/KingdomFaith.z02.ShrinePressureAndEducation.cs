using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFaith
	{
		/// <summary>
		/// One staffed, consecrated shrine's day-by-day work on one neutral resident, and the
		/// brink at the end of it.
		/// <para>
		/// The pull accrues over the days the shrine actually stood staffed over them (Addendum 8
		/// clause 1 and clause 2 together: a consecrated building argues every day, and an
		/// unstaffed one argues on none of them). The staffing is read as it stands on this pass
		/// and applied to the whole stretch, which is the same honest approximation every
		/// labour-gated clock in the mod makes: the founder's own passes are the only places the
		/// settlement's staffing is ever decided, so the staffing at the last pass is the staffing
		/// that held through the stretch.
		/// </para>
		/// <para>
		/// At the end of the road NOTHING FIRES. This is the channel that used to change what
		/// somebody believed with no warning of any kind &mdash; the only irreversible social
		/// consequence in the mod without one. It now records a brink through
		/// <c>KingdomConversion.NoteRoadsEnd</c>, which names the settler and the shrine's creed
		/// and the honest elapsed in both registers (STANDARDS 7b), and the conversion itself
		/// waits out <c>KingdomBrinkRules.CreedBrinkWindowDays</c> of WORLD TIME in which
		/// deconsecrating the shrine, taking its staff off it, or moving the settler out of its
		/// reach all stop it. Addendum 10(a): that window spends whether or not the founder comes
		/// back to watch it, and the conversion is dated to the day it ran out &mdash; but it only
		/// ever starts on the day the word reaches them.
		/// </para>
		/// </summary>
		private static void AdvancePull(KingdomSystem System, Zone Z, GameObject Settler, string ShrineCreed, string BuildingName)
		{
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			BrinkRecord brink = KingdomBrink.Of(Settler, BrinkKind.Creed);
			if (brink.Stands)
			{
				if (brink.Channel == (int)ConversionChannel.Shrine)
				{
					SpendShrineWindow(System, Z, Settler, ShrineCreed, brink);
				}
				// A brink reached through some other channel is somebody else's window to spend,
				// and nothing accrues past a brink in any case.
				return;
			}
			if (Settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
				Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			long last = Settler.GetLongProperty(ShrinePullTickProperty);
			if (last <= 0L || now <= 0L)
			{
				// Planting the stamp before the first count, for the reason the fetch clock plants
				// its own: an unplanted stamp read as elapsed is the age of the world.
				Settler.SetLongProperty(ShrinePullTickProperty, now);
				return;
			}
			int days = KingdomRules.ElapsedDays(now - last);
			if (days <= 0)
			{
				return;
			}
			Settler.SetLongProperty(ShrinePullTickProperty, KingdomRules.AdvanceCheckpoint(last, now));
			int was = Settler.GetIntProperty(ShrinePullProperty);
			int pull = KingdomFaithRules.PullAfterDays(was, days);
			Settler.SetIntProperty(ShrinePullProperty, pull);
			if (!KingdomFaithRules.ConversionReady(pull))
			{
				return;
			}
			long reached = KingdomBrinkRules.CrossingTick(
				now - (long)days * KingdomRules.TicksPerDay, now, was,
				KingdomFaithRules.ConversionPullThreshold, 1);
			KingdomConversion.NoteRoadsEnd(System, Z, Settler, NameOf(Settler), ShrineCreed, ConversionChannel.Shrine, reached, now);
		}

		// A standing shrine brink, judged against the world's clock. The cause is re-derived by
		// the caller -- this only runs for a settler a staffed, consecrated shrine still reaches
		// and still finds neutral -- so reaching here at all is the pressure still standing.
		private static void SpendShrineWindow(KingdomSystem System, Zone Z, GameObject Settler, string ShrineCreed, BrinkRecord Brink)
		{
			if (Brink.Cause != ShrineCreed)
			{
				// A different shrine has claimed them, or this one was reconsecrated. The creed at
				// the end of their road is not the creed being pressed any more.
				LiftShrineBrink(System, Z, Settler);
				return;
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			if (KingdomBrink.MarkWarned(Settler, BrinkKind.Creed, now))
			{
				// Recorded by a path that could not speak, or carried across a save from before
				// the word went out. Told now, and the whole window runs from now.
				KingdomBrink.Announce(System, BrinkKind.Creed, NameOf(Settler), KingdomCreed.CreedName(ShrineCreed),
					KingdomBrink.Of(Settler, BrinkKind.Creed), now, KingdomWord.StandsIn(Z), System.SeatName, null);
				return;
			}
			long windowStart = KingdomFaithRules.EffectiveWindowStart(Brink.WarnedTick,
				Settler.GetLongProperty(ShrineWindowAnchorProperty), now);
			if (!KingdomBrinkRules.WindowSpent(BrinkKind.Creed, windowStart, now))
			{
				return;
			}
			int ago = KingdomBrinkRules.DaysStood(
				KingdomBrinkRules.ExpiryTick(BrinkKind.Creed, windowStart), now);
			string residentName = NameOf(Settler);
			int roads = Settler.GetIntProperty(KingdomConversion.RoadsWalkedProperty);
			string settlementId = KingdomChronicle.SettlementId(System);
			if (!KingdomIdentityRules.IsSettlementId(settlementId)) return;
			bool turns = KingdomConversionRules.Converts(
				settlementId, ConversionChannel.Shrine, residentName,
				KingdomConversionRules.RoadEnd(roads));
			Settler.SetIntProperty(KingdomConversion.RoadsWalkedProperty, roads + 1);
			KingdomBrink.Lift(Settler, BrinkKind.Creed);
			Settler.SetIntProperty(ShrinePullProperty, 0);
			Settler.SetLongProperty(ShrinePullTickProperty, 0L);
			Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			if (!turns)
			{
				// The shrine argued a whole season and it did not take. Said, because the founder
				// was told it was coming.
				KingdomBrink.Unsay(System, BrinkKind.Creed, residentName, KingdomWord.StandsIn(Z), System.SeatName);
				return;
			}
			// The one path a conversion may take. Calling KingdomCreed.Record directly here
			// skipped Forget (the old creed's tally never decremented), left the settler's
			// standing pressure entries in place, and wrote a chronicle line without the
			// two-register dispute the contested case demands. Convert does all of it, in the
			// only order that keeps the tally honest.
			if (!KingdomConversion.Convert(System, Z, Settler, ShrineCreed, ConversionChannel.Shrine))
			{
				return;
			}
			string creedName = KingdomCreed.CreedName(ShrineCreed);
			MessageQueue.AddPlayerMessage(KingdomFaithRules.ConversionMessage(
				KingdomPresentation.Rich(residentName), creedName)
				+ KingdomBrinkRules.FiredClause(ago));
			KingdomLog.Log("faith: conversion " + residentName + " -> " + ShrineCreed + " at " + (Z?.ZoneID ?? "-"));
		}

		/// <summary>
		/// Addendum 5's guard, for the shrine's own case. A resident whose creed is OPPOSED to a
		/// staffed, consecrated shrine in their own quarter is never pulled toward it &mdash; the
		/// caller has already filtered that out &mdash; but the pressure of standing that ground
		/// anyway is real, and the guard promises it never becomes slow compulsion with no exit.
		/// <para>
		/// Hands off to <see cref="KingdomConversion.NotePressure"/> &mdash; the surface every
		/// channel shares &mdash; rather than reimplementing the grace here: the named, chronicled,
		/// warned, world-day window ending in the existing emigration machinery is that file's own
		/// state to own, not a second copy of <c>KingdomLodgingRules.GraceDays</c> kept here. This is
		/// deliberately the only place in this file that reaches outside
		/// <c>KingdomFaith</c>/<c>KingdomFaithRules</c> and the already-shipped
		/// <c>KingdomCreed</c>/<c>KingdomChronicle</c>/<c>KingdomLog</c> surfaces.
		/// </para>
		/// </summary>
		private static void HandOffOpposedPressure(KingdomSystem System, Zone Z, GameObject Resident, string ShrineCreed)
		{
			KingdomConversion.NotePressure(System, Z, Resident, ConversionChannel.Shrine, ShrineCreed);
		}

		private static void RunEducationLapse(GameObject Scriptorium, KingdomRules.BuildEntry Entry)
		{
			bool staffed = Scriptorium.GetIntProperty(StaffedProperty) == 1;
			if (staffed)
			{
				Scriptorium.SetIntProperty(EducationLapsedAnnouncedProperty, 0);
				return;
			}
			if (Scriptorium.GetIntProperty(EducationLapsedAnnouncedProperty) == 1)
			{
				return;
			}
			Scriptorium.SetIntProperty(EducationLapsedAnnouncedProperty, 1);
			MessageQueue.AddPlayerMessage(KingdomFaithRules.EducationLapsedLine(Entry.Name));
		}

		private static string NameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
			return (Resident == null) ? "a settler" : Resident.BaseDisplayNameStripped;
		}

	}
}
