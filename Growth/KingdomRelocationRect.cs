namespace ThousandAndFirst
{
	/// <summary>Engine-free inclusive rectangle frozen into relocation authority.</summary>
	public struct KingdomRelocationRect
	{
		public int X1;
		public int Y1;
		public int X2;
		public int Y2;

		public KingdomRelocationRect(int x1, int y1, int x2, int y2)
		{
			X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
		}

		public int Width { get { return X2 - X1 + 1; } }
		public int Height { get { return Y2 - Y1 + 1; } }
		public int CenterX { get { return X1 + (Width - 1) / 2; } }
		public int CenterY { get { return Y1 + (Height - 1) / 2; } }
		public int Area
		{
			get
			{
				long area = (long)Width * Height;
				return area > int.MaxValue ? int.MaxValue : (area < 0 ? 0 : (int)area);
			}
		}

		public bool Contains(int x, int y)
		{
			return x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
		}
	}
}
