using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransaction
	{
		private static bool EnsureAbility(GameObject Actor)
		{
			if (Actor == null || !GameObject.Validate(Actor))
			{
				return false;
			}
			KingdomCharterPart charter = Actor.RequirePart<KingdomCharterPart>();
			charter.EnsureAbility();
			return Actor.GetActivatedAbilityByCommand(KingdomCharterPart.COMMAND) != null;
		}

		private static bool EnsurePlacement(KingdomSystem System, Zone Site, int RiteX, int RiteY)
		{
			if (System == null || Site?.GetCell(RiteX, RiteY) == null)
			{
				return false;
			}
			return KingdomPlots.EnsureFoundingHeartProjection(System, Site, RiteX, RiteY);
		}

		internal static string FoundingEventID(KingdomFoundingKind Kind,
			string TransactionID, string Lane)
		{
			if (!KingdomFoundingTransactionRules.IsKnownKind(Kind) ||
				Kind == KingdomFoundingKind.None ||
				!KingdomFoundingTransactionRules.IsNonce(TransactionID) ||
				string.IsNullOrEmpty(Lane) || Lane.Length > 32)
			{
				return null;
			}
			for (int i = 0; i < Lane.Length; i++)
			{
				char c = Lane[i];
				if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
				{
					return null;
				}
			}
			return "taf:founding:v1:" + ((int)Kind) + ":" + TransactionID + ":" + Lane;
		}

		private static string EncodeComponents(Dictionary<string, int> Components)
		{
			if (Components == null)
			{
				return null;
			}
			List<string> keys = new List<string>(Components.Keys);
			keys.Sort(StringComparer.Ordinal);
			System.Text.StringBuilder encoded = new System.Text.StringBuilder();
			foreach (string key in keys)
			{
				if (encoded.Length > 0)
				{
					encoded.Append(';');
				}
				encoded.Append(Convert.ToBase64String(
					System.Text.Encoding.UTF8.GetBytes(key ?? "")))
					.Append(':').Append(Components[key]);
			}
			return encoded.ToString();
		}

	}
}
