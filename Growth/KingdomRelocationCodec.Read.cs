using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocationCodec
	{
		private static bool Read(BinaryReader R, out KingdomRelocationReceipt Receipt,
			out string Failure)
		{
			Receipt = null; Failure = null;
			if (R.ReadInt32() != Magic) return Fail("relocation receipt magic is unknown", out Failure);
			int schema = R.ReadInt32();
			if (schema != KingdomRelocationRules.Schema)
				return Fail("relocation receipt schema " + schema + " is unknown", out Failure);
			KingdomRelocationReceipt result = new KingdomRelocationReceipt
			{
				Schema = schema,
				PlanId = ReadText(R, KingdomRelocationRules.MaxIdChars),
				ZoneId = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				RealmId = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				HeartId = ReadText(R, KingdomRelocationRules.MaxIdChars),
				SuccessorKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				HeartGround = ReadRect(R), CreatedTick = R.ReadInt64(),
				Generation = R.ReadInt32(), CurrentMove = R.ReadInt32(),
				Held = R.ReadBoolean(), ObstructionAnnounced = R.ReadBoolean(),
				Phase = (KingdomRelocationPhase)R.ReadByte(),
				Failure = ReadOptional(R, KingdomRelocationRules.MaxFailureChars)
			};
			int count = Count(R, KingdomRelocationRules.MaxMoves, false);
			result.Moves = new List<KingdomRelocationMove>(count);
			for (int i = 0; i < count; i++) result.Moves.Add(ReadMove(R));
			Receipt = result; return true;
		}

		private static KingdomRelocationMove ReadMove(BinaryReader R)
		{
			KingdomRelocationMove move = new KingdomRelocationMove
			{
				RootId = ReadText(R, KingdomRelocationRules.MaxIdChars),
				PlotId = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				BuildKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				DisplayName = ReadOptional(R, KingdomRelocationRules.MaxNameChars),
				Source = ReadRect(R), Destination = ReadRect(R), Footprint = ReadRect(R),
				Roof = R.ReadInt32(), StartedTick = R.ReadInt64(), LastTick = R.ReadInt64(),
				RequiredTicks = R.ReadInt64(), RemainingTicks = R.ReadInt64(),
				CompletionTick = R.ReadInt64(), Phase = (KingdomRelocationMovePhase)R.ReadByte(),
				FrameId = ReadText(R, KingdomRelocationRules.MaxIdChars)
			};
			int stakes = Count(R, KingdomRelocationRules.MaxStakeIds, true);
			move.StakeIds = new string[stakes];
			for (int i = 0; i < stakes; i++)
				move.StakeIds[i] = ReadText(R, KingdomRelocationRules.MaxIdChars);
			if (R.ReadBoolean()) move.Architecture = ReadArchitecture(R);
			int rows = Count(R, KingdomRelocationRules.MaxRowsPerMove, false);
			move.Rows = new List<KingdomRelocationRow>(rows);
			for (int i = 0; i < rows; i++) move.Rows.Add(ReadRow(R));
			int clear = Count(R, KingdomRelocationRules.MaxClearRowsPerMove, true);
			move.Clearance = new List<KingdomRelocationClearRow>(clear);
			for (int i = 0; i < clear; i++) move.Clearance.Add(ReadClear(R));
			return move;
		}

		private static KingdomRelocationArchitecture ReadArchitecture(BinaryReader R)
		{
			return new KingdomRelocationArchitecture
			{
				Schema = R.ReadInt32(), BuildKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				PlanKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				BindingKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				TierKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				VariantKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				PaletteKey = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				LotType = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				LotSize = R.ReadInt32(), Facing = R.ReadInt32(),
				Snapshot = ReadText(R, KingdomRelocationRules.MaxSnapshotChars),
				Hash = ReadText(R, 64), MainX = R.ReadInt32(), MainY = R.ReadInt32()
			};
		}

		private static KingdomRelocationRow ReadRow(BinaryReader R)
		{
			return new KingdomRelocationRow
			{
				ObjectId = ReadText(R, KingdomRelocationRules.MaxIdChars),
				Blueprint = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				OffsetX = R.ReadInt32(), OffsetY = R.ReadInt32(), Root = R.ReadBoolean(),
				State = (KingdomRelocationRowState)R.ReadByte()
			};
		}

		private static KingdomRelocationClearRow ReadClear(BinaryReader R)
		{
			return new KingdomRelocationClearRow
			{
				ObjectId = ReadText(R, KingdomRelocationRules.MaxIdChars),
				Blueprint = ReadText(R, KingdomRelocationRules.MaxKeyChars),
				X = R.ReadInt32(), Y = R.ReadInt32(),
				State = (KingdomRelocationClearState)R.ReadByte()
			};
		}

		private static int Count(BinaryReader R, int Maximum, bool ZeroAllowed)
		{
			int count = R.ReadInt32();
			if (count < (ZeroAllowed ? 0 : 1) || count > Maximum)
				throw new InvalidDataException("collection count is outside its bound");
			return count;
		}
	}
}
