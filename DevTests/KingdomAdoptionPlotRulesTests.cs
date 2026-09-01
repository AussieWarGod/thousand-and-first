using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomAdoptionPlotRulesTests
	{
		[TestCase(KingdomPlotRules.PlotSize.Small, 6, 4)]
		[TestCase(KingdomPlotRules.PlotSize.Medium, 8, 6)]
		[TestCase(KingdomPlotRules.PlotSize.Large, 12, 10)]
		[TestCase(KingdomPlotRules.PlotSize.Huge, 20, 18)]
		public void CenteredPlotFreezesEveryExactCell(KingdomPlotRules.PlotSize size,
			int width, int height)
		{
			Assert.That(KingdomAdoptionPlotRules.TryCenteredCells(40, 12, size, 80, 25,
				out KingdomPlotRules.PlotRect rect, out List<ArchitecturePoint> cells,
				out string failure), Is.True, failure);
			Assert.That(rect.Width, Is.EqualTo(width));
			Assert.That(rect.Height, Is.EqualTo(height));
			Assert.That(cells.Count, Is.EqualTo(width * height));
			Assert.That(KingdomAdoptionPlotRules.Contains(rect, 40, 12), Is.True);
			Assert.That(cells[0].X, Is.EqualTo(rect.X1));
			Assert.That(cells[0].Y, Is.EqualTo(rect.Y1));
			Assert.That(cells[cells.Count - 1].X, Is.EqualTo(rect.X2));
			Assert.That(cells[cells.Count - 1].Y, Is.EqualTo(rect.Y2));
		}

		[Test]
		public void EdgeCrossingFailsWithoutPartialAuthority()
		{
			Assert.That(KingdomAdoptionPlotRules.TryCenteredCells(0, 0,
				KingdomPlotRules.PlotSize.Medium, 80, 25, out _,
				out List<ArchitecturePoint> cells, out string failure), Is.False);
			Assert.That(cells, Is.Empty);
			StringAssert.Contains("edge", failure);
		}

		[Test]
		public void OpenPlotReceiptRoundTripsAsCurrentExactAuthority()
		{
			Assert.That(KingdomAdoptionPlotRules.TryCenteredCells(20, 10,
				KingdomPlotRules.PlotSize.Small, 80, 25, out _,
				out List<ArchitecturePoint> cells, out _), Is.True);
			Assert.That(KingdomAdoptionDesignationRules.TryCreate("zone", "root", "fire",
				cells, false, true, null, null, null, null,
				out KingdomAdoptionDesignationReceipt receipt, out string failure), Is.True, failure);
			string encoded = KingdomAdoptionDesignationRules.Encode(receipt);
			StringAssert.StartsWith("d2|", encoded);
			Assert.That(KingdomAdoptionDesignationRules.TryDecode(encoded,
				out KingdomAdoptionDesignationReceipt read, out failure), Is.True, failure);
			Assert.That(read.OpenPlot, Is.True);
			Assert.That(read.ContainerOnly, Is.False);
			Assert.That(read.Cells.Count, Is.EqualTo(24));
			Assert.That(KingdomAdoptionDesignationRules.Encode(read), Is.EqualTo(encoded));
		}

		[Test]
		public void LegacyRoomReceiptRemainsByteCanonical()
		{
			string body = "d1|" + Frame("zone") + "|" + Frame("root") + "|"
				+ Frame("house") + "|||||0|1,1;2,1;1,2;2,2";
			string encoded = body + "|" + Hash(body);
			Assert.That(KingdomAdoptionDesignationRules.TryDecode(encoded,
				out KingdomAdoptionDesignationReceipt read, out string failure), Is.True, failure);
			Assert.That(read.WireVersion, Is.EqualTo(1));
			Assert.That(read.OpenPlot, Is.False);
			Assert.That(KingdomAdoptionDesignationRules.Encode(read), Is.EqualTo(encoded));
		}

		[Test]
		public void ContainerAndOpenPlotCannotShareOneReceipt()
		{
			ArchitecturePoint[] one = { new ArchitecturePoint(1, 1) };
			Assert.That(KingdomAdoptionDesignationRules.TryCreate("zone", "root", "larder",
				one, true, true, null, null, null, null, out _, out string failure), Is.False);
			StringAssert.Contains("both", failure);
		}

		[TestCase(false, true)]
		[TestCase(true, false)]
		public void NonRoomAuthorityCannotClaimForeignFootprintProof(bool container,
			bool open)
		{
			ArchitecturePoint[] one = { new ArchitecturePoint(1, 1) };
			Assert.That(KingdomAdoptionDesignationRules.TryCreate("zone", "root", "fire",
				one, container, open, "hearthpyre", "2.2.3", "room", "revision",
				out _, out string failure), Is.False);
			StringAssert.Contains("only to an exact room", failure);
		}

		private static string Frame(string value) => Convert.ToBase64String(
			Encoding.UTF8.GetBytes(value ?? ""));

		private static string Hash(string value)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
				StringBuilder result = new StringBuilder(64);
				for (int i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
				return result.ToString();
			}
		}
	}
}
