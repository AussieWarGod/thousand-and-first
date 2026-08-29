using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private const int VisualGallerySchema = 1;
		private const string VisualSuite = "visual";
		private const string VisualCheckpointState = "r_TAF_NonPlotRoadGalleryCheckpoint_v1";
		private const string VisualSchemaProperty = "r_TAF_VisualGallerySchema";
		private const string VisualReceiptProperty = "r_TAF_VisualGalleryReceipt";
		private const string VisualCaseProperty = "r_TAF_VisualGalleryCase";
		private const string VisualNumberProperty = "r_TAF_VisualGalleryNumber";
		private const string VisualZoneProperty = "r_TAF_VisualGalleryZone";
		private const string VisualDigestProperty = "r_TAF_VisualGalleryDigest";
		private const string VisualScreenshotProperty = "r_TAF_VisualGalleryScreenshot";
		private const string VisualExpectedScreenshotProperty = "r_TAF_VisualGalleryExpectedScreenshot";
		private const string VisualVerdictProperty = "r_TAF_VisualGalleryVerdict";
		private const string VisualNoteProperty = "r_TAF_VisualGalleryNote";
		private const string VisualRoleProperty = "r_TAF_VisualGalleryRole";
		private const string VisualExpectedCountProperty = "r_TAF_VisualGalleryExpectedCount";
		private const string VisualXProperty = "r_TAF_VisualGalleryX";
		private const string VisualYProperty = "r_TAF_VisualGalleryY";
		private const string VisualWidthProperty = "r_TAF_VisualGalleryWidth";
		private const string VisualHeightProperty = "r_TAF_VisualGalleryHeight";
		private const string VisualPriorTallyProperty = "r_TAF_VisualGalleryPriorTally";
		private const string VisualPriorTallyPresentProperty = "r_TAF_VisualGalleryPriorTallyPresent";

		private static bool VisualGalleryActive()
		{
			return GameObject.Validate(The.Player)
				&& The.Player.GetIntProperty(VisualSchemaProperty) == VisualGallerySchema;
		}

		private static string VisualCatalogueDigest(List<VisualCase> Cases)
		{
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < Cases.Count; i++)
			{
				VisualCase item = Cases[i];
				text.Append(item.Number).Append('|').Append(item.Key).Append('|')
					.Append((int)item.Kind).Append('|').Append(item.Width).Append('x')
					.Append(item.Height).Append('\n');
				for (int p = 0; p < item.Placements.Count; p++)
					text.Append(item.Placements[p].Role).Append('|')
						.Append(item.Placements[p].Blueprint).Append('|')
						.Append(item.Placements[p].X).Append(',').Append(item.Placements[p].Y)
						.Append('|').Append(item.Placements[p].Declaration ?? "<default>")
						.Append('\n');
			}
			return Hash(text.ToString());
		}

		private static bool TryVisualCheckpoint(List<VisualCase> Cases, out byte[] States,
			out string Failure)
		{
			States = null;
			if (The.Game == null) return Fail("Enter a running game before using proof traversal.", out Failure);
			return KingdomVisualProofRules.TryDecodeCheckpoint(The.Game.GetStringGameState(
				VisualCheckpointState, ""), Cases.Count, VisualCatalogueDigest(Cases),
				out States, out Failure);
		}

		private static bool TryWriteVisualVerdict(List<VisualCase> Cases, int Number,
			string Verdict, out string Failure)
		{
			byte[] states;
			if (!TryVisualCheckpoint(Cases, out states, out Failure)) return false;
			if (Number < 1 || Number > states.Length)
				return Fail("The staged visual case is outside this catalogue.", out Failure);
			states[Number - 1] = Verdict == "pass"
				? KingdomVisualProofRules.Pass : KingdomVisualProofRules.Fail;
			string encoded = KingdomVisualProofRules.EncodeCheckpoint(
				VisualCatalogueDigest(Cases), states);
			The.Game.SetStringGameState(VisualCheckpointState, encoded);
			return The.Game.GetStringGameState(VisualCheckpointState, "") == encoded
				|| Fail("The visual checkpoint did not persist; the verdict was not accepted.", out Failure);
		}

		private static string VisualScreenshot(int Number, int Total)
		{
			return KingdomVisualProofRules.ExpectedScreenshot(VisualSuite, Number, Total);
		}

		private static string VisualReceiptFor(VisualCase Case, int Total, string Digest)
		{
			string payload = VisualGallerySchema.ToString(CultureInfo.InvariantCulture) + "\n"
				+ ModVersion + "\n" + XRLGame.CoreVersion + "\n" + Case.Number.ToString(
					CultureInfo.InvariantCulture) + "/" + Total.ToString(CultureInfo.InvariantCulture)
				+ "\n" + Case.Key + "\n" + Digest;
			return "vg1-" + Hash(payload).Substring(0, 24);
		}

		private static void StampVisualItem(GameObject Item, string Receipt, string CaseKey,
			string Role)
		{
			Item.SetIntProperty(VisualSchemaProperty, VisualGallerySchema);
			Item.SetStringProperty(VisualReceiptProperty, Receipt);
			Item.SetStringProperty(VisualCaseProperty, CaseKey);
			Item.SetStringProperty(VisualRoleProperty, Role);
		}

		private static void StampVisualAnchor(VisualCase Case, Zone Zone,
			KingdomPlotRules.PlotRect Rect, int Total, string Receipt, string Digest,
			bool PriorTallyPresent, string PriorTally)
		{
			GameObject player = The.Player;
			player.SetStringProperty(VisualReceiptProperty, Receipt);
			player.SetStringProperty(VisualCaseProperty, Case.Key);
			player.SetIntProperty(VisualNumberProperty, Case.Number);
			player.SetStringProperty(VisualZoneProperty, Zone.ZoneID);
			player.SetStringProperty(VisualDigestProperty, Digest);
			player.SetStringProperty(VisualExpectedScreenshotProperty,
				VisualScreenshot(Case.Number, Total));
			player.SetIntProperty(VisualExpectedCountProperty, Case.ExpectedObjects);
			player.SetIntProperty(VisualXProperty, Rect.X1);
			player.SetIntProperty(VisualYProperty, Rect.Y1);
			player.SetIntProperty(VisualWidthProperty, Case.Width);
			player.SetIntProperty(VisualHeightProperty, Case.Height);
			player.SetIntProperty(VisualPriorTallyPresentProperty, PriorTallyPresent ? 1 : 0);
			player.SetStringProperty(VisualPriorTallyProperty, PriorTally, RemoveIfNull: true);
			player.SetIntProperty(VisualSchemaProperty, VisualGallerySchema);
		}

		private static void ClearVisualAnchor()
		{
			GameObject player = The.Player;
			if (!GameObject.Validate(player)) return;
			player.RemoveIntProperty(VisualSchemaProperty);
			player.RemoveStringProperty(VisualReceiptProperty);
			player.RemoveStringProperty(VisualCaseProperty);
			player.RemoveIntProperty(VisualNumberProperty);
			player.RemoveStringProperty(VisualZoneProperty);
			player.RemoveStringProperty(VisualDigestProperty);
			player.RemoveStringProperty(VisualScreenshotProperty);
			player.RemoveStringProperty(VisualExpectedScreenshotProperty);
			player.RemoveStringProperty(VisualVerdictProperty);
			player.RemoveStringProperty(VisualNoteProperty);
			player.RemoveIntProperty(VisualExpectedCountProperty);
			player.RemoveIntProperty(VisualXProperty);
			player.RemoveIntProperty(VisualYProperty);
			player.RemoveIntProperty(VisualWidthProperty);
			player.RemoveIntProperty(VisualHeightProperty);
			player.RemoveStringProperty(VisualPriorTallyProperty);
			player.RemoveIntProperty(VisualPriorTallyPresentProperty);
		}
	}
}
