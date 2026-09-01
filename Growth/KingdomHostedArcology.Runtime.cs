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
			Receipt = null;
			List<KingdomHostedLotReceipt> rows;
			if (!TryReceiptSlate(Root, out rows, out Failure)) return false;
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomHostedLotReceipt row = rows[i];
				if (row.LotKey != LotKey) continue;
				Receipt = row;
			}
			return true;
		}

		private static bool TryReceiptSlate(r_KingdomArcology Root,
			out List<KingdomHostedLotReceipt> Receipts, out string Failure)
		{
			Receipts = null;
			Failure = null;
			GameObject owner = Root?.ParentObject;
			if (Root == null || Root.LotReceipts == null || !GameObject.Validate(owner))
				return Fail("The hosted-lot slate has no exact shell authority.", out Failure);
			return KingdomHostedArcologySlateRules.TryRead(Root.LotReceipts,
				owner.IDIfAssigned, out Receipts, out Failure);
		}

		internal static bool SetReceipt(r_KingdomArcology Root, KingdomHostedLotReceipt Receipt,
			out string Failure)
		{
			Failure = null;
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(Receipt);
			List<KingdomHostedLotReceipt> existing;
			if (!TryReceiptSlate(Root, out existing, out Failure)) return false;
			if (Receipt == null || Receipt.RootId != Root.ParentObject.IDIfAssigned
				|| string.IsNullOrEmpty(encoded))
				return Fail("The hosted-lot receipt is invalid.", out Failure);
			List<string> next = new List<string>(); bool replaced = false;
			for (int i = 0; i < existing.Count; i++)
			{
				KingdomHostedLotReceipt row = existing[i];
				if (row.LotKey == Receipt.LotKey)
				{
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
				List<KingdomHostedLotReceipt> rows;
				string failure = null;
				if (root != null && IsOperationalPure(work)
					&& TryReceiptSlate(root, out rows, out failure))
				{
					need = 4;
					for (int j = 0; j < rows.Count; j++)
					{
						KingdomHostedLotDefinition lot;
						KingdomHostedLotReceipt receipt = rows[j];
						if (KingdomHostedArcologyRules.TryHostedLot(receipt.LotKey, out lot)
							&& receipt.Phase == KingdomHostedLotPhase.Working)
							need += lot.Crew;
					}
				}
				else if (root != null && !string.IsNullOrEmpty(failure)) Quarantine(root, failure);
				work.SetIntProperty("KingdomStaffNeeded", need);
			}
		}

		internal static GameObject RootOf(Zone Zone)
		{
			InteriorZone interior = Zone as InteriorZone;
			if (interior == null || interior.Schema != KingdomHostedArcologyTopology.Schema
				|| !KingdomHostedArcologyTopology.InBounds(interior.X, interior.Y, interior.Z))
				return null;
			return TryLoadedInteriorRoot(interior, out GameObject host, out string ignored)
				? host : null;
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
					? ObservationStatus(Root.ParentObject, lots[i].Key)
					: receipt.Phase == KingdomHostedLotPhase.Working
					? (receipt.Remaining + " labour ticks remain") : "quarantined";
				lines.Add("{{C|" + lots[i].DisplayName + "}} — " + state);
			}
			return string.Join("\n", lines.ToArray());
		}

		internal static void Quarantine(r_KingdomArcology Root, string Reason)
		{
			if (Root == null) return;
			if (!TryQuarantineAuthority(Root, Reason, out string failure))
				KingdomLog.Log("hosted quarantine refused: "
					+ (failure ?? "unproved exact authority"));
		}
	}
}
