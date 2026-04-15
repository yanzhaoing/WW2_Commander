Shader "Hidden/ScreenNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseIntensity ("Noise Intensity", Range(0, 0.5)) = 0.08
        _ScanlineIntensity ("Scanline Intensity", Range(0, 0.2)) = 0.04
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.35
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.05)) = 0.0
        _TrackingLineActive ("Tracking Line Active", Range(0, 1)) = 0.0
        _TrackingLineIntensity ("Tracking Line Intensity", Range(0, 0.5)) = 0.15
        _TrackingLineOffset ("Tracking Line Offset", Range(0, 1)) = 0.0
        _FilmGrainIntensity ("Film Grain Intensity", Range(0, 0.1)) = 0.03
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float _NoiseIntensity;
            float _ScanlineIntensity;
            float _VignetteIntensity;
            float _ChromaticAberration;
            float _TrackingLineActive;
            float _TrackingLineIntensity;
            float _TrackingLineOffset;
            float _FilmGrainIntensity;
            float _TimeY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 伪随机
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 改进的噪声
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 色差偏移 — RGB 分离
                float3 col;
                if (_ChromaticAberration > 0.001)
                {
                    float2 rOffset = float2(_ChromaticAberration, 0.0);
                    float2 bOffset = float2(-_ChromaticAberration, 0.0);
                    col.r = tex2D(_MainTex, uv + rOffset).r;
                    col.g = tex2D(_MainTex, uv).g;
                    col.b = tex2D(_MainTex, uv + bOffset).b;
                }
                else
                {
                    col = tex2D(_MainTex, uv).rgb;
                }

                // 噪点 (时变)
                float n = hash(uv * _Time.y * 100.0) * _NoiseIntensity;
                col.rgb += n - _NoiseIntensity * 0.5;

                // 扫描线 (CRT)
                float scanline = sin(uv.y * 800.0) * 0.5 + 0.5;
                col.rgb -= scanline * _ScanlineIntensity;

                // 暗角
                float2 center = uv - 0.5;
                float vignette = 1.0 - dot(center, center) * _VignetteIntensity * 2.0;
                col.rgb *= saturate(vignette);

                // VHS 追踪线
                if (_TrackingLineActive > 0.5)
                {
                    float trackY = frac(_TrackingLineOffset);
                    float trackDist = abs(uv.y - trackY);
                    if (trackDist < 0.02)
                    {
                        float trackFade = 1.0 - trackDist / 0.02;
                        float trackNoise = hash(float2(uv.x * 50.0, _Time.y * 10.0));
                        col.rgb += trackNoise * trackFade * _TrackingLineIntensity;

                        // 追踪线区域水平偏移 (扭曲效果)
                        float distort = sin(_Time.y * 20.0 + uv.y * 100.0) * 0.002 * trackFade;
                        float3 distortCol = tex2D(_MainTex, uv + float2(distort, 0.0)).rgb;
                        col.rgb = lerp(col.rgb, distortCol, trackFade * 0.5);
                    }
                }

                // 胶片颗粒
                if (_FilmGrainIntensity > 0.001)
                {
                    float grain = noise(uv * 500.0 + _Time.y * 50.0) * 2.0 - 1.0;
                    col.rgb += grain * _FilmGrainIntensity;
                }

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
}
