using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace ThousandAndFirst.WorkshopSteam
{
    public sealed partial class UploadPackage
    {
        internal sealed class InventoryRow
        {
            internal readonly string Name, Hash;
            internal readonly long Size;
            internal InventoryRow(string name, string hash, long size)
            { Name = name; Hash = hash; Size = size; }
        }

        /// <summary>Exact content identity from a still-held closed package. Paths to the package,
        /// plan and receipt, and equivalent receipt spellings, cannot make unchanged content new.</summary>
        internal string VerifiedInventorySHA()
        {
            Require(Request != null && Revalidate(), "package inventory authority unavailable");
            string inventory = InstallInventorySHA();
            Require(Revalidate(), "package changed during inventory proof");
            return inventory;
        }

        private string InstallInventorySHA()
        {
            List<string> names = new List<string>(entries.Keys);
            names.Sort(CompareUtf8);
            List<InventoryRow> rows = new List<InventoryRow>(names.Count);
            foreach (string name in names)
            {
                Entry row = entries[name];
                Verify(row);
                rows.Add(new InventoryRow(name, row.Hash, row.Size));
            }
            return CanonicalInventorySHA(rows);
        }

        /// <summary>Pure framing shared with installed verification; not package custody or
        /// next-attempt authority. Preserves the existing taf-installed-inventory-v1 bytes.</summary>
        internal static string CanonicalInventorySHA(IList<InventoryRow> values)
        {
            Require(values != null && values.Count > 0 && values.Count <= 10000, "inventory row bounds");
            List<InventoryRow> rows = new List<InventoryRow>(values);
            long total = 0;
            foreach (InventoryRow row in rows)
            {
                Require(row != null && Name(row.Name) == row.Name && HashShape(row.Hash)
                    && row.Size >= 0 && row.Size <= Maximum - total, "inventory row invalid");
                total += row.Size;
            }
            rows.Sort((left, right) => CompareUtf8(left.Name, right.Name));
            for (int i = 1; i < rows.Count; i++)
                Require(CompareUtf8(rows[i - 1].Name, rows[i].Name) < 0, "duplicate inventory name");
            using (MemoryStream bytes = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(bytes, Utf8, true))
                {
                    InstallFrame(writer, "taf-installed-inventory-v1");
                    InstallFrame(writer, rows.Count.ToString(CultureInfo.InvariantCulture));
                    foreach (InventoryRow row in rows)
                    {
                        InstallFrame(writer, row.Name);
                        InstallFrame(writer, row.Hash);
                        InstallFrame(writer, row.Size.ToString(CultureInfo.InvariantCulture));
                    }
                    writer.Flush();
                }
                bytes.Position = 0;
                using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
            }
        }

        private static void InstallFrame(BinaryWriter writer, string value)
        {
            byte[] bytes = Utf8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
