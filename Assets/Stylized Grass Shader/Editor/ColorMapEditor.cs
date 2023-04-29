using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if URP
using UnityEngine.Rendering.Universal;
#if !UNITY_2021_2_OR_NEWER
//Backwards compatibility
using UniversalRendererData = UnityEngine.Rendering.Universal.ForwardRendererData;
#endif
#endif

namespace StylizedGrass
{
    public class ColorMapEditor : Editor
    {
        private const float CLIP_PADDING = 3f;
        private const float HEIGHT_OFFSET = 2000f;

        //Scene lighting state
        private static List<Light> dirLights;
        private static Dictionary<Light, bool> originalLightStates = new Dictionary<Light, bool>();
        private static AmbientMode ambientMode;
        private static DefaultReflectionMode reflectionMode;
        private static bool fogEnabled;
        private static Color ambientColor;
        
        private static Material splatExtractionMat;
        private static Material unlitTerrainMat;
        
        #if URP
        private static UniversalRendererData rendererData;
        #endif
        private static Camera renderCam;

        //Get all terrains
        private static List<Terrain> terrains = new List<Terrain>();
        private struct TerrainState
        {
            public Terrain terrain;
            public float worldSpaceHeightPos;
            public Material material;
            public bool renderFoliage;
        }
        private static Dictionary<GameObject, TerrainState> originalTerrainStates = new Dictionary<GameObject, TerrainState>();

        public static string[] reslist = new string[] { "64x64", "128x128", "256x256", "512x512", "1024x1024", "2048x2048", "4096x4096" };

        public static GrassColorMap NewColorMap()
        {
            GrassColorMap colorMap = ScriptableObject.CreateInstance<GrassColorMap>();

            SetName(colorMap);

            return colorMap;
        }

        public static void RenderColorMap(GrassColorMapRenderer renderer)
        {
            if (!renderer.colorMap) renderer.colorMap = ScriptableObject.CreateInstance<GrassColorMap>();

            //If no area was defined, automatically calculate it
            if (renderer.colorMap.bounds.size == Vector3.zero)
            {
                ApplyUVFromTerrainBounds(renderer.colorMap, renderer);
            }
            else
            {
                renderer.colorMap.uv = BoundsToUV(renderer.colorMap.bounds);
            }

            renderer.colorMap.overrideTexture = false;
            
            terrains.Clear();
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;
                Terrain t = item.GetComponent<Terrain>();

                if (t) terrains.Add(t);
            }
            
            //Error: Trying to access current progress scope, but the scope stack is empty. UnityEditor.EditorUtility:CompressTexture
            //EditorUtility.DisplayProgressBar("Colormap Renderer", "Rendering...", 1f);

            SetupTerrains(renderer);
            SetupRenderer(renderer);
            SetupLighting(renderer);
            
            RenderToTexture(renderer);

            RestoreLighting(renderer);
            RestoreRenderer();
            RestoreTerrains(renderer);
            
            //EditorUtility.ClearProgressBar();

            renderer.colorMap.SetActive();
        }

