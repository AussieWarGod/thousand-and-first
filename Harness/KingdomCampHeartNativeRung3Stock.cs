using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The synthetic stock and craft the rung 2 -> 3 leg establishes, and the arithmetic that
	/// keeps it honest. Everything here is fixture setup, all of it disclosed in the report line:
	/// the designs taught so the keepers reach the craft level the moot yard is gated on, and the
	/// authored rung-3 bill minted into the camp store. Nothing here begins, funds or advances
	/// anything, and nothing here pushes a dedicated stockpile past its declared capacity.
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>
			/// SYNTHETIC CRAFT, DISCLOSED. The moot yard is gated at the workshop craft level
			/// (RuntimeData/KingdomBuildings.xml:652 MinTech="workshop"), which production reckons
			/// from the keepers' own roster: one point per taught design, five points for workshop
			/// (Growth/KingdomZoningRules.cs:210,237). Nothing here writes the roster or the
			/// level: each design is taught through the production API a founder's own data disk
			/// would use, and the level is READ BACK afterwards. A rung-2 run teaches nothing.
			/// </summary>
			private void TeachCraftIfOwed()
			{
				if (TargetRung < 3) return;
				TechLevel before = KingdomZoning.Tech(System);
				for (int i = 0; i < WorkshopDisks; i++)
					Require(KingdomZoning.Learn(System, "disk",
						"camp heart fixture design " + (i + 1)),
						"taf-camp-craft-unlearned: the keepers refused a synthetic taught design");
				TechLevel after = KingdomZoning.Tech(System);
				Require(after >= TechLevel.Workshop,
					"taf-camp-craft-short: the keepers stand at " + after
						+ " and the moot yard wants " + TechLevel.Workshop);
				Evidence.Append("\nsynthetic-craft disks=").Append(WorkshopDisks)
					.Append("; craft=").Append(before).Append(" -> ").Append(after);
			}

			/// <summary>
			/// SYNTHETIC STOCK, DISCLOSED, AND RELATIVE TO WHAT IS OBSERVED. The authored rung
			/// 2 -> 3 bill, minted into the SAME camp store through the same production custody
			/// path the rung-2 fill uses, at the phase-2 boundary.
			///
			/// <para>WHAT NATIVE RUN 5 TAUGHT US. The first attempt assumed the store held exactly
			/// the unasked units once the rung-2 bill was spent, and refused at 46. The store is
			/// not the fixture's to predict: the settlement's own keepers deposit what its yard
			/// work makes into the dedicated stockpile while the rung is built, so the count at
			/// this boundary is the unasked units PLUS whatever production earned in that window.
			/// Both numbers are now read, never assumed, and both are journaled per material.</para>
			///
			/// <para>CAPACITY IS NEVER EXCEEDED. A dedicated stockpile holds what its blueprint
			/// declares (<c>Core/KingdomRules.MaterialStores.cs</c>, read through
			/// <c>KingdomSurvey.StockCapacityOf</c>), and a delivery that finds no room is turned
			/// away rather than overfilling it. A fixture that added units over that line would be
			/// asserting a settlement state production can never reach, so this refuses instead,
			/// with the whole census in the refusal, and nothing is minted.</para>
			/// </summary>
			internal void MintRung3Bill()
			{
				Require(TargetRung >= 3, "taf-camp-rung3-mint-unsealed: this run climbs one rung");
				Require(MintedRung3.Count == 0, "taf-camp-rung3-mint-twice: the rung-3 bill is "
					+ "already minted");
				int bill = Rung3TimberUnits + Rung3StoneUnits + Rung3ShapedTimberUnits;
				int capacity = KingdomSurvey.StockCapacityOf(Store);
				int before = KingdomSurvey.StockHeldIn(Store);
				List<GameObject> bodies;
				List<KingdomCampHeartNativeCensus.Unit> observed = ContentUnits(out bodies);
				string census = KingdomCampHeartNativeCensus.Describe(observed);
				Evidence.Append("\nsynthetic-rung3-bill observed-before=").Append(before)
					.Append("; capacity=").Append(capacity).Append("; bill=").Append(bill)
					.Append("; observed census=").Append(census);
				// PREFERRED: the whole authored bill on top of whatever stands, so the settlement
				// pays this rung out of units this fixture is accountable for. When production's
				// own deposits have left no room for the whole bill, only the SHORTFALL per
				// material is minted -- the store must end up holding at least the bill, and a
				// fixture may not push a dedicated stockpile past the capacity its blueprint
				// declares. Which path ran is journaled either way, and neither invents room.
				bool whole = before + bill <= capacity;
				int timber = whole ? Rung3TimberUnits
					: Short(observed, KingdomMaterial.Timber, Rung3TimberUnits);
				int stone = whole ? Rung3StoneUnits
					: Short(observed, KingdomMaterial.Stone, Rung3StoneUnits);
				int shaped = whole ? Rung3ShapedTimberUnits
					: Short(observed, KingdomMaterial.ShapedTimber, Rung3ShapedTimberUnits);
				int minting = timber + stone + shaped;
				Evidence.Append("\nsynthetic-rung3-bill whole-bill=").Append(whole)
					.Append("; minting=").Append(minting).Append("; timber=").Append(timber)
					.Append("; stone=").Append(stone).Append("; shapedtimber=").Append(shaped);
				Require(before + minting <= capacity, "taf-camp-rung3-no-room: the store holds "
					+ before + " of " + capacity + " unit(s) and the authored rung-3 bill still "
					+ "needs " + minting + " more; the fixture will not mint over a declared "
					+ "capacity. Observed: " + census);
				Mint(KingdomMaterial.Timber, timber, MintedRung3);
				Mint(KingdomMaterial.Stone, stone, MintedRung3);
				Mint(KingdomMaterial.ShapedTimber, shaped, MintedRung3);
				Require(MintedRung3.Count == minting,
					"taf-camp-rung3-mint-short: the rung-3 bill did not mint whole: "
						+ MintedRung3.Count + " of " + minting);
				int after = KingdomSurvey.StockHeldIn(Store);
				List<KingdomCampHeartNativeCensus.Unit> standing = ContentUnits(out bodies);
				string held = KingdomCampHeartNativeCensus.Describe(standing);
				Require(after == before + minting, "taf-camp-rung3-store-fill: the store holds "
					+ after + " unit(s), not the observed " + before + " plus the " + minting
					+ " unit(s) minted. Before: " + census + ". After: " + held);
				RequireBillMaterialStanding(standing, KingdomMaterial.Timber, Rung3TimberUnits,
					held);
				RequireBillMaterialStanding(standing, KingdomMaterial.Stone, Rung3StoneUnits,
					held);
				RequireBillMaterialStanding(standing, KingdomMaterial.ShapedTimber,
					Rung3ShapedTimberUnits, held);
				Evidence.Append("\nsynthetic-rung3-bill minted=").Append(MintedRung3.Count)
					.Append("; authored timber=").Append(Rung3TimberUnits)
					.Append("; authored stone=").Append(Rung3StoneUnits)
					.Append("; authored shapedtimber=").Append(Rung3ShapedTimberUnits)
					.Append("; observed-after=").Append(after).Append("; capacity=")
					.Append(capacity).Append("; store census=").Append(held);
			}

			/// <summary>
			/// DIAGNOSTIC ONLY, ASSERTS NOTHING. The ground the moot yard would have to annex:
			/// the successor rect production itself resolves for the next rung, minus the rect the
			/// waterstone already stands on. A rung-3 refusal for want of ground is the likeliest
			/// next RED -- the 8x6 to 12x10 growth is seventy-two new cells, and twenty-five
			/// enrolled bodies wander over them -- so what stands on that ring is journaled before
			/// the settlement pass is ever asked to commission it. Read-only: no cell is cleared,
			/// no body is moved, and a failure to resolve is recorded rather than raised.
			/// </summary>
			internal void RecordAnnexGround()
			{
				try
				{
					GameObject standing = SecondStanding;
					KingdomPlotRules.PlotRect held;
					string failure = null;
					if (!GameObject.Validate(standing)
						|| !KingdomPlots.TryReadRect(standing, out held)
						|| !KingdomArchitectureRuntime.TryRead(standing, out var before,
							out failure)
						|| !KingdomArchitectureRuntime.TryPrepareSuccessor(System, Zone, before,
							ThirdRungKey, out var after, out failure))
					{
						Evidence.Append("\nannex-ground-unresolved=")
							.Append(KingdomScenarioRules.Bounded(failure));
						return;
					}
					int cells = 0;
					int living = 0;
					int solid = 0;
					int objects = 0;
					string first = null;
					for (int y = after.Rect.Y1; y <= after.Rect.Y2; y++)
						for (int x = after.Rect.X1; x <= after.Rect.X2; x++)
						{
							if (x >= held.X1 && x <= held.X2 && y >= held.Y1 && y <= held.Y2)
								continue;
							cells++;
							Cell cell = Zone.GetCell(x, y);
							if (cell == null) continue;
							foreach (GameObject item in cell.GetObjects())
							{
								if (!GameObject.Validate(item)) continue;
								objects++;
								if (item.IsAlive) living++;
								if (item.Physics != null && item.Physics.Solid) solid++;
								if (first == null) first = item.Blueprint + "@" + x + "," + y;
							}
						}
					Evidence.Append("\nannex-ground rect=").Append(after.Rect.X1).Append(',')
						.Append(after.Rect.Y1).Append(' ').Append(after.Rect.X2).Append(',')
						.Append(after.Rect.Y2).Append("; held=").Append(held.X1).Append(',')
						.Append(held.Y1).Append(' ').Append(held.X2).Append(',').Append(held.Y2)
						.Append("; annex cells=").Append(cells).Append("; objects=")
						.Append(objects).Append("; living=").Append(living).Append("; solid=")
						.Append(solid).Append("; first=").Append(first ?? "(none)");
				}
				catch (Exception error)
				{
					// Diagnostics must never replace or invent a production refusal.
					Evidence.Append("\nannex-ground-error=").Append(KingdomScenarioRules.Bounded(
						error.GetType().Name + ": " + error.Message));
				}
			}

			/// <summary>Units of one material the store still lacks against the authored bill.
			/// Production's own deposits count: a bill term already standing in the store is not
			/// minted twice.</summary>
			private int Short(List<KingdomCampHeartNativeCensus.Unit> Present,
				KingdomMaterial Material, int Units)
			{
				string blueprint = KingdomMaterials.BlueprintFor(Material);
				int found = 0;
				for (int i = 0; i < Present.Count; i++)
					if (Present[i] != null && Present[i].Blueprint == blueprint)
						found += Present[i].RawCount > 0 ? Present[i].RawCount : 1;
				return found >= Units ? 0 : Units - found;
			}

			/// <summary>One material of the authored bill, counted where it must be countable:
			/// in the store, at or above the units the bill asks for. Production may have
			/// deposited some of it already, which is why this is "at least" and the total above
			/// is exact.</summary>
			private void RequireBillMaterialStanding(
				List<KingdomCampHeartNativeCensus.Unit> Present, KingdomMaterial Material,
				int Units, string Census)
			{
				string blueprint = KingdomMaterials.BlueprintFor(Material);
				int found = 0;
				for (int i = 0; i < Present.Count; i++)
					if (Present[i] != null && Present[i].Blueprint == blueprint)
						found += Present[i].RawCount > 0 ? Present[i].RawCount : 1;
				Require(found >= Units, "taf-camp-rung3-bill-short: the store holds " + found
					+ " unit(s) of " + Material + " (" + blueprint + ") and the authored rung-3 "
					+ "bill asks for " + Units + ". Observed: " + Census);
			}
		}
	}
}
