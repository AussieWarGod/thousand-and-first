using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The founding social act, turned on the founder's own people: a settler of the realm can
	/// share water with you, through Qud's own water ritual, for the realm's own faction.
	/// <para>
	/// BUILDING-CATALOGUE-BRIEF Addendum 13, lane 1. <b>This file builds no ritual.</b> Vanilla's
	/// ritual is gated on exactly one thing &mdash; the speaker carrying <c>GivesRep</c>, which is
	/// what <c>WaterRitualChoice</c> tests (<c>B/Conversations.xml:6</c>,
	/// <c>D/XRL/World/Conversations/ConversationDelegates.cs:711-712</c>) &mdash; plus a
	/// conversation to host the choice, which any conversation built through
	/// <c>ConversationsAPI</c> inherits from <c>BaseConversation</c> through the dynamic shim
	/// (<c>D/Qud/API/ConversationsAPI.cs:77-84</c>). Both are two lines. Everything the rite then
	/// gives comes off the runtime faction the founding already mints.
	/// </para>
	/// <para>
	/// <b>What it completes.</b> <c>KingdomDish</c> stamps the realm's favoured dish onto
	/// <c>Faction.WaterRitualRecipe</c>, where vanilla's <c>WaterRitualCookingRecipe</c> reads it
	/// (<c>D/XRL/World/Conversations/Parts/WaterRitualCookingRecipe.cs:49-55</c>). Until this,
	/// nothing alive belonged to that faction and could be shared water with &mdash; so the
	/// settlement's own dish was a recipe no creature in Qud could ever hand over. The basin's
	/// fiction, finished.
	/// </para>
	/// <para>
	/// <b>The protection law holds.</b> Nothing is consumed, moved or destroyed: a settler gains a
	/// part and, only if they had no conversation at all, a greeting. A settler another mod gave a
	/// conversation to keeps it &mdash; an XML conversation already inherits
	/// <c>BaseConversation</c> and already carries the ritual choice
	/// (<c>D/XRL/World/Conversations/ConversationLoader.cs:46-49</c>), so replacing it would take
	/// away somebody's content to add something that is already there.
	/// </para>
	/// <para>
	/// <b>It owns no loop.</b> Every citizen on the ground is walked once per settlement pass, by
	/// <c>KingdomWaterRite.OnSettlementPass</c>, which was already walking them for the shared-days
	/// counter this rite's greeting reads. A second enumeration of the same zone for the same
	/// filter would be a per-pass cost for a step that is a no-op after the first pass.
	/// </para>
	/// </summary>
	public static class KingdomCitizenRite
	{
		/// <summary>Its own gate, and not the inward rite's: sharing water with a settler on Qud's
		/// terms has nothing to do with whether this mod's rite of belief is switched on.</summary>
		public static bool Enabled
		{
			get { return Options.GetOption("r_TAF_OptionCitizenRite", "Yes") != "No"; }
		}

		/// <summary>Set once the settler has been made a host. Read by the city book's people
		/// chapter, and by nothing that decides anything &mdash; the decision always asks the
		/// object's actual state.</summary>
		public const string HostProperty = "KingdomRiteHost";

		/// <summary>Set on a settler whose conversation is ours, so a later pass may rebuild that
		/// one and will never touch anybody else's.</summary>
		public const string ConversationProperty = "KingdomRiteConversation";

		/// <summary>Which greeting is currently stamped, plus one, so zero means "none stamped".
		/// A settler who crosses into a different band gets the greeting they have earned instead
		/// of the one they arrived with.</summary>
		public const string GreetingBandProperty = "KingdomRiteGreeting";

		/// <summary>
		/// What one pass over one zone's citizens found. Carried across the loop so the rite can
		/// speak once about the ground rather than once per settler.
		/// </summary>
		public sealed class RiteTally
		{
			internal int Hosts;

			internal int Citizens;

			internal CitizenRiteVerdict Worst;

			internal string Liquid;
		}

		/// <summary>
		/// Opens a tally for this pass, or null when the rite has nothing to do here.
		/// <para>
		/// Preconditions: none. Side effects: none. Failure mode: returns null, and the caller
		/// skips <see cref="Observe"/> and <see cref="Close"/> entirely.
		/// </para>
		/// </summary>
		public static RiteTally Begin(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || System.ClaimedZones == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return null;
			}
			return new RiteTally();
		}

		/// <summary>
		/// Makes one citizen a host of the rite, and records what happened in the tally.
		/// <para>
		/// Preconditions: <paramref name="Tally"/> from <see cref="Begin"/> on this ground.
		/// Side effects: may add <c>GivesRep</c>, may add or rebuild a greeting that is ours.
		/// Failure mode: records the verdict that stopped it, having changed nothing.
		/// </para>
		/// <para>
		/// Re-checked rather than remembered: the condition asked is the ACTUAL state of the
		/// object, so a part lost to a mod change, a save round-trip or a rebuild is repaired on
		/// the next pass instead of being remembered as done.
		/// </para>
		/// </summary>
		public static void Observe(RiteTally Tally, KingdomSystem System, GameObject Citizen)
		{
			if (Tally == null)
			{
				return;
			}
			Tally.Citizens++;
			string liquid;
			CitizenRiteVerdict verdict = Host(System, Citizen, out liquid);
			if (verdict == CitizenRiteVerdict.Host)
			{
				Tally.Hosts++;
				return;
			}
			if (verdict > Tally.Worst)
			{
				Tally.Worst = verdict;
				Tally.Liquid = liquid;
			}
		}

		/// <summary>
		/// Says the one thing this pass has to say about the rite, if it has anything.
		/// <para>
		/// Preconditions: <paramref name="Tally"/> may be null. Side effects: at most one ledger
		/// note, and only when the block is new. Failure mode: silence.
		/// </para>
		/// <para>
		/// <b>It speaks only when NOBODY on this ground can host.</b> STANDARDS &sect;7b asks for
		/// the reason a thing did not happen, and "its people will not share water" is false of the
		/// forty-nine settlers who can if one of fifty cannot.
		/// </para>
		/// </summary>
		public static void Close(KingdomSystem System, RiteTally Tally)
		{
			if (Tally == null || System == null || System.City == null)
			{
				return;
			}
			Chronicle(System);
			string line = (Tally.Hosts > 0 || Tally.Citizens <= 0)
				? ""
				: KingdomCitizenRiteRules.BlockedLine(Tally.Worst, KingdomPresentation.Rich(System.SeatName), Tally.Liquid);
			if (string.IsNullOrEmpty(line))
			{
				// The block lifted, or there never was one. Clearing the flag is what makes the
				// "once" a once-per-block rather than a once-per-save.
				System.City.RiteBlocked = 0;
				return;
			}
			if (System.City.RiteBlocked == (int)Tally.Worst + 1)
			{
				return;
			}
			System.City.RiteBlocked = (int)Tally.Worst + 1;
			System.Ledger.Note("{{r|" + line + "}}");
			KingdomLog.Log("citizen rite blocked: " + Tally.Worst + " liquid=" + (Tally.Liquid ?? "-"));
		}

		/// <summary>
		/// Files what the roads are saying about the city as a secret the founder can trade.
		/// <para>
		/// <b>W5's named remainder, and the narrowest honest slice of it.</b> This builds no trade
		/// system, no new conversation, and no new interest table. It writes ONE
		/// <c>JournalObservation</c> per settlement pass and hands it to vanilla, which already
		/// owns every part of what happens next: <c>IWaterRitualSecretPart.ShuffleNotes</c> walks
		/// <c>JournalAPI.Observations</c> into the ritual's ball bag,
		/// <c>Faction.GetBuySecretWeight</c> asks each faction's own
		/// <c>&lt;interest Tags="..."/&gt;</c> table whether it wants to hear it, and
		/// <c>WaterRitualSellSecret.SellEntry</c> pays the reputation and stamps the entry
		/// <i>shared with</i> whoever bought it
		/// (<c>D/XRL/World/Conversations/Parts/IWaterRitualSecretPart.cs</c>,
		/// <c>D/XRL/World/Faction.cs:1197-1280,1533</c>,
		/// <c>D/XRL/World/Conversations/Parts/WaterRitualSellSecret.cs</c>).
		/// </para>
		/// <para>
		/// <b>It is sellable and never buyable</b>, which falls out of vanilla's own rule rather
		/// than being enforced: <c>CanSell</c> is <c>Tradable &amp;&amp; Revealed</c> and
		/// <c>CanBuy</c> is <c>Tradable &amp;&amp; !Revealed</c>
		/// (<c>D/Qud/API/IBaseJournalEntry.cs:193-206</c>). A founder knows their own city's
		/// history the day it happens, so the entry is filed revealed — you can tell the world about
		/// your city, and nobody can sell it back to you.
		/// </para>
		/// <para>
		/// <b>Bounded and idempotent.</b> One entry per pass, the newest telling only; the id is
		/// derived from the realm and the words, so re-filing after a reload, a seat swap or a
		/// register trim is a no-op inside <c>JournalAPI.AddObservation</c>, which refuses an id it
		/// already holds. There is no cursor to fall out of step with the two-hundred-entry ring.
		/// </para>
		/// <para>
		/// Under the chronicle's own option, because it is the chronicle leaving the city.
		/// </para>
		/// </summary>
		private static void Chronicle(KingdomSystem System)
		{
			if (!System.Founded || System.OutsiderEntries == null || System.OutsiderEntries.Count == 0
				|| Options.GetOption("r_TAF_OptionChronicle") == "No")
			{
				return;
			}
			string id;
			string text;
			if (!KingdomCitizenRiteRules.TryTradableSecret(
					System.CurrentRealmId,
					System.OutsiderEntries[System.OutsiderEntries.Count - 1],
					out id,
					out text)
				|| JournalAPI.GetObservation(id) != null)
			{
				return;
			}
			JournalAPI.AddObservation(
				text,
				id,
				KingdomCitizenRiteRules.SecretCategory,
				id,
				KingdomCitizenRiteRules.SecretTags(),
				revealed: true,
				-1L);
			KingdomLog.Log("citizen rite: filed a telling of " + System.KingdomDisplayName + " as tradable gossip (" + id + ")");
		}

		/// <summary>
		/// Makes one citizen a host of the rite.
		/// <para>
		/// Preconditions: a founded realm. Side effects: as <see cref="Observe"/>. Failure mode:
		/// returns the verdict that stopped it, having changed nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Citizen">The settler.</param>
		/// <param name="Liquid">The ritual liquid that could not be poured, when the verdict is
		/// <see cref="CitizenRiteVerdict.UnknownLiquid"/>; null otherwise. Reported rather than
		/// re-derived, because the faction that was judged is the SETTLER'S base allegiance and
		/// need not be the seated realm's.</param>
		/// <returns>What the judgment was. <see cref="CitizenRiteVerdict.Host"/> means they are
		/// now one, whether or not this call is what made them one.</returns>
		public static CitizenRiteVerdict Host(KingdomSystem System, GameObject Citizen, out string Liquid)
		{
			Liquid = null;
			bool founded = System != null && System.Founded;
			bool citizen = Citizen != null && KingdomCitizenship.BelongsTo(System, Citizen)
				&& !Citizen.IsPlayer();
			bool body = Citizen != null && Citizen.Brain != null;
			string faction = (citizen && body) ? Citizen.GetPrimaryFaction(Base: true) : null;
			bool known = !string.IsNullOrEmpty(faction) && Factions.Exists(faction);
			string liquid = known ? Factions.Get(faction).WaterRitualLiquid : null;
			// An empty ritual liquid is safe and is NOT a refusal: Brain only layers the faction's
			// value over the event when it is non-empty (D/XRL/World/Parts/Brain.cs:2102-2109), so
			// the engine's own "water" default stands. Only a liquid that was NAMED and does not
			// exist is fatal.
			bool pourable = string.IsNullOrEmpty(liquid) || LiquidVolume.GetLiquid(liquid) != null;
			CitizenRiteVerdict verdict = KingdomCitizenRiteRules.Judge(founded, citizen, body, known, pourable);
			if (verdict != CitizenRiteVerdict.Host)
			{
				if (verdict == CitizenRiteVerdict.UnknownLiquid)
				{
					Liquid = liquid;
				}
				return verdict;
			}
			if (!Citizen.HasPart<GivesRep>())
			{
				GivesRep rep = Citizen.AddPart<GivesRep>();
				// The related-faction table is what carries the rite's secondary awards and its
				// hates (D/XRL/World/Conversations/Parts/WaterRitual.cs:174-209). Filled the way
				// the engine fills a village warden's (D/XRL/World/ZoneBuilders/VillageCoda.cs:558).
				rep.FillInRelatedFactions(Initial: true);
			}
			Speak(System, Citizen);
			if (Citizen.GetIntProperty(HostProperty) != 1)
			{
				Citizen.SetIntProperty(HostProperty, 1);
				KingdomLog.Log("citizen rite: " + (Citizen.GetStringProperty("KingdomName") ?? Citizen.ShortDisplayName)
					+ " hosts the rite for " + faction);
			}
			return CitizenRiteVerdict.Host;
		}

		/// <summary>How many of this ground's citizens the rite stands open on. Read by the city
		/// book's people chapter.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || Z == null)
			{
				return "";
			}
			int hosts = 0;
			int citizens = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				if (!KingdomCitizenship.BelongsTo(System, item))
				{
					continue;
				}
				citizens++;
				if (item.HasPart<GivesRep>())
				{
					hosts++;
				}
			}
			return (citizens == 0) ? "" : ("rite: " + hosts + " of " + citizens + " citizens here will share water");
		}

		/// <summary>
		/// Gives a settler with no conversation at all one, and keeps ours current as they settle
		/// in.
		/// <para>
		/// A conversation built here is a fixed string on the object. Stamped once on the pass that
		/// first saw them &mdash; when they have lived here no days at all &mdash; a settler would
		/// greet their founder as a newcomer for the rest of their life, and two thirds of the
		/// greetings would be unreachable. So the band is stamped beside it and re-read: crossing
		/// into a different one rebuilds OUR conversation, and only ever ours.
		/// </para>
		/// </summary>
		private static void Speak(KingdomSystem system, GameObject citizen)
		{
			int band = KingdomCitizenRiteRules.Band(KingdomWaterRite.SharedDaysOf(citizen));
			bool ours = citizen.GetIntProperty(ConversationProperty) == 1;
			bool none = !citizen.HasPart<ConversationScript>();
			if (!none && (!ours || citizen.GetIntProperty(GreetingBandProperty) == band + 1))
			{
				return;
			}
			ConversationsAPI.addSimpleConversationToObject(citizen,
				KingdomCitizenRiteRules.Greeting(KingdomPresentation.Rich(system.SeatName), KingdomWaterRite.SharedDaysOf(citizen)),
				KingdomCitizenRiteRules.Farewell());
			citizen.SetIntProperty(ConversationProperty, 1);
			citizen.SetIntProperty(GreetingBandProperty, band + 1);
		}
	}
}
