using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomData
	{
		private static void HandleBuilding(XmlDataHelper xml)
		{
			// One read of every attribute, into one draft, before anything is parsed. Merge-by-key
			// happens on the raw strings (KingdomMergeRules): a later <building Key="X"> overrides the
			// attributes it names, leaves the ones it omits standing, and appends its skins, so every
			// parser below goes on reading exactly the string it always read and none of them has to
			// learn that catalogues layer. Reading each attribute unconditionally is required either
			// way: the engine records which attributes a pass asked for and warns about the rest.
			BuildingDraft declared = new BuildingDraft(xml.GetAttribute("Key"));
			declared.Set(KingdomMergeRules.AttrDisplayName, xml.GetAttribute("DisplayName"));
			declared.Set(KingdomMergeRules.AttrBlueprint, xml.GetAttribute("Blueprint"));
			declared.Set(KingdomMergeRules.AttrCost, xml.GetAttribute("Cost"));
			declared.Set(KingdomMergeRules.AttrTicks, xml.GetAttribute("Ticks"));
			declared.Set(KingdomMergeRules.AttrStyles, xml.GetAttribute("Styles"));
			declared.Set(KingdomMergeRules.AttrCategory, xml.GetAttribute("Category"));
			declared.Set(KingdomMergeRules.AttrMinStage, xml.GetAttribute("MinStage"));
			declared.Set(KingdomMergeRules.AttrStaff, xml.GetAttribute("Staff"));
			declared.Set(KingdomMergeRules.AttrManning, xml.GetAttribute("Manning"));
			declared.Set(KingdomMergeRules.AttrDefence, xml.GetAttribute("Defence"));
			declared.Set(KingdomMergeRules.AttrAdoptable, xml.GetAttribute("Adoptable"));
			declared.Set(KingdomMergeRules.AttrCarries, xml.GetAttribute("Carries"));
			declared.Set(KingdomMergeRules.AttrMaterials, xml.GetAttribute("Materials"));
			declared.Set(KingdomMergeRules.AttrDistricts, xml.GetAttribute("Districts"));
			declared.Set(KingdomMergeRules.AttrMinZones, xml.GetAttribute("MinZones"));
			declared.Set(KingdomMergeRules.AttrKnowledge, xml.GetAttribute("Knowledge"));
			declared.Set(KingdomMergeRules.AttrMinTech, xml.GetAttribute("MinTech"));
			declared.Set(KingdomMergeRules.AttrCovenant, xml.GetAttribute("Covenant"));
			declared.Set(KingdomMergeRules.AttrMinStanding, xml.GetAttribute("MinStanding"));
			declared.Set(KingdomMergeRules.AttrBuilders, xml.GetAttribute("Builders"));
			declared.Set(KingdomMergeRules.AttrCreed, xml.GetAttribute("Creed"));
			declared.Set(KingdomMergeRules.AttrCreedShare, xml.GetAttribute("CreedShare"));
			declared.Set(KingdomMergeRules.AttrStrata, xml.GetAttribute("Strata"));
			declared.Set(KingdomMergeRules.AttrMegastructure, xml.GetAttribute("Megastructure"));
			declared.Set(KingdomMergeRules.AttrCapital, xml.GetAttribute("Capital"));
			declared.Set(KingdomMergeRules.AttrSatellite, xml.GetAttribute("Satellite"));
			declared.Set(KingdomMergeRules.AttrUpgradesTo, xml.GetAttribute("UpgradesTo"));
			declared.Set(KingdomMergeRules.AttrUpgradeCost, xml.GetAttribute("UpgradeCost"));
			declared.Set(KingdomMergeRules.AttrUpgradeTicks, xml.GetAttribute("UpgradeTicks"));
			declared.Set(KingdomMergeRules.AttrUpgradeCrew, xml.GetAttribute("UpgradeCrew"));
			declared.Set(KingdomMergeRules.AttrUpgradeMinStage, xml.GetAttribute("UpgradeMinStage"));
			declared.Set(KingdomMergeRules.AttrUpgradeMaterials, xml.GetAttribute("UpgradeMaterials"));
			declared.Set(KingdomMergeRules.AttrPlot, xml.GetAttribute("Plot"));
			declared.Set(KingdomMergeRules.AttrOpen, xml.GetAttribute("Open"));
			declared.Set(KingdomMergeRules.AttrSky, xml.GetAttribute("Sky"));
			declared.Set(KingdomMergeRules.AttrContents, xml.GetAttribute("Contents"));
			declared.Set(KingdomMergeRules.AttrFootprint, xml.GetAttribute("Footprint"));
			declared.Set(KingdomMergeRules.AttrRoof, xml.GetAttribute("Roof"));
			declared.Set(KingdomMergeRules.AttrProvides, xml.GetAttribute("Provides"));
			declared.Set(KingdomMergeRules.AttrCloseness, xml.GetAttribute("Closeness"));
			declared.Set(KingdomMergeRules.AttrReach, xml.GetAttribute("Reach"));
			declared.Set(KingdomMergeRules.AttrCrewNeeds, xml.GetAttribute("CrewNeeds"));
			declared.Set(KingdomMergeRules.AttrBits, xml.GetAttribute("Bits"));
			declared.Set(KingdomMergeRules.AttrExotics, xml.GetAttribute("Exotics"));
			declared.Set(KingdomMergeRules.AttrRefines, xml.GetAttribute("Refines"));
			declared.Set(KingdomMergeRules.AttrPurpose, xml.GetAttribute("Purpose"));
			declared.Set(KingdomMergeRules.AttrPurposeSite, xml.GetAttribute("PurposeSite"));
			declared.Set(KingdomMergeRules.AttrPurposeCargoKey, xml.GetAttribute("PurposeCargoKey"));
			declared.Set(KingdomMergeRules.AttrPurposeCargoName, xml.GetAttribute("PurposeCargoName"));
			declared.Set(KingdomMergeRules.AttrPurposeCargoMaterial, xml.GetAttribute("PurposeCargoMaterial"));
			declared.Set(KingdomMergeRules.AttrPurposeCargoWater, xml.GetAttribute("PurposeCargoWater"));
			declared.Set(KingdomMergeRules.AttrPurposeCargoCost, xml.GetAttribute("PurposeCargoCost"));
			declared.Set(KingdomMergeRules.AttrPurposeProducers, xml.GetAttribute("PurposeProducers"));
			declared.Set(KingdomMergeRules.AttrPurposeEffect, xml.GetAttribute("PurposeEffect"));
			BuildingDraft design = KingdomMergeRules.Absorb(declared);
			if (!KingdomRules.TryParseBuildAttributes(design.Key, design.Get(KingdomMergeRules.AttrDisplayName), design.Get(KingdomMergeRules.AttrBlueprint), design.Get(KingdomMergeRules.AttrCost), design.Get(KingdomMergeRules.AttrTicks), design.Get(KingdomMergeRules.AttrStyles), design.Get(KingdomMergeRules.AttrCategory), design.Get(KingdomMergeRules.AttrMinStage), design.Get(KingdomMergeRules.AttrStaff), design.Get(KingdomMergeRules.AttrManning), design.Get(KingdomMergeRules.AttrDefence), out var entry, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				// Nothing is registered and nothing already registered is cleared: a malformed entry
				// does not replace the design of the same key that is already loaded, so it must not
				// replace that design's gate or chain either. The draft is kept even so, so a later
				// file that completes a half-declaration still can.
				SkipChildren(xml);
				return;
			}
			if (!KingdomZoningRules.TryParseCovenantAttributes(design.Key,
				design.Get(KingdomMergeRules.AttrCovenant), design.Get(KingdomMergeRules.AttrMinStanding),
				out CovenantGate covenant, out string covenantError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + covenantError);
				SkipChildren(xml);
				return;
			}
			if (!covenant.IsOpen && Factions.GetIfExists(covenant.Faction) == null)
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + design.Key
					+ " names unknown Covenant faction " + covenant.Faction);
				SkipChildren(xml);
				return;
			}
			entry.Carries = design.Get(KingdomMergeRules.AttrCarries);
			if (!KingdomAdoptRules.TryParseAdoptable(design.Get(KingdomMergeRules.AttrAdoptable),
				out entry.Adoptable, out string adoptableError))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + design.Key
					+ " " + adoptableError);
				SkipChildren(xml);
				return;
			}
			if (entry.Adoptable)
			{
				KingdomPlotRules.PlotSpec adoptionSpec;
				string adoptionError;
				if (!KingdomPlotRules.TryParsePlotAttributes(design.Key,
					design.Get(KingdomMergeRules.AttrPlot), design.Get(KingdomMergeRules.AttrOpen),
					design.Get(KingdomMergeRules.AttrSky), design.Get(KingdomMergeRules.AttrContents),
					design.Get(KingdomMergeRules.AttrFootprint), design.Get(KingdomMergeRules.AttrRoof),
					out adoptionSpec, out adoptionError)
					|| !KingdomAdoptabilityRules.TryClassify(design.Key, entry.Category,
						adoptionSpec.Size, adoptionSpec.Open, out _, out adoptionError))
				{
					MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + design.Key
						+ " has unsafe Adoptable declaration (" + adoptionError + ")");
					SkipChildren(xml);
					return;
				}
			}
			entry.Materials = design.Get(KingdomMergeRules.AttrMaterials);
			entry.Skins = design.Skins;
			entry.CovenantFaction = covenant.Faction;
			entry.CovenantMinStanding = covenant.MinStanding;
			KingdomZoning.RegisterGate(entry.Key, design.Get(KingdomMergeRules.AttrDistricts), design.Get(KingdomMergeRules.AttrMinZones), design.Get(KingdomMergeRules.AttrKnowledge), design.Get(KingdomMergeRules.AttrMinTech),
				design.Get(KingdomMergeRules.AttrBuilders), design.Get(KingdomMergeRules.AttrCreed), design.Get(KingdomMergeRules.AttrCreedShare), design.Get(KingdomMergeRules.AttrStrata),
				design.Get(KingdomMergeRules.AttrMegastructure), design.Get(KingdomMergeRules.AttrCapital));
			// Beside the gate and out of the same merged draft, because it IS a gate -- one asked of
			// the whole realm rather than of this ground, which is why it keeps its own small
			// registry instead of a field on ZoneGate (KingdomSatellite says why at length).
			KingdomSatellite.Declare(entry.Key, design.Get(KingdomMergeRules.AttrSatellite));
			KingdomUpgrade.RegisterChain(entry.Key, design.Get(KingdomMergeRules.AttrUpgradesTo), design.Get(KingdomMergeRules.AttrUpgradeCost), design.Get(KingdomMergeRules.AttrUpgradeTicks), design.Get(KingdomMergeRules.AttrUpgradeCrew), design.Get(KingdomMergeRules.AttrUpgradeMinStage));
			KingdomMaterials.RegisterCost(entry.Key, design.Get(KingdomMergeRules.AttrMaterials), design.Get(KingdomMergeRules.AttrUpgradeMaterials));
			// Beside the material cost and out of the same merged draft, so a later file that
			// re-prices a design in bits layers exactly the way one that re-prices it in timber does.
			KingdomMaterials.RegisterHighCraft(entry.Key, design.Get(KingdomMergeRules.AttrBits), design.Get(KingdomMergeRules.AttrExotics));
			// What makes a building a yard, and the whole of it: a third party's own sawmill is a
			// sawyer's yard the moment it declares Refines, and the build gate counts it like ours.
			KingdomMaterials.RegisterRefinery(entry.Key, design.Get(KingdomMergeRules.AttrRefines));
			KingdomPurpose.RegisterDefinition(entry.Key,
				design.Get(KingdomMergeRules.AttrPurpose),
				design.Get(KingdomMergeRules.AttrPurposeSite),
				design.Get(KingdomMergeRules.AttrPurposeCargoKey),
				design.Get(KingdomMergeRules.AttrPurposeCargoName),
				design.Get(KingdomMergeRules.AttrPurposeCargoMaterial),
				design.Get(KingdomMergeRules.AttrPurposeCargoWater),
				design.Get(KingdomMergeRules.AttrPurposeCargoCost),
				design.Get(KingdomMergeRules.AttrPurposeProducers),
				design.Get(KingdomMergeRules.AttrPurposeEffect));
			// The footprint and roof are registered post-merge like everything else: a file that
			// shrinks the plot and a file that declares the footprint are two files, and only the
			// merged pair is the design the validator can check.
			KingdomPlots.RegisterSpec(entry.Key, design.Get(KingdomMergeRules.AttrPlot), design.Get(KingdomMergeRules.AttrOpen), design.Get(KingdomMergeRules.AttrSky), design.Get(KingdomMergeRules.AttrContents), design.Get(KingdomMergeRules.AttrFootprint), design.Get(KingdomMergeRules.AttrRoof));
			// After the plot spec, and for the same reason it is registered post-merge: what a design
			// offers a resident is the tags its author declared plus the ones its roof gives, and the
			// roof is only settled once the merged spec is in.
			KingdomQol.RegisterProvides(entry.Key, design.Get(KingdomMergeRules.AttrProvides));
			// Last of the post-merge registrations, and after the plot spec on purpose: a design that
			// declares no Closeness is measured, and what it is measured against is the beds in its
			// merged Carries over the footprint the merged plot spec just registered. Registering it
			// before either would measure a design nobody had finished declaring.
			KingdomLodging.RegisterCloseness(entry.Key, design.Get(KingdomMergeRules.AttrCloseness));
			// After the plot spec and the chain, both of which the derivation reads: a design that
			// declares no Reach is placed on the ladder by the ground it stands on and its place in
			// its own chain, and only a design that overrides it registers anything here.
			KingdomReach.RegisterReach(entry.Key, design.Get(KingdomMergeRules.AttrReach));
			KingdomCrews.RegisterCrewNeeds(entry.Key, design.Get(KingdomMergeRules.AttrCrewNeeds));
			KingdomRules.BuildEntry parsed = entry;
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Key == entry.Key)
				{
					// In place, so the catalogue keeps first-declaration order: a mod that re-costs
					// the tent does not move the tent to the bottom of the founder's list.
					_buildings[i] = entry;
					entry = null;
					break;
				}
			}
			if (entry != null)
			{
				_buildings.Add(entry);
			}
			// HandleNodes stands in for DoneWithElement: it returns at once on a self-closing
			// <building/>, which is every entry that declares no skins, and otherwise dispatches
			// the children.
			xml.HandleNodes(new Dictionary<string, Action<XmlDataHelper>>
			{
				{
					"skin",
					delegate(XmlDataHelper skinXml)
					{
						HandleSkin(skinXml, parsed, design);
					}
				}
			});
		}

	}
}
