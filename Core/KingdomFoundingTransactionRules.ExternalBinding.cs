using System.Security.Cryptography;
using System.Text;
using ThousandAndFirst.Api;

namespace ThousandAndFirst
{
	public static partial class KingdomFoundingTransactionRules
	{
		public static string PayloadDigestWithExternalBinding(KingdomFoundingKind Kind,
			string Name, string Vocation, string VillageFaction, string VillageDisplay,
			int OriginalVolume, int OriginalMax, int CommittedVolume, int CommittedMax,
			string OriginalComponents, string CommittedComponents, string ExternalBinding)
		{
			if (!IsKnownKind(Kind) || Kind == KingdomFoundingKind.None
				|| !KingdomExternalOwnershipRules.TryDecode(ExternalBinding, out var binding)
				|| !KingdomExternalOwnershipRules.ValidBinding(binding)) return null;
			StringBuilder payload = new StringBuilder();
			AppendDigestField(payload, "taf-founding-payload-v2");
			AppendDigestField(payload, ((int)Kind).ToString());
			AppendDigestField(payload, Name);
			AppendDigestField(payload, Vocation);
			AppendDigestField(payload, VillageFaction);
			AppendDigestField(payload, VillageDisplay);
			AppendDigestField(payload, OriginalVolume.ToString());
			AppendDigestField(payload, OriginalMax.ToString());
			AppendDigestField(payload, CommittedVolume.ToString());
			AppendDigestField(payload, CommittedMax.ToString());
			AppendDigestField(payload, OriginalComponents);
			AppendDigestField(payload, CommittedComponents);
			AppendDigestField(payload, ExternalBinding);
			try
			{
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(payload.ToString()));
					StringBuilder hex = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++) hex.Append(digest[i].ToString("x2"));
					return hex.ToString();
				}
			}
			catch
			{
				return null;
			}
		}
	}
}
