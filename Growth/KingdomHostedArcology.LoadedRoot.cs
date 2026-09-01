using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>Load-free exterior-shell resolution for a resident native interior.</summary>
	public static partial class KingdomHostedArcology
	{
		private const int MaxLoadedRootZones = 1024;
		private const int MaxLoadedRootObjects = 16384;

		internal static bool TryLoadedInteriorRoot(InteriorZone Interior,
			out GameObject Root, out string Failure)
		{
			Root = null; Failure = null;
			ZoneManager manager = The.ZoneManager;
			if (Interior == null || manager == null
				|| Interior.Schema != KingdomHostedArcologyTopology.Schema
				|| !KingdomHostedArcologyTopology.InBounds(
					Interior.X, Interior.Y, Interior.Z)
				|| string.IsNullOrEmpty(Interior.Instance))
				return LoadedRootFail("hosted interior identity is incomplete", out Failure);
			Dictionary<string, Zone> cached = manager.CachedZones;
			if (cached == null || cached.Count > MaxLoadedRootZones)
				return LoadedRootFail("loaded-zone cache exceeds the hosted lookup bound", out Failure);
			HashSet<Zone> seen = new HashSet<Zone>();
			int inspected = 0;
			if (!TryLoadedRootInZone(manager.ActiveZone, Interior, true,
				seen, ref inspected, ref Root, out Failure)) return false;
			foreach (KeyValuePair<string, Zone> pair in cached)
			{
				Zone held;
				if (pair.Value == null || pair.Key != pair.Value.ZoneID
					|| !cached.TryGetValue(pair.Value.ZoneID, out held)
					|| !ReferenceEquals(held, pair.Value))
					return LoadedRootFail("the loaded-zone cache identity is inconsistent",
						out Failure);
				if (!TryLoadedRootInZone(pair.Value, Interior, false,
					seen, ref inspected, ref Root, out Failure)) return false;
			}
			if (!GameObject.Validate(Root) || Root.GetPart<r_KingdomArcology>() == null)
				return LoadedRootFail("the hosted exterior shell is not resident", out Failure);
			if (!TryNativeInteriorTarget(Root, Interior.X, Interior.Y, Interior.Z,
				out string target, out Failure) || target != Interior.ZoneID)
				return LoadedRootFail(Failure
					?? "loaded shell does not declare this exact native interior", out Failure);
			return true;
		}

		private static bool TryLoadedRootInZone(Zone Z, InteriorZone Interior,
			bool IsActive, HashSet<Zone> Seen, ref int Inspected,
			ref GameObject Root, out string Failure)
		{
			Failure = null;
			if (Z == null || !Seen.Add(Z)) return true;
			if (IsActive && !ReferenceEquals(The.ZoneManager.ActiveZone, Z))
				return LoadedRootFail("the active-zone identity changed during hosted lookup",
					out Failure);
			List<GameObject> objects = Z.GetObjects();
			if (objects == null || objects.Count > MaxLoadedRootObjects)
				return LoadedRootFail("loaded hosted-shell object lookup exceeds its bound",
					out Failure);
			long next = (long)Inspected + objects.Count;
			if (next > MaxLoadedRootObjects)
				return LoadedRootFail("aggregate loaded hosted-shell lookup exceeds its bound",
					out Failure);
			Inspected = (int)next;
			GameObject candidate = null; int count = 0;
			for (int i = 0; i < objects.Count; i++)
				if (objects[i]?.IDIfAssigned == Interior.Instance)
				{
					candidate = objects[i]; count++;
				}
			if (count > 1)
				return LoadedRootFail("loaded hosted-shell identity is ambiguous", out Failure);
			if (count == 0) return true;
			Cell cell = candidate?.CurrentCell;
			if (!GameObject.Validate(candidate) || cell == null || candidate.CurrentZone != Z
				|| cell.ParentZone != Z || Interior.Location == null
				|| Interior.Location.ZoneID != Z.ZoneID
				|| Interior.Location.CellX != cell.X || Interior.Location.CellY != cell.Y
				|| candidate.GetPart<r_KingdomArcology>() == null)
				return LoadedRootFail("loaded hosted-shell identity names a foreign object",
					out Failure);
			if (Root != null && !ReferenceEquals(Root, candidate))
				return LoadedRootFail("loaded hosted-shell identity is duplicated across zones",
					out Failure);
			Root = candidate; return true;
		}

		private static bool LoadedRootFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
