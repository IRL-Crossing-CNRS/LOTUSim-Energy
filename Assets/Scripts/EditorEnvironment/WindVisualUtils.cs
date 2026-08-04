/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  WindVisualUtils.cs
//
//  Description:
//  Small shared helpers for the wind visualization scripts (WindArrowFieldRenderer,
//  WindFieldVisualizer, WindRegionVisualizer) so unlit-transparent material setup and color
//  assignment aren't duplicated across each of them.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace Lotusim
{
    public static class WindVisualUtils
    {
        /// <summary>
        /// Configures an HDRP/Unlit (or Unlit/Color fallback) material for alpha-blended,
        /// double-sided rendering — used by every translucent wind visual (arrows, zone
        /// perimeter, zone floor) so they blend correctly instead of clipping their own alpha.
        /// </summary>
        public static void ConfigureTransparent(Material mat, Color initialColor)
        {
            if (mat == null) return;

            if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f); // 0 = Opaque, 1 = Transparent
            if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f);     // Alpha blend
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);       // 0 = double-sided
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;

            SetColor(mat, initialColor);
        }

        /// <summary>
        /// Sets the material's main color across every property name Unity's built-in and SRP
        /// unlit shaders might use for it. HDRP/Unlit's [MainColor] remap normally makes
        /// `.color` do the right thing on its own, but setting the known aliases directly is
        /// cheap insurance against pipeline/shader variations.
        /// </summary>
        public static void SetColor(Material mat, Color color)
        {
            if (mat == null) return;

            mat.color = color;
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        }
    }
}
