using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomScenarioSaveAuthorityChecks
	{
		private static void AppendCompletedHeart(StringBuilder Text, Owner Owner)
		{
			Owner.Check();
			string receipt = Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
			Require(KingdomFoundingHeartRules.TryDecode(receipt, out KingdomFoundingHeartPlan plan)
				&& KingdomFoundingHeartRules.Complete(plan) && KingdomFoundingHeartRules.Encode(plan) == receipt
				&& plan.TransactionId == Owner.Transaction && plan.ZoneId == Owner.ZoneId,
				"completed heart receipt is not canonical authority for this owner");
			string seal = KingdomFoundingHeartRules.CompletionSeal(plan);
			Require(Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == seal,
				"completed heart seal does not match its receipt");
			Require(KingdomPlots.TryDecodePlotPayload(plan.Payload, out KingdomPlotRules.PlotRect rect,
				out string skin, out KingdomArchitectureIntent frozen, out bool legacy, out _)
				&& frozen != null && !legacy && string.IsNullOrEmpty(skin)
				&& rect.X1 == plan.RectX1 && rect.Y1 == plan.RectY1
				&& rect.X2 == plan.RectX2 && rect.Y2 == plan.RectY2
				&& KingdomPlotRules.ValidZoneRect(rect, Owner.Zone.Width, Owner.Zone.Height),
				"completed heart frozen geometry or architecture is malformed");
			Require(KingdomFoundingHeartStakeRules.TryDecode(plan.StakeTruth,
				out KingdomFoundingHeartStakeTruth stake) && frozen.BuildKey == stake.BuildKey
				&& stake.Carved == KingdomPlotRules.IsUnderground(Owner.Zone.Z),
				"completed heart stake truth disagrees with its frozen architecture");
			string raw = Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
			Require(KingdomFoundingHeartTerminalRules.TryDecode(raw, out KingdomFoundingHeartTerminalPlan terminal)
				&& KingdomFoundingHeartTerminalRules.Encode(terminal) == raw
				&& terminal.Phase == KingdomFoundingHeartTerminalPhase.EffectsSettled
				&& terminal.Raising == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.Heart == KingdomFoundingHeartSinkDisposition.Settled
				&& terminal.TransactionId == Owner.Transaction && terminal.ZoneId == Owner.ZoneId
				&& terminal.CompletionSeal == seal && terminal.PlotId == plan.PlotId
				&& terminal.PredecessorId == KingdomFoundingHeartRules.SlotId(plan, KingdomFoundingHeartRules.WorksSlot)
				&& terminal.FinalId == KingdomFoundingHeartRules.StableId(Owner.Transaction, Owner.ZoneId, "final")
				&& terminal.BuildKey == stake.BuildKey && terminal.Blueprint == stake.Blueprint
				&& terminal.X == frozen.MainWorldX && terminal.Y == frozen.MainWorldY
				&& Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalFailureProperty, null) == null,
				"completed heart terminal binding or settled effects changed");
			GameObject final = CompletedHeartLive(Owner, terminal);
			var strings = final.Property;
			var ints = final.IntProperty;
			Require(strings != null && ints != null, "completed heart property maps are absent");
			Field(Text, KingdomPlots.FoundingHeartTerminalProperty); Field(Text, raw);
			Field(Text, terminal.PredecessorId); Field(Text, final.IDIfAssigned);
			Field(Text, final.Blueprint); Field(Text, final.CurrentZone.ZoneID);
			Field(Text, final.CurrentCell.X.ToString(CultureInfo.InvariantCulture));
			Field(Text, final.CurrentCell.Y.ToString(CultureInfo.InvariantCulture));
			Field(Text, "unique-live-cell;count=1;inventory=absent;equipment=absent");
			CompletedHeartText(Text, final, KingdomPlots.FoundingHeartTerminalProperty, raw);
			CompletedHeartText(Text, final, KingdomUpgrade.BuildKeyProperty, terminal.BuildKey);
			CompletedHeartText(Text, final, KingdomPlots.PlotIdProperty, plan.PlotId);
			CompletedHeartText(Text, final, r_KingdomScaffold.RemovalProofProperty, terminal.PredecessorId);
			Require(r_KingdomScaffold.HasRemovalProof(final, terminal.PredecessorId)
				&& !final.HasStringProperty(KingdomPlots.FoundingHeartTerminalFailureProperty)
				&& !final.HasIntProperty(KingdomPlots.FoundingHeartTerminalFailureProperty),
				"completed heart removal proof or failure state changed");
			CompletedHeartInt(Text, final, "KingdomBuilt", 1);
			CompletedHeartInt(Text, final, KingdomPlots.PlotX1Property, rect.X1);
			CompletedHeartInt(Text, final, KingdomPlots.PlotY1Property, rect.Y1);
			CompletedHeartInt(Text, final, KingdomPlots.PlotX2Property, rect.X2);
			CompletedHeartInt(Text, final, KingdomPlots.PlotY2Property, rect.Y2);
			KingdomPlotRules.PlotRect foot = new KingdomPlotRules.PlotRect(stake.FootprintX1,
				stake.FootprintY1, stake.FootprintX2, stake.FootprintY2);
			Require(KingdomPlotRules.ValidZoneRect(foot, Owner.Zone.Width, Owner.Zone.Height)
				&& KingdomPlots.TryReadRect(final, out KingdomPlotRules.PlotRect observedRect)
				&& SameHeartRect(observedRect, rect)
				&& KingdomPlots.TryReadFootprint(final, out KingdomPlotRules.PlotRect observedFoot)
				&& SameHeartRect(observedFoot, foot), "completed heart physical geometry changed");
			CompletedHeartInt(Text, final, KingdomPlots.FootX1Property, foot.X1);
			CompletedHeartInt(Text, final, KingdomPlots.FootY1Property, foot.Y1);
			CompletedHeartInt(Text, final, KingdomPlots.FootX2Property, foot.X2);
			CompletedHeartInt(Text, final, KingdomPlots.FootY2Property, foot.Y2);
			CompletedHeartInt(Text, final, KingdomPlots.PlotRoofProperty, stake.Roof);
			AppendCompletedArchitecture(Text, final, frozen, plan.PlotId);
			Owner.Check();
			Require(ReferenceEquals(strings, final.Property) && ReferenceEquals(ints, final.IntProperty)
				&& ReferenceEquals(final, CompletedHeartLive(Owner, terminal))
				&& Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == receipt
				&& Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == seal
				&& Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == raw,
				"completed heart authority changed during observation");
			Require(KingdomConstruction.FindGlobalLiveId(terminal.PredecessorId, out _)
				== KingdomPhysicalLookupState.Absent, "completed heart predecessor still has live custody");
			Field(Text, "live-predecessor-absent"); Field(Text, terminal.PredecessorId);
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
				AppendAbsentHeartRoot(Text, Owner, KingdomPlots.FoundingHeartRootPrefix
					+ KingdomFoundingHeartRules.SlotId(plan, slot));
			AppendAbsentHeartRoot(Text, Owner, KingdomPlots.FoundingHeartFinalRootPrefix + terminal.FinalId);
			AppendAbsentHeartRoot(Text, Owner, "r_TAF_PlotFinalRoot:" + terminal.FinalId);
			Owner.Check();
		}

		private static void AppendCompletedArchitecture(StringBuilder Text, GameObject Final,
			KingdomArchitectureIntent Frozen, string PlotId)
		{
			Require(KingdomArchitectureStamper.TryReadOwner(Final, out KingdomArchitectureIntent observed,
				out ArchitectureLayoutSnapshot snapshot, out string lot, out _)
				&& observed != null && snapshot != null && lot == PlotId
				&& observed.EncodedSnapshot == Frozen.EncodedSnapshot && observed.SnapshotHash == Frozen.SnapshotHash
				&& SameHeartRect(observed.Rect, Frozen.Rect)
				&& observed.MainWorldX == Frozen.MainWorldX && observed.MainWorldY == Frozen.MainWorldY,
				"completed heart architecture endpoint disagrees with frozen payload");
			// Component simulation is not independently reproved here. The production complete
			// verifier can quarantine on failure, so it belongs to the later real audit, not capture.
			CompletedHeartInt(Text, Final, KingdomArchitectureStamper.SchemaProperty, KingdomArchitectureStamper.LayoutSchema);
			CompletedHeartInt(Text, Final, KingdomArchitectureStamper.NextLayerProperty, 3);
			CompletedHeartText(Text, Final, KingdomArchitectureStamper.LotIdProperty, lot);
			CompletedHeartText(Text, Final, KingdomArchitectureStamper.HashProperty, observed.SnapshotHash);
			Field(Text, observed.EncodedSnapshot);
			Field(Text, observed.MainWorldX.ToString(CultureInfo.InvariantCulture));
			Field(Text, observed.MainWorldY.ToString(CultureInfo.InvariantCulture));
		}

		private static GameObject CompletedHeartLive(Owner Owner, KingdomFoundingHeartTerminalPlan Terminal)
		{
			Owner.Check();
			Require(KingdomConstruction.FindGlobalLiveId(Terminal.FinalId, out GameObject final)
				== KingdomPhysicalLookupState.Exact && GameObject.Validate(final)
				&& final.IDIfAssigned == Terminal.FinalId && final.Blueprint == Terminal.Blueprint
				&& ReferenceEquals(final.CurrentZone, Owner.Zone) && final.CurrentCell != null
				&& ReferenceEquals(final.CurrentCell, Owner.Zone.GetCell(Terminal.X, Terminal.Y))
				&& final.InInventory == null && final.Equipped == null && final.Count == 1,
				"completed heart final is not one exact loaded endpoint");
			List<GameObject> bodies = Owner.Zone.GetObjects();
			Require(bodies != null && bodies.Count <= 65536, "completed heart loaded roster is unreadable or over bound");
			int copies = 0;
			foreach (GameObject body in bodies)
				if (body != null && body.IDIfAssigned == Terminal.FinalId)
				{
					Require(ReferenceEquals(body, final), "completed heart final ID has a foreign loaded body");
					copies++;
				}
			Require(copies == 1, "completed heart final has repeated or missing physical custody");
			List<GameObject> cellBodies = final.CurrentCell.GetObjects();
			Require(cellBodies != null && cellBodies.Count <= 65536, "completed heart cell custody is unreadable or over bound");
			int inCell = 0;
			foreach (GameObject body in cellBodies) if (ReferenceEquals(body, final)) inCell++;
			Require(inCell == 1, "completed heart final is not attached once to its recorded cell");
			Owner.Check();
			return final;
		}

		private static void AppendAbsentHeartRoot(StringBuilder Text, Owner Owner, string Key)
		{
			Require(!KingdomNativeRegressionContext.HasAnyState(Owner.Game, Key),
				"completed heart retains a saved custody root: " + Key);
			Field(Text, Key); Field(Text, "absent-across-five-tables");
		}

		private static void CompletedHeartText(StringBuilder Text, GameObject Body, string Key, string Expected)
		{
			Require(Body.HasStringProperty(Key) && !Body.HasIntProperty(Key) && Body.GetStringProperty(Key) == Expected,
				"completed heart text stamp changed: " + Key);
			Field(Text, Key); Field(Text, Body.GetStringProperty(Key));
		}

		private static void CompletedHeartInt(StringBuilder Text, GameObject Body, string Key, int Expected)
		{
			Require(Body.HasIntProperty(Key) && !Body.HasStringProperty(Key) && Body.GetIntProperty(Key) == Expected,
				"completed heart integer stamp changed: " + Key);
			Field(Text, Key); Field(Text, Body.GetIntProperty(Key).ToString(CultureInfo.InvariantCulture));
		}

		private static bool SameHeartRect(KingdomPlotRules.PlotRect A, KingdomPlotRules.PlotRect B)
		{
			return A.X1 == B.X1 && A.Y1 == B.Y1 && A.X2 == B.X2 && A.Y2 == B.Y2;
		}
	}
}
