using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// SYNTHETIC TOWN, DISCLOSED (issue #159, native runs 7 and 33). The moot yard is gated on a
	/// Town, and production is entitled to shed people its works cannot carry: run 33 lost 25
	/// residents to the water support tally and the roof grace within days of the rung-2 raise,
	/// so the second climb was never assessed as a Town. Subsidence and lodging stay ON. Instead
	/// the fixture stands up, as REAL finished works, what production itself would require for
	/// twenty-five people: roofed lodging rows until the benefit index counts a bed each, and
	/// crewless water works until the summed water carries the population at the stage the
	/// settlement holds. Population equilibrium is then production's own reckoning.
	/// <para>HOW. Each lot is staked receiptless through <c>KingdomPlots.Stake</c> (the
	/// quickstart shelter's own path, <c>World/KingdomQuickstartBootstrap.Shelter.cs</c>), which
	/// keeps it on the shipped schema-zero calendar, and finished with the same compatibility
	/// <c>KingdomPlots.Advance</c> the completed-heart seam uses, so production's own Finish
	/// stamps KingdomBuilt, the build key, the plot id and the architecture designation. Nothing
	/// is hand-stamped, and no crew labour is claimed for any of it. Lodging is then assigned by
	/// <c>KingdomLodging.OnSettlementPass</c>, never by writing a home id.</para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal const string LodgingKey = "tentrow";
		/// <summary>The crewless dew lane, in the order tried; a style may refuse one.</summary>
		internal static readonly string[] WaterKeys = { "catchmentbank", "catchment", "airwellcourt" };
		internal const int MaxLodgingLots = 16;
		internal const int MaxWaterLots = 16;

		private sealed partial class Frame
		{
			internal readonly List<KingdomPlotRules.PlotRect> SeededLots =
				new List<KingdomPlotRules.PlotRect>();
			internal readonly List<string> SeededIds = new List<string>();

			internal void SeedTownIfOwed()
			{
				if (TargetRung < 3) return;
				KingdomSurvey survey = Census();
				GrowthStage stage = KingdomRules.StageFor(System.Population, survey.StorageCapacity);
				Require(stage >= GrowthStage.Town, "taf-camp-town-seed-stage: " + System.Population
					+ " people over " + survey.StorageCapacity + " drams of capacity is a " + stage
					+ ", not the Town the moot yard asks for");
				// A hand-written production field, as KingdomSubsidenceNativeFixture writes it, and
				// LOAD-BEARING here: KingdomZoning.Permits refuses the dew lane's MinStage="Steading"
				// at the fresh camp's Camp stage until a daily pass has raised it, and Stake asks
				// Permits. The daily pass re-reckons the stage through StageWithHysteresis from the
				// same two readings, so nothing here outlives production's own reckoning.
				System.Stage = stage;
				Evidence.Append("\nsynthetic-town stage-derived=").Append(stage)
					.Append("; population=").Append(System.Population)
					.Append("; capacity=").Append(survey.StorageCapacity);
				int beds = 0;
				int lodgingLots = 0;
				while (lodgingLots < MaxLodgingLots && beds < Residents)
				{
					SeedFinishedWork(LodgingKey);
					lodgingLots++;
					string failure;
					Require(KingdomGrowth.TryCountBeds(Zone, out beds, out failure),
						"taf-camp-town-seed-beds-unreadable: " + KingdomScenarioRules.Bounded(failure));
				}
				Require(beds >= Residents, "taf-camp-town-seed-roof: " + lodgingLots
					+ " seeded lodging lot(s) count " + beds + " bed(s) for " + Residents + " people");
				int water = 0;
				int waterLots = 0;
				string waterKey = null;
				while (waterLots < MaxWaterLots
					&& KingdomSubsidenceRules.LevelFromWater(water, stage) < Residents)
				{
					waterKey = SeedFirstPermitted(WaterKeys, waterKey);
					waterLots++;
					water = KingdomSubsidence.ScopedSupports(System, Zone, Census()).Water;
				}
				Require(KingdomSubsidenceRules.LevelFromWater(water, stage) >= Residents,
					"taf-camp-town-seed-water: " + waterLots + " seeded " + waterKey
					+ " lot(s) carry water " + water + ", which supports "
					+ KingdomSubsidenceRules.LevelFromWater(water, stage) + " at " + stage
					+ ", not " + Residents);
				KingdomCatalogueRules.SupportTally tally =
					KingdomSubsidence.ScopedSupports(System, Zone, Census());
				int level = KingdomSubsidenceRules.SupportedLevel(tally, stage, System.Shade);
				Require(level >= Residents, "taf-camp-town-seed-level: the seeded works support "
					+ level + " people at " + stage + ", not " + Residents);
				AssignLodging();
				// The per-lot lines are a row of their own (bookkeeping to the persona matrix), so
				// the setup and check rows keep their reads inside the journal's row cap.
				KingdomScenarioJournal.Append("town-lots", true, "native-camp-heart town-lots="
					+ SeededLots.Count + "; stake-refused=" + RefusedLots.Count + LotEvidence);
				Evidence.Append("\nsynthetic-town-works lodging=").Append(LodgingKey).Append('x')
					.Append(lodgingLots).Append("; beds=").Append(beds).Append("; water=")
					.Append(waterKey ?? "(none)").Append('x').Append(waterLots)
					.Append("; supports water=").Append(tally.Water).Append(" roof=")
					.Append(tally.Roof).Append(" lift=").Append(tally.Lift)
					.Append("; supported level=").Append(level).Append("; stage=").Append(stage)
					.Append("; crewed=false; hand-stamped=false");
			}

			/// <summary>The first key of Keys the settlement's zoning permits, seeded; a key that
			/// was already seeded is kept so the water lane stays one design.</summary>
			private string SeedFirstPermitted(string[] Keys, string Chosen)
			{
				if (Chosen != null) { SeedFinishedWork(Chosen); return Chosen; }
				for (int i = 0; i < Keys.Length; i++)
				{
					KingdomRules.BuildEntry entry;
					string refusal;
					if (KingdomData.TryGetBuilding(Keys[i], out entry) && entry != null
						&& KingdomZoning.Permits(System, Zone.ZoneID, entry, out refusal))
					{
						SeedFinishedWork(Keys[i]);
						return Keys[i];
					}
				}
				Require(false, "taf-camp-town-seed-water-refused: zoning permits none of "
					+ string.Join(",", Keys) + " at " + System.Stage + "/"
					+ KingdomZoning.Tech(System));
				return null;
			}

			/// <summary>One lot of Key: the first candidate production's own preflight accepts,
			/// staked receiptless and finished by production's own calendar. A Stake refusal after
			/// a passed preflight is journaled and the next candidate is tried; the seed refuses
			/// only when the candidates are exhausted (<c>KingdomCampHeartNativeTownLots</c>).</summary>
			private void SeedFinishedWork(string Key)
			{
				KingdomRules.BuildEntry entry;
				KingdomPlotRules.PlotSpec spec = null;
				int width = 0;
				int height = 0;
				Require(KingdomData.TryGetBuilding(Key, out entry) && entry != null
					&& KingdomPlots.TryGetSpec(Key, out spec) && spec != null
					&& KingdomPlotRules.TryDimensions(spec.Size, out width, out height),
					"taf-camp-town-seed-design: " + Key + " is not an authored plotted design");
				KingdomDesignRules.SkinEntry skin = KingdomDesignRules.ResolveDefaultSkinForKeys(
					entry.Skins, KingdomData.StyleKeys(System.Style));
				string skinKey = skin == null ? null : skin.Key;
				GameObject works = null;
				KingdomPlotRules.PlotRect lot = default(KingdomPlotRules.PlotRect);
				KingdomArchitectureIntent intent = null;
				while (works == null)
				{
					KingdomPlots.GroundGrid grid = new KingdomPlots.GroundGrid(Zone);
					Require(TryFindLot(grid, entry, width, height, skinKey, out lot, out intent),
						"taf-camp-town-seed-ground: no " + width + "x" + height + " lot production's "
							+ "preflight accepts remains for " + Key + " after " + SeededLots.Count
							+ " seeded and " + RefusedLots.Count + " stake-refused lot(s)");
					works = KingdomPlots.Stake(System, Zone, lot, entry, spec, grid, skinKey,
						KingdomPlotRules.IsUnderground(Zone.Z));
					if (works != null) break;
					RefusedLots.Add(lot);
					LotEvidence.Append("\nsynthetic-town-stake-refused key=").Append(Key)
						.Append("; rect=").Append(lot.X1).Append(',').Append(lot.Y1).Append(' ')
						.Append(lot.X2).Append(',').Append(lot.Y2)
						.Append("; the architecture log line carries the reason");
				}
				r_KingdomPlotWorks part = works.GetPart<r_KingdomPlotWorks>();
				string plotId = works.GetStringProperty(KingdomPlots.PlotIdProperty);
				Require(part != null && part.DesignKey == Key && !string.IsNullOrEmpty(plotId)
					&& !works.HasIntProperty(KingdomPlots.PlotWorkSchemaProperty)
					&& !works.HasStringProperty(KingdomPlots.PlotWorkSchemaProperty),
					"taf-camp-town-seed-calendar: the staked " + Key + " is not a schema-zero works");
				KingdomPlots.Advance(part, System, checked(part.StartTick + part.TotalTicks));
				GameObject final = FinishedRoot(plotId);
				Require(final != null && KingdomUpgrade.DesignKeyOf(final) == Key
					&& KingdomUpgrade.IsFunctionallyBuilt(final),
					"taf-camp-town-seed-unfinished: " + Key + " at " + lot.X1 + "," + lot.Y1
						+ " did not finish as a built " + Key + "; standing="
						+ (final == null ? "(absent)" : KingdomUpgrade.DesignKeyOf(final)));
				Require(final.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1,
					"taf-camp-town-seed-heart-marked: a seeded lot carries the heart plot mark");
				SeededLots.Add(lot);
				SeededIds.Add(final.IDIfAssigned);
				SeededRoots.Add(final);
				LotEvidence.Append("\nsynthetic-town-lot key=").Append(Key).Append("; rect=")
					.Append(lot.X1).Append(',').Append(lot.Y1).Append(' ').Append(lot.X2)
					.Append(',').Append(lot.Y2).Append("; ingress=").Append(intent.Facing)
					.Append("; lane depth=").Append(LaneDepth)
					.Append("; lane-reserved=").Append(ReservedLaneCells)
					.Append("; heart-envelope-reserved=").Append(ReservedEnvelopeCells)
					.Append("; plot=").Append(plotId)
					.Append("; final=").Append(final.IDIfAssigned);
			}

			/// <summary>The built root standing on a plot id, read off a fresh custody survey so
			/// the object Finish left behind is the one production will count.</summary>
			private GameObject FinishedRoot(string PlotId)
			{
				KingdomSurvey survey = Census();
				for (int i = 0; i < survey.Built.Count; i++)
				{
					GameObject root = survey.Built[i];
					if (GameObject.Validate(root)
						&& root.GetStringProperty(KingdomPlots.PlotIdProperty) == PlotId)
						return root;
				}
				return null;
			}

			/// <summary>Production's own lodging pass, on a bound survey, then a readback that
			/// every enrolled resident was given a home by it.</summary>
			private void AssignLodging()
			{
				KingdomSurvey survey = KingdomSurvey.Take(Zone, System);
				Require(survey != null, "taf-camp-town-seed-survey: the lodging pass has no survey");
				using (survey.BindPass())
				{
					KingdomLodging.OnSettlementPass(System, Zone, survey);
				}
				int housed = 0;
				for (int i = 0; i < Owned.Count; i++)
				{
					GameObject body = Owned[i];
					if (!GameObject.Validate(body) || body.GetIntProperty("KingdomBorn") != 1) continue;
					if (!string.IsNullOrEmpty(body.GetStringProperty(KingdomLodging.HomePlotIdProperty)))
						housed++;
				}
				Require(housed >= Residents, "taf-camp-town-seed-unhoused: the lodging pass housed "
					+ housed + " of " + Residents + " residents");
				Evidence.Append("\nsynthetic-town lodging-pass housed=").Append(housed);
			}
		}
	}
}
