# Renders the Play Store feature graphic (1024x500) + the 512x512 store icon.
# Replicates PrimitiveSprites.GlossyOrb/OrbGloss exactly so the art matches the game.
param(
  [string]$Repo = "C:\Users\ojh20\Downloads\Unity\CoralCascade",
  [string]$Out  = "C:\Users\ojh20\Downloads\Unity\CoralCascade\store"
)

Add-Type -AssemblyName System.Drawing

$src = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Drawing.Imaging;

public static class Feature
{
    // ---- Unity math, replicated faithfully -------------------------------
    static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

    // Unity's Mathf.SmoothStep(from,to,t) maps INTO [from,to] -- it is NOT the
    // classic 0..1 edge function. GlossyOrb's "rim shadow" therefore dims the
    // WHOLE ball; replicating it as an edge function would produce a brighter,
    // wrong orb. (Recorded in CLAUDE.md after RenderOrbs.cs hit exactly this.)
    static float SmoothStep(float from, float to, float t)
    {
        t = Clamp01(t);
        t = -2f * t * t * t + 3f * t * t;
        return to * t + from * (1f - t);
    }

    // ---- Game palette (Assets/Scripts/Board/BubbleColor.cs + GameFlow.cs) --
    public static Color MapTop    = Color.FromArgb(158, 230, 247); // 0.62,0.90,0.97
    public static Color MapBottom = Color.FromArgb( 33, 143, 191); // 0.13,0.56,0.75
    public static Color DeepTeal  = Color.FromArgb(  5,  66,  92); // 0.02,0.26,0.36

    static Color[] Balls = new Color[] {
        Color.FromArgb(230,  64,  64), // Red    0.90,0.25,0.25
        Color.FromArgb(242, 140,  51), // Orange 0.95,0.55,0.20
        Color.FromArgb(242, 217,  64), // Yellow 0.95,0.85,0.25
        Color.FromArgb( 77, 191,  89), // Green  0.30,0.75,0.35
        Color.FromArgb( 64, 140, 230), // Blue   0.25,0.55,0.90
        Color.FromArgb(166,  89, 217), // Purple 0.65,0.35,0.85
    };

    /// <summary>One orb, rendered at `size` px: GlossyOrb luminance * tint, then
    /// the UNTINTED OrbGloss layer alpha-composited on top -- the same two-layer
    /// construction BubbleArt uses, because multiply-tint can never make a
    /// highlight whiter than the tint.</summary>
    public static Bitmap Orb(int size, Color tint)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        float center = (size - 1) * 0.5f;
        float radius = size * 0.5f;
        const float lx = -0.42f, ly = 0.52f, lz = 0.744f;

        var data = bmp.LockBits(new Rectangle(0, 0, size, size),
                                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[data.Stride * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / radius;
                // Unity texture space is bottom-up; System.Drawing is top-down.
                // Flipping dy here is what keeps the light in the upper-LEFT.
                float dy = -((y - center) / radius);
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                float a = Clamp01((1f - d) * radius / 1.5f);

                int o = y * data.Stride + x * 4;
                if (a <= 0f) { buf[o] = buf[o+1] = buf[o+2] = buf[o+3] = 0; continue; }

                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - d * d));
                float lambert = Clamp01(dx * lx + dy * ly + nz * lz);
                float lum = 0.60f + 0.40f * lambert;
                lum *= 1f - 0.22f * SmoothStep(0.78f, 1f, d);

                float r = (tint.R / 255f) * lum;
                float g = (tint.G / 255f) * lum;
                float b = (tint.B / 255f) * lum;

                // --- OrbGloss, additively over the tinted body ---
                if (d <= 1f)
                {
                    float ex = (dx + 0.38f) / 0.30f;
                    float ey = (dy - 0.42f) / 0.22f;
                    float fall = Clamp01(1f - (ex * ex + ey * ey));
                    float ga = 0.9f * fall * fall;
                    float band = SmoothStep(0.70f, 0.85f, d) * (1f - SmoothStep(0.88f, 0.98f, d));
                    float down = Clamp01((-dy - 0.15f) / 0.5f);
                    ga = Math.Min(1f, ga + 0.18f * band * down);

                    r = r * (1f - ga) + ga;
                    g = g * (1f - ga) + ga;
                    b = b * (1f - ga) + ga;
                }

