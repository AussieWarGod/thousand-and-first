using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitecture
	{
		private const int MaxPoseAuditRows = 4096;
		private const int MaxPoseAuditText = 4096;
		private const int MaxPoseAuditBytes = 1024 * 1024;

		private static readonly HashSet<string> PoseVisualRenderFields =
			new HashSet<string>(new string[]
			{
				"Tile", "RenderString", "ColorString", "DetailColor", "TileColor",
				"HFlip", "VFlip"
			}, StringComparer.Ordinal);

		private static bool TryPoseParity(string SemanticBase, string[] Siblings,
			out string Failure)
		{
			Failure = null;
			try
			{
				GameObjectBlueprint basis =
					GameObjectFactory.Factory.GetBlueprintIfExists(SemanticBase);
				if (basis == null || !TryPoseFingerprint(basis, out string expected, out Failure))
					return false;
				for (int i = 0; i < Siblings.Length; i++)
				{
					GameObjectBlueprint sibling =
						GameObjectFactory.Factory.GetBlueprintIfExists(Siblings[i]);
					if (sibling == null || (Siblings[i] != SemanticBase
						&& !sibling.InheritsFrom(SemanticBase)))
						return PoseParityFail("directional blueprint is not in the semantic family: "
							+ Siblings[i], out Failure);
					if (!TryPoseFingerprint(sibling, out string actual, out Failure)) return false;
					if (actual != expected)
						return PoseParityFail("directional blueprint changes effective nonvisual behavior: "
							+ Siblings[i], out Failure);
				}
				return true;
			}
			catch (Exception exception)
			{
				return PoseParityFail("effective blueprint audit failed: " + exception.Message,
					out Failure);
			}
		}

		private static bool TryPoseFingerprint(GameObjectBlueprint Blueprint,
			out string Fingerprint, out string Failure)
		{
			Fingerprint = null;
			Failure = null;
			if (!KnownPoseAuditSurfaces())
				return PoseParityFail("Qud blueprint public fields changed; pose audit needs review",
					out Failure);
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				int rows = 0;
				if (!WriteText(writer, Blueprint.Load, stream)
					|| !WriteParts(writer, Blueprint.Parts, true, ref rows, stream)
					|| !WriteStrings(writer, Blueprint.RemovedParts, ref rows, stream)
					|| !WriteParts(writer, Blueprint.Mutations, false, ref rows, stream)
					|| !WriteParts(writer, Blueprint.Skills, false, ref rows, stream)
					|| !WriteParts(writer, Blueprint.Builders, false, ref rows, stream)
					|| !WriteStats(writer, Blueprint.Stats, ref rows, stream)
					|| !WriteTextMap(writer, Blueprint.Props, ref rows, stream)
					|| !WriteIntMap(writer, Blueprint.IntProps, ref rows, stream)
					|| !WriteTextMap(writer, Blueprint.Tags, ref rows, stream)
					|| !WriteXTags(writer, Blueprint.xTags, ref rows, stream)
					|| !WriteInventory(writer, Blueprint.Inventory, ref rows, stream))
					return PoseParityFail("effective blueprint exceeds the bounded audit surface",
						out Failure);
				writer.Flush();
				using (SHA256 hash = SHA256.Create())
					Fingerprint = Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
			}
			return true;
		}

		private static bool WriteParts(BinaryWriter Writer,
			IDictionary<string, GamePartBlueprint> Parts, bool AllowRenderVisuals,
			ref int Rows, MemoryStream Stream)
		{
			List<string> keys = Keys(Parts);
			if (!Count(Writer, keys.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < keys.Count; i++)
			{
				GamePartBlueprint part = Parts[keys[i]];
				if (part == null || !WriteText(Writer, keys[i], Stream)
					|| !WriteText(Writer, part.Namespace, Stream)
					|| !WriteText(Writer, part.Name, Stream)) return false;
				Writer.Write(part.ChanceOneIn);
				List<KeyValuePair<string, string>> parameters =
					new List<KeyValuePair<string, string>>();
				foreach (KeyValuePair<string, string> parameter in part.GetParameterStrings())
				{
					if (AllowRenderVisuals && part.Name == "Render"
						&& PoseVisualRenderFields.Contains(parameter.Key)) continue;
					if (parameters.Count >= MaxPoseAuditRows) return false;
					parameters.Add(parameter);
				}
				parameters.Sort(delegate(KeyValuePair<string, string> a,
					KeyValuePair<string, string> b)
				{
					int order = string.CompareOrdinal(a.Key, b.Key);
					return order != 0 ? order : string.CompareOrdinal(a.Value, b.Value);
				});
				if (!Count(Writer, parameters.Count, ref Rows, Stream)) return false;
				for (int p = 0; p < parameters.Count; p++)
					if (!WriteText(Writer, parameters[p].Key, Stream)
						|| !WriteText(Writer, parameters[p].Value, Stream)) return false;
			}
			return Stream.Length <= MaxPoseAuditBytes;
		}

		private static bool WriteStats(BinaryWriter Writer, IDictionary<string, Statistic> Stats,
			ref int Rows, MemoryStream Stream)
		{
			List<string> keys = Keys(Stats);
			if (!Count(Writer, keys.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < keys.Count; i++)
			{
				Statistic stat = Stats[keys[i]];
				if (stat == null || stat.Owner != null || !WriteText(Writer, keys[i], Stream)
					|| !WriteText(Writer, stat.Name, Stream)
					|| !WriteText(Writer, stat.sValue, Stream)) return false;
				Writer.Write(stat.Boost); Writer.Write(stat._Value);
				Writer.Write(stat._Bonus); Writer.Write(stat._Penalty);
				int shifts = stat.Shifts == null ? 0 : stat.Shifts.Count;
				if (!Count(Writer, shifts, ref Rows, Stream)) return false;
				for (int s = 0; s < shifts; s++)
				{
					Statistic.StatShift shift = stat.Shifts[s];
					Writer.Write(shift.ID.ToByteArray()); Writer.Write(shift.Amount);
					if (!WriteText(Writer, shift.DisplayName, Stream)) return false;
					Writer.Write(shift.BaseValue);
				}
			}
			return Stream.Length <= MaxPoseAuditBytes;
		}

		private static bool WriteInventory(BinaryWriter Writer, IList<InventoryObject> Inventory,
			ref int Rows, MemoryStream Stream)
		{
			int count = Inventory == null ? 0 : Inventory.Count;
			if (!Count(Writer, count, ref Rows, Stream)) return false;
			for (int i = 0; i < count; i++)
			{
				InventoryObject item = Inventory[i];
				if (item == null || !WriteText(Writer, item.Blueprint, Stream)
					|| !WriteText(Writer, item.Number, Stream)
					|| !WriteText(Writer, item.CellType, Stream)
					|| !WriteText(Writer, item.AutoMod, Stream)) return false;
				Writer.Write(item.Chance); Writer.Write(item.SetMods);
				Writer.Write(item.BoostModChance); Writer.Write(item.NoEquip);
				Writer.Write(item.NoSell); Writer.Write(item.NotReal); Writer.Write(item.Full);
				Writer.Write(item.CellChance.HasValue);
				if (item.CellChance.HasValue) Writer.Write(item.CellChance.Value);
				Writer.Write(item.CellFullChance.HasValue);
				if (item.CellFullChance.HasValue) Writer.Write(item.CellFullChance.Value);
				if (!WriteTextMap(Writer, item.StringProperties, ref Rows, Stream)
					|| !WriteIntMap(Writer, item.IntProperties, ref Rows, Stream)) return false;
			}
			return Stream.Length <= MaxPoseAuditBytes;
		}

		private static bool WriteXTags(BinaryWriter Writer,
			IDictionary<string, Dictionary<string, string>> Tags,
			ref int Rows, MemoryStream Stream)
		{
			List<string> keys = Keys(Tags);
			if (!Count(Writer, keys.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < keys.Count; i++)
				if (!WriteText(Writer, keys[i], Stream)
					|| !WriteTextMap(Writer, Tags[keys[i]], ref Rows, Stream)) return false;
			return true;
		}

		private static bool WriteTextMap(BinaryWriter Writer,
			IDictionary<string, string> Values, ref int Rows, MemoryStream Stream)
		{
			List<string> keys = Keys(Values);
			if (!Count(Writer, keys.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < keys.Count; i++)
				if (!WriteText(Writer, keys[i], Stream)
					|| !WriteText(Writer, Values[keys[i]], Stream)) return false;
			return true;
		}

		private static bool WriteIntMap(BinaryWriter Writer,
			IDictionary<string, int> Values, ref int Rows, MemoryStream Stream)
		{
			List<string> keys = Keys(Values);
			if (!Count(Writer, keys.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < keys.Count; i++)
			{
				if (!WriteText(Writer, keys[i], Stream)) return false;
				Writer.Write(Values[keys[i]]);
			}
			return true;
		}

		private static bool WriteStrings(BinaryWriter Writer, IList<string> Values,
			ref int Rows, MemoryStream Stream)
		{
			List<string> sorted = Values == null ? new List<string>() : new List<string>(Values);
			sorted.Sort(StringComparer.Ordinal);
			if (!Count(Writer, sorted.Count, ref Rows, Stream)) return false;
			for (int i = 0; i < sorted.Count; i++)
				if (!WriteText(Writer, sorted[i], Stream)) return false;
			return true;
		}

		private static List<string> Keys<T>(IDictionary<string, T> Values)
		{
			List<string> keys = Values == null ? new List<string>()
				: new List<string>(Values.Keys);
			keys.Sort(StringComparer.Ordinal);
			return keys;
		}

		private static bool Count(BinaryWriter Writer, int Count,
			ref int Rows, MemoryStream Stream)
		{
			if (Count < 0 || Count > MaxPoseAuditRows - Rows) return false;
			Rows += Count; Writer.Write(Count);
			return Stream.Length <= MaxPoseAuditBytes;
		}

		private static bool WriteText(BinaryWriter Writer, string Text, MemoryStream Stream)
		{
			Writer.Write(Text != null);
			if (Text != null)
			{
				if (Text.Length > MaxPoseAuditText) return false;
				Writer.Write(Text);
			}
			return Stream.Length <= MaxPoseAuditBytes;
		}

		private static bool KnownPoseAuditSurfaces()
		{
			return ExactFields(typeof(GameObjectBlueprint), "Name,Inherits,Load,hasChildren,Parts,RemovedParts,Mutations,Skills,Builders,Stats,Props,IntProps,Tags,xTags,Inventory")
				&& ExactFields(typeof(GamePartBlueprint), "Reflector,Name,Namespace,ChanceOneIn")
				&& ExactFields(typeof(Statistic), "Owner,Name,sValue,Boost,_Value,_Bonus,_Penalty,Shifts")
				&& ExactFields(typeof(InventoryObject), "Blueprint,Number,Chance,SetMods,BoostModChance,NoEquip,NoSell,NotReal,Full,CellChance,CellFullChance,CellType,AutoMod,StringProperties,IntProperties");
		}

		private static bool ExactFields(Type Type, string Expected)
		{
			List<string> fields = new List<string>();
			foreach (FieldInfo field in Type.GetFields(BindingFlags.Instance | BindingFlags.Public))
				fields.Add(field.Name);
			fields.Sort(StringComparer.Ordinal);
			string[] expected = Expected.Split(','); Array.Sort(expected, StringComparer.Ordinal);
			return string.Join(",", fields.ToArray()) == string.Join(",", expected);
		}

		private static bool PoseParityFail(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
