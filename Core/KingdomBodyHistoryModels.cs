using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One non-abstract part in Qud's current native anatomy order.</summary>
	[Serializable]
	public sealed class KingdomLiveAnatomyPart
	{
		/// <summary>Index in Qud's read-only Body.GetParts() traversal.</summary>
		public int NativeOrderIndex = -1;
		/// <summary>Child-index path from Qud's native anatomy root.</summary>
		public string NativePath = "";
		/// <summary>Assigned native ID when one already exists; zero is valid and never minted.</summary>
		public int BodyPartId;
		public string Type = "";
		public string OrdinalName = "";
		public int Category;
		public bool Extrinsic;
		public string CyberneticsBlueprint = "";
	}

	/// <summary>Truthful delivery state for a paid physical lab procedure.</summary>
	public enum KingdomLabBodyHistoryPhase : byte
	{
		LegacyPhysicalOnly = 0,
		Pending = 1,
		Applied = 2,
		OmittedPreservingMemory = 3
	}

	internal enum KingdomBodyHistoryDeliveryResult : byte
	{
		Retryable = 0,
		Applied = 1,
		OmittedPreservingMemory = 2
	}

	/// <summary>A read-only observation of one exact, currently loaded body.</summary>
	[Serializable]
	public sealed class KingdomLiveAnatomySnapshot
	{
		public string ResidentIdentity = "";
		public string BodyObjectId = "";
		public string BodyIdentityDigest = "";
		public long ObservedTick;
		public List<KingdomLiveAnatomyPart> OrderedParts =
			new List<KingdomLiveAnatomyPart>();
	}

	/// <summary>
	/// Exact evidence emitted only by a completed lab-procedure owner. This is an
	/// input to the bounded book, not a second persistence owner.
	/// </summary>
	internal sealed class KingdomWitnessedBodyEventEvidence
	{
		public string OwnerKind = "";
		public string OwnerReceiptId = "";
		public string ResidentIdentity = "";
		public string BodyObjectId = "";
		public string ProcedureKey = "";
		public string BodyPartFact = "";
		public long WitnessedTick;
	}

	[Serializable]
	public sealed class KingdomBodyHistoryReceipt
	{
		public int Version = 1;
		public string ReceiptId = "";
		public string ResidentIdentity = "";
		public string BodyObjectId = "";
		public string ProcedureKey = "";
		public string ProcedureReceiptId = "";
		public string BodyPartFact = "";
		public string Description = "";
		public string Digest = "";
		public long WitnessedTick;

		public KingdomBodyHistoryReceipt Copy()
		{
			return new KingdomBodyHistoryReceipt
			{
				Version = Version,
				ReceiptId = ReceiptId,
				ResidentIdentity = ResidentIdentity,
				BodyObjectId = BodyObjectId,
				ProcedureKey = ProcedureKey,
				ProcedureReceiptId = ProcedureReceiptId,
				BodyPartFact = BodyPartFact,
				Description = Description,
				Digest = Digest,
				WitnessedTick = WitnessedTick
			};
		}
	}

	[Serializable]
	public sealed class KingdomBodyHistoryBook
	{
		public long Revision;
		public List<KingdomBodyHistoryReceipt> Rows =
			new List<KingdomBodyHistoryReceipt>();

		public KingdomBodyHistoryBook Copy()
		{
			KingdomBodyHistoryBook copy = new KingdomBodyHistoryBook { Revision = Revision };
			for (int i = 0; i < Rows.Count; i++) copy.Rows.Add(Rows[i]?.Copy());
			return copy;
		}
	}
}
