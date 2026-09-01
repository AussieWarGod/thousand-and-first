using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private const string ArrivalMarkerProperty = "r_TAF_GrowthArrivalMarker";
		private const string ArrivalOriginPlanProperty = "r_TAF_GrowthArrivalOriginPlan";
		private const string ArrivalCreedPlanProperty = "r_TAF_GrowthArrivalCreedPlan";
		private const string ArrivalNamePlanProperty = "r_TAF_GrowthArrivalNamePlan";
		private const string ArrivalDatePlanProperty = "r_TAF_GrowthArrivalDatePlan";
		private const string ArrivalCitizenshipPlanProperty = "r_TAF_GrowthCitizenshipPlan";
		private const string ArrivalCitizenshipPlanValue = "base-slot-v1";
		private const string ArrivalEnrollmentReceiptProperty = "r_TAF_GrowthArrivalEnrollment";
		private const string ArrivalRosterReceiptProperty = "r_TAF_GrowthArrivalRoster";
		private const string ArrivalCreedReceiptProperty = "r_TAF_GrowthArrivalCreed";
		private const string ArrivalConversationReceiptProperty = "r_TAF_GrowthArrivalConversation";
		private const string ArrivalConversationText =
			"Live and drink, friend. We heard there was water here, and a place worth the walk.";
		private const string ArrivalConversationGoodbye = "Live and drink.";
		private const string ArrivalConversationQuestion = "Why did you come?";
		private const string ArrivalConversationAnswerPrefix = "The road from ";
		private const string ArrivalConversationAnswerSuffix =
			" was long, and the wells there are bitter. Here the water is shared. That is the whole of it.";
		private const int MaxArrivalConversationNodes = 64;
		private const int MaxArrivalConversationDepth = 16;
		private const int MaxArrivalConversationAttributes = 64;
		private const int MaxArrivalAllegianceDepth = 16;
		private const int MaxArrivalFactionMemberships = 64;

		private enum ArrivalResult
		{
			Failed,
			Deferred,
			Joined,
			Refused,
			WaterUnavailable,
			NoGround,
			PopulationCap,
			SupportCap,
			Declined,
			Departed
		}

		public static bool Enabled => Options.GetOption("r_TAF_OptionGrowth") != "No";

		/// <summary>
		/// Whether physical water upkeep and water scarcity run. Option ID stays unchanged for
		/// settings compatibility. Food is physical optional economy, never scarcity.
		/// </summary>
		public static bool ScarcityEnabled => Options.GetOption("r_TAF_OptionThirst") != "No";

		/// <summary>Compatibility alias for <see cref="ScarcityEnabled"/>.</summary>
		public static bool ThirstEnabled => ScarcityEnabled;

		/// <summary>Legacy API projection. Passive hunger is permanently retired.</summary>
		public static bool HungerEnabled => false;

		public static long Interval(KingdomSystem System, Zone Z)
		{
			return Interval(System, Z, System.Population);
		}

		private static long Interval(KingdomSystem System, Zone Z, int cohort)
		{
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out var district);
			return KingdomRules.PolicyInterval(KingdomRules.ArrivalIntervalTicks(cohort, district),
				System.Gate, System.Stores);
		}
	}
}
