﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace StylizedGrass
{
    [AddComponentMenu("Stylized Grass/Colormap Renderer")]
    [ExecuteInEditMode]
    [HelpURL(("http://staggart.xyz/unity/stylized-grass-shader/sgs-docs/?section=blending-with-terrain-colors"))]
    public class GrassColorMapRenderer : MonoBehaviour
    {
        public static GrassColorMapRenderer Instance;

        public GrassColorMap colorMap;
        [Tooltip("These objects can be Unity Terrains or custom Mesh Terrains. Their size can be used to automatically fit the render area")]
        public List<GameObject> terrainObjects = new List<GameObject>();
        public int resIdx;
        public int resolution = 1024;
        [Tooltip("Objects set to this layer will be included in the render")]
        public LayerMask cullingMask = -1;
        [Tooltip("Effectively sets which mipmap level of the terrain layer's diffuse texture must be sampled." +
                 "\nA value of 0 is recommend to filter out any high frequency details. Whereas as value 100 ensures the finest detail is captured." +
                 "" +
                 "\n\nThis has no effect on custom terrain shaders, or non-terrain objects!")]
        [Range(0f, 100f)]
        public float textureDetail = 0f;
        [Tooltip("Enable this option if you're using a custom terrain shader which greatly alters the terrain color (eg. global noise).\n\n" +
                 "When disabled, the terrains are temporarily rendered using an Unlit shader (based on the default Unity terrain shader)\n\nThis only applies to Unity terrain, not meshes")]
        
        public bool thirdPartyShader = false;
        [NonSerialized]
        public bool showBounds = true;

        [Serializable]
        public class LayerScaleSettings
        {
            public int layerID;
            [Range(0f, 1f)]
            public float strength = 1f;
        }
        public List<LayerScaleSettings> layerScaleSettings = new List<LayerScaleSettings>();

        private void OnEnable()
        {
            Instance = this;
            AssignColorMap();

#if UNITY_EDITOR
            if (this.gameObject.name == "GameObject") this.gameObject.name = "Grass Colormap renderer";

            EditorSceneManager.sceneSaved += OnSceneSave;
#endif
        }

        private void OnDisable()
        {
            Instance = null;
            //Disable sampling of color map
            GrassColorMap.DisableGlobally();

#if UNITY_EDITOR
            EditorSceneManager.sceneSaved -= OnSceneSave;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (!colorMap || !showBounds) return;

            Color32 color = new Color(0f, 0.66f, 1f, 0.25f);
            Gizmos.color = color;
            Gizmos.DrawCube(colorMap.bounds.center, colorMap.bounds.size);

            color = new Color(0f, 0.66f, 1f, 1f);
            Gizmos.color = color;
            Gizmos.DrawWireCube(colorMap.bounds.center, colorMap.bounds.size);
        }

        public void AssignActiveTerrains()
        {
            Terrain[] terrains = Terrain.activeTerrains;

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrainObjects.Contains(terrains[i].gameObject) == false) terrainObjects.Add(terrains[i].gameObject);
            }
        }

        public void AssignVegetationStudioMeshTerrains()
        {
            #if VEGETATION_STUDIO_PRO
            AwesomeTechnologies.MeshTerrains.MeshTerrain[] terrains = GameObject.FindObjectsOfType<AwesomeTechnologies.MeshTerrains.MeshTerrain>();

            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrainObjects.Contains(terrains[i].gameObject) == false) terrainObjects.Add(terrains[i].gameObject);
            }
            #endif
        }

        public void AssignChildMeshes()
        {
            //All childs, recursively
            MeshRenderer[] children = gameObject.GetComponentsInChildren<MeshRenderer>();

            for (int i = 0; i < children.Length; i++)
            {
                if (terrainObjects.Contains(children[i].gameObject) == false) terrainObjects.Add(children[i].gameObject);
            }
        }

        public void AssignColorMap()
        {
            if (!colorMap) return;

            colorMap.SetActive();
        }

#if UNITY_EDITOR
        private void OnSceneSave(UnityEngine.SceneManagement.Scene scene)
        {
            AssignColorMap();
        }
#endif
    }
}