using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Durable exact plan published before a purpose operation mutates local stock.</summary>
	public sealed class KingdomPurposeLocalDebitReceipt
	{
		public const int Schema = 1;
		public string PairId;
		public long PairEpoch;
		public string OperationId;
		public string SourceSettlementId;
		public string SourceZoneId;
		public string SourceWorkId;
		public string SourceInputStoreId;
		public int WaterRequested;
		public int FoodRequested;
		public string MaterialRequested;
		public List<KingdomPurposeDebitLine> Lines = new List<KingdomPurposeDebitLine>();

		public KingdomPurposeLocalDebitReceipt Copy()
		{
			KingdomPurposeLocalDebitReceipt copy =
				(KingdomPurposeLocalDebitReceipt)MemberwiseClone();
			copy.Lines = new List<KingdomPurposeDebitLine>();
			for (int i = 0; Lines != null && i < Lines.Count; i++)
				copy.Lines.Add(Lines[i]?.Copy());
			return copy;
		}
	}
}
