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
					case 5: Phase5(state); break;
					case 6: Phase6(state); break;
					case 7: Phase7(state); Done = true; Armed = false; break;
					default: Require(false, "forage check ran past its final phase"); break;
				}
			}

			/// <summary>The checkpoint has attached; zero on a fresh part means never worked, so
			/// no assertion is made about cutting yet.</summary>
			private void Phase1(r_KingdomForage state)
			{
				Require(state.LastWorkedTick > 0, "the forage checkpoint never attached");
				Phase = 2;
				Evidence.Append("\nphase1 tick=").Append(Game.TimeTicks)
					.Append("; checkpoint=").Append(state.LastWorkedTick);
			}

			/// <summary>Real removal paired with real stock gain (proved by direct raw census AND
			/// the available Tally, not by arithmetic on one alone), and every exclusion proved
			/// untouched fact-by-fact.</summary>
			private void Phase2(r_KingdomForage state)
			{
				for (int i = 0; i < Eligible.Length; i++)
					Require(!GameObject.Validate(Eligible[i]),
						"eligible plant " + i + " was not cut by the real settlement pass");
				VerifyAllExclusions();
				int raw = CensusBrushRaw(StockpileContainer);
				int tally = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(raw == RawBefore + Eligible.Length, "raw census gained " + (raw - RawBefore)
					+ " brush, not exactly " + Eligible.Length + " for the plants physically removed");
				Require(tally == TallyBefore + Eligible.Length, "available tally gained "
					+ (tally - TallyBefore) + " brush, not exactly " + Eligible.Length);
				int notes = NotesCount();
				Require(KingdomForageNativeGeometry.NewNotices(NotesBaseline, notes, 0)
					|| KingdomForageNativeGeometry.NewNotices(NotesBaseline, notes, 1),
					"the first cutting interval emitted repeated or inconsistent exhaustion notices");
				Phase = 3;
				Evidence.Append("\nphase2 tick=").Append(Game.TimeTicks)
					.Append("; cut=").Append(Eligible.Length).Append("; raw now=").Append(raw)
					.Append("; tally now=").Append(tally).Append("; exclusions verified=6");
			}

			/// <summary>Exhaustion: no candidates remain, so the once-only notice fires exactly
			/// once since setup. Its onset may precede this final observation.</summary>
			private void Phase3(r_KingdomForage state)
			{
				Require(state.NoBrushAnnounced, "exhaustion did not announce with no candidates left");
				Require(!state.EnoughAnnounced, "the ceiling announced before it was ever reached");
				int notes = NotesCount();
				Require(KingdomForageNativeGeometry.NewNotices(NotesBaseline, notes, 1),
					"exhaustion did not emit exactly one notice since setup");
				NotesAfterFirstExhaustion = notes;
				Phase = 4;
				Evidence.Append("\nphase3 tick=").Append(Game.TimeTicks)
					.Append("; exhausted=true; notices=").Append(notes);
			}

			/// <summary>Second exhausted interval: still no candidates, so the ledger must gain
			/// NO further notice -- the once-only witness, not a boolean alone.</summary>
			private void Phase4(r_KingdomForage state)
			{
				Require(state.NoBrushAnnounced, "exhaustion stopped announcing on the second interval");
				int notes = NotesCount();
				Require(notes == NotesAfterFirstExhaustion, "a second exhausted interval emitted "
					+ (notes - NotesAfterFirstExhaustion) + " more notices; the notice is not once-only");
				Phase = 5;
				Evidence.Append("\nphase4 tick=").Append(Game.TimeTicks)
					.Append("; exhausted-again=true; notices still=").Append(notes);
			}

			/// <summary>Third exhausted interval, same witness. A new plant is then introduced so
			/// the next phase proves the flag re-arms.</summary>
			private void Phase5(r_KingdomForage state)
			{
				Require(state.NoBrushAnnounced, "exhaustion stopped announcing on the third interval");
				int notes = NotesCount();
				Require(notes == NotesAfterFirstExhaustion, "a third exhausted interval emitted "
					+ (notes - NotesAfterFirstExhaustion) + " more notices; the notice is not once-only");
				ExtraPlant = Plant("Plant", NextOutdoorCell("re-arm plant"));
				Phase = 6;
				Evidence.Append("\nphase5 tick=").Append(Game.TimeTicks)
					.Append("; exhausted-third-interval=true; extra=").Append(EvidenceOf(ExtraPlant));
			}

			/// <summary>Re-arm: a new plant is cut, then a second exhaustion notice proves the
			/// intervening re-arm. The two-day interval need not end on the transient cleared flag.
			/// Then the store is topped up to the ceiling with
			/// RESERVED units -- marked with the real production marker, physically present,
			/// but excluded from the available-only Tally -- so the next phase proves the
			/// ceiling counts them anyway.</summary>
			private void Phase6(r_KingdomForage state)
			{
				Require(!GameObject.Validate(ExtraPlant), "the re-armed plant was not cut");
				int notes = NotesCount();
				Require(state.NoBrushAnnounced
					&& KingdomForageNativeGeometry.NewNotices(NotesAfterFirstExhaustion, notes, 1),
					"a second exhaustion episode did not prove exactly one re-armed notice");
				int raw = CensusBrushRaw(StockpileContainer);
				int tally = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				int afterExtra = RawBefore + Eligible.Length + 1;
				Require(raw == afterExtra, "raw census did not gain exactly one more brush for the "
					+ "re-armed plant: " + raw + " vs expected " + afterExtra);
				Require(tally == afterExtra, "available tally did not gain exactly one more brush");
				string blueprint = KingdomMaterials.BlueprintFor(KingdomMaterial.Brush);
				const string marker = "native-forage-reservation";
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
					brush.SetStringProperty(KingdomConstruction.InputMarkerProperty, marker);
					Reserved.Add(new ReservedBrush(brush, marker));
				}
				RawAtCeiling = CensusBrushRaw(StockpileContainer);
				TallyAtCeiling = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(RawAtCeiling == afterExtra + ReservedTopUp, "the direct raw census after "
					+ "topping up is " + RawAtCeiling + ", not " + (afterExtra + ReservedTopUp));
				Require(TallyAtCeiling == afterExtra, "the reserved top-up leaked into the "
					+ "available-only Tally: " + afterExtra + " -> " + TallyAtCeiling);
				VerifyReserved();
				FinalPlant = Plant("Plant", NextOutdoorCell("final plant"));
				Phase = 7;
				Evidence.Append("\nphase6 tick=").Append(Game.TimeTicks)
					.Append("; rearmed=true; reserved=").Append(ReservedTopUp)
					.Append("; exhaustion-notices=").Append(notes)
					.Append("; raw at ceiling=").Append(RawAtCeiling)
					.Append("; tally at ceiling=").Append(TallyAtCeiling)
					.Append("; final=").Append(EvidenceOf(FinalPlant));
			}

			/// <summary>The ceiling counts reserved units: raw physical brush sits at the
			/// twelve-unit cap even though the available-only Tally reads under it, so the final
			/// plant is left standing, NO stock moves at all (raw and Tally both unchanged), and
			/// every exclusion is re-verified one last time.</summary>
			private void Phase7(r_KingdomForage state)
			{
				Require(GameObject.Validate(FinalPlant),
					"the ceiling cut a plant despite twelve physical (incl. reserved) brush units");
				Require(state.EnoughAnnounced, "the ceiling did not announce with brush enough "
					+ "in physical stock (including reserved units)");
				int raw = CensusBrushRaw(StockpileContainer);
				int tally = KingdomMaterials.Stock(Zone).Tally.Get(KingdomMaterial.Brush);
				Require(raw == RawAtCeiling, "raw stock moved at the ceiling: "
					+ RawAtCeiling + " -> " + raw);
				Require(tally == TallyAtCeiling, "available tally moved at the ceiling: "
					+ TallyAtCeiling + " -> " + tally);
				VerifyReserved();
				VerifyAllExclusions();
				Evidence.Append("\nphase7 tick=").Append(Game.TimeTicks)
					.Append("; ceiling-held-reserved-counted=true; raw unchanged=").Append(raw)
					.Append("; tally unchanged=").Append(tally)
					.Append("; final plant untouched=").Append(EvidenceOf(FinalPlant));
			}

			private int NotesCount()
			{
				return KingdomForageNativeGeometry.CountContaining(System.Ledger.Notes, ExhaustionMarker);
			}

			private void VerifyAllExclusions()
			{
				VerifyUntouched(TreeSnap);
				VerifyUntouched(OwnedSnap);
				VerifyUntouched(FoodSnap);
				VerifyUntouched(ProtectedSnap);
				VerifyUntouched(PlotSnap);
				VerifyUntouched(CanvasSnap);
			}

			private static void VerifyUntouched(ExclusionSnapshot Snap)
			{
				Require(GameObject.Validate(Snap.Item), Snap.Label + " no longer validates");
				Require(Snap.Item.IDIfAssigned == Snap.Id, Snap.Label + " id changed");
				Require(Snap.Item.CurrentCell != null && Snap.Item.CurrentCell.X == Snap.X
					&& Snap.Item.CurrentCell.Y == Snap.Y, Snap.Label + " moved cell");
				Require(Snap.Item.CurrentZone != null && Snap.Item.CurrentZone.ZoneID == Snap.ZoneId,
					Snap.Label + " changed zone");
				Require(Snap.Item.Count == Snap.Count, Snap.Label + " count changed: "
					+ Snap.Count + " -> " + Snap.Item.Count);
				Require(Snap.Item.GetPart<Physics>()?.Owner == Snap.Owner,
					Snap.Label + " owner/custody changed");
				Require(KingdomOrdinaryCustody.TryProveEmpty(Snap.Item, out _),
					Snap.Label + " custody is no longer empty");
				Require(Snap.Fact(Snap.Item), Snap.Label + " lost its exclusion fact");
			}

			private void VerifyReserved()
			{
				foreach (ReservedBrush reserved in Reserved)
				{
					Require(GameObject.Validate(reserved.Item), "a reserved brush unit no longer validates");
					Require(reserved.Item.IDIfAssigned == reserved.Id, "a reserved brush unit's id changed");
					Require(reserved.Item.GetStringProperty(KingdomConstruction.InputMarkerProperty)
						== reserved.Marker, "a reserved brush unit lost its reservation marker");
					Require(reserved.Item.Physics != null
						&& ReferenceEquals(reserved.Item.Physics.InInventory, StockpileContainer),
						"a reserved brush unit is no longer physically in the stockpile");
				}
			}
		}
	}
}
