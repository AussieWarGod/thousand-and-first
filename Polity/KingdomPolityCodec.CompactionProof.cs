using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCodec
	{
		internal static byte[] EncodeProfileSetForDigest(
			IList<KingdomPolityProfileRevision> Profiles)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(0x54505031); // TPP1
				WriteList(writer, Profiles, KingdomPolityRules.MaxProfiles, WriteProfile);
				writer.Flush(); return stream.ToArray();
			}
		}

		internal static byte[] EncodeCompactionForFold(string PreviousDigest,
			KingdomPolityCompactionReceipt Receipt)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(0x54504331); // TPC1
				WriteString(writer, PreviousDigest); WriteCompaction(writer, Receipt);
				writer.Flush(); return stream.ToArray();
			}
		}
	}
}
