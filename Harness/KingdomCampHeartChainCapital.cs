using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The arcology's own prerequisites, which no rung below it asks for and which the paid chain
	/// therefore never seeded: the arclight knowledge key, the arclight craft level (seeded in
	/// <see cref="KingdomCampHeartChainSupport"/>), four claimed zones, and the crown.
	/// <para>
	/// SYNTHETIC, DISCLOSED, AND ALL THROUGH PRODUCTION APIS. The node key is minted by
	/// <c>KingdomZoning.Learn</c>, which is the same call production's own research completion
	/// makes (Growth/KingdomResearch.Completion.cs:30-33); no research node is completed here. The
	/// three extra zones are taken with <c>KingdomFounding.ClaimZone</c>, the founder's own claim.
	/// The crown hall is staked and finished on the production plot path the eighteen fixture tent
	/// rows already use; the crown itself is never written - it is read back through
	/// <c>KingdomCrown.CrownedOn</c>, which resolves from a standing hall
	/// (Growth/KingdomCrownDiscovery.cs:84-119, Growth/KingdomCrownRules.cs:146-168).
	/// </para>
	/// <para>
	/// This seam spends no turns and drives no improvement.
	/// </para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal const string ArclightNode = "arclight";
		internal const int ArcologyZones = 4;
		private static readonly string[] ChainClaimDirections = { "N", "S", "E", "W" };

		private sealed partial class Frame
		{
			private readonly List<Zone> ChainClaimed = new List<Zone>();
			private readonly List<string> ChainClaimNotes = new List<string>();
			private GameObject ChainCrownHall;
			private string ChainCrownZoneId;

			private void SeedChainCapital()
			{
				long tick = Game.TimeTicks;
				RequireChainSupport();
				Require(KingdomPlots.HeartRung(Zone) == 4,
					"taf-camp-rung5-capital-rung: the capital seed wants a standing rung-four court");
				Require(System.Stage == GrowthStage.City,
					"taf-camp-rung5-capital-stage: the capital seed wants the supported City stage");
				TeachArclightNode();
				ClaimChainTerritory();
				RaiseChainCrownHall();
				ChainCapitalReport = DescribeChainCapital();
				Require(KingdomScenarioJournal.Append("camp-heart-chain-crown", true,
					ChainCapitalReport) == null, "capital seed journal unavailable");
				RequireChainCapitalGates();
				Require(Game.TimeTicks == tick && ReferenceEquals(The.Game, Game),
					"taf-camp-rung5-capital-clock: the capital seed advanced the real clock");
				RequireChainCustody();
			}

			/// <summary>The one roster key the arcology's <c>Knowledge</c> gate names. Craft POINTS
			/// come from the disk lessons in the support seed: a node is worth zero points
			/// (Growth/KingdomZoningRules.cs:234), so the two are separate obligations.</summary>
			private void TeachArclightNode()
			{
				Require(KingdomZoning.Learn(System, KingdomZoningRules.KindNode, ArclightNode),
					"taf-camp-rung5-node-refused: the arclight node key was present or refused");
				Require(KingdomZoning.Tech(System) == TechLevel.Arclight,
					"taf-camp-rung5-craft-short: the keepers do not stand at arclight craft");
			}

			/// <summary>Three more zones, because the arcology declares <c>MinZones="4"</c> and
			/// <c>KingdomZoning.Permits</c> - the UNFILTERED gate Begin runs
			/// (Growth/KingdomUpgrade.14.Begin.cs:97-103) - refuses territory that Assess admits
			/// (Growth/KingdomUpgradeRules.Assessment.cs:11-26).</summary>
			private void ClaimChainTerritory()
			{
				var player = The.Player;
				string standing = player?.CurrentCell == null ? null
					: player.CurrentCell.X + "," + player.CurrentCell.Y;
				foreach (string direction in ChainClaimDirections)
				{
					if (System.ClaimedZones.Count >= ArcologyZones) break;
					Zone neighbour = Zone.GetZoneFromDirection(direction);
					if (neighbour == null)
					{ ChainClaimNotes.Add(direction + ":absent"); continue; }
					if (System.ClaimedZones.Contains(neighbour.ZoneID))
					{ ChainClaimNotes.Add(direction + ":already-held"); continue; }
					if (!KingdomFounding.ZonesAdjacent(Zone.ZoneID, neighbour.ZoneID))
					{ ChainClaimNotes.Add(direction + ":not-adjacent"); continue; }
					if (!KingdomFounding.ClaimZone(neighbour))
					{ ChainClaimNotes.Add(direction + ":refused"); continue; }
					Require(System.ClaimedZones.Contains(neighbour.ZoneID),
						"taf-camp-rung5-claim-unpublished: the claim reported success without a row");
					ChainClaimNotes.Add(direction + ":claimed");
					ChainClaimed.Add(neighbour);
				}
				Require(ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
					&& ReferenceEquals(The.Player, player) && player.CurrentZone == Zone
					&& (standing == null || player.CurrentCell.X + "," + player.CurrentCell.Y == standing),
					"taf-camp-rung5-claim-moved-founder: claiming ground moved the founder or the "
						+ "active zone");
			}

			/// <summary>The crown hall, staked and finished on ground the city now holds. It is an
			/// XL fourteen-by-ten lot (RuntimeData/KingdomBuildings.xml:1380-1386) and does not fit
			/// beside the twenty-by-eighteen heart, the paid tent, eighteen tent rows and eight
			/// eight-by-six air-well footprints on this ground, so it stands on a claimed
			/// neighbour. The city BOOK is what the crown is read from
			/// (Growth/KingdomCrownDiscovery.cs:121-143), and the book spans every zone the city
			/// holds.</summary>
			private void RaiseChainCrownHall()
			{
				Require(KingdomData.TryGetBuilding(KingdomCrownRules.CrownKey, out var entry),
					"authored crown hall missing from the catalogue");
				Require(KingdomPlots.TryGetSpec(KingdomCrownRules.CrownKey, out var spec),
					"authored crown hall plot spec missing");
				string last = null;
				foreach (Zone ground in ChainClaimed)
				{
					if (ChainCrownHall != null) break;
					if (!KingdomPlotRules.TryInterior(ground.Width, ground.Height, out var interior))
					{ last = "claimed ground has no plot interior"; continue; }
					ChainCrownHall = StakeCrownHall(ground, interior, entry, spec, ref last);
				}
				Require(ChainCrownHall != null, "taf-camp-rung5-crownhall-unsited: no claimed "
					+ "ground took the crown hall: " + KingdomScenarioRules.Bounded(last));
			}

			private GameObject StakeCrownHall(Zone Ground, KingdomPlotRules.PlotRect Interior,
				KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, ref string Last)
			{
				// The PLOT tier, not the tier footprint: a crown hall is an XL lot with a
				// fourteen-by-ten building inside it, exactly as the fixture tent rows are M lots.
				Require(KingdomPlotRules.TryDimensions(Spec.Size, out int width, out int height),
					"the authored crown hall plot tier has no dimensions");
				for (int y = Interior.Y1; y + height - 1 <= Interior.Y2; y++)
					for (int x = Interior.X1; x + width - 1 <= Interior.X2; x++)
					{
						var rect = new KingdomPlotRules.PlotRect(x, y, x + width - 1, y + height - 1);
						if (!KingdomPlotRules.Fits(rect, Interior))
						{ Last = "candidate leaves the claimed ground's plot interior"; continue; }
						if (KingdomPlotRules.CrowdsExisting(rect, KingdomPlots.ReadPlots(Ground)))
						{ Last = "candidate crowds an existing plot's reserved lane"; continue; }
						var grid = new KingdomPlots.GroundGrid(Ground);
						if (grid.AnyRefusal(rect))
						{ Last = "candidate contains protected or liquid ground"; continue; }
						if (!KingdomPlots.TryPreparePlotPayload(System, Ground, rect, Entry.Key,
							Entry.Category, null, out _, out _, out Last)) continue;
						var work = KingdomPlots.Stake(System, Ground, rect, Entry, Spec, grid,
							null, false);
						if (work == null) { Last = "crown hall stake refused after preflight"; continue; }
						var works = work.GetPart<XRL.World.Parts.r_KingdomPlotWorks>();
						Require(works != null, "staked crown hall lacks exact works");
						KingdomPlots.Advance(works, System,
							checked(works.StartTick + works.TotalTicks));
						Require(KingdomUpgrade.DesignKeyOf(work) == KingdomCrownRules.CrownKey
							&& KingdomUpgrade.IsFunctionallyBuilt(work)
							&& KingdomArchitectureStamper.TryVerifyComplete(work, Ground, out Last),
							"taf-camp-rung5-crownhall-unfinished: " + Last);
						ChainCrownZoneId = Ground.ZoneID;
						return work;
					}
				return null;
			}

			/// <summary>Everything the arcology's own gates ask, read back through production and
			/// asserted only AFTER the journal row is written, so a refusal is named rather than
			/// inferred. <c>CanReserveAt</c> is the hosted-shell half
			/// (Growth/KingdomHostedArcology.Authority.cs:19-48) and is read, never reserved.
			/// </summary>
			private void RequireChainCapitalGates()
			{
				Require(System.ClaimedZones.Count >= ArcologyZones,
					"taf-camp-rung5-territory-short: the city holds "
						+ System.ClaimedZones.Count + " zone(s), not " + ArcologyZones);
				Require(KingdomCrown.Enabled,
					"taf-camp-rung5-capital-option-off: the capital option is disabled");
				Require(KingdomCrown.CrownedOn(System, Zone.ZoneID),
					"taf-camp-rung5-uncrowned: the standing crown hall did not crown this ground; "
						+ "capital=" + KingdomScenarioRules.Bounded(KingdomCrown.CapitalName(System)));
				Require(KingdomHostedArcology.CanReserveAt(System, Zone.ZoneID, out string failure),
					"taf-camp-rung5-shell-refused: " + KingdomScenarioRules.Bounded(failure));
				Require(KingdomData.TryGetBuilding(KingdomHostedArcology.ArcologyKey, out var entry),
					"the authored arcology entry is absent");
				var verdict = KingdomZoning.Judge(System, Zone.ZoneID, entry);
				Require(verdict.Verdict == ZoningVerdict.Permitted,
					"taf-camp-rung5-zoning-refused: " + verdict.Verdict + "; detail="
						+ KingdomScenarioRules.Bounded(verdict.Detail));
			}

			private string DescribeChainCapital()
			{
				var text = new StringBuilder();
				text.Append("claimed-zones=").Append(System.ClaimedZones.Count)
					.Append("; claims=").Append(string.Join(",", ChainClaimNotes))
					.Append("; crown-hall=").Append(ChainCrownHall?.IDIfAssigned ?? "absent")
					.Append("; crown-zone=").Append(ChainCrownZoneId ?? "absent")
					.Append("; capital=").Append(KingdomScenarioRules.Bounded(
						KingdomCrown.CapitalName(System)))
					.Append("; crowned=").Append(KingdomCrown.CrownedOn(System, Zone.ZoneID))
					.Append("; capital-option=").Append(KingdomCrown.Enabled)
					.Append("; craft=").Append(KingdomZoning.Tech(System))
					.Append("; node-arclight=true; synthetic-claims=true; synthetic-crown-hall=true")
					.Append("; synthetic-knowledge=true; no-turns=true");
				return text.ToString();
			}
		}
	}
}
