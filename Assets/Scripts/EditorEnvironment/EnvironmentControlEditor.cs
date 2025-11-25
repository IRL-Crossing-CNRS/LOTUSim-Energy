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
// EnvironmentControlEditor.cs
//  
// Description:
// Scripts to control the sun and the rain in a scene.
//  
// --------------------------------------------------------------------------------------------------------------------

//using UnityEditor;
//using UnityEngine;

//[CustomEditor(typeof(EnvironmentController))]
//public class EnvironmentControllerEditor : Editor
//{
    // Sun
//    SerializedProperty m_sun_angle;
//    SerializedProperty m_sun_object;

    // Rain
//    SerializedProperty m_rain_object;
 //   SerializedProperty m_rain_heaviness;

//    void OnEnable()
//    {
//        m_sun_angle = serializedObject.FindProperty("m_sun_angle");
//        m_sun_object = serializedObject.FindProperty("m_sun_object");
//        m_rain_object = serializedObject.FindProperty("m_rain_object");
//        m_rain_heaviness = serializedObject.FindProperty("m_rain_heaviness");
//    }
    
//    public override void OnInspectorGUI()
//    {
        // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
//        serializedObject.Update();

        // Show the custom GUI controls.
//        EditorGUILayout.IntSlider(m_sun_angle, 0, 190, new GUIContent("Sun Angle"));
//        EditorGUILayout.PropertyField(m_sun_object, new GUIContent ("Sun Object"));
//        EditorGUILayout.PropertyField(m_rain_object, new GUIContent ("Rain Object"));
//        EditorGUILayout.IntSlider(m_rain_heaviness, 0, 1000, new GUIContent("Rain Heaviness"));

        // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
//        serializedObject.ApplyModifiedProperties();

//    }
//}
