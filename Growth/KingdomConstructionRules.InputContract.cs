using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionRules
	{
		public static bool TryInputIntent(KingdomConstructionJob Job,
			int WaterRequested, string MaterialRequestedClaim,
			out KingdomConstructionInputIntent Intent, out string Digest)
		{
			Intent = null;
			Digest = null;
			KingdomMaterialDebitCost material;
			if (Job == null || Job.Claims == null || Job.Compacted
				|| string.IsNullOrEmpty(Job.Id) || string.IsNullOrEmpty(Job.OwnerKey)
				|| string.IsNullOrEmpty(Job.ZoneId) || Job.X < 0 || Job.Y < 0
				|| Job.CreatedTick < 0L || Job.StartedTick < Job.CreatedTick
				|| Job.DueTick < Job.StartedTick || WaterRequested < 0
				|| !KingdomMaterialDebitCost.TryParseClaim(MaterialRequestedClaim,
					out material) || MaterialRequestedClaim != material.ToClaimString()
				|| !ValidBuildTruth(Job)) return false;

			StringBuilder payload = new StringBuilder("TAF-CONSTRUCTION-INPUT-PAYLOAD-1");
			payload.Append('|').Append(Sha256(Job.Payload ?? string.Empty))
				.Append('|').Append(EncodeText(Job.PhysicalDestinationId))
				.Append('|').Append(Sha256(Job.PhysicalReceipt ?? string.Empty))
				.Append('|').Append(Job.Claims.WaterRequested)
				.Append('|').Append(EncodeText(Job.Claims.MaterialRequested));
			StringBuilder truth = new StringBuilder("TAF-CONSTRUCTION-INPUT-TRUTH-1");
			truth.Append('|').Append(Job.BuildTruthSchema)
				.Append('|').Append(Job.BuildHasPlot ? '1' : '0')
				.Append('|').Append(Job.BuildFrontier ? '1' : '0')
				.Append('|').Append(Job.BuildDefence);

			Intent = new KingdomConstructionInputIntent(Job.Id, Job.OwnerKey, Job.ZoneId,
				(int)Job.Route, (int)Job.Projection, Job.X, Job.Y, Job.SubjectId,
				Job.SourceId, Job.TargetKey, Sha256(payload.ToString()),
				Sha256(truth.ToString()), WaterRequested, MaterialRequestedClaim,
				Job.CreatedTick, Job.StartedTick, Job.DueTick);
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryIntentDigest(Intent, out Digest, out fault))
			{
				Intent = null;
				Digest = null;
				return false;
			}
			return true;
		}
	}
}
