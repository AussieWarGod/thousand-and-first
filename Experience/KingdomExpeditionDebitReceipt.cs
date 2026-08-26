using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
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
}
