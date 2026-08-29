using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomLodging
	{
		internal static bool TryLabHome(Zone Z, GameObject Resident,
			out GameObject Home, out string PlotId)
		{
			Home = null;
			PlotId = Resident?.GetStringProperty(HomePlotIdProperty);
			if (Z == null || string.IsNullOrEmpty(PlotId)) return false;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
			{
				if (!GameObject.Validate(item)
					|| item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1
					|| !string.Equals(item.GetStringProperty(KingdomPlots.PlotIdProperty),
						PlotId, StringComparison.Ordinal)) continue;
				if (Home != null) { Home = null; return false; }
				Home = item;
			}
			return GameObject.Validate(Home);
		}

		internal static bool TryPrepareLabRehouse(Zone Z, GameObject Resident,
			string ExpectedSourcePlot, out GameObject TargetHome, out string TargetPlot,
			out string Failure)
		{
			TargetHome = null; TargetPlot = null; Failure = null;
			if (!TryLabHome(Z, Resident, out GameObject source, out string current)
				|| !string.Equals(current, ExpectedSourcePlot, StringComparison.Ordinal))
				return LabFail("The neighbour no longer occupies the exact source plot.", out Failure);
			List<GameObject> homes = HousingIn(Z);
			for (int i = homes.Count - 1; i >= 0; i--)
				if (homes[i] == source || string.Equals(homes[i].GetStringProperty(
					KingdomPlots.PlotIdProperty), ExpectedSourcePlot, StringComparison.Ordinal))
					homes.RemoveAt(i);
			Dictionary<string, List<GameObject>> occupancy =
				new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
			List<GameObject> residents = ResidentsIn(Z);
			for (int i = 0; i < residents.Count; i++)
			{
				if (ReferenceEquals(residents[i], Resident)) continue;
				string plot = residents[i].GetStringProperty(HomePlotIdProperty);
				if (!string.IsNullOrEmpty(plot)) AddOccupant(occupancy, plot, residents[i]);
			}
			KingdomLodgingRules.UnhousedReason reason;
			KingdomLodgingRules.Closeness roomiest;
			List<string> needs;
			TargetPlot = ChooseHome(Z, Resident, homes, occupancy, out TargetHome,
				out reason, out roomiest, out needs);
			if (TargetPlot == null)
				return LabFail(
					"No different acceptable roof has exact free capacity for that neighbour.",
					out Failure);
			string targetId = TargetHome.IDIfAssigned;
			if (string.IsNullOrEmpty(targetId))
			{
				TargetHome = null; TargetPlot = null;
				return LabFail("The proposed target roof has no stable object identity.", out Failure);
			}
			int plotMatches = 0;
			for (int i = 0; i < homes.Count; i++)
				if (homes[i].GetStringProperty(KingdomPlots.PlotIdProperty) == TargetPlot)
					plotMatches++;
			int idMatches = 0;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				if (item.IDIfAssigned == targetId) idMatches++;
			if (plotMatches != 1 || idMatches != 1)
			{
				TargetHome = null; TargetPlot = null;
				return LabFail("The proposed target roof has a duplicate plot or object identity.",
					out Failure);
			}
			return true;
		}

		internal static bool TryApplyLabRehouse(KingdomSystem System, Zone Z,
			GameObject Resident, string ExpectedSourcePlot, string ExpectedTargetPlot,
			string ExpectedTargetObjectId, out string Failure)
		{
			Failure = null;
			string held = Resident?.GetStringProperty(HomePlotIdProperty);
			if (string.Equals(held, ExpectedTargetPlot, StringComparison.Ordinal))
			{
				if (!TryLabHome(Z, Resident, out GameObject recovered,
					out string recoveredPlot)
					|| !string.Equals(recoveredPlot, ExpectedTargetPlot,
						StringComparison.Ordinal)
					|| !string.Equals(recovered.IDIfAssigned, ExpectedTargetObjectId,
						StringComparison.Ordinal))
					return LabFail("The applied target roof can no longer be proved exactly.",
						out Failure);
				FinishLabRehouse(System, Z, Resident, ExpectedTargetPlot);
				return true;
			}
			if (!string.Equals(held, ExpectedSourcePlot, StringComparison.Ordinal))
				return LabFail("The neighbour's home changed after the request was frozen.", out Failure);
			if (!TryPrepareLabRehouse(Z, Resident, ExpectedSourcePlot,
				out GameObject target, out string targetPlot, out Failure)) return false;
			if (!string.Equals(targetPlot, ExpectedTargetPlot, StringComparison.Ordinal)
				|| !string.Equals(target?.IDIfAssigned, ExpectedTargetObjectId, StringComparison.Ordinal))
				return LabFail("The exact target roof is no longer the lawful rehouse result.", out Failure);
			Resident.SetStringProperty(HomePlotIdProperty, ExpectedTargetPlot);
			if (!string.Equals(Resident.GetStringProperty(HomePlotIdProperty),
				ExpectedTargetPlot, StringComparison.Ordinal))
				return LabFail("The exact home assignment did not persist.", out Failure);
			FinishLabRehouse(System, Z, Resident, ExpectedTargetPlot);
			return true;
		}

		private static void FinishLabRehouse(KingdomSystem System, Zone Z,
			GameObject Resident, string TargetPlot)
		{
			KingdomConversion.ForgetCohabitation(Resident);
			bool warned = KingdomBrink.Of(Resident, BrinkKind.Roof).Warned;
			if (KingdomBrink.Lift(Resident, BrinkKind.Roof) && warned)
				KingdomBrink.Unsay(System, BrinkKind.Roof, NameOf(Resident),
					KingdomWord.StandsIn(Z), System.SeatName);
			Resident.SetIntProperty(UnhousedAnnouncedProperty, 0);
			KingdomLabCivicRuntime.ObserveRehoused(System, Z, Resident, TargetPlot);
		}

		internal static void StartLabRoofBrink(KingdomSystem System, Zone Z,
			GameObject Resident, string RefusedTag, string WorkName)
		{
			if (System == null || Z == null || Resident == null) return;
			Resident.SetIntProperty(UnhousedAnnouncedProperty, 1);
			string name = RollNameOf(Resident);
			string spoken = "{{R|" + KingdomPresentation.Rich(name) + " refuses "
				+ KingdomPresentation.Rich(RefusedTag) + " at "
				+ KingdomPresentation.Rich(WorkName)
				+ ". Rehouse them beyond that exact work's reach before the roof window ends.}}";
			RunRoofBrink(System, Z, Resident, name, spoken);
		}

		private static bool LabFail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
