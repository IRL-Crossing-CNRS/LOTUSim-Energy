Shader "Custom/ScanBeamGrid"
{
    Properties
    {
        _GridColor ("Grid Line Color", Color) = (0.3, 1, 0.9, 1)
        _BaseAlpha ("Fill Alpha", Range(0,1)) = 0.08
        _LineAlpha ("Line Alpha", Range(0,1)) = 0.6
        _CellSize ("Cell Size", Float) = 8
        _LineWidth ("Line Width", Range(0.001, 0.2)) = 0.03
        _ScanSpeed ("Scanline Speed", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _GridColor;
            float _BaseAlpha;
            float _LineAlpha;
            float _CellSize;
            float _LineWidth;
            float _ScanSpeed;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 grid = frac(i.uv * _CellSize);
                float lineX = step(grid.x, _LineWidth) + step(1 - _LineWidth, grid.x);
                float lineY = step(grid.y, _LineWidth) + step(1 - _LineWidth, grid.y);
                float lines = saturate(lineX + lineY);

                // moving scanline band
                float scan = frac(i.uv.y - _Time.y * _ScanSpeed);
                float scanBand = smoothstep(0.0, 0.05, scan) * smoothstep(0.15, 0.05, scan);

                float alpha = _BaseAlpha + lines * _LineAlpha + scanBand * 0.3;
                return float4(_GridColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}