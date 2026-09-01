using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		// ==================================================================================
		// Re-dress: any registered skin, on any standing building, trivially
		// ==================================================================================

		/// <summary>
		/// Applies a registered skin to a standing building. Reads the building's own design
		/// LIVE from the current catalogue rather than from anything cached at the moment it was
		/// raised, which is what lets a skin a mod added after the building went up be offered
		/// here (Addendum 1: "including one a mod added later"). Structural: never changes what
		/// the building is, what it costs to run, or what it produces &mdash; only
		/// <c>Render</c>, through <c>KingdomDesign.ApplyRenderOverrides</c>, unmodified.
		/// </summary>
		public static bool Redress(KingdomSystem System, Zone Z, GameObject Building, string SkinKey, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is re-dressed on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to re-dress.";
				return false;
			}
			if (!KingdomUpgrade.IsFunctionallyBuilt(Building))
			{
				Failure = "The settlement re-dresses what it stands behind. That is not one of its buildings.";
				return false;
			}
			if (HasBlockingReceipt(Building))
			{
				Failure = "That building already has construction work in hand.";
				return false;
			}
			if (KingdomConstruction.HasActiveSubject(System, Z,
				KingdomConstructionRoute.SocketRedress, Building))
			{
				Failure = "That building already has a re-dressing receipt in hand.";
				return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			if (string.IsNullOrEmpty(key))
			{
				key = Building.GetStringProperty(KingdomAdopt.AdoptedKeyProperty);
			}
			if (!KingdomData.TryGetBuilding(key, out KingdomRules.BuildEntry entry))
			{
				Failure = KingdomSocketRules.RefuseUnknownDesign(Building.ShortDisplayName);
				return false;
			}
			if (string.IsNullOrEmpty(SkinKey))
			{
				Failure = "Choose a look to re-dress it in.";
				return false;
			}
			KingdomDesignRules.SkinEntry skin = KingdomDesignRules.FindSkin(entry.Skins, SkinKey);
			if (skin == null)
			{
				Failure = KingdomSocketRules.RefuseUnknownSkin(SkinKey, Building.ShortDisplayName);
				return false;
			}
			KingdomMaterialTally cost = KingdomSocketRules.RedressCost(KingdomMaterials.CostFor(entry.Key));
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(cost);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, claim);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.SocketRedress, Building.CurrentCell, Building,
				entry.Key, SkinKey, 0, claim);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stockpiles could not cover the re-dressing after all.";
				return false;
			}
			KingdomConstruction.Bind(Building, job);
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The re-dressing receipt remains outstanding and will retry without another charge.}}");
				return true;
			}
			if (!KingdomCeremony.PrepareSocketRedressed(System, Building.ShortDisplayName,
				SkinKey, ref job))
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The paid re-dressing telling could not be frozen safely. Its receipt needs inspection.}}");
				return true;
			}
			if (!ProjectRedress(Building, skin, job, out job, out string projectionFailure))
			{
				KingdomGovernanceScope.Commit("redress building");
				System.Ledger.Note("{{r|The paid re-dressing could not yet be verified. Its receipt remains queued.}}");
				KingdomLog.Log("construction: redress waits: " + projectionFailure);
				return true;
			}
			KingdomGovernanceScope.Commit("redress building");
			KingdomCeremony.DispatchPending(System, ref job);
			KingdomLog.Log("socket: redress " + Building.ShortDisplayName + " (" + entry.Key + ") as " + SkinKey);
			return true;
		}

		private static bool ProjectRedress(GameObject Building,
			KingdomDesignRules.SkinEntry Skin, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Building) || Building.CurrentCell == null
				|| !KingdomUpgrade.IsFunctionallyBuilt(Building) || Skin == null)
			{
				Failure = "The paid building is no longer available to re-dress.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (IsRedressed(Building, Skin))
			{
				KingdomConstruction.Complete(ref Updated);
				return true;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			KingdomConstruction.Bind(Building, Updated);
			KingdomDesign.ApplyRenderOverrides(Building, Skin.ColorString, Skin.DetailColor,
				Skin.RenderString, Skin.Tile);
			if (!IsRedressed(Building, Skin))
			{
				Failure = "The new appearance could not be verified on the paid building.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.Complete(ref Updated);
			return true;
		}

		private static bool IsRedressed(GameObject Building, KingdomDesignRules.SkinEntry Skin)
		{
			Render render = GameObject.Validate(Building) ? Building.GetPart<Render>() : null;
			return render != null && Building.CurrentCell != null
				&& (string.IsNullOrEmpty(Skin.ColorString) || render.ColorString == Skin.ColorString)
				&& (string.IsNullOrEmpty(Skin.DetailColor) || render.DetailColor == Skin.DetailColor)
				&& (string.IsNullOrEmpty(Skin.RenderString) || render.RenderString == Skin.RenderString)
				&& (string.IsNullOrEmpty(Skin.Tile) || render.Tile == Skin.Tile);
		}

		// ==================================================================================
		// Charter entry points
		// ==================================================================================

		private static void CollectNearby(Cell Anchor, List<GameObject> Into, Func<GameObject, bool> Predicate)
		{
			if (Anchor == null)
			{
				return;
			}
			foreach (GameObject item in Anchor.GetObjects())
			{
				if (Predicate(item) && !Into.Contains(item))
				{
					Into.Add(item);
				}
			}
		}
	}
}
