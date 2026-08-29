using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>
		/// Resolves founding-heart architecture against the poured rite, never against drifted layout
		/// marks. Every fitting pose is compiled; exactly one may bind its immutable basin to the rite.
		/// </summary>
		internal static bool TryPrepareFoundingHeart(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey, string LotType,
			int RiteX, int RiteY, out KingdomArchitectureIntent Intent, out string Failure)
		{
			Intent = null;
			Failure = null;
			if (System == null || !System.Founded || Z == null || Z.GetCell(RiteX, RiteY) == null
				|| !ValidRectInZone(Rect, Z)
				|| !TryRectLotSize(Rect, out ArchitectureLotSize size)
				|| !KingdomArchitecture.TryGetMapping(BuildKey, LotType, size,
					out KingdomArchitectureMapping mapping)
				|| mapping.Frontage != ArchitectureFrontage.Heart
				|| KingdomPlotRules.HeartRungOf(BuildKey) != 1)
				return Fail("founding-heart mapping, ground, or rite is malformed", out Failure);
			if (!TrySelectionContext(System, Z, out ArchitectureSelectionContext context,
				out Failure)) return false;

			ArchitectureFacing[] poses = new ArchitectureFacing[]
			{
				ArchitectureFacing.North, ArchitectureFacing.East,
				ArchitectureFacing.South, ArchitectureFacing.West
			};
			KingdomArchitectureIntent exact = null;
			for (int i = 0; i < poses.Length; i++)
			{
				ArchitectureFacing pose = poses[i];
				if (!KingdomArchitectureRules.TryDimensions(size, pose,
					out int width, out int height) || width != Rect.Width || height != Rect.Height)
					continue;
				if (!KingdomArchitecture.TryResolve(BuildKey, mapping.TypeKey, mapping.LotSize,
					context, pose, out ArchitectureLayoutSnapshot snapshot, out Failure)
					|| !MatchesMapping(snapshot, mapping)
					|| !TryHeartBasinCoordinate(snapshot, Rect, out int basinX, out int basinY,
						out Failure)) return false;
				if (basinX != RiteX || basinY != RiteY) continue;
				if (exact != null)
					return Fail("more than one founding-heart pose binds the immutable basin",
						out Failure);
				if (!KingdomArchitectureRules.TryEncodeSnapshot(snapshot, out string encoded,
					out Failure)
					|| !KingdomArchitectureRules.TrySnapshotHash(snapshot, out string hash,
						out Failure)
					|| !TryWorldCoordinate(snapshot, Rect, snapshot.MainX, snapshot.MainY,
						out int mainX, out int mainY, out Failure)) return false;
				exact = KingdomArchitectureIntent.Create(snapshot, encoded, hash, Rect, mainX, mainY);
				if (!TryValidateIntent(exact, out _, out Failure)
					|| !TryFoundingHeartBasinInvariant(exact, RiteX, RiteY, out Failure)) return false;
			}
			if (exact == null)
				return Fail("no authored founding-heart pose binds its basin to the poured rite",
					out Failure);
			Intent = exact;
			return true;
		}
	}
}
