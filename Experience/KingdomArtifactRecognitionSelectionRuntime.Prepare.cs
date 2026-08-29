using System;

namespace ThousandAndFirst
{
	/// <summary>Engine-free copy-before-mutation half of explicit artifact recognition.</summary>
	internal static partial class KingdomArtifactRecognitionSelectionRuntime
	{
		internal static bool TryPrepareRecognition(KingdomArtifactRecognitionBook Current,
			long ExpectedRevision, KingdomArtifactSnapshot Snapshot,
			KingdomArtifactRecognitionKind Kind, int AttributedResidentId,
			string AttributionName, long Tick, out KingdomArtifactRecognitionBook Candidate,
			out KingdomArtifactRecognitionReceipt Receipt, out string Failure)
		{
			Candidate = null;
			Receipt = null;
			Failure = null;
			try
			{
				Candidate = KingdomArtifactRecognitionCodec.Decode(
					KingdomArtifactRecognitionCodec.Encode(Current));
			}
			catch (Exception error)
			{
				Failure = "Recognition authority could not be copied ("
					+ error.GetType().Name + ").";
				return false;
			}
			if (KingdomArtifactRecognitionRules.TryRecognize(Candidate, ExpectedRevision,
				Snapshot, Kind, AttributedResidentId, AttributionName, Tick,
				out Receipt, out Failure)) return true;
			Candidate = null;
			Receipt = null;
			return false;
		}
	}
}
