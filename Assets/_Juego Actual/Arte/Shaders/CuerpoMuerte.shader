// Copia congelada del cuerpo de un personaje al morir. Se ilumina como URP Lit para que el
// cambio no se note y anade lo que necesitan los efectos de muerte: disolucion con borde
// brillante, carbonizado con brasas, grietas que se abren desde el golpe y un destello.
// EfectoMuerte crea las instancias de este material y anima sus valores desde codigo.
Shader "UdeM/Efectos/CuerpoMuerte"
{
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metalico", Range(0, 1)) = 0
        _Smoothness ("Suavidad", Range(0, 1)) = 0.5
        [HideInInspector] _Cull ("Cull", Float) = 2

        [Header(Disolucion)]
        _Progreso ("Progreso (lo que tenga un campo menor desaparece)", Float) = -1
        _AnchoBorde ("Ancho del borde", Range(0.001, 0.3)) = 0.06
        [HDR] _ColorBorde ("Color del borde", Color) = (4, 1.6, 0.4, 1)
        _EscalaRuido ("Escala del ruido", Float) = 5
        _PesoAltura ("Peso de la altura frente al ruido", Range(0, 1)) = 0.6
        _SentidoAltura ("1 = de arriba hacia abajo, 0 = de abajo hacia arriba", Range(0, 1)) = 1
        _AlturaMin ("Altura minima del cuerpo", Float) = 0
        _AlturaMax ("Altura maxima del cuerpo", Float) = 2
        _OrigenRuido ("Desplazamiento del cuerpo (para que el ruido no resbale)", Vector) = (0, 0, 0, 0)

        [Header(Quemadura)]
        _Quemado ("Quemado de todo el cuerpo", Range(0, 1)) = 0
        _AnchoQuemado ("Franja quemada junto al borde (0 = sin franja)", Range(0, 1)) = 0
        _ColorQuemado ("Color calcinado", Color) = (0.05, 0.04, 0.035, 1)
        _Brasas ("Brasas", Range(0, 4)) = 0
        [HDR] _ColorBrasas ("Color de las brasas", Color) = (3, 0.9, 0.2, 1)

        [Header(Grietas)]
        _EscalaGrietas ("Escala de las grietas", Float) = 6
        _AnchoGrieta ("Ancho de la grieta", Range(0.005, 0.3)) = 0.06
        _ColorGrietas ("Color de las grietas", Color) = (0.08, 0.07, 0.06, 1)
        [HDR] _BrilloGrietas ("Brillo dentro de las grietas", Color) = (0, 0, 0, 1)
        _PuntoImpacto ("Origen de las grietas (xyz) y alcance (w)", Vector) = (0, 0, 0, 0)

        [Header(Destello)]
        _Destello ("Destello", Range(0, 1)) = 0
        [HDR] _ColorDestello ("Color del destello", Color) = (1, 1, 1, 1)
    }

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Metallic;
            half _Smoothness;
            half _Cull;
            float _Progreso;
            float _AnchoBorde;
            half4 _ColorBorde;
            float _EscalaRuido;
            float _PesoAltura;
            float _SentidoAltura;
            float _AlturaMin;
            float _AlturaMax;
            float4 _OrigenRuido;
            half _Quemado;
            float _AnchoQuemado;
            half4 _ColorQuemado;
            half _Brasas;
            half4 _ColorBrasas;
            float _EscalaGrietas;
            float _AnchoGrieta;
            half4 _ColorGrietas;
            half4 _BrilloGrietas;
            float4 _PuntoImpacto;
            half _Destello;
            half4 _ColorDestello;
        CBUFFER_END

        // El ruido y el campo de disolucion estan replicados en CuerpoMuerte.cs para que las
        // particulas salgan justo del punto que desaparece. Si cambias algo aqui, cambialo alla.
        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float RuidoValor(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            float3 u = f * f * (3.0 - 2.0 * f);

            float x00 = lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), u.x);
            float x10 = lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), u.x);
            float x01 = lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), u.x);
            float x11 = lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), u.x);
            return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
        }

        float RuidoDisolucion(float3 p)
        {
            float n = RuidoValor(p) * 0.65 + RuidoValor(p * 2.17 + 19.1) * 0.35;
            return saturate((n - 0.5) * 2.2 + 0.5);
        }

        float3 PosicionCuerpo(float3 positionWS)
        {
            return positionWS - _OrigenRuido.xyz;
        }

        float CampoDisolucion(float3 p)
        {
            float altura = saturate((p.y - _AlturaMin) / max(0.001, _AlturaMax - _AlturaMin));
            float campoAltura = lerp(altura, 1.0 - altura, _SentidoAltura);
            return lerp(RuidoDisolucion(p * _EscalaRuido), campoAltura, _PesoAltura);
        }

        // Devuelve cuanto le falta a este punto para desaparecer y lo recorta si ya paso.
        float Recortar(float3 positionWS)
        {
            float distancia = CampoDisolucion(PosicionCuerpo(positionWS)) - _Progreso;
            clip(distancia);
            return distancia;
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 nieblaYLuz : TEXCOORD3; // x: niebla, yzw: luces adicionales por vertice
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 4);
            #ifdef USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion : TEXCOORD5;
            #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posicion = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normales = GetVertexNormalInputs(input.normalOS);

                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.positionWS = posicion.positionWS;
                output.normalWS = normales.normalWS;
                output.positionCS = posicion.positionCS;

                half niebla = ComputeFogFactor(posicion.positionCS.z);
                half3 luzVertice = VertexLighting(posicion.positionWS, normales.normalWS);
                output.nieblaYLuz = half4(niebla, luzVertice);

                OUTPUT_SH4(posicion.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(posicion.positionWS), output.vertexSH, output.probeOcclusion);
                return output;
            }

            void frag(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float distancia = Recortar(input.positionWS);
                float3 p = PosicionCuerpo(input.positionWS);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Carbonizado: todo el cuerpo con _Quemado y una franja que avanza delante del borde.
                half franja = _AnchoQuemado > 0.0001 ? 1.0 - saturate(distancia / _AnchoQuemado) : 0.0;
                half quemado = saturate(max(_Quemado, franja));
                albedo = lerp(albedo, _ColorQuemado.rgb, quemado);

                // Grietas de celdas que se abren en circulo desde el punto del golpe.
                half grieta = 0.0;
                if (_PuntoImpacto.w > 0.0001)
                {
                    float3 celda = floor(p * _EscalaGrietas);
                    float3 local = frac(p * _EscalaGrietas);
                    float d1 = 8.0;
                    float d2 = 8.0;
                    for (int z = -1; z <= 1; z++)
                    for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        float3 vecina = float3(x, y, z);
                        float3 semilla = float3(Hash13(celda + vecina), Hash13(celda + vecina + 17.3), Hash13(celda + vecina + 41.7));
                        float3 r = vecina + semilla - local;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                    float borde = sqrt(d2) - sqrt(d1);
                    float linea = 1.0 - smoothstep(_AnchoGrieta * 0.4, _AnchoGrieta, borde);
                    float alcance = saturate((_PuntoImpacto.w - distance(p, _PuntoImpacto.xyz)) * 6.0);
                    grieta = linea * alcance;
                    albedo = lerp(albedo, _ColorGrietas.rgb, grieta);
                }

                half3 emision = 0;

                half brilloBorde = 1.0 - saturate(distancia / _AnchoBorde);
                emision += _ColorBorde.rgb * brilloBorde * brilloBorde;

                if (_Brasas > 0.0001)
                {
                    float vetas = RuidoValor(p * _EscalaRuido * 2.6 + float3(0.0, -_Time.y * 0.8, 0.0));
                    half brasa = smoothstep(0.55, 0.8, vetas) * quemado;
                    half parpadeo = 0.7 + 0.3 * sin(_Time.y * 13.0 + vetas * 23.0);
                    emision += _ColorBrasas.rgb * brasa * _Brasas * parpadeo;
                }

                emision += _BrilloGrietas.rgb * grieta;
                emision += _ColorDestello.rgb * _Destello;

                SurfaceData superficie = (SurfaceData)0;
                superficie.albedo = albedo;
                superficie.metallic = _Metallic;
                superficie.specular = half3(0.0, 0.0, 0.0);
                superficie.smoothness = lerp(_Smoothness, 0.0, quemado);
                superficie.normalTS = half3(0.0, 0.0, 1.0);
                superficie.emission = emision;
                superficie.occlusion = 1.0;
                superficie.alpha = 1.0;

                InputData datos = (InputData)0;
                datos.positionWS = input.positionWS;
                datos.positionCS = input.positionCS;
                datos.normalWS = NormalizeNormalPerPixel(input.normalWS);
                datos.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                datos.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                datos.shadowCoord = float4(0, 0, 0, 0);
            #endif
                datos.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.nieblaYLuz.x);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                datos.vertexLighting = input.nieblaYLuz.yzw;
            #endif
                datos.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

            #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
                datos.bakedGI = SAMPLE_GI(input.vertexSH,
                    GetAbsolutePositionWS(datos.positionWS),
                    datos.normalWS,
                    datos.viewDirectionWS,
                    input.positionCS.xy,
                    input.probeOcclusion,
                    datos.shadowMask);
            #else
                datos.bakedGI = SAMPLE_GI(0, input.vertexSH, datos.normalWS);
                datos.shadowMask = SAMPLE_SHADOWMASK(0);
            #endif

                half4 color = UniversalFragmentPBR(datos, superficie);
                color.rgb = MixFog(color.rgb, datos.fogCoord);
                outColor = half4(color.rgb, 1.0);

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertSombra
            #pragma fragment fragSombra
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertSombra(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direccionLuz = normalize(_LightPosition - positionWS);
            #else
                float3 direccionLuz = _LightDirection;
            #endif
                output.positionWS = positionWS;
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direccionLuz)));
                return output;
            }

            half4 fragSombra(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Recortar(input.positionWS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertProfundidad
            #pragma fragment fragProfundidad
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertProfundidad(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half fragProfundidad(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                Recortar(input.positionWS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertNormales
            #pragma fragment fragNormales
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vertNormales(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            void fragNormales(
                Varyings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                Recortar(input.positionWS);
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
}
