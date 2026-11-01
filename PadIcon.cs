using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PadBattery;

static class PadIcon
{
    const int Supersample = 4;
    const float SvgWidth = 1280f, SvgHeight = 825f;
    const string GamepadSvg = "M2870 8230 c-370 -79 -762 -394 -1157 -930 -776 -1054 -1451 -2859 -1653 -4415 -44 -340 -54 -500 -54 -860 0 -366 10 -507 54 -755 135 -749 488 -1174 1043 -1256 264 -39 525 12 787 153 249 134 457 300 750 599 257 262 426 464 860 1029 139 182 295 381 347 442 l94 113 2459 0 2459 0 94 -113 c52 -61 220 -276 375 -477 456 -594 628 -795 918 -1080 246 -240 416 -374 629 -494 278 -156 547 -213 822 -172 129 19 235 52 344 107 679 340 915 1448 654 3064 -210 1300 -740 2753 -1371 3764 -204 326 -395 571 -619 797 -265 266 -479 405 -731 476 -79 23 -84 23 -934 26 -830 3 -856 2 -920 -17 -85 -26 -209 -105 -410 -261 -181 -140 -280 -204 -357 -232 -49 -17 -104 -18 -953 -18 -849 0 -904 1 -953 18 -77 28 -176 92 -357 232 -200 155 -324 234 -408 260 -62 19 -93 20 -896 19 -764 0 -838 -2 -916 -19z m6713 -1244 c105 -25 197 -106 233 -208 20 -55 20 -161 0 -216 -83 -232 -381 -295 -546 -116 -65 70 -85 123 -85 224 0 65 5 96 21 131 42 90 125 159 223 185 60 16 84 16 154 0z m-6223 -491 l0 -355 355 0 355 0 -2 -236 -3 -235 -352 1 -353 0 -2 -357 -3 -358 -237 -3 -238 -2 0 360 0 360 -355 0 -355 0 0 235 0 235 355 0 355 0 0 355 0 355 240 0 240 0 0 -355z m5423 -309 c108 -25 207 -118 237 -222 18 -62 9 -180 -18 -236 -31 -63 -96 -126 -162 -157 -80 -37 -192 -36 -271 2 -66 32 -132 99 -162 165 -31 66 -31 198 -1 263 43 92 132 165 231 188 55 13 81 12 146 -3z m1684 -26 c113 -56 178 -161 178 -290 0 -65 -5 -85 -33 -142 -58 -119 -161 -183 -292 -183 -132 1 -234 65 -292 183 -28 57 -33 76 -33 142 1 60 6 87 27 131 80 174 273 243 445 159z m-805 -808 c64 -31 126 -96 157 -162 34 -73 36 -191 4 -262 -30 -66 -95 -133 -162 -165 -48 -24 -68 -28 -141 -28 -73 0 -93 4 -142 28 -167 82 -234 293 -142 453 31 55 114 129 167 148 64 24 199 17 259 -12z m-4507 -1287 c241 -51 446 -255 501 -501 34 -154 4 -333 -78 -472 -52 -89 -160 -191 -253 -239 -107 -55 -189 -76 -305 -76 -256 -1 -480 139 -590 368 -50 105 -64 169 -63 285 1 257 138 473 370 586 127 62 275 79 418 49z m2768 0 c137 -29 284 -121 371 -231 96 -122 139 -252 139 -414 -1 -185 -66 -333 -204 -465 -127 -121 -273 -179 -449 -178 -177 1 -333 65 -455 188 -123 124 -183 257 -192 425 -7 123 13 220 69 333 82 164 260 303 438 341 85 19 199 19 283 1z";

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    public static void Destroy(Icon icon)
    {
        DestroyIcon(icon.Handle);
        icon.Dispose();
    }

