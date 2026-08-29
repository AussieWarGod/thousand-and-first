using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>One caller-supplied simultaneous request. It is never persisted as a queue.</summary>
	public sealed class KingdomExperienceAdmissionCandidate
	{
		public KingdomExperienceLane Lane;
		public string SettlementId;
		public string SourceId;
		public long CauseTick;
		public ulong WindowOrdinal;
		public int BodyCount;
		public bool ExactRetry;
		public bool HasDirectFallback;
	}

	/// <summary>Common deterministic priority/fairness vocabulary for W0 participants.</summary>
	public static class KingdomExperienceFairnessRules
	{
		public static string Ticket(KingdomExperienceLane Lane, string SettlementId,
			string SourceId, long CauseTick, ulong WindowOrdinal)
		{
			if (Lane < KingdomExperienceLane.CivicVoices || Lane > KingdomExperienceLane.PolityCohort
				|| !KernelSemanticId.IsValid(SettlementId) || !KernelSemanticId.IsValid(SourceId)
				|| CauseTick < 0L) return null;
			return "taf:experience-fairness:v1:" + Digest(((byte)Lane).ToString(
				CultureInfo.InvariantCulture), SettlementId, SourceId,
				CauseTick.ToString(CultureInfo.InvariantCulture),
				WindowOrdinal.ToString(CultureInfo.InvariantCulture));
		}

		/// <summary>Orders a supplied simultaneous set only; retains no backlog and evicts nothing.</summary>
		public static bool TryOrder(IList<KingdomExperienceAdmissionCandidate> Requests,
			out List<KingdomExperienceAdmissionCandidate> Ordered, out string Failure)
		{
			Ordered = new List<KingdomExperienceAdmissionCandidate>(); Failure = null;
			if (Requests == null || Requests.Count > KingdomExperienceRules.MaxBodyReservations)
				return Fail("W0 fairness request set is absent or unbounded", out Failure);
			HashSet<string> tickets = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Requests.Count; i++)
			{
				KingdomExperienceAdmissionCandidate row = Requests[i];
				string ticket = row == null ? null : Ticket(row.Lane, row.SettlementId,
					row.SourceId, row.CauseTick, row.WindowOrdinal);
				if (ticket == null || row.BodyCount < 0
					|| row.BodyCount > KingdomExperienceRules.MaxBodiesPerReservation
					|| !tickets.Add(ticket))
					return Fail("W0 fairness request is invalid or duplicated", out Failure);
				Ordered.Add(row);
			}
			Ordered.Sort(Compare); return true;
		}

		private static int Compare(KingdomExperienceAdmissionCandidate A,
			KingdomExperienceAdmissionCandidate B)
		{
			int priority = Priority(A).CompareTo(Priority(B));
			if (priority != 0) return priority;
			int rotation = Rotation(A).CompareTo(Rotation(B));
			if (rotation != 0) return rotation;
			return string.CompareOrdinal(Ticket(A.Lane, A.SettlementId, A.SourceId,
				A.CauseTick, A.WindowOrdinal), Ticket(B.Lane, B.SettlementId, B.SourceId,
				B.CauseTick, B.WindowOrdinal));
		}

		private static int Priority(KingdomExperienceAdmissionCandidate R)
		{
			if (R.ExactRetry) return 0;
			return R.HasDirectFallback ? 2 : 1;
		}

		private static int Rotation(KingdomExperienceAdmissionCandidate R)
		{
			const int lanes = (int)KingdomExperienceLane.PolityCohort;
			return ((int)R.Lane - 1 - (int)(R.WindowOrdinal % (ulong)lanes) + lanes) % lanes;
		}

		private static string Digest(params string[] Values)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
			{
				Write(writer, "experience-fairness-v1");
				for (int i = 0; i < Values.Length; i++) Write(writer, Values[i]);
				writer.Flush(); using (SHA256 sha = SHA256.Create())
					return Hex(sha.ComputeHash(stream.ToArray()));
			}
		}

		private static void Write(BinaryWriter Writer, string Value)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(Value ?? "");
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static string Hex(byte[] Bytes)
		{
			const string alphabet = "0123456789abcdef"; char[] chars = new char[Bytes.Length * 2];
			for (int i = 0; i < Bytes.Length; i++)
			{
				chars[i * 2] = alphabet[Bytes[i] >> 4];
				chars[i * 2 + 1] = alphabet[Bytes[i] & 15];
			}
			return new string(chars);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
