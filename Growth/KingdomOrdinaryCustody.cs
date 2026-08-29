using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Bounded, cycle-rejecting read of every engine custody edge exposed by
	/// <see cref="GameObject.GetContents"/>. Ordinary movers may inspect this graph; destructive
	/// consumers require an empty root so body, socket, magazine, and inventory contents survive.</summary>
	internal static class KingdomOrdinaryCustody
	{
		internal const int MaxNodes = 1024;
		internal const int MaxDepth = 32;

		internal static bool TryCollect(GameObject root, out List<GameObject> graph,
			out string failure)
		{
			graph = new List<GameObject>();
			failure = null;
			if (!GameObject.Validate(root))
			{
				failure = "The custody root is unavailable.";
				return false;
			}
			List<int> depths = new List<int>();
			graph.Add(root);
			depths.Add(0);
			for (int cursor = 0; cursor < graph.Count; cursor++)
			{
				IList<GameObject> contents;
				try { contents = graph[cursor].GetContents(new List<GameObject>()); }
				catch (Exception error)
				{
					failure = "The custody graph could not be read (" + error.GetType().Name + ").";
					return false;
				}
				if (contents == null)
				{
					failure = "The custody graph returned no exact contents index.";
					return false;
				}
				if (contents.Count > 0 && depths[cursor] >= MaxDepth)
				{
					failure = "The custody graph exceeds its depth bound.";
					return false;
				}
				if (contents.Count > MaxNodes - graph.Count)
				{
					failure = "The custody graph exceeds its object bound.";
					return false;
				}
				for (int i = 0; i < contents.Count; i++)
				{
					GameObject child = contents[i];
					if (!GameObject.Validate(child) || ContainsReference(graph, child))
					{
						failure = "The custody graph contains an invalid, duplicate, or cyclic edge.";
						return false;
					}
					graph.Add(child);
					depths.Add(depths[cursor] + 1);
				}
			}
			return true;
		}

		internal static bool TryProveEmpty(GameObject root, out string failure)
		{
			failure = null;
			if (!TryCollect(root, out List<GameObject> graph, out failure)) return false;
			if (graph.Count == 1) return true;
			failure = "The object holds equipment, inventory, socket, or magazine contents.";
			return false;
		}

		/// <summary>Exact callback aftermath for an invalidated graveyard root. The root may
		/// no longer validate, but every exposed custody edge must still be readable and empty.</summary>
		internal static bool TryProveRetiredEmpty(GameObject root, out string failure)
		{
			failure = null;
			if (root == null || !root.IsInGraveyard())
			{ failure = "The retired custody root is not in the graveyard."; return false; }
			try
			{
				IList<GameObject> contents = root.GetContents(new List<GameObject>());
				if (contents != null && contents.Count == 0) return true;
			}
			catch (Exception error)
			{
				failure = "The retired custody graph could not be read ("
					+ error.GetType().Name + ").";
				return false;
			}
			failure = "The retired root retains inventory, socket, magazine, or equipment custody.";
			return false;
		}

		internal static bool TryProveNoProtectedCargo(GameObject root, out string failure)
		{
			if (!TryCollect(root, out List<GameObject> graph, out failure)) return false;
			for (int i = 0; i < graph.Count; i++)
				if (KingdomPurpose.HasProtectedCargoEvidence(graph[i]))
				{
					failure = "Protected purpose cargo exists in this custody graph.";
					return false;
				}
			return true;
		}

		private static bool ContainsReference(List<GameObject> values, GameObject wanted)
		{
			for (int i = 0; i < values.Count; i++)
				if (ReferenceEquals(values[i], wanted)) return true;
			return false;
		}
	}
}