        private enum Pass
        {
            IsolateChannel,
            MaxBlend,
            FillWhite,
            AlphaMerge
        }
        private static void GenerateScalemap(List<Terrain> terrains, GrassColorMapRenderer renderer, RenderTexture rgb)
        {
            if (terrains.Count == 0) return;

            if (renderer.layerScaleSettings.Count > 0)
            {
                splatExtractionMat = new Material(Shader.Find("Hidden/TerrainSplatmask"));
                
                //Temporarily override terrain material
                //Note: This is safe, the actual terrain material will be restored later on
                foreach (Terrain t in terrains) t.materialTemplate = splatExtractionMat;

                RenderTexture alphaBuffer = new RenderTexture(renderer.resolution, renderer.resolution, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
                RenderTexture scalemapBuffer = new RenderTexture(renderer.resolution, renderer.resolution, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
                //Alpha-weights combined makes up the scale map
                RenderTexture scalemap = new RenderTexture(renderer.resolution, renderer.resolution, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

                MaterialPropertyBlock props = new MaterialPropertyBlock();

                //Sort by strength
                //List<GrassColorMapRenderer.LayerScaleSettings> settings = renderer.layerScaleSettings.OrderByDescending(o => o.strength).ToList();

                int currentSplatIndex = 0;
                foreach (GrassColorMapRenderer.LayerScaleSettings layer in renderer.layerScaleSettings)
                {
                    int splatmapID = GetSplatmapID(layer.layerID);

                    Shader.SetGlobalVector("_SplatMask", ColorMapEditor.GetVectorMask(layer.layerID));
                    Shader.SetGlobalFloat("_SplatChannelStrength", layer.strength);

                    //Terrain render splatmap 0 by default, force to render next splatmap in base pass
                    if (splatmapID != currentSplatIndex)
                    {
                        //Debug.Log("layer.layerID requres splatmap switch to " + splatmapID);

                        foreach (Terrain t in terrains)
                        {
                            props.SetTexture("_Control", t.terrainData.GetAlphamapTexture(splatmapID));
                            t.SetSplatMaterialPropertyBlock(props);
                        }

                        currentSplatIndex = splatmapID;
                    }

                    //Render now visible alpha weight into buffer
                    renderCam.targetTexture = alphaBuffer;
                    renderCam.Render();

                    Shader.SetGlobalTexture("_InputAlphamap", alphaBuffer);
                    Shader.SetGlobalTexture("_InputHeightmap", scalemap);
                    
                    //Max blending copy here!
                    Graphics.Blit(alphaBuffer, scalemapBuffer, splatExtractionMat, (int)Pass.MaxBlend);
                    Graphics.Blit(scalemapBuffer, scalemap);
                }

                //Fill any black pixels with white (taking into account blank splatmap channels)
                Shader.SetGlobalTexture("_InputHeightmap", scalemapBuffer);
                Graphics.Blit(null, scalemap, splatExtractionMat, (int)Pass.FillWhite);
                
                //Restore
                foreach (Terrain t in terrains)
                {
                    t.SetSplatMaterialPropertyBlock(null);
                }

                //Add heightmap to alpha channel of rgb map
                Shader.SetGlobalTexture("_InputColormap", rgb);
                Shader.SetGlobalTexture("_InputHeightmap", scalemap);

                RenderTexture colorBuffer = new RenderTexture(rgb);
                Graphics.Blit(null, colorBuffer, splatExtractionMat, (int)Pass.AlphaMerge);

                Graphics.Blit(colorBuffer, rgb);

                renderer.colorMap.hasScalemap = true;
            }
            else
            {
                renderer.colorMap.hasScalemap = false;
            }
        }

        public static void SetName(GrassColorMap colorMap)
        {
            string prefix = EditorSceneManager.GetActiveScene().name;
            if (prefix == "") prefix = "Untitled";

#if UNITY_EDITOR
            colorMap.name = EditorSceneManager.GetActiveScene().name + "_GrassColormap";
            if (colorMap.texture != null) colorMap.texture.name = EditorSceneManager.GetActiveScene().name + "_GrassColormap";
#else
            colorMap.name = EditorSceneManager.GetActiveScene().name + "_GrassColormap";
            if (colorMap.texture != null) colorMap.texture.name = "GrassColorMap_" + colorMap.GetInstanceID();
#endif
        }

        public static GrassColorMap SaveColorMapToAsset(GrassColorMap colorMap)
        {
            string assetPath = "Assets/";
            assetPath = EditorUtility.SaveFolderPanel("Asset destination folder", assetPath, "");
            if (assetPath == string.Empty) return colorMap;

            assetPath = assetPath.Replace(Application.dataPath, "Assets");
            assetPath += "/" + colorMap.name + ".asset";
            Debug.Log("Saved to <i>" + assetPath + "</i>");

            AssetDatabase.CreateAsset(colorMap, assetPath);

            colorMap = (GrassColorMap)AssetDatabase.LoadAssetAtPath(assetPath, typeof(GrassColorMap));

            //Save texture to asset
            if (!colorMap.texture) colorMap.texture = new Texture2D(colorMap.resolution, colorMap.resolution);

            colorMap.texture.name = colorMap.name + " Texture";
            AssetDatabase.AddObjectToAsset(colorMap.texture, colorMap);
            string path = AssetDatabase.GetAssetPath(colorMap.texture);
            AssetDatabase.ImportAsset(path);

            //Reference serialized texture asset
            colorMap.texture = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));

            return colorMap;
        }

        public static double GetTexelSize(float texelSize, float worldSize)
        {
            return System.Math.Round(texelSize / worldSize, 2);
        }

        public static int IndexToResolution(int i)
        {
            int res = 0;

            switch (i)
            {
                case 0:
                    res = 64; break;
                case 1:
                    res = 128; break;
                case 2:
                    res = 256; break;
                case 3:
                    res = 512; break;
                case 4:
                    res = 1024; break;
                case 5:
                    res = 2048; break;
				case 6:
                    res = 4096; break;
            }

            return res;
        }

        public static int DetailPercentageToMipLevel(float percentage)
        {
            return Mathf.FloorToInt((100f - percentage) / 10);
        }

        public static void ApplyUVFromTerrainBounds(GrassColorMap colorMap, GrassColorMapRenderer renderer)
        {
            colorMap.bounds = ColorMapEditor.GetTerrainBounds(renderer.terrainObjects);
            colorMap.uv = ColorMapEditor.BoundsToUV(renderer.colorMap.bounds);
        }

        private static void SetupTerrains(GrassColorMapRenderer renderer)
        {
            originalTerrainStates.Clear();
            
            //Override the material with an Unlit variant
            if(!renderer.thirdPartyShader)
            {
                if(!unlitTerrainMat) unlitTerrainMat = new Material(Shader.Find("Hidden/TerrainAlbedo"));
            }
            
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;
                
                Terrain terrain = item.GetComponent<Terrain>();
                
                //Save original user state
                TerrainState state = new TerrainState
                {
                    terrain = terrain
                };

                if (terrain)
                {
                    state.material = terrain.materialTemplate;
                    state.worldSpaceHeightPos = terrain.transform.position.y;
                    state.renderFoliage = terrain.drawTreesAndFoliage;

                    //Disable, don't want these captured
                    terrain.drawTreesAndFoliage = false;
                }

                originalTerrainStates.Add(item, state);

                //Temporarily move everything up and out of the way
                item.transform.position = new Vector3(item.transform.position.x, item.transform.position.y + HEIGHT_OFFSET, item.transform.position.z);
                
                //Set up optimal rendering state
                if(!renderer.thirdPartyShader && terrain) terrain.materialTemplate = unlitTerrainMat;
            }
        }

