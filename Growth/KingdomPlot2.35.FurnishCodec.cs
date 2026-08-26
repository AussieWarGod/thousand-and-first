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
		private static bool TryFreezeFurnishPlan(KingdomSystem System, Zone Z,
			KingdomPlotRules.PlotRect Rect, string Table, string Key, string StreamId,
			out List<FurnishRow> Rows)
		{
			Rows = new List<FurnishRow>();
			if (string.IsNullOrEmpty(Table) || Rect.Width <= 2 || Rect.Height <= 2) return true;
			if (!TryGetSpec(Key, out var spec)) return false;
			List<Cell> open = new List<Cell>();
			for (int y = Rect.Y1 + 1; y <= Rect.Y2 - 1; y++)
				for (int x = Rect.X1 + 1; x <= Rect.X2 - 1; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable()) open.Add(cell);
				}
			int rolls = KingdomPlotRules.ContentsRolls(spec.Size);
			for (int roll = 0; roll < rolls && open.Count > 0; roll++)
			{
				string blueprint;
				string failure;
				if (!KingdomSemanticSelection.TryChoosePopulationBlueprint(System, Table,
					null, StreamId, KingdomSemanticSelection.FurnishEventKind, 0UL,
					(uint)roll, out blueprint, out failure))
				{
					KingdomLog.Log("furnishing semantic plan refused: " + failure);
					return false;
				}
				if (Rows.Count >= MaxFurnishItems) return false;
				Cell cell = open[0]; open.RemoveAt(0);
				Rows.Add(new FurnishRow { Blueprint = blueprint, X = cell.X, Y = cell.Y });
			}
			return true;
		}

		private static string EncodeFurnish(List<FurnishRow> Rows, int Version = 2)
		{
			if (Rows == null || Rows.Count > MaxFurnishItems
				|| (Version != 1 && Version != 2)) return null;
			System.Text.StringBuilder text = new System.Text.StringBuilder(
				Version == 1 ? "f1" : "f2");
			for (int i = 0; i < Rows.Count; i++)
			{
				FurnishRow row = Rows[i];
				if (row == null || string.IsNullOrEmpty(row.Blueprint) || row.X < 0
					|| row.X > 1023 || row.Y < 0 || row.Y > 1023) return null;
				text.Append(';').Append(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(row.Blueprint)))
					.Append(',').Append(row.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture))
					.Append(',').Append(row.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)).Append(',')
					.Append(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(row.Id ?? "")))
					.Append(',').Append(row.Settled ? '1' : '0');
			}
			return text.Length <= KingdomConstructionRules.MaxPhysicalReceiptChars
				? text.ToString() : null;
		}

		private static bool TryDecodeFurnish(string Receipt, out List<FurnishRow> Rows)
		{
			Rows = null;
			if (string.IsNullOrEmpty(Receipt)
				|| Receipt.Length > KingdomConstructionRules.MaxPhysicalReceiptChars) return false;
			string[] terms = Receipt.Split(';');
			int version = terms[0] == "f1" ? 1 : terms[0] == "f2" ? 2 : 0;
			if (version == 0 || terms.Length - 1 > MaxFurnishItems) return false;
			List<FurnishRow> parsed = new List<FurnishRow>();
			try
			{
				for (int i = 1; i < terms.Length; i++)
				{
					string[] f = terms[i].Split(',');
					if (f.Length != 5 || (f[4] != "0" && f[4] != "1")
						|| !TryPlotCoordinate(f[1], out int x)
						|| !TryPlotCoordinate(f[2], out int y)
						|| x < 0 || x > 1023 || y < 0 || y > 1023) return false;
					string blueprint = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[0]));
					string id = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[3]));
					if (string.IsNullOrEmpty(blueprint) || blueprint.Length > 256 || id.Length > 128)
						return false;
					parsed.Add(new FurnishRow { Blueprint = blueprint, X = x, Y = y,
						Id = id.Length == 0 ? null : id, Settled = f[4] == "1" });
				}
			}
			catch { return false; }
			if (EncodeFurnish(parsed, version) != Receipt) return false;
			Rows = parsed;
			return true;
		}

		private static bool ExactFurnishing(GameObject Item, Zone Z, FurnishRow Row,
			string PlotId, string Receipt)
		{
			return GameObject.Validate(Item) && Item.ID == Row.Id && Item.CurrentZone == Z
				&& Item.CurrentCell == Z.GetCell(Row.X, Row.Y) && Item.Blueprint == Row.Blueprint
				&& Item.GetIntProperty(PlotPartProperty) == 1
				&& Item.GetStringProperty(PlotIdProperty) == PlotId
				&& Item.GetStringProperty(FurnishReceiptProperty) == Receipt;
		}

		private static bool FurnishLegacyDurable(GameObject Building, Zone Z,
			KingdomPlotRules.PlotRect Rect, string Table, string Id, string Key)
		{
			if (string.IsNullOrEmpty(Table) || Rect.Width <= 2 || Rect.Height <= 2)
			{
				return true;
			}
			if (!GameObject.Validate(Building) || Z == null || !TryGetSpec(Key, out _))
			{
				return false;
			}
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			string streamId;
			if (!Simulation.Kernel.KingdomSemanticSelectionRules.TryOwnerStreamId(
				"furnish-legacy", Id ?? (Key + ":" + Rect.X1 + ":" + Rect.Y1),
				out streamId)) return false;
			string encoded = Building.GetStringProperty(LegacyFurnishPlanProperty);
			List<FurnishRow> rows;
			if (string.IsNullOrEmpty(encoded))
			{
				if (!TryFreezeFurnishPlan(system, Z, Rect, Table, Key, streamId, out rows))
					return false;
				encoded = EncodeFurnish(rows);
				if (encoded == null) return false;
				Building.SetStringProperty(LegacyFurnishPlanProperty, encoded);
				if (!string.Equals(Building.GetStringProperty(LegacyFurnishPlanProperty),
					encoded, StringComparison.Ordinal)) return false;
			}
			if (!TryDecodeFurnish(encoded, out rows)) return false;
			for (int i = 0; i < rows.Count; i++)
			{
				FurnishRow row = rows[i];
				GameObject exact;
				KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z,
					row.Id, out exact);
				if (state == KingdomPhysicalLookupState.Ambiguous) return false;
				if (row.Settled)
				{
					if (state != KingdomPhysicalLookupState.Exact
						|| !ExactFurnishing(exact, Z, row, Id, streamId)) return false;
					continue;
				}
				if (!string.IsNullOrEmpty(row.Id))
				{
					if (state != KingdomPhysicalLookupState.Exact
						|| !ExactFurnishing(exact, Z, row, Id, streamId)) return false;
					row.Settled = true;
					if (!WriteLegacyFurnishPlan(Building, rows)) return false;
					continue;
				}
				Cell cell = Z.GetCell(row.X, row.Y);
				if (cell == null || !cell.IsEmpty() || !cell.IsPassable()) return false;
				GameObject placed;
				try { placed = GameObject.Create(row.Blueprint); }
				catch { return false; }
				if (!GameObject.Validate(placed)) return false;
				row.Id = placed.ID;
				placed.SetIntProperty(PlotPartProperty, 1);
				if (!string.IsNullOrEmpty(Id)) placed.SetStringProperty(PlotIdProperty, Id);
				placed.SetStringProperty(FurnishReceiptProperty, streamId);
				if (!WriteLegacyFurnishPlan(Building, rows))
				{
					RemoveCreatedWorks(placed, Z);
					return false;
				}
				GameObject accepted = null;
				try
				{
					accepted = cell.AddObject(placed);
					KingdomSurvey.ObserveAddResultInActive(Z, placed, accepted);
				}
				catch
				{
					if (RemoveCreatedWorks(placed, Z))
					{
						row.Id = null;
						WriteLegacyFurnishPlan(Building, rows);
					}
					return false;
				}
				if (!ReferenceEquals(accepted, placed)
					|| !ExactFurnishing(placed, Z, row, Id, streamId)) return false;
				row.Settled = true;
				if (!WriteLegacyFurnishPlan(Building, rows)) return false;
			}
			return true;
		}

		private static bool WriteLegacyFurnishPlan(GameObject Building,
			List<FurnishRow> Rows)
		{
			string encoded = EncodeFurnish(Rows);
			if (encoded == null || !GameObject.Validate(Building)) return false;
			Building.SetStringProperty(LegacyFurnishPlanProperty, encoded);
			return string.Equals(Building.GetStringProperty(LegacyFurnishPlanProperty),
				encoded, StringComparison.Ordinal);
		}

		// --- Saying so --------------------------------------------------------------------

		/// <summary>
		/// Says the yielding mark out loud at the moment the ground is spoken for, and files it in
		/// the ledger so a founder who was elsewhere reads it too. Told UP FRONT and once: the plot
		/// carries the same sentence in its own description from here on, so consent is given
		/// knowing what was promised and can be read back at any time.
		/// </summary>
		private static void SayYielding(KingdomSystem System, bool Yielding, string Name)
		{
			if (!Yielding || string.IsNullOrEmpty(Name))
			{
				return;
			}
			string line = KingdomPlotRules.YieldingLine(Name);
			MessageQueue.AddPlayerMessage("{{W|" + line + "}}");
			System?.Ledger.Note("{{W|" + line + "}}");
		}

		private static void AnnounceOnce(KingdomSystem System, GameObject Marker, string Message)
		{
			if (Marker == null || Marker.GetStringProperty(BlockAnnouncedProperty) == Message)
			{
				return;
			}
			Marker.SetStringProperty(BlockAnnouncedProperty, Message);
			System?.Ledger.Note("{{K|" + Message + "}}");
			MessageQueue.AddPlayerMessage("{{K|" + Message + "}}");
		}
	}
}
