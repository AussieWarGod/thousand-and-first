using System;
using System.Collections.Generic;

using Genkit;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Dev-only camera framing for native architecture evidence. A staged XL case can sit near the
	/// edge of the viewport because the production gallery law chooses the nearest untouched canvas
	/// while forbidding the player from that canvas. This verb re-proves the exact gallery owner,
	/// chooses a safe walkable cell nearest the lot centre, moves only the test
	/// operator there, then centres Qud's native camera on the proved lot at native zoom. It never
	/// edits architecture, terrain, receipts, options, or production state. A receipted floor is a
	/// lawful framing surface only when the engine says that it descends from Floor; the before/after
	/// realized digest then proves that entering it did not randomize or otherwise change the lot.
	/// </summary>
	internal static class KingdomScenarioFrame
	{
		internal static string Run(out bool Ok)
		{
			Ok = false;
			GameObject player = The.Player;
			Zone zone = player?.CurrentZone;
			if (!GameObject.Validate(player) || zone == null || player.CurrentCell == null)
				return Refused("frame runs only with a live player in a loaded zone");
			GameObject owner;
			KingdomArchitectureIntent intent;
			string lot;
			string failure;
			if (!TryExactGalleryOwner(zone, out owner, out intent, out lot, out failure))
				return Refused(failure);
			if (!KingdomArchitectureStamper.TryVerifyComplete(owner, zone, out failure))
				return Refused("the exact staged lot is not physically complete: "
					+ KingdomScenarioRules.Bounded(failure));
			string beforeDigest;
			int beforeWidth;
			int beforeHeight;
			if (!KingdomRealizedArchitectureCapture.TryCapture(owner, out beforeDigest,
				out beforeWidth, out beforeHeight, out failure))
				return Refused("the exact staged lot cannot be captured before framing: "
					+ KingdomScenarioRules.Bounded(failure));
			Cell target = FindTarget(zone, player, owner, intent.Rect, lot);
			if (target == null)
				return Refused("the exact staged lot has no safe walkable framing cell "
					+ "in or beside it");
			if (!ReferenceEquals(player.CurrentCell, target))
			{
				try
				{
					if (!player.SystemLongDistanceMoveTo(target, 0, forced: true,
						ignoreCombat: true) || !ReferenceEquals(player.CurrentCell, target))
						return Refused("the engine declined to move the tester to the proved framing cell");
				}
				catch (Exception exception)
				{
					return Refused("the engine refused the framing move: "
						+ KingdomScenarioRules.Bounded(exception.Message));
				}
			}
			KingdomArchitectureIntent observed;
			ArchitectureLayoutSnapshot snapshot;
			string afterDigest;
			int afterWidth;
			int afterHeight;
			if (!GameObject.Validate(owner))
				return Changed("its exact owner became invalid");
			if (!KingdomArchitectureStamper.TryReadOwner(owner, out observed, out snapshot,
					out _, out failure))
				return Changed("its owner receipt became unreadable: "
					+ KingdomScenarioRules.Bounded(failure));
			if (observed == null)
				return Changed("its owner receipt returned no architecture intent");
			if (!SameLot(intent, observed))
				return Changed("its frozen lot identity changed");
			if (!KingdomRealizedArchitectureCapture.TryCapture(owner, out afterDigest,
					out afterWidth, out afterHeight, out failure))
				return Changed("its realized architecture became unreadable: "
					+ KingdomScenarioRules.Bounded(failure));
			if (beforeWidth != afterWidth || beforeHeight != afterHeight)
				return Changed("its realized extent changed from " + beforeWidth + "x"
					+ beforeHeight + " to " + afterWidth + "x" + afterHeight);
			if (!string.Equals(beforeDigest, afterDigest, StringComparison.Ordinal))
				return Changed("its exact realized-architecture digest changed");
			if (!TryCenterCamera(target, intent.Rect, out failure))
				return Refused("the native evidence camera could not centre on the proved lot: "
					+ KingdomScenarioRules.Bounded(failure));
			Ok = true;
			return "Framed " + intent.BuildKey + "/" + intent.VariantKey + " lot "
				+ intent.Rect.X1 + "," + intent.Rect.Y1 + "-" + intent.Rect.X2 + ","
				+ intent.Rect.Y2 + " from " + target.X + "," + target.Y
				+ "; native camera centred at " + intent.Rect.CenterX + ","
				+ intent.Rect.CenterY + " at zoom 1; its exact architecture digest is unchanged.";
		}

		/// <summary>
		/// Marshals every Unity presentation call to Qud's UI thread. Synchronising the player tracker
		/// first prevents the next screen-buffer update from replacing the lot-centred camera target
		/// with the tester's newly moved cell. Zoom one is the largest fixed value that keeps the
		/// authored 20-by-18 XL footprint inside the current native evidence frame; cropping may make
		/// the Workshop image close, but the game renderer must retain the complete building.
		/// </summary>
		private static bool TryCenterCamera(Cell Target, KingdomPlotRules.PlotRect Rect,
			out string Failure)
		{
			Failure = null;
			GameManager manager = GameManager.Instance;
			if (manager == null || manager.uiQueue == null
				|| GameManager.MainCameraLetterbox == null)
			{
				Failure = "Qud's native game manager, UI queue, or letterbox camera is unavailable";
				return false;
			}
			try
			{
				manager.uiQueue.awaitTask(delegate
				{
					manager.TargetZoomFactor = 1f;
					manager.SetPlayerCell(new Point2D(Target.X, Target.Y), updateCamera: false);
					manager.RefreshLayout(updateForceFullscreenIfSwapped: true);
					manager.CenterOnCell(Rect.CenterX, Rect.CenterY);
					GameManager.MainCameraLetterbox.OnUpdate();
				});
			}
			catch (Exception exception)
			{
				Failure = "Qud refused the UI-thread camera frame: "
					+ KingdomScenarioRules.Bounded(exception.Message);
				return false;
			}
			return true;
		}

		private static bool TryExactGalleryOwner(Zone Zone, out GameObject Owner,
			out KingdomArchitectureIntent Intent, out string Lot, out string Failure)
		{
			Owner = null;
			Intent = null;
			Lot = null;
			Failure = null;
			IList<GameObject> owners = KingdomScenarioCapture.Owners(Zone);
			for (int i = 0; i < owners.Count; i++)
			{
				GameObject candidate = owners[i];
				if (!KingdomScenarioGallerySlice.CarriesGalleryAuthority(candidate)
					|| string.IsNullOrEmpty(KingdomScenarioGallerySlice.Receipt(candidate))) continue;
				if (Owner != null)
				{
					Failure = "more than one staged gallery owner stands in this zone";
					return false;
				}
				Owner = candidate;
			}
			ArchitectureLayoutSnapshot snapshot;
			if (!GameObject.Validate(Owner))
			{
				Failure = "no exact staged gallery owner stands in this zone";
				return false;
			}
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out Intent, out snapshot,
				out Lot, out Failure) || Intent == null)
			{
				if (Failure == null) Failure = "the staged gallery owner has no architecture intent";
				return false;
			}
			return true;
		}

		private static Cell FindTarget(Zone Zone, GameObject Player, GameObject Owner,
			KingdomPlotRules.PlotRect Rect, string Lot)
		{
			Cell best = null;
			int bestDistance = int.MaxValue;
			int x1 = Math.Max(0, Rect.X1 - 1);
			int y1 = Math.Max(0, Rect.Y1 - 1);
			int x2 = Math.Min(Zone.Width - 1, Rect.X2 + 1);
			int y2 = Math.Min(Zone.Height - 1, Rect.Y2 + 1);
			for (int y = y1; y <= y2; y++)
				for (int x = x1; x <= x2; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (!Safe(cell, Player, Owner, Lot)) continue;
					int distance = Math.Max(Math.Abs(x - Rect.CenterX),
						Math.Abs(y - Rect.CenterY));
					if (distance >= bestDistance) continue;
					best = cell;
					bestDistance = distance;
				}
			return best;
		}

		private static bool Safe(Cell Cell, GameObject Player, GameObject Owner, string Lot)
		{
			if (Cell == null || Cell.HasOpenLiquidVolume() || !Cell.IsEmptyOfSolid()
				|| !Cell.IsPassable(Player, false)) return false;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (BelongsToLot(item, Lot) && !ReferenceEquals(item, Owner)
					&& !WalkableLotSurface(item)) return false;
				if (GameObject.Validate(item) && item.IsCreature
					&& !ReferenceEquals(item, Player)) return false;
			}
			return true;
		}

		private static bool WalkableLotSurface(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return false;
			GameObjectBlueprint blueprint = Item.GetBlueprint();
			return blueprint != null && blueprint.InheritsFrom("Floor");
		}

		private static bool BelongsToLot(GameObject Item, string Lot)
		{
			if (!GameObject.Validate(Item) || string.IsNullOrEmpty(Lot)) return false;
			return Item.HasStringProperty(KingdomPlots.PlotIdProperty)
					&& string.Equals(Item.GetStringProperty(KingdomPlots.PlotIdProperty), Lot,
						StringComparison.Ordinal)
				|| Item.HasStringProperty(KingdomArchitectureStamper.LotIdProperty)
					&& string.Equals(Item.GetStringProperty(
						KingdomArchitectureStamper.LotIdProperty), Lot, StringComparison.Ordinal);
		}

		private static bool SameLot(KingdomArchitectureIntent A, KingdomArchitectureIntent B)
		{
			return A != null && B != null
				&& string.Equals(A.SnapshotHash, B.SnapshotHash, StringComparison.Ordinal)
				&& string.Equals(A.BuildKey, B.BuildKey, StringComparison.Ordinal)
				&& string.Equals(A.VariantKey, B.VariantKey, StringComparison.Ordinal)
				&& A.Rect.X1 == B.Rect.X1 && A.Rect.Y1 == B.Rect.Y1
				&& A.Rect.X2 == B.Rect.X2 && A.Rect.Y2 == B.Rect.Y2;
		}

		private static string Refused(string Message)
		{
			return "{{R|Frame refused}}: " + KingdomScenarioRules.Bounded(Message);
		}

		private static string Changed(string Detail)
		{
			return Refused("the staged lot changed while the tester was framed: " + Detail);
		}
	}
}
