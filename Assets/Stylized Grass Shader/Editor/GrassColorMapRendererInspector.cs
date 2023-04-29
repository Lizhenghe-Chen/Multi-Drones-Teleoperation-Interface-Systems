using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace StylizedGrass
{
    [CustomEditor(typeof(GrassColorMapRenderer))]
    public class GrassColorMapRendererInspector : Editor
    {
        GrassColorMapRenderer script;
        SerializedProperty colorMap;
        SerializedProperty resIdx;
        SerializedProperty resolution;
        SerializedProperty cullingMask;
        SerializedProperty textureDetail;
        private SerializedProperty thirdPartyShader;
        SerializedProperty terrainObjects;

        private TerrainLayer[] terrainLayers;
        private GUIContent[] layerNames;

        private static string iconPrefix => EditorGUIUtility.isProSkin ? "d_" : "";
        private static GUIContent RenderButtonContent;
        
        private void OnEnable()
        {
            script = (GrassColorMapRenderer)target;

            colorMap = serializedObject.FindProperty("colorMap");
            resIdx = serializedObject.FindProperty("resIdx");
            resolution = serializedObject.FindProperty("resolution");
            cullingMask = serializedObject.FindProperty("cullingMask");
            textureDetail = serializedObject.FindProperty("textureDetail");
            thirdPartyShader = serializedObject.FindProperty("thirdPartyShader");
            terrainObjects = serializedObject.FindProperty("terrainObjects");

            if (!script.colorMap) script.colorMap = ColorMapEditor.NewColorMap();

            RefreshTerrainLayers();

            if (terrainObjects.arraySize == 0) terrainObjects.isExpanded = true;
            
            RenderButtonContent  = new GUIContent("  Render", EditorGUIUtility.IconContent(iconPrefix + "Animation.Record").image);
        }

        bool editingCollider
        {
            get { return EditMode.editMode == EditMode.SceneViewEditMode.Collider && EditMode.IsOwner(this); }
        }

        static Color s_HandleColor = new Color(127f, 214f, 244f, 100f) / 255;
        static Color s_HandleColorSelected = new Color(127f, 214f, 244f, 210f) / 255;
        static Color s_HandleColorDisabled = new Color(127f * 0.75f, 214f * 0.75f, 244f * 0.75f, 100f) / 255;
        BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();
        

        Bounds GetBounds()
        {
            return script.colorMap.bounds;
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Version " + AssetInfo.INSTALLED_VERSION, EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(5);

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(colorMap);
                
                if (GUILayout.Button(new GUIContent(" New", EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_editicon.sml" : "editicon.sml").image), GUILayout.MaxWidth(70f)))
                {
                    colorMap.objectReferenceValue = ColorMapEditor.NewColorMap();
                }
            }

            if (!colorMap.objectReferenceValue)
            {
                EditorGUILayout.HelpBox("No color map assigned", MessageType.Error);
                return;
            }

            if (colorMap.objectReferenceValue)
            {
                //EditorGUILayout.LabelField(string.Format("Area size: {0}x{1}", script.colorMap.bounds.size.x, script.colorMap.bounds.size.z));

                if (EditorUtility.IsPersistent(script.colorMap) == false)
                {
                    Action saveColorMap = new Action(SaveColorMap);
                    StylizedGrassGUI.DrawActionBox("  The color map asset has not been saved to a file\n  and can only be used in this scene", "Save", MessageType.Warning, saveColorMap);
                }

                if (script.colorMap.overrideTexture)
                {
                    EditorGUILayout.HelpBox("The assigned color map uses a texture override. Rendering a new/updated color map will revert this.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Terrain(s)", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(terrainObjects, GUIContent.none);

            if (terrainObjects.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Assign the target terrain objects to the list above to begin", MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent(" Quick actions", EditorGUIUtility.IconContent("Settings").image)))
                {
                    GenericMenu menu = new GenericMenu();
                    
                    menu.AddItem(new GUIContent("Add active terrains (" + Terrain.activeTerrains.Length + ")"), false, () =>
                    {
                        AssignActiveTerrains();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    menu.AddItem(new GUIContent("Add child meshes"), false, () =>
                    {
                        script.AssignChildMeshes();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });

                    #if VEGETATION_STUDIO_PRO
                    menu.AddItem(new GUIContent("Add VSP mesh terrains"), false, () =>
                    {
                        script.AssignVegetationStudioMeshTerrains();
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                    });
                    #endif
                    
                    menu.AddSeparator(string.Empty);
                    
                    menu.AddItem(new GUIContent("Clear list"), false, () =>
                    {
                        terrainObjects.ClearArray();
                        serializedObject.ApplyModifiedProperties();
                    });
                    
                    menu.ShowAsContext();
                }
            }
            
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Render area", EditorStyles.boldLabel);

            EditMode.DoEditModeInspectorModeButton(EditMode.SceneViewEditMode.Collider, "Edit Volume", EditorGUIUtility.IconContent("EditCollider"), GetBounds, this);
            script.colorMap.bounds.size = EditorGUILayout.Vector3Field("Size", script.colorMap.bounds.size);
            script.colorMap.bounds.center = EditorGUILayout.Vector3Field("Center", script.colorMap.bounds.center);
            
            if (script.colorMap.bounds.size == Vector3.zero && terrainObjects.arraySize == 0) EditorGUILayout.HelpBox("The render area cannot be zero", MessageType.Error);
            if (script.colorMap.bounds.size == Vector3.zero && terrainObjects.arraySize > 0) EditorGUILayout.HelpBox("The render area will be automatically calculate based on terrain size", MessageType.Info);

            using (new EditorGUI.DisabledGroupScope(script.terrainObjects.Count == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Calculate from terrain(s)"))
                    {
                        ColorMapEditor.ApplyUVFromTerrainBounds(colorMap.objectReferenceValue as GrassColorMap, script);
                        EditorUtility.SetDirty(target);

                        SceneView.RepaintAll();
                    }
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Layer-based grass scale", EditorStyles.boldLabel);
            
            DrawLayerHeightSettings();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.PropertyField(cullingMask);

                if (cullingMask.intValue == 0) EditorGUILayout.HelpBox("The render layer is set to \"Nothing\", no objects will be rendered into the color map", MessageType.Error);

                EditorGUILayout.PropertyField(thirdPartyShader, new GUIContent("Using custom terrain shader", thirdPartyShader.tooltip));
                EditorGUILayout.PropertyField(textureDetail);
                //EditorGUILayout.LabelField("Mip map level: " + ColorMapEditor.DetailPercentageToMipLevel(textureDetail.floatValue), EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    resIdx.intValue = EditorGUILayout.Popup("Resolution", resIdx.intValue, ColorMapEditor.reslist, GUILayout.MaxWidth(EditorGUIUtility.labelWidth + 100f));
                }
                
                EditorGUILayout.Space();

                if (GUILayout.Button(RenderButtonContent, GUILayout.Height(30f)))
                {
                    ColorMapEditor.RenderColorMap((GrassColorMapRenderer)target);
                }

            }

            if (EditorGUI.EndChangeCheck())
            {
                resolution.intValue = ColorMapEditor.IndexToResolution(resIdx.intValue);
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.LabelField("- Staggart Creations -", EditorStyles.centeredGreyMiniLabel);
        }

        private void AssignActiveTerrains()
        {
            script.AssignActiveTerrains();
            
            RefreshTerrainLayers();
        }

        private void DrawLayerHeightSettings()
        {
            if (layerNames == null)
            {
                EditorGUILayout.HelpBox("This feature only works with Unity terrains (The first item in the Terrain Objects list isn't a terrain)", MessageType.Info);
                return;
            }

            if (script.layerScaleSettings != null && script.layerScaleSettings.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUI.BeginChangeCheck();

                    for (int i = 0; i < script.layerScaleSettings.Count; i++)
                    {
                        GrassColorMapRenderer.LayerScaleSettings s = script.layerScaleSettings[i];

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            s.layerID = EditorGUILayout.Popup(s.layerID, layerNames, GUILayout.MaxWidth(150f));
                            float strength = s.strength * 100f;
                            strength = EditorGUILayout.Slider(strength, 1f, 100f);
                            s.strength = strength * 0.01f;
                            EditorGUILayout.LabelField("%", GUILayout.MaxWidth(17f));

                            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent(iconPrefix + "TreeEditor.Trash").image, "Delete item")))
                            {
                                script.layerScaleSettings.RemoveAt(i);
                            }

                        }
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        ColorMapEditor.RenderColorMap((GrassColorMapRenderer)target);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No scale settings for terrain layers, grass will stay a uniform scale", MessageType.None);
            }

            using (new EditorGUI.DisabledScope(script.layerScaleSettings.Count == terrainLayers.Length))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Add layer setting", EditorGUIUtility.IconContent(iconPrefix + "Toolbar Plus").image)))
                    {
                        if (script.terrainObjects.Count == 0)
                        {
                            Debug.LogError("No terrains assigned");
                            return;
                        }
                        
                        GenericMenu menu = new GenericMenu();
                        for (int i = 0; i < terrainLayers.Length; i++)
                        {
                            if(terrainLayers[i] == null) continue;
                
                            //Check if layer already added
                            if (script.layerScaleSettings.Find(x => x.layerID == i) == null)
                                menu.AddItem(new GUIContent(terrainLayers[i].name), false, AddTerrainLayerMask, i);
                        }
                        menu.ShowAsContext();
                        
                       
                    }
                }
            }

        }
        
        private void AddTerrainLayerMask(object id)
        {
            GrassColorMapRenderer.LayerScaleSettings s = new GrassColorMapRenderer.LayerScaleSettings();
            s.layerID = (int)id;
            
            script.layerScaleSettings.Add(s);

            ColorMapEditor.RenderColorMap((GrassColorMapRenderer)target);
        }

        private void RefreshTerrainLayers()
        {
            if (script.terrainObjects.Count == 0)
            {
                layerNames = null;
                return;
            }

            Terrain t = script.terrainObjects[0].GetComponent<Terrain>();

            if (t == null)
            {
                layerNames = null;
                return;
            }


            terrainLayers = t.terrainData.terrainLayers;
            
            layerNames = new GUIContent[terrainLayers.Length];
            for (int i = 0; i < layerNames.Length; i++)
            {
                layerNames[i] = new GUIContent(t.terrainData.terrainLayers[i] ? t.terrainData.terrainLayers[i].name : "(Missing)");
            }

        }

        public override bool HasPreviewGUI()
        {
            if (script.colorMap == null) return false;
            if (script.colorMap.texture == null) return false;

            return script.colorMap.texture == true;
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("Color/scale map");
        }

        public override void OnPreviewSettings()
        {
            if (script.colorMap.texture == false) return;

            GUILayout.Label(string.Format("Output ({0}x{0})", script.colorMap.texture.height));
        }

        private bool previewColor = true;
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (script.colorMap.texture == null) return;

            if (previewColor)
            {
                GUI.DrawTexture(r, script.colorMap.texture, ScaleMode.ScaleToFit, false);
            }
            else
            {
                EditorGUI.DrawTextureAlpha(r, script.colorMap.texture, ScaleMode.ScaleToFit);
            }

            Rect btnRect = r;
            btnRect.x += 10f;
            btnRect.y += 10f;
            btnRect.width = 50f;
            btnRect.height = 20f;

            previewColor = GUI.Toggle(btnRect, previewColor, new GUIContent("Color"), "Button");
            btnRect.x += 49f;
            previewColor = !GUI.Toggle(btnRect, !previewColor, new GUIContent("Scale"), "Button");
            
            GUI.Label(new Rect(r.width * 0.5f - (175 * 0.5f), r.height - 5, 175, 25), string.Format("{0} texel(s) per unit", ColorMapEditor.GetTexelSize(script.colorMap.texture.height, script.colorMap.bounds.size.x)), EditorStyles.toolbarButton);
        }

        private void SaveColorMap()
        {
            ColorMapEditor.SaveColorMapToAsset(colorMap.objectReferenceValue as GrassColorMap);
        }

        void OnSceneGUI()
        {

            if (!editingCollider || script.colorMap == null)
                return;

            Bounds bounds = script.colorMap.bounds;
            Color color = script.enabled ? s_HandleColor : s_HandleColorDisabled;
            using (new Handles.DrawingScope(color, Matrix4x4.identity))
            {
                m_BoundsHandle.center = bounds.center;
                m_BoundsHandle.size = bounds.size;

                EditorGUI.BeginChangeCheck();
                m_BoundsHandle.DrawHandle();
                //m_BoundsHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(script.colorMap, "Modified Grass color map bounds");
                    Vector3 center = m_BoundsHandle.center;
                    Vector3 size = m_BoundsHandle.size;

                    script.colorMap.bounds.center = center;
                    script.colorMap.bounds.size = size;
                    EditorUtility.SetDirty(target);
                }
            }
        }
    }
}
