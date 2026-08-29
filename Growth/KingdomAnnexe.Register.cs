using System;
using System.Collections.Generic;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of the becoming annexe: the register the founder reads, the
	/// once-ever enrolment ceremony, and the live read that tells a body whether its city still
	/// keeps its roll.
	/// <para>
	/// <b>Nothing here is a second cybernetics system.</b> The annexe grants two things and no
	/// more: a line on the city's roster, and the license points the vanilla terminal budgets in
	/// (<c>XRL/UI/CyberneticsTerminal.cs:71</c>, an int property character creation writes and the
	/// event never touches). Every implant, every price, every slot conflict and every removal
	/// after that is the shipped becoming nook's, unmodified.
	/// </para>
	/// <para>
	/// Every decision that does not need a real object &mdash; who may be entered, what it costs,
	/// what a refusal says, what the register draws &mdash; is delegated to the engine-free
	/// <see cref="KingdomAnnexeRules"/>, and the cardinality and creed-friction arithmetic to
	/// <see cref="KingdomLabRules"/>, which already ships them.
	/// </para>
	/// </summary>
	internal static partial class KingdomAnnexe
	{
		/// <summary>Whether the annexe is switched on. Off, the register does not open and no roll
		/// is written &mdash; but a roll already written still stands, because switching a module
		/// off is not a way to take a thing off a founder.</summary>
		internal static bool Enabled => Options.GetOption("r_TAF_OptionAnnexe") != "No";

		/// <summary>The engine's own license budget, by the name the terminal reads it under
		/// (<c>XRL/UI/CyberneticsTerminal.cs:71</c>).</summary>
		internal const string LicenseProperty = "CyberneticsLicenses";

		/// <summary>
		/// Game-state key for the once-ever chrome-debt petition latch.
		/// <para>
		/// <b>Game state rather than a field, deliberately.</b> The lab's twin latch sits on the
		/// founder's own lab record because that record already existed; nothing here does, and
		/// appending a bool to <c>KingdomSystem</c> for one bit would be a serialized field bought
		/// with a save migration. The mirror-gate's register is the shipped precedent for
		/// realm-scoped state that is not a city's property, and this is one bit of exactly that
		/// kind: whether the founder has been asked, once, about the debt.
		/// </para>
		/// </summary>
		internal const string SpokenState = "r_TAF_AnnexeChromeSpoken";

		// ==================================================================================
		// The live read
		// ==================================================================================

		/// <summary>
		/// Whether any city the realm still holds carries this roll.
		/// <para>
		/// <b>Both cities, not the seat only.</b> Addendum 22 B4's seat-only rule is about what
		/// the KEEPERS know, and knowledge is a thing a city teaches. A roll is not knowledge: it
		/// is the realm asserting who it counts, and a founder standing in their other city has
		/// not stopped being counted. What ends a roll is the realm ceasing to hold the book
		/// &mdash; which is exactly what secession and exile do to the container.
		/// </para>
		/// </summary>
		/// <param name="Who">The <c>GeneID</c> the roll was written under.</param>
		internal static bool RealmHolds(string Who)
		{
			if (string.IsNullOrEmpty(Who) || The.Game == null)
			{
				return false;
			}
			return HeldBy(The.Game.RequireSystem<KingdomSystem>(), Who);
		}

		/// <summary>The same read against a realm already in hand, for callers holding one.</summary>
		internal static bool HeldBy(KingdomSystem Realm, string Who)
		{
			if (Realm == null || !Realm.Founded || string.IsNullOrEmpty(Who))
			{
				return false;
			}
			if (KingdomAnnexeRules.Enrolled(KingdomZoning.Roster(Realm), Who))
			{
				return true;
			}
			List<KingdomSettlement> nonSeat = Realm.NonSeatSettlements();
			for (int i = 0; i < nonSeat.Count; i++)
				if (KingdomAnnexeRules.Enrolled(KingdomZoning.RosterOf(nonSeat[i]), Who))
					return true;
			return false;
		}

		/// <summary>Says the one sentence a founder whose nooks stopped opening is owed, once.</summary>
		internal static void AnnounceLapse(string City)
		{
			string shownCity = KingdomPresentation.Rich(City);
			MessageQueue.AddPlayerMessage(KingdomAnnexeRules.LapseLine(shownCity));
			KingdomSystem realm = The.Game?.RequireSystem<KingdomSystem>();
			if (realm != null && realm.Founded)
			{
				KingdomChronicle.Record(realm, KingdomAnnexeRules.LapseTelling(shownCity));
			}
			KingdomLog.Log("annexe: roll lapsed (" + City + ")");
		}

		/// <summary>
		/// Whether the engine would call this creature True Kin WITHOUT us.
		/// <para>
		/// Read off <c>genotypeEntry</c> directly rather than through <c>IsTrueKin()</c>, and the
		/// difference is the whole point: <c>IsTrueKin()</c> would include our own answer, so an
		/// already-enrolled citizen would be refused as "already Kin" and the founder told
		/// nonsense. This is the raw seed <c>IsTrueKinEvent.Check</c> itself starts from
		/// (<c>XRL/World/IsTrueKinEvent.cs:32</c>).
		/// </para>
		/// </summary>
		internal static bool KinByBirth(GameObject Who)
		{
			return Who != null && Who.genotypeEntry != null && Who.genotypeEntry.IsTrueKin;
		}

		// ==================================================================================
		// The register
		// ==================================================================================

		/// <summary>
		/// The screen at the annexe: who keeps the book, whose names are in it, and the one act
		/// that adds a name.
		/// <para>
		/// One <c>Popup.PickOption</c> and no new screen class, which is the house idiom the lab's
		/// slate and the keepers' screen both keep. The rolls are shown HERE and nowhere else: the
		/// keepers' screen lists what a city KNOWS, and who a city COUNTS is a different book kept
		/// in a different room.
		/// </para>
		/// </summary>
		internal static void OpenRegister(GameObject Building, GameObject Actor)
		{
			if (!Enabled)
			{
				return;
			}
			KingdomSystem realm = The.Game?.RequireSystem<KingdomSystem>();
			if (realm == null || !realm.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			// Whether the building this screen was opened from may hold the ceremony at all. The
			// enrolment gate already asks this (JudgeFor's Annexe: argument), and asking it here as
			// well is what keeps the OFFER honest: a registry office that listed a name and then
			// refused it on the press would be teaching the founder that the outpost is broken
			// rather than that it is an outpost (Addendum 22 A2; END-STATE §5.5).
			bool enrols = Building != null && Building.HasPart("r_KingdomBecomingAnnexe");
			string city = CityAt(realm, Building);
			string shownCity = KingdomPresentation.Rich(city);
			while (true)
			{
				List<string> rolls = KingdomAnnexeRules.Rolls(KingdomZoning.Roster(realm));
				List<GameObject> candidates = enrols ? Candidates(realm, Building, Actor) : new List<GameObject>();
				List<string> options = new List<string>();
				List<GameObject> targets = new List<GameObject>();
				for (int i = 0; i < candidates.Count; i++)
				{
					options.Add("{{W|Enter " + KingdomPresentation.Rich(PlainName(candidates[i]))
						+ " on the rolls}}");
					targets.Add(candidates[i]);
				}
				if (options.Count == 0)
				{
					// 7b's applicable-but-blocked case, and it has two readings that must not be
					// told with one sentence: the annexe with nobody in front of it is waiting for a
					// person, and the outpost is never going to write anybody down at all.
					options.Add(enrols
						? "{{K|There is nobody here this city could enter}}"
						: "{{K|Names are entered on the rolls only at the annexe itself}}");
					targets.Add(null);
				}
				List<string> names = RollNames(Building, rolls);
				for (int i = 0; i < names.Count; i++)
				{
					options.Add(KingdomAnnexeRules.RegisterRow(
						KingdomPresentation.Rich(names[i]), Held: true));
					targets.Add(null);
				}
				options.Add("Close");
				targets.Add(null);
				int picked = Popup.PickOption(
					Title: KingdomAnnexeRules.RegisterTitle(shownCity),
					Intro: KingdomAnnexeRules.RegisterIntro(
						KingdomPresentation.Rich(KeeperAt(realm, Building)), rolls.Count),
					Options: options, AllowEscape: true, RespectOptionNewlines: true);
				if (picked < 0 || picked >= targets.Count || targets[picked] == null)
				{
					return;
				}
				Offer(realm, Building, Actor, targets[picked]);
			}
		}

	}
}