                // Straight alpha; GDI+ DrawImage blends it.
                buf[o + 0] = (byte)(Clamp01(b) * 255f);
                buf[o + 1] = (byte)(Clamp01(g) * 255f);
                buf[o + 2] = (byte)(Clamp01(r) * 255f);
                buf[o + 3] = (byte)(a * 255f);
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    static void DrawFit(Graphics g, string text, Font font, FontFamily fam, RectangleF box,
                        Color fill, Color shadow, float shadowDx, float shadowDy)
    {
        // MEASURE, never assume: Kenney Future runs wide, and this project has
        // been bitten repeatedly by unmeasured text (banner, HUD, splash).
        float size = font.Size;
        SizeF m;
        Font f = font;
        while (true)
        {
            m = g.MeasureString(text, f);
            if (m.Width <= box.Width || size <= 8f) break;
            size -= 2f;
            f.Dispose();
            f = new Font(fam, size, font.Style, GraphicsUnit.Pixel);
        }
        float x = box.X + (box.Width - m.Width) * 0.5f;
        float y = box.Y;
        using (var sb = new SolidBrush(shadow)) g.DrawString(text, f, sb, x + shadowDx, y + shadowDy);
        using (var fb = new SolidBrush(fill))   g.DrawString(text, f, fb, x, y);
        if (f != font) f.Dispose();
    }

    public static void Render(string outPath, string fontDisplay, string fontBody, int W, int H)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;

