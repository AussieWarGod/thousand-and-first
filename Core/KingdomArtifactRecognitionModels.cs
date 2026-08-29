using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomArtifactRecognitionKind : byte
	{
		None = 0, Remark = 1, Inscription = 2, Representation = 3
	}

	[Serializable]
	public sealed class KingdomArtifactSnapshot
	{
		public string ObjectId;
		public string Blueprint;
		public string DisplayName;
		public string OwnerId;
		public string LocationId;
		public string DeedId;
		public string DeedText;
		public long ObservedTick;
		public string SnapshotDigest;
	}

	[Serializable]
	public sealed class KingdomArtifactRecognitionReceipt
	{
		public const int CurrentVersion = 1;
		public int Version = CurrentVersion;
		public string RecognitionId;
		public KingdomArtifactRecognitionKind Kind;
		public KingdomArtifactSnapshot Source;
		public int AttributedResidentId;
		public string AttributionName;
		public string Text;
		public int CommerceValue;
		public bool CustodyClaimed;
		public long RecognizedTick;
	}

	[Serializable]
	public sealed class KingdomArtifactRecognitionBook
	{
		public long Revision;
		public List<KingdomArtifactRecognitionReceipt> Rows =
			new List<KingdomArtifactRecognitionReceipt>();
	}
}
