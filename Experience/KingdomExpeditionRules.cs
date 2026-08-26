using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The one frozen answer a salvage commission may return.</summary>
	internal enum KingdomExpeditionOutcome : byte
	{
		None = 0,
		PickedClean = 1,
		ModestFind = 2,
		RichFind = 3,
		Cancelled = 4,
		ResidentDiedOnGround = 5,
		ResidentMissingFromBoundGround = 6,
		ResidentJoinedFounder = 7
	}

	/// <summary>Everything the confirmation prompt promises, in exact physical units and world
	/// ticks. It is copied into the realm job row at dispatch, so a reload never requotes it.</summary>
	internal readonly struct KingdomExpeditionQuote
	{
		internal readonly int DistanceCells;
		internal readonly int DurationDays;
		internal readonly long DueTick;
		internal readonly int WaterDrams;
		internal readonly int Provisions;

		internal KingdomExpeditionQuote(int distanceCells, int durationDays, long dueTick,
			int waterDrams, int provisions)
		{
			DistanceCells = distanceCells;
			DurationDays = durationDays;
			DueTick = dueTick;
			WaterDrams = waterDrams;
			Provisions = provisions;
		}
	}

	/// <summary>One receipt-bound dedicated water vessel.</summary>
	internal readonly struct KingdomExpeditionWaterLeg
	{
		internal readonly string OwnerId;
		internal readonly int BeforeVolume;
		internal readonly int AfterVolume;
		internal readonly int MaxVolume;

		internal KingdomExpeditionWaterLeg(string ownerId, int beforeVolume, int afterVolume,
			int maxVolume)
		{
			OwnerId = ownerId;
			BeforeVolume = beforeVolume;
			AfterVolume = afterVolume;
			MaxVolume = maxVolume;
		}
	}

	/// <summary>One receipt-bound food stack. A debit may stop at any count between before and
	/// after; retry resumes from that observed count instead of spending the quoted amount twice.</summary>
	internal readonly struct KingdomExpeditionProvisionLeg
	{
		internal readonly string LarderId;
		internal readonly string ItemId;
		internal readonly int BeforeCount;
		internal readonly int AfterCount;

		internal KingdomExpeditionProvisionLeg(string larderId, string itemId, int beforeCount,
			int afterCount)
		{
			LarderId = larderId;
			ItemId = itemId;
			BeforeCount = beforeCount;
			AfterCount = afterCount;
		}
	}

	/// <summary>
	/// Bounded, engine-free receipt persisted on the exact commissioned body. The realm job is
	/// published first as prepared authority; this receipt is then attached before the first drain
	/// or food callback. Every leg records an exact object identity and before/after CAS range.
	/// </summary>
	internal sealed class KingdomExpeditionDebitReceipt
	{
		private const int Magic = 0x54455231; // TER1
		private const int Version = 1;
		internal const int MaxEncodedChars = 16384;
		internal const int MaxIdentityChars = 256;
		internal const int MaxZoneChars = 512;
		internal const int MaxWaterLegs = KingdomExpeditionRules.MaxDurationDays
			* KingdomExpeditionRules.WaterPerDay;
		internal const int MaxProvisionLegs = KingdomExpeditionRules.MaxDurationDays
			* KingdomExpeditionRules.ProvisionsPerDay;

		internal readonly int JobId;
		internal readonly string SourceZoneId;
		internal readonly int WaterCost;
		internal readonly int ProvisionCost;
		private readonly KingdomExpeditionWaterLeg[] water;
		private readonly KingdomExpeditionProvisionLeg[] provisions;

		private KingdomExpeditionDebitReceipt(int jobId, string sourceZoneId, int waterCost,
			int provisionCost, KingdomExpeditionWaterLeg[] water,
			KingdomExpeditionProvisionLeg[] provisions)
		{
			JobId = jobId;
			SourceZoneId = sourceZoneId;
			WaterCost = waterCost;
			ProvisionCost = provisionCost;
			this.water = water;
			this.provisions = provisions;
		}

		internal int WaterLegCount => water.Length;

		internal int ProvisionLegCount => provisions.Length;

		internal bool TryWaterLeg(int index, out KingdomExpeditionWaterLeg leg)
		{
			leg = default(KingdomExpeditionWaterLeg);
			if (index < 0 || index >= water.Length) return false;
			leg = water[index];
			return true;
		}

		internal bool TryProvisionLeg(int index, out KingdomExpeditionProvisionLeg leg)
		{
			leg = default(KingdomExpeditionProvisionLeg);
			if (index < 0 || index >= provisions.Length) return false;
			leg = provisions[index];
			return true;
		}

		internal static bool TryCreate(int jobId, string sourceZoneId, int waterCost,
			int provisionCost, KingdomExpeditionWaterLeg[] water,
			KingdomExpeditionProvisionLeg[] provisions,
			out KingdomExpeditionDebitReceipt receipt)
		{
			receipt = null;
			KingdomExpeditionWaterLeg[] waterCopy = (water == null)
				? null : (KingdomExpeditionWaterLeg[])water.Clone();
			KingdomExpeditionProvisionLeg[] provisionCopy = (provisions == null)
				? null : (KingdomExpeditionProvisionLeg[])provisions.Clone();
			if (!Valid(jobId, sourceZoneId, waterCost, provisionCost, waterCopy, provisionCopy))
				return false;
			receipt = new KingdomExpeditionDebitReceipt(jobId, sourceZoneId, waterCost,
				provisionCost, waterCopy, provisionCopy);
			return true;
		}

		internal bool TryEncode(out string encoded)
		{
			encoded = null;
			if (!Valid(JobId, SourceZoneId, WaterCost, ProvisionCost, water, provisions))
				return false;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
				{
					writer.Write(Magic);
					writer.Write(Version);
					writer.Write(JobId);
					WriteText(writer, SourceZoneId, MaxZoneChars);
					writer.Write(WaterCost);
					writer.Write(ProvisionCost);
					writer.Write(water.Length);
					for (int i = 0; i < water.Length; i++)
					{
						WriteText(writer, water[i].OwnerId, MaxIdentityChars);
						writer.Write(water[i].BeforeVolume);
						writer.Write(water[i].AfterVolume);
						writer.Write(water[i].MaxVolume);
					}
					writer.Write(provisions.Length);
					for (int i = 0; i < provisions.Length; i++)
					{
						WriteText(writer, provisions[i].LarderId, MaxIdentityChars);
						WriteText(writer, provisions[i].ItemId, MaxIdentityChars);
						writer.Write(provisions[i].BeforeCount);
						writer.Write(provisions[i].AfterCount);
					}
					writer.Flush();
					encoded = Convert.ToBase64String(stream.ToArray());
				}
				if (encoded.Length > MaxEncodedChars)
				{
					encoded = null;
					return false;
				}
				return true;
			}
			catch
			{
				encoded = null;
				return false;
			}
		}

		internal static bool TryDecode(string encoded, out KingdomExpeditionDebitReceipt receipt)
		{
			receipt = null;
			if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedChars) return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(encoded);
				using (MemoryStream stream = new MemoryStream(bytes, false))
				using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
				{
					if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;
					int jobId = reader.ReadInt32();
					string sourceZoneId = ReadText(reader, MaxZoneChars);
					int waterCost = reader.ReadInt32();
					int provisionCost = reader.ReadInt32();
					int waterCount = reader.ReadInt32();
					if (waterCount < 0 || waterCount > MaxWaterLegs) return false;
					KingdomExpeditionWaterLeg[] water = new KingdomExpeditionWaterLeg[waterCount];
					for (int i = 0; i < waterCount; i++)
						water[i] = new KingdomExpeditionWaterLeg(
							ReadText(reader, MaxIdentityChars), reader.ReadInt32(),
							reader.ReadInt32(), reader.ReadInt32());
					int provisionCount = reader.ReadInt32();
					if (provisionCount < 0 || provisionCount > MaxProvisionLegs) return false;
					KingdomExpeditionProvisionLeg[] provisions =
						new KingdomExpeditionProvisionLeg[provisionCount];
					for (int i = 0; i < provisionCount; i++)
						provisions[i] = new KingdomExpeditionProvisionLeg(
							ReadText(reader, MaxIdentityChars), ReadText(reader, MaxIdentityChars),
							reader.ReadInt32(), reader.ReadInt32());
					if (stream.Position != stream.Length
						|| !TryCreate(jobId, sourceZoneId, waterCost, provisionCost,
							water, provisions, out receipt)) return false;
					return true;
				}
			}
			catch
			{
				receipt = null;
				return false;
			}
		}

		private static bool Valid(int jobId, string sourceZoneId, int waterCost,
			int provisionCost, KingdomExpeditionWaterLeg[] water,
			KingdomExpeditionProvisionLeg[] provisions)
		{
			if (jobId <= 0 || !Bounded(sourceZoneId, MaxZoneChars) || water == null
				|| provisions == null || waterCost <= 0 || provisionCost <= 0
				|| waterCost > MaxWaterLegs || provisionCost > MaxProvisionLegs
				|| water.Length <= 0 || water.Length > MaxWaterLegs
				|| provisions.Length <= 0 || provisions.Length > MaxProvisionLegs) return false;
			int waterSum = 0;
			HashSet<string> waterIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < water.Length; i++)
			{
				KingdomExpeditionWaterLeg leg = water[i];
				if (!Bounded(leg.OwnerId, MaxIdentityChars) || !waterIds.Add(leg.OwnerId)
					|| leg.BeforeVolume <= 0 || leg.AfterVolume < 0
					|| leg.AfterVolume >= leg.BeforeVolume || leg.MaxVolume < leg.BeforeVolume)
					return false;
				waterSum += leg.BeforeVolume - leg.AfterVolume;
			}
			int provisionSum = 0;
			HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < provisions.Length; i++)
			{
				KingdomExpeditionProvisionLeg leg = provisions[i];
				if (!Bounded(leg.LarderId, MaxIdentityChars)
					|| !Bounded(leg.ItemId, MaxIdentityChars) || !itemIds.Add(leg.ItemId)
					|| leg.BeforeCount <= 0 || leg.AfterCount < 0
					|| leg.AfterCount >= leg.BeforeCount) return false;
				provisionSum += leg.BeforeCount - leg.AfterCount;
			}
			return waterSum == waterCost && provisionSum == provisionCost;
		}

		private static bool Bounded(string value, int max)
		{
			return !string.IsNullOrEmpty(value) && value.Length <= max;
		}

		private static void WriteText(BinaryWriter writer, string value, int maxChars)
		{
			if (!Bounded(value, maxChars)) throw new InvalidDataException("Receipt text out of bounds.");
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			writer.Write(bytes.Length);
			writer.Write(bytes);
		}

		private static string ReadText(BinaryReader reader, int maxChars)
		{
			int byteCount = reader.ReadInt32();
			int maxBytes = maxChars * 4;
			if (byteCount <= 0 || byteCount > maxBytes) throw new InvalidDataException(
				"Receipt text out of bounds.");
			byte[] bytes = reader.ReadBytes(byteCount);
			if (bytes.Length != byteCount) throw new EndOfStreamException();
			string value = Encoding.UTF8.GetString(bytes);
			if (!Bounded(value, maxChars)) throw new InvalidDataException("Receipt text out of bounds.");
			return value;
		}
	}

	/// <summary>
	/// Pure commission law. A destination must be a real zone in the same world; the engine edge
	/// separately proves that its journal note was personally visited. Travel is bounded, quoted
	/// once, and outcome draws are counter-addressed by the durable realm job id.
	/// </summary>
	internal static class KingdomExpeditionRules
	{
		internal const int MinDurationDays = 3;
		internal const int MaxDurationDays = 30;
		internal const int WaterPerDay = 3;
		internal const int ProvisionsPerDay = 1;
		internal const int MaxSkillBonus = 20;

		internal const string StreamId = "taf:stream:salvage-expedition";
		internal const uint KindCode = 1u;
		internal const uint OutcomeDrawIndex = 0u;
		internal const uint YieldDrawIndex = 1u;

		/// <summary>Quotes an out-search-back journey. Surface distance costs one day per three
		/// cells (rounded up), each stratum costs another day, and searching costs one day. The
		/// whole journey is clamped to the explicit three-to-thirty-day gameplay bound.</summary>
		internal static bool TryQuote(string sourceZoneId, string targetZoneId, long startTick,
			out KingdomExpeditionQuote quote)
		{
			quote = default(KingdomExpeditionQuote);
			if (startTick < 0L || string.IsNullOrEmpty(sourceZoneId)
				|| string.IsNullOrEmpty(targetZoneId)
				|| string.Equals(sourceZoneId, targetZoneId, StringComparison.Ordinal)) return false;
			string sourceWorld;
			int sx, sy, sz;
			string targetWorld;
			int tx, ty, tz;
			if (!KingdomRules.TryParseZoneID(sourceZoneId, out sourceWorld, out sx, out sy, out sz)
				|| !KingdomRules.TryParseZoneID(targetZoneId, out targetWorld, out tx, out ty, out tz)
				|| !string.Equals(sourceWorld, targetWorld, StringComparison.Ordinal)) return false;
			long dx = Math.Abs((long)tx - sx);
			long dy = Math.Abs((long)ty - sy);
			long dz = Math.Abs((long)tz - sz);
			long surface = (dx > dy) ? dx : dy;
			long distance = surface + dz;
			if (distance <= 0L || distance > int.MaxValue) return false;
			long oneWayDays = 1L + ((surface + 2L) / 3L) + dz;
			long days = oneWayDays * 2L + 1L;
			if (days < MinDurationDays) days = MinDurationDays;
			if (days > MaxDurationDays) days = MaxDurationDays;
			long durationTicks = days * KingdomRules.TicksPerDay;
			if (durationTicks < 0L || startTick > long.MaxValue - durationTicks) return false;
			quote = new KingdomExpeditionQuote((int)distance, (int)days,
				startTick + durationTicks, (int)days * WaterPerDay,
				(int)days * ProvisionsPerDay);
			return true;
		}

		/// <summary>Freezes one outcome and scrap yield. Skill can improve the band but can never
		/// add a draw or exceed the named twenty-point cap. A picked-clean site returns the resident
		/// safely with no cargo; bodily death is never a random result here.</summary>
		internal static bool TryDrawOutcome(KernelSeed128 seed, string settlementId, int jobId,
			int skillBonus, out KingdomExpeditionOutcome outcome, out int scrap)
		{
			outcome = KingdomExpeditionOutcome.None;
			scrap = 0;
			if (jobId <= 0 || string.IsNullOrEmpty(settlementId)) return false;
			if (skillBonus < 0) skillBonus = 0;
			if (skillBonus > MaxSkillBonus) skillBonus = MaxSkillBonus;
			SemanticEventKey key;
			KernelFaultCode fault;
			if (!SemanticEventKey.TryCreate(KingdomCityRules.RulesVersion, settlementId,
				StreamId, KindCode, (ulong)jobId, out key, out fault)) return false;
			ulong band;
			ulong yield;
			if (!CounterRandom.TryDrawBelow(seed, key, OutcomeDrawIndex, 100uL, out band, out fault)
				|| !CounterRandom.TryDrawBelow(seed, key, YieldDrawIndex, 2uL, out yield, out fault))
				return false;
			int score = (int)band + skillBonus;
			if (score < 20)
			{
				outcome = KingdomExpeditionOutcome.PickedClean;
				scrap = 0;
			}
			else if (score < 75)
			{
				outcome = KingdomExpeditionOutcome.ModestFind;
				scrap = 1 + (int)yield;
			}
			else
			{
				outcome = KingdomExpeditionOutcome.RichFind;
				scrap = 3 + (int)yield;
			}
			return true;
		}

		internal static bool IsFrozenOutcome(int code)
		{
			return code >= (int)KingdomExpeditionOutcome.PickedClean
				&& code <= (int)KingdomExpeditionOutcome.RichFind;
		}

		internal static bool Due(long nowTick, long dueTick)
		{
			return nowTick >= 0L && dueTick > 0L && nowTick >= dueTick;
		}

		internal static bool IsPhase(int stored)
		{
			return KingdomJobRules.IsExpeditionPhase(stored);
		}

		internal static bool IsPaid(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.Paid
				|| stored == (int)KingdomExpeditionPhase.Dispatched;
		}

		internal static bool IsDispatched(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.Dispatched;
		}

		internal static bool IsResolutionPrepared(int stored)
		{
			return stored == (int)KingdomExpeditionPhase.ResolutionPrepared;
		}

		internal static bool IsTerminalOutcome(int stored)
		{
			return stored >= (int)KingdomExpeditionOutcome.ResidentDiedOnGround
				&& stored <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder;
		}

		/// <summary>Classifies a receipt-bound physical state after any injected cut. A value still
		/// between before and after is safe forward progress; outside that closed range is a conflict
		/// and must not be charged again. Absence is exact only for a zero target.</summary>
		internal static bool TryDebitProgress(int before, int after, bool present, int current,
			out int remaining)
		{
			remaining = 0;
			if (before <= 0 || after < 0 || after >= before) return false;
			if (!present) return after == 0;
			if (current < after || current > before) return false;
			remaining = current - after;
			return true;
		}
	}
}
