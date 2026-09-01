using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public sealed class KingdomAdoptionDesignationReceipt
	{
		public int WireVersion = 2;
		public string ZoneId;
		public string RootId;
		public string BuildingKey;
		public bool ContainerOnly;
		public bool OpenPlot;
		public string ForeignProviderId;
		public string ForeignProviderVersion;
		public string ForeignIdentity;
		public string ForeignRevision;
		public List<ArchitecturePoint> Cells = new List<ArchitecturePoint>();
		public string Revision;
	}

	/// <summary>Canonical durable exact-space receipt. The hash is the designation revision.</summary>
	public static class KingdomAdoptionDesignationRules
	{
		public const int Schema = 1;
		public const int MaxReceiptChars = 16384;

		public static bool TryCreate(string ZoneId, string RootId, string BuildingKey,
			IReadOnlyList<ArchitecturePoint> Cells, bool ContainerOnly, string ForeignProviderId,
			string ForeignProviderVersion, string ForeignIdentity, string ForeignRevision,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			return TryCreate(ZoneId, RootId, BuildingKey, Cells, ContainerOnly, false,
				ForeignProviderId, ForeignProviderVersion, ForeignIdentity, ForeignRevision,
				out Receipt, out Failure);
		}

		public static bool TryCreate(string ZoneId, string RootId, string BuildingKey,
			IReadOnlyList<ArchitecturePoint> Cells, bool ContainerOnly, bool OpenPlot,
			string ForeignProviderId, string ForeignProviderVersion, string ForeignIdentity,
			string ForeignRevision, out KingdomAdoptionDesignationReceipt Receipt,
			out string Failure)
		{
			Receipt = null; Failure = null;
			if (ContainerOnly && OpenPlot)
				return Fail("adoption designation cannot be both container and open plot", out Failure);
			bool foreign = !string.IsNullOrEmpty(ForeignProviderId)
				|| !string.IsNullOrEmpty(ForeignProviderVersion)
				|| !string.IsNullOrEmpty(ForeignIdentity) || !string.IsNullOrEmpty(ForeignRevision);
			if (foreign && (ContainerOnly || OpenPlot))
				return Fail("foreign footprint proof belongs only to an exact room", out Failure);
			if (!KingdomDesignationRules.SafeToken(ZoneId, 256)
				|| !KingdomDesignationRules.SafeToken(RootId, 256)
				|| !KingdomDesignationRules.SafeToken(BuildingKey, 128)
				|| (foreign && (!KingdomDesignationRules.SafeToken(ForeignProviderId, 64)
					|| !KingdomDesignationRules.SafeToken(ForeignProviderVersion, 32)
					|| !KingdomDesignationRules.SafeToken(ForeignIdentity, 256)
					|| !KingdomDesignationRules.SafeToken(ForeignRevision, 256))))
				return Fail("adoption designation identity is malformed", out Failure);
			int minimum = ContainerOnly ? 1 : OpenPlot ? 1 : KingdomAdoptRules.MinEnclosedRoomCells;
			int maximum = ContainerOnly ? 1 : OpenPlot
				? KingdomDesignationRules.MaxCellsPerDesignation
				: KingdomAdoptRules.MaxEnclosedRoomCells;
			if (Cells == null || Cells.Count < minimum
				|| Cells.Count > maximum)
				return Fail("adoption designation has no bounded exact target", out Failure);
			List<ArchitecturePoint> cells = new List<ArchitecturePoint>();
			HashSet<long> seen = new HashSet<long>();
			for (int i = 0; i < Cells.Count; i++)
			{
				if (Cells[i].X < 0 || Cells[i].Y < 0
					|| !seen.Add(KingdomDesignationRules.Pack(Cells[i].X, Cells[i].Y)))
					return Fail("adoption designation cells are malformed or duplicated", out Failure);
				cells.Add(Cells[i]);
			}
			cells.Sort(ComparePoints);
			KingdomAdoptionDesignationReceipt receipt = new KingdomAdoptionDesignationReceipt {
				ZoneId = ZoneId, RootId = RootId, BuildingKey = BuildingKey,
					WireVersion = 2, ContainerOnly = ContainerOnly, OpenPlot = OpenPlot,
				ForeignProviderId = foreign ? ForeignProviderId : "",
				ForeignProviderVersion = foreign ? ForeignProviderVersion : "",
				ForeignIdentity = foreign ? ForeignIdentity : "",
				ForeignRevision = foreign ? ForeignRevision : "", Cells = cells
			};
			receipt.Revision = Hash(Body(receipt)); Receipt = receipt;
			return true;
		}

		public static string Encode(KingdomAdoptionDesignationReceipt Receipt)
		{
			if (Receipt == null) return null;
			string body = Body(Receipt);
			string hash = Hash(body);
			if (Receipt.Revision != hash) return null;
			string encoded = body + "|" + hash;
			return encoded.Length <= MaxReceiptChars ? encoded : null;
		}

		public static bool TryDecode(string Encoded,
			out KingdomAdoptionDesignationReceipt Receipt, out string Failure)
		{
			Receipt = null; Failure = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxReceiptChars)
				return Fail("adoption designation receipt is absent or over its bound", out Failure);
			string[] fields = Encoded.Split('|');
			bool legacy = fields.Length == 11 && fields[0] == "d1";
			bool current = fields.Length == 12 && fields[0] == "d2";
			if (!legacy && !current)
				return Fail("adoption designation receipt schema is unknown", out Failure);
			string zone; string root; string building; string provider; string version;
			string identity; string revision;
			if (!Unframe(fields[1], out zone) || !Unframe(fields[2], out root)
				|| !Unframe(fields[3], out building) || !Unframe(fields[4], out provider)
				|| !Unframe(fields[5], out version) || !Unframe(fields[6], out identity)
				|| !Unframe(fields[7], out revision)
					|| (fields[8] != "0" && fields[8] != "1")
					|| (current && fields[9] != "0" && fields[9] != "1"))
				return Fail("adoption designation receipt text is malformed", out Failure);
			List<ArchitecturePoint> cells = new List<ArchitecturePoint>();
			int cellsField = current ? 10 : 9;
			int hashField = current ? 11 : 10;
			string[] pairs = fields[cellsField].Split(';');
			for (int i = 0; i < pairs.Length; i++)
			{
				string[] xy = pairs[i].Split(',');
				if (xy.Length != 2 || !int.TryParse(xy[0], NumberStyles.None,
					CultureInfo.InvariantCulture, out int x) || !int.TryParse(xy[1], NumberStyles.None,
					CultureInfo.InvariantCulture, out int y))
					return Fail("adoption designation cell text is malformed", out Failure);
				cells.Add(new ArchitecturePoint(x, y));
			}
			if (!TryCreate(zone, root, building, cells, fields[8] == "1",
				current && fields[9] == "1", provider, version, identity, revision,
				out Receipt, out Failure)) return false;
			Receipt.WireVersion = current ? 2 : 1;
			Receipt.Revision = Hash(Body(Receipt));
			if (Receipt.Revision != fields[hashField] || Encode(Receipt) != Encoded)
					return Failure != null ? false : Fail("adoption designation hash disagrees", out Failure);
			return true;
		}

		private static string Body(KingdomAdoptionDesignationReceipt Receipt)
		{
			StringBuilder cells = new StringBuilder();
			for (int i = 0; i < Receipt.Cells.Count; i++)
			{
				if (i > 0) cells.Append(';');
				cells.Append(Receipt.Cells[i].X.ToString(CultureInfo.InvariantCulture));
				cells.Append(','); cells.Append(Receipt.Cells[i].Y.ToString(CultureInfo.InvariantCulture));
			}
			string head = (Receipt.WireVersion == 1 ? "d1" : "d2") + "|"
				+ Frame(Receipt.ZoneId) + "|" + Frame(Receipt.RootId) + "|"
				+ Frame(Receipt.BuildingKey) + "|" + Frame(Receipt.ForeignProviderId) + "|"
				+ Frame(Receipt.ForeignProviderVersion) + "|" + Frame(Receipt.ForeignIdentity)
				+ "|" + Frame(Receipt.ForeignRevision) + "|"
				+ (Receipt.ContainerOnly ? "1" : "0") + "|";
			return Receipt.WireVersion == 1 ? head + cells
				: head + (Receipt.OpenPlot ? "1" : "0") + "|" + cells;
		}

		private static string Frame(string Value) => Convert.ToBase64String(
			Encoding.UTF8.GetBytes(Value ?? ""));
		private static bool Unframe(string Value, out string Result)
		{
			try { Result = Encoding.UTF8.GetString(Convert.FromBase64String(Value)); return true; }
			catch { Result = null; return false; }
		}
		private static string Hash(string Value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Value));
				StringBuilder result = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
				return result.ToString();
			}
		}
		private static int ComparePoints(ArchitecturePoint A, ArchitecturePoint B)
		{
			int y = A.Y.CompareTo(B.Y); return y != 0 ? y : A.X.CompareTo(B.X);
		}
		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
