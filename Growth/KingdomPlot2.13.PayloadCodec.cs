using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		internal static bool TryPreparePlotPayload(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey, string SkinKey,
			out KingdomArchitectureIntent Intent, out string Payload, out string Failure)
		{
			Intent = null;
			Payload = null;
			KingdomRules.BuildEntry entry;
			if (!KingdomData.TryGetBuilding(BuildKey, out entry))
			{
				Failure = "The authored plot design is absent from the merged building catalogue.";
				return false;
			}
			return TryPreparePlotPayload(System, Z, Rect, BuildKey, entry.Category, SkinKey,
				out Intent, out Payload, out Failure);
		}

		internal static bool TryPreparePlotPayload(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string BuildKey, string LotType, string SkinKey,
			out KingdomArchitectureIntent Intent, out string Payload, out string Failure)
		{
			Intent = null;
			Payload = null;
			if (!KingdomArchitectureRuntime.TryPrepare(System, Z, Rect, BuildKey, LotType,
				out KingdomArchitectureIntent prepared, out Failure)) return false;
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(
				KingdomMaterials.CostFor(BuildKey), KingdomMaterials.BitCostFor(BuildKey),
				KingdomMaterials.ExoticCostFor(BuildKey));
			if (!KingdomArchitectureStamper.TryPreflight(System, Z, prepared, claim,
				out Failure)) return false;
			KingdomDelveLinkIntent delveLink;
			if (!KingdomDelveLink.TryPreflight(System, Z, prepared, out delveLink,
				out Failure)) return false;
			if (!TryEncodePlotPayload(Rect, SkinKey, prepared, out Payload, out Failure)) return false;
			Intent = prepared;
			return true;
		}

		/// <summary>
		/// Canonical v2: version, exact rect, canonical UTF-8 skin, the architecture codec's three
		/// fields, then a SHA-256 over every preceding field. Snapshot is not base64-wrapped again,
		/// keeping the complete construction payload beneath its 8192-character wire bound.
		/// </summary>
		internal static bool TryEncodePlotPayload(KingdomPlotRules.PlotRect Rect, string SkinKey,
			KingdomArchitectureIntent Intent, out string Payload, out string Failure)
		{
			return TryEncodePlotPayloadCore(Rect, SkinKey, Intent, true, out Payload, out Failure);
		}

		private static bool TryEncodePlotPayloadCore(KingdomPlotRules.PlotRect Rect, string SkinKey,
			KingdomArchitectureIntent Intent, bool RequireCurrentSnapshot,
			out string Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			if (!TryPlotRect(Rect) || Intent == null
				|| !KingdomArchitectureRuntime.TryValidate(Intent, out Failure)
				|| !SameRect(Rect, Intent.Rect))
			{
				if (Failure == null) Failure = "The authored plot intent does not match its exact rectangle.";
				return false;
			}
			string skin;
			if (!TryEncodePlotSkin(SkinKey, out skin))
			{
				Failure = "The chosen plot skin is malformed or over the payload bound.";
				return false;
			}
			string snapshot = Intent.EncodedSnapshot;
			string[] snapshotFields = snapshot == null ? null : snapshot.Split('|');
			if (snapshotFields == null || snapshotFields.Length != 3
				|| (snapshotFields[0] != "a1" && snapshotFields[0] != "a2")
				|| (RequireCurrentSnapshot && snapshotFields[0] != "a2")
				|| snapshotFields[2] != Intent.SnapshotHash)
			{
				Failure = "The authored plot snapshot is not canonical.";
				return false;
			}
			string preimage = "v2|" + PlotCoordinate(Rect.X1) + "|" + PlotCoordinate(Rect.Y1)
				+ "|" + PlotCoordinate(Rect.X2) + "|" + PlotCoordinate(Rect.Y2) + "|" + skin
				+ "|" + snapshotFields[0] + "|" + snapshotFields[1] + "|" + snapshotFields[2];
			string hash = PlotPayloadHash(preimage);
			string encoded = preimage + "|" + hash;
			if (hash == null || encoded.Length > KingdomConstructionRules.MaxPayloadChars)
			{
				Failure = "The authored plot payload exceeds the construction receipt bound.";
				return false;
			}
			Payload = encoded;
			return true;
		}

		/// <summary>
		/// Reads canonical v2 into a catalogue-independent intent. V1 remains readable only for jobs
		/// written before authored receipts existed and is explicitly marked legacy; no writer emits it.
		/// </summary>
		internal static bool TryDecodePlotPayload(string Payload,
			out KingdomPlotRules.PlotRect Rect, out string SkinKey,
			out KingdomArchitectureIntent Intent, out bool Legacy, out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			SkinKey = null;
			Intent = null;
			Legacy = false;
			Failure = null;
			if (string.IsNullOrEmpty(Payload)
				|| Payload.Length > KingdomConstructionRules.MaxPayloadChars)
			{
				Failure = "The plot payload is absent or over the construction receipt bound.";
				return false;
			}
			if (Payload.StartsWith("v1|", StringComparison.Ordinal))
			{
				if (!TryDecodeLegacyPlotPayload(Payload, out Rect, out SkinKey))
				{
					Failure = "The legacy plot payload is malformed.";
					return false;
				}
				Legacy = true;
				return true;
			}
			string[] fields = Payload.Split('|');
			int x1;
			int y1;
			int x2;
			int y2;
			if (fields.Length != 10 || fields[0] != "v2"
				|| !TryPlotCoordinate(fields[1], out x1) || !TryPlotCoordinate(fields[2], out y1)
				|| !TryPlotCoordinate(fields[3], out x2) || !TryPlotCoordinate(fields[4], out y2)
				|| x2 < x1 || y2 < y1
				|| (fields[6] != "a1" && fields[6] != "a2")
				|| !CanonicalPlotHash(fields[9]))
			{
				Failure = "The authored plot payload shape is malformed or unknown.";
				return false;
			}
			int hashSplit = Payload.LastIndexOf('|');
			if (hashSplit <= 0 || PlotPayloadHash(Payload.Substring(0, hashSplit)) != fields[9])
			{
				Failure = "The authored plot payload hash does not match its contents.";
				return false;
			}
			if (!TryDecodePlotSkin(fields[5], out SkinKey))
			{
				Failure = "The authored plot skin is not canonical UTF-8.";
				return false;
			}
			Rect = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			string snapshot = fields[6] + "|" + fields[7] + "|" + fields[8];
			if (!TryIntentFromSnapshot(Rect, snapshot, out Intent, out Failure)) return false;
			string canonical;
			if (!TryEncodePlotPayloadCore(Rect, SkinKey, Intent, false,
				out canonical, out Failure)
				|| canonical != Payload)
			{
				if (Failure == null) Failure = "The authored plot payload is not canonical.";
				Intent = null;
				return false;
			}
			return true;
		}

	}
}
