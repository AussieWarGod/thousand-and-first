using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Detached exact topology replacement prepared before second-city publication.
	/// Live Trade and Carry references remain untouched until one callback-free commit.</summary>
	public sealed class KingdomSecondCityTopologyPlan
	{
		internal string RealmId;
		internal string SettlementId;
		internal List<string> SettlementIds;
		internal KingdomTradeBook SourceTrade;
		internal KingdomCarryBook SourceCarry;
		internal KingdomTradeBook ReplacementTrade;
		internal KingdomCarryBook ReplacementCarry;
		internal byte[] SourceTradeBytes;
		internal byte[] SourceCarryBytes;
		internal bool TradeAlreadyExact;
		internal bool CarryAlreadyExact;
		internal bool Committed;
	}

	/// <summary>Engine-free prepare/commit and abort/settle laws for later-city authority.
	/// No caller may publish one book without the other or erase a forward-recovery tuple.</summary>
	public static class KingdomSecondCityPublicationRules
	{
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryPrepare(string RealmId, IEnumerable<string> CurrentSettlementIds,
			string NewSettlementId, KingdomTradeBook Trade, KingdomCarryBook Carry,
			out KingdomSecondCityTopologyPlan Plan, out string Failure)
		{
			Plan = null;
			Failure = null;
			if (!KingdomIdentityRules.IsRealmId(RealmId) ||
				!KingdomIdentityRules.IsSettlementId(NewSettlementId) ||
				!TryTarget(RealmId, CurrentSettlementIds, NewSettlementId,
					out List<string> target, out Failure) ||
				!ExactTradeAuthority(Trade, RealmId, null) ||
				!ExactCarryAuthority(Carry, RealmId, null))
			{
				Failure = Failure ??
					"Second-city topology requires exact bound Trade and Carry authority.";
				return false;
			}
			try
			{
				byte[] tradeBytes = KingdomTradeCodec.EncodeEnvelope(Trade);
				byte[] carryBytes = CarryBytes(Carry);
				KingdomTradeBook tradeCandidate = KingdomTradeCodec.DecodeEnvelopeRaw(tradeBytes);
				KingdomCarryBook carryCandidate = CarryFromBytes(carryBytes);
				bool tradeExact = ExactTradeAuthority(Trade, RealmId, target);
				bool carryExact = ExactCarryAuthority(Carry, RealmId, target);
				if (!KingdomTradeRules.ExpandExactIdentity(tradeCandidate, RealmId,
					target, out Failure) ||
					!KingdomLifecycleRules.ExpandCarryIdentity(carryCandidate, RealmId,
						target, out Failure) ||
					!ExactTradeAuthority(tradeCandidate, RealmId, target) ||
					!ExactCarryAuthority(carryCandidate, RealmId, target))
				{
					Failure = Failure ??
						"Detached second-city topology did not retain exact authority.";
					return false;
				}
				Plan = new KingdomSecondCityTopologyPlan
				{
					RealmId = RealmId,
					SettlementId = NewSettlementId,
					SettlementIds = target,
					SourceTrade = Trade,
					SourceCarry = Carry,
					ReplacementTrade = tradeCandidate,
					ReplacementCarry = carryCandidate,
					SourceTradeBytes = tradeBytes,
					SourceCarryBytes = carryBytes,
					TradeAlreadyExact = tradeExact,
					CarryAlreadyExact = carryExact
				};
				return true;
			}
			catch (Exception ex)
			{
				Failure = "Second-city topology could not be frozen: " + ex.Message;
				Plan = null;
				return false;
			}
		}

		/// <summary>Commits both detached books with no engine callback between assignments.
		/// An exact retry preserves both live references and their bytes.</summary>
		public static bool TryCommit(KingdomSecondCityTopologyPlan Plan,
			ref KingdomTradeBook Trade, ref KingdomCarryBook Carry, out string Failure)
		{
			Failure = null;
			if (Plan == null)
			{
				Failure = "Second-city topology plan is absent.";
				return false;
			}
			if (Plan.Committed)
			{
				if (ExactTradeAuthority(Trade, Plan.RealmId, Plan.SettlementIds) &&
					ExactCarryAuthority(Carry, Plan.RealmId, Plan.SettlementIds)) return true;
				Failure = "Committed second-city topology was replaced.";
				return false;
			}
			byte[] currentTrade;
			byte[] currentCarry;
			try
			{
				currentTrade = TradeBytes(Trade);
				currentCarry = CarryBytes(Carry);
			}
			catch (Exception ex)
			{
				Failure = "Trade or Carry authority could not be reproved: " + ex.Message;
				return false;
			}
			if (!ReferenceEquals(Trade, Plan.SourceTrade) ||
				!ReferenceEquals(Carry, Plan.SourceCarry) ||
				!ExactBytes(Plan.SourceTradeBytes, currentTrade) ||
				!ExactBytes(Plan.SourceCarryBytes, currentCarry))
			{
				Failure = "Trade or Carry authority changed after second-city preflight.";
				return false;
			}
			if (!ExactTradeAuthority(Plan.ReplacementTrade, Plan.RealmId,
					Plan.SettlementIds) ||
				!ExactCarryAuthority(Plan.ReplacementCarry, Plan.RealmId,
					Plan.SettlementIds))
			{
				Failure = "Detached second-city topology no longer proves its target.";
				return false;
			}
			KingdomTradeBook nextTrade = Plan.TradeAlreadyExact
				? Trade : Plan.ReplacementTrade;
			KingdomCarryBook nextCarry = Plan.CarryAlreadyExact
				? Carry : Plan.ReplacementCarry;
			Trade = nextTrade;
			Carry = nextCarry;
			Plan.Committed = true;
			return ExactTradeAuthority(Trade, Plan.RealmId, Plan.SettlementIds) &&
				ExactCarryAuthority(Carry, Plan.RealmId, Plan.SettlementIds);
		}

		public static bool CanAbort(IList<string> PublishedSettlementIds,
			string PendingSettlementId, string RealmId, KingdomTradeBook Trade,
			KingdomCarryBook Carry)
		{
			return ExactPublished(PublishedSettlementIds, PendingSettlementId,
				MustContainPending: false, RealmId, Trade, Carry);
		}

		public static bool CanSettle(IList<string> PublishedSettlementIds,
			string PendingSettlementId, string RealmId, KingdomTradeBook Trade,
			KingdomCarryBook Carry)
		{
			return ExactPublished(PublishedSettlementIds, PendingSettlementId,
				MustContainPending: true, RealmId, Trade, Carry);
		}

		public static bool ExactTopology(IList<string> PublishedSettlementIds,
			string RealmId, KingdomTradeBook Trade, KingdomCarryBook Carry)
		{
			return PublishedSettlementIds != null &&
				ExactTradeAuthority(Trade, RealmId, PublishedSettlementIds) &&
				ExactCarryAuthority(Carry, RealmId, PublishedSettlementIds);
		}

		private static bool ExactPublished(IList<string> PublishedSettlementIds,
			string PendingSettlementId, bool MustContainPending, string RealmId,
			KingdomTradeBook Trade, KingdomCarryBook Carry)
		{
			if (PublishedSettlementIds == null ||
				!KingdomIdentityRules.IsSettlementId(PendingSettlementId)) return false;
			bool contains = PublishedSettlementIds.Contains(PendingSettlementId);
			return contains == MustContainPending &&
				ExactTopology(PublishedSettlementIds, RealmId, Trade, Carry);
		}

		private static bool TryTarget(string RealmId, IEnumerable<string> Current,
			string NewSettlementId, out List<string> Target, out string Failure)
		{
			Target = new List<string>();
			Failure = null;
			if (Current == null)
			{
				Failure = "Current city topology is absent.";
				return false;
			}
			try
			{
				foreach (string id in Current)
				{
					if (Target.Count >= KingdomIdentityRules.MaxSettlements ||
						!KingdomIdentityRules.IsSettlementId(id) || Target.Contains(id))
					{
						Failure = "Current city topology is malformed, duplicate, or over cap.";
						return false;
					}
					Target.Add(id);
				}
			}
			catch
			{
				Failure = "Current city topology could not be enumerated exactly.";
				return false;
			}
			if (!Target.Contains(NewSettlementId)) Target.Add(NewSettlementId);
			Target.Sort(StringComparer.Ordinal);
			KingdomIdentityFault fault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, Target, out fault))
			{
				Failure = "Second-city target topology is invalid (" + fault + ").";
				return false;
			}
			return true;
		}

		private static bool ExactTradeAuthority(KingdomTradeBook Book, string RealmId,
			IList<string> Expected)
		{
			return KingdomTradeRules.BookUsable(Book) &&
				string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal) &&
				(Expected == null || ExactStrings(Book.SettlementIds, Expected));
		}

		private static bool ExactCarryAuthority(KingdomCarryBook Book, string RealmId,
			IList<string> Expected)
		{
			return KingdomLifecycleRules.CanOwnAuthority(Book) &&
				string.Equals(Book.RealmId, RealmId, StringComparison.Ordinal) &&
				(Expected == null || ExactStrings(Book.SettlementIds, Expected));
		}

		private static bool ExactStrings(IList<string> Left, IList<string> Right)
		{
			if (Left == null || Right == null || Left.Count != Right.Count) return false;
			for (int i = 0; i < Left.Count; i++)
				if (!string.Equals(Left[i], Right[i], StringComparison.Ordinal)) return false;
			return true;
		}

		private static byte[] TradeBytes(KingdomTradeBook Book)
		{
			return KingdomTradeCodec.EncodeEnvelope(Book);
		}

		private static byte[] CarryBytes(KingdomCarryBook Book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
					KingdomLifecycleWireCodec.WriteCarry(writer, Book);
				return stream.ToArray();
			}
		}

		private static KingdomCarryBook CarryFromBytes(byte[] Bytes)
		{
			using (MemoryStream stream = new MemoryStream(Bytes, false))
			using (BinaryReader reader = new BinaryReader(stream, StrictUtf8, true))
			{
				KingdomCarryBook result = new KingdomCarryBook();
				KingdomLifecycleWireCodec.ReadCarry(reader, result);
				if (stream.Position != stream.Length)
					throw new InvalidDataException("Carry clone has trailing bytes.");
				return result;
			}
		}

		private static bool ExactBytes(byte[] Left, byte[] Right)
		{
			if (Left == null || Right == null || Left.Length != Right.Length) return false;
			for (int i = 0; i < Left.Length; i++) if (Left[i] != Right[i]) return false;
			return true;
		}
	}
}
