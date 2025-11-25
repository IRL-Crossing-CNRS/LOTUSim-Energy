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
//  LotusimConnectorEditor.cs
// Description:
// Custom Unity Editor script for LotusimInterface that lets users select and update the interface type 
// and namespace directly in the Inspector, automatically applying changes and triggering relevant callbacks.
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Lotusim;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LotusimInterface))]
public class LotusimInterfaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty interfaceTypeProp = serializedObject.FindProperty(
            "selectedInterfaceType"
        );
        SerializedProperty namespaceProp = serializedObject.FindProperty("m_namespace");
        string oldInterfaceType = interfaceTypeProp.stringValue;
        string oldNamespace = namespaceProp.stringValue;

        var options = GetAvailableInterfaceTypes().ToArray();
        int selectedIndex = Mathf.Max(
            0,
            System.Array.IndexOf(options, interfaceTypeProp.stringValue)
        );
        selectedIndex = EditorGUILayout.Popup("Interface Type", selectedIndex, options);
        interfaceTypeProp.stringValue = options[selectedIndex];

        EditorGUILayout.PropertyField(namespaceProp);

        serializedObject.ApplyModifiedProperties();

        if (namespaceProp.stringValue != oldNamespace)
        {
            Debug.Log("Namespace changed!");
            ((LotusimInterface)target).OnNamespaceChanged();
        }

        if (interfaceTypeProp.stringValue != oldInterfaceType)
        {
            Debug.Log("Interface Type changed!");
            ((LotusimInterface)target).OnInterfaceTypeChanged();
        }
    }

    private void DrawDefaultInspectorExcept(string propertyName)
    {
        SerializedProperty prop = serializedObject.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == propertyName)
                continue;
            EditorGUILayout.PropertyField(prop, true);
        }
    }

    private List<string> GetAvailableInterfaceTypes()
    {
        return LotusimInterfaceFactory.GetAvailableInterfaceTypes().ToList();
    }
}
