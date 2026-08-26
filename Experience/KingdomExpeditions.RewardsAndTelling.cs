using System;
using System.Collections.Generic;

using Qud.API;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		private static bool EnsureReward(GameObject Body, KingdomJobRow Row)
		{
			if (Row.CargoAmount <= 0) return true;
			if (!GameObject.Validate(Body) || Body.Inventory == null) return false;
			GameObject found = null;
			foreach (GameObject item in Body.Inventory.GetObjects())
			{
				if (item.GetIntProperty(RewardJobProperty) != Row.JobId) continue;
				if (found != null) return false;
				found = item;
			}
			if (found != null) return found.Count == Row.CargoAmount;
			GameObject reward = GameObject.Create(
				KingdomMaterials.MaterialBlueprints[(int)KingdomMaterial.Scrap]);
			if (!GameObject.Validate(reward)) return false;
			reward.Count = Row.CargoAmount;
			reward.SetIntProperty(RewardJobProperty, Row.JobId);
			try
			{
				Body.Inventory.AddObject(reward, null, Silent: true, NoStack: true);
			}
			catch { }
			if (reward.InInventory == Body && reward.Count == Row.CargoAmount)
			{
				KingdomSurvey.ObserveChangedInActive(Body.CurrentZone, Body);
				return true;
			}
			if (GameObject.Validate(reward) && reward.InInventory == null && reward.CurrentCell == null)
				reward.Obliterate(null, Silent: true);
			return false;
		}

		private static void ConsumeRemainingProvisions(GameObject Body, int JobId)
		{
			if (!GameObject.Validate(Body) || Body.Inventory == null) return;
			List<GameObject> items = new List<GameObject>(Body.Inventory.GetObjects());
			for (int i = 0; i < items.Count; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.GetIntProperty(ProvisionJobProperty) != JobId)
					continue;
				while (GameObject.Validate(item) && item.Count > 0)
				{
					int before = item.Count;
					try { item.Destroy(null, Silent: true); }
					catch { break; }
					if (GameObject.Validate(item) && item.Count >= before) break;
				}
			}
			KingdomSurvey.ObserveChangedInActive(Body.CurrentZone, Body);
		}

		private static int SkillBonus(GameObject Body)
		{
			int bonus = 0;
			if (Body.HasSkill("Survival_RuinsSurvival")) bonus += 10;
			if (Body.HasSkill("Survival_Trailblazer")) bonus += 5;
			if (Body.HasSkill("Tinkering") || Body.HasSkill("Tinkering_Tinker1")
				|| Body.HasSkill("Tinkering_Tinker2")) bonus += 10;
			return (bonus > KingdomExpeditionRules.MaxSkillBonus)
				? KingdomExpeditionRules.MaxSkillBonus : bonus;
		}

		private static string ResultLine(KingdomJobRow Row, KingdomExpeditionOutcome Outcome,
			long Tick)
		{
			string who = ShownName(Row.SubjectName, "Resident " + Row.SubjectId);
			string where = ShownName(Row.TargetName, Row.DestZoneId);
			string date = DateAt(Tick);
			switch (Outcome)
			{
			case KingdomExpeditionOutcome.RichFind:
			case KingdomExpeditionOutcome.ModestFind:
				return "{{G|On " + date + ", " + who + " returned from " + where + " with "
					+ Row.CargoAmount + ((Row.CargoAmount == 1) ? " piece" : " pieces") + " of scrap.}}";
			case KingdomExpeditionOutcome.PickedClean:
				return "{{K|On " + date + ", " + who + " returned from " + where
					+ "; the site had already been picked clean.}}";
			case KingdomExpeditionOutcome.Cancelled:
				return "{{K|On " + date + ", " + who + " was recalled from " + where
					+ "; the dated dispatch receipt remained the only charge.}}";
			case KingdomExpeditionOutcome.ResidentDiedOnGround:
				return "{{r|On " + date + ", " + who + " was found dead at " + where
					+ "; the commission ended there.}}";
			case KingdomExpeditionOutcome.ResidentMissingFromBoundGround:
				return "{{r|On " + date + ", " + who + " was not found on the ground their binding named at "
					+ where + "; the roll records them astray, not dead.}}";
			default:
				return "{{K|On " + date + ", " + who + " joined the founder before the commission from "
					+ where + " could be completed.}}";
			}
		}

		private static string ChronicleLine(KingdomJobRow Row, KingdomExpeditionOutcome Outcome,
			long Tick)
		{
			string plain = ResultLine(Row, Outcome, Tick).Replace("{{G|", "")
				.Replace("{{K|", "").Replace("{{r|", "").Replace("}}", "");
			if (plain.EndsWith(".", StringComparison.Ordinal))
				plain = plain.Substring(0, plain.Length - 1);
			return plain;
		}

		private static string DateAt(long Tick)
		{
			long safe = (Tick < 0L) ? 0L : Tick;
			return XRL.World.Calendar.GetDay(safe) + " of " + XRL.World.Calendar.GetMonth(safe)
				+ ", " + XRL.World.Calendar.GetYear(safe) + " AR";
		}

		private static string SafeName(string Value, string Fallback)
		{
			string value = string.IsNullOrEmpty(Value) ? Fallback : Value;
			if (string.IsNullOrEmpty(value)) value = "unnamed ground";
			value = value.Replace('\r', ' ').Replace('\n', ' ');
			return (value.Length <= 160) ? value : value.Substring(0, 160);
		}

		/// <summary>Persisted job/journal names are plain; only this sink projection is rich.</summary>
		private static string ShownName(string Value, string Fallback)
		{
			return KingdomPresentation.Rich(SafeName(Value, Fallback));
		}

		private static bool Refuse(string Reason, out string Failure)
		{
			Failure = Reason;
			return false;
		}

	}
}
