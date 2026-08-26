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
	public class KingdomArchitectureGalleryWishes
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
						+ "Then capture the map and submit {{W|kingdom:archverdict pass|SCREENSHOT|NOTE}} "
						+ "or {{W|kingdom:archverdict fail|SCREENSHOT|NOTE}}. "
						+ "Use {{W|kingdom:archgalleryclear}} only after a verdict.\n\n"
						+ "This review harness deliberately bypasses stock debit and current-realm "
						+ "technology, knowledge, skill, and power eligibility. It proves the exact "
						+ "production snapshot, stamper, fixtures, and rendering—not affordability.");
					return;
				}
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
					+ intent.SnapshotHash + "\nReceipt {{W|" + receipt + "}}\nZone " + zone.ZoneID
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
				owner.SetStringProperty(GalleryVerdictProperty, verdict);
				owner.SetStringProperty(GalleryScreenshotProperty, screenshot);
				owner.SetStringProperty(GalleryNoteProperty, note, RemoveIfNull: true);
				string line = "[TAF architecture-gallery] receipt="
					+ owner.GetStringProperty(GalleryReceiptProperty) + " case="
					+ owner.GetStringProperty(GalleryCaseProperty) + " verdict=" + verdict
					+ " screenshot=" + screenshot + " note=" + (note ?? "-");
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

		private static List<GalleryCase> Cases()
		{
			List<GalleryCase> result = new List<GalleryCase>();
			IList<KingdomArchitectureMapping> mappings = KingdomArchitecture.InspectMappings();
			for (int m = 0; m < mappings.Count; m++)
			{
				KingdomArchitectureMapping mapping = mappings[m];
				IList<string> variants = mapping.VariantKeys;
				for (int v = 0; v < variants.Count; v++)
					for (int facing = 0; facing < 4; facing++)
						result.Add(new GalleryCase { Number = result.Count + 1, Mapping = mapping,
							Variant = variants[v], Facing = (ArchitectureFacing)facing });
			}
			return result;
		}

		private static bool TryStage(Zone Zone, GalleryCase Case, int Total,
			out GameObject Owner, out string Receipt, out string Failure)
		{
			Owner = null;
			Receipt = null;
			Failure = null;
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitecture.Healthy)
				return Fail("The authored architecture catalogue is not healthy.", out Failure);
			if (!KingdomArchitecture.TryResolveVariant(Case.Mapping.BuildKey,
				Case.Mapping.TypeKey, Case.Mapping.LotSize, Case.Variant, Case.Facing,
				out snapshot, out Failure)) return false;
			int width;
			int height;
			if (!KingdomArchitectureRules.TryWorldDimensions(snapshot.Width, snapshot.Height,
				snapshot.Facing, out width, out height))
				return Fail("The selected pose has impossible world dimensions.", out Failure);
			KingdomPlotRules.PlotRect rect;
			if (!TryFindCanvas(Zone, width, height, out rect, out Failure)) return false;
			string encoded;
			string hash;
			int mainX;
			int mainY;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(snapshot, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(snapshot, out hash, out Failure)
				|| !KingdomArchitectureRules.TryToWorld(rect.X1, rect.Y1, snapshot.Width,
					snapshot.Height, snapshot.Facing, snapshot.MainX, snapshot.MainY,
					out mainX, out mainY)) return false;
			KingdomArchitectureIntent intent = KingdomArchitectureIntent.Create(snapshot, encoded,
				hash, rect, mainX, mainY);
			if (!KingdomArchitectureRuntime.TryValidate(intent, out Failure)) return false;
			Receipt = ReceiptFor(Case, Total, hash);
			string lot = "taf-gallery-" + Receipt + "-" + Guid.NewGuid().ToString("N");
			GameObject synthetic = null;
			GameObject works = null;
			GameObject final = null;
			try
			{
				if (!TryCreateSyntheticAuthority(Zone, snapshot, intent, Receipt,
					out synthetic, out Failure)) return false;
				works = GameObject.Create(WorksBlueprint);
				if (!GameObject.Validate(works))
					return Fail("The production plot-works blueprint created no gallery owner.", out Failure);
				StampGallery(works, Receipt, Case.Key);
				if (!KingdomArchitectureRuntime.TryFreeze(works, intent, out Failure)
					|| !KingdomArchitectureStamper.TryInitializeOwner(works, intent, lot, out Failure))
					return false;
				Cell main = Zone.GetCell(mainX, mainY);
				GameObject accepted = main == null ? null : main.AddObject(works, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, works))
					return Fail("The engine replaced the exact gallery plot-works owner.", out Failure);
				if (!KingdomArchitectureStamper.TryStageLayer(works, Zone,
					ArchitectureLayer.Ground, out Failure)
					|| !KingdomArchitectureStamper.TryStageLayer(works, Zone,
						ArchitectureLayer.Structure, out Failure)
					|| !KingdomArchitectureStamper.TryStageLayer(works, Zone,
						ArchitectureLayer.Object, out Failure)
					|| !KingdomArchitectureStamper.TryVerifyComplete(works, Zone, out Failure)) return false;

				final = GameObject.Create(Case.Mapping.BuildingBlueprint);
				if (!GameObject.Validate(final) || final.Blueprint != Case.Mapping.BuildingBlueprint)
					return Fail("The production behavior-root blueprint created no exact object.", out Failure);
				StampGallery(final, Receipt, Case.Key);
				final.DisplayName = "gallery: " + Case.Mapping.BuildKey;
				if (!KingdomArchitectureStamper.TryCopyFrozenOwner(works, final, out Failure)) return false;
				accepted = main.AddObject(final, NoStack: true, Silent: true);
				if (!ReferenceEquals(accepted, final))
					return Fail("The engine replaced the exact gallery behavior root.", out Failure);
				final.MakeActive();
				if (!KingdomArchitectureStamper.TryVerifyComplete(final, Zone, out Failure)) return false;
				if (!works.Destroy(null, Silent: true) || GameObject.Validate(works))
					return Fail("The temporary production plot-works owner would not retire.", out Failure);
				works = null;
				if (!KingdomArchitectureStamper.TryVerifyComplete(final, Zone, out Failure)
					|| !StampExactGallerySet(final, Zone, snapshot, lot, Receipt, out Failure)) return false;
				Owner = final;
				KingdomLog.Log("[TAF architecture-gallery] receipt=" + Receipt + " case=" + Case.Key
					+ " mod=" + ModVersion + " qud=" + XRLGame.CoreVersion + " snapshot=" + hash
					+ " zone=" + Zone.ZoneID + " rect=" + rect.X1 + "," + rect.Y1 + ","
					+ rect.X2 + "," + rect.Y2
					+ " economy=bypassed eligibility=not-asserted stage=complete");
				return true;
			}
			catch (Exception exception)
			{
				Failure = "Gallery staging threw: " + Bounded(exception.Message, MaxNoteChars);
				return false;
			}
			finally
			{
				if (Owner == null) RollBackCreated(Zone, lot, works, final, synthetic);
			}
		}

		private static bool TryFindCanvas(Zone Zone, int Width, int Height,
			out KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Failure = null;
			if (Zone == null || Width < 1 || Height < 1 || Width + 2 > Zone.Width
				|| Height + 2 > Zone.Height)
				return Fail("The selected pose cannot fit inside this zone with a review margin.", out Failure);
			HashSet<int> connections = ConnectionCells(Zone);
			Cell player = The.Player?.CurrentCell;
			int best = int.MaxValue;
			for (int y = 1; y + Height < Zone.Height; y++)
				for (int x = 1; x + Width < Zone.Width; x++)
				{
					KingdomPlotRules.PlotRect candidate = new KingdomPlotRules.PlotRect(
						x, y, x + Width - 1, y + Height - 1);
					if (!SafeCanvas(Zone, candidate, connections, player)) continue;
					int distance = player == null ? y * Zone.Width + x
						: Math.Abs(candidate.CenterX - player.X) + Math.Abs(candidate.CenterY - player.Y);
					if (distance >= best) continue;
					Rect = candidate;
					best = distance;
				}
			if (best == int.MaxValue)
				return Fail("No untouched passable rectangle with a one-cell review margin fits here. "
					+ "Move to an empty test zone; the gallery will not clear live terrain or objects.",
					out Failure);
			return true;
		}

		private static bool SafeCanvas(Zone Zone, KingdomPlotRules.PlotRect Rect,
			HashSet<int> Connections, Cell Player)
		{
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			for (int y = Rect.Y1 - 1; y <= Rect.Y2 + 1; y++)
				for (int x = Rect.X1 - 1; x <= Rect.X2 + 1; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || (Player != null && cell == Player)
						|| Connections.Contains(y * Zone.Width + x) || cell.HasStairs()
						|| cell.HasObjectWithPart("StairsUp") || cell.HasObjectWithPart("StairsDown")
						|| cell.HasOpenLiquidVolume() || !cell.IsPassable()) return false;
					if (Rect.Contains(x, y))
					{
						string blocker;
						if (KingdomPlots.ReadGround(cell, out blocker)
							!= KingdomPlotRules.GroundKind.Bare
							|| (system != null && KingdomConstruction.HasActiveAt(system, Zone, cell)))
							return false;
						List<GameObject> objects = cell.GetObjects();
						for (int i = 0; i < objects.Count; i++)
							if (GameObject.Validate(objects[i])
								&& (objects[i].IsCreature || objects[i].IsPlayer()
									|| objects[i].GetIntProperty(GallerySchemaProperty) == GallerySchema))
								return false;
					}
				}
			return true;
		}

		private static bool TryCreateSyntheticAuthority(Zone Zone,
			ArchitectureLayoutSnapshot Snapshot, KingdomArchitectureIntent Intent, string Receipt,
			out GameObject Synthetic, out string Failure)
		{
			Synthetic = null;
			Failure = null;
			ArchitecturePlacement existing = null;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
				if (Snapshot.Placements[i].ExistingAuthority)
				{
					if (existing != null)
						return Fail("The gallery case declares more than one existing authority.", out Failure);
					existing = Snapshot.Placements[i];
				}
			if (existing == null) return true;
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1)
					return Fail("A real first basin already stands in this zone. Heart-plan gallery cases "
						+ "require an isolated test zone and never borrow or alter that relic.", out Failure);
			int x;
			int y;
			if (existing.Blueprint != KingdomPlots.HeartRelicBlueprint
				|| !KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, existing,
					out x, out y, out Failure)) return false;
			Synthetic = GameObject.Create(existing.Blueprint);
			if (!GameObject.Validate(Synthetic))
				return Fail("The synthetic gallery basin blueprint created no object.", out Failure);
			Synthetic.SetIntProperty(KingdomPlots.HeartRelicProperty, 1);
			Synthetic.SetIntProperty(GallerySyntheticProperty, 1);
			StampGallery(Synthetic, Receipt, "synthetic:first-basin");
			GameObject accepted = Zone.GetCell(x, y).AddObject(Synthetic, NoStack: true, Silent: true);
			return ReferenceEquals(accepted, Synthetic)
				|| Fail("The engine replaced the exact synthetic gallery basin.", out Failure);
		}

		private static bool StampExactGallerySet(GameObject Owner, Zone Zone,
			ArchitectureLayoutSnapshot Snapshot, string Lot, string Receipt, out string Failure)
		{
			Failure = null;
			List<GameObject> components = Components(Zone, Lot);
			if (components.Count != Snapshot.Placements.Count)
				return Fail("The complete stamper output count disagrees with the snapshot.", out Failure);
			StampGallery(Owner, Receipt, Owner.GetStringProperty(GalleryCaseProperty));
			FreezeContents(Owner);
			for (int i = 0; i < components.Count; i++)
			{
				StampGallery(components[i], Receipt, "component");
				FreezeContents(components[i]);
			}
			return true;
		}

		private static void StampGallery(GameObject Item, string Receipt, string CaseKey)
		{
			Item.SetStringProperty(GalleryReceiptProperty, Receipt);
			Item.SetStringProperty(GalleryCaseProperty, CaseKey);
			Item.SetIntProperty(GallerySchemaProperty, GallerySchema);
		}

		private static void FreezeContents(GameObject Item)
		{
			Item.SetStringProperty(GalleryInventoryProperty, InventoryHash(Item));
			Item.SetStringProperty(GalleryLiquidProperty, LiquidHash(Item));
		}

		private static bool FrozenContents(GameObject Item)
		{
			return Item.GetStringProperty(GalleryInventoryProperty) == InventoryHash(Item)
				&& Item.GetStringProperty(GalleryLiquidProperty) == LiquidHash(Item);
		}

		private static string LiquidHash(GameObject Item)
		{
			LiquidVolume liquid = Item?.GetPart<LiquidVolume>();
			if (liquid == null) return "<none>";
			List<string> rows = new List<string>
			{
				"volume=" + liquid.Volume.ToString(CultureInfo.InvariantCulture),
				"maximum=" + liquid.MaxVolume.ToString(CultureInfo.InvariantCulture),
				"flags=" + liquid.Flags.ToString(CultureInfo.InvariantCulture)
			};
			if (liquid.ComponentLiquids != null)
				foreach (KeyValuePair<string, int> component in liquid.ComponentLiquids)
					rows.Add("component=" + (component.Key ?? "<null>") + "="
						+ component.Value.ToString(CultureInfo.InvariantCulture));
			rows.Sort(StringComparer.Ordinal);
			return Hash(string.Join("\n", rows.ToArray()));
		}

		private static string InventoryHash(GameObject Item)
		{
			List<string> rows = new List<string>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			AppendInventory(Item, "<root>", rows, seen, 0);
			rows.Sort(StringComparer.Ordinal);
			return Hash(string.Join("\n", rows.ToArray()));
		}

		private static void AppendInventory(GameObject Parent, string ParentKey,
			List<string> Rows, HashSet<GameObject> Seen, int Depth)
		{
			if (Parent == null || Rows == null || Seen == null) return;
			if (Depth > 64 || !Seen.Add(Parent))
			{
				Rows.Add(ParentKey + "\t<cycle-or-depth>");
				return;
			}
			Inventory inventory = Parent.Inventory;
			for (int i = 0; inventory != null && i < inventory.Objects.Count; i++)
			{
				GameObject child = inventory.Objects[i];
				string id = child?.ID ?? "<null>";
				string blueprint = child?.Blueprint ?? "<null>";
				int count = child == null ? 0 : child.Count;
				Rows.Add(ParentKey + "\t" + id + "\t" + blueprint + "\t"
					+ count.ToString(CultureInfo.InvariantCulture));
				if (child != null) AppendInventory(child, id, Rows, Seen, Depth + 1);
			}
		}

		private static bool TryClearExact(GameObject Owner, Zone Zone, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!ExactGalleryObject(Owner, Owner.GetStringProperty(GalleryReceiptProperty))
				|| !KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot,
					out lot, out Failure)
				|| !KingdomArchitectureStamper.TryVerifyComplete(Owner, Zone, out Failure)) return false;
			List<GameObject> components = Components(Zone, lot);
			if (components.Count != snapshot.Placements.Count)
				return Fail("The exact gallery component set is absent or duplicated.", out Failure);
			string receipt = Owner.GetStringProperty(GalleryReceiptProperty);
			if (!FrozenContents(Owner))
				return Fail("The gallery behavior root gained or lost contents; empty or restore it first.",
					out Failure);
			for (int i = 0; i < components.Count; i++)
				if (!ExactGalleryObject(components[i], receipt) || !FrozenContents(components[i]))
					return Fail("A gallery component changed contents or ownership; cleanup stopped before "
						+ "selecting any removal.", out Failure);
			for (int i = 0; i < components.Count; i++)
				if (!components[i].Destroy(null, Silent: true) || GameObject.Validate(components[i]))
					return Fail("An exact gallery component refused removal; remaining receipts stay named.",
						out Failure);
			if (!Owner.Destroy(null, Silent: true) || GameObject.Validate(Owner))
				return Fail("The exact gallery behavior root refused removal.", out Failure);
			return true;
		}

		private static List<GameObject> Components(Zone Zone, string Lot)
		{
			List<GameObject> result = new List<GameObject>();
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
						== KingdomArchitectureStamper.ComponentSchema) result.Add(item);
			result.Sort(delegate(GameObject a, GameObject b)
			{
				return string.CompareOrdinal(a.ID, b.ID);
			});
			return result;
		}

		private static void RollBackCreated(Zone Zone, string Lot, GameObject Works,
			GameObject Final, GameObject Synthetic)
		{
			List<GameObject> created = string.IsNullOrEmpty(Lot)
				? new List<GameObject>() : Components(Zone, Lot);
			for (int i = 0; i < created.Count; i++) SafeDestroy(created[i]);
			SafeDestroy(Final);
			SafeDestroy(Works);
			SafeDestroy(Synthetic);
		}

		private static void SafeDestroy(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return;
			try { Item.Destroy(null, Silent: true); }
			catch { }
		}

		private static bool TryUniqueGallery(Zone Zone, out GameObject Owner, out string Failure)
		{
			Owner = null;
			Failure = null;
			int count = 0;
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetIntProperty(GallerySchemaProperty) == GallerySchema
					&& item.HasIntProperty(KingdomArchitectureStamper.SchemaProperty))
				{
					Owner = item;
					count++;
				}
			if (count > 1)
			{
				Owner = null;
				return Fail("This zone has multiple gallery owners. Inspect their exact receipts; "
					+ "automatic staging and cleanup are disabled.", out Failure);
			}
			return true;
		}

		private static bool ExactGalleryObject(GameObject Item, string Receipt)
		{
			return GameObject.Validate(Item)
				&& Item.GetIntProperty(GallerySchemaProperty) == GallerySchema
				&& !string.IsNullOrEmpty(Receipt)
				&& Item.GetStringProperty(GalleryReceiptProperty) == Receipt;
		}

		private static bool TryParseVerdict(string Parameter, out string Verdict,
			out string Screenshot, out string Note, out string Failure)
		{
			Verdict = null;
			Screenshot = null;
			Note = null;
			Failure = null;
			string[] parts = (Parameter ?? "").Split(new char[] { '|' }, 3);
			string verdict = parts.Length > 0 ? parts[0].Trim().ToLowerInvariant() : "";
			string screenshot = parts.Length > 1 ? parts[1].Trim() : "";
			string note = parts.Length > 2 ? parts[2].Trim() : null;
			if (verdict != "pass" && verdict != "fail")
				return Fail("Use pass or fail: kingdom:archverdict pass|SCREENSHOT|NOTE", out Failure);
			if (screenshot.Length < 1 || screenshot.Length > MaxScreenshotChars
				|| screenshot.IndexOf('\n') >= 0 || screenshot.IndexOf('\r') >= 0)
				return Fail("Name the captured screenshot in 1–" + MaxScreenshotChars
					+ " single-line characters.", out Failure);
			if (note != null && (note.Length > MaxNoteChars || note.IndexOf('\n') >= 0
				|| note.IndexOf('\r') >= 0))
				return Fail("Keep the verdict note to " + MaxNoteChars + " single-line characters.",
					out Failure);
			Verdict = verdict;
			Screenshot = screenshot;
			Note = string.IsNullOrEmpty(note) ? null : note;
			return true;
		}

		private static string ReceiptFor(GalleryCase Case, int Total, string SnapshotHash)
		{
			string payload = GallerySchema.ToString(CultureInfo.InvariantCulture) + "\n"
				+ ModVersion + "\n" + XRLGame.CoreVersion + "\n" + Case.Number.ToString(
					CultureInfo.InvariantCulture) + "/" + Total.ToString(CultureInfo.InvariantCulture)
				+ "\n" + Case.Key + "\n" + SnapshotHash;
			return "ag1-" + Hash(payload).Substring(0, 24);
		}

		private static HashSet<int> ConnectionCells(Zone Zone)
		{
			HashSet<int> result = new HashSet<int>();
			foreach (ZoneConnection connection in Zone.EnumerateConnections())
				AddConnection(result, Zone, connection);
			if (Zone.ZoneConnectionCache != null)
				for (int i = 0; i < Zone.ZoneConnectionCache.Count; i++)
					AddConnection(result, Zone, Zone.ZoneConnectionCache[i]);
			return result;
		}

		private static void AddConnection(HashSet<int> Into, Zone Zone, ZoneConnection Connection)
		{
			if (Connection != null && Connection.X >= 0 && Connection.X < Zone.Width
				&& Connection.Y >= 0 && Connection.Y < Zone.Height)
				Into.Add(Connection.Y * Zone.Width + Connection.X);
		}

		private static string Hash(string Value)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(Value ?? ""));
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}

		private static string Bounded(string Text, int Maximum)
		{
			if (string.IsNullOrEmpty(Text) || Text.Length <= Maximum) return Text;
			return Text.Substring(0, Maximum);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
