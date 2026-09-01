using System.Collections.Generic;
using System.Globalization;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		[WishCommand("kingdom:visualgallery", null)]
		public static void VisualGallery(string Parameter)
		{
			KingdomSystem.Guard("non-plot and road visual gallery", delegate
			{
				KingdomData.EnsureBuildings();
				List<VisualCase> cases = VisualCases();
				if (string.IsNullOrEmpty(Parameter))
				{
					Popup.Show("{{C|Native non-plot and road gallery}}\nMod " + ModVersion
						+ ", Qud " + XRLGame.CoreVersion + "\n" + cases.Count
						+ " isolated cases: every ten non-plot catalogue families, four hosted "
						+ "yard works, plus worn, trodden, path, and paved road states.\n\nUse "
						+ "{{W|kingdom:visualgallery NUMBER}}, "
						+ "{{W|list}}, {{W|status}}, {{W|next}}, {{W|resume}}, or {{W|checkpoint}}. "
						+ "Capture the prescribed filename, then use {{W|kingdom:visualverdict "
						+ "pass|SCREENSHOT|NOTE}} or fail. Clear only with "
						+ "{{W|kingdom:visualgalleryclear}} after a verdict.\n\nThe gallery creates only "
						+ "isolated proof objects or exact road state on untouched ground. It spends no "
						+ "stock and proves no economy, eligibility, or settlement authority.");
					return;
				}
				string selection;
				if (HandleVisualControl(Parameter, cases, out selection)) return;
				Parameter = selection;
				int number;
				if (!int.TryParse(Parameter.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
					out number) || number < 1 || number > cases.Count)
				{
					Popup.Show("Choose a non-plot/road visual case from 1 to " + cases.Count + ".");
					return;
				}
				Zone zone = The.Player?.CurrentZone;
				if (zone == null)
				{
					Popup.Show("Enter a loaded zone before staging a visual case.");
					return;
				}
				if (VisualGalleryActive())
				{
					Popup.Show("Resume, verdict, and clear the active visual case before staging another.");
					return;
				}
				GameObject architecture;
				string failure;
				if (!TryUniqueGallery(zone, out architecture, out failure))
				{
					Popup.Show(failure);
					return;
				}
				if (architecture != null)
				{
					Popup.Show("Clear the active architecture gallery before staging non-plot/road proof.");
					return;
				}
				VisualCase selected = cases[number - 1];
				string receipt;
				string digest;
				KingdomPlotRules.PlotRect rect;
				if (!TryStageVisual(zone, selected, cases.Count, out receipt, out digest,
					out rect, out failure))
				{
					Popup.Show("Visual case refused without replacing live ground:\n\n"
						+ (failure ?? "unknown staging failure"));
					return;
				}
				Popup.Show("{{C|Non-plot/road gallery " + number + "/" + cases.Count + "}}\n"
					+ selected.Key + "\nDigest " + digest + "\nReceipt {{W|" + receipt
					+ "}}\nScreenshot {{W|" + VisualScreenshot(number, cases.Count) + "}}\nZone "
					+ zone.ZoneID + ", rect " + rect.X1 + "," + rect.Y1 + "–" + rect.X2 + ","
					+ rect.Y2 + "\n\nClose this receipt and capture the native map. Check material, "
					+ "silhouette, topology, readability, and Qud fit. Submit a human pass/fail verdict "
					+ "with the prescribed screenshot name; no verdict is generated automatically.");
			});
		}

		[WishCommand("kingdom:visualverdict", null)]
		public static void VisualVerdict(string Parameter)
		{
			KingdomSystem.Guard("non-plot and road visual verdict", delegate
			{
				List<VisualCase> cases = VisualCases();
				VisualCase selected;
				Zone zone;
				string failure;
				if (!TryCurrentVisualCase(cases, out selected, out zone, out failure))
				{
					Popup.Show(failure);
					return;
				}
				List<VisualCreated> items;
				KingdomPlotRules.PlotRect rect;
				if (!TryValidateVisualActive(zone, selected, cases.Count, out items, out rect, out failure))
				{
					Popup.Show(failure);
					return;
				}
				string verdict;
				string screenshot;
				string note;
				if (!TryParseVerdict(Parameter, out verdict, out screenshot, out note, out failure))
				{
					Popup.Show((failure ?? "Invalid verdict.").Replace("archverdict", "visualverdict"));
					return;
				}
				string expected = VisualScreenshot(selected.Number, cases.Count);
				if (!KingdomVisualProofRules.ScreenshotMatches(screenshot, expected))
				{
					Popup.Show("Capture this case as {{W|" + expected + "}}. A directory prefix is allowed; "
						+ "the deterministic filename is not optional.");
					return;
				}
				The.Player.SetStringProperty(VisualVerdictProperty, verdict);
				The.Player.SetStringProperty(VisualScreenshotProperty, screenshot);
				The.Player.SetStringProperty(VisualNoteProperty, note, RemoveIfNull: true);
				if (!TryWriteVisualVerdict(cases, selected.Number, verdict, out failure))
				{
					The.Player.RemoveStringProperty(VisualVerdictProperty);
					The.Player.RemoveStringProperty(VisualScreenshotProperty);
					The.Player.RemoveStringProperty(VisualNoteProperty);
					Popup.Show(failure);
					return;
				}
				string line = KingdomVisualProofRules.EvidenceRow(VisualSuite, selected.Number,
					cases.Count, selected.Key, The.Player.GetStringProperty(VisualReceiptProperty),
					The.Player.GetStringProperty(VisualDigestProperty), verdict, screenshot, note);
				KingdomLog.Log(line);
				Popup.Show("Human visual verdict recorded and logged.\n\n" + line
					+ "\n\nUse {{W|kingdom:visualgalleryclear}} when ready for the next case.");
			});
		}

		[WishCommand("kingdom:visualgalleryclear", null)]
		public static void VisualClear()
		{
			KingdomSystem.Guard("non-plot and road visual cleanup", delegate
			{
				List<VisualCase> cases = VisualCases();
				VisualCase selected;
				Zone zone;
				string failure;
				if (!TryCurrentVisualCase(cases, out selected, out zone, out failure))
				{
					Popup.Show(failure);
					return;
				}
				string verdict = The.Player.GetStringProperty(VisualVerdictProperty);
				byte[] states;
				if ((verdict != "pass" && verdict != "fail")
					|| !TryVisualCheckpoint(cases, out states, out failure)
					|| states[selected.Number - 1] != (verdict == "pass"
						? KingdomVisualProofRules.Pass : KingdomVisualProofRules.Fail))
				{
					Popup.Show(failure ?? "Submit and persist a human verdict before cleanup.");
					return;
				}
				string receipt = The.Player.GetStringProperty(VisualReceiptProperty);
				if (!TryClearVisual(zone, selected, cases.Count, out failure))
				{
					Popup.Show("Visual cleanup refused: " + failure
						+ "\nNo foreign object was selected for removal.");
					return;
				}
				KingdomLog.Log("[TAF visual-gallery] receipt=" + receipt + " cleanup=complete");
				Popup.Show("Visual gallery receipt {{W|" + receipt + "}} cleared exactly.");
			});
		}

		private static bool TryCurrentVisualCase(List<VisualCase> Cases, out VisualCase Case,
			out Zone Zone, out string Failure)
		{
			Case = null;
			Zone = The.Player?.CurrentZone;
			Failure = null;
			int number = The.Player?.GetIntProperty(VisualNumberProperty) ?? 0;
			if (!VisualGalleryActive() || number < 1 || number > Cases.Count)
				return Fail("No exact non-plot/road visual gallery is active.", out Failure);
			Case = Cases[number - 1];
			if (Zone == null || The.Player.GetStringProperty(VisualZoneProperty) != Zone.ZoneID)
				return Fail("Return to zone " + The.Player.GetStringProperty(VisualZoneProperty)
					+ " to finish this visual case.", out Failure);
			return Case.Key == The.Player.GetStringProperty(VisualCaseProperty)
				|| Fail("The active visual case no longer binds this catalogue.", out Failure);
		}
	}
}
