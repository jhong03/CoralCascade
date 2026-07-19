using UnityEngine;

namespace CoralCascade
{
    /// <summary>
    /// Display cutout / gesture-bar insets, expressed the way IMGUI needs them.
    ///
    /// COORDINATE TRAP: <see cref="Screen.safeArea"/> is in screen pixels with the origin at
    /// the BOTTOM-left and y increasing upward; IMGUI's origin is the TOP-left with y
    /// increasing downward. So the GUI-space TOP inset is measured from safeArea.yMax, and
    /// the BOTTOM inset is safeArea.y. Getting these two the wrong way round puts the HUD
    /// under the notch on exactly the devices you were trying to protect.
    ///
    /// Values are pixels, refreshed only when the screen or safe area actually changes, so
    /// this is safe to call from OnGUI (which runs several times per frame).
    /// </summary>
    public static class SafeAreaUtil
    {
        public static float Left { get; private set; }
        public static float Right { get; private set; }
        public static float Top { get; private set; }
        public static float Bottom { get; private set; }

        private static Rect _cachedArea;
        private static int _cachedW = -1, _cachedH = -1;

        /// <summary>The drawable rect in GUI space (origin top-left).</summary>
        public static Rect Content
        {
            get
            {
                Refresh();
                return new Rect(Left, Top,
                                Mathf.Max(1f, Screen.width - Left - Right),
                                Mathf.Max(1f, Screen.height - Top - Bottom));
            }
        }

        public static void Refresh()
        {
            Rect area = Screen.safeArea;
            if (_cachedW == Screen.width && _cachedH == Screen.height && _cachedArea == area) return;
            _cachedW = Screen.width;
            _cachedH = Screen.height;
            _cachedArea = area;

            // Guard against the degenerate values some devices/editor states report.
            if (area.width <= 0f || area.height <= 0f)
            {
                Left = Right = Top = Bottom = 0f;
                return;
            }

            Left = Mathf.Max(0f, area.x);
            Right = Mathf.Max(0f, Screen.width - area.xMax);
            Top = Mathf.Max(0f, Screen.height - area.yMax); // <- from the TOP of the screen
            Bottom = Mathf.Max(0f, area.y);
        }
    }
}
