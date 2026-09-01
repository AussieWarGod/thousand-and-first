using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		// --- Canonical snapshot codec -------------------------------------------------------

		public static bool TryEncodeSnapshot(ArchitectureLayoutSnapshot Snapshot,
			out string Encoded, out string Failure)
		{
			return TryEncodeSnapshotVersion(Snapshot, SnapshotSchema, out Encoded, out Failure);
		}

		private static bool TryEncodeSnapshotVersion(ArchitectureLayoutSnapshot Snapshot,
			int Schema, out string Encoded, out string Failure)
		{
			Encoded = null;
			Failure = null;
			bool legacy = Schema == LegacySnapshotSchema;
			bool supported = Schema >= LegacySnapshotSchema && Schema <= SnapshotSchema;
			if (!supported
				|| !TryValidateTopologyCore(Snapshot, null, legacy, out Failure)) return false;
			if (legacy && !LegacyPlacementTruthOnly(Snapshot))
				return Fail("legacy snapshot placement truth is not empty", out Failure);
			if (Schema < TransitionSnapshotSchema
				&& Snapshot.IncomingTransitionMode != ArchitectureTransitionMode.None)
				return Fail("legacy snapshot cannot carry transition mode", out Failure);
			if (Schema < SnapshotSchema && !LegacyClaimTruthOnly(Snapshot))
				return Fail("legacy snapshot cannot distinguish building and yard claims", out Failure);
			if (Schema == SnapshotSchema && !TryValidateCurrentFootprint(Snapshot, out Failure))
				return false;
			List<ArchitectureCellState> cells = new List<ArchitectureCellState>(Snapshot.Cells);
			List<ArchitectureAnchor> anchors = new List<ArchitectureAnchor>(Snapshot.Anchors);
			List<ArchitecturePlacement> placements = new List<ArchitecturePlacement>(Snapshot.Placements);
			cells.Sort(CompareCells);
			anchors.Sort(delegate(ArchitectureAnchor A, ArchitectureAnchor B)
			{
				return string.CompareOrdinal(A.Key, B.Key);
			});
			placements.Sort(ComparePlacements);
			List<string> blueprints = BlueprintTable(placements);
			Dictionary<string, byte> blueprintIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			for (int i = 0; i < blueprints.Count; i++) blueprintIndexes[blueprints[i]] = (byte)i;
			List<string> materials = legacy ? new List<string>() : PlacementTextTable(placements, 0);
			List<string> techs = legacy ? new List<string>() : PlacementTextTable(placements, 1);
			List<string> knowledge = legacy ? new List<string>() : PlacementTextTable(placements, 2);
			List<string> powers = legacy ? new List<string>() : PlacementTextTable(placements, 3);
			Dictionary<string, byte> materialIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> techIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> knowledgeIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			Dictionary<string, byte> powerIndexes = new Dictionary<string, byte>(StringComparer.Ordinal);
			for (int i = 0; i < materials.Count; i++) materialIndexes[materials[i]] = (byte)i;
			for (int i = 0; i < techs.Count; i++) techIndexes[techs[i]] = (byte)i;
			for (int i = 0; i < knowledge.Count; i++) knowledgeIndexes[knowledge[i]] = (byte)i;
			for (int i = 0; i < powers.Count; i++) powerIndexes[powers[i]] = (byte)i;
			Dictionary<string, ushort> anchorIndexes = new Dictionary<string, ushort>(StringComparer.Ordinal);
			for (int i = 0; i < anchors.Count; i++) anchorIndexes[anchors[i].Key] = (ushort)i;
			byte[] payload;
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8))
				{
					writer.Write((byte)'T');
					writer.Write((byte)'A');
					writer.Write((byte)'F');
					writer.Write((byte)Schema);
					WriteText(writer, Snapshot.PlanKey, MaxKeyChars);
					WriteText(writer, Snapshot.BindingKey, MaxKeyChars);
					WriteText(writer, Snapshot.BuildKey, MaxKeyChars);
					WriteText(writer, Snapshot.TierKey, MaxKeyChars);
					WriteText(writer, Snapshot.VariantKey, MaxKeyChars);
					WriteText(writer, Snapshot.PaletteKey, MaxKeyChars);
					WriteText(writer, Snapshot.LotType, MaxKeyChars);
					writer.Write((byte)Snapshot.LotSize);
					writer.Write((byte)Snapshot.Facing);
					writer.Write((byte)Snapshot.Width);
					writer.Write((byte)Snapshot.Height);
					writer.Write((byte)Snapshot.MainX);
					writer.Write((byte)Snapshot.MainY);
					if (Schema >= TransitionSnapshotSchema)
						writer.Write((byte)Snapshot.IncomingTransitionMode);
					if (Schema == SnapshotSchema)
					{
						writer.Write((byte)Snapshot.FootprintX);
						writer.Write((byte)Snapshot.FootprintY);
						writer.Write((byte)Snapshot.FootprintWidth);
						writer.Write((byte)Snapshot.FootprintHeight);
						writer.Write((byte)Snapshot.BaseRoof);
					}
					writer.Write((byte)blueprints.Count);
					for (int i = 0; i < blueprints.Count; i++)
						WriteText(writer, blueprints[i], MaxBlueprintChars);
					if (!legacy)
					{
						writer.Write((byte)materials.Count);
						for (int i = 0; i < materials.Count; i++)
							WriteText(writer, materials[i], MaxKeyChars);
						writer.Write((byte)techs.Count);
						for (int i = 0; i < techs.Count; i++)
							WriteText(writer, techs[i], MaxKeyChars);
						writer.Write((byte)knowledge.Count);
						for (int i = 0; i < knowledge.Count; i++)
							WriteText(writer, knowledge[i], MaxKeyChars);
						writer.Write((byte)powers.Count);
						for (int i = 0; i < powers.Count; i++)
							WriteText(writer, powers[i], MaxKeyChars);
					}
					writer.Write((ushort)cells.Count);
					for (int i = 0; i < cells.Count; i++)
					{
						ArchitectureCellState cell = cells[i];
						writer.Write((byte)cell.X);
						writer.Write((byte)cell.Y);
						int flags = Schema == SnapshotSchema
							? (int)cell.Claim | ((int)cell.Passability << 2)
								| ((int)cell.Cover << 4)
							: (IsClaimed(cell.Claim) ? 1 : 0)
								| ((int)cell.Passability << 1) | ((int)cell.Cover << 3);
						writer.Write((byte)flags);
					}
					writer.Write((byte)anchors.Count);
					for (int i = 0; i < anchors.Count; i++)
					{
						ArchitectureAnchor anchor = anchors[i];
						WriteText(writer, anchor.Key, MaxKeyChars);
						writer.Write((byte)anchor.X);
						writer.Write((byte)anchor.Y);
						writer.Write((byte)anchor.Access);
					}
					writer.Write((ushort)placements.Count);
					for (int i = 0; i < placements.Count; i++)
					{
						ArchitecturePlacement placement = placements[i];
						writer.Write((byte)placement.Layer);
						writer.Write((byte)placement.X);
						writer.Write((byte)placement.Y);
						writer.Write(blueprintIndexes[placement.Blueprint]);
						writer.Write(string.IsNullOrEmpty(placement.StatefulAnchor)
							? NoAnchorIndex : anchorIndexes[placement.StatefulAnchor]);
						if (!legacy)
						{
							writer.Write(materialIndexes[placement.Material]);
							writer.Write(techIndexes[placement.MinTech]);
							writer.Write((byte)((placement.Natural ? 1 : 0)
								| (placement.ExistingAuthority ? 2 : 0)));
							writer.Write(string.IsNullOrEmpty(placement.Knowledge)
								? NoKnowledgeIndex : knowledgeIndexes[placement.Knowledge]);
							writer.Write(string.IsNullOrEmpty(placement.Power)
								? NoPowerIndex : powerIndexes[placement.Power]);
						}
					}
					writer.Flush();
					payload = stream.ToArray();
				}
			}
			catch (Exception exception)
			{
				return Fail("snapshot encoding failed: " + exception.Message, out Failure);
			}
			if (payload.Length > MaxSnapshotPayloadBytes)
				return Fail("snapshot payload exceeds the byte bound ("
					+ payload.Length.ToString(CultureInfo.InvariantCulture) + " > "
					+ MaxSnapshotPayloadBytes.ToString(CultureInfo.InvariantCulture) + ")", out Failure);
			string hash = Hash(payload);
			string encoded = "a" + Schema.ToString(CultureInfo.InvariantCulture) + "|"
				+ Convert.ToBase64String(payload) + "|" + hash;
			if (encoded.Length > MaxSnapshotChars)
				return Fail("snapshot exceeds the character bound", out Failure);
			Encoded = encoded;
			return true;
		}

		private static List<string> BlueprintTable(IList<ArchitecturePlacement> Placements)
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++) seen.Add(Placements[i].Blueprint);
			List<string> result = new List<string>(seen);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static List<string> PlacementTextTable(
			IList<ArchitecturePlacement> Placements, int Field)
		{
			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Placements.Count; i++)
			{
				string value = Field == 0 ? Placements[i].Material
					: (Field == 1 ? Placements[i].MinTech
						: (Field == 2 ? Placements[i].Knowledge : Placements[i].Power));
				if (!string.IsNullOrEmpty(value)) seen.Add(value);
			}
			List<string> result = new List<string>(seen);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		private static bool LegacyPlacementTruthOnly(ArchitectureLayoutSnapshot Snapshot)
		{
			if (Snapshot == null || Snapshot.Placements == null) return false;
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement == null || !string.IsNullOrEmpty(placement.Material)
					|| !string.IsNullOrEmpty(placement.MinTech)
					|| !string.IsNullOrEmpty(placement.Knowledge) || placement.Natural
					|| !string.IsNullOrEmpty(placement.Power)
					|| placement.ExistingAuthority) return false;
			}
			return true;
		}

		private static bool LegacyClaimTruthOnly(ArchitectureLayoutSnapshot Snapshot)
		{
			if (Snapshot == null || Snapshot.Cells == null) return false;
			for (int i = 0; i < Snapshot.Cells.Count; i++)
			{
				ArchitectureCellState cell = Snapshot.Cells[i];
				if (cell == null || (cell.Claim != ArchitectureClaim.Unclaimed
					&& cell.Claim != ArchitectureClaim.LegacyClaimed)) return false;
			}
			return true;
		}

		private static void WriteText(BinaryWriter Writer, string Text, int MaximumChars)
		{
			if (Text == null || Text.Length > MaximumChars) throw new InvalidDataException("text bound");
			byte[] bytes = StrictUtf8.GetBytes(Text);
			if (bytes.Length > ushort.MaxValue) throw new InvalidDataException("text byte bound");
			Writer.Write((ushort)bytes.Length);
			Writer.Write(bytes);
		}

		private static string ReadText(BinaryReader Reader, int MaximumChars)
		{
			int length = Reader.ReadUInt16();
			if (length > MaximumChars * 4) throw new InvalidDataException("text byte bound");
			byte[] bytes = Reader.ReadBytes(length);
			if (bytes.Length != length) throw new EndOfStreamException();
			string result = StrictUtf8.GetString(bytes);
			if (result.Length > MaximumChars || StrictUtf8.GetByteCount(result) != length)
				throw new InvalidDataException("text character bound");
			return result;
		}

		private static string Hash(byte[] Payload)
		{
			byte[] digest;
			using (SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Payload);
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}

		private static bool CanonicalHash(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9') || (Value[i] >= 'a' && Value[i] <= 'f')))
					return false;
			return true;
		}

	}
}
