using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomGuestActionsNativeChecks
	{
		private static XRLGame Game;
		private static Zone Zone;
		private static KingdomSystem System;
		private static readonly List<GameObject> Owned = new List<GameObject>();
		private static string[] ExpectedOptions;
		private static string ExpectedTitle;
		private static int Pick;
		private static bool Quickstart;
		internal static bool ActionActive;
		internal static readonly List<GameObject> Founders = new List<GameObject>();
		internal static readonly StringBuilder Evidence = new StringBuilder();
		internal static bool Vacant => Game == null;
		private static void Require(bool value, string reason) => KingdomGuestActionsNativeProvider.Require(value, reason);

		internal static string Start(XRLGame game, Zone zone)
		{
			Require(Vacant, "guest actions already started");
			Game = game; Zone = zone;
			System = KingdomNativeCampFounding.Found(game, zone, Require);
			KingdomNativeCampFounding.Dedicate(game, zone, System, 64, Owned.Add, Require);
			Require(System.Population == 0 && KingdomResidents.OnRollCount(System) == 0, "camp already has citizens");
			Require(!KingdomGrowth.TryCountBeds(zone, out int beds, out _) || beds == 0, "camp already has housing");
			return "native-guest-actions phase=awaiting; synthetic-camp=true synthetic-water=64 menu-input=scripted save-load=untested";
		}

		internal static string StartQuickstart(XRLGame game, Zone zone)
		{
			Require(Vacant, "guest actions already started");
			Game = game; Zone = zone; Quickstart = true;
			System = game.GetSystem<KingdomSystem>();
			Require(KingdomQuickstartSettlementChecks.Observe(game, zone, System, "startup", out string failure), failure);
			Require(KingdomQuickstartRules.TryDecode(game.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "Quickstart receipt absent");
			foreach (string id in receipt.FounderObjectIds) Founders.Add(zone.FindObjectByID(id));
			return "native-guest-actions phase=awaiting; real-quickstart=true menu-input=scripted save-load=untested";
		}

		internal static string Check(XRLGame game, Zone zone)
		{
			Require(ReferenceEquals(Game, game) && ReferenceEquals(Zone, zone), "guest action owner changed");
			Evidence.Append("; census-population=").Append(System.Population);
			bool countedBeds = KingdomGrowth.TryCountBeds(Zone, out int observedBeds, out _);
			Evidence.Append(" beds=").Append(countedBeds ? observedBeds : -1);
			foreach (GameObject founder in Founders) Evidence.Append(" founder=").Append(founder?.IDIfAssigned)
				.Append(":alive=").Append(GameObject.Validate(founder) && founder.IsAlive)
				.Append(":citizen=").Append(KingdomCitizenship.BelongsTo(System, founder));
			Require(KingdomFirstGuestRuntime.IsAwaitingAnswer(System), "real due pass did not open correspondence");
			KingdomGrowthArrivalCandidate candidate = System.LifecycleBook.Growth.ArrivalCandidate;
			Evidence.Append("; planned-creed=").Append(candidate.PlannedCreed);
			int populationBefore = System.Population;
			string candidateId = candidate.Id, opportunityId = candidate.FirstGuest.OpportunityId;
			string[] correspondence = { "Admit this person through Growth", "Defer without limit", "Decline without penalty" };
			Choose("A first guest writes to " + System.KingdomDisplayName, correspondence, 1,
				() => KingdomFirstGuestRuntime.Open(System, The.Player));
			Require(KingdomFirstGuestRuntime.IsAwaitingAnswer(System)
				&& System.LifecycleBook.Growth.ArrivalCandidate.Id == candidateId
				&& System.Population == populationBefore, "deferral changed the candidate or admitted a citizen");
			Choose("A first guest writes to " + System.KingdomDisplayName, correspondence, 0,
				() => KingdomFirstGuestRuntime.Open(System, The.Player));
			candidate = System.LifecycleBook.Growth.ArrivalCandidate;
			Evidence.Append("; admission-phase=").Append(candidate.Phase)
				.Append(" guest-phase=").Append(candidate.FirstGuest.GuestPhase);
			Require(candidate.Id == candidateId && candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted,
				"Charter admission did not host the exact guest");
			GameObject body = Zone.FindObjectByID(candidate.ObjectId);
			Require(GameObject.Validate(body) && body.GetPart<r_KingdomFirstGuestBody>() != null,
				"hosted guest has no exact physical interaction part");
			Require(System.Population == populationBefore && !KingdomCitizenship.BelongsTo(System, body), "hosting silently granted citizenship");
			Require(KingdomGrowth.CanUsePhysicalFirstGuest(body, The.Player, candidateId, opportunityId)
				&& !KingdomGrowth.CanUsePhysicalFirstGuest(body, body, candidateId, opportunityId), "guest interaction actor authority differs");
			var actions = new Dictionary<string, InventoryAction>();
			GetInventoryActionsEvent.Send(The.Player, body, actions);
			Require(actions.TryGetValue("Chat", out InventoryAction chat)
				&& chat.Command == "r_TAF_FirstGuestChoice" && chat.Display == "speak with the first guest",
				"real inventory action event omitted the guest dialogue action");
			string[] bodyChoices = { "Welcome as citizen", "Ask to depart", "Remain our guest" };
			Choose("Your first guest", bodyChoices, 2,
				() => KingdomGrowth.OpenPhysicalFirstGuest(body, The.Player, candidateId, opportunityId));
			Require(candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				&& System.Population == populationBefore, "remaining a guest changed citizenship");
			if (!Quickstart)
			{
				int waterBefore = KingdomGrowth.CountStoredWater(Zone);
				for (int retry = 0; retry < 2; retry++)
				{
					Choose("Your first guest", bodyChoices, 0,
						() => KingdomGrowth.OpenPhysicalFirstGuest(body, The.Player, candidateId, opportunityId));
					Require(GameObject.Validate(body) && ReferenceEquals(Zone.FindObjectByID(candidate.ObjectId), body)
						&& candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
						&& candidate.FirstGuest.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted
						&& System.Population == 0 && !KingdomCitizenship.BelongsTo(System, body)
						&& KingdomGrowth.CountStoredWater(Zone) == waterBefore
						&& KingdomGrowth.CanUsePhysicalFirstGuest(body, The.Player, candidateId, opportunityId),
						"housing refusal consumed the guest, water, population or interaction");
				}
				return "native-guest-actions cases=1 passed=1 failed=0; no-beds-welcome=refused-twice same-guest=preserved"
					+ "; synthetic-camp=true menu-input=scripted rendered-ui=false save-load=untested" + Evidence;
			}
			Require(KingdomGrowth.TryCountBeds(Zone, out int beds, out _) && beds >= 6
				&& populationBefore == 4, "Quickstart did not finish its real homes with four founders");
			Choose("Your first guest", bodyChoices, 0,
				() => KingdomGrowth.OpenPhysicalFirstGuest(body, The.Player, candidateId, opportunityId));
			Evidence.Append("; welcome-phase=").Append(candidate.Phase)
				.Append(" guest-phase=").Append(candidate.FirstGuest.GuestPhase)
				.Append(" population=").Append(System.Population);
			Require(GameObject.Validate(body) && ReferenceEquals(Zone.FindObjectByID(body.IDIfAssigned), body)
				&& KingdomCitizenship.BelongsTo(System, body) && System.Population == populationBefore + 1,
				"explicit welcome did not enroll the same guest exactly once");
			Require(!KingdomGrowth.CanUsePhysicalFirstGuest(body, The.Player, candidateId, opportunityId),
				"completed guest action remains usable");
			int water = KingdomGrowth.CountStoredWater(Zone);
			KingdomGrowth.OpenPhysicalFirstGuest(body, The.Player, candidateId, opportunityId);
			Require(System.Population == populationBefore + 1 && KingdomGrowth.CountStoredWater(Zone) == water
				&& KingdomCitizenship.BelongsTo(System, body), "repeated welcome changed population, water or citizenship");
			return "native-guest-actions cases=1 passed=1 failed=0; scripted-menu-input=true rendered-ui=false save-load=untested"
				+ Evidence;
		}

		private static void Choose(string title, string[] options, int pick, Action action)
		{
			Require(ExpectedOptions == null, "another fixture menu choice is pending");
			ExpectedTitle = title; ExpectedOptions = options; Pick = pick; ActionActive = true;
			try
			{
				using (KingdomGovernanceScope.Begin(The.Player)) action();
				Require(!KingdomSurvey.HasBoundPass, "guest action leaked its local survey scope");
				Require(ExpectedOptions == null, "production action did not request the expected menu");
			}
			finally { ExpectedOptions = null; ExpectedTitle = null; ActionActive = false; }
		}

		internal static bool Select(string title, IReadOnlyList<string> options, ref int result)
		{
			if (ExpectedOptions == null) return true;
			Require(ConsoleLib.Console.ColorUtility.StripFormatting(title)
				== ConsoleLib.Console.ColorUtility.StripFormatting(ExpectedTitle),
				"production menu title differs: " + title);
			Require(options != null && options.Count == ExpectedOptions.Length, "production menu option count differs");
			for (int i = 0; i < options.Count; i++) Require(options[i] == ExpectedOptions[i], "production menu label differs");
			result = Pick; ExpectedOptions = null;
			return false;
		}
	}

	// Only replace a menu while this exact fixture has explicitly armed one expected choice.
	[HarmonyPatch(typeof(Popup), "PickOption")]
	internal static class KingdomGuestActionsMenuInput
	{
		[HarmonyPrefix]
		internal static bool Prefix(string Title, IReadOnlyList<string> Options, ref int __result)
			=> KingdomGuestActionsNativeChecks.Select(Title, Options, ref __result);
	}
}
