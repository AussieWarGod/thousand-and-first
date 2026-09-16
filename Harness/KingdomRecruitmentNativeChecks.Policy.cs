using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using XRL;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	internal sealed partial class KingdomRecruitmentNativeChecks
	{
		[ThreadStatic] internal static XRLGame StoryOwner;
		[ThreadStatic] internal static string StoryOverride;

		private void ProbeStoryPolicy(StringBuilder Evidence)
		{
			Require(PopulationManager.TryResolvePopulation("r_KingdomSettlers", out var population),
				"recruitment population absent for policy probe");
			var items = population.Items;
			var growth = System.LifecycleBook.Growth;
			var head = growth.ArrivalDebtRanges[0];
			long next = System.NextArrivalTick;
			string experience = Convert.ToBase64String(KingdomExperienceCodec.EncodeEnvelope(System.Experience));
			Require(StoryOwner == null && StoryOverride == null, "another story probe is armed");
			try
			{
				SetRegards("Hindren");
				const string extension = "r_TAF_RecruitmentExtensionProbe";
				Require(!KingdomLifecycleRules.GrowthFirstGuestBlueprintAllowed(extension),
					"extension unexpectedly belongs to the owned story-guest allowlist");
				population.Items = new List<PopulationItem>
					{ new PopulationObject { Blueprint = extension, Number = "1", Weight = 100U } };
				StoryOwner = Game; StoryOverride = "Yes";
				Require(Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") == "Yes"
					&& KingdomRecruitment.PendingNeed(System)?.StartsWith("No settlers are willing to come:") == true,
					"story report offered an extension without first-guest authority");
				Require(!KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(System, head.FirstOrdinal,
					head.FirstDueTick, true, out _, out _), "story route admitted the extension profile");
				StoryOverride = "No";
				Require(Options.GetOption(KingdomExperienceOptions.StoryOptionId, "Yes") == "No"
					&& KingdomRecruitment.PendingNeed(System) == null,
					"disabled-story report hid an eligible ordinary extension recruit");
				Require(KingdomSemanticSelection.TryPrepareGrowthArrivalPayload(System, head.FirstOrdinal,
					head.FirstDueTick, false, out var plan, out string failure)
					&& plan.Blueprint == extension, failure ?? "ordinary extension recruitment was refused");
				Require(ReferenceEquals(head, growth.ArrivalDebtRanges[0]) && growth.ArrivalOpportunity == null
					&& growth.ArrivalCandidate == null && growth.ArrivalOp == null && System.Population == 0
					&& System.NextArrivalTick == next && experience == Convert.ToBase64String(
						KingdomExperienceCodec.EncodeEnvelope(System.Experience)),
					"report or catalogue probe published an option epoch, arrival or citizen");
			}
			finally { StoryOverride = null; StoryOwner = null; population.Items = items; }
			Evidence.Append("; recruitment-story-policy enabled=restricted disabled=ordinary-extension")
				.Append(" observation-only=true synthetic-option-read=true catalogue-restored=true");
		}
	}

	// Exact synchronous scenario-owned option read; no option write, file save or callback.
	[HarmonyPatch(typeof(Options), "GetOption", new Type[] { typeof(string), typeof(string) })]
	internal static class KingdomRecruitmentStoryOptionProbe
	{
		[HarmonyPrefix]
		internal static bool Prefix(string ID, ref string __result)
		{
			if (KingdomRecruitmentNativeChecks.StoryOverride == null
				|| !ReferenceEquals(KingdomRecruitmentNativeChecks.StoryOwner, The.Game)
				|| ID != KingdomExperienceOptions.StoryOptionId) return true;
			__result = KingdomRecruitmentNativeChecks.StoryOverride;
			return false;
		}
	}
}