    public static Icon Draw(int size, int? percent, bool charging, bool light)
    {
        var color = light ? Color.Black : Color.White;
        float unit = size / 16f;

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            int padHeight = (int)MathF.Round(size * SvgHeight / SvgWidth);
            var pad = new Rectangle(0, (size - padHeight) / 2 + 1, size, padHeight);
            DrawGamepad(g, color, pad, 2 * unit, percent);

            if (charging) DrawBolt(g, color, size / 2f, pad.Y + padHeight * 0.42f, padHeight * 0.85f, unit);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    static void DrawBolt(Graphics g, Color color, float cx, float cy, float h, float unit)
    {
        using var bolt = new GraphicsPath();
        bolt.AddPolygon(new[]
        {
            new PointF(cx + 0.10f * h, cy - 0.5f * h),
            new PointF(cx - 0.32f * h, cy + 0.08f * h),
            new PointF(cx - 0.02f * h, cy + 0.08f * h),
            new PointF(cx - 0.10f * h, cy + 0.5f * h),
            new PointF(cx + 0.32f * h, cy - 0.08f * h),
            new PointF(cx + 0.02f * h, cy - 0.08f * h),
        });

        g.CompositingMode = CompositingMode.SourceCopy;
        using (var gap = new Pen(Color.Transparent, 2 * unit) { LineJoin = LineJoin.Round })
            g.DrawPath(gap, bolt);
        g.CompositingMode = CompositingMode.SourceOver;

        using var brush = new SolidBrush(color);
        g.FillPath(brush, bolt);
    }

    static void DrawGamepad(Graphics g, Color color, Rectangle pad, float stroke, int? percent)
    {
        int width = pad.Width * Supersample, height = pad.Height * Supersample;
        var shape = Rasterize(width, height, details: pad.Height >= 12);
        int radius = (int)MathF.Round(stroke * Supersample);
        int fillTo = percent is int p ? (int)MathF.Round(width * p / 100f) : -1;

        bool Empty(int x, int y) => x < 0 || y < 0 || x >= width || y >= height || !shape[x, y];
        bool NearEdge(int x, int y)
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    if (dx * dx + dy * dy < radius * radius && Empty(x + dx, y + dy)) return true;
            return false;
        }

        using var result = new Bitmap(pad.Width, pad.Height, PixelFormat.Format32bppArgb);
        for (int oy = 0; oy < pad.Height; oy++)
            for (int ox = 0; ox < pad.Width; ox++)
            {
                int covered = 0;
                for (int sy = 0; sy < Supersample; sy++)
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        int x = ox * Supersample + sx, y = oy * Supersample + sy;
                        if (shape[x, y] && (x < fillTo || NearEdge(x, y))) covered++;
                    }
                if (covered > 0)
                    result.SetPixel(ox, oy, Color.FromArgb(color.A * covered / (Supersample * Supersample), color));
            }
        g.DrawImageUnscaled(result, pad.X, pad.Y);
    }

    static bool[,] Rasterize(int width, int height, bool details)
    {
        using var mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(mask))
        using (var path = GamepadPath(new RectangleF(0, 0, width, height), details))
            g.FillPath(Brushes.White, path);

        var data = mask.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var pixels = new int[width * height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        mask.UnlockBits(data);

        var shape = new bool[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                shape[x, y] = (pixels[y * width + x] >>> 24) > 127;
        return shape;
    }

    static GraphicsPath GamepadPath(RectangleF bounds, bool details)
    {
        var path = new GraphicsPath(FillMode.Alternate);
        var tokens = Regex.Matches(GamepadSvg, @"[MmLlCcZz]|-?\d*\.?\d+");
        int i = 0;
        char command = 'M';
        PointF current = PointF.Empty, start = PointF.Empty;

        float Number() => float.Parse(tokens[i++].Value, CultureInfo.InvariantCulture);
        PointF Point(bool relative, PointF origin)
        {
            float x = Number(), y = Number();
            return relative ? new PointF(origin.X + x, origin.Y + y) : new PointF(x, y);
        }

        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i].Value[0])) command = tokens[i++].Value[0];
            bool relative = char.IsLower(command);
            switch (char.ToUpper(command))
            {
                case 'M':
                    path.StartFigure();
                    current = start = Point(relative, current);
                    command = relative ? 'l' : 'L';
                    break;
                case 'L':
                    var end = Point(relative, current);
                    path.AddLine(current, end);
                    current = end;
                    break;
                case 'C':
                    var origin = current;
                    var c1 = Point(relative, origin);
                    var c2 = Point(relative, origin);
                    current = Point(relative, origin);
                    path.AddBezier(origin, c1, c2, current);
                    break;
                case 'Z':
                    path.CloseFigure();
                    current = start;
                    if (!details) i = tokens.Count;
                    break;
            }
        }

        using var transform = new Matrix();
        transform.Translate(bounds.X, bounds.Y);
        transform.Scale(bounds.Width / SvgWidth, bounds.Height / SvgHeight);
        transform.Translate(0, SvgHeight);
        transform.Scale(0.1f, -0.1f);
        path.Transform(transform);
        return path;
    }
}