        private static void SetupRenderer(GrassColorMapRenderer renderer)
        {
            if (!renderCam) renderCam = new GameObject().AddComponent<Camera>();
            
            renderCam.name = "Grass color map renderCam";
            renderCam.enabled = false;
            renderCam.hideFlags = HideFlags.HideAndDontSave;

            //Camera set up
            renderCam.orthographic = true;
            renderCam.orthographicSize = (renderer.colorMap.bounds.size.x / 2);
            renderCam.farClipPlane = renderer.colorMap.bounds.size.y + CLIP_PADDING;
            renderCam.clearFlags = CameraClearFlags.Color;
            renderCam.backgroundColor = Color.red;
            renderCam.cullingMask = renderer.cullingMask;

            //Position cam in given center of terrain(s)
            renderCam.transform.position = new Vector3(
                renderer.colorMap.bounds.center.x,
                renderer.colorMap.bounds.center.y + renderer.colorMap.bounds.extents.y + CLIP_PADDING + HEIGHT_OFFSET,
                renderer.colorMap.bounds.center.z
                );

            renderCam.transform.localEulerAngles = new Vector3(90, 0, 0);
            
#if URP
            UniversalAdditionalCameraData camData = renderCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camData.renderShadows = false;
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.requiresColorOption = CameraOverrideOption.Off;
            camData.requiresDepthOption = CameraOverrideOption.Off;
            camData.requiresColorTexture = false;
            camData.requiresDepthTexture = false;

            if (UniversalRenderPipeline.asset)
            {
                rendererData = CreateInstance<UniversalRendererData>();
                rendererData.postProcessData = null;
                rendererData.name = "Colormap Renderer";
                rendererData.hideFlags = HideFlags.DontSave;
                
                PipelineUtilities.ValidatePipelineRenderers(rendererData);
                PipelineUtilities.AssignRendererToCamera(camData, rendererData);
            }
            else
            {
                Debug.LogError("[StylizedGrassRenderer] No Universal Render Pipeline is currently active.");
            }
#endif
        }

