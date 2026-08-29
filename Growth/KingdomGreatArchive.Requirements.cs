using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomGreatArchive
	{
		private static bool TryRequirements(string Source,
			Dictionary<string, List<ResearchNode>> Grants,
			List<KingdomGreatArchiveRequirementFacts> Result, out string Failure)
		{
			Failure = null;
			foreach (string token in KingdomZoningRules.Tokens(Source))
			{
				KingdomGreatArchiveRequirementFacts group =
					new KingdomGreatArchiveRequirementFacts();
				string[] arms = token.Split(KingdomZoningRules.RosterSeparator);
				for (int i = 0; i < arms.Length; i++)
				{
					string arm = arms[i];
					if (KingdomZoningRules.KindOf(arm) == KingdomZoningRules.KindNode)
					{
						if (!Grants.TryGetValue(arm, out List<ResearchNode> sources)
							|| sources.Count == 0) return Fail(
							"A research prerequisite names no node grant.", out Failure);
						for (int j = 0; j < sources.Count; j++) AddAlternative(group,
							sources[j].Key, sources[j].Named);
					}
					else AddAlternative(group, null, ExternalName(arm));
				}
				if (group.Alternatives.Count > 0) Result.Add(group);
			}
			return true;
		}

		private static void AddAlternative(KingdomGreatArchiveRequirementFacts Group,
			string NodeKey, string Name)
		{
			for (int i = 0; i < Group.Alternatives.Count; i++)
				if (Group.Alternatives[i].NodeKey == NodeKey
					&& Group.Alternatives[i].DisplayName == Name) return;
			Group.Alternatives.Add(new KingdomGreatArchiveAlternativeFacts {
				NodeKey = NodeKey, DisplayName = Name
			});
		}

		private static string ExternalName(string Token)
		{
			string name = KingdomZoningRules.NameOf(Token) ?? Token;
			string kind = KingdomZoningRules.KindOf(Token);
			return string.IsNullOrEmpty(kind) ? name : name + " (" + kind + ")";
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
