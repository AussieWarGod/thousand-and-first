using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// Native architecture-review harness. One command resolves a named catalogue variant and pose,
	/// freezes its canonical production snapshot, and sends that receipt through the same layered
	/// stamper and final-owner copy used by paid plots. The harness never clears live ground, spends
	/// stock, changes realm state, or silently replaces an existing gallery.
	/// </summary>
	[HasWishCommand]
	public partial class KingdomArchitectureGalleryWishes
	{
		private const int GallerySchema = 1;
		private const int MaxScreenshotChars = 180;
		private const int MaxNoteChars = 300;
		private const string ModVersion = KingdomReleaseInfo.Version;
		private const string GallerySchemaProperty = "r_TAF_ArchitectureGallerySchema";
		private const string GalleryReceiptProperty = "r_TAF_ArchitectureGalleryReceipt";
		private const string GalleryCaseProperty = "r_TAF_ArchitectureGalleryCase";
		private const string GalleryInventoryProperty = "r_TAF_ArchitectureGalleryInventory";
		private const string GalleryLiquidProperty = "r_TAF_ArchitectureGalleryLiquid";
		private const string GalleryVerdictProperty = "r_TAF_ArchitectureGalleryVerdict";
		private const string GalleryScreenshotProperty = "r_TAF_ArchitectureGalleryScreenshot";
		private const string GalleryNoteProperty = "r_TAF_ArchitectureGalleryNote";
		private const string GallerySyntheticProperty = "r_TAF_ArchitectureGallerySynthetic";
		private const string GalleryNumberProperty = "r_TAF_ArchitectureGalleryNumber";
		private const string GalleryDigestProperty = "r_TAF_ArchitectureGalleryDigest";
		private const string GalleryExpectedScreenshotProperty =
			"r_TAF_ArchitectureGalleryExpectedScreenshot";
		private const string WorksBlueprint = "r_KingdomPlotWorks";

		private sealed class GalleryCase
		{
			public int Number;
			public KingdomArchitectureMapping Mapping;
			public string Variant;
			public ArchitectureFacing Facing;

			public string Key
			{
				get
				{
					return Mapping.BuildKey + "|" + Mapping.TypeKey + "|"
						+ Mapping.LotSize.ToString() + "|" + Variant + "|" + Facing.ToString();
				}
			}
		}

		[WishCommand("kingdom:archgallery", null)]
		public static void Gallery(string Parameter)
		{
			KingdomSystem.Guard("architecture gallery", delegate
			{
				KingdomData.EnsureBuildings();
				List<GalleryCase> cases = Cases();
				if (string.IsNullOrEmpty(Parameter))
				{
					Popup.Show("{{C|Native architecture gallery}}\nMod " + ModVersion
						+ ", Qud " + XRLGame.CoreVersion + "\n" + cases.Count
						+ " exact type/size/variant/facing cases.\n\n"
						+ "Use {{W|kingdom:archgallery NUMBER}} on spacious, empty, passable ground. "
						+ "Traversal: {{W|list [PAGE]}}, {{W|status}}, {{W|next}}, {{W|resume}}, "
						+ "or {{W|checkpoint}}. "
						+ "Then capture the map and submit {{W|kingdom:archverdict pass|SCREENSHOT|NOTE}} "
						+ "or {{W|kingdom:archverdict fail|SCREENSHOT|NOTE}}. "
						+ "Use {{W|kingdom:archgalleryclear}} only after a verdict.\n\n"
						+ "This review harness deliberately bypasses stock debit and current-realm "
						+ "technology, knowledge, skill, and power eligibility. It proves the exact "
						+ "production snapshot, stamper, fixtures, and rendering—not affordability.");
					return;
				}
				string selection;
				if (HandleArchitectureControl(Parameter, cases, out selection)) return;
				Parameter = selection;
				int number;
				if (!int.TryParse(Parameter.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
					out number) || number < 1 || number > cases.Count)
				{
					Popup.Show("Choose an architecture gallery case from 1 to " + cases.Count + ".");
					return;
				}
				Zone zone = The.Player?.CurrentZone;
				if (zone == null)
				{
					Popup.Show("Enter a loaded zone before staging an architecture gallery case.");
					return;
				}
				if (VisualGalleryActive())
				{
					Popup.Show("Clear the active non-plot/road visual gallery before staging architecture.");
					return;
				}
				GameObject existing;
				string failure;
				if (!TryUniqueGallery(zone, out existing, out failure))
				{
					Popup.Show(failure);
					return;
				}
				if (existing != null)
				{
					Popup.Show("This zone already holds gallery receipt {{W|"
						+ existing.GetStringProperty(GalleryReceiptProperty)
						+ "}}. Record its verdict and clear it before staging another case.");
					return;
				}
				GalleryCase selected = cases[number - 1];
				GameObject owner;
				string receipt;
				if (!TryStage(zone, selected, cases.Count, out owner, out receipt, out failure))
				{
					Popup.Show("Architecture gallery case refused without replacing live ground:\n\n"
						+ (failure ?? "unknown staging failure"));
					return;
				}
				ArchitectureLayoutSnapshot snapshot;
				KingdomArchitectureIntent intent;
				if (!KingdomArchitectureStamper.TryReadOwner(owner, out intent, out snapshot,
					out _, out failure))
				{
					Popup.Show("The staged gallery lost its exact receipt: " + failure);
					return;
				}
				Popup.Show("{{C|Architecture gallery " + number + "/" + cases.Count + "}}\n"
					+ selected.Mapping.BuildKey + " — " + selected.Mapping.PlanKey + " / "
					+ selected.Mapping.TierKey + "\nTyped lot: " + selected.Mapping.TypeKey + " "
					+ selected.Mapping.LotSize + "; variant " + selected.Variant + "; faces "
					+ selected.Facing + "\nPalette " + snapshot.PaletteKey + "\nSnapshot "
					+ intent.SnapshotHash + "\nReceipt {{W|" + receipt + "}}\nScreenshot {{W|"
					+ ArchitectureScreenshot(number, cases.Count) + "}}\nZone " + zone.ZoneID
					+ ", rect " + intent.Rect.X1 + "," + intent.Rect.Y1 + "–" + intent.Rect.X2
					+ "," + intent.Rect.Y2 + "\n\nCapture a native-resolution screenshot. Check silhouette, "
					+ "materials, ingress, furniture, function, readable roof/open space, and Qud fit. "
					+ "Then submit a pass/fail verdict naming that screenshot.\n\n"
					+ "Harness scope: production snapshot/stamper/rendering; stock debit and current "
					+ "realm technology, knowledge, skill, and power eligibility are deliberately bypassed.");
			});
		}

		[WishCommand("kingdom:archverdict", null)]
		public static void Verdict(string Parameter)
		{
			KingdomSystem.Guard("architecture gallery verdict", delegate
			{
				Zone zone = The.Player?.CurrentZone;
				GameObject owner = null;
				string failure = null;
				if (zone == null || !TryUniqueGallery(zone, out owner, out failure) || owner == null)
				{
					Popup.Show(failure ?? "No exact architecture gallery stands in this zone.");
					return;
				}
				string verdict;
				string screenshot;
				string note;
				if (!TryParseVerdict(Parameter, out verdict, out screenshot, out note, out failure))
				{
					Popup.Show(failure);
					return;
				}
				int number = owner.GetIntProperty(GalleryNumberProperty);
				List<GalleryCase> cases = Cases();
				string expected = owner.GetStringProperty(GalleryExpectedScreenshotProperty);
				if (number < 1 || number > cases.Count
					|| cases[number - 1].Key != owner.GetStringProperty(GalleryCaseProperty)
					|| expected != ArchitectureScreenshot(number, cases.Count))
				{
					Popup.Show("The staged architecture case no longer binds this exact catalogue.");
					return;
				}
				if (!KingdomVisualProofRules.ScreenshotMatches(screenshot, expected))
				{
					Popup.Show("Capture this case as {{W|" + expected + "}}. A directory prefix is allowed; "
						+ "the deterministic filename is not optional.");
					return;
				}
				owner.SetStringProperty(GalleryVerdictProperty, verdict);
				owner.SetStringProperty(GalleryScreenshotProperty, screenshot);
				owner.SetStringProperty(GalleryNoteProperty, note, RemoveIfNull: true);
				if (!TryWriteArchitectureVerdict(cases, number, verdict, out failure))
				{
					owner.RemoveStringProperty(GalleryVerdictProperty);
					owner.RemoveStringProperty(GalleryScreenshotProperty);
					owner.RemoveStringProperty(GalleryNoteProperty);
					Popup.Show(failure);
					return;
				}
				string line = KingdomVisualProofRules.EvidenceRow(ArchitectureSuite, number,
					cases.Count, owner.GetStringProperty(GalleryCaseProperty),
					owner.GetStringProperty(GalleryReceiptProperty),
					owner.GetStringProperty(GalleryDigestProperty), verdict, screenshot, note);
				KingdomLog.Log(line);
				Popup.Show("Gallery verdict recorded and logged.\n\n" + line
					+ "\n\nUse {{W|kingdom:archgalleryclear}} when ready for the next case.");
			});
		}

		[WishCommand("kingdom:archgalleryclear", null)]
		public static void Clear()
		{
			KingdomSystem.Guard("architecture gallery cleanup", delegate
			{
				Zone zone = The.Player?.CurrentZone;
				GameObject owner = null;
				string failure = null;
				if (zone == null || !TryUniqueGallery(zone, out owner, out failure) || owner == null)
				{
					Popup.Show(failure ?? "No exact architecture gallery stands in this zone.");
					return;
				}
				if (string.IsNullOrEmpty(owner.GetStringProperty(GalleryVerdictProperty)))
				{
					Popup.Show("Record a pass/fail screenshot verdict before clearing this gallery.");
					return;
				}
				List<GalleryCase> cases = Cases();
				byte[] states;
				int number = owner.GetIntProperty(GalleryNumberProperty);
				if (!TryArchitectureCheckpoint(cases, out states, out failure)
					|| number < 1 || number > states.Length
					|| states[number - 1] != (owner.GetStringProperty(GalleryVerdictProperty) == "pass"
						? KingdomVisualProofRules.Pass : KingdomVisualProofRules.Fail))
				{
					Popup.Show(failure ?? "The saved checkpoint does not contain this verdict; submit it again before cleanup.");
					return;
				}
				string receipt = owner.GetStringProperty(GalleryReceiptProperty);
				if (!TryClearExact(owner, zone, out failure))
				{
					Popup.Show("Gallery cleanup refused: " + failure
						+ "\nNo foreign object was selected for removal.");
					return;
				}
				KingdomLog.Log("[TAF architecture-gallery] receipt=" + receipt + " cleanup=complete");
				Popup.Show("Gallery receipt {{W|" + receipt + "}} cleared exactly.");
			});
		}
	}
}
