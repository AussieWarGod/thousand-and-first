using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The rite of shared water held with one of the founder's own settlers: Addendum 5's
	/// diplomacy channel, and the only one that works on one named person at a time.
	/// <see cref="KingdomWaterRiteRules"/> owns every decision and every hand-written line; this
	/// file gathers the facts off real people and real buildings, spends the real water, and hands
	/// the outcome to the surfaces that already exist for it.
	/// <para>
	/// <b>What this is.</b> Qud's own water ritual is the setting's central act: you share your
	/// water with a stranger, and afterwards you are water-bonded to them and to everything they
	/// belong to. This is that act turned inward, and it is deliberately <em>not</em> the engine's
	/// ritual machinery &mdash; no <c>WaterRitualRecord</c>, no reputation award, no Sifrah board.
	/// Those exist to move the player's standing with a faction through whichever stranger is
	/// standing there. Our settlers are the realm's own roll, not vanilla ritualists; what moves
	/// here is one person's belief, and it moves because the founder filled a bowl from the city's
	/// stores, set it on the ground, and waited.
	/// </para>
	/// <para>
	/// <b>It builds nothing that already exists.</b> A conversion goes through
	/// <c>KingdomConversion.Convert</c> &mdash; the one path every channel's conversion takes, so
	/// the tally, both registers and the ledger cannot drift apart. A settler pressed past bearing
	/// goes to <c>KingdomConversion</c>'s pressure surface through
	/// <see cref="IConversionPressure"/>, so there is one exit in this mod, with one set of words
	/// and one grace, and this file does not open a second. What is genuinely this channel's own
	/// is here and nowhere else: the invitation, the price, the answer, and the rule that a
	/// question asked once is not asked again until something is different.
	/// </para>
	/// <para>
	/// <b>Rare on purpose.</b> One rite, one soul: the cadence is the same three days
	/// <c>KingdomCreed.HoldRite</c> asks between rites of shared water between cities, read from
	/// the same constant and for the same stated reason. The price is the founding basin's own
	/// eight drams plus a measure for whatever stands in the way, disclosed before it is paid, and
	/// spent whichever way they answer.
	/// </para>
	/// <para>
	/// <b>Its state lives on the people.</b> Shared living, the stamp a refusal leaves, and the
	/// mark of an asking that went too far are string and int properties on the settler, so they
	/// travel with the person, go with the person, and cost a settlement that has never held a
	/// rite exactly nothing. The realm carries one new field, the tick of the last rite, for the
	/// cadence.
	/// </para>
	/// </summary>
	public static class KingdomWaterRite
	{
		/// <summary>
		/// Gated on the conversion machinery rather than on an option of its own: an acceptance is
		/// recorded through <c>KingdomConversion.Convert</c>, so a rite offered while that is off
		/// would pour real water into a channel that could not land it.
		/// </summary>
		public static bool Enabled
		{
			get { return KingdomConversion.Enabled; }
		}

		/// <summary>
		/// Attended passes this settler has been present for &mdash; shared living WITH THE
		/// SETTLEMENT, which is not <c>KingdomConversionRules.SharedLivingForConversion</c>'s
		/// shared living TOWARD ONE CREED. That one is household-scoped, closeness-scaled and
		/// redirected the moment somebody moves house; this one asks only how much of this
		/// settlement's life this person has stood through. The rite needs the second because it
		/// exists to reach the people the first cannot: the settler in a quarter of their own,
		/// whom no household majority is pulling at.
		/// <para>
		/// Advanced by <see cref="OnSettlementPass"/> and by nothing else, so an absent founder
		/// adds none of it.
		/// </para>
		/// </summary>
		public const string SharedDaysProperty = "KingdomSharedDays";

		/// <summary>Tick <see cref="SharedDaysProperty"/> was last advanced at, so two callers
		/// resolving the same moment cannot count one evening twice.</summary>
		public const string SharedDayTickProperty = "KingdomSharedDayTick";

		/// <summary>Refusals this settler has given. See
		/// <c>KingdomWaterRiteRules.AskedTooOften</c>.</summary>
		public const string RefusalsProperty = "KingdomWaterRiteRefusals";

		/// <summary>The answer their last refusal gave, stored as the enum value plus one so that
		/// zero &mdash; what an absent property reads as &mdash; means "never asked".</summary>
		public const string StampAnswerProperty = "KingdomWaterRiteAnswer";

		/// <summary>Hostility as it stood at their refusal.</summary>
		public const string StampHostilityProperty = "KingdomWaterRiteHostility";

		/// <summary>Whether a rival shrine stood in their quarter at their refusal.</summary>
		public const string StampShrineProperty = "KingdomWaterRiteShrine";

		/// <summary>Whether only a change of the realm's own creed can re-open the question.</summary>
		public const string StampAbsoluteProperty = "KingdomWaterRiteAbsolute";

		/// <summary>Shared passes at which their reach would have covered the distance, or zero.</summary>
		public const string StampNeededProperty = "KingdomWaterRiteNeeded";

		/// <summary>The realm's creed as it stood at their refusal.</summary>
		public const string StampCreedProperty = "KingdomWaterRiteCreed";

		/// <summary>
		/// The creed a settler was asked about one time too many. While the realm still holds it,
		/// the rite is shut to them and <see cref="RepeatedAsking"/> reports it to
		/// <c>KingdomConversion</c> as pressure &mdash; which walks them out only if they resent it
		/// (<c>KingdomConversionRules.Resents</c>), and otherwise simply leaves them alone.
		/// </summary>
		public const string AskedTooOftenCreedProperty = "KingdomWaterRiteClosedCreed";

		// ==================================================================================
		// The pressure source. Repeated asking is the one thing about this channel that IS
		// imposed: KingdomConversionRules.IsImposed names Diplomacy as invited and consented,
		// and it is -- once. Asked over and over it stops being an invitation, so this file
		// reports it as standing pressure through the sanctioned surface rather than growing an
		// exit of its own. Re-derived every pass by that surface's own contract, so a founder
		// who stops holding the creed they kept asking about takes the pressure off by doing it.
		// ==================================================================================

		private sealed class RepeatedAsking : IConversionPressure
		{
			public string PressingCreed(KingdomSystem System, Zone Z, GameObject Settler)
			{
				if (System == null || Settler == null)
				{
					return null;
				}
				string closed = Settler.GetStringProperty(AskedTooOftenCreedProperty);
				if (string.IsNullOrEmpty(closed))
				{
					return null;
				}
				return KingdomWaterRiteRules.SameCreed(closed, RealmCreed(System)) ? closed : null;
			}
		}

		private static readonly RepeatedAsking Pressure = new RepeatedAsking();

		/// <summary>
		/// Registers this channel's standing pressure source with <c>KingdomConversion</c>.
		/// Idempotent both here and there, and called from every entry point of this file rather
		/// than from a load hook, so a mid-session mod rebuild or a registry re-read cannot leave
		/// the exit unregistered.
		/// </summary>
		public static void Register()
		{
			KingdomConversion.AddPressureSource(Pressure);
		}

		// ==================================================================================
		// The Charter's own action
		// ==================================================================================

		/// <summary>
		/// The Charter's "share water with a settler" action: lists everyone standing here the
		/// founder could ask and what each would cost, and holds the rite on the one they pick.
		/// <para>
		/// Preconditions: a founded realm, the founder standing on its own claimed ground, and a
		/// creed the realm actually holds. Side effects: on a rite held, drams leave the dedicated
		/// stores, the settler's creed may change through <c>KingdomConversion.Convert</c>, the
		/// registers record the night, and the realm's rite cadence is stamped. Failure mode: every
		/// refusal to offer is a founder-facing line naming what would have to be different, and
		/// nothing is spent.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Founder">The Charter's own object, read for the ground it is standing on.</param>
		public static void OpenRite(KingdomSystem System, GameObject Founder)
		{
			KingdomSystem.Guard("water rite: charter", delegate
			{
				Register();
				if (System == null || Founder == null)
				{
					return;
				}
				if (!Enabled)
				{
					Popup.Show("You are not keeping account of what your people believe.");
					return;
				}
				if (!System.Founded)
				{
					Popup.Show("You rule nothing yet.");
					return;
				}
				Zone zone = Founder.CurrentZone;
				if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
				{
					Popup.Show(KingdomWaterRiteRules.BarLine(WaterRiteBar.NotOnOurGround, null, null, 0, 0));
					return;
				}
				string realmCreed = RealmCreed(System);
				if (string.IsNullOrEmpty(realmCreed))
				{
					Popup.Show(KingdomWaterRiteRules.BarLine(WaterRiteBar.RealmBelievesNothing, null, null, 0, 0));
					return;
				}
				while (true)
				{
					List<GameObject> people = CandidatesIn(System, zone);
					if (people.Count == 0)
					{
						Popup.Show("There is nobody standing in " + KingdomPresentation.Rich(System.SeatName) + " whose name the roll carries. Water is shared with a person, and a person has a name.");
						return;
					}
					int stored = KingdomGrowth.CountStoredWater(zone);
					List<RiteOffer> offers = new List<RiteOffer>();
					string[] options = new string[people.Count];
					for (int i = 0; i < people.Count; i++)
					{
						RiteOffer offer = OfferFor(System, zone, people[i], realmCreed, stored);
						offers.Add(offer);
						options[i] = KingdomWaterRiteRules.RowLabel(
							KingdomPresentation.Rich(NameOf(people[i])),
							KingdomCreed.CreedName(people[i].GetStringProperty(KingdomCreed.CreedProperty)),
							offer.Drams,
							offer.Bar,
							KingdomWaterRiteRules.AskedTooOften(people[i].GetIntProperty(RefusalsProperty)));
					}
					int picked = Popup.PickOption(
						Title: "Share water, at " + KingdomPresentation.Rich(System.SeatName),
						Intro: "The stores hold {{C|" + stored + " drams}}. " + KingdomPresentation.Rich(System.SeatName) + " holds with {{C|"
							+ KingdomCreed.CreedName(realmCreed) + "}}.\n\nYou are asking one person, and you are asking them once.",
						Options: options,
						AllowEscape: true);
					if (picked < 0 || picked >= offers.Count)
					{
						return;
					}
					RiteOffer chosen = offers[picked];
					if (chosen.Bar != WaterRiteBar.Ready)
					{
						Popup.Show(KingdomWaterRiteRules.BarLine(chosen.Bar,
							KingdomPresentation.Rich(NameOf(people[picked])),
							KingdomCreed.CreedName(realmCreed), chosen.Drams, stored));
						continue;
					}
					Hold(System, zone, people[picked], realmCreed, chosen);
					return;
				}
			});
		}

		// ==================================================================================
		// The attended pass: shared living, and nothing else
		// ==================================================================================

		/// <summary>
		/// The kingdom's one attended pass over shared living: counts one pass of it for every
		/// citizen standing here.
		/// <para>
		/// Preconditions: called from the settlement pass, on claimed ground, beside
		/// <c>KingdomLodging.OnSettlementPass</c>. Side effects: advances
		/// <see cref="SharedDaysProperty"/> by the whole days each citizen has lived here since
		/// they were last counted (<c>KingdomWaterRiteRules.SharedDaysAfter</c>), and registers
		/// this channel's pressure source if a rebuild dropped it. Failure mode: returns having
		/// done nothing.
		/// </para>
		/// <para>
		/// Days pass here whether or not the founder does (Addendum 8 clause 1): a settler goes on
		/// living in the settlement while nobody is watching, and pretending otherwise made a
		/// founder who came home every third day the only founder whose people ever settled in.
		/// Nothing irreversible rides on it &mdash; shared living buys REACH, and reach only makes
		/// an invitation the founder must still extend and the settler must still accept more
		/// likely to be accepted &mdash; so this counter carries no brink of its own.
		/// </para>
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || System.ClaimedZones == null
				|| Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			// Two jobs, two gates, ONE enumeration of the ground. Lane 1 of Addendum 13 (the water
			// ritual with citizens) stands on its own option -- whether the founder's settlers will
			// share water with them on Qud's terms has nothing to do with whether this mod's
			// inward rite of belief is switched on -- but it walks the same citizens under the same
			// filter this counter already walks, and a second Z.GetObjects() a pass for a step that
			// is a no-op after the first pass is a cost with nothing behind it.
			bool shared = Enabled;
			KingdomCitizenRite.RiteTally rite = KingdomCitizenRite.Begin(System, Z);
			if (!shared && rite == null)
			{
				return;
			}
			if (shared)
			{
				Register();
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			for (int i = 0; i < Survey.CitizenBodies.Count; i++)
			{
				GameObject item = Survey.CitizenBodies[i];
				if (shared)
				{
					// Counted BEFORE the rite observes them, so a settler who crossed into a
					// different greeting this pass is greeted with the one they have earned.
					AdvanceSharedDays(item, now);
				}
				KingdomCitizenRite.Observe(rite, System, item);
			}
			KingdomCitizenRite.Close(System, rite);
		}

		/// <summary>One settler's share of this pass's shared living. Side effects: advances
		/// <see cref="SharedDayTickProperty"/> and <see cref="SharedDaysProperty"/> by the whole
		/// days since they were last counted. Failure mode: returns having done nothing.</summary>
		private static void AdvanceSharedDays(GameObject citizen, long now)
		{
			long last = citizen.GetLongProperty(SharedDayTickProperty);
			if (last <= 0L || now <= 0L)
			{
				// Planted before the first count, never read as elapsed: an unplanted stamp
				// resolved against an uncapped clock is the age of the world, and a newcomer
				// would arrive having already lived here a lifetime.
				citizen.SetLongProperty(SharedDayTickProperty, now);
				return;
			}
			int days = KingdomRules.ElapsedDays(now - last);
			if (days <= 0)
			{
				return;
			}
			// Advanced by exactly the days credited, so the part-day counts toward the next one
			// and a founder who steps out of the zone and back in buys nobody a free day.
			citizen.SetLongProperty(SharedDayTickProperty, KingdomRules.AdvanceCheckpoint(last, now));
			citizen.SetIntProperty(SharedDaysProperty, KingdomWaterRiteRules.SharedDaysAfter(citizen.GetIntProperty(SharedDaysProperty), days));
		}

		/// <summary>Cohabited days this settler has lived here. Zero for anybody the pass has not
		/// reached yet, which is the ordinary state of a newcomer.</summary>
		public static int SharedDaysOf(GameObject Resident)
		{
			return (Resident == null) ? 0 : Resident.GetIntProperty(SharedDaysProperty);
		}

		/// <summary>The line <c>kingdom:dump</c> appends for the zone the founder is standing in:
		/// how much of this settlement's life the people standing here have lived, and who has been
		/// asked once too often.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || Z == null)
			{
				return "";
			}
			int here = 0;
			int total = 0;
			List<string> closed = new List<string>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (!KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				here++;
				total += SharedDaysOf(item);
				string creed = item.GetStringProperty(AskedTooOftenCreedProperty);
				if (!string.IsNullOrEmpty(creed))
				{
					closed.Add(KingdomPresentation.Rich(NameOf(item)) + " (" + creed + ")");
				}
			}
			if (here == 0)
			{
				return "";
			}
			string line = "\nShared living: " + total + " passes over " + here + " here (cap "
				+ KingdomWaterRiteRules.MaxCountedDays + " each)";
			if (closed.Count > 0)
			{
				line += "  asked too often: " + string.Join(", ", closed);
			}
			return line;
		}

		// ==================================================================================
		// Holding the rite
		// ==================================================================================

		private static void Hold(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, RiteOffer Offer)
		{
			string name = NameOf(Resident);
			string shownName = KingdomPresentation.Rich(name);
			bool closing = KingdomWaterRiteRules.AskedTooOften(Resident.GetIntProperty(RefusalsProperty));
			bool takesTheRoad = KingdomConversionRules.Resents(Offer.Facts.Hostility);
			string prompt = KingdomWaterRiteRules.OfferPrompt(
				shownName,
				KingdomCreed.CreedName(Resident.GetStringProperty(KingdomCreed.CreedProperty)),
				KingdomCreed.CreedName(RealmCreed),
				KingdomPresentation.Rich(System.SeatName),
				Offer.Drams);
			if (closing)
			{
				prompt += KingdomWaterRiteRules.PressedWarning(shownName, takesTheRoad);
			}
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes)
			{
				return;
			}
			WaterRiteAnswer answer = closing
				? default(WaterRiteAnswer)
				: KingdomWaterRiteRules.Answer(Offer.Facts);
			// One survey binds one exact receipt to the dedicated vessels that are standing here
			// now. A stale row in the picker may say the water existed; only this reservation is
			// permission to hold the rite.
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(Offer.Drams, out debit))
			{
				Popup.Show("The rite requires exactly {{C|" + Offer.Drams
					+ " drams}} from the dedicated stores, and they cannot provide it.");
				return;
			}
			// Last safe point before any answer, stamp or cadence changes. Commit itself is
			// all-or-nothing; a failed commit restores every receipt-bound vessel.
			if (!debit.Commit())
			{
				Popup.Show("The dedicated stores could not yield exactly {{C|" + Offer.Drams
					+ " drams}}. No rite was held.");
				return;
			}
			if (closing)
			{
				Close(System, Resident, RealmCreed, name, shownName, takesTheRoad);
				System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
				return;
			}
			if (KingdomWaterRiteRules.Converted(answer))
			{
				if (!Accept(System, Z, Resident, RealmCreed, shownName))
				{
					bool returned = debit.Rollback();
					if (!returned)
					{
						MetricsManager.LogError("ThousandAndFirst water rite: conversion failed and the exact "
							+ Offer.Drams + "-dram debit could not be restored: " + (debit.Failure ?? "unknown failure"));
					}
					Popup.Show(returned
						? "The rite did not take hold. Exactly {{C|" + Offer.Drams + " drams}} were returned to the same stores."
						: "The rite did not take hold, and the stores could not be restored exactly. See the game log.");
					return;
				}
				LogRite(name, answer, Offer);
				System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
				return;
			}
			Refuse(System, Resident, RealmCreed, Offer, answer, shownName);
			LogRite(name, answer, Offer);
			System.LastSoulRiteTick = (The.Game != null) ? The.Game.TimeTicks : 0L;
		}

		private static void LogRite(string Name, WaterRiteAnswer Answer, RiteOffer Offer)
		{
			KingdomLog.Log("water rite: " + Name + " answer=" + Answer
				+ " distance=" + KingdomWaterRiteRules.Distance(Offer.Facts)
				+ " reach=" + KingdomWaterRiteRules.Reach(Offer.Facts.SharedDays)
				+ " poured=" + Offer.Drams);
		}

		private static bool Accept(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, string Name)
		{
			// One path for every conversion in the mod: the tally moves, both registers are
			// written in this channel's own words, and the ledger is noted -- all of it there, none
			// of it here, so no two channels can ever tell a conversion differently.
			if (!KingdomConversion.Convert(System, Z, Resident, RealmCreed,
				ConversionChannel.Diplomacy, "share water rite"))
			{
				return false;
			}
			ClearStamp(Resident);
			Resident.SetIntProperty(RefusalsProperty, 0, RemoveIfZero: true);
			Resident.SetStringProperty(AskedTooOftenCreedProperty, null, RemoveIfNull: true);
			Popup.Show(KingdomWaterRiteRules.AcceptNotice(Name, KingdomCreed.CreedName(RealmCreed)));
			return true;
		}

		private static void Refuse(KingdomSystem System, GameObject Resident, string RealmCreed, RiteOffer Offer, WaterRiteAnswer Answer, string Name)
		{
			WriteStamp(Resident, KingdomWaterRiteRules.StampFor(Offer.Facts, Answer));
			KingdomGovernanceScope.Commit("share water rite");
			Resident.SetIntProperty(RefusalsProperty, KingdomWaterRiteRules.RefusalsAfter(Resident.GetIntProperty(RefusalsProperty)));
			Chronicle(System,
				Offer.Facts.Hostility,
				KingdomWaterRiteRules.RefusalTelling(Answer, Name, KingdomPresentation.Rich(System.SeatName)),
				KingdomWaterRiteRules.RefusalRumour(Name, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			Popup.Show(KingdomWaterRiteRules.RefusalNotice(
				Answer,
				Name,
				KingdomCreed.CreedName(Resident.GetStringProperty(KingdomCreed.CreedProperty)),
				KingdomCreed.CreedName(RealmCreed),
				KingdomCreed.CreedName(Offer.ShrineCreed)));
		}

		// The asking that went one too far. The mark shuts the rite for as long as the realm holds
		// this creed, and RepeatedAsking reports it to KingdomConversion, whose own machinery
		// decides whether this person minds enough to take the road -- and, if they do, names them,
		// graces them and chronicles them exactly as every other resented creed is.
		private static void Close(KingdomSystem System, GameObject Resident, string RealmCreed,
			string Name, string ShownName, bool TakesTheRoad)
		{
			Resident.SetStringProperty(AskedTooOftenCreedProperty, RealmCreed);
			KingdomGovernanceScope.Commit("share water rite");
			Chronicle(System,
				KingdomConversionRules.ContestedHostility,
				KingdomWaterRiteRules.ClosedTelling(ShownName, KingdomPresentation.Rich(System.SeatName)),
				KingdomWaterRiteRules.ClosedRumour(ShownName, KingdomPresentation.Rich(System.SeatName), KingdomPresentation.Rich(KingdomChronicle.FounderName())));
			System.Ledger.Note("{{r|" + KingdomWaterRiteRules.ClosedNote(ShownName,
				KingdomCreed.CreedName(RealmCreed)) + "}}");
			Popup.Show(KingdomWaterRiteRules.ClosedNotice(ShownName,
				KingdomPresentation.Rich(System.SeatName), TakesTheRoad));
			KingdomLog.Log("water rite: " + Name + " asked too often about " + (RealmCreed ?? "-") + " road=" + TakesTheRoad);
		}

		// The two registers disagree where the day is contested, and agree where it is not, by
		// exactly the rule KingdomConversion applies to a conversion. A settler who holds nothing
		// in particular saying no is not a thing the roads argue about.
		private static void Chronicle(KingdomSystem System, int Hostility, string Telling, string Rumour)
		{
			if (KingdomConversionRules.Contested(Hostility))
			{
				KingdomChronicle.RecordDisputed(System, Telling, Rumour);
				return;
			}
			KingdomChronicle.Record(System, Telling);
		}

		// ==================================================================================
		// The facts, gathered off real people and real buildings
		// ==================================================================================

		// Everything one row of the Charter's list needs, decided once so the label, the refusal
		// line and the rite itself can never disagree about the same person.
		private sealed class RiteOffer
		{
			public WaterRiteBar Bar;

			public int Drams;

			public WaterRiteFacts Facts;

			public string ShrineCreed;
		}

		private static RiteOffer OfferFor(KingdomSystem System, Zone Z, GameObject Resident, string RealmCreed, int Stored)
		{
			RiteOffer offer = new RiteOffer();
			string shrineCreed;
			offer.Facts = FactsFor(Z, Resident, RealmCreed, out shrineCreed);
			offer.ShrineCreed = shrineCreed;
			offer.Drams = KingdomWaterRiteRules.Cost(KingdomWaterRiteRules.Distance(offer.Facts));
			offer.Bar = BarFor(System, Resident, offer.Facts, offer.Drams, Stored);
			return offer;
		}

		private static WaterRiteFacts FactsFor(Zone Z, GameObject Resident, string RealmCreed, out string ShrineCreed)
		{
			string theirs = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			QolProfile profile = KingdomQol.ProfileOf(Resident);
			// One vocabulary, and no new tag: a creature whose Refuses names the faith tag will not
			// have belief put to them by anybody, exactly as an authored Refuses is absolute at
			// every closeness rung; one whose Prefers names it is somebody for whom belief is a
			// thing they think about, and so not a thing they trade over a bowl. A mod ships an
			// unconvertible zealot by writing r_TAF_Refuses="taf:faith" on a blueprint, and needs
			// nothing from this file to do it.
			string faith = KingdomCeremonyRules.CategoryTag("faith");
			ShrineCreed = RivalShrineNear(Z, Resident, RealmCreed);
			return new WaterRiteFacts(
				KingdomCreed.HostilityBetween(theirs, RealmCreed),
				SharedDaysOf(Resident),
				!string.IsNullOrEmpty(theirs),
				!string.IsNullOrEmpty(ShrineCreed),
				KingdomQolRules.Has(profile.Prefers, faith),
				KingdomQolRules.Has(profile.Refuses, faith),
				RealmCreed);
		}

		private static WaterRiteBar BarFor(KingdomSystem System, GameObject Resident, WaterRiteFacts Facts, int Drams, int Stored)
		{
			if (KingdomWaterRiteRules.SameCreed(Resident.GetStringProperty(KingdomCreed.CreedProperty), Facts.RealmCreed))
			{
				return WaterRiteBar.NothingBetweenYou;
			}
			if (Simulation.City.KingdomResidents.IdOf(Resident) > 0
				&& Simulation.City.KingdomResidents.IdOf(Resident)
					== System.OfficeHolderResidentId)
			{
				return WaterRiteBar.TheirOffice;
			}
			if (!CouldWalkAway(System, Resident))
			{
				return WaterRiteBar.NoRoadOut;
			}
			string closed = Resident.GetStringProperty(AskedTooOftenCreedProperty);
			if (!string.IsNullOrEmpty(closed) && KingdomWaterRiteRules.SameCreed(closed, Facts.RealmCreed))
			{
				return WaterRiteBar.AskedTooOften;
			}
			WaterRiteStamp stamp;
			if (TryReadStamp(Resident, out stamp) && !KingdomWaterRiteRules.SomethingChanged(stamp, Facts))
			{
				return WaterRiteBar.AlreadyAnswered;
			}
			// The same cadence, from the same constant, as the rite of shared water between two
			// cities: one definition of "you poured too recently", never two that can drift.
			if (!KingdomCreedRules.RiteReady(System.LastSoulRiteTick, (The.Game != null) ? The.Game.TimeTicks : 0L))
			{
				return WaterRiteBar.PouredTooRecently;
			}
			return (Stored < Drams) ? WaterRiteBar.StoresCannotBear : WaterRiteBar.Ready;
		}

		// A yes from somebody with nowhere to go is not a yes, so the rite is only ever put to
		// somebody the settlement's own emigration machinery would actually take: one of its own
		// arrivals, and not the last of the loyal core. KingdomGrowth.Emigrate's own conditions,
		// asked before the question rather than discovered after it.
		private static bool CouldWalkAway(KingdomSystem System, GameObject Resident)
		{
			if (Resident.GetIntProperty("KingdomBorn") != 1 || Resident.IsPlayer() || Resident.IsPlayerLed())
			{
				return false;
			}
			return System.Population > KingdomRules.LoyalCoreSettlers;
		}

		// A shrine consecrated to anything other than the realm's creed, standing within sight of
		// this settler's own door. Read off KingdomFaith's own property rather than a second name
		// for the same fact, so a settlement with the faith module switched off simply has no
		// object carrying it and this reads false.
		private static string RivalShrineNear(Zone Z, GameObject Resident, string RealmCreed)
		{
			Cell door = DoorOf(Z, Resident);
			if (Z == null || door == null)
			{
				return null;
			}
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				string consecrated = item.GetStringProperty(KingdomFaith.ShrineCreedProperty);
				if (string.IsNullOrEmpty(consecrated) || KingdomWaterRiteRules.SameCreed(consecrated, RealmCreed))
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell != null && KingdomWaterRiteRules.WithinQuarter(cell.X - door.X, cell.Y - door.Y))
				{
					return consecrated;
				}
			}
			return null;
		}

		// Their own door, which is the only reading of "their quarter" the code can honestly make
		// (Addendum 4d: quarters emerge from the layout grammar and no code knows the word). A
		// settler with no home is judged from where they are standing, which for somebody sleeping
		// in the open is the same thing.
		private static Cell DoorOf(Zone Z, GameObject Resident)
		{
			if (Z == null || Resident == null)
			{
				return null;
			}
			string plotId = Resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (!string.IsNullOrEmpty(plotId))
			{
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				{
					if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1 && item.GetStringProperty(KingdomPlots.PlotIdProperty) == plotId)
					{
						Cell home = item.CurrentCell;
						if (home != null)
						{
							return home;
						}
					}
				}
			}
			return Resident.CurrentCell;
		}

		private static string RealmCreed(KingdomSystem System)
		{
			return string.IsNullOrEmpty(System.DeclaredCreed) ? KingdomCreed.SeatCreed(System) : System.DeclaredCreed;
		}

		// ==================================================================================
		// The stamp a refusal leaves
		// ==================================================================================

		private static void WriteStamp(GameObject Resident, WaterRiteStamp Stamp)
		{
			Resident.SetIntProperty(StampAnswerProperty, (int)Stamp.Answer + 1);
			Resident.SetIntProperty(StampHostilityProperty, Stamp.Hostility);
			Resident.SetIntProperty(StampShrineProperty, Stamp.RivalShrine ? 1 : 0);
			Resident.SetIntProperty(StampAbsoluteProperty, Stamp.Absolute ? 1 : 0);
			Resident.SetIntProperty(StampNeededProperty, Stamp.NeededDays);
			Resident.SetStringProperty(StampCreedProperty, Stamp.RealmCreed ?? "");
		}

		private static bool TryReadStamp(GameObject Resident, out WaterRiteStamp Stamp)
		{
			int answer = Resident.GetIntProperty(StampAnswerProperty);
			if (answer <= 0)
			{
				Stamp = default(WaterRiteStamp);
				return false;
			}
			Stamp = new WaterRiteStamp(
				(WaterRiteAnswer)(answer - 1),
				Resident.GetIntProperty(StampHostilityProperty),
				Resident.GetIntProperty(StampShrineProperty) == 1,
				Resident.GetIntProperty(StampAbsoluteProperty) == 1,
				Resident.GetIntProperty(StampNeededProperty),
				Resident.GetStringProperty(StampCreedProperty));
			return true;
		}

		private static void ClearStamp(GameObject Resident)
		{
			Resident.SetIntProperty(StampAnswerProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampHostilityProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampShrineProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampAbsoluteProperty, 0, RemoveIfZero: true);
			Resident.SetIntProperty(StampNeededProperty, 0, RemoveIfZero: true);
			Resident.SetStringProperty(StampCreedProperty, null, RemoveIfNull: true);
		}

		// ==================================================================================
		// People
		// ==================================================================================

		// Everyone the rite could be put to: a citizen of this settlement whom the roll carries
		// under a name, because water is shared with a person and a person has a name. Sorted by
		// that name, so the same settlement always offers the same list in the same order.
		private static List<GameObject> CandidatesIn(KingdomSystem System, Zone Z)
		{
			List<GameObject> people = new List<GameObject>();
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (KingdomCitizenship.BelongsTo(System, item)
					&& !string.IsNullOrEmpty(item.GetStringProperty("KingdomName")))
				{
					people.Add(item);
				}
			}
			people.Sort((a, b) => string.CompareOrdinal(a.GetStringProperty("KingdomName"), b.GetStringProperty("KingdomName")));
			return people;
		}

		private static string NameOf(GameObject Resident)
		{
			if (Resident == null)
			{
				return "";
			}
			string name = Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? Resident.BaseDisplayNameStripped : name;
		}
	}
}
