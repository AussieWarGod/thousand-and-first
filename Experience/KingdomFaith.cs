using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled shell for Addendum 5's shrine and education conversion channels
	/// (<see cref="KingdomFaithRules"/> owns every decision that has one right answer given the
	/// facts): consecrating a standing faith building to a creed from the Charter, the attended
	/// pass that lets a staffed, consecrated shrine draw the neutral toward it, and the query
	/// surface a staffed knowledge building offers the cohabitation and osmosis ladders so they
	/// read the ambient grudge one band gentler.
	/// <para>
	/// <b>What this file never does.</b> It never converts anyone OPPOSED to a shrine's creed
	/// &mdash; that stance is filtered out in <see cref="KingdomFaithRules.ClassifyStance"/>
	/// before a single property is written &mdash; and it never touches an opposed resident's
	/// emigration state itself. Addendum 5's own guard is that a settler may always emigrate
	/// rather than convert, answered once, by one surface, for every channel that can put a
	/// resident under pressure they might resent (a declared realm creed against theirs, a rival
	/// shrine consecrated in their own quarter). This file only supplies the trigger for the
	/// shrine's own case; see <see cref="HandOffOpposedPressure"/> for exactly where that call is
	/// made and what it is waiting on.
	/// </para>
	/// <para>
	/// <b>Where "quarter" lives.</b> Nothing here or anywhere else in the mod has a type or a
	/// field named "quarter" &mdash; Addendum 4d is explicit that the word names an emergent
	/// pattern in the layout, not a game object. A shrine's own quarter is read the same way
	/// every other attended pass in this mod reads locality (<c>KingdomCreed</c>'s dissent pass,
	/// <c>KingdomCeremony</c>'s raising roll call, <c>KingdomLocus</c>'s keeper and guest passes):
	/// the zone it stands in, not a radius, not a district.
	/// </para>
	/// </summary>
	public static class KingdomFaith
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionFaith") != "No";

		/// <summary>
		/// The creed a standing faith building was consecrated to, or empty for a shrine stone
		/// nobody has yet named a creed for. Written only by <see cref="OpenConsecration"/>.
		/// </summary>
		public const string ShrineCreedProperty = "KingdomShrineCreed";

		/// <summary>7b flag: has the founder already been told this consecrated shrine is
		/// standing empty of hands. Cleared the pass it is staffed again, so a shrine that lapses
		/// twice is announced twice.</summary>
		public const string ShrineLapsedAnnouncedProperty = "KingdomShrineLapsedAnnounced";

		/// <summary>7b flag: the same idiom, for a knowledge building built to be staffed that
		/// presently has nobody at it.</summary>
		public const string EducationLapsedAnnouncedProperty = "KingdomEducationLapsedAnnounced";

		/// <summary>
		/// Days a neutral resident has spent drawn toward whichever staffed, consecrated shrine
		/// claimed them. Lives on the resident, exactly as <c>KingdomCreed.CreedProperty</c> and
		/// <c>KingdomBrink</c>'s own records do, because a resident's belief travelling with their
		/// own object is correct here: a shrine only ever pulls a resident who already has a place
		/// in this settlement's own zone, never one sleeping in the open with no stable object to
		/// speak of.
		/// </summary>
		public const string ShrinePullProperty = "KingdomShrinePull";

		/// <summary>Tick <see cref="ShrinePullProperty"/> was last advanced at, so the days a
		/// shrine argued at somebody are counted once however many passes resolve them.</summary>
		public const string ShrinePullTickProperty = "KingdomShrinePullTick";

		/// <summary>Bounded v1 option observation for this zone's faith clocks.</summary>
		public const string OptionStateProperty = "r_TAF_FaithOption_v1";

		/// <summary>Exact immutable settlement owning <see cref="OptionStateProperty"/>.</summary>
		public const string OptionOwnerProperty = "r_TAF_FaithOptionOwner_v1";

		/// <summary>Realm-wide option epoch. Every city/zone compares its local anchor against
		/// this so a transition first observed elsewhere cannot be billed here later.</summary>
		public const string GlobalOptionStatePrefix = "r_TAF_FaithGlobalOption_v1:";

		/// <summary>
		/// Later of a shrine brink's original warning and a master-resume observation. Master-off
		/// preserves the warned brink, while this anchor gives it a complete future window rather
		/// than billing the disabled span into a conversion.
		/// </summary>
		public const string ShrineWindowAnchorProperty = "KingdomShrineWindowOptionAnchor";

		/// <summary>One-bit receipt that this resident had unpaid shrine pressure when the
		/// module was disabled. Resume uses it once to plant a new full future pull interval.</summary>
		public const string ShrineDisabledActiveProperty = "KingdomShrineOptionWasActive";

		/// <summary>Raw property name AssignWork stamps on every crewed work. No public const
		/// exists for it yet anywhere in the mod (<c>KingdomLocus</c> reads the same literal
		/// directly); this file follows that precedent rather than inventing a second one.</summary>
		private const string StaffedProperty = "KingdomStaffed";

		private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,
			Zone Z, long Now)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			string realmId = System.CurrentRealmId;
			if (The.Game == null || !KingdomIdentityRules.IsSettlementId(settlementId)
				|| !KingdomIdentityRules.IsRealmId(realmId))
			{
				return KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
			}

			string globalKey = GlobalOptionStatePrefix + realmId;
			KingdomElapsedOptionRecord globalPrior;
			bool globalDecoded = KingdomElapsedOptionRules.TryDecode(
				The.Game.GetStringGameState(globalKey, ""), out globalPrior);
			if (!globalDecoded) globalPrior = KingdomElapsedOptionRecord.Unobserved;
			KingdomElapsedOptionDecision global = KingdomElapsedOptionRules.Observe(globalPrior,
				Enabled, System.MasterAppliedResumeToken, Now);
			if (!global.Valid)
			{
				global = KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
				globalDecoded = false;
			}
			string current = global.Valid
				? KingdomElapsedOptionRules.Encode(global.Record) : null;
			if (global.Valid && current != null && (!globalDecoded
				|| global.Transition != KingdomElapsedOptionTransition.None))
				The.Game.SetStringGameState(globalKey, current);

			string priorOwner = Z.GetZoneProperty(OptionOwnerProperty, null);
			bool ownerMatches = global::System.String.Equals(
				priorOwner, settlementId,
				global::System.StringComparison.Ordinal);
			string encoded = ownerMatches
				? Z.GetZoneProperty(OptionStateProperty, null) : null;
			KingdomElapsedOptionRecord prior = KingdomElapsedOptionRecord.Unobserved;
			bool zoneDecoded = ownerMatches
				&& KingdomElapsedOptionRules.TryDecode(encoded, out prior);
			bool zoneMatches = zoneDecoded && global.Valid
				&& prior.State == global.Record.State
				&& prior.ObservedTick == global.Record.ObservedTick
				&& prior.MasterResumeToken == global.Record.MasterResumeToken;
			if (!zoneMatches && global.Valid && current != null)
			{
				KingdomElapsedOptionTransition transition =
					KingdomElapsedOptionRules.LocalTransition(Enabled,
						!string.IsNullOrEmpty(priorOwner) && !ownerMatches,
						zoneDecoded, prior, global.Record);
				return new KingdomElapsedOptionDecision(true, global.Record, transition,
					Enabled ? KingdomElapsedOptionAction.AnchorEnabled
						: KingdomElapsedOptionAction.AnchorDisabled);
			}
			return global;
		}

		private static void CommitOption(KingdomSystem System, Zone Z,
			KingdomElapsedOptionRecord Record)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			if (Z == null || !KingdomIdentityRules.IsSettlementId(settlementId)) return;
			string current = KingdomElapsedOptionRules.Encode(Record);
			if (current == null) return;
			// Semantic cancellation/anchoring is complete before this helper. State then
			// owner makes a cut with a foreign owner retry safely.
			Z.SetZoneProperty(OptionStateProperty, current);
			Z.SetZoneProperty(OptionOwnerProperty, settlementId);
		}

		/// <summary>Module-off cancels only unpaid shrine pressure. Consecration, buildings,
		/// resident creed, education, and every non-shrine brink remain byte-for-byte.</summary>
		private static void CancelUncommittedFaith(KingdomSurvey Survey)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (!GameObject.Validate(settler)) continue;
				BrinkRecord brink = KingdomBrink.Of(settler, BrinkKind.Creed);
				bool shrineBrink = brink.Stands
					&& brink.Channel == (int)ConversionChannel.Shrine;
				if (settler.GetIntProperty(ShrinePullProperty) != 0
					|| settler.GetLongProperty(ShrinePullTickProperty) != 0L
					|| shrineBrink)
					settler.SetIntProperty(ShrineDisabledActiveProperty, 1);
				if (settler.GetIntProperty(ShrinePullProperty) != 0)
					settler.SetIntProperty(ShrinePullProperty, 0);
				if (settler.GetLongProperty(ShrinePullTickProperty) != 0L)
					settler.SetLongProperty(ShrinePullTickProperty, 0L);
				if (settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
				if (shrineBrink)
				{
					// No per-resident green reward/push: this is option cancellation, not an
					// in-world arrest authored by player action.
					KingdomBrink.Lift(settler, BrinkKind.Creed);
				}
			}
		}

		private static void ResumeCanceledFaith(KingdomSurvey Survey, long Now)
		{
			// Also cancel stale pressure on a body not present for the disable transition, then
			// consume the one-bit receipt into a fresh clock. Nothing converts on this wake.
			CancelUncommittedFaith(Survey);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (!GameObject.Validate(settler)
					|| settler.GetIntProperty(ShrineDisabledActiveProperty) != 1) continue;
				settler.SetIntProperty(ShrineDisabledActiveProperty, 0);
				settler.SetLongProperty(ShrinePullTickProperty, Now);
			}
		}

		private static void AnchorPreservedFaith(KingdomSurvey Survey, long Now)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (!GameObject.Validate(settler)) continue;
				if (settler.GetIntProperty(ShrinePullProperty) != 0
					|| settler.GetLongProperty(ShrinePullTickProperty) != 0L)
					settler.SetLongProperty(ShrinePullTickProperty, Now);
				BrinkRecord brink = KingdomBrink.Of(settler, BrinkKind.Creed);
				if (brink.Stands && brink.Channel == (int)ConversionChannel.Shrine
					&& brink.Warned)
					settler.SetLongProperty(ShrineWindowAnchorProperty, Now);
				else if (settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			}
		}

		// ==================================================================================
		// The attended pass: shrine conversion, and both channels' 7b lapse lines.
		// ==================================================================================

		/// <summary>
		/// The kingdom's one attended pass over this zone's faith and knowledge buildings. Call
		/// from <c>KingdomSystem.HandleEvent(ZoneActivatedEvent)</c>, after growth has resolved
		/// this pass's staffing (<see cref="StaffedProperty"/> must already be current) and after
		/// creed has resolved this pass's dissent (residents' own creeds are stable facts by
		/// then). Wrapped by the caller's own <c>Guard</c>, like every other module's pass.
		/// </summary>
		/// <param name="System">The kingdom. Unfounded or a zone the realm does not claim does
		/// nothing.</param>
		/// <param name="Z">The activated zone.</param>
		/// <param name="Survey">This pass's already-taken survey, for its <c>Works</c> and
		/// <c>Settlers</c> lists.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null
				|| The.Game == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long now = The.Game.TimeTicks;
			KingdomElapsedOptionDecision option = ObserveOption(System, Z, now);
			if (!option.Valid) return;
			if (option.Action == KingdomElapsedOptionAction.AnchorDisabled)
			{
				if (option.Transition == KingdomElapsedOptionTransition.Disabled
					|| option.Transition == KingdomElapsedOptionTransition.InitializedDisabled)
					CancelUncommittedFaith(Survey);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action == KingdomElapsedOptionAction.AnchorEnabled)
			{
				if (option.Transition == KingdomElapsedOptionTransition.Enabled)
					ResumeCanceledFaith(Survey, now);
				else
					AnchorPreservedFaith(Survey, now);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action != KingdomElapsedOptionAction.Run) return;
			HashSet<GameObject> claimed = new HashSet<GameObject>();
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				KingdomRules.BuildEntry entry;
				string key = work.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				if (KingdomFaithRules.CanConsecrate(entry.Category))
				{
					RunShrine(System, Z, Survey, work, entry, claimed);
				}
				else if (KingdomFaithRules.IsEducationCategory(entry.Category))
				{
					RunEducationLapse(work, entry);
				}
			}
			ForgetUnreached(System, Z, Survey, claimed);
		}

		// Rule 2 of the brink, for the settlers no shrine spoke to at all this pass: the building
		// that had them at the end of its road was struck, deconsecrated, unstaffed, or simply no
		// longer reaches where they stand, so the pressure is gone and the brink goes with it.
		// Without this sweep a shrine brink would outlive its shrine, which is the exact failure
		// IConversionPressure's re-derive-every-pass contract exists to forbid.
		private static void ForgetUnreached(KingdomSystem System, Zone Z, KingdomSurvey Survey, HashSet<GameObject> Claimed)
		{
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (Claimed.Contains(settler))
				{
					continue;
				}
				LiftShrineBrink(System, Z, settler);
				if (settler.GetIntProperty(ShrinePullProperty) != 0)
				{
					settler.SetIntProperty(ShrinePullProperty, 0);
				}
				if (settler.GetLongProperty(ShrinePullTickProperty) != 0L)
					settler.SetLongProperty(ShrinePullTickProperty, 0L);
				if (settler.GetIntProperty(ShrineDisabledActiveProperty) != 0)
					settler.SetIntProperty(ShrineDisabledActiveProperty, 0);
				if (settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			}
		}

		// Lifts a standing shrine brink and unsays it. A creed brink reached through any other
		// channel is not this file's to touch -- KingdomConversion spends and arrests those.
		private static bool LiftShrineBrink(KingdomSystem System, Zone Z, GameObject Settler)
		{
			BrinkRecord brink = KingdomBrink.Of(Settler, BrinkKind.Creed);
			if (!brink.Stands || brink.Channel != (int)ConversionChannel.Shrine)
			{
				if (Settler != null && Settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
					Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
				return false;
			}
			bool wasWarned = brink.Warned;
			KingdomBrink.Lift(Settler, BrinkKind.Creed);
			Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
			if (wasWarned)
			{
				// Only what was actually said is unsaid.
				KingdomBrink.Unsay(System, BrinkKind.Creed, NameOf(Settler), KingdomWord.StandsIn(Z), System.SeatName);
			}
			return true;
		}

		private static void RunShrine(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Shrine, KingdomRules.BuildEntry Entry, HashSet<GameObject> Claimed)
		{
			string shrineCreed = Shrine.GetStringProperty(ShrineCreedProperty);
			if (string.IsNullOrEmpty(shrineCreed))
			{
				// Not applicable: an unconsecrated shrine has no pass to run and says nothing
				// (STANDARDS 7b's other kind of early return).
				return;
			}
			bool staffed = Shrine.GetIntProperty(StaffedProperty) == 1;
			if (!staffed)
			{
				if (Shrine.GetIntProperty(ShrineLapsedAnnouncedProperty) != 1)
				{
					Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 1);
					MessageQueue.AddPlayerMessage(KingdomFaithRules.ShrineLapsedLine(Entry.Name, KingdomCreed.CreedName(shrineCreed)));
				}
				return;
			}
			Shrine.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (Claimed.Contains(settler))
				{
					continue;
				}
				// Addendum 6: a shrine draws whoever is IN ITS REACH, which for an S or M plot is
				// its own quarter -- the cluster of built ground it stands in, measured, not the
				// whole zone. Before the claim, so a settler this shrine cannot reach is still
				// there for the shrine in their own quarter.
				if (!KingdomReach.Reaches(System, Z, Shrine, settler))
				{
					continue;
				}
				Claimed.Add(settler);
				string residentCreed = settler.GetStringProperty(KingdomCreed.CreedProperty);
				int hostility = KingdomCreed.HostilityBetween(residentCreed, shrineCreed);
				KingdomFaithRules.ShrineStance stance = KingdomFaithRules.ClassifyStance(residentCreed, shrineCreed, hostility);
				switch (stance)
				{
				case KingdomFaithRules.ShrineStance.Neutral:
					AdvancePull(System, Z, settler, shrineCreed, Entry.Name);
					break;
				case KingdomFaithRules.ShrineStance.Opposed:
					ForgetPull(System, Z, settler);
					HandOffOpposedPressure(System, Z, settler, shrineCreed);
					break;
				default:
					ForgetPull(System, Z, settler);
					break;
				}
			}
		}

		// Clears a settler's pull and any shrine brink standing over them, because the shrine has
		// stopped arguing at them -- they took a creed, or they came to oppose it.
		private static void ForgetPull(KingdomSystem System, Zone Z, GameObject Settler)
		{
			LiftShrineBrink(System, Z, Settler);
			if (Settler.GetIntProperty(ShrinePullProperty) != 0)
			{
				Settler.SetIntProperty(ShrinePullProperty, 0);
			}
			if (Settler.GetLongProperty(ShrinePullTickProperty) != 0L)
				Settler.SetLongProperty(ShrinePullTickProperty, 0L);
			if (Settler.GetIntProperty(ShrineDisabledActiveProperty) != 0)
				Settler.SetIntProperty(ShrineDisabledActiveProperty, 0);
			if (Settler.GetLongProperty(ShrineWindowAnchorProperty) != 0L)
				Settler.SetLongProperty(ShrineWindowAnchorProperty, 0L);
		}

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

		// ==================================================================================
		// Education's query surface -- consulted on demand, owns no pass of its own.
		// ==================================================================================

		/// <summary>
		/// Whether a staffed knowledge building stands in this zone right now. The one fact the
		/// cohabitation and osmosis ladders need; neither is told which building or which creed,
		/// because education softens the whole zone's grudge rather than taking a side in it.
		/// </summary>
		public static bool ZoneEducated(Zone Z)
		{
			if (!Enabled || Z == null)
			{
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.Built)
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1 || item.GetIntProperty(StaffedProperty) != 1)
				{
					continue;
				}
				string key = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
				{
					continue;
				}
				if (KingdomFaithRules.IsEducationCategory(entry.Category))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Convenience for a caller that already has the resident's own closeness rung and just
		/// wants education's own softening folded in: one band gentler when a knowledge building
		/// actually reaches this roof, the rung unchanged otherwise. See
		/// <c>KingdomFaithRules.SoftenedCloseness</c> for the arithmetic, and
		/// <c>Growth/KingdomLodging.cs</c> for the two call sites.
		/// </summary>
		/// <param name="Z">The zone the roof stands in.</param>
		/// <param name="Quarters">The resident's own closeness rung.</param>
		/// <param name="Home">The roof being judged. Naming it asks the re-based question
		/// (Addendum 6: education softens the grudge of whoever the knowledge work REACHES); a
		/// caller that cannot name one gets the zone-wide answer this file has always given.
		/// </param>
		public static KingdomLodgingRules.Closeness EducatedCloseness(Zone Z, KingdomLodgingRules.Closeness Quarters, GameObject Home = null)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			bool educated = (Home != null && system != null)
				? KingdomReach.EducatedAt(system, Z, Home)
				: ZoneEducated(Z);
			return educated ? KingdomFaithRules.SoftenedCloseness(Quarters) : Quarters;
		}

		// ==================================================================================
		// Consecration -- the Charter's own ceremony.
		// ==================================================================================

		/// <summary>
		/// The Charter's "consecrate a shrine" action: names a standing faith building for a
		/// creed the realm has dealt with. One creed per shrine; naming a second creed later is a
		/// second ceremony, and the chronicle keeps the first exactly as it was written.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Founder">The Charter's own object, for its current zone &mdash; the same
		/// shape every other Charter action in this mod takes (<c>KingdomDesign.RenameBuilding</c>,
		/// <c>KingdomSocket.OpenConvert</c>).</param>
		public static void OpenConsecration(KingdomSystem System, GameObject Founder)
		{
			if (!Enabled || System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			Zone zone = Founder?.CurrentZone;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A shrine is consecrated standing on the realm's own ground.");
				return;
			}
			List<GameObject> shrines = FaithBuildingsIn(zone);
			if (shrines.Count == 0)
			{
				Popup.Show("Nothing built here answers to a creed. Raise a shrine stone, a shrine garth, or a temple first.");
				return;
			}
			string[] shrineOptions = new string[shrines.Count];
			for (int i = 0; i < shrines.Count; i++)
			{
				string held = shrines[i].GetStringProperty(ShrineCreedProperty);
				shrineOptions[i] = shrines[i].ShortDisplayName + (string.IsNullOrEmpty(held) ? "" : (" {{C|[" + KingdomCreed.CreedName(held) + "]}}"));
			}
			int picked = Popup.PickOption(Title: "Consecrate a shrine, at " + KingdomPresentation.Rich(System.SeatName), Options: shrineOptions, AllowEscape: true);
			if (picked < 0)
			{
				return;
			}
			GameObject target = shrines[picked];
			List<string> candidates = KingdomCreed.Candidates(System);
			if (candidates.Count == 0)
			{
				Popup.Show("The realm has dealt with nobody yet that it could consecrate a shrine to. Standings come first.");
				return;
			}
			string currentCreed = target.GetStringProperty(ShrineCreedProperty);
			string[] creedOptions = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				creedOptions[i] = KingdomCreed.CreedName(candidates[i]) + ((candidates[i] == currentCreed) ? " {{G|[consecrated]}}" : "");
			}
			int creedPicked = Popup.PickOption(Title: "Consecrate " + target.ShortDisplayName + " to", Options: creedOptions, AllowEscape: true);
			if (creedPicked < 0)
			{
				return;
			}
			string chosenCreed = candidates[creedPicked];
			if (chosenCreed == currentCreed)
			{
				Popup.Show("It is consecrated to them already.");
				return;
			}
			bool reconsecration = !string.IsNullOrEmpty(currentCreed);
			bool neverStaffable = !KingdomData.TryGetBuilding(target.GetStringProperty(KingdomUpgrade.BuildKeyProperty), out KingdomRules.BuildEntry entry) || entry.Staff <= 0;
			string creedDisplay = KingdomCreed.CreedName(chosenCreed);
			if (Popup.ShowYesNo(KingdomFaithRules.ConsecrationPrompt(target.ShortDisplayName, creedDisplay, reconsecration, neverStaffable)) != DialogResult.Yes)
			{
				return;
			}
			target.SetStringProperty(ShrineCreedProperty, chosenCreed);
			KingdomGovernanceScope.Commit("consecrate shrine");
			target.SetIntProperty(ShrineLapsedAnnouncedProperty, 0);
			KingdomChronicle.Record(System, KingdomFaithRules.ConsecrationChronicle(target.ShortDisplayName, KingdomPresentation.Rich(System.SeatName), creedDisplay, reconsecration));
			Popup.Show(KingdomFaithRules.ConsecrationNotice(target.ShortDisplayName, creedDisplay, reconsecration, neverStaffable));
			KingdomLog.Log("faith: consecrated " + target.ShortDisplayName + " to " + chosenCreed + " reconsecration=" + reconsecration);
		}

		private static List<GameObject> FaithBuildingsIn(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			foreach (GameObject item in survey.Built)
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				string key = item.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
				{
					continue;
				}
				if (KingdomFaithRules.CanConsecrate(entry.Category))
				{
					found.Add(item);
				}
			}
			return found;
		}
	}
}
