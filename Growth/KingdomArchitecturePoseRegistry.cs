using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Frozen pose authority accepted by the compiler. Only the engine loader and internal tests can
	/// construct one; public callers cannot turn unaudited blueprint names into concrete placements.
	/// </summary>
	internal sealed class ArchitecturePoseRegistry
	{
		private readonly Dictionary<string, ArchitecturePoseDraft> poses;
		private readonly HashSet<string> poisoned;

		internal static readonly ArchitecturePoseRegistry Empty =
			new ArchitecturePoseRegistry(
				new Dictionary<string, ArchitecturePoseDraft>(StringComparer.Ordinal),
				new HashSet<string>(StringComparer.Ordinal));

		internal ArchitecturePoseRegistry(
			Dictionary<string, ArchitecturePoseDraft> Poses, HashSet<string> Poisoned)
		{
			poses = new Dictionary<string, ArchitecturePoseDraft>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, ArchitecturePoseDraft> row in Poses)
			{
				ArchitecturePoseDraft source = row.Value;
				poses.Add(row.Key, new ArchitecturePoseDraft
				{
					Blueprint = source.Blueprint, Mode = source.Mode,
					North = source.North, East = source.East,
					South = source.South, West = source.West
				});
			}
			poisoned = new HashSet<string>(Poisoned, StringComparer.Ordinal);
		}

		internal bool TryGet(string Blueprint, out ArchitecturePoseDraft Pose)
		{
			return poses.TryGetValue(Blueprint, out Pose);
		}

		internal bool IsPoisoned(string Blueprint)
		{
			return poisoned.Contains(Blueprint);
		}
	}
}
