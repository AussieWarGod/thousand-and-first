using System;

using ThousandAndFirst.Simulation.City;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Player-facing transaction seam for frozen liquid declarations.</summary>
	internal static class KingdomLiquidConfiguration
	{
		internal static bool Open(r_KingdomLiquidConduit Part, GameObject Actor)
		{
			if (Part == null || Actor == null || !Actor.IsPlayer()) return false;
			bool brine = KingdomLiquidVisualRules.IsBrine(Part.Liquid);
			int choice = Popup.PickOption(Title: "Configure " + (brine ? "brine main" : "fresh-water main"),
				Intro: KingdomLiquidConfigurationRules.Status(Part.Liquid, Part.Joins, false),
				Options: KingdomLiquidConfigurationRules.Options(brine), AllowEscape: true);
			return Commit(Part, choice);
		}

		internal static bool Open(r_KingdomLiquidTap Part, GameObject Actor)
		{
			if (Part == null || Actor == null || !Actor.IsPlayer()) return false;
			bool brine = KingdomLiquidVisualRules.IsBrine(Part.Liquid);
			int choice = Popup.PickOption(Title: "Configure " + (brine ? "brine tap" : "fresh-water tap"),
				Intro: KingdomLiquidConfigurationRules.Status(Part.Liquid, Part.Joins, true),
				Options: KingdomLiquidConfigurationRules.Options(brine), AllowEscape: true);
			return Commit(Part, choice);
		}

		internal static bool Open(r_KingdomLiquidCrossover Part, GameObject Actor)
		{
			if (Part == null || Actor == null || !Actor.IsPlayer()) return false;
			int choice = Popup.PickOption(Title: "Configure liquid crossing",
				Intro: KingdomLiquidConfigurationRules.CrossingStatus(Part.Pairs),
				Options: new string[2]
				{
					"Fresh north-south; brine east-west  [map sign " + (char)216 + "]",
					"Fresh east-west; brine north-south  [map sign " + (char)215 + "]"
				}, AllowEscape: true);
			if (choice < 0) return false;
			string before = Part.Pairs;
			string next;
			bool changed;
			string failure;
			if (!KingdomLiquidConfigurationRules.TryPlanCrossing(before, choice, true,
				out next, out changed, out failure))
			{
				Popup.Show(failure);
				return false;
			}
			if (!changed) return false;
			Part.Pairs = next;
			if (!KingdomLiquidConfigurationRules.CrossingReadsBack(Part.Pairs, choice == 0))
			{
				Part.Pairs = before;
				Popup.Show("The crossing did not retain its declared orientation; nothing changed.");
				return false;
			}
			KingdomNetworks.MarkTopologyChanged();
			return true;
		}

		private static bool Commit(r_KingdomLiquidConduit Part, int Choice)
		{
			if (Choice < 0) return false;
			string before = Part.Joins;
			string next;
			int mask;
			bool changed;
			string failure;
			if (!KingdomLiquidConfigurationRules.TryPlanDeclaration(before, Choice, true,
				out next, out mask, out changed, out failure))
			{
				Popup.Show(failure);
				return false;
			}
			if (!changed) return false;
			Part.Joins = next;
			if (!KingdomLiquidConfigurationRules.DeclarationReadsBack(Part.Joins, mask))
			{
				Part.Joins = before;
				Popup.Show("The main did not retain its declared faces; nothing changed.");
				return false;
			}
			KingdomNetworks.MarkTopologyChanged();
			return true;
		}

		private static bool Commit(r_KingdomLiquidTap Part, int Choice)
		{
			if (Choice < 0) return false;
			string before = Part.Joins;
			string next;
			int mask;
			bool changed;
			string failure;
			if (!KingdomLiquidConfigurationRules.TryPlanDeclaration(before, Choice, true,
				out next, out mask, out changed, out failure))
			{
				Popup.Show(failure);
				return false;
			}
			if (!changed) return false;
			Part.Joins = next;
			if (!KingdomLiquidConfigurationRules.DeclarationReadsBack(Part.Joins, mask))
			{
				Part.Joins = before;
				Popup.Show("The tap did not retain its declared faces; nothing changed.");
				return false;
			}
			KingdomNetworks.MarkTopologyChanged();
			return true;
		}
	}
}