            // --- water ---
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                                                    MapTop, MapBottom, LinearGradientMode.Vertical))
                g.FillRectangle(lg, 0, 0, W, H);

            // --- sun rays from above the top edge (GameFlow.DrawSunRays) ---
            var sun = new PointF(W * 0.5f, -H * 0.55f);
            for (int i = 0; i < 11; i++)
            {
                float a0 = -1.15f + i * 0.23f;
                float spread = 0.045f + (i % 3) * 0.012f;
                float len = H * 2.6f;
                var pts = new PointF[] {
                    sun,
                    new PointF(sun.X + (float)Math.Sin(a0 - spread) * len, sun.Y + (float)Math.Cos(a0 - spread) * len),
                    new PointF(sun.X + (float)Math.Sin(a0 + spread) * len, sun.Y + (float)Math.Cos(a0 + spread) * len)
                };
                using (var sb = new SolidBrush(Color.FromArgb(i % 2 == 0 ? 20 : 12, 255, 255, 255)))
                    g.FillPolygon(sb, pts);
            }

            // --- drifting bubbles (ReefBackdrop's risers) ---
            var rnd = new Random(4242);
            for (int i = 0; i < 34; i++)
            {
                float bx = (float)rnd.NextDouble() * W;
                float by = (float)rnd.NextDouble() * H;
                float br = 4f + (float)rnd.NextDouble() * 18f;
                int al = 16 + rnd.Next(26);
                using (var sb = new SolidBrush(Color.FromArgb(al, 255, 255, 255)))
                    g.FillEllipse(sb, bx - br, by - br, br * 2, br * 2);
                using (var pen = new Pen(Color.FromArgb(al + 22, 255, 255, 255), 1.5f))
                    g.DrawEllipse(pen, bx - br, by - br, br * 2, br * 2);
            }

            // --- sand shelf along the bottom ---
            using (var path = new GraphicsPath())
            {
                path.AddBezier(new PointF(-10, H - 46), new PointF(W * 0.28f, H - 78),
                               new PointF(W * 0.62f, H - 30), new PointF(W + 10, H - 66));
                path.AddLine(new PointF(W + 10, H - 66), new PointF(W + 10, H + 10));
                path.AddLine(new PointF(W + 10, H + 10), new PointF(-10, H + 10));
                path.CloseFigure();
                using (var sb = new SolidBrush(Color.FromArgb(255, 244, 226, 168)))
                    g.FillPath(sb, path);
                using (var sb = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    g.FillPath(sb, path);
            }

            // --- the bubble board: a shallow arc of orbs above the sand ------
            int[] cols = { 0, 4, 2, 5, 1, 3, 0, 2, 4 };
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float cx = 62f + t * (W - 124f);
                float lift = (float)Math.Sin(t * Math.PI) * 26f;
                float cy = H - 96f - lift;
                float r = 40f + (float)Math.Sin(t * Math.PI * 2f) * 5f;
                using (var orb = Orb((int)(r * 2f), Balls[cols[i]]))
                {
                    using (var sh = new SolidBrush(Color.FromArgb(46, 2, 40, 60)))
                        g.FillEllipse(sh, cx - r + 3, cy - r + 7, r * 2, r * 2);
                    g.DrawImage(orb, cx - r, cy - r, r * 2, r * 2);
                }
            }

            // --- three orbs mid-cascade, with fall streaks ------------------
            // Kept clear of the arc: an arc orb's top edge sits at ~338px, so a
            // faller's y + r must stay above that or it reads as a collision.
            float[][] fall = new float[][] {
                new float[] { W * 0.115f, H * 0.545f, 28f, 1 },
                new float[] { W * 0.930f, H * 0.505f, 30f, 5 },
                new float[] { W * 0.500f, H * 0.575f, 20f, 3 },
            };
            foreach (var f in fall)
            {
                float cx = f[0], cy = f[1], r = f[2];
                using (var lg = new LinearGradientBrush(
                           new RectangleF(cx - r * 0.55f, cy - r * 3.4f, r * 1.1f, r * 3.4f),
                           Color.FromArgb(0, 255, 255, 255), Color.FromArgb(70, 255, 255, 255),
                           LinearGradientMode.Vertical))
                    g.FillRectangle(lg, cx - r * 0.5f, cy - r * 3.4f, r, r * 3.4f);
                using (var orb = Orb((int)(r * 2f), Balls[(int)f[3]]))
                    g.DrawImage(orb, cx - r, cy - r, r * 2, r * 2);
            }

            // --- title + tagline, in the game's own fonts ------------------
            var pfc = new PrivateFontCollection();
            pfc.AddFontFile(fontDisplay);
            pfc.AddFontFile(fontBody);
            FontFamily famDisp = pfc.Families[0], famBody = pfc.Families[0];
            foreach (var fam in pfc.Families)
            {
                if (fam.Name.IndexOf("Narrow", StringComparison.OrdinalIgnoreCase) >= 0) famBody = fam;
                else famDisp = fam;
            }

            using (var title = new Font(famDisp, 92f, FontStyle.Regular, GraphicsUnit.Pixel))
                DrawFit(g, "CORAL CASCADE", title, famDisp,
                        new RectangleF(W * 0.06f, H * 0.20f, W * 0.88f, 120f),
                        Color.White, Color.FromArgb(150, DeepTeal.R, DeepTeal.G, DeepTeal.B), 4f, 5f);

            using (var tag = new Font(famBody, 34f, FontStyle.Regular, GraphicsUnit.Pixel))
                // \u00B7 as an escape, NOT a literal: the .ps1 is read as ANSI by
                // Windows PowerShell 5.1, which turned a literal middot into "Â\u00B7".
                DrawFit(g, "POP  \u00B7  CASCADE  \u00B7  GROW YOUR REEF", tag, famBody,
                        new RectangleF(W * 0.08f, H * 0.20f + 112f, W * 0.84f, 60f),
                        Color.FromArgb(255, 255, 246, 214),
                        Color.FromArgb(130, DeepTeal.R, DeepTeal.G, DeepTeal.B), 2f, 3f);

            // --- vignette ---
            using (var path = new GraphicsPath())
            {
                path.AddRectangle(new Rectangle(0, 0, W, H));
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(38, 2, 40, 60) };
                    pgb.CenterPoint = new PointF(W * 0.5f, H * 0.45f);
                    g.FillRectangle(pgb, 0, 0, W, H);
                }
            }
        }
        // Play REQUIRES the feature graphic as 24-bit PNG with NO alpha channel.
        // Saving the 32bppArgb surface directly writes an alpha channel and is
        // rejected on upload, so flatten onto an opaque 24bpp surface first.
        using (var flat = new Bitmap(W, H, PixelFormat.Format24bppRgb))
        {
            using (var fg = Graphics.FromImage(flat))
            {
                fg.CompositingQuality = CompositingQuality.HighQuality;
                fg.Clear(MapBottom);
                fg.DrawImage(bmp, 0, 0, W, H);
            }
            flat.Save(outPath, ImageFormat.Png);
        }
        bmp.Dispose();
    }

    public static void Resize(string inPath, string outPath, int size)
    {
        using (var src = new Bitmap(inPath))
        using (var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(src, 0, 0, size, size);
            }
            dst.Save(outPath, ImageFormat.Png);
        }
    }
}
'@

Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing -ErrorAction Stop

if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out | Out-Null }

$fd = Join-Path $Repo "Assets\Resources\Fonts\KenneyFuture.ttf"
$fb = Join-Path $Repo "Assets\Resources\Fonts\KenneyFutureNarrow.ttf"

[Feature]::Render((Join-Path $Out "feature_graphic.png"), $fd, $fb, 1024, 500)
[Feature]::Resize((Join-Path $Repo "Assets\Art\Icon\icon_full.png"), (Join-Path $Out "icon_512.png"), 512)

Get-ChildItem $Out | ForEach-Object { "{0}  {1} bytes" -f $_.Name, $_.Length }
