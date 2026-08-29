using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		public const string LegacyEffectsProperty = "r_TAF_PlotLegacyEffects";

		private static bool FinishLegacyPlotEffects(KingdomSystem System, Zone Z,
			GameObject Building)
		{
			if (!TryReadOrCreateLegacyEffects(System, Z, Building,
				out KingdomPlotLegacyEffectsPlan plan)) return false;
			if (plan.Heart)
			{
				string rung = KingdomPlotRules.HeartRungOf(plan.BuildKey).ToString(
					global::System.Globalization.CultureInfo.InvariantCulture);
				try { Z.SetZoneProperty(HeartRungProperty, rung); }
				catch { }
				if (Z.GetZoneProperty(HeartRungProperty, null) != rung) return false;
			}
			if (!DriveLegacyEffectSink(System, Z, Building, ref plan, 0, delegate
			{
				if (plan.Founded) KingdomCeremony.OnBuildingRaised(System, Building.CurrentCell,
					Building.GetStringProperty(r_KingdomScaffold.CompletionNameProperty)
						?? Building.ShortDisplayName ?? "structure", LegacyCompletionTick(Building),
					Building.GetStringProperty(r_KingdomScaffold.CompletionPlanProperty));
				else MessageQueue.AddPlayerMessage("{{G|The "
					+ (Building.ShortDisplayName ?? "structure") + " is complete.}}");
			})) return false;
			if (plan.Heart && !DriveLegacyEffectSink(System, Z, Building, ref plan, 1,
				() => KingdomCeremonyHeart.OnRungRaised(System, Z, plan.BuildKey, true))) return false;
			if (plan.Delve)
			{
				if (!KingdomDelveLink.TrySettle(Building, Z, out _)) return false;
				if (!ExactLegacyEffectEndpoint(System, Z, Building, plan)) return false;
				bool shaftReturned = false;
				try { KingdomDelve.RecordShaft(Z.ZoneID); shaftReturned = true; }
				catch { }
				if (!ExactLegacyEffectEndpoint(System, Z, Building, plan)
					|| !shaftReturned) return false;
				if (!KingdomDelve.ShaftStands(Z.ZoneID)) return false;
				if (!DriveLegacyEffectSink(System, Z, Building, ref plan, 2, delegate
				{
					if (!TryExactSettlementName(System, Z, out string settlementName))
						throw new global::System.InvalidOperationException("settlement name is unavailable");
					string opened = KingdomDelveRules.ShaftOpens(KingdomPresentation.Rich(settlementName));
					System.Ledger.Note("{{G|" + opened + "}}");
					MessageQueue.AddPlayerMessage("{{G|" + opened + "}}");
				})) return false;
				KingdomCivicKnowledgeRuntime.ObserveCurrentDelveBestEffort(System, Z,
					LegacyCompletionTick(Building));
			}
			return KingdomPlotLegacyEffectsRules.Complete(plan)
				&& ExactLegacyEffectEndpoint(System, Z, Building, plan)
				&& RetirePlotFinalRoot(plan.FinalId, Building);
		}

		private static bool DriveLegacyEffectSink(KingdomSystem System, Zone Z,
			GameObject Building, ref KingdomPlotLegacyEffectsPlan Plan, int Sink,
			global::System.Action Callback)
		{
			KingdomFoundingHeartSinkDisposition state = Sink == 0 ? Plan.Raising
				: Sink == 1 ? Plan.HeartSink : Plan.DelveSink;
			if (!ExactLegacyEffectEndpoint(System, Z, Building, Plan)) return false;
			if (state == KingdomFoundingHeartSinkDisposition.Settled
				|| state == KingdomFoundingHeartSinkDisposition.Lost) return true;
			if (state == KingdomFoundingHeartSinkDisposition.Attempting)
				return AdvanceLegacyEffectSink(System, Z, Building, ref Plan, Sink,
					KingdomFoundingHeartSinkDisposition.Attempting,
					KingdomFoundingHeartSinkDisposition.Lost);
			if (!AdvanceLegacyEffectSink(System, Z, Building, ref Plan, Sink,
				KingdomFoundingHeartSinkDisposition.Pending,
				KingdomFoundingHeartSinkDisposition.Attempting)) return false;
			bool callbackReturned = false;
			try { Callback(); callbackReturned = true; }
			catch { }
			if (!ExactLegacyEffectEndpoint(System, Z, Building, Plan)) return false;
			if (!callbackReturned) return false;
			return AdvanceLegacyEffectSink(System, Z, Building, ref Plan, Sink,
				KingdomFoundingHeartSinkDisposition.Attempting,
				KingdomFoundingHeartSinkDisposition.Settled);
		}

		private static bool AdvanceLegacyEffectSink(KingdomSystem System, Zone Z,
			GameObject Building, ref KingdomPlotLegacyEffectsPlan Plan, int Sink,
			KingdomFoundingHeartSinkDisposition Expected,
			KingdomFoundingHeartSinkDisposition Next)
		{
			string prior = KingdomPlotLegacyEffectsRules.Encode(Plan);
			KingdomPlotLegacyEffectsPlan changed = Plan.Copy();
			if (!ExactLegacyEffectEndpoint(System, Z, Building, Plan)
				|| !KingdomPlotLegacyEffectsRules.TryAdvance(changed, Sink, Expected, Next)
				|| Building.GetStringProperty(LegacyEffectsProperty) != prior) return false;
			string encoded = KingdomPlotLegacyEffectsRules.Encode(changed);
			Building.SetStringProperty(LegacyEffectsProperty, encoded);
			if (Building.GetStringProperty(LegacyEffectsProperty) != encoded) return false;
			Plan = changed;
			return true;
		}

		private static bool TryReadOrCreateLegacyEffects(KingdomSystem System, Zone Z,
			GameObject Building, out KingdomPlotLegacyEffectsPlan Plan)
		{
			Plan = null;
			if (!ExactLegacyEffectBase(System, Z, Building)) return false;
			string raw = Building.GetStringProperty(LegacyEffectsProperty);
			if (string.IsNullOrEmpty(raw))
			{
				if (!KingdomPlotLegacyEffectsRules.TryCreate(Building.IDIfAssigned,
					Building.GetStringProperty(PlotFinalPredecessorProperty), Building.Blueprint,
					Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty),
					Building.GetStringProperty(PlotIdProperty), Z.ZoneID, Building.CurrentCell.X,
					Building.CurrentCell.Y, System.Founded,
					Building.GetIntProperty(HeartPlotProperty) == 1,
					System.Founded && KingdomDelveRules.IsDelve(Building.GetStringProperty(
						KingdomUpgrade.BuildKeyProperty)), out Plan)) return false;
				raw = KingdomPlotLegacyEffectsRules.Encode(Plan);
				Building.SetStringProperty(LegacyEffectsProperty, raw);
			}
			return KingdomPlotLegacyEffectsRules.TryDecode(raw, out Plan)
				&& ExactLegacyEffectEndpoint(System, Z, Building, Plan);
		}

		private static bool ExactLegacyEffectBase(KingdomSystem System, Zone Z,
			GameObject Building)
		{
			if (System == null || Z == null || !GameObject.Validate(Building)) return false;
			string predecessor;
			try { predecessor = Building.GetStringProperty(PlotFinalPredecessorProperty); }
			catch { return false; }
			return Building.CurrentZone == Z && Building.CurrentCell != null
				&& string.IsNullOrEmpty(Building.GetStringProperty(KingdomConstruction.ReceiptProperty))
				&& Building.GetIntProperty("KingdomBuilt") == 1 && !string.IsNullOrEmpty(predecessor)
				&& r_KingdomScaffold.HasRemovalProof(Building, predecessor)
				&& ExactLegacyPlotRemovalTombstone(predecessor,
					Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty))
				&& ExactPlotFinalRootCustody(Building.IDIfAssigned, Building);
		}

		private static bool ExactLegacyEffectEndpoint(KingdomSystem System, Zone Z,
			GameObject Building, KingdomPlotLegacyEffectsPlan Plan)
		{
			return ExactLegacyEffectBase(System, Z, Building)
				&& KingdomPlotLegacyEffectsRules.Valid(Plan) && Plan.FinalId == Building.IDIfAssigned
				&& Plan.PredecessorId == Building.GetStringProperty(PlotFinalPredecessorProperty)
				&& Plan.Blueprint == Building.Blueprint
				&& Plan.BuildKey == Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty)
				&& Plan.PlotId == Building.GetStringProperty(PlotIdProperty)
				&& Plan.ZoneId == Z.ZoneID && Plan.X == Building.CurrentCell.X
				&& Plan.Y == Building.CurrentCell.Y && Plan.Founded == System.Founded
				&& Plan.Heart == (Building.GetIntProperty(HeartPlotProperty) == 1)
				&& Building.GetStringProperty(LegacyEffectsProperty)
					== KingdomPlotLegacyEffectsRules.Encode(Plan);
		}

		private static long LegacyCompletionTick(GameObject Building)
		{
			return long.TryParse(Building?.GetStringProperty(r_KingdomScaffold.CompletionTickProperty),
				global::System.Globalization.NumberStyles.Integer,
				global::System.Globalization.CultureInfo.InvariantCulture, out long tick) ? tick : 0L;
		}

		/// <summary>Audits only already-loaded OGS roots in the activated zone; never thaws a zone.</summary>
		internal static bool RecoverLegacyPlotFinalEffects(KingdomSystem System, Zone Z)
		{
			if (System == null || Z == null || The.Game?.ObjectGameState == null
				|| The.Game.ObjectGameState.Count > 65536) return false;
			List<GameObject> candidates = new List<GameObject>();
			foreach (KeyValuePair<string, object> row in The.Game.ObjectGameState)
				if (row.Key.StartsWith(PlotFinalRootPrefix, global::System.StringComparison.Ordinal)
					&& row.Value is GameObject candidate && GameObject.Validate(candidate)
					&& candidate.CurrentZone == Z) candidates.Add(candidate);
			for (int i = 0; i < candidates.Count; i++)
			{
				GameObject candidate = candidates[i];
				if (!string.IsNullOrEmpty(candidate.GetStringProperty(
					KingdomConstruction.ReceiptProperty))) continue;
				string predecessor = candidate.GetStringProperty(PlotFinalPredecessorProperty);
				bool ready = !string.IsNullOrEmpty(predecessor)
					&& r_KingdomScaffold.HasRemovalProof(candidate, predecessor);
				if (!ready && string.IsNullOrEmpty(candidate.GetStringProperty(LegacyEffectsProperty)))
					continue;
				if (!FinishLegacyPlotEffects(System, Z, candidate)) return false;
			}
			return true;
		}
	}
}