        private static void SetupLighting(GrassColorMapRenderer renderer)
        {
            //If unable to use an unlit terrain material, set up scene lighting to closely represent plain albedo lighting
            if (!renderer.thirdPartyShader) return;
            
            //Setup faux albedo lighting
            var lights = FindObjectsOfType<Light>();
            dirLights = new List<Light>();
            originalLightStates.Clear();
            
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    //Save the original enabled state
                    originalLightStates.Add(light, light.enabled);
      
                    //Disable it, since it shouldn't influence the scene during rendering
                    light.enabled = false;
                    
                    dirLights.Add(light);
                }
            }

            ambientMode = RenderSettings.ambientMode;
            ambientColor = RenderSettings.ambientLight;
            reflectionMode = RenderSettings.defaultReflectionMode;
            fogEnabled = RenderSettings.fog;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;
        }

        private static void RenderToTexture(GrassColorMapRenderer renderer)
        {
            if (!renderCam)
            {
                Debug.LogError("Renderer does not have a render cam set up");
                return;
            }
            
            //Set up render texture
            RenderTexture rt = new RenderTexture(renderer.resolution, renderer.resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            renderCam.targetTexture = rt;
            RenderTexture.active = rt;
            
            Shader.SetGlobalInt("_ColormapMipLevel", DetailPercentageToMipLevel(renderer.textureDetail));

            //Render camera into a texture
            renderCam.Render();

            //Generate heightmap from terrain layers
            if (terrains.Count > 0) GenerateScalemap(terrains, renderer, rt);

            Graphics.SetRenderTarget(rt);
            Texture2D render = new Texture2D(renderer.resolution, renderer.resolution, TextureFormat.ARGB32, false, true);
            render.ReadPixels(new Rect(0, 0, renderer.resolution, renderer.resolution), 0, 0);
            render.Apply();
            
            //DTX5 not supported on mobile which Texture2D.Compress will use if the alpha channel is used (scale map)
            EditorUtility.CompressTexture(render, renderer.layerScaleSettings.Count > 0 ? TextureFormat.RGBA32 : TextureFormat.DXT1, TextureCompressionQuality.Normal);
            render.name = renderer.colorMap.name;

            if (!renderer.colorMap.texture) renderer.colorMap.texture = new Texture2D(renderer.colorMap.resolution, renderer.colorMap.resolution);

            //Saving texture
            if (EditorUtility.IsPersistent(renderer.colorMap))
            {
                //string texPath = AssetDatabase.GetAssetPath(renderer.colorMap.texture);
                //byte[] bytes = render.EncodeToPNG();
                //System.IO.File.WriteAllBytes(texPath, bytes);
                //AssetDatabase.ImportAsset(texPath, ImportAssetOptions.Default);
                //SaveTexture(render, renderer.colorMap);

                EditorUtility.CopySerialized(render, renderer.colorMap.texture);
                DestroyImmediate(render);
            }
            else
            {
                renderer.colorMap.texture = render;
            }

            SetName(renderer.colorMap);

            EditorUtility.SetDirty(renderer.colorMap);

            //Cleanup
            renderCam.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            
        }

        public static void RestoreLighting(GrassColorMapRenderer renderer)
        {
            if (!renderer.thirdPartyShader) return;
            
            //Restore previously enabled lights
            foreach (Light light in dirLights)
            {
                if (originalLightStates.TryGetValue(light, out var state))
                {
                    light.enabled = state;
                }
            }

            //Restore scene lighting
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.fog = fogEnabled;
        }

        private static void RestoreRenderer()
        {
            DestroyImmediate(renderCam.gameObject);
            renderCam = null;
            
#if URP
            PipelineUtilities.RemoveRendererFromPipeline(rendererData);
#endif
        }
        
        private static void RestoreTerrains(GrassColorMapRenderer renderer)
        {
            foreach (GameObject item in renderer.terrainObjects)
            {
                if (item == null) continue;

                //Get the original user state
                originalTerrainStates.TryGetValue(item, out var state);

                item.transform.position = new Vector3(item.transform.position.x, state.worldSpaceHeightPos, item.transform.position.z);

                if (state.terrain)
                {
                    //Re-enable if originally enabled
                    state.terrain.drawTreesAndFoliage = state.renderFoliage;
                    state.terrain.materialTemplate = state.material;
                }
            }
        }
        
        public static Bounds GetTerrainBounds(List<GameObject> terrainObjects)
        {
            Vector3 minSum = Vector3.one * Mathf.Infinity;
            Vector3 maxSum = Vector3.one * Mathf.NegativeInfinity;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            
            foreach (GameObject item in terrainObjects)
            {
                if (item == null) continue;

                Terrain t = item.GetComponent<Terrain>();
                MeshRenderer r = t ? null : item.GetComponent<MeshRenderer>();

                if (t)
                {
                    //Min/max bounds corners in world-space
                    min = t.GetPosition(); //Doesn't exactly represent the minimum bounds value, but doesn't have to be
                    max = t.GetPosition() + t.terrainData.size; //Note, size is slightly more correct in height than bounds
                }

                if (r)
                {
                    //World-space bounds corners
                    min = r.bounds.min;
                    max = r.bounds.max;
                }
                
                minSum = Vector3.Min(minSum, min);
                
                //Must handle each axis separately, terrain may be further away, but not necessarily higher
                maxSum.x = Mathf.Max(maxSum.x, max.x);
                maxSum.y = Mathf.Max(maxSum.y, max.y);
                maxSum.z = Mathf.Max(maxSum.z, max.z);
            }

            Bounds b = new Bounds(Vector3.zero, Vector3.zero);

            b.SetMinMax(minSum, maxSum);

            //Increase bounds height for flat terrains
            if (b.size.y < 2f)
            {
                b.Encapsulate(new Vector3(b.center.x, b.center.y + 1f, b.center.z));
                b.Encapsulate(new Vector3(b.center.x, b.center.y - 1f, b.center.z));
            }

            //Ensure bounds is always square
            b.size = new Vector3(Mathf.Max(b.size.x, b.size.z), b.size.y, Mathf.Max(b.size.x, b.size.z));
            b.center = Vector3.Lerp(b.min, b.max, 0.5f);

            return b;
        }

        public static Vector4 BoundsToUV(Bounds b)
        {
            Vector4 uv = new Vector4();

            //Origin position
            uv.x = b.min.x;
            uv.y = b.min.z;
            //Scale factor
            uv.z = 1f / b.size.x;
            uv.w = 0f;

            return uv;
        }

        //Create an RGBA component mask (eg. i=2 samples the Blue channel)
        public static Vector4 GetVectorMask(int i)
        {
            int index = i % 4;
            switch (index)
            {
                case 0: return new Vector4(1, 0, 0, 0);
                case 1: return new Vector4(0, 1, 0, 0);
                case 2: return new Vector4(0, 0, 1, 0);
                case 3: return new Vector4(0, 0, 0, 1);

                default: return Vector4.zero;
            }
        }

        //Returns the splatmap index for a given terrain layer
        public static int GetSplatmapID(int layerID)
        {
            if (layerID > 11) return 3;
            if (layerID > 7) return 2;
            if (layerID > 3) return 1;

            return 0;
        }
    }
}