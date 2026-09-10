using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Phase dispatch for <see cref="KingdomForageNativeChecks"/>. Every phase reads
	/// the real <c>r_KingdomForage</c> part the settlement pass maintains and the real physical
	/// stockpile; none of it calls the private duty directly.</summary>
	internal static partial class KingdomForageNativeChecks
	{
		private sealed partial class Frame
		{
			private const int ReservedTopUp = 8;

			internal void Check()
			{
				Require(Phase >= 1 && !Done, "forage setup did not run");
				Require(!KingdomScenarioAdvance.Pending, "turns are still owed");
				r_KingdomForage state = Heart.GetPart<r_KingdomForage>();
				Require(state != null, "the heart lost its forage part");
				switch (Phase)
				{
					case 1: Phase1(state); break;
					case 2: Phase2(state); break;
					case 3: Phase3(state); break;
					case 4: Phase4(state); break;
					case 5: Phase5(state); Done = true; Armed = false; break;
					default: Require(false, "forage check ran past its final phase"); break;
				}
			}

			/// <summary>The checkpoint has attached; zero on a fresh part means never worked, so
			/// no assertion is made about cutting yet (STANDARDS: fresh attachment forages
			/// nothing on its own first pass).</summary>
			private void Phase1(r_KingdomForage state)
			{
				Require(state.LastWorkedTick > 0, "the forage checkpoint never attached");
				Phase = 2;
				Evidence.Append("\nphase1 tick=").Append(Game.TimeTicks)
					.Append("; checkpoint=").Append(state.LastWorkedTick);
			}

			/// <summary>Real removal paired with real stock gain, and every exclusion untouched.
			/// </summary>
			private void Phase2(r_KingdomForage state)
			{
				for (int i = 0; i < Eligible.Length; i++)
					Require(!GameObject.Validate(Eligible[i]),
						"eligible plant " + i + " was not cut by the real settlement pass");
				RequireUntouched(Tree, "tree");
				RequireUntouched(Owned, "owned plant");
				RequireUntouched(Food, "food plant");
				RequireUntouched(Protected, "protected plant");
				RequireUntouched(PlotPlant, "in-plot plant");
				RequireUntouched(Canvas, "camp canvas");
				int held = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(held == BrushBefore + Eligible.Length, "stock gained " + (held - BrushBefore)
					+ " brush, not exactly " + Eligible.Length + " for the " + Eligible.Length
					+ " plants physically removed");
				Phase = 3;
				Evidence.Append("\nphase2 tick=").Append(Game.TimeTicks)
					.Append("; cut=").Append(Eligible.Length).Append("; brush now=").Append(held)
					.Append("; exclusions untouched=tree,owned,food,protected,plot,canvas");
			}

			/// <summary>Exhaustion: no candidates remain, so the once-only notice fires. A new
			/// plant is then introduced so the next phase proves the flag re-arms.</summary>
			private void Phase3(r_KingdomForage state)
			{
				Require(state.NoBrushAnnounced, "exhaustion did not announce with no candidates left");
				Require(!state.EnoughAnnounced, "the ceiling announced before it was ever reached");
				ExtraPlant = Plant("Plant", NearRite(8));
				Phase = 4;
				Evidence.Append("\nphase3 tick=").Append(Game.TimeTicks)
					.Append("; exhausted=true; extra=").Append(EvidenceOf(ExtraPlant));
			}

			/// <summary>Re-arm: the new plant is cut and the exhaustion flag clears. Then the
			/// store is topped up to the ceiling with RESERVED units (marked, not counted by the
			/// available-only Tally) so the next phase proves the ceiling counts them anyway.
			/// </summary>
			private void Phase4(r_KingdomForage state)
			{
				Require(!GameObject.Validate(ExtraPlant), "the re-armed plant was not cut");
				Require(!state.NoBrushAnnounced, "exhaustion did not re-arm once a plant appeared");
				int held = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				int afterExtra = BrushBefore + Eligible.Length + 1;
				Require(held == afterExtra, "stock did not gain exactly one more brush for the "
					+ "re-armed plant");
				string blueprint = KingdomMaterials.BlueprintFor(KingdomMaterial.Brush);
				for (int i = 0; i < ReservedTopUp; i++)
				{
					GameObject brush = GameObject.Create(blueprint);
					Require(GameObject.Validate(brush), "the brush blueprint produced no object");
					GameObject accepted = StockpileContainer.Inventory.AddObject(brush, null,
						Silent: true, NoStack: true);
					Require(ReferenceEquals(accepted, brush), "the topped-up brush did not land "
						+ "as its own physical unit");
					// The real production marker KingdomConstructionInputLeaseAuthority.CanUseMaterial
					// reads: a marked unit is routed to a job and excluded from the available-only
					// Tally, but stays physically in the stockpile.
					brush.SetStringProperty(KingdomConstruction.InputMarkerProperty,
						"native-forage-reservation");
				}
				int heldAfterTopUp = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(heldAfterTopUp == afterExtra, "the reserved top-up leaked into the "
					+ "available-only Tally: " + afterExtra + " -> " + heldAfterTopUp);
				FinalPlant = Plant("Plant", NearRite(9));
				Phase = 5;
				Evidence.Append("\nphase4 tick=").Append(Game.TimeTicks)
					.Append("; rearmed=true; reserved=").Append(ReservedTopUp)
					.Append("; tally still=").Append(heldAfterTopUp)
					.Append("; raw physical=").Append(heldAfterTopUp + ReservedTopUp)
					.Append("; final=").Append(EvidenceOf(FinalPlant));
			}

			/// <summary>The ceiling counts reserved units: raw physical brush is at the twelve-
			/// unit cap even though the available-only Tally reads under it, so the final plant
			/// is left standing and the ceiling announces.</summary>
			private void Phase5(r_KingdomForage state)
			{
				Require(GameObject.Validate(FinalPlant),
					"the ceiling cut a plant despite twelve physical (incl. reserved) brush units");
				Require(state.EnoughAnnounced, "the ceiling did not announce with brush enough "
					+ "in physical stock (including reserved units)");
				int held = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Evidence.Append("\nphase5 tick=").Append(Game.TimeTicks)
					.Append("; ceiling-held-reserved-counted=true; tally=").Append(held)
					.Append("; raw physical=").Append(held + ReservedTopUp)
					.Append("; final plant untouched=").Append(EvidenceOf(FinalPlant));
			}

			private static void RequireUntouched(GameObject Item, string Label)
			{
				Require(GameObject.Validate(Item), Label + " was removed but is excluded from forage");
			}
		}
	}
}
