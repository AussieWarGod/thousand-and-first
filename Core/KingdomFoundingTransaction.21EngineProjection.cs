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
			Cell rite = Site?.GetCell(RiteX, RiteY);
			if (System == null || Site == null || rite == null)
			{
				return false;
			}
			Site.SetZoneProperty(KingdomPlots.RiteXProperty, RiteX.ToString());
			Site.SetZoneProperty(KingdomPlots.RiteYProperty, RiteY.ToString());
			if (!KingdomPlots.TryRiteGround(Site, out var readX, out var readY) ||
				readX != RiteX || readY != RiteY)
			{
				return false;
			}
			if (!KingdomPlots.TrySurveyedHeart(Site, out var survey))
			{
				if (!KingdomPlots.SurveyHeart(System, Site, RiteX, RiteY) ||
					!KingdomPlots.TrySurveyedHeart(Site, out survey))
				{
					return false;
				}
			}
			return EnsureMark(rite, KingdomPlots.HeartRelicBlueprint,
					KingdomPlots.HeartRelicProperty) &&
				EnsureMark(Site.GetCell(survey.X1, survey.Y1), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X2, survey.Y1), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X1, survey.Y2), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty) &&
				EnsureMark(Site.GetCell(survey.X2, survey.Y2), KingdomPlots.SurveyStakeBlueprint,
					KingdomPlots.HeartStakeProperty);
		}

		private static bool EnsureMark(Cell Cell, string Blueprint, string Property)
		{
			if (Cell == null)
			{
				return false;
			}
			foreach (GameObject item in Cell.Objects)
			{
				if (item.GetIntProperty(Property) == 1 && item.CurrentCell == Cell)
				{
					return true;
				}
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return false;
			}
			placed.SetIntProperty(Property, 1);
			Cell.AddObject(placed);
			if (placed.CurrentCell == Cell && Cell.Objects.Contains(placed))
			{
				return true;
			}
			try
			{
				placed.Obliterate();
			}
			catch
			{
			}
			return false;
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
