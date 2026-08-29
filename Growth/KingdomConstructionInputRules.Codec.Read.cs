using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomConstructionInputRules
	{
		private static bool TryReadPayload(byte[] payload,
			out KingdomConstructionInputReceipt receipt, out KingdomConstructionInputFault fault)
		{
			receipt = null;
			try
			{
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader r = new BinaryReader(stream, StrictUtf8))
				{
					if (r.ReadByte() != 'T' || r.ReadByte() != 'A' || r.ReadByte() != 'F'
						|| r.ReadByte() != 'C' || r.ReadByte() != 'R' || r.ReadByte() != 1)
						return Refuse(KingdomConstructionInputFault.Codec, out fault);
					int schema = r.ReadInt32();
					if (schema > Schema) return Refuse(KingdomConstructionInputFault.FutureSchema, out fault);
					if (schema < LegacySchema) return Refuse(KingdomConstructionInputFault.Schema, out fault);
					string receiptId = ReadText(r, MaxIdentityChars, false);
					string jobId = ReadText(r, MaxIdentityChars, false);
					string owner = ReadText(r, MaxIdentityChars, false); long epoch = r.ReadInt64();
					string target = ReadText(r, MaxIdentityChars, false);
					int targetX = r.ReadInt32(), targetY = r.ReadInt32();
					string intent = ReadText(r, 64, false);
					string[] required;
					if (schema == LegacySchema)
					{
						string legacyRequired = ReadText(r, MaxIdentityChars, true);
						required = string.IsNullOrEmpty(legacyRequired)
							? new string[0] : new[] { legacyRequired };
					}
					else
					{
						int requiredCount = r.ReadByte();
						if (requiredCount > MaxRequiredObjects)
							return Refuse(KingdomConstructionInputFault.Bounds, out fault);
						required = new string[requiredCount];
						for (int i = 0; i < required.Length; i++)
							required[i] = ReadText(r, MaxIdentityChars, false);
					}
					int water = r.ReadInt32(); string material = ReadText(r, MaxClaimChars, false);
					int waterFloor = r.ReadInt32(), policy = r.ReadInt32();
					int priorSpent = r.ReadInt32(), priorLost = r.ReadInt32();
					string priorMaterialSpent = ReadText(r, MaxClaimChars, false);
					string priorMaterialLost = ReadText(r, MaxClaimChars, false);
					KingdomConstructionInputTxPhase phase = (KingdomConstructionInputTxPhase)r.ReadByte();
					int revision = r.ReadInt32(); string plan = ReadText(r, 64, false);
					long pauseStart = r.ReadInt64(), pausedTicks = r.ReadInt64();
					int sourceCount = r.ReadByte();
					if (sourceCount < 1 || sourceCount > MaxSourceLines)
						return Refuse(KingdomConstructionInputFault.Bounds, out fault);
					KingdomConstructionInputSourceLine[] sources = new KingdomConstructionInputSourceLine[sourceCount];
					for (int i = 0; i < sources.Length; i++) sources[i] = ReadSource(r);
					int cargoCount = r.ReadByte();
					if (cargoCount < 1 || cargoCount > MaxCargoLines)
						return Refuse(KingdomConstructionInputFault.Bounds, out fault);
					KingdomConstructionInputCargoLine[] cargo = new KingdomConstructionInputCargoLine[cargoCount];
					for (int i = 0; i < cargo.Length; i++) cargo[i] = ReadCargo(r);
					int childCount = r.ReadByte();
					if (childCount < 1 || childCount > MaxChildren)
						return Refuse(KingdomConstructionInputFault.Bounds, out fault);
					KingdomConstructionInputChild[] children = new KingdomConstructionInputChild[childCount];
					for (int i = 0; i < children.Length; i++) children[i] = ReadChild(r);
					if (stream.Position != stream.Length)
						return Refuse(KingdomConstructionInputFault.Codec, out fault);
					receipt = new KingdomConstructionInputReceipt(schema, receiptId, jobId, owner,
						epoch, target, targetX, targetY, intent, required, water, material,
						waterFloor, policy, priorSpent, priorLost, priorMaterialSpent,
						priorMaterialLost, phase, revision, plan, pauseStart, pausedTicks,
						sources, cargo, children);
				}
				if (!TryValidate(receipt, out fault)) { receipt = null; return false; }
				return true;
			}
			catch
			{
				receipt = null;
				return Refuse(KingdomConstructionInputFault.Codec, out fault);
			}
		}

		private static KingdomConstructionInputSourceLine ReadSource(BinaryReader r)
		{
			int ordinal = r.ReadInt32(); string line = ReadText(r, MaxIdentityChars, false);
			KingdomConstructionInputKind kind = (KingdomConstructionInputKind)r.ReadByte();
			string classification = ReadText(r, MaxClaimChars, false);
			string settlement = ReadText(r, MaxIdentityChars, false);
			string zone = ReadText(r, MaxIdentityChars, false);
			string holder = ReadText(r, MaxIdentityChars, false);
			string objectId = ReadText(r, MaxIdentityChars, false);
			KingdomConstructionInputTopology topology = (KingdomConstructionInputTopology)r.ReadByte();
			int x = r.ReadInt32(), y = r.ReadInt32(); string blueprint = ReadText(r, MaxBlueprintChars, false);
			int before = r.ReadInt32(), take = r.ReadInt32(), residual = r.ReadInt32();
			int stock = r.ReadInt32(), prior = r.ReadInt32(), floor = r.ReadInt32();
			int cargo = r.ReadInt32(), cost = r.ReadInt32(), dedication = r.ReadInt32();
			string marker = ReadText(r, MaxIdentityChars, true);
			KingdomConstructionInputSourcePhase phase = (KingdomConstructionInputSourcePhase)r.ReadByte();
			string remainder = ReadText(r, MaxIdentityChars, true);
			string beforeHash = ReadText(r, 64, true), afterHash = ReadText(r, 64, true);
			int lost = r.ReadInt32();
			return new KingdomConstructionInputSourceLine(ordinal, line, kind, classification,
				settlement, zone, holder, objectId, topology, x, y, blueprint, before, take,
				residual, stock, prior, floor, cargo, cost, dedication, marker, phase,
				remainder, beforeHash, afterHash, lost);
		}

		private static KingdomConstructionInputCargoLine ReadCargo(BinaryReader r)
		{
			int ordinal = r.ReadInt32(); string key = ReadText(r, MaxIdentityChars, false);
			string marker = ReadText(r, MaxIdentityChars, false);
			KingdomConstructionInputKind kind = (KingdomConstructionInputKind)r.ReadByte();
			string classification = ReadText(r, MaxClaimChars, false); int amount = r.ReadInt32();
			string blueprint = ReadText(r, MaxBlueprintChars, false); int capacity = r.ReadInt32();
			int source = r.ReadInt32(); string expected = ReadText(r, MaxIdentityChars, true);
			int job = r.ReadInt32(), trip = r.ReadInt32(); string objectId = ReadText(r, MaxIdentityChars, true);
			KingdomConstructionInputCargoPhase phase = (KingdomConstructionInputCargoPhase)r.ReadByte();
			KingdomConstructionInputTopology topology = (KingdomConstructionInputTopology)r.ReadByte();
			string owner = ReadText(r, MaxIdentityChars, true), zone = ReadText(r, MaxIdentityChars, true);
			int x = r.ReadInt32(), y = r.ReadInt32(); string before = ReadText(r, 64, true);
			string after = ReadText(r, 64, true); int spent = r.ReadInt32(), lost = r.ReadInt32();
			return new KingdomConstructionInputCargoLine(ordinal, key, marker, kind,
				classification, amount, blueprint, capacity, source, expected, job, trip,
				objectId, phase, topology, owner, zone, x, y, before, after, spent, lost);
		}

		private static KingdomConstructionInputChild ReadChild(BinaryReader r)
		{
			return new KingdomConstructionInputChild(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(),
				r.ReadInt32(), r.ReadInt32(), (KingdomConstructionInputCargoShape)r.ReadByte(),
				r.ReadInt32(), ReadText(r, MaxIdentityChars, true), ReadText(r, MaxIdentityChars, false),
				r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), ReadText(r, MaxIdentityChars, true),
				ReadText(r, MaxIdentityChars, false), r.ReadInt32(), r.ReadInt32(), r.ReadInt64(),
				ReadText(r, 64, false), r.ReadInt32(), r.ReadInt64());
		}
	}
}
