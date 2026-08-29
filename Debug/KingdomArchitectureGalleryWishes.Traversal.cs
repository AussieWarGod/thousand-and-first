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
		private const string ArchitectureSuite = "architecture";
		private const string ArchitectureCheckpointState = "r_TAF_ArchitectureGalleryCheckpoint_v1";

		/// <summary>Returns true when the control is complete; otherwise rewrites it to a case number.</summary>
		private static bool HandleArchitectureControl(string Parameter, List<GalleryCase> Cases,
			out string Selection)
		{
			Selection = Parameter;
			if (string.IsNullOrWhiteSpace(Parameter)) return false;
			string[] words = Parameter.Trim().Split(new char[] { ' ' },
				StringSplitOptions.RemoveEmptyEntries);
			string command = words[0].ToLowerInvariant();
			if (command == "next")
				return SelectNextArchitecture(Cases, out Selection);
			if (command == "resume")
			{
				Zone zone = The.Player?.CurrentZone;
				GameObject active;
				string failure;
				if (zone != null && TryUniqueGallery(zone, out active, out failure) && active != null)
				{
					ShowArchitectureResume(active, Cases.Count);
					return true;
				}
				return SelectNextArchitecture(Cases, out Selection);
			}
			if (command == "status")
			{
				ShowArchitectureStatus(Cases);
				return true;
			}
			if (command == "checkpoint")
			{
				ShowArchitectureCheckpoint(Cases);
				return true;
			}
			if (command == "list")
			{
				int page = 1;
				if (words.Length > 2 || (words.Length == 2 && (!int.TryParse(words[1],
					NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1)))
				{
					Popup.Show("Use kingdom:archgallery list [PAGE].");
					return true;
				}
				ShowArchitectureList(Cases, page);
				return true;
			}
			return false;
		}

		private static bool SelectNextArchitecture(List<GalleryCase> Cases, out string Selection)
		{
			Selection = null;
			byte[] states;
			string failure;
			if (!TryArchitectureCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return true;
			}
			int next = KingdomVisualProofRules.Next(states);
			if (next < 0)
			{
				Popup.Show("All " + Cases.Count + " architecture cases carry human verdicts.");
				return true;
			}
			Selection = (next + 1).ToString(CultureInfo.InvariantCulture);
			return false;
		}

		private static void ShowArchitectureStatus(List<GalleryCase> Cases)
		{
			byte[] states;
			string failure;
			if (!TryArchitectureCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			KingdomVisualProofRules.Counts(states, out int passed, out int failed, out int open);
			int next = KingdomVisualProofRules.Next(states);
			Popup.Show("{{C|Architecture proof status}}\nCatalogue " + ArchitectureCatalogueDigest(Cases)
				+ "\nPassed " + passed + "; failed " + failed + "; open " + open
				+ (next < 0 ? "\nTraversal complete."
					: "\nNext " + (next + 1) + ": " + Cases[next].Key
						+ "\nScreenshot " + ArchitectureScreenshot(next + 1, Cases.Count)));
		}

		private static void ShowArchitectureList(List<GalleryCase> Cases, int Page)
		{
			const int pageSize = 18;
			int pages = (Cases.Count + pageSize - 1) / pageSize;
			if (Page > pages)
			{
				Popup.Show("Choose an architecture list page from 1 to " + pages + ".");
				return;
			}
			byte[] states;
			string failure;
			if (!TryArchitectureCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			StringBuilder text = new StringBuilder("{{C|Architecture cases " + Page + "/" + pages + "}}");
			int start = (Page - 1) * pageSize;
			int end = Math.Min(Cases.Count, start + pageSize);
			for (int i = start; i < end; i++)
			{
				char mark = states[i] == KingdomVisualProofRules.Pass ? '+'
					: states[i] == KingdomVisualProofRules.Fail ? '!' : '-';
				text.Append('\n').Append(mark).Append(' ').Append(i + 1).Append("  ")
					.Append(Cases[i].Key);
			}
			text.Append("\n\n- open; + pass; ! fail. Use kingdom:archgallery list PAGE.");
			Popup.Show(text.ToString());
		}

		private static void ShowArchitectureCheckpoint(List<GalleryCase> Cases)
		{
			byte[] states;
			string failure;
			if (!TryArchitectureCheckpoint(Cases, out states, out failure))
			{
				Popup.Show(failure);
				return;
			}
			string wire = KingdomVisualProofRules.EncodeCheckpoint(
				ArchitectureCatalogueDigest(Cases), states);
			string line = "[TAF visual-checkpoint] suite=" + ArchitectureSuite + " wire=" + wire;
			KingdomLog.Log(line);
			Popup.Show("Architecture checkpoint logged.\n\n" + wire);
		}

		private static void ShowArchitectureResume(GameObject Owner, int Total)
		{
			Popup.Show("{{C|Architecture proof resumed}}\nCase "
				+ Owner.GetIntProperty(GalleryNumberProperty) + "/" + Total + "\n"
				+ Owner.GetStringProperty(GalleryCaseProperty) + "\nDigest "
				+ Owner.GetStringProperty(GalleryDigestProperty) + "\nReceipt "
				+ Owner.GetStringProperty(GalleryReceiptProperty) + "\nScreenshot "
				+ Owner.GetStringProperty(GalleryExpectedScreenshotProperty)
				+ "\n\nThe staged production geometry remains live. Capture it, then submit its human verdict.");
		}

		private static bool TryArchitectureCheckpoint(List<GalleryCase> Cases, out byte[] States,
			out string Failure)
		{
			States = null;
			if (The.Game == null) return Fail("Enter a running game before using proof traversal.", out Failure);
			return KingdomVisualProofRules.TryDecodeCheckpoint(The.Game.GetStringGameState(
				ArchitectureCheckpointState, ""), Cases.Count, ArchitectureCatalogueDigest(Cases),
				out States, out Failure);
		}

		private static bool TryWriteArchitectureVerdict(List<GalleryCase> Cases, int Number,
			string Verdict, out string Failure)
		{
			byte[] states;
			if (!TryArchitectureCheckpoint(Cases, out states, out Failure)) return false;
			if (Number < 1 || Number > states.Length)
				return Fail("The staged architecture case is outside this catalogue.", out Failure);
			states[Number - 1] = Verdict == "pass"
				? KingdomVisualProofRules.Pass : KingdomVisualProofRules.Fail;
			string encoded = KingdomVisualProofRules.EncodeCheckpoint(
				ArchitectureCatalogueDigest(Cases), states);
			The.Game.SetStringGameState(ArchitectureCheckpointState, encoded);
			return The.Game.GetStringGameState(ArchitectureCheckpointState, "") == encoded
				|| Fail("The architecture checkpoint did not persist; the verdict was not accepted.", out Failure);
		}

		private static string ArchitectureCatalogueDigest(List<GalleryCase> Cases)
		{
			List<string> keys = new List<string>(Cases.Count);
			for (int i = 0; i < Cases.Count; i++) keys.Add(Cases[i].Key);
			return Hash(string.Join("\n", keys.ToArray()));
		}

		private static string ArchitectureScreenshot(int Number, int Total)
		{
			return KingdomVisualProofRules.ExpectedScreenshot(ArchitectureSuite, Number, Total);
		}
	}
}
