using System.Collections.Generic;
using Qud.API;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool TryResolveAdvisor(Zone Zone, KingdomQuickstartProfile Profile,
			KingdomQuickstartReceipt Receipt, out GameObject Advisor,
			out KingdomQuickstartAdvisorDisposition Disposition, out string Failure)
		{
			Advisor = null;
			Disposition = KingdomQuickstartAdvisorDisposition.Unresolved;
			Failure = "";
			if (!TryObserveGrant(Zone, Receipt, KingdomQuickstartPhase.AdvisorResolved,
				KingdomQuickstartRules.AdvisorCellX, KingdomQuickstartRules.AdvisorCellY, false,
				out GameObject existing, out KingdomQuickstartGrantObservation observation,
				out Failure)) return false;
			KingdomQuickstartRecoveryAction action = KingdomQuickstartRules.RecoveryAction(
				Receipt.Phase, KingdomQuickstartPhase.AdvisorResolved, observation);
			if (action == KingdomQuickstartRecoveryAction.PublishExisting)
			{
				if (!VerifyAdvisor(Zone, existing, Receipt, out Failure)) return false;
				Advisor = existing;
				Disposition = KingdomQuickstartAdvisorDisposition.Included;
				return true;
			}
			if (action != KingdomQuickstartRecoveryAction.PreparePlaceAndPublish)
			{
				Failure = "The camp-advisor recovery boundary was not lawful.";
				return false;
			}
			bool include = Options.GetOption(KingdomQuickstartRules.AdvisorOption, "Yes")
				!= "No";
			if (!include)
			{
				Disposition = KingdomQuickstartAdvisorDisposition.Omitted;
				return true;
			}
			Advisor = CreateAdvisor(Zone, Profile, Receipt, out Failure);
			if (!VerifyAdvisor(Zone, Advisor, Receipt, out Failure)) return false;
			Disposition = KingdomQuickstartAdvisorDisposition.Included;
			return true;
		}

		private static GameObject CreateAdvisor(Zone Zone, KingdomQuickstartProfile Profile,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			GameObject advisor = GameObject.Create("NPC");
			if (!GameObject.Validate(advisor) || advisor.Inventory == null) return advisor;
			List<GameObject> inventory = new List<GameObject>(advisor.Inventory.Objects);
			for (int i = 0; i < inventory.Count; i++) inventory[i].Obliterate(null, Silent: true);
			if (advisor.Inventory.Objects.Count != 0) return advisor;

			advisor.GiveProperName(AdvisorName(Profile), Force: true);
			if (advisor.Render != null)
			{
				advisor.Render.Tile = "Assets_Content_Textures_Creatures_sw_farmer.bmp";
				advisor.Render.ColorString = "&y";
				advisor.Render.DetailColor = "w";
			}
			Description description = advisor.GetPart<Description>();
			if (description != null) description.Short = "A quiet wayfarer keeps the first "
				+ "inventory in charcoal, careful never to mistake a store for a spring.";
			advisor.SetIntProperty("NoXP", 1);
			advisor.SetIntProperty("SuppressCorpseDrops", 1);
			advisor.RequirePart<NoXPGain>();
			Commerce commerce = advisor.GetPart<Commerce>();
			if (commerce != null) commerce.Value = 0.0;
			Corpse corpse = advisor.GetPart<Corpse>();
			if (corpse != null)
			{
				corpse.CorpseChance = 0;
				corpse.BurntCorpseChance = 0;
				corpse.VaporizedCorpseChance = 0;
				corpse.BuildCorpseChance = 0;
			}
			Brain brain = advisor.Brain;
			if (brain == null) return advisor;
			brain.Allegiance = new AllegianceSet();
			brain.Allegiance.Calm = true;
			brain.Allegiance.Hostile = false;
			brain.Passive = true;
			brain.Mobile = false;
			brain.Staying = true;
			brain.Wanders = false;
			brain.WandersRandomly = false;
			brain.DoReequip = false;
			brain.PartyLeader = null;
			ConversationsAPI.addSimpleConversationToObject(advisor,
				"Count what is here, founder, not what you wish were here. The casks hold "
				+ "twenty-four drams and the larder twelve meals. They make nothing. "
				+ "Raise shelter, then give hands and ground to the works that gather food "
				+ "and water; only such work replaces what the city spends.",
				"Live and drink.");
			if (!TryPrepareGrant(advisor, Receipt,
				KingdomQuickstartPhase.AdvisorResolved, out Failure)
				|| !TryPlaceGrant(Zone, advisor, KingdomQuickstartRules.AdvisorCellX,
					KingdomQuickstartRules.AdvisorCellY, out Failure)) return null;
			return advisor;
		}

		private static bool VerifyAdvisor(Zone Zone, GameObject Advisor,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			Brain brain = Advisor?.Brain;
			Corpse corpse = Advisor?.GetPart<Corpse>();
			Commerce commerce = Advisor?.GetPart<Commerce>();
			if (!ExactRole(Zone, Advisor, "NPC", KingdomQuickstartRules.AdvisorCellX,
				KingdomQuickstartRules.AdvisorCellY)
				|| !ExactGrantMarker(Advisor, Receipt,
					KingdomQuickstartPhase.AdvisorResolved)
				|| (Receipt.Phase >= KingdomQuickstartPhase.AdvisorResolved
					&& !ReceiptOwns(Advisor, Receipt.AdvisorObjectId))
				|| Advisor.Inventory == null || Advisor.Inventory.Objects.Count != 0
				|| Advisor.GetIntProperty("NoXP") != 1
				|| Advisor.GetIntProperty("SuppressCorpseDrops") != 1
				|| Advisor.GetPart<NoXPGain>() == null
				|| Advisor.GetIntProperty("KingdomCitizen") != 0
				|| Advisor.GetIntProperty("KingdomBorn") != 0
				|| Advisor.GetIntProperty("KingdomBuilt") != 0
				|| Advisor.GetIntProperty("KingdomStaffNeeded") != 0
				|| Advisor.GetIntProperty("KingdomDefence") != 0
				|| brain == null || !brain.Passive || brain.Mobile || !brain.Staying
				|| brain.Wanders || brain.WandersRandomly || brain.DoReequip
				|| brain.PartyLeader != null || !brain.Allegiance.Calm
				|| brain.Allegiance.Hostile || commerce == null || commerce.Value != 0.0
				|| corpse == null || corpse.CorpseChance != 0
				|| corpse.BurntCorpseChance != 0 || corpse.VaporizedCorpseChance != 0
				|| corpse.BuildCorpseChance != 0)
			{
				Failure = "The optional advisor was not an inert, benefit-free guide.";
				return false;
			}
			return true;
		}

		private static string AdvisorName(KingdomQuickstartProfile Profile)
		{
			if (Profile == null) return "the camp guide";
			if (Profile.Key == "marsh") return "Reed-at-Dawn";
			if (Profile.Key == "canyon") return "Shale-of-Rifts";
			return "Salt-at-Noon";
		}
	}
}
