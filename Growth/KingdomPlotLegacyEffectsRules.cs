using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded canonical codec and monotone sink transitions for legacy plot effects.</summary>
	public static class KingdomPlotLegacyEffectsRules
	{
		private const int MaximumText = 1024;

		public static bool TryCreate(string FinalId, string PredecessorId, string Blueprint,
			string BuildKey, string PlotId, string ZoneId, int X, int Y, bool Founded,
			bool Heart, bool Delve, out KingdomPlotLegacyEffectsPlan Plan)
		{
			Plan = new KingdomPlotLegacyEffectsPlan
			{
				FinalId = FinalId, PredecessorId = PredecessorId, Blueprint = Blueprint,
				BuildKey = BuildKey, PlotId = PlotId, ZoneId = ZoneId, X = X, Y = Y,
				Founded = Founded, Heart = Heart, Delve = Delve,
				Raising = KingdomFoundingHeartSinkDisposition.Pending,
				HeartSink = Heart ? KingdomFoundingHeartSinkDisposition.Pending
					: KingdomFoundingHeartSinkDisposition.Settled,
				DelveSink = Delve ? KingdomFoundingHeartSinkDisposition.Pending
					: KingdomFoundingHeartSinkDisposition.Settled
			};
			if (Valid(Plan)) return true;
			Plan = null;
			return false;
		}

		public static bool Valid(KingdomPlotLegacyEffectsPlan Plan)
		{
			return Plan != null && Token(Plan.FinalId) && Token(Plan.PredecessorId)
				&& Plan.FinalId != Plan.PredecessorId && Token(Plan.Blueprint)
				&& Token(Plan.BuildKey) && Token(Plan.PlotId) && Token(Plan.ZoneId)
				&& Plan.X >= 0 && Plan.X <= 4096 && Plan.Y >= 0 && Plan.Y <= 4096
				&& Sink(Plan.Raising) && Sink(Plan.HeartSink) && Sink(Plan.DelveSink)
				&& (Plan.Heart || Plan.HeartSink == KingdomFoundingHeartSinkDisposition.Settled)
				&& (Plan.Delve || Plan.DelveSink == KingdomFoundingHeartSinkDisposition.Settled);
		}

		public static bool SameBinding(KingdomPlotLegacyEffectsPlan A,
			KingdomPlotLegacyEffectsPlan B)
		{
			return Valid(A) && Valid(B) && A.FinalId == B.FinalId
				&& A.PredecessorId == B.PredecessorId && A.Blueprint == B.Blueprint
				&& A.BuildKey == B.BuildKey && A.PlotId == B.PlotId && A.ZoneId == B.ZoneId
				&& A.X == B.X && A.Y == B.Y && A.Founded == B.Founded
				&& A.Heart == B.Heart && A.Delve == B.Delve;
		}

		public static bool TryAdvance(KingdomPlotLegacyEffectsPlan Plan, int SinkIndex,
			KingdomFoundingHeartSinkDisposition Expected,
			KingdomFoundingHeartSinkDisposition Next)
		{
			if (!Valid(Plan) || SinkIndex < 0 || SinkIndex > 2
				|| (Expected == KingdomFoundingHeartSinkDisposition.Pending
					? Next != KingdomFoundingHeartSinkDisposition.Attempting
					: Expected != KingdomFoundingHeartSinkDisposition.Attempting
						|| Next != KingdomFoundingHeartSinkDisposition.Settled
							&& Next != KingdomFoundingHeartSinkDisposition.Lost)) return false;
			KingdomFoundingHeartSinkDisposition current = SinkIndex == 0 ? Plan.Raising
				: SinkIndex == 1 ? Plan.HeartSink : Plan.DelveSink;
			if (current != Expected) return false;
			if (SinkIndex == 0) Plan.Raising = Next;
			else if (SinkIndex == 1) Plan.HeartSink = Next;
			else Plan.DelveSink = Next;
			return Valid(Plan);
		}

		public static bool Complete(KingdomPlotLegacyEffectsPlan Plan)
		{
			return Valid(Plan) && Terminal(Plan.Raising) && Terminal(Plan.HeartSink)
				&& Terminal(Plan.DelveSink);
		}

		public static string Encode(KingdomPlotLegacyEffectsPlan Plan)
		{
			if (!Valid(Plan)) return null;
			string body = "le1|" + B64(Plan.FinalId) + "|" + B64(Plan.PredecessorId)
				+ "|" + B64(Plan.Blueprint) + "|" + B64(Plan.BuildKey) + "|"
				+ B64(Plan.PlotId) + "|" + B64(Plan.ZoneId) + "|" + N(Plan.X) + "|"
				+ N(Plan.Y) + "|" + B(Plan.Founded) + "|" + B(Plan.Heart) + "|"
				+ B(Plan.Delve) + "|" + N((int)Plan.Raising) + "|" + N((int)Plan.HeartSink)
				+ "|" + N((int)Plan.DelveSink);
			return body + "|" + Digest(body);
		}

		public static bool TryDecode(string Encoded, out KingdomPlotLegacyEffectsPlan Plan)
		{
			Plan = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaximumText * 8) return false;
			string[] p = Encoded.Split('|');
			if (p.Length != 16 || p[0] != "le1") return false;
			string body = string.Join("|", p, 0, 15);
			if (p[15] != Digest(body)) return false;
			try
			{
				Plan = new KingdomPlotLegacyEffectsPlan
				{
					FinalId = Text(p[1]), PredecessorId = Text(p[2]), Blueprint = Text(p[3]),
					BuildKey = Text(p[4]), PlotId = Text(p[5]), ZoneId = Text(p[6]),
					X = Number(p[7]), Y = Number(p[8]), Founded = Bool(p[9]),
					Heart = Bool(p[10]), Delve = Bool(p[11]),
					Raising = (KingdomFoundingHeartSinkDisposition)Number(p[12]),
					HeartSink = (KingdomFoundingHeartSinkDisposition)Number(p[13]),
					DelveSink = (KingdomFoundingHeartSinkDisposition)Number(p[14])
				};
				if (!Valid(Plan) || Encode(Plan) != Encoded) Plan = null;
			}
			catch { Plan = null; }
			return Plan != null;
		}

		private static bool Sink(KingdomFoundingHeartSinkDisposition Value) =>
			Value >= KingdomFoundingHeartSinkDisposition.Pending
				&& Value <= KingdomFoundingHeartSinkDisposition.Lost;
		private static bool Terminal(KingdomFoundingHeartSinkDisposition Value) =>
			Value == KingdomFoundingHeartSinkDisposition.Settled
				|| Value == KingdomFoundingHeartSinkDisposition.Lost;
		private static bool Token(string Value) => !string.IsNullOrEmpty(Value)
			&& Value.Length <= MaximumText;
		private static string B64(string Value) => Convert.ToBase64String(
			Encoding.UTF8.GetBytes(Value ?? ""));
		private static string Text(string Value) => Encoding.UTF8.GetString(
			Convert.FromBase64String(Value));
		private static string N(int Value) => Value.ToString(CultureInfo.InvariantCulture);
		private static int Number(string Value) => int.Parse(Value, NumberStyles.Integer,
			CultureInfo.InvariantCulture);
		private static string B(bool Value) => Value ? "1" : "0";
		private static bool Bool(string Value)
		{
			if (Value == "1") return true;
			if (Value == "0") return false;
			throw new FormatException();
		}

		private static string Digest(string Value)
		{
			byte[] digest;
			using (SHA256 hash = SHA256.Create())
				digest = hash.ComputeHash(Encoding.UTF8.GetBytes(Value ?? ""));
			StringBuilder text = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++) text.Append(digest[i].ToString("x2",
				CultureInfo.InvariantCulture));
			return text.ToString();
		}
	}
}
