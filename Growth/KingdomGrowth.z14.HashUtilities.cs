using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Conversations;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{

		private static string LiquidComposition(LiquidVolume vessel, int projectedVolume)
		{
			if (projectedVolume == 0) return "empty";
			if (vessel?.ComponentLiquids == null) return "invalid";
			List<string> keys = new List<string>(vessel.ComponentLiquids.Keys);
			keys.Sort(StringComparer.Ordinal);
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < keys.Count; i++)
			{
				if (i > 0) result.Append(';');
				result.Append(keys[i]).Append('=').Append(vessel.ComponentLiquids[keys[i]]
					.ToString(CultureInfo.InvariantCulture));
			}
			return result.Length == 0 ? "empty" : result.ToString();
		}

		private static string WaterOwnerHash(GameObject owner, int volume, string composition)
		{
			return HashText("arrival-water-owner", owner?.IDIfAssigned, owner?.Blueprint,
				owner?.GetIntProperty("KingdomStores").ToString(CultureInfo.InvariantCulture),
				owner?.CurrentZone?.ZoneID,
				owner?.CurrentCell?.X.ToString(CultureInfo.InvariantCulture),
				owner?.CurrentCell?.Y.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture), composition);
		}

		private static string WaterPartHash(GameObject owner, int volume, string composition)
		{
			LiquidVolume vessel = owner?.GetPart<LiquidVolume>();
			return HashText("arrival-water-part", owner?.IDIfAssigned,
				vessel?.MaxVolume.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture), composition);
		}

		private static string WaterTopologyHash(Zone zone, GameObject owner, int volume)
		{
			return HashText("arrival-water-topology", zone?.ZoneID, owner?.IDIfAssigned,
				owner?.CurrentCell?.X.ToString(CultureInfo.InvariantCulture),
				owner?.CurrentCell?.Y.ToString(CultureInfo.InvariantCulture),
				volume.ToString(CultureInfo.InvariantCulture));
		}

		private static string StableId(string domain, string value)
		{
			return HashText(domain, value);
		}

		private static string ReferenceHash(string domain, object authority, object reference)
		{
			string id = reference is GameObject obj ? obj.IDIfAssigned
				: reference is LiquidVolume liquid ? liquid.ParentObject?.IDIfAssigned
				: reference?.GetType().FullName;
			return HashText("arrival-reference", domain, authority?.GetType().FullName, id);
		}

		private static string HashText(params string[] values)
		{
			return Hash(delegate(BinaryWriter writer)
			{
				writer.Write(values == null ? -1 : values.Length);
				if (values != null) for (int i = 0; i < values.Length; i++)
					WriteString(writer, values[i]);
			});
		}

		private static string Hash(Action<BinaryWriter> write)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
			{
				WriteString(writer, "taf:growth-arrival-runtime:v1");
				write(writer); writer.Flush();
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(stream.ToArray());
					StringBuilder text = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
		}

		private static void WriteString(BinaryWriter writer, string value)
		{
			if (value == null) { writer.Write(-1); return; }
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			writer.Write(bytes.Length); writer.Write(bytes);
		}

		private static void WriteList(BinaryWriter writer, List<string> list, string append)
		{
			int count = (list?.Count ?? 0) + (append == null ? 0 : 1);
			writer.Write(count);
			if (list != null) for (int i = 0; i < list.Count; i++) WriteString(writer, list[i]);
			if (append != null) WriteString(writer, append);
		}

		private static void WriteDictionary(BinaryWriter writer,
			Dictionary<string, int> dictionary, string incrementKey, int increment)
		{
			Dictionary<string, int> projected = new Dictionary<string, int>(
				dictionary ?? new Dictionary<string, int>(), StringComparer.Ordinal);
			if (incrementKey != null && increment != 0)
			{
				projected.TryGetValue(incrementKey, out int before);
				projected[incrementKey] = before + increment;
			}
			List<string> keys = new List<string>(projected.Keys);
			keys.Sort(StringComparer.Ordinal); writer.Write(keys.Count);
			for (int i = 0; i < keys.Count; i++)
			{
				WriteString(writer, keys[i]); writer.Write(projected[keys[i]]);
			}
		}

		private static bool TryHashStringList(List<string> list, out string hash)
		{
			hash = null;
			if (list == null) return false;
			try
			{
				hash = Hash(delegate(BinaryWriter writer)
				{
					WriteString(writer, "arrival-outbox-list"); WriteList(writer, list, null);
				});
				return true;
			}
			catch { hash = null; return false; }
		}

		private static bool TryHashStringListAfter(List<string> list, string append,
			out string hash)
		{
			hash = null;
			if (list == null || append == null) return false;
			try
			{
				hash = Hash(delegate(BinaryWriter writer)
				{
					WriteString(writer, "arrival-outbox-list"); WriteList(writer, list, append);
				});
				return true;
			}
			catch { hash = null; return false; }
		}
	}
}
