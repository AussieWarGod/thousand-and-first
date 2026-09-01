using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool VerifyFounded(KingdomSystem System, Zone Zone,
			KingdomQuickstartProfile Profile, out string Failure)
		{
			Failure = "";
			if (System == null || Zone == null || Profile == null || !System.Founded
				|| !string.Equals(System.SeatName, Profile.CityName, StringComparison.Ordinal)
				|| !string.Equals(System.KingdomDisplayName, Profile.CityName,
					StringComparison.Ordinal)
				|| System.ClaimedZones == null || !System.ClaimedZones.Contains(Zone.ZoneID)
				|| !string.Equals(System.SettlementIdentityFirstClaimedZone, Zone.ZoneID,
					StringComparison.Ordinal)
				|| !System.TryGetCurrentIdentity(out string _, out string _)
				|| !KingdomPlots.TryRiteGround(Zone, out int riteX, out int riteY)
				|| riteX != KingdomQuickstartRules.StartCellX
				|| riteY != KingdomQuickstartRules.StartCellY
				|| !KingdomPlots.TrySurveyedHeart(Zone, out KingdomPlotRules.PlotRect survey)
				|| survey.Width != KingdomPlotRules.HugeWidth
				|| survey.Height != KingdomPlotRules.HugeHeight
				|| KingdomPlots.HeartRung(Zone) < 1)
			{
				Failure = "The normal founding transaction did not leave one exact founded heart and city identity.";
				return false;
			}
			return true;
		}

		private static bool VerifyComplete(KingdomSystem System, Zone Zone,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			KingdomQuickstartProfile profile;
			if (!KingdomQuickstartRules.Valid(Receipt)
				|| Receipt.Phase != KingdomQuickstartPhase.Complete
				|| !KingdomQuickstartRules.TryProfile(Receipt.ProfileKey, out profile)
				|| !VerifyFounded(System, Zone, profile, out Failure)
				|| !VerifyWaterGrant(Zone, Zone.FindObjectByID(Receipt.WaterObjectId),
					Receipt, false, out Failure)
				|| !VerifyLarderGrant(Zone, Zone.FindObjectByID(Receipt.LarderObjectId),
					Receipt, false, out Failure)
				|| !VerifyMaterialsGrant(Zone,
					Zone.FindObjectByID(Receipt.StockpileObjectId), Receipt, false, out Failure))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The completed quickstart receipt was invalid.";
				return false;
			}
			if (Receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Included)
				return VerifyAdvisor(Zone, Zone.FindObjectByID(Receipt.AdvisorObjectId),
					Receipt, out Failure);
			if (Receipt.AdvisorDisposition != KingdomQuickstartAdvisorDisposition.Omitted)
			{
				Failure = "The complete receipt had no advisor decision.";
				return false;
			}
			return true;
		}

		private static Cell RoleCell(Zone Zone, int X, int Y)
		{
			return Zone?.GetCell(X, Y);
		}

		private static bool EmptyRoleCell(Cell Cell)
		{
			if (Cell == null || !Cell.IsPassable() || Cell.HasOpenLiquidVolume()) return false;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i]) && (objects[i].IsCreature
					|| KingdomPlots.ReadObject(objects[i]) != KingdomPlotRules.GroundKind.Bare))
					return false;
			return true;
		}

		private static bool ExactRole(Zone Zone, GameObject Object, string Blueprint,
			int X, int Y)
		{
			return GameObject.Validate(Object) && Zone != null
				&& string.Equals(Object.Blueprint, Blueprint, StringComparison.Ordinal)
				&& Object.CurrentZone == Zone && Object.CurrentCell == Zone.GetCell(X, Y)
				&& !string.IsNullOrEmpty(Object.ID);
		}

		private static void ObliterateExact(GameObject Object)
		{
			if (!GameObject.Validate(Object)) return;
			if (Object.Inventory != null)
			{
				List<GameObject> contents = new List<GameObject>(Object.Inventory.Objects);
				for (int i = 0; i < contents.Count; i++)
					if (GameObject.Validate(contents[i]))
						contents[i].Obliterate(null, Silent: true);
			}
			if (GameObject.Validate(Object)) Object.Obliterate(null, Silent: true);
		}
	}
}
