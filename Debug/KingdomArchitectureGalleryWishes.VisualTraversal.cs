using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool HandleVisualControl(string Parameter, List<VisualCase> Cases,
			out string Selection)
		{
			Selection = Parameter;
			if (string.IsNullOrWhiteSpace(Parameter)) return false;
			string[] words = Parameter.Trim().Split(new char[] { ' ' },
				StringSplitOptions.RemoveEmptyEntries);
			string command = words[0].ToLowerInvariant();
			if (command == "next") return SelectNextVisual(Cases, out Selection);
			if (command == "resume")
			{
				if (VisualGalleryActive())
				{
					ShowVisualResume(Cases);
					return true;
				}
				return SelectNextVisual(Cases, out Selection);
			}
			if (command == "status")
			{
				ShowVisualStatus(Cases);
				return true;
			}
			if (command == "checkpoint")
			{
				ShowVisualCheckpoint(Cases);
				return true;
			}
			if (command == "list")
			{
				if (words.Length != 1) Popup.Show("Use kingdom:visualgallery list.");
				else ShowVisualList(Cases);
				return true;
			}
			return false;
		}

		private static bool SelectNextVisual(List<VisualCase> Cases, out string Selection)
		{
			Selection = null;
			byte[] states;
			string failure;
			if (!TryVisualCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return true;
			}
			int next = KingdomVisualProofRules.Next(states);
			if (next < 0)
			{
				Popup.Show("All " + Cases.Count + " non-plot/road cases carry human verdicts.");
				return true;
			}
			Selection = (next + 1).ToString(CultureInfo.InvariantCulture);
			return false;
		}

		private static void ShowVisualStatus(List<VisualCase> Cases)
		{
			byte[] states;
			string failure;
			if (!TryVisualCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomVisualProofRules.Counts(states, out int passed, out int failed, out int open);
			int next = KingdomVisualProofRules.Next(states);
			string active = VisualGalleryActive() ? "\nActive "
				+ The.Player.GetStringProperty(VisualCaseProperty) + " in "
				+ The.Player.GetStringProperty(VisualZoneProperty) : "";
			Popup.Show("{{C|Non-plot and road proof status}}\nCatalogue " + VisualCatalogueDigest(Cases)
				+ "\nPassed " + passed + "; failed " + failed + "; open " + open + active
				+ (next < 0 ? "\nTraversal complete."
					: "\nNext " + (next + 1) + ": " + Cases[next].Key
						+ "\nScreenshot " + VisualScreenshot(next + 1, Cases.Count)));
		}

		private static void ShowVisualList(List<VisualCase> Cases)
		{
			byte[] states;
			string failure;
			if (!TryVisualCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			StringBuilder text = new StringBuilder("{{C|Non-plot and road visual cases}}");
			for (int i = 0; i < Cases.Count; i++)
			{
				char mark = states[i] == KingdomVisualProofRules.Pass ? '+'
					: states[i] == KingdomVisualProofRules.Fail ? '!' : '-';
				text.Append('\n').Append(mark).Append(' ').Append(i + 1).Append("  ")
					.Append(Cases[i].Key).Append("  ").Append(VisualScreenshot(i + 1, Cases.Count));
			}
			text.Append("\n\n- open; + pass; ! fail.");
			Popup.Show(text.ToString());
		}

		private static void ShowVisualCheckpoint(List<VisualCase> Cases)
		{
			byte[] states;
			string failure;
			if (!TryVisualCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			string wire = KingdomVisualProofRules.EncodeCheckpoint(VisualCatalogueDigest(Cases), states);
			KingdomLog.Log("[TAF visual-checkpoint] suite=" + VisualSuite + " wire=" + wire);
			Popup.Show("Non-plot/road checkpoint logged.\n\n" + wire);
		}

		private static void ShowVisualResume(List<VisualCase> Cases)
		{
			int number = The.Player.GetIntProperty(VisualNumberProperty);
			if (number < 1 || number > Cases.Count
				|| The.Player.GetStringProperty(VisualCaseProperty) != Cases[number - 1].Key)
			{
				Popup.Show("The active visual-gallery anchor is malformed.");
				return;
			}
			string zone = The.Player.GetStringProperty(VisualZoneProperty);
			if (The.Player.CurrentZone == null || The.Player.CurrentZone.ZoneID != zone)
			{
				Popup.Show("Return to zone " + zone + " to resume visual case " + number + ".");
				return;
			}
			List<VisualCreated> items;
			KingdomPlotRules.PlotRect rect;
			string failure;
			if (!TryValidateVisualActive(The.Player.CurrentZone, Cases[number - 1], Cases.Count,
				out items, out rect, out failure))
			{
				Popup.Show(failure);
				return;
			}
			Popup.Show("{{C|Visual proof resumed}}\nCase " + number + "/" + Cases.Count + "\n"
				+ Cases[number - 1].Key + "\nDigest " + The.Player.GetStringProperty(VisualDigestProperty)
				+ "\nReceipt " + The.Player.GetStringProperty(VisualReceiptProperty) + "\nScreenshot "
				+ The.Player.GetStringProperty(VisualExpectedScreenshotProperty)
				+ "\n\nThe exact isolated state remains live. Capture it, then submit its human verdict.");
		}
	}
}
