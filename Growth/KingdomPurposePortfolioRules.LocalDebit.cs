using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public const int MaxDebitLines = 64;

		public static bool ValidLocalDebit(KingdomPurposeLocalDebitReceipt Receipt)
		{
			if (Receipt == null || !Id(Receipt.PairId) || Receipt.PairEpoch < 1L
				|| !Id(Receipt.OperationId) || !Id(Receipt.SourceSettlementId)
				|| !Id(Receipt.SourceZoneId) || !Id(Receipt.SourceWorkId)
				|| !Id(Receipt.SourceInputStoreId) || Receipt.WaterRequested < 0
				|| Receipt.FoodRequested < 0 || !CanonicalClaim(Receipt.MaterialRequested)
				|| Receipt.Lines == null || Receipt.Lines.Count < 1
				|| Receipt.Lines.Count > MaxDebitLines) return false;
			long water = 0L;
			long food = 0L;
			KingdomMaterialTally materials = new KingdomMaterialTally();
			HashSet<string> objects = new HashSet<string>();
			for (int i = 0; i < Receipt.Lines.Count; i++)
			{
				KingdomPurposeDebitLine line = Receipt.Lines[i];
				if (!ValidDebitLine(line) || !objects.Add(line.ObjectId)) return false;
				int delta = line.Before - line.After;
				if (line.Kind == KingdomPurposeDebitKind.Water) water += delta;
				else if (line.Kind == KingdomPurposeDebitKind.Food) food += delta;
				else
				{
					if (line.ContainerId != Receipt.SourceInputStoreId) return false;
					materials.Add((KingdomMaterial)line.TypeIndex, delta);
				}
			}
			if (water != Receipt.WaterRequested || food != Receipt.FoodRequested) return false;
			return KingdomMaterialDebitCost.TryParseClaim(Receipt.MaterialRequested,
				out KingdomMaterialDebitCost requested)
				&& requested.Bits.IsEmpty() && requested.Exotics.IsEmpty()
				&& new KingdomMaterialDebitCost(materials).ToClaimString()
					== Receipt.MaterialRequested;
		}

		private static bool ValidDebitLine(KingdomPurposeDebitLine Line)
		{
			if (Line == null || Line.Kind < KingdomPurposeDebitKind.Water
				|| Line.Kind > KingdomPurposeDebitKind.Food || !Id(Line.ContainerId)
				|| !Id(Line.ObjectId) || Line.Before <= 0 || Line.After < 0
				|| Line.After >= Line.Before || Line.Capacity < 0) return false;
			if (Line.Kind == KingdomPurposeDebitKind.Water)
				return Line.ObjectId == Line.ContainerId && string.IsNullOrEmpty(Line.Blueprint)
					&& Line.TypeIndex == -1 && Line.Capacity >= Line.Before;
			if (!Id(Line.Blueprint) || Line.Capacity != 0) return false;
			return Line.Kind == KingdomPurposeDebitKind.Food
				? Line.TypeIndex == -1
				: Line.TypeIndex >= 0 && Line.TypeIndex < KingdomMaterialRules.MaterialCount;
		}
	}
}
