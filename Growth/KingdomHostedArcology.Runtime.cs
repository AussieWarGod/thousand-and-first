using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomHostedArcology
	{
		internal static bool TryReceipt(r_KingdomArcology Root, string LotKey,
			out KingdomHostedLotReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (Root == null || Root.LotReceipts == null || Root.LotReceipts.Count >
				KingdomHostedArcologyRules.MaxHostedLots)
				return Fail("The hosted-lot slate is unbounded.", out Failure);
			for (int i = 0; i < Root.LotReceipts.Count; i++)
			{
				KingdomHostedLotReceipt row;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(Root.LotReceipts[i], out row))
					return Fail("A hosted-lot receipt cannot be read.", out Failure);
				if (!KingdomHostedArcologyRules.TryHostedLot(row.LotKey,
					out KingdomHostedLotDefinition definition) || definition.ReadOnly
					|| row.Supports != definition.Supports
					|| row.RequiresWater != definition.RequiresWater)
					return Fail("A hosted-lot receipt diverges from its registered work contract.",
						out Failure);
				if (row.LotKey != LotKey) continue;
				if (Receipt != null) return Fail("A hosted lot has duplicate receipts.", out Failure);
				Receipt = row;
			}
			return true;
		}

		internal static bool SetReceipt(r_KingdomArcology Root, KingdomHostedLotReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(Receipt);
			if (Root == null || Root.LotReceipts == null || string.IsNullOrEmpty(encoded))
				return Fail("The hosted-lot receipt is invalid.", out Failure);
			List<string> next = new List<string>(); bool replaced = false;
			for (int i = 0; i < Root.LotReceipts.Count; i++)
			{
				KingdomHostedLotReceipt row;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(Root.LotReceipts[i], out row))
					return Fail("The hosted-lot slate cannot be changed safely.", out Failure);
				if (row.LotKey == Receipt.LotKey)
				{
					if (replaced) return Fail("The hosted-lot slate is duplicated.", out Failure);
					next.Add(encoded); replaced = true;
				}
				else next.Add(Root.LotReceipts[i]);
			}
			if (!replaced) next.Add(encoded);
			if (next.Count > KingdomHostedArcologyRules.MaxHostedLots)
				return Fail("The hosted-lot slate is full.", out Failure);
			Root.LotReceipts = next;
			KingdomHostedLotReceipt proved;
			return TryReceipt(Root, Receipt.LotKey, out proved, out Failure)
				&& proved != null && KingdomHostedArcologyReceiptCodec.EncodeLot(proved) == encoded;
		}

		internal static void PrepareStaffing(KingdomSystem System, KingdomSurvey Survey)
		{
			if (System == null || Survey == null) return;
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (KingdomUpgrade.DesignKeyOf(work) != ArcologyKey) continue;
				int need = 0; r_KingdomArcology root = work.GetPart<r_KingdomArcology>();
				if (root != null && root.LotReceipts != null && Operational(work))
				{
					need = 4;
					for (int j = 0; j < root.LotReceipts.Count; j++)
					{
						KingdomHostedLotReceipt receipt;
						KingdomHostedLotDefinition lot;
						if (KingdomHostedArcologyReceiptCodec.TryDecodeLot(root.LotReceipts[j], out receipt)
							&& receipt.Phase == KingdomHostedLotPhase.Working
							&& KingdomHostedArcologyRules.TryHostedLot(receipt.LotKey, out lot)) need += lot.Crew;
					}
				}
				work.SetIntProperty("KingdomStaffNeeded", need);
			}
		}

		internal static List<KindAmount> HostedCarries(GameObject Work,
			List<KindAmount> BaseCarries, bool FreshWaterAvailable)
		{
			if (KingdomUpgrade.DesignKeyOf(Work) != ArcologyKey) return BaseCarries;
			List<KindAmount> answer = new List<KindAmount>();
			if (!Operational(Work)) return answer;
			if (BaseCarries != null) answer.AddRange(BaseCarries);
			r_KingdomArcology root = Work.GetPart<r_KingdomArcology>();
			if (root == null || root.LotReceipts == null) return answer;
			for (int i = 0; i < root.LotReceipts.Count; i++)
			{
				KingdomHostedLotReceipt receipt;
				if (!KingdomHostedArcologyReceiptCodec.TryDecodeLot(root.LotReceipts[i], out receipt)
					|| receipt.Phase != KingdomHostedLotPhase.Active
					|| (receipt.RequiresWater && !FreshWaterAvailable)) continue;
				List<KindAmount> hosted;
				KingdomCatalogueRules.TryParseTally(receipt.Supports, out hosted, out _);
				if (hosted != null) answer.AddRange(hosted);
			}
			return answer;
		}

		internal static GameObject RootOf(Zone Zone)
		{
			Zone cursor = Zone;
			for (int i = 0; i < 8; i++)
			{
				InteriorZone interior = cursor as InteriorZone;
				GameObject host = interior?.ParentObject;
				if (!GameObject.Validate(host)) return null;
				if (host.GetPart<r_KingdomArcology>() != null) return host;
				cursor = host.CurrentZone;
			}
			return null;
		}

		internal static string Status(r_KingdomArcology Root)
		{
			if (Root == null) return "No hosted-shell authority is present.";
			if (!string.IsNullOrEmpty(Root.QuarantineReason))
				return "{{r|Quarantined: " + Root.QuarantineReason + "}}";
			List<string> lines = new List<string>();
			List<KingdomHostedLotDefinition> lots = KingdomHostedArcologyRules.RegisteredHostedLots();
			for (int i = 0; i < lots.Count; i++)
			{
				KingdomHostedLotReceipt receipt; string failure;
				if (!TryReceipt(Root, lots[i].Key, out receipt, out failure))
					return "{{r|Quarantined: " + failure + "}}";
				string state = lots[i].ReadOnly ? "read-only view"
					: receipt == null ? "unbuilt" : receipt.Phase == KingdomHostedLotPhase.Active
					? "active" : receipt.Phase == KingdomHostedLotPhase.Working
					? (receipt.Remaining + " labour ticks remain") : "quarantined";
				lines.Add("{{C|" + lots[i].DisplayName + "}} — " + state);
			}
			return string.Join("\n", lines.ToArray());
		}

		internal static void Quarantine(r_KingdomArcology Root, string Reason)
		{
			if (Root != null && string.IsNullOrEmpty(Root.QuarantineReason))
				Root.QuarantineReason = string.IsNullOrEmpty(Reason) ? "ambiguous hosted-shell evidence"
					: Reason.Substring(0, Math.Min(512, Reason.Length));
		}
	}
}
