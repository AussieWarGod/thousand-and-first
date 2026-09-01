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
		public static bool TryDecodeSnapshot(string Encoded, out ArchitectureLayoutSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null;
			Failure = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxSnapshotChars)
				return Fail("snapshot is empty or over the character bound", out Failure);
			string[] terms = Encoded.Split('|');
			int schema = terms.Length == 3 && terms[0] == "a1" ? LegacySnapshotSchema
				: (terms.Length == 3 && terms[0] == "a2" ? PlacementTruthSnapshotSchema
					: (terms.Length == 3 && terms[0] == "a3" ? TransitionSnapshotSchema
						: (terms.Length == 3 && terms[0] == "a4" ? SnapshotSchema : 0)));
			if (schema == 0)
				return Fail("snapshot version is unsupported", out Failure);
			if (!CanonicalHash(terms[2])) return Fail("snapshot hash is malformed", out Failure);
			byte[] payload;
			try { payload = Convert.FromBase64String(terms[1]); }
			catch { return Fail("snapshot payload is not base64", out Failure); }
			if (payload.Length == 0 || payload.Length > MaxSnapshotPayloadBytes)
				return Fail("snapshot payload is empty or over the byte bound", out Failure);
			if (Hash(payload) != terms[2]) return Fail("snapshot hash does not match its payload", out Failure);
			ArchitectureLayoutSnapshot parsed;
			try
			{
				using (MemoryStream stream = new MemoryStream(payload, false))
				using (BinaryReader reader = new BinaryReader(stream, StrictUtf8))
				{
					if (reader.ReadByte() != (byte)'T' || reader.ReadByte() != (byte)'A'
						|| reader.ReadByte() != (byte)'F' || reader.ReadByte() != schema)
						return Fail("snapshot payload header is unsupported", out Failure);
					parsed = new ArchitectureLayoutSnapshot
					{
						PlanKey = ReadText(reader, MaxKeyChars),
						BindingKey = ReadText(reader, MaxKeyChars),
						BuildKey = ReadText(reader, MaxKeyChars),
						TierKey = ReadText(reader, MaxKeyChars),
						VariantKey = ReadText(reader, MaxKeyChars),
						PaletteKey = ReadText(reader, MaxKeyChars),
						LotType = ReadText(reader, MaxKeyChars),
						LotSize = (ArchitectureLotSize)reader.ReadByte(),
						Facing = (ArchitectureFacing)reader.ReadByte(),
						Width = reader.ReadByte(),
						Height = reader.ReadByte(),
						MainX = reader.ReadByte(),
						MainY = reader.ReadByte()
					};
					parsed.IncomingTransitionMode = schema >= TransitionSnapshotSchema
						? (ArchitectureTransitionMode)reader.ReadByte()
						: ArchitectureTransitionMode.None;
					if (schema == SnapshotSchema)
					{
						parsed.FootprintX = reader.ReadByte();
						parsed.FootprintY = reader.ReadByte();
						parsed.FootprintWidth = reader.ReadByte();
						parsed.FootprintHeight = reader.ReadByte();
						parsed.BaseRoof = (KingdomPlotRules.RoofState)reader.ReadByte();
					}
					else
					{
						parsed.FootprintWidth = parsed.Width;
						parsed.FootprintHeight = parsed.Height;
						parsed.BaseRoof = (KingdomPlotRules.RoofState)byte.MaxValue;
					}
					int blueprintCount = reader.ReadByte();
					if (blueprintCount > MaxPaletteSlots) throw new InvalidDataException("blueprint table bound");
					List<string> blueprints = new List<string>();
					for (int i = 0; i < blueprintCount; i++)
						blueprints.Add(ReadText(reader, MaxBlueprintChars));
					List<string> materials = new List<string>();
					List<string> techs = new List<string>();
					List<string> knowledge = new List<string>();
					List<string> powers = new List<string>();
					if (schema >= PlacementTruthSnapshotSchema)
					{
						int materialCount = reader.ReadByte();
						if (materialCount > MaxPaletteSlots)
							throw new InvalidDataException("material table bound");
						for (int i = 0; i < materialCount; i++)
							materials.Add(ReadText(reader, MaxKeyChars));
						int techCount = reader.ReadByte();
						if (techCount > MaxPaletteSlots)
							throw new InvalidDataException("tech table bound");
						for (int i = 0; i < techCount; i++)
							techs.Add(ReadText(reader, MaxKeyChars));
						int knowledgeCount = reader.ReadByte();
						if (knowledgeCount > MaxPaletteSlots)
							throw new InvalidDataException("knowledge table bound");
						for (int i = 0; i < knowledgeCount; i++)
							knowledge.Add(ReadText(reader, MaxKeyChars));
						int powerCount = reader.ReadByte();
						if (powerCount > MaxPaletteSlots)
							throw new InvalidDataException("power table bound");
						for (int i = 0; i < powerCount; i++)
							powers.Add(ReadText(reader, MaxKeyChars));
					}
					int cellCount = reader.ReadUInt16();
					if (cellCount > MaxMapArea) throw new InvalidDataException("cell bound");
					for (int i = 0; i < cellCount; i++)
					{
						int x = reader.ReadByte();
						int y = reader.ReadByte();
						int flags = reader.ReadByte();
						if ((flags & ~(schema == SnapshotSchema ? 63 : 31)) != 0)
							throw new InvalidDataException("cell flags");
						ArchitectureClaim claim = schema == SnapshotSchema
							? (ArchitectureClaim)(flags & 3)
							: ((flags & 1) == 0 ? ArchitectureClaim.Unclaimed
								: ArchitectureClaim.LegacyClaimed);
						if (schema == SnapshotSchema && claim == ArchitectureClaim.LegacyClaimed)
							throw new InvalidDataException("legacy claim in current snapshot");
						parsed.Cells.Add(new ArchitectureCellState
						{
							X = x,
							Y = y,
							Claim = claim,
							Passability = (ArchitecturePassability)((flags
								>> (schema == SnapshotSchema ? 2 : 1)) & 3),
							Cover = (ArchitectureCover)((flags
								>> (schema == SnapshotSchema ? 4 : 3)) & 3)
						});
					}
					int anchorCount = reader.ReadByte();
					if (anchorCount > MaxAnchors) throw new InvalidDataException("anchor bound");
					for (int i = 0; i < anchorCount; i++)
					{
						parsed.Anchors.Add(new ArchitectureAnchor
						{
							Key = ReadText(reader, MaxKeyChars),
							X = reader.ReadByte(),
							Y = reader.ReadByte(),
							Access = (ArchitectureAnchorAccess)reader.ReadByte()
						});
					}
					int placementCount = reader.ReadUInt16();
					if (placementCount > MaxPlacements) throw new InvalidDataException("placement bound");
					for (int i = 0; i < placementCount; i++)
					{
						ArchitectureLayer layer = (ArchitectureLayer)reader.ReadByte();
						int x = reader.ReadByte();
						int y = reader.ReadByte();
						int blueprint = reader.ReadByte();
						int anchor = reader.ReadUInt16();
						if (blueprint >= blueprints.Count || (anchor != NoAnchorIndex && anchor >= parsed.Anchors.Count))
							throw new InvalidDataException("placement reference");
						int material = -1;
						int tech = -1;
						bool natural = false;
						bool existing = false;
						int knowledgeIndex = NoKnowledgeIndex;
						int powerIndex = NoPowerIndex;
						if (schema >= PlacementTruthSnapshotSchema)
						{
							material = reader.ReadByte();
							tech = reader.ReadByte();
							int truthFlags = reader.ReadByte();
							if (material >= materials.Count || tech >= techs.Count || truthFlags > 3)
								throw new InvalidDataException("placement truth reference");
							natural = (truthFlags & 1) != 0;
							existing = (truthFlags & 2) != 0;
							knowledgeIndex = reader.ReadByte();
							if (knowledgeIndex != NoKnowledgeIndex
								&& knowledgeIndex >= knowledge.Count)
								throw new InvalidDataException("placement knowledge reference");
							powerIndex = reader.ReadByte();
							if (powerIndex != NoPowerIndex && powerIndex >= powers.Count)
								throw new InvalidDataException("placement power reference");
						}
						parsed.Placements.Add(new ArchitecturePlacement
						{
							Layer = layer,
							X = x,
							Y = y,
							Blueprint = blueprints[blueprint],
							Slot = SlotFor(layer, x, y),
							Material = schema >= PlacementTruthSnapshotSchema ? materials[material] : null,
							MinTech = schema >= PlacementTruthSnapshotSchema ? techs[tech] : null,
							Knowledge = schema >= PlacementTruthSnapshotSchema && knowledgeIndex != NoKnowledgeIndex
								? knowledge[knowledgeIndex] : null,
							Power = schema >= PlacementTruthSnapshotSchema && powerIndex != NoPowerIndex
								? powers[powerIndex] : null,
							Natural = natural,
							ExistingAuthority = existing,
							StatefulAnchor = anchor == NoAnchorIndex ? null : parsed.Anchors[anchor].Key
						});
					}
					if (stream.Position != stream.Length) throw new InvalidDataException("trailing bytes");
				}
			}
			catch (Exception exception)
			{
				return Fail("snapshot payload is malformed: " + exception.Message, out Failure);
			}
			if (!TryValidateTopologyCore(parsed, null, schema == LegacySnapshotSchema, out Failure)) return false;
			if (!TryEncodeSnapshotVersion(parsed, schema, out string canonical, out Failure)
				|| canonical != Encoded)
				return Failure != null ? false : Fail("snapshot is not canonical", out Failure);
			Snapshot = parsed;
			return true;
		}

		public static bool TrySnapshotHash(ArchitectureLayoutSnapshot Snapshot,
			out string SnapshotHash, out string Failure)
		{
			SnapshotHash = null;
			if (!TryEncodeSnapshot(Snapshot, out string encoded, out Failure)) return false;
			SnapshotHash = encoded.Substring(encoded.LastIndexOf('|') + 1);
			return true;
		}

		public static bool TryEncodedSnapshotHash(string Encoded,
			out string SnapshotHash, out string Failure)
		{
			SnapshotHash = null;
			ArchitectureLayoutSnapshot ignored;
			if (!TryDecodeSnapshot(Encoded, out ignored, out Failure)) return false;
			SnapshotHash = Encoded.Substring(Encoded.LastIndexOf('|') + 1);
			return true;
		}

		public static bool IsLatestSnapshotEncoding(string Encoded)
		{
			return Encoded != null && Encoded.StartsWith("a4|", StringComparison.Ordinal);
		}

		public static bool IsManagedSnapshotEncoding(string Encoded)
		{
			return Encoded != null && (Encoded.StartsWith("a3|", StringComparison.Ordinal)
				|| Encoded.StartsWith("a4|", StringComparison.Ordinal));
		}

		public static bool IsCurrentSnapshotEncoding(string Encoded)
		{
			return IsLatestSnapshotEncoding(Encoded);
		}

	}
}
