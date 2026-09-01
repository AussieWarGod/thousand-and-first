using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomForeignFootprints
	{
		internal static bool TryMatchExact(Zone Z, IReadOnlyList<ArchitecturePoint> Cells,
			out KingdomForeignFootprint Match, out string Failure)
		{
			Match = null; Failure = null;
			if (!TryNormalizeCells(Cells, Z, out List<ArchitecturePoint> wanted, out Failure)
				|| !TryObserveAll(Z, out List<KingdomForeignProviderSnapshot> snapshots,
					out Failure)
				|| !KingdomForeignFootprintSnapshotRules.TryMatch(snapshots, wanted,
					out KingdomForeignFootprintEvidence match, out Failure)) return false;
			if (match != null) Match = Copy(match);
			return true;
		}

		internal static bool TryReprove(Zone Z, KingdomAdoptionDesignationReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			if (Receipt == null || string.IsNullOrEmpty(Receipt.ForeignProviderId))
				return Fail("bound foreign footprint identity is absent", out Failure);
			if (!TryObserveAll(Z, out List<KingdomForeignProviderSnapshot> snapshots,
				out Failure)) return false;
			return KingdomForeignFootprintSnapshotRules.TryReprove(snapshots,
				Receipt.ForeignProviderId, Receipt.ForeignProviderVersion,
				Receipt.ForeignIdentity, Receipt.ForeignRevision, Receipt.Cells, out Failure);
		}

		private static KingdomForeignFootprint Copy(KingdomForeignFootprintEvidence Source)
		{
			return new KingdomForeignFootprint { ProviderId = Source.ProviderId,
				ProviderVersion = Source.ProviderVersion, Identity = Source.Identity,
				Revision = Source.Revision, Refusal = Source.Refusal, ZoneId = Source.ZoneId,
				SectorId = Source.SectorId, DeclaredCount = Source.Cells.Count,
				OriginX = Source.OriginX, OriginY = Source.OriginY,
				Cells = Source.Cells.ToArray() };
		}
	}
}
