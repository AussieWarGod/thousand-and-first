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
			SupportCap
		}

		public static bool Enabled => Options.GetOption("r_TAF_OptionGrowth") != "No";

		/// <summary>
		/// Whether the settlement's people consume what they need and can suffer for the want of
		/// it. ONE switch for both binding goods, deliberately: water and food are the same
		/// promise to the player ("this place has needs and can fail them"), and a founder who
		/// turned scarcity off did not ask to keep half of it. The option ID is unchanged so no
		/// save or settings file notices; only its display text moved.
		/// </summary>
		public static bool ScarcityEnabled => Options.GetOption("r_TAF_OptionThirst") != "No";

		/// <summary>The water half of <see cref="ScarcityEnabled"/>, under the name every caller
		/// written before food was a flow reads.</summary>
		public static bool ThirstEnabled => ScarcityEnabled;

		/// <summary>The food half of <see cref="ScarcityEnabled"/>, named so a reader of the
		/// hunger path is not left wondering whether it has a switch of its own.</summary>
		public static bool HungerEnabled => ScarcityEnabled;

		public static long Interval(KingdomSystem System, Zone Z)
		{
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out var district);
			return KingdomRules.PolicyInterval(KingdomRules.ArrivalIntervalTicks(System.Population, district), System.Gate, System.Stores);
		}
	}
}
