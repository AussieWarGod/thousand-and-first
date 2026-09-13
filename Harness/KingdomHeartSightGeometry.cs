using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read physical output and custody; no stamping, completion, quarantine or repair.</summary>
	internal static class KingdomHeartSightGeometry
	{
		internal static string Capture(out string detail)
		{
			Zone zone = The.ActiveZone;
			Require(zone != null && !KingdomSurvey.HasBoundPass, "heart zone unavailable");
			string receipt = zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
			Require(KingdomFoundingHeartRules.TryDecode(receipt, out KingdomFoundingHeartPlan plan), "heart plan absent");
			string wire = zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			Require(KingdomFoundingHeartTerminalRules.TryDecode(wire, out KingdomFoundingHeartTerminalPlan terminal)
				&& terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled,
				"ordinary heart construction has not settled: "
					+ zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null));
			Require(terminal.TransactionId == plan.TransactionId && terminal.ZoneId == zone.ZoneID
				&& terminal.ZoneId == plan.ZoneId && terminal.CompletionSeal == KingdomFoundingHeartRules.CompletionSeal(plan)
				&& terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)
				&& terminal.FinalId == KingdomFoundingHeartRules.StableId(plan.TransactionId, plan.ZoneId, "final")
				&& terminal.Blueprint == "r_KingdomRiteGround" && terminal.BuildKey == "heartbasin"
				&& terminal.PlotId == plan.PlotId, "heart terminal authority differs");
			Require(KingdomPlots.FindGlobalFoundingHeartId(terminal.FinalId, out GameObject owner, out bool graveyard)
				== KingdomPhysicalLookupState.Exact && !graveyard && GameObject.Validate(owner)
				&& owner.Blueprint == terminal.Blueprint && ReferenceEquals(owner.CurrentCell, zone.GetCell(terminal.X, terminal.Y))
				&& owner.Count == 1 && owner.InInventory == null && owner.Equipped == null,
				"heart final is not one live object on its receipted cell");
			Require(owner.GetStringProperty(KingdomPlots.FoundingHeartTerminalProperty) == wire
				&& owner.GetStringProperty(KingdomUpgrade.BuildKeyProperty) == terminal.BuildKey
				&& owner.GetStringProperty(KingdomPlots.PlotIdProperty) == terminal.PlotId
				&& r_KingdomScaffold.HasRemovalProof(owner, terminal.PredecessorId)
				&& zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) == null,
				"heart final mirror or predecessor removal proof differs");
			Require(KingdomRealizedArchitectureCapture.TryCapture(owner, out string digest, out int width,
				out int height, out string failure, Stable: true), "realized heart capture refused: " + failure);
			Require(KingdomArchitectureStamper.TryReadOwner(owner, out var intent, out var snapshot, out _, out failure), failure);
			Require(snapshot.PlanKey == "civic-heart" && snapshot.TierKey == "heartbasin"
				&& snapshot.BindingKey == "civic-s-heart" && snapshot.Width == 6 && snapshot.Height == 4
				&& snapshot.FootprintX == 1 && snapshot.FootprintY == 0 && snapshot.FootprintWidth == 4
				&& snapshot.FootprintHeight == 4 && KingdomPlots.RoofOf(owner) == KingdomPlotRules.RoofState.Open,
				"first heart is no longer the authored open 6x4 camp with 4x4 footprint");
			var expected = new HashSet<string> { "1,0", "2,0", "3,0", "4,0", "0,1", "5,1", "0,2" };
			var rows = new List<string>();
			var cells = new List<string>();
			foreach (ArchitecturePlacement placement in snapshot.Placements)
			{
				if (placement.Blueprint != "r_KingdomStructureCanvasWall") continue;
				Require(expected.Remove(placement.X + "," + placement.Y), "unexpected or duplicate canvas coordinate");
				Require(KingdomArchitectureRuntime.TryWorldPlacement(snapshot, intent.Rect, placement,
					out int x, out int y, out failure), failure);
				string id = owner.GetStringProperty(KingdomArchitectureStamper.OutputIdPrefix + placement.Slot.Replace(':', '_'));
				Require(!string.IsNullOrEmpty(id), "canvas output identity absent");
				GameObject canvas = null;
				foreach (GameObject item in zone.GetCell(x, y).GetObjects())
					if (item.IDIfAssigned == id) { Require(canvas == null, "duplicate canvas custody"); canvas = item; }
				Require(GameObject.Validate(canvas) && canvas.Blueprint == placement.Blueprint && canvas.Physics?.Solid == true
					&& ReferenceEquals(canvas.CurrentCell, zone.GetCell(x, y)) && canvas.Render != null
					&& (!string.IsNullOrEmpty(canvas.Render.Tile) || !string.IsNullOrEmpty(canvas.Render.RenderString)),
					"canvas missing, nonblocking or undrawn at " + x + "," + y);
				rows.Add(Encode(id, placement.Slot, x, y)); cells.Add(x + "," + y);
			}
			Require(expected.Count == 0 && rows.Count == 7, "completed heart lacks its seven canvas cells including both flanks");
			rows.Sort(StringComparer.Ordinal); cells.Sort(StringComparer.Ordinal);
			detail = "completed=true; canvas=7; canvas-cells=" + string.Join("|", cells)
				+ "; layout=" + width + "x" + height + "; roof=open; enclosed=false; realized-sha256=" + digest
				+ "; heart=" + owner.IDIfAssigned + "; synthetic-completion=false; world-repair=false";
			return Encode(The.Game.GameID, zone.ZoneID, receipt, wire, owner.IDIfAssigned, intent.EncodedSnapshot,
				digest, string.Join("\n", rows));
		}

		private static string Encode(params object[] values)
		{
			var fields = new List<string>();
			foreach (object value in values)
				fields.Add(value == null ? "-" : "+" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
					Convert.ToString(value, CultureInfo.InvariantCulture))));
			return string.Join("\n", fields);
		}
		private static void Require(bool value, string failure) => KingdomHeartSightNativeProvider.Require(value, failure);
	}
}
