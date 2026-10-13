using UnityEngine;

namespace Lotusim
{
    public static class UISpriteFactory
    {
        /// <summary>
        /// Generates a rounded-rectangle sprite suitable for 9-slicing on an Image component.
        /// </summary>
        /// <param name="radius">Corner radius in pixels.</param>
        /// <param name="size">Texture size (square). Keep small — it's just a border reference for slicing.</param>
        public static Sprite CreateRoundedRectSprite(int radius = 24, int size = 64, Color? color = null)
        {
            Color c = color ?? Color.white;
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2(size / 2f, size / 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = InsideRoundedRect(x, y, size, radius) ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * alpha));
                }
            }
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static bool InsideRoundedRect(int x, int y, int size, int radius)
        {
            int minX = radius, maxX = size - radius;
            int minY = radius, maxY = size - radius;

            float cx = Mathf.Clamp(x, minX, maxX);
            float cy = Mathf.Clamp(y, minY, maxY);

            float dx = x - cx;
            float dy = y - cy;

            return (dx * dx + dy * dy) <= radius * radius;
        }
    }
}