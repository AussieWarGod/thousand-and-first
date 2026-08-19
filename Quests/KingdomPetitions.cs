using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Petitions: what the settlement asks of its founder. Each one is generated from a
	/// condition the settlement is genuinely in, is spoken by a settler with a name, is
	/// answered by a change the player can see, and can always be refused. One at a time,
	/// never nagging, and silence when the settlement is content.
	/// </summary>
	public static class KingdomPetitions
	{
		public static bool Enabled => XRL.UI.Options.GetOption("r_TAF_OptionPetitions") != "No";

		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || !System.Founded)
			{
				return;
			}
			if (System.PetitionKind != KingdomRules.PetitionKind.None)
			{
				Check(System, Z, Survey);
				return;
			}
			if (The.Game.TimeTicks < System.LastPetitionTick + KingdomRules.PetitionCooldownTicks)
			{
				return;
			}
			Issue(System, Z, Survey);
		}

		public static void Issue(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			string worstFaction = null;
			int worstStanding = 0;
			foreach (System.Collections.Generic.KeyValuePair<string, int> standing in System.Standings)
			{
				if (standing.Value < worstStanding)
				{
					worstStanding = standing.Value;
					worstFaction = standing.Key;
				}
			}
			bool hasShrine = false;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Shrine"))
				{
					hasShrine = true;
					break;
				}
			}
			KingdomRules.PetitionKind kind = KingdomRules.ChoosePetition(Survey.StoredWater, System.Population, Survey.Beds, System.IdleWorks, worstStanding, hasShrine, System.Dead);
			if (kind == KingdomRules.PetitionKind.None)
			{
				return;
			}
			string petitioner = (System.RosterNames.Count > 0) ? System.RosterNames.GetRandomElement() : "a settler";
			System.PetitionKind = kind;
			System.PetitionPetitioner = petitioner;
			System.PetitionFaction = worstFaction;
			System.PetitionIssuedTick = The.Game.TimeTicks;
			System.PetitionTarget = ((kind == KingdomRules.PetitionKind.Thirst) ? KingdomRules.ThirstPetitionTarget(System.Population) : ((kind == KingdomRules.PetitionKind.Peace) ? (-100) : 0));
			System.Ledger.Note("{{W|" + petitioner + " is waiting to speak with you.}}");
			MessageQueue.AddPlayerMessage("{{W|" + petitioner + " would have a word with you about " + Subject(kind) + ".}}");
			if (KingdomLog.Enabled) KingdomLog.Log("petition issued: " + kind + " by " + petitioner + " target=" + System.PetitionTarget);
		}

		public static void Check(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			bool hasShrine = false;
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomBuilt") == 1 && item.HasPart("Shrine"))
				{
					hasShrine = true;
					break;
				}
			}
			int standing = (System.PetitionFaction != null) ? System.GetStanding(System.PetitionFaction) : 0;
			if (KingdomRules.IsPetitionMet(System.PetitionKind, System.PetitionTarget, Survey.StoredWater, System.Population, Survey.Beds, System.IdleWorks, standing, hasShrine))
			{
				Fulfil(System);
			}
			else if (The.Game.TimeTicks > System.PetitionIssuedTick + KingdomRules.PetitionLifetimeTicks)
			{
				System.Ledger.Note("{{K|" + System.PetitionPetitioner + " stopped asking. The matter was not pressed.}}");
				Close(System);
			}
		}

		public static void Fulfil(KingdomSystem System)
		{
			string deed = Deed(System.PetitionKind, System.KingdomDisplayName);
			KingdomChronicle.Record(System, System.PetitionPetitioner + " asked, and " + deed, Accomplishment: true);
			System.RecordDeed(deed);
			System.Ledger.Note("{{G|" + System.PetitionPetitioner + " has what they asked for. Word of it will travel.}}");
			MessageQueue.AddPlayerMessage("{{G|" + System.PetitionPetitioner + " thanks you. " + XRL.Language.Grammar.InitCap(deed) + ".}}");
			System.PetitionsMet++;
			Close(System);
		}

		public static void Close(KingdomSystem System)
		{
			System.PetitionKind = KingdomRules.PetitionKind.None;
			System.PetitionPetitioner = null;
			System.PetitionFaction = null;
			System.PetitionTarget = 0;
			System.LastPetitionTick = The.Game.TimeTicks;
		}

		public static string Subject(KingdomRules.PetitionKind Kind)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return "the water";
			case KingdomRules.PetitionKind.Shelter:
				return "where people are sleeping";
			case KingdomRules.PetitionKind.Craft:
				return "the works standing idle";
			case KingdomRules.PetitionKind.Peace:
				return "the ones who hate us";
			case KingdomRules.PetitionKind.Memorial:
				return "the dead";
			default:
				return "the settlement";
			}
		}

		/// <summary>What the petitioner actually says, in their own mouth.</summary>
		public static string Speech(KingdomSystem System)
		{
			switch (System.PetitionKind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return "\"We are counting drams again. I am not asking for plenty — I am asking for " + System.PetitionTarget + " in the stores, so that when the month turns dry we do not have to decide who drinks.\"";
			case KingdomRules.PetitionKind.Shelter:
				return "\"There are more of us than there are beds, and the newest sleep where they can. Raise another bunk. People will forgive a great deal, but not being made to feel like they are visiting.\"";
			case KingdomRules.PetitionKind.Craft:
				return "\"We built the works and then left them standing. Every day I walk past a thing we paid water for that nobody is turning. Either find us hands, or let me pull it down for the timber.\"";
			case KingdomRules.PetitionKind.Peace:
				return "\"" + ((System.PetitionFaction != null) ? XRL.World.Faction.GetFormattedName(System.PetitionFaction) : "They") + " will not hear us, and my people flinch at the road. I do not care how it is done — bought, begged, or drunk over. Just make it so they do not hate us.\"";
			case KingdomRules.PetitionKind.Memorial:
				return "\"We have buried people here now. There is nowhere to put a hand and say a name. Raise a shrine stone, and let the ground admit what it has taken.\"";
			default:
				return "\"It is nothing. It has passed.\"";
			}
		}

		public static string Deed(KingdomRules.PetitionKind Kind, string Name)
		{
			switch (Kind)
			{
			case KingdomRules.PetitionKind.Thirst:
				return "the stores of " + Name + " were filled against the dry month";
			case KingdomRules.PetitionKind.Shelter:
				return "a bed was raised for every soul in " + Name;
			case KingdomRules.PetitionKind.Craft:
				return "the works of " + Name + " were set turning again";
			case KingdomRules.PetitionKind.Peace:
				return "the peace " + Name + " made with its enemies";
			case KingdomRules.PetitionKind.Memorial:
				return "the shrine " + Name + " raised over its dead";
			default:
				return "the matter was settled at " + Name;
			}
		}
	}
}
