using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		/// <summary>Freezes every natural thing which must leave the destination. Relocation
		/// clears no liquid, civic work, owned/takeable object, or unknown obstruction and awards
		/// no clearance output.</summary>
		private static bool TryFreezeClearance(Zone Zone, KingdomSurvey Survey,
			KingdomPlotRules.PlotRect Destination,
			out List<KingdomRelocationClearRow> Rows, out string Failure)
		{
			Rows = new List<KingdomRelocationClearRow>(); Failure = null;
			if (Zone == null || Survey == null)
			{
				Failure = "The destination ground cannot be surveyed exactly.";
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int y = Destination.Y1; y <= Destination.Y2; y++)
			{
				for (int x = Destination.X1; x <= Destination.X2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null)
					{
						Failure = "The destination reaches beyond the lawful zone interior.";
						return false;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (!GameObject.Validate(item) || item.IsCreature || item.IsPlayer()) continue;
						KingdomPlotRules.GroundKind kind = KingdomPlots.ReadObject(item);
						if (kind == KingdomPlotRules.GroundKind.Bare) continue;
						if (KingdomPlotRules.Refuses(kind))
						{
							string what = kind == KingdomPlotRules.GroundKind.Liquid
								? "open water" : KingdomDesign.ReferenceFor(item,
									item.ShortDisplayNameStripped);
							Failure = "The destination is protected by " + what + " at "
								+ x + "," + y + ".";
							return false;
						}
						GameObject exact;
						string itemId = item.IDIfAssigned;
						if (item.CurrentZone != Zone || item.CurrentCell != cell
							|| string.IsNullOrEmpty(itemId)
							|| itemId.Length > KingdomRelocationRules.MaxIdChars
							|| string.IsNullOrEmpty(item.Blueprint)
							|| item.Blueprint.Length > KingdomRelocationRules.MaxKeyChars
							|| !ids.Add(itemId)
							|| KingdomConstruction.FindExactId(Zone, itemId, out exact)
								!= KingdomPhysicalLookupState.Exact
							|| !ReferenceEquals(exact, item))
						{
							Failure = "Natural destination clearance is malformed or duplicated.";
							return false;
						}
						Rows.Add(new KingdomRelocationClearRow
						{
							ObjectId = itemId, Blueprint = item.Blueprint, X = x, Y = y,
							State = KingdomRelocationClearState.Standing
						});
						if (Rows.Count > KingdomRelocationRules.MaxClearRowsPerMove)
						{
							Failure = "Natural destination clearance exceeds its bounded receipt.";
							return false;
						}
					}
				}
			}
			Rows.Sort(delegate(KingdomRelocationClearRow a, KingdomRelocationClearRow b)
			{
				int compared = a.Y.CompareTo(b.Y);
				if (compared != 0) return compared;
				compared = a.X.CompareTo(b.X);
				return compared != 0 ? compared : string.CompareOrdinal(a.ObjectId, b.ObjectId);
			});
			return true;
		}
	}
}
