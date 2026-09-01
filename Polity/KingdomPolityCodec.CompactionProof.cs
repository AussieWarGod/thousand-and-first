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
				writer.Write(0x54505032); // TPP2 includes typed expression cues
				WriteList(writer, Profiles, KingdomPolityRules.MaxProfiles, WriteProfileV7);
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
