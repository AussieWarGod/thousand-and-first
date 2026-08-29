using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free hosted-lot catalogue, identity, cardinality, and labour law.</summary>
	public static class KingdomHostedArcologyRules
	{
		public const int MaxHostedLots = 16;
		public const int MaxText = 512;
		public const long MaxLaborCatchupTicks = 36000L;
		private static readonly object Sync = new object();
		private static readonly SortedDictionary<string, KingdomHostedLotDefinition> Lots =
			new SortedDictionary<string, KingdomHostedLotDefinition>(StringComparer.Ordinal);

		static KingdomHostedArcologyRules()
		{
			string ignored;
			RegisterHostedLot(new KingdomHostedLotDefinition {
				Key = "arcologyward", DisplayName = "sealed vertical lodging ward",
				InteriorCell = "TAFArcologyWard", MaterialKey = "arcologyward",
				BuildTicks = 9600L, Crew = 2, Supports = "roof:26,luxury:2"
			}, out ignored);
			RegisterHostedLot(new KingdomHostedLotDefinition {
				Key = "arcologyterrace", DisplayName = "sealed hydroponic terrace",
				InteriorCell = "TAFArcologyTerrace", MaterialKey = "arcologyterrace",
				BuildTicks = 7200L, Crew = 2, Supports = "food:14", RequiresWater = true,
				PhysicalProducerBlueprint = "r_KingdomArcologyGrowbed",
				PhysicalProducerCount = 14
			}, out ignored);
		}

		/// <summary>Stable registration seam for bounded hosted content. Read-only definitions
		/// may provide a knowledge view but cannot acquire a material key or build queue.</summary>
		public static bool RegisterHostedLot(KingdomHostedLotDefinition Definition,
			out string Failure)
		{
			Failure = null;
			if (!Valid(Definition, out Failure)) return false;
			lock (Sync)
			{
				if (Lots.Count >= MaxHostedLots) return Fail("hosted-lot capacity is full", out Failure);
				if (Lots.ContainsKey(Definition.Key))
					return Fail("hosted-lot key is already registered", out Failure);
				Lots.Add(Definition.Key, Definition.Copy());
				return true;
			}
		}

		public static bool TryHostedLot(string Key, out KingdomHostedLotDefinition Definition)
		{
			Definition = null;
			if (string.IsNullOrEmpty(Key)) return false;
			lock (Sync)
			{
				KingdomHostedLotDefinition found;
				if (!Lots.TryGetValue(Key, out found)) return false;
				Definition = found.Copy();
				return true;
			}
		}

		public static List<KingdomHostedLotDefinition> RegisteredHostedLots()
		{
			List<KingdomHostedLotDefinition> answer = new List<KingdomHostedLotDefinition>();
			lock (Sync) foreach (var row in Lots) answer.Add(row.Value.Copy());
			return answer;
		}

		public static bool IsHostedLotKey(string Key)
		{
			KingdomHostedLotDefinition ignored;
			return TryHostedLot(Key, out ignored);
		}

		public static KingdomHostedAuthorityAction AuthorityAction(
			KingdomHostedArcologyAuthority Existing, string RealmId, string SettlementId,
			string ZoneId, string CarrierId)
		{
			if (!Token(RealmId) || !Token(SettlementId) || !Token(ZoneId) || !Token(CarrierId))
				return KingdomHostedAuthorityAction.Quarantine;
			if (Existing == null) return KingdomHostedAuthorityAction.Reserve;
			if (!Existing.Valid()) return KingdomHostedAuthorityAction.Quarantine;
			if (Existing.Phase == KingdomHostedAuthorityPhase.Quarantined)
				return KingdomHostedAuthorityAction.Reject;
			return Existing.RealmId == RealmId && Existing.SettlementId == SettlementId
				&& Existing.ZoneId == ZoneId && Existing.CarrierId == CarrierId
				? KingdomHostedAuthorityAction.Confirm : KingdomHostedAuthorityAction.Reject;
		}

		/// <summary>Selects one of two fixed authority slots. Only the current realm and the
		/// single realm retained by the exile archive are protected from replacement.</summary>
		public static int AuthoritySlotForWrite(KingdomHostedArcologyAuthority First,
			KingdomHostedArcologyAuthority Second, string CurrentRealmId,
			string RetainedRealmId)
		{
			if (!Token(CurrentRealmId) || (First != null && !First.Valid())
				|| (Second != null && !Second.Valid())) return -1;
			if (First != null && Second != null && First.RealmId == Second.RealmId) return -1;
			if (First != null && First.RealmId == CurrentRealmId) return 0;
			if (Second != null && Second.RealmId == CurrentRealmId) return 1;
			if (First == null) return 0;
			if (Second == null) return 1;
			bool protectFirst = !string.IsNullOrEmpty(RetainedRealmId)
				&& First.RealmId == RetainedRealmId;
			bool protectSecond = !string.IsNullOrEmpty(RetainedRealmId)
				&& Second.RealmId == RetainedRealmId;
			if (protectFirst && protectSecond) return -1;
			if (protectFirst) return 1;
			if (protectSecond) return 0;
			return 0;
		}

		public static string StableChildId(string RootId, string Role)
		{
			if (!Token(RootId) || !Token(Role)) return "";
			return "taf:arcology:v1:" + Digest("TAF-HOSTED-CHILD-V1", RootId, Role);
		}

		public static int AdvanceLabor(int Remaining, long LastTick, long NowTick,
			int PriorEffectiveness, out long NextTick)
		{
			NextTick = NowTick;
			if (Remaining <= 0) return 0;
			if (LastTick <= 0L || NowTick <= LastTick) return Remaining;
			long elapsed = NowTick - LastTick;
			if (elapsed > MaxLaborCatchupTicks) elapsed = MaxLaborCatchupTicks;
			int effectiveness = Math.Max(0, Math.Min(100, PriorEffectiveness));
			long spent = elapsed * effectiveness / 100L;
			return spent >= Remaining ? 0 : Remaining - (int)spent;
		}

		/// <summary>Advances only labour elapsed after the latest master-option edge.
		/// A hosted receipt can live on a physical carrier outside the resume transaction, so
		/// clamping its clock here prevents disabled time from becoming catch-up labour.</summary>
		public static int AdvanceLaborAfterMasterEdge(int Remaining, long LastTick,
			long MasterOptionTick, long NowTick, int PriorEffectiveness, out long NextTick)
		{
			return AdvanceLabor(Remaining, Math.Max(LastTick, MasterOptionTick),
				NowTick, PriorEffectiveness, out NextTick);
		}

		private static bool Valid(KingdomHostedLotDefinition D, out string Failure)
		{
			Failure = null;
			if (D == null || !Key(D.Key) || !Text(D.DisplayName) || !Key(D.InteriorCell))
				return Fail("hosted-lot identity is malformed", out Failure);
			if (D.ReadOnly)
			{
				if (!string.IsNullOrEmpty(D.MaterialKey) || D.BuildTicks != 0L || D.Crew != 0
					|| !string.IsNullOrEmpty(D.Supports) || !Key(D.KnowledgeView)
					|| !string.IsNullOrEmpty(D.PhysicalProducerBlueprint)
					|| D.PhysicalProducerCount != 0)
					return Fail("read-only hosted lots may expose only a knowledge view", out Failure);
			}
			else if (!Key(D.MaterialKey) || D.BuildTicks <= 0L || D.BuildTicks > 1000000L
				|| D.Crew < 1 || D.Crew > 12 || !Text(D.Supports))
				return Fail("hosted-lot work contract is malformed", out Failure);
			bool hasProducer = !string.IsNullOrEmpty(D.PhysicalProducerBlueprint)
				|| D.PhysicalProducerCount != 0;
			if (hasProducer && (!Key(D.PhysicalProducerBlueprint)
				|| D.PhysicalProducerCount < 1 || D.PhysicalProducerCount > 256))
				return Fail("hosted physical-producer contract is malformed", out Failure);
			if (Carries(D.Supports, "food") && !hasProducer)
				return Fail("hosted food support lacks a physical producer contract", out Failure);
			return true;
		}

		private static bool Carries(string Supports, string Kind)
		{
			string[] rows = (Supports ?? "").Split(',');
			for (int i = 0; i < rows.Length; i++)
			{
				string[] pair = rows[i].Split(':');
				if (pair.Length == 2 && string.Equals(pair[0].Trim(), Kind,
					StringComparison.OrdinalIgnoreCase)) return true;
			}
			return false;
		}

		private static bool Key(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 64) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ':')) return false;
			}
			return true;
		}

		private static bool Token(string Value) { return Text(Value) && Value.IndexOf('\0') < 0; }
		private static bool Text(string Value) { return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxText; }
		private static bool Fail(string Message, out string Failure) { Failure = Message; return false; }

		private static string Digest(string Domain, params string[] Fields)
		{
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{
					writer.Write(Domain); for (int i = 0; i < Fields.Length; i++) writer.Write(Fields[i]);
					writer.Flush(); using (SHA256 sha = SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray()); StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
						return text.ToString();
					}
				}
			}
			catch { return ""; }
		}
	}
}
