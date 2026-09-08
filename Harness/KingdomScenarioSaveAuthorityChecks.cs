using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomScenarioSaveAuthorityChecks
	{
		internal const string StateKey = "r_TAF_ScenarioSaveAuthority_v2";
		private const string Header = "taf-scenario-save-authority-v2";

		internal static void Prepare(KingdomSystem System, Zone Zone)
		{
			Owner owner = new Owner(System, Zone);
			string failure;
			Require(KingdomPolityProfileRuntime.TryReconcile(System, owner.Game.TimeTicks, out failure),
				"profile reconciliation refused: " + failure);
			owner.Check();
			VerifyReconciled(System, Zone);
			owner.Check();
		}

		internal static void Capture(KingdomSystem System, Zone Zone)
		{
			Owner owner = new Owner(System, Zone);
			Require(!KingdomNativeRegressionContext.HasAnyState(owner.Game, StateKey),
				"authority baseline already exists");
			string wire = CurrentWire(owner);
			owner.Check();
			Require(!KingdomNativeRegressionContext.HasAnyState(owner.Game, StateKey),
				"authority baseline appeared during capture");
			// PrimarySHA protects this saved baseline; the external snapshot does not independently
			// commit these authority fields. This witness is not historical-save compatibility proof.
			owner.Game.SetStringGameState(StateKey, wire);
			owner.Check();
			Require(KingdomScenarioDurableState.ProvesExactText(StateKey, wire),
				"authority baseline did not publish exactly");
			VerifyExact(System, Zone);
		}

		internal static void VerifyExact(KingdomSystem System, Zone Zone)
		{
			Owner owner = new Owner(System, Zone);
			KingdomDurableKeyObservation observed = KingdomScenarioDurableState.Observe(StateKey);
			string wire = observed == null ? null : observed.String;
			Require(ValidWire(wire) && KingdomScenarioDurableState.ProvesExactText(StateKey, wire),
				"authority baseline is absent, torn or malformed");
			string current = CurrentWire(owner);
			owner.Check();
			Require(KingdomScenarioDurableState.ProvesExactText(StateKey, wire)
				&& string.Equals(wire, current, StringComparison.Ordinal),
				"saved polity or founding-heart authority changed");
		}

		internal static void VerifyReconciled(KingdomSystem System, Zone Zone)
		{
			Owner owner = new Owner(System, Zone);
			long revision;
			KingdomSealRecord record = CanonicalProfile(owner, out revision);
			string failure;
			Require(KingdomPlots.AuditFoundingHeartReservations(System, Zone),
				"founding-heart reservation audit refused");
			owner.Check();
			Require(KingdomSealProfileCaptureRules.StillMatches(owner.Ledger, owner.Realm, record,
				revision, out failure), "canonical seal profile changed during audit: " + failure);
			HeartDigest(owner);
			owner.Check();
		}

		private static KingdomSealRecord CanonicalProfile(Owner Owner, out long Revision)
		{
			Owner.Check();
			KingdomSealRecord record = new KingdomSealRecord();
			string failure;
			Require(KingdomSealProfileCaptureRules.TryCapture(Owner.Ledger, Owner.Realm, record,
				out Revision, out failure), "canonical seal profile refused: " + failure);
			Require(KingdomPolityProfileRules.IsCommittedLegacyProfileSchema(record.ProfileSchema),
				"canonical seal profile lacks committed provenance");
			Owner.Check();
			return record;
		}

		private static string CurrentWire(Owner Owner)
		{
			Owner.Check();
			long revision;
			KingdomSealRecord record = CanonicalProfile(Owner, out revision);
			byte[] envelope = KingdomPolityCodec.EncodeEnvelope(Owner.Ledger);
			string profile;
			using (SHA256 sha = SHA256.Create())
				profile = BitConverter.ToString(sha.ComputeHash(envelope)).Replace("-", "").ToLowerInvariant();
			string heart = HeartDigest(Owner);
			Owner.Check();
			string failure;
			Require(KingdomSealProfileCaptureRules.StillMatches(Owner.Ledger, Owner.Realm, record,
				revision, out failure), "canonical seal profile changed during capture: " + failure);
			Owner.Check();
			return Header + "\n" + profile + "\n" + heart + "\n";
		}

		private static string HeartDigest(Owner Owner)
		{
			Owner.Check();
			string prefix = KingdomPlots.FoundingHeartReservationPrefix;
			HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal);
			for (int slot = 0; slot < KingdomFoundingHeartRules.SlotCount; slot++)
				expected.Add(prefix + KingdomFoundingHeartRules.StableId(Owner.Transaction, Owner.ZoneId,
					"slot-" + slot.ToString(CultureInfo.InvariantCulture)));
			expected.Add(prefix + KingdomFoundingHeartRules.StableId(Owner.Transaction, Owner.ZoneId, "final"));
			SortedDictionary<string, string> rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, string> row in Owner.Game.StringGameState)
			{
				if (!row.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
				Require(expected.Remove(row.Key) && KingdomScenarioDurableState.ProvesExactText(row.Key, row.Value),
					"founding-heart reservation identity or table shape changed");
				string transaction, zone, id;
				Require(KingdomFoundingHeartReservationRules.TryRead(row.Key, row.Value,
					out transaction, out zone, out id) && transaction == Owner.Transaction && zone == Owner.ZoneId,
					"founding-heart reservation payload changed owner");
				rows.Add(row.Key, row.Value);
			}
			Require(rows.Count == 7 && expected.Count == 0, "founding heart does not retain six slots and final reservation");
			NoReservations(Owner.Game.IntGameState);
			NoReservations(Owner.Game.Int64GameState);
			NoReservations(Owner.Game.ObjectGameState);
			NoReservations(Owner.Game.BooleanGameState);
			StringBuilder text = new StringBuilder();
			Field(text, "taf-scenario-founding-heart-v2");
			foreach (KeyValuePair<string, string> row in rows)
			{ Field(text, row.Key); Field(text, row.Value); }
			Field(text, KingdomPlots.FoundingHeartReceiptProperty);
			Field(text, Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null));
			Field(text, KingdomPlots.FoundingHeartSealProperty);
			Field(text, Owner.Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null));
			Field(text, "SettlementIdentityFirstClaimedZone");
			Field(text, Owner.System.SettlementIdentityFirstClaimedZone);
			Field(text, "SettlementIdentityOrigin");
			Field(text, ((int)Owner.System.SettlementIdentityOrigin).ToString(CultureInfo.InvariantCulture));
			Field(text, "SettlementIdentityTransactionId");
			Field(text, Owner.System.SettlementIdentityTransactionId);
			AppendCompletedHeart(text, Owner);
			Owner.Check();
			return KingdomScenarioSaveFiles.HashText(text.ToString());
		}

		private static void NoReservations<T>(IDictionary<string, T> Table)
		{
			foreach (string key in Table.Keys)
				Require(!key.StartsWith(KingdomPlots.FoundingHeartReservationPrefix, StringComparison.Ordinal),
					"founding-heart reservation occupies a foreign type table");
		}

		private static void Field(StringBuilder Text, string Value)
		{
			if (Value == null) { Text.Append("-1:"); return; }
			Require(Value.Length <= 65536, "authority field exceeds its bound");
			for (int i = 0; i < Value.Length; i++)
			{
				Require(!char.IsControl(Value[i]), "authority field contains a control character");
				if (!char.IsSurrogate(Value[i])) continue;
				Require(char.IsHighSurrogate(Value[i]) && i + 1 < Value.Length && char.IsLowSurrogate(Value[i + 1]),
					"authority field contains an unpaired surrogate");
				i++;
			}
			Text.Append(Value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(Value);
		}

		private static bool ValidWire(string Wire)
		{
			if (Wire == null || Wire.Length != Header.Length + 131) return false;
			string[] lines = Wire.Split('\n');
			return lines.Length == 4 && lines[0] == Header && lines[3] == ""
				&& Digest(lines[1]) && Digest(lines[2]);
		}

		private static bool Digest(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!(Value[i] >= '0' && Value[i] <= '9' || Value[i] >= 'a' && Value[i] <= 'f')) return false;
			return true;
		}

		private static void Require(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, "save authority: " + KingdomScenarioRules.Bounded(Failure));
		}

		private sealed class Owner
		{
			internal readonly XRLGame Game;
			internal readonly KingdomSystem System;
			internal readonly Zone Zone;
			internal readonly KingdomPolityLedger Ledger;
			internal readonly string Realm, Settlement, ZoneId, Transaction;
			private readonly object City, Strings, Ints, Longs, Objects, Booleans;
			private readonly GameObject Player;
			private readonly long Tick;

			internal Owner(KingdomSystem System, Zone Zone)
			{
				Game = The.Game; this.System = System; this.Zone = Zone; Player = The.Player;
				Require(Game != null && System != null && Zone != null, "game, system or zone is absent");
				Ledger = System.PolityLedger; City = System.City; Tick = Game.TimeTicks;
				Realm = System.CurrentRealmId; Settlement = System.CurrentSettlementId; ZoneId = Zone.ZoneID;
				Transaction = System.SettlementIdentityTransactionId;
				Strings = Game.StringGameState; Ints = Game.IntGameState; Longs = Game.Int64GameState;
				Objects = Game.ObjectGameState; Booleans = Game.BooleanGameState;
				Check();
			}

			internal void Check()
			{
				Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
					&& ReferenceEquals(The.Player, Player) && GameObject.Validate(Player)
					&& ReferenceEquals(Player.CurrentZone, Zone) && ReferenceEquals(Game.ZoneManager?.ActiveZone, Zone)
					&& ReferenceEquals(The.ZoneManager, Game.ZoneManager) && Zone.ZoneID == ZoneId && Game.TimeTicks == Tick
					&& Tick >= 0 && System.Founded && City != null && ReferenceEquals(System.City, City)
					&& Ledger != null && ReferenceEquals(System.PolityLedger, Ledger)
					&& !string.IsNullOrEmpty(Realm) && System.CurrentRealmId == Realm
					&& !string.IsNullOrEmpty(Settlement) && System.CurrentSettlementId == Settlement
					&& System.SettlementIdentityFirstClaimedZone == ZoneId
					&& System.SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction
					&& KingdomIdentityRules.IsFoundingTransaction(Transaction) && System.SettlementIdentityTransactionId == Transaction
					&& Strings != null && ReferenceEquals(Game.StringGameState, Strings)
					&& Ints != null && ReferenceEquals(Game.IntGameState, Ints)
					&& Longs != null && ReferenceEquals(Game.Int64GameState, Longs)
					&& Objects != null && ReferenceEquals(Game.ObjectGameState, Objects)
					&& Booleans != null && ReferenceEquals(Game.BooleanGameState, Booleans),
					"exact game, settlement, founding transaction or durable tables changed");
			}
		}
	}
}
