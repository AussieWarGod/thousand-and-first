using System;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		internal sealed class CapacityObservation
		{
			internal readonly KingdomChronicleCapacityWitness Witness;
			internal readonly XRLGame Game;
			internal readonly KingdomSystem System;
			internal readonly string Realm, Settlement, Raw;
			internal readonly object[] Tables;
			internal CapacityObservation(KingdomChronicleCapacityWitness witness, XRLGame game,
				KingdomSystem system, string realm, string settlement, string raw, object[] tables)
			{ Witness = witness; Game = game; System = system; Realm = realm; Settlement = settlement; Raw = raw; Tables = tables; }
		}

		/// <summary>Observes an absent dated event in an exact full registry. This does not publish,
		/// mark a sink Lost, or infer anything from a prior publisher's return value.</summary>
		internal static bool TryObserveCapacityRefusalAt(KingdomSystem system, string eventId,
			string text, long tick, Func<bool> ownerExact, out CapacityObservation observation)
		{
			observation = null;
			try
			{
				XRLGame game = The.Game;
				if (ownerExact == null || !PublicationAllowed(ownerExact) || game == null || system == null
					|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
					|| !system.TryGetCurrentIdentity(out string realm, out string settlement)
					|| tick > game.TimeTicks
					|| !KingdomChronicleCapacityRules.TryFingerprint(realm, settlement, eventId, text, tick, out string fingerprint)
					|| !ReadCapacityTables(game, out KingdomDurableKeyObservation shape, out object[] tables)
					|| !KingdomChronicleCapacityRules.TryObserve(shape, eventId, fingerprint, out var witness)) return false;
				var value = new CapacityObservation(witness, game, system, realm, settlement, shape.String, tables);
				if (!ReproveCapacityRefusal(value, ownerExact)) return false;
				observation = value; return true;
			}
			catch { return false; }
		}

		/// <summary>Same capacity proof for the existing undated RecordOnce fingerprint. A prior
		/// event, including a foreign fingerprint, stays on its original publisher/replay path.</summary>
		internal static bool TryObserveCapacityRefusal(KingdomSystem system, string eventId,
			string text, Func<bool> ownerExact, out CapacityObservation observation)
		{
			observation = null;
			try
			{
				XRLGame game = The.Game;
				if (ownerExact == null || !PublicationAllowed(ownerExact) || game == null || system == null
					|| !ReferenceEquals(game.GetSystem<KingdomSystem>(), system)
					|| !system.TryGetCurrentIdentity(out string realm, out string settlement)
					|| !KingdomChronicleReceiptRules.TryFingerprint(eventId, text, false, null, out string fingerprint)
					|| !ReadCapacityTables(game, out KingdomDurableKeyObservation shape, out object[] tables)
					|| !KingdomChronicleCapacityRules.TryObserve(shape, eventId, fingerprint, out var witness)) return false;
				var value = new CapacityObservation(witness, game, system, realm, settlement, shape.String, tables);
				if (!ReproveCapacityRefusal(value, ownerExact)) return false;
				observation = value; return true;
			}
			catch { return false; }
		}

		/// <summary>Rechecks the same game, seat, all five table references and exact authority bytes
		/// immediately before the caller publishes its own durable non-delivery witness.</summary>
		internal static bool ReproveCapacityRefusal(CapacityObservation value, Func<bool> ownerExact)
		{
			try
			{
				if (value == null || ownerExact == null || !PublicationAllowed(ownerExact)
					|| !ReferenceEquals(The.Game, value.Game)
					|| !ReferenceEquals(value.Game.GetSystem<KingdomSystem>(), value.System)
					|| !value.System.TryGetCurrentIdentity(out string realm, out string settlement)
					|| realm != value.Realm || settlement != value.Settlement
					|| !ReadCapacityTables(value.Game, out KingdomDurableKeyObservation row, out object[] tables)
					|| !KingdomScenarioStateShape.TryAuthorityText(row, out string raw, out bool present, out _)
					|| !present || !string.Equals(raw, value.Raw, StringComparison.Ordinal)) return false;
				for (int i = 0; i < tables.Length; i++) if (!ReferenceEquals(tables[i], value.Tables[i])) return false;
				return ReferenceEquals(The.Game, value.Game);
			}
			catch { return false; }
		}

		private static bool ReadCapacityTables(XRLGame game, out KingdomDurableKeyObservation row, out object[] tables)
		{
			row = new KingdomDurableKeyObservation(); tables = new object[] { game.StringGameState,
				game.IntGameState, game.Int64GameState, game.ObjectGameState, game.BooleanGameState };
			foreach (object table in tables) if (table == null) return false;
			row.HasString = game.HasStringGameState(EventRegistryState);
			row.HasInt = game.HasIntGameState(EventRegistryState);
			row.HasInt64 = game.HasInt64GameState(EventRegistryState);
			row.HasObject = game.HasObjectGameState(EventRegistryState);
			row.HasBoolean = game.HasBooleanGameState(EventRegistryState);
			if (row.HasString) row.String = game.GetStringGameState(EventRegistryState);
			return ReferenceEquals(game, The.Game) && ReferenceEquals(tables[0], game.StringGameState)
				&& ReferenceEquals(tables[1], game.IntGameState) && ReferenceEquals(tables[2], game.Int64GameState)
				&& ReferenceEquals(tables[3], game.ObjectGameState) && ReferenceEquals(tables[4], game.BooleanGameState);
		}
	}
}
