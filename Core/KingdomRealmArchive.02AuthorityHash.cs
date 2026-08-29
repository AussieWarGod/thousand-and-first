using System;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		internal bool TryAuthorityHash(KingdomRealmCallbackReceipt ExcludedReceipt,
			KingdomRealmCallbackScope Scope, out string Hash, out string Failure)
		{
			Hash = null;
			Failure = null;
			if (Scope == KingdomRealmCallbackScope.None ||
				!Enum.IsDefined(typeof(KingdomRealmCallbackScope), Scope))
			{
				Failure = "callback authority scope is invalid";
				return false;
			}
			if (!OwnsCallbackReceipt(ExcludedReceipt))
			{
				Failure = "callback receipt is not owned by this archive";
				return false;
			}
			try
			{
				if (!KingdomArchivedSettlementCodec.TryEncode(Seat, out byte[] seatBytes,
					out Failure) ||
					!KingdomArchivedSettlementCodec.TryEncode(Seceded,
						out byte[] secededBytes, out Failure) ||
					!TryCarryBytes(CarryBook, out byte[] carryBytes, out Failure)) return false;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(0x54414131); // TAA1
					writer.Write((byte)Scope);
					WriteGraphBytes(writer, seatBytes); WriteTopologyGraph(writer,
						SettlementTopology);
					WriteGraphBytes(writer, secededBytes); WriteGraphBytes(writer, carryBytes);
					WriteGraphString(writer, RealmId); WriteGraphString(writer, FactionName);
					WriteGraphString(writer, DisplayName); WriteGraphString(writer, ExileDeed);
					writer.Write(ClosedTick); WriteGraphStrings(writer, SettlementIds);
					writer.Write(RealmIdentityVersion); writer.Write((byte)RealmIdentityOrigin);
					WriteGraphString(writer, RealmIdentityTransactionId);
					WriteGraphString(writer, RealmIdentityLegacyFaction);
					writer.Write(RealmIdentityFoundedTick); writer.Write(RealmIdentitySeedHigh);
					writer.Write(RealmIdentitySeedLow);
					WriteGraphString(writer, RealmIdentityFirstClaimedZone);
					writer.Write(SimulationSeedHigh); writer.Write(SimulationSeedLow);
					WriteGraphBindings(writer, Bindings); WriteGraphJobs(writer, Jobs);
					writer.Write(ResidentCounter); writer.Write(LastSliceTick);
					writer.Write(ReifyTick); writer.Write(ReifyThirdsSpent);
					writer.Write(ReifyHeavySpent); writer.Write(ReifyQuietUntilTick);
					writer.Write(DedicationCounter); WriteGraphDictionary(writer, Standings);
					if (CallbackAuthoritySchemaVersion >= 2)
					{
						writer.Write(CallbackAuthoritySchemaVersion);
						writer.Write(DirectionalStandingSchemaVersion);
						WriteGraphString(writer, DirectionalStandingDigest);
						WriteGraphDictionary(writer, RealmPolicyToward);
						WriteGraphDictionary(writer, RegardSpilloverRemainders);
						WriteGraphDictionary(writer, RegardSpilloverObservedReputation);
					}
					if (Scope != KingdomRealmCallbackScope.Chronicle)
					{
						WriteGraphStrings(writer, ChronicleEntries);
						WriteGraphStrings(writer, OutsiderEntries);
						WriteGraphString(writer, ChronicleRegistry);
						WriteGraphString(writer, ChronicleRegistryFault);
					}
					if (Scope != KingdomRealmCallbackScope.Feelings) writer.Write(RegardSpoken);
					writer.Write(Dissent); writer.Write(DissentSpoken); writer.Write(LastDissentTick);
					WriteGraphString(writer, DeclaredCreed); WriteGraphString(writer, DishName);
					WriteGraphString(writer, DishText); WriteGraphString(writer, DishStaple);
					WriteGraphString(writer, DishSource); writer.Write(LastRiteTick);
					writer.Write(LastSoulRiteTick); writer.Write(SecededTick);
					WriteGraphHaul(writer, Haul); writer.Write(ReturnRegard);
					WritePriorAuthorityCallbacks(writer, ExcludedReceipt);
					writer.Flush();
					if (stream.Length > KingdomArchivedSettlementCodec.MaxPayloadBytes * 6L)
						throw new InvalidDataException("Archive authority graph exceeds proof cap.");
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] digest = sha.ComputeHash(stream.ToArray());
						StringBuilder text = new StringBuilder(64);
						for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
						Hash = text.ToString();
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Failure = Bound(ex.Message, 512);
				return false;
			}
		}

		private bool OwnsCallbackReceipt(KingdomRealmCallbackReceipt Value)
		{
			return Value != null && (ReferenceEquals(Value, ExileChronicle) ||
				ReferenceEquals(Value, ExileAbility) || ReferenceEquals(Value, ReturnChronicle) ||
				ReferenceEquals(Value, ReturnReputation) || ReferenceEquals(Value, ReturnFeelings) ||
				ReferenceEquals(Value, ReturnSeat) || ReferenceEquals(Value, ReturnAbility));
		}

		private void WritePriorAuthorityCallbacks(BinaryWriter Writer,
			KingdomRealmCallbackReceipt Current)
		{
			Writer.Write((byte)0x71);
			if (ReferenceEquals(Current, ExileChronicle)) return;
			WriteAuthorityCallback(Writer, ExileChronicle);
			if (ReferenceEquals(Current, ExileAbility)) return;
			WriteAuthorityCallback(Writer, ExileAbility);
			if (ReferenceEquals(Current, ReturnChronicle)) return;
			WriteAuthorityCallback(Writer, ReturnChronicle);
			if (ReferenceEquals(Current, ReturnReputation)) return;
			WriteAuthorityCallback(Writer, ReturnReputation);
			if (ReferenceEquals(Current, ReturnFeelings)) return;
			WriteAuthorityCallback(Writer, ReturnFeelings);
			if (ReferenceEquals(Current, ReturnSeat)) return;
			WriteAuthorityCallback(Writer, ReturnSeat);
		}

		private static void WriteAuthorityCallback(BinaryWriter Writer,
			KingdomRealmCallbackReceipt Value)
		{
			Writer.Write((byte)1); Writer.Write((byte)Value.Phase);
			Writer.Write((byte)Value.Disposition); Writer.Write((byte)Value.Scope);
			WriteGraphString(Writer, Value.BeforeGraph); WriteGraphString(Writer, Value.AfterGraph);
			WriteGraphString(Writer, Value.BeforeArchiveGraph);
			WriteGraphString(Writer, Value.AfterArchiveGraph);
			WriteGraphString(Writer, Value.BeforeEffect); WriteGraphString(Writer, Value.AfterEffect);
			WriteGraphString(Writer, Value.ObservedEffect);
			Writer.Write(Value.BeforeStamp); Writer.Write(Value.AfterStamp);
		}

	}
}
