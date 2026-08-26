using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>
		/// Craft points the roster is worth. Each kind is weighed by what it cost to acquire; a
		/// kind this file does not know is worth nothing, so a third party inventing a knowledge
		/// kind can gate designs on it without silently inflating the settlement's craft level.
		/// </summary>
		public static int TechPoints(IEnumerable<string> Roster)
		{
			if (Roster == null)
			{
				return 0;
			}
			int points = 0;
			List<string> counted = new List<string>();
			foreach (string entry in Roster)
			{
				string key = Fold(entry);
				if (key == null || counted.Contains(key))
				{
					continue;
				}
				counted.Add(key);
				points += PointsForKind(KindOf(key));
			}
			return points;
		}

		/// <summary>Points one roster key of the given kind is worth.</summary>
		public static int PointsForKind(string Kind)
		{
			string kind = Fold(Kind);
			if (kind == KindDisk)
			{
				return TechPointsPerDisk;
			}
			if (kind == KindMachine)
			{
				return TechPointsPerCertification;
			}
			if (kind == KindOrigin)
			{
				return TechPointsPerOrigin;
			}
			if (kind == KindNode)
			{
				return TechPointsPerNode;
			}
			return 0;
		}

		/// <summary>
		/// The level a point total reaches. Monotonic and clamped at both ends: a negative total
		/// (which nothing here can produce, but a corrupted store could) reads as
		/// <see cref="TechLevel.Hands"/> rather than wrapping below it.
		/// </summary>
		public static TechLevel LevelForPoints(int Points)
		{
			TechLevel level = TechLevel.Hands;
			for (int i = 0; i < TechThresholds.Length; i++)
			{
				if (Points >= TechThresholds[i])
				{
					level = (TechLevel)i;
				}
			}
			return level;
		}

		/// <summary>Points the given level asks for. Out-of-range values clamp to the ends
		/// rather than throwing, because a level can arrive from third-party XML.</summary>
		public static int PointsForLevel(TechLevel Level)
		{
			int index = (int)Level;
			if (index < 0)
			{
				index = 0;
			}
			if (index >= TechThresholds.Length)
			{
				index = TechThresholds.Length - 1;
			}
			return TechThresholds[index];
		}

		/// <summary>Points still wanted for the next level up, or 0 at the top of the ladder.
		/// The number the keepers' screen shows so the level never looks like a mystery.</summary>
		public static int PointsToNext(int Points)
		{
			TechLevel level = LevelForPoints(Points);
			if ((int)level >= TechThresholds.Length - 1)
			{
				return 0;
			}
			int wanted = TechThresholds[(int)level + 1] - Points;
			return (wanted > 0) ? wanted : 0;
		}

		/// <summary>What the settlement calls a level. Out-of-range clamps rather than throws.</summary>
		public static string TechName(TechLevel Level)
		{
			int index = (int)Level;
			if (index < 0)
			{
				index = 0;
			}
			if (index >= TechLevelNames.Length)
			{
				index = TechLevelNames.Length - 1;
			}
			return TechLevelNames[index];
		}

		/// <summary>Whether a value is one of the levels this file defines. The guard that keeps
		/// <c>MinTech="99"</c> out of a gate.</summary>
		public static bool IsKnownTechLevel(TechLevel Level)
		{
			return (int)Level >= 0 && (int)Level < TechThresholds.Length;
		}

	}
}
