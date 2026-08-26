using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		private static void HandleStyle(XmlDataHelper xml)
		{
			KingdomStyleDraft declared = new KingdomStyleDraft
			{
				Name = xml.GetAttribute("Name"),
				Terrain = xml.GetAttribute("Terrain"),
				Region = xml.GetAttribute("Region"),
				Strata = xml.GetAttribute("Strata"),
				Priority = xml.GetAttribute("Priority"),
				GroundClause = xml.GetAttribute("GroundClause"),
				Crop = xml.GetAttribute("Crop"),
				Seed = xml.GetAttribute("Seed"),
				CropRow = xml.GetAttribute("CropRow"),
				WallMaterial = xml.GetAttribute("WallMaterial"),
				TimberWall = xml.GetAttribute("TimberWall")
			};
			if (!KingdomStyleRules.ValidName(declared.Name))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: style needs a valid Name");
				xml.DoneWithElement();
				return;
			}
			declared.Name = declared.Name.Trim();
			int found = -1;
			for (int i = 0; i < _styleDrafts.Count; i++)
			{
				if (string.Equals(_styleDrafts[i].Name, declared.Name,
					StringComparison.OrdinalIgnoreCase))
				{
					found = i;
					break;
				}
			}
			KingdomStyleDraft merged = (found < 0) ? declared.Copy()
				: KingdomStyleRules.Merge(_styleDrafts[found], declared);
			if (!KingdomStyleRules.TryParse(merged, out KingdomStyleDefinition parsed,
				out string error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				xml.DoneWithElement();
				return;
			}
			if (!KingdomStyleRules.TryValidateBehavior(_styleDefinitions, parsed, found,
				out error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				xml.DoneWithElement();
				return;
			}
			if (!StyleBlueprintsValid(parsed, out string behaviorError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: style " + parsed.Name
					+ " has invalid behavior: " + behaviorError);
				xml.DoneWithElement();
				return;
			}
			if (found < 0)
			{
				if (_styleDrafts.Count >= KingdomStyleRules.MaxStyles)
				{
					MetricsManager.LogError("ThousandAndFirst KingdomBuildings: too many styles; "
						+ declared.Name + " was refused");
				}
				else
				{
					_styleDrafts.Add(merged);
					_styleDefinitions.Add(parsed);
					_styles.Add(parsed.Name);
				}
			}
			else
			{
				_styleDrafts[found] = merged;
				_styleDefinitions[found] = parsed;
				_styles[found] = parsed.Name;
			}
			xml.DoneWithElement();
		}

		private static bool StyleBlueprintsValid(KingdomStyleDefinition Definition,
			out string Error)
		{
			Error = null;
			if (Definition == null) return false;
			string[] names = new string[]
			{
				Definition.CropBlueprint, Definition.SeedBlueprint,
				Definition.CropRowBlueprint, Definition.TimberWallBlueprint
			};
			GameObjectBlueprint[] blueprints = new GameObjectBlueprint[names.Length];
			for (int i = 0; i < names.Length; i++)
			{
				if (string.IsNullOrEmpty(names[i])) continue;
				try { blueprints[i] = GameObjectFactory.Factory.GetBlueprintIfExists(names[i]); }
				catch { blueprints[i] = null; }
				if (blueprints[i] == null)
				{
					Error = "unknown blueprint " + names[i];
					return false;
				}
			}
			if (blueprints[0] != null && !blueprints[0].HasPart("Food")
				&& !blueprints[0].HasPart("PreparedCookingIngredient"))
			{
				Error = "Crop " + names[0] + " is not food";
				return false;
			}
			if (blueprints[1] != null && !blueprints[1].HasPart("r_KingdomSeed"))
			{
				Error = "Seed " + names[1] + " has no r_KingdomSeed part";
				return false;
			}
			if (blueprints[2] != null && (!blueprints[2].InheritsFrom("Plant")
				|| !blueprints[2].HasPart("Harvestable")))
			{
				Error = "CropRow " + names[2] + " is not a harvestable Plant";
				return false;
			}
			if (blueprints[3] != null
				&& !blueprints[3].GetPartParameter("Physics", "Solid", false))
			{
				Error = "TimberWall " + names[3] + " is not solid";
				return false;
			}
			return true;
		}
	}
}
