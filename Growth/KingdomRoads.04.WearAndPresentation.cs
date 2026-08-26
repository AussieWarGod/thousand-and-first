using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRoads
	{
		// --- Letting it show ---------------------------------------------------------------

		/// <summary>
		/// Brings the ground up to what the tally says, no more than
		/// <c>KingdomRoadRules.MaxFloorChangesPerPass</c> cells of it at a time, and retires every
		/// cell that has become a path &mdash; from then on the laid path is the record, and the
		/// tally has room for whatever the settlement wears next.
		/// </summary>
		/// <returns>The highest rung any cell was brought to this pass, for the ledger's one
		/// line.</returns>
		private static KingdomRoadRules.WearState Apply(Zone Z, IList<KingdomRoadRules.WornCell> Tally, IList<KingdomPlotRules.PlotRect> Plots)
		{
			KingdomRoadRules.WearState reached = KingdomRoadRules.WearState.Untouched;
			int changes = 0;
			for (int i = Tally.Count - 1; i >= 0; i--)
			{
				if (changes >= KingdomRoadRules.MaxFloorChangesPerPass)
				{
					break;
				}
				KingdomRoadRules.WornCell cell = Tally[i];
				KingdomRoadRules.WearState wanted = KingdomRoadRules.WearAt(cell.Traffic);
				if (wanted <= KingdomRoadRules.WearState.Worn)
				{
					continue;
				}
				Cell ground = Z.GetCell(cell.X, cell.Y);
				GameObject exactFloor;
				KingdomPhysicalLookupState floorState = FindOurFloor(ground, out exactFloor);
				if (floorState == KingdomPhysicalLookupState.Ambiguous) continue;
				KingdomRoadRules.WearState applied = floorState == KingdomPhysicalLookupState.Exact
					? (KingdomRoadRules.WearState)exactFloor.GetIntProperty(PathStateProperty)
					: KingdomRoadRules.WearState.Untouched;
				if (applied >= wanted)
				{
					if (wanted == KingdomRoadRules.WearState.Path)
					{
						Tally.RemoveAt(i);
					}
					continue;
				}
				if (!Wearable(ground, Plots))
				{
					// Something has been set down here since the last pass. The ground keeps its
					// tally and waits; nothing is moved to make room for a floor.
					continue;
				}
				if (!Lay(ground, wanted, null))
				{
					continue;
				}
				changes++;
				if (wanted > reached)
				{
					reached = wanted;
				}
				if (wanted == KingdomRoadRules.WearState.Path)
				{
					Tally.RemoveAt(i);
				}
			}
			return reached;
		}

		/// <summary>
		/// Lays one floor, taking up the one this system laid before it. The only destruction
		/// anywhere in this file, and it is always of an object this file created and marked
		/// (STANDARDS 7).
		/// </summary>
		/// <param name="C">The cell. Must already have passed <see cref="Wearable"/>.</param>
		/// <param name="State">The rung to bring it to. <c>Untouched</c> and <c>Worn</c> lay
		/// nothing, because neither is a floor.</param>
		/// <param name="PavedBlueprint">The blueprint paving is laid as, from
		/// <c>KingdomRoadRules.PavedFloorFor</c>. Ignored except when
		/// <paramref name="State"/> is <c>Paved</c>.</param>
		/// <returns>False when nothing was laid, including when the blueprint does not exist in
		/// this install.</returns>
		public static bool Lay(Cell C, KingdomRoadRules.WearState State, string PavedBlueprint)
		{
			GameObject ignored;
			return Lay(C, State, PavedBlueprint, null, out ignored);
		}

		private static bool Lay(Cell C, KingdomRoadRules.WearState State,
			string PavedBlueprint, KingdomConstructionJob Job, out GameObject Floor)
		{
			Floor = null;
			if (C == null)
			{
				return false;
			}
			string blueprint;
			switch (State)
			{
				case KingdomRoadRules.WearState.Trodden:
					blueprint = TroddenBlueprint;
					break;
				case KingdomRoadRules.WearState.Path:
					blueprint = PathBlueprint;
					break;
				case KingdomRoadRules.WearState.Paved:
					blueprint = string.IsNullOrEmpty(PavedBlueprint) ? PathBlueprint : PavedBlueprint;
					break;
				default:
					return false;
			}
			if (Job != null)
			{
				GameObject existing = null;
				List<GameObject> old = new List<GameObject>();
				foreach (GameObject item in C.GetObjects())
				{
					if (item.GetIntProperty(PathStateProperty) <= 0) continue;
					if (item.Blueprint == blueprint
						&& item.GetIntProperty(PathStateProperty) == (int)State
						&& KingdomConstruction.HasReceipt(item, Job))
					{
						if (existing == null) existing = item;
						else old.Add(item);
					}
					else old.Add(item);
				}
				if (existing != null)
				{
					for (int i = 0; i < old.Count; i++)
					{
						bool removed;
						try { removed = old[i].Obliterate(null, Silent: true); }
						finally
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, old[i]);
						}
						if (removed && !GameObject.Validate(old[i]))
							KingdomSurvey.ObserveRemovedFromActive(C.ParentZone, old[i]);
					}
					for (int i = 0; i < old.Count; i++)
					{
						if (old[i].CurrentCell == C) return false;
					}
					Floor = existing;
					return true;
				}
			}
			GameObject floor = GameObject.Create(blueprint);
			if (floor == null)
			{
				KingdomLog.Log("roads: no blueprint named " + blueprint + "; the ground was left as it was");
				return false;
			}
			List<GameObject> previous = new List<GameObject>();
			foreach (GameObject item in C.GetObjects())
			{
				if (item.GetIntProperty(PathStateProperty) > 0) previous.Add(item);
			}
			floor.SetIntProperty(PathStateProperty, (int)State);
			if (Job != null)
			{
				KingdomConstruction.Bind(floor, Job);
			}
			GameObject accepted = null;
			try { accepted = C.AddObject(floor); }
			finally { KingdomSurvey.ObserveAddResultInActive(C.ParentZone, floor, accepted); }
			if (!ReferenceEquals(accepted, floor)) return false;
			if (floor.CurrentCell != C)
			{
				// Measured rather than trusted (STANDARDS 1): if the engine declined the cell for
				// any reason, the ground keeps exactly what it had and nothing is taken up.
				try { floor.Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, floor); }
				return false;
			}
			for (int i = 0; i < previous.Count; i++)
			{
				bool removed;
				try { removed = previous[i].Obliterate(null, Silent: true); }
				finally
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, previous[i]);
				}
				if (removed && !GameObject.Validate(previous[i]))
					KingdomSurvey.ObserveRemovedFromActive(C.ParentZone, previous[i]);
			}
			for (int i = 0; i < previous.Count; i++)
			{
				if (previous[i].CurrentCell == C) return false;
			}
			Floor = floor;
			return true;
		}

		private static void Announce(KingdomSystem System, Zone Z, KingdomRoadRules.WearState Reached, bool Full, int Tracked)
		{
			if (Full)
			{
				if (Z.GetZoneProperty(FullSaidProperty, null) != "1")
				{
					Z.SetZoneProperty(FullSaidProperty, "1");
					System.Ledger.Note(KingdomRoadRules.RefuseTallyFull(KingdomPresentation.Rich(System.SeatName)));
				}
			}
			else if (Tracked < KingdomRoadRules.MaxTrackedCells)
			{
				Z.SetZoneProperty(FullSaidProperty, "0");
			}
			if (Reached <= KingdomRoadRules.WearState.Worn)
			{
				return;
			}
			int said = int.TryParse(Z.GetZoneProperty(SaidProperty, null), out var value) ? value : 0;
			if ((int)Reached <= said)
			{
				return;
			}
			Z.SetZoneProperty(SaidProperty, ((int)Reached).ToString());
			string line = KingdomRoadRules.WearLine(Reached, KingdomPresentation.Rich(System.SeatName));
			if (line != null)
			{
				System.Ledger.Note(line);
			}
			if (Reached == KingdomRoadRules.WearState.Path)
			{
				KingdomChronicle.Record(System, "paths showed themselves through " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ ", worn by nothing but the going back and forth of the people who live there");
			}
		}

	}
}
