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

		private static bool ArrivalAllegianceRepresentable(AllegianceSet allegiance)
		{
			List<AllegianceSet> seen = new List<AllegianceSet>();
			int depth = 0;
			while (allegiance != null)
			{
				if (depth > MaxArrivalAllegianceDepth
					|| allegiance.Count > MaxArrivalFactionMemberships
					|| !ArrivalAllyReasonRepresentable(allegiance.Reason)) return false;
				for (int i = 0; i < seen.Count; i++)
					if (ReferenceEquals(seen[i], allegiance)) return false;
				seen.Add(allegiance);
				allegiance = allegiance.Previous;
				depth++;
			}
			return true;
		}

		private static bool ArrivalAllegianceAcyclic(AllegianceSet allegiance)
		{
			HashSet<AllegianceSet> seen = new HashSet<AllegianceSet>();
			bool foundBase = false;
			while (allegiance != null)
			{
				if (!seen.Add(allegiance)) return false;
				if (allegiance.SourceID == 0) foundBase = true;
				allegiance = allegiance.Previous;
			}
			return foundBase;
		}

		private static bool ArrivalAllyReasonRepresentable(IAllyReason reason)
		{
			if (reason == null) return true;
			switch (reason.GetType().FullName)
			{
			case "XRL.World.AI.AllyAscend":
			case "XRL.World.AI.AllyBeguile":
			case "XRL.World.AI.AllyBirth":
			case "XRL.World.AI.AllyBond":
			case "XRL.World.AI.AllyClan":
			case "XRL.World.AI.AllyClone":
			case "XRL.World.AI.AllyConstructed":
			case "XRL.World.AI.AllyCurio":
			case "XRL.World.AI.AllyDefault":
			case "XRL.World.AI.AllyHoundmaster":
			case "XRL.World.AI.AllyPack":
			case "XRL.World.AI.AllyPet":
			case "XRL.World.AI.AllyPilot":
			case "XRL.World.AI.AllyProselytize":
			case "XRL.World.AI.AllyRebuke":
			case "XRL.World.AI.AllyRetinue":
			case "XRL.World.AI.AllySummon":
			case "XRL.World.AI.AllyWish":
				return true;
			default:
				return false;
			}
		}

		private static bool ArrivalConversationRepresentable(
			ConversationXMLBlueprint blueprint)
		{
			int remaining = MaxArrivalConversationNodes;
			return ArrivalConversationRepresentable(blueprint, 0, ref remaining,
				new List<ConversationXMLBlueprint>());
		}

		private static bool ArrivalConversationRepresentable(
			ConversationXMLBlueprint blueprint, int depth, ref int remaining,
			List<ConversationXMLBlueprint> lineage)
		{
			if (blueprint == null) return true;
			if (depth > MaxArrivalConversationDepth || remaining <= 0
				|| blueprint.Attributes != null && blueprint.Attributes.Count
					> MaxArrivalConversationAttributes
				|| blueprint.Children != null && blueprint.Children.Count
					> MaxArrivalConversationNodes) return false;
			for (int i = 0; i < lineage.Count; i++)
				if (ReferenceEquals(lineage[i], blueprint)) return false;
			remaining--;
			lineage.Add(blueprint);
			if (blueprint.Children != null)
				for (int i = 0; i < blueprint.Children.Count; i++)
					if (!ArrivalConversationRepresentable(blueprint.Children[i], depth + 1,
						ref remaining, lineage)) return false;
			lineage.RemoveAt(lineage.Count - 1);
			return true;
		}

		private static bool ArrivalConversationAcyclic(ConversationXMLBlueprint blueprint)
		{
			return ArrivalConversationAcyclic(blueprint,
				new HashSet<ConversationXMLBlueprint>());
		}

		private static bool ArrivalConversationAcyclic(ConversationXMLBlueprint blueprint,
			HashSet<ConversationXMLBlueprint> lineage)
		{
			if (blueprint == null) return true;
			if (!lineage.Add(blueprint)) return false;
			if (blueprint.Children != null)
				for (int i = 0; i < blueprint.Children.Count; i++)
					if (!ArrivalConversationAcyclic(blueprint.Children[i], lineage)) return false;
			lineage.Remove(blueprint);
			return true;
		}
	}
}
