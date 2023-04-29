﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StylizedGrass
{
    [CustomEditor(typeof(GrassBender))]
    public class GrassBenderInspector : Editor
    {
        GrassBender bender;
        private Vector3[] points;
        private Vector3[] circlePoints;

        SerializedProperty renderer;
        SerializedProperty sortingLayer;
        SerializedProperty flattenStrength;
        SerializedProperty heightOffset;
        SerializedProperty scaleMultiplier;
        SerializedProperty pushStrength;
        
        //Mesh
        SerializedProperty alphaBlending;
        
        //Trail
        SerializedProperty forceUpdating;

        private int m_layer;

        private void OnEnable()
        {
            bender = (GrassBender)target;
            
            renderer = serializedObject.FindProperty("renderer");
            sortingLayer = serializedObject.FindProperty("sortingLayer");

            scaleMultiplier = serializedObject.FindProperty("scaleMultiplier");
            
            heightOffset = serializedObject.FindProperty("heightOffset");
            flattenStrength = serializedObject.FindProperty("flattenStrength");
            pushStrength = serializedObject.FindProperty("pushStrength");
            
            alphaBlending = serializedObject.FindProperty("alphaBlending");
            forceUpdating = serializedObject.FindProperty("forceUpdating");
        }

        public override void OnInspectorGUI()
        {            
            #if URP
            EditorGUILayout.LabelField("Version " + AssetInfo.INSTALLED_VERSION, EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(5);
            
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(renderer);

                if (GUILayout.Button("This", EditorStyles.miniButton, GUILayout.MaxWidth(75f)))
                {
                    renderer.objectReferenceValue = bender.GetComponent<Renderer>();
                    bender.GetRenderer();
                }
            }

            if (renderer.objectReferenceValue)
            {
                if (renderer.objectReferenceValue.GetType() == typeof(MeshRenderer))
                {
                    EditorGUILayout.PropertyField(scaleMultiplier);
                }
                
                if (renderer.objectReferenceValue.GetType() == typeof(TrailRenderer))
                {
                    EditorGUILayout.PropertyField(forceUpdating);
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledGroupScope(GrassBendingFeature.SRPBatcherEnabled() == false))
                {
                    EditorGUILayout.PropertyField(sortingLayer);
                    EditorGUILayout.PropertyField(alphaBlending);
                }
                if (GrassBendingFeature.SRPBatcherEnabled() == false)
                {
                    EditorGUILayout.HelpBox("These options are only available if the SRP Batcher is enabled", MessageType.Info);
                }
                
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Bending", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(heightOffset);
                EditorGUILayout.PropertyField(flattenStrength);
                EditorGUILayout.PropertyField(pushStrength);
            }
            else
            {
                EditorGUILayout.HelpBox("A renderer component must be assigned. This can be a:\n\n" +
                                        "• Mesh Renderer\n" +
                                        "• Trail Renderer\n" +
                                        "• Line Renderer\n" +
                                        "• Particle System\n", 
                    MessageType.Error);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                bender.UpdateProperties();
            }

            #else
            EditorGUILayout.HelpBox("Universal Render Pipeline package isn't installed", MessageType.Error);
            #endif
            EditorGUILayout.LabelField("- Staggart Creations -", EditorStyles.centeredGreyMiniLabel);

            //base.OnInspectorGUI();
        }


        private void OnSceneGUI()
        {
            if (bender.trailRenderer == null) return;

            points = new Vector3[bender.trailRenderer.positionCount];
            bender.trailRenderer.GetPositions(points);
            Handles.color = Color.white;

            circlePoints = new Vector3[16];
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 15;
                circlePoints[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * bender.trailRenderer.widthMultiplier * 0.5f;
                circlePoints[i] += bender.transform.position;
            }
            Handles.color = new Color(1, 1, 1, 0.5f);
            Handles.DrawAAPolyLine(Texture2D.whiteTexture, 2.5f, circlePoints);

            Handles.color = new Color(1, 1, 1, 0.1f);
            Handles.DrawSolidDisc(bender.transform.position, Vector3.up, bender.trailRenderer.widthMultiplier * 0.5f);

            DrawDottedLines(points, 5f);
        }

        public void DrawDottedLines(Vector3[] lineSegments, float screenSpaceSize)
        {
            //UnityEditor.Handles.BeginGUI();
            var dashSize = screenSpaceSize * EditorGUIUtility.pixelsPerPoint;
            for (int i = 0; i < lineSegments.Length - 1; i += 2)
            {
                var p1 = lineSegments[i + 0];
                var p2 = lineSegments[i + 1];

                if (p1 == null || p2 == null) continue;

                Handles.color = new Color(1, 1, 1, 1f - bender.trailRenderer.colorGradient.Evaluate((float)i / (float)lineSegments.Length).r);
                Handles.DrawAAPolyLine(Texture2D.whiteTexture, dashSize, new Vector3[] { p1, p2 });
            }
            //UnityEditor.Handles.EndGUI();
        }

    }
}
