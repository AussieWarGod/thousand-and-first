using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Engine-free, closed validation for one hosted shell's complete receipt slate.</summary>
	public static class KingdomHostedArcologySlateRules
	{
		public static bool TryRead(IList<string> Encoded, string ExpectedRootId,
			out List<KingdomHostedLotReceipt> Receipts, out string Failure)
		{
			Receipts = null;
			Failure = null;
			if (Encoded == null || Encoded.Count > KingdomHostedArcologyRules.MaxHostedLots)
				return Fail("The hosted-lot slate is unbounded.", out Failure);
			if (string.IsNullOrEmpty(ExpectedRootId) || ExpectedRootId.Length > 512)
				return Fail("The hosted-lot slate has no exact shell identity.", out Failure);

			List<KingdomHostedLotReceipt> rows = new List<KingdomHostedLotReceipt>();
			HashSet<string> lots = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Encoded.Count; i++)
			{
				KingdomHostedLotReceipt row;
				KingdomHostedLotDefinition definition;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(Encoded[i], out row)
					|| KingdomHostedArcologyReceiptCodec.EncodeLot(row) != Encoded[i])
					return Fail("A hosted-lot receipt cannot be read canonically.", out Failure);
				if (!KingdomHostedArcologyRules.TryHostedLot(row.LotKey, out definition)
					|| definition.ReadOnly || row.Supports != definition.Supports
					|| row.RequiresWater != definition.RequiresWater)
					return Fail("A hosted-lot receipt diverges from its registered work contract.",
						out Failure);
				if (row.RootId != ExpectedRootId)
					return Fail("A hosted-lot receipt belongs to another shell.", out Failure);
				if (!lots.Add(row.LotKey))
					return Fail("A hosted lot has duplicate receipts.", out Failure);
				rows.Add(row);
			}
			Receipts = rows;
			return true;
		}

		/// <summary>Reads canonical copy-on-read final observations. A malformed or duplicate row
		/// refuses the complete slate so no ambiguous dated output survives.</summary>
		public static bool TryReadObservations(IList<string> Encoded, string ExpectedRootId,
			out List<KingdomHostedObservation> Observations, out string Failure)
		{
			Observations = null; Failure = null;
			if (Encoded == null || Encoded.Count > KingdomHostedArcologyRules.MaxHostedLots)
				return Fail("The hosted observation slate is unbounded.", out Failure);
			if (string.IsNullOrEmpty(ExpectedRootId) || ExpectedRootId.Length > 512)
				return Fail("The hosted observation slate has no exact shell identity.", out Failure);
			List<KingdomHostedObservation> rows = new List<KingdomHostedObservation>();
			HashSet<string> lots = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Encoded.Count; i++)
			{
				KingdomHostedObservation row;
				KingdomHostedLotDefinition definition;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeObservation(Encoded[i], out row)
					|| KingdomHostedArcologyReceiptCodec.EncodeObservation(row) != Encoded[i])
					return Fail("A hosted observation cannot be read canonically.", out Failure);
				if (row.RootId != ExpectedRootId)
					return Fail("A hosted observation belongs to another shell.", out Failure);
				if (!KingdomHostedArcologyRules.TryHostedLot(row.LotKey, out definition)
					|| definition.ReadOnly)
					return Fail("A hosted observation names no paid lot.", out Failure);
				if (!WithinContract(row, definition))
					return Fail("A hosted observation exceeds its physical work contract.",
						out Failure);
				if ((row.LotKey == KingdomHostedArcologyTopology.WardLotKey && row.Food != 0)
					|| (row.LotKey == KingdomHostedArcologyTopology.TerraceLotKey
						&& (row.Roof != 0 || row.Luxury != 0)))
					return Fail("A hosted observation crosses its physical output lane.", out Failure);
				if (!lots.Add(row.LotKey))
					return Fail("A hosted lot has duplicate observations.", out Failure);
				rows.Add(row.Copy());
			}
			Observations = rows; return true;
		}

		public static bool Matches(KingdomHostedObservation Observation, string RootId,
			string LotKey, string ReceiptRevision, string InteriorZoneId, string AnchorId,
			long NowTick, out string Failure)
		{
			Failure = null;
			if (Observation == null || !Observation.Valid())
				return Fail("The hosted lot has not been observed.", out Failure);
			if (Observation.RootId != RootId || Observation.LotKey != LotKey
				|| Observation.ReceiptRevision != ReceiptRevision
				|| Observation.InteriorZoneId != InteriorZoneId
				|| Observation.AnchorId != AnchorId)
				return Fail("The hosted observation no longer matches its exact receipt or ground.",
					out Failure);
			if (NowTick < 0L || Observation.ObservedTick > NowTick)
				return Fail("The hosted observation is dated in the future.", out Failure);
			return true;
		}

		public static int AgeDays(long ObservedTick, long NowTick, long TicksPerDay)
		{
			if (ObservedTick < 0L || NowTick <= ObservedTick || TicksPerDay <= 0L) return 0;
			long days = (NowTick - ObservedTick) / TicksPerDay;
			return days >= int.MaxValue ? int.MaxValue : (int)days;
		}

		private static bool WithinContract(KingdomHostedObservation Row,
			KingdomHostedLotDefinition Definition)
		{
			return Row.Roof <= KingdomHostedArcologyRules.ContractCap(Definition, "roof")
				&& Row.Food <= KingdomHostedArcologyRules.ContractCap(Definition, "food")
				&& Row.Luxury <= KingdomHostedArcologyRules.ContractCap(Definition, "luxury");
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
