using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Names the profile this run was actually launched from, so a journal row can be bound to the
	/// session that wrote it rather than assumed into one.
	///
	/// <para>THE SEAM. The launcher passes the profile root as the engine's save path
	/// (Tools/run-scenario.ps1 launch arguments), and KingdomScenarioJournal.ProfileRoot
	/// (Harness/KingdomScenarioJournal.cs:56-69) derives that root back from XRLCore.SavePath --
	/// the same root every journal row is already written under. The caller passes that root in;
	/// this shard touches no engine type, so it compiles and is tested outside the game. Two
	/// identifiers come from the root, and both are the ones Tools/scenario_run_record.py sealed
	/// at preparation time:</para>
	///
	/// <list type="bullet">
	/// <item>profile: the root directory's own name, which the record sealed as profileName.</item>
	/// <item>seal: the SHA-256 of the closed profile seal at &lt;root&gt;.seal/profile.sha256
	/// (header taf-scenario-profile-seal-v1), which the record sealed as profileSeal.</item>
	/// </list>
	///
	/// <para>Nothing is invented. If the root cannot be derived, or the seal cannot be read, or it
	/// does not carry its own header, this returns null and the caller refuses -- a row stamped
	/// with a guess would be worse than a row that never landed.</para>
	/// </summary>
	internal static class KingdomQuickstartLifecycleStamp
	{
		/// <summary>The seal file's header, as Tools/scenario_profile.py writes it.</summary>
		internal const string SealHeader = "taf-scenario-profile-seal-v1";

		/// <summary>A closed seal over a whole profile is small; a larger file is not one.</summary>
		internal const int MaxSealBytes = 4 * 1024 * 1024;

		private static string Cached;
		private static string CachedRoot;

		/// <summary>
		/// "profile=&lt;name&gt; seal=&lt;sha256&gt;" for the given profile root, or null when it
		/// cannot be read honestly. Computed once per root: the profile a run was launched from
		/// cannot change under it, and re-hashing the seal on every row would be a cost with no
		/// evidence behind it.
		/// </summary>
		internal static string Text(string Root)
		{
			if (Cached != null && CachedRoot == Root) return Cached;
			try
			{
				string root = Root;
				if (string.IsNullOrEmpty(root)) return null;
				string name = new DirectoryInfo(root.TrimEnd('\\', '/')).Name;
				if (string.IsNullOrEmpty(name) || !Ascii(name)) return null;
				string seal = Path.Combine(root.TrimEnd('\\', '/') + ".seal", "profile.sha256");
				if (!File.Exists(seal)) return null;
				FileInfo info = new FileInfo(seal);
				if (info.Length <= 0 || info.Length > MaxSealBytes) return null;
				byte[] bytes = File.ReadAllBytes(seal);
				if (!StartsWithHeader(bytes)) return null;
				string digest = Hash(bytes);
				if (digest == null) return null;
				CachedRoot = Root;
				Cached = "profile=" + name + " seal=" + digest;
				return Cached;
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>The seal's first bytes are its own header, read as bytes so no encoding
		/// guess can turn a different file into this one.</summary>
		private static bool StartsWithHeader(byte[] Bytes)
		{
			byte[] header = Encoding.ASCII.GetBytes(SealHeader);
			if (Bytes == null || Bytes.Length < header.Length) return false;
			for (int i = 0; i < header.Length; i++) if (Bytes[i] != header[i]) return false;
			return true;
		}

		/// <summary>Lowercase hexadecimal SHA-256, the same spelling the run record wrote.</summary>
		private static string Hash(byte[] Bytes)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] digest = sha.ComputeHash(Bytes);
				StringBuilder text = new StringBuilder(digest.Length * 2);
				for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2"));
				return text.ToString();
			}
		}

		/// <summary>Printable ASCII only: a journal row is a fixed-width tab record, and a name
		/// carrying anything else would not survive it unchanged.</summary>
		private static bool Ascii(string Value)
		{
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] < ' ' || Value[i] > '~' || Value[i] == '\t' || Value[i] == ';')
					return false;
			return true;
		}
	}
}
