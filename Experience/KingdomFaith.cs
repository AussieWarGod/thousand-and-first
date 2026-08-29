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
	public static partial class KingdomFaith
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

	}
}
