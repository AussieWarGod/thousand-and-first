using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSeal : IPlayerSystem
	{
		private bool TryCapture(KingdomSystem Kingdom, string CaptureLegacyId,
			int CaptureGeneration, int CaptureRevision, long WrittenTick,
			out KingdomSealRecord Record, out string Failure)
		{
			Record = null;
			Failure = "";
			try
			{
				if (!TryRequireAuthority(out Failure))
				{
					return false;
				}
				if (Kingdom == null || !Kingdom.Founded || !KingdomSealReceipt.ValidId(LineageId)
					|| !KingdomSealReceipt.ValidId(CaptureLegacyId)
					|| !KingdomSealReceipt.ValidId(OriginGameId))
				{
					Failure = "the living snapshot has no complete lineage identity";
					return false;
				}
				string founder = The.Player?.BaseDisplayNameStripped;
					if (string.IsNullOrEmpty(founder))
				{
						founder = The.Game?.PlayerName;
					}
					if (!Kingdom.TryCaptureSealIdentity(out KingdomSealIdentity identity,
						out Failure)) return false;
					KingdomSettlement seat = Kingdom.Capture();
					if (!Kingdom.SealIdentityStillMatches(identity) ||
						!KingdomSealRules.ExactIdentity(identity, seat))
					{
						Failure = "the immutable realm topology changed during seal capture";
						return false;
					}
					Record = KingdomSealRules.Capture(seat, identity,
						new KingdomSealLineage(LineageId, CaptureLegacyId, OriginGameId,
							CaptureGeneration, CaptureRevision),
					Kingdom.KingdomDisplayName, founder, Kingdom.ChronicleEntries,
					Kingdom.OutsiderEntries, WrittenTick);
					Record.WriterVersion = VersionOf(typeof(KingdomSeal).Assembly);
					Record.EngineVersion = VersionOf(typeof(XRLGame).Assembly);
					KingdomInheritanceSpatialCaptureResult spatial =
						KingdomInheritanceSpatial.TryCapture(seat.City, Record,
							The.ZoneManager?.ActiveZone, out string spatialFailure);
					if (spatial == KingdomInheritanceSpatialCaptureResult.Malformed)
					{
						Failure = spatialFailure;
						Record = null;
						return false;
					}
					if (spatial == KingdomInheritanceSpatialCaptureResult.Unavailable)
					{
						// Captures may happen away from the one inherited seat. Reuse only an exact
						// prior geometry basis for this generation; otherwise retain the explicit
						// schema-4 spatial-v0 proxy until the seat is witnessed.
						KingdomSealRecord prior = GetStore().ReadStage(OriginGameId);
						if (SameSpatialBasis(prior, Record))
							KingdomInheritanceSpatial.CopyEvidence(prior, Record);
					}
					if (!Kingdom.SealIdentityStillMatches(identity))
					{
						Failure = "the immutable realm topology changed before seal storage";
						Record = null;
						return false;
					}
				// Compose/read validates bounds and derived vigour before any store path is touched.
				KingdomSealRecord echo;
				KingdomSealFault fault;
				string detail;
				if (!KingdomSealRecord.TryParse(Record.Compose(), out echo, out fault, out detail))
				{
					Failure = string.IsNullOrEmpty(detail) ? "the coherent snapshot did not validate" : detail;
					Record = null;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Record = null;
				Failure = ex.Message;
				return false;
			}
		}

		private static bool SameSpatialBasis(KingdomSealRecord Earlier,
			KingdomSealRecord Current)
		{
			if (Earlier == null || Current == null || Earlier.LineageId != Current.LineageId
				|| Earlier.LegacyId != Current.LegacyId
				|| Earlier.OriginGameId != Current.OriginGameId
				|| Earlier.Generation != Current.Generation
				|| Earlier.GroundZoneId != Current.GroundZoneId
				|| Earlier.WorkKeys.Count != Current.WorkKeys.Count
				|| Earlier.WorkX.Count != Current.WorkX.Count
				|| Earlier.WorkY.Count != Current.WorkY.Count) return false;
			for (int i = 0; i < Current.WorkKeys.Count; i++)
				if (Earlier.WorkKeys[i] != Current.WorkKeys[i]
					|| Earlier.WorkX[i] != Current.WorkX[i]
					|| Earlier.WorkY[i] != Current.WorkY[i]) return false;
			return true;
		}

	}
}
