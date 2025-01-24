using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mapbox.VectorModule.Editor;
using Mapbox.VectorModule.MeshGeneration.MeshModifiers;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(ModifierStackObject), true)]
public class ModifierStackEditor : Editor
{
    private Dictionary<SerializedProperty, List<Editor>> m_Editors = new Dictionary<SerializedProperty, List<Editor>>();
    private SerializedProperty m_MeshModifiers;
    private SerializedProperty m_GoModifiers;
    private SerializedProperty m_FilterStack;
    [SerializeField] private bool falseBool = false;
    private SerializedProperty m_FalseBool;
    
    private Editor _filterEditor;
    
    private void OnEnable()
    {
        

        m_MeshModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.MeshModifiers));
        m_Editors.Add(m_MeshModifiers, new List<Editor>());
        m_GoModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.GoModifiers));
        m_Editors.Add(m_GoModifiers, new List<Editor>());
        
        var editorObj = new SerializedObject(this);
        m_FalseBool = editorObj.FindProperty(nameof(falseBool));
        UpdateEditorList();
    }

    private void CreateFilterStack()
    {
        m_FilterStack = serializedObject.FindProperty(nameof(ModifierStackObject.Filters));
        if(m_FilterStack == null || m_FilterStack.objectReferenceValue == null)
        {
            ScriptableObject component = CreateInstance(nameof(VectorFilterStackObject));
            component.name = $"New_{nameof(VectorFilterStackObject)}";
            if (EditorUtility.IsPersistent(target))
            {
                AssetDatabase.AddObjectToAsset(component, target);
            }

            _filterEditor = CreateEditor(component);
            m_FilterStack.objectReferenceValue = component;
            serializedObject.ApplyModifiedProperties();
            var success = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);
            Debug.Log(success);
        }
    }

    public override void OnInspectorGUI()
    {
        if (!EditorUtility.IsPersistent(target))
            return;
        
        
        if (m_FilterStack == null)
            CreateFilterStack();

        serializedObject.Update();
        
        SerializedProperty nameProperty = serializedObject.FindProperty("m_Name");
        EditorGUILayout.LabelField(nameProperty.stringValue);
        
        if(_filterEditor == null) 
            _filterEditor = CreateEditor(m_FilterStack.objectReferenceValue);
        if (_filterEditor != null)
        {
            CoreEditorUtils.DrawSplitter();
            EditorGUI.BeginChangeCheck();
            _filterEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            CoreEditorUtils.DrawSplitter();
        }

        EditorGUILayout.LabelField(new GUIContent("Mesh Modifiers"), EditorStyles.boldLabel);
        EditorGUILayout.Space();
        DrawMeshModifiers(m_MeshModifiers, typeof(ScriptableMeshModifierObject));
        EditorGUILayout.LabelField(new GUIContent("GameObject Modifiers"), EditorStyles.boldLabel);
        EditorGUILayout.Space();
        DrawMeshModifiers(m_GoModifiers, typeof(ScriptableGameObjectModifierObject));
        //DrawRendererFeatureList();
    }
    
    private void DrawMeshModifiers(SerializedProperty property, Type type)
    {
        if (property.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No modifiers added", MessageType.Info);
        }
        else
        {
            //Draw List
            CoreEditorUtils.DrawSplitter();
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty renderFeaturesProperty = property.GetArrayElementAtIndex(i);
                DrawModifier(property, i, ref renderFeaturesProperty);
                CoreEditorUtils.DrawSplitter();
            }
        }
        EditorGUILayout.Space();
        if (GUILayout.Button("Add Modifier", EditorStyles.miniButton))
        {
            AddPassMenu(property, type);
        }
    }
    
    private void AddPassMenu(SerializedProperty property, Type modType)
    {
        GenericMenu menu = new GenericMenu();
        TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom(modType);
        foreach (Type type in types)
        {
            var data = target as VectorFilterStackObject;
            // if (data.DuplicateFeatureCheck(type))
            // {
            //     continue;
            // }

            string path = type.Name;
            menu.AddItem(new GUIContent(path), false, (o) => AddComponent(property, o), type.Name);
        }
        menu.ShowAsContext();
    }
    
    private void AddComponent(SerializedProperty property, object type)
    {
        serializedObject.Update();

        ScriptableObject component = CreateInstance((string)type);
        component.name = $"New{(string)type}";
        Undo.RegisterCreatedObjectUndo(component, "Add modifier");

        // Store this new effect as a sub-asset so we can reference it safely afterwards
        // Only when we're not dealing with an instantiated asset
        if (EditorUtility.IsPersistent(target))
        {
            AssetDatabase.AddObjectToAsset(component, target);
        }
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);

        // Grow the list first, then add - that's how serialized lists work in Unity
        property.arraySize++;
        SerializedProperty componentProp = property.GetArrayElementAtIndex(property.arraySize - 1);
        componentProp.objectReferenceValue = component;

        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Force save / refresh
        if (EditorUtility.IsPersistent(target))
        {
            ForceSave();
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawModifier(SerializedProperty property, int index, ref SerializedProperty renderFeatureProperty)
    {
        Object modifierObjRef = renderFeatureProperty.objectReferenceValue;
        if (modifierObjRef != null)
        {
            bool hasChangedProperties = false;
            string title = ObjectNames.GetInspectorTitle(modifierObjRef);

            // Get the serialized object for the editor script & update it
            UnityEditor.Editor modifierEditor = m_Editors[property][index];
            SerializedObject serializedModifierEditor = modifierEditor.serializedObject;
            serializedModifierEditor.Update();

            // Foldout header
            EditorGUI.BeginChangeCheck();
            SerializedProperty activeProperty = serializedModifierEditor.FindProperty("m_Active");
            bool displayContent = CoreEditorUtils.DrawHeaderToggle(title, renderFeatureProperty, activeProperty, pos => OnContextClick(property, pos, index));
            hasChangedProperties |= EditorGUI.EndChangeCheck();

            // ObjectEditor
            if (displayContent)
            {
                EditorGUI.BeginChangeCheck();
                SerializedProperty nameProperty = serializedModifierEditor.FindProperty("m_Name");
                nameProperty.stringValue =
                    ValidateName(EditorGUILayout.DelayedTextField(new GUIContent("Name"), nameProperty.stringValue));

                if (EditorGUI.EndChangeCheck())
                {
                    hasChangedProperties = true;

                    // We need to update sub-asset name
                    modifierObjRef.name = nameProperty.stringValue;
                    AssetDatabase.SaveAssets();

                    // Triggers update for sub-asset name change
                    ProjectWindowUtil.ShowCreatedAsset(target);
                }

                EditorGUI.BeginChangeCheck();
                modifierEditor.OnInspectorGUI();
                hasChangedProperties |= EditorGUI.EndChangeCheck();

                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            }

            // Apply changes and save if the user has modified any settings
            if (hasChangedProperties)
            {
                serializedModifierEditor.ApplyModifiedProperties();
                serializedObject.ApplyModifiedProperties();
                ForceSave();
            }
        }
        else
        {
            CoreEditorUtils.DrawHeaderToggle(new GUIContent("Missing Modifier"), renderFeatureProperty, m_FalseBool,
                pos => OnContextClick(property, pos, index));
            m_FalseBool.boolValue = false; // always make sure false bool is false
        }
    }
    private void OnContextClick(SerializedProperty property, Vector2 position, int id)
    {
        var menu = new GenericMenu();

        if (id == 0)
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
        else
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false, () => MoveComponent(property, id, -1));

        if (id == property.arraySize - 1)
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
        else
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false, () => MoveComponent(property, id, 1));

        menu.AddSeparator(string.Empty);
        menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false, () => RemoveComponent(property, id));

        menu.DropDown(new Rect(position, Vector2.zero));
    }
    
    private void RemoveComponent(SerializedProperty sproperty, int id)
    {
        SerializedProperty property = sproperty.GetArrayElementAtIndex(id);
        Object component = property.objectReferenceValue;
        property.objectReferenceValue = null;

        Undo.SetCurrentGroupName(component == null ? "Remove Modifier" : $"Remove {component.name}");

        // remove the array index itself from the list
        sproperty.DeleteArrayElementAtIndex(id);
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
        // actions will be in the wrong order and the reference to the setting object in the
        // list will be lost.
        if (component != null)
        {
            Undo.DestroyObjectImmediate(component);
        }

        // Force save / refresh
        ForceSave();
    }
    
    private void MoveComponent(SerializedProperty sproperty, int id, int offset)
    {
        Undo.SetCurrentGroupName("Move Render Feature");
        serializedObject.Update();
        sproperty.MoveArrayElement(id, id + offset);
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        // Force save / refresh
        ForceSave();
    }


    private void UpdateEditorList()
    {
        ClearEditorsList();
        
        if(!m_Editors.ContainsKey(m_MeshModifiers)) m_Editors.Add(m_MeshModifiers, new List<Editor>());
        if(!m_Editors.ContainsKey(m_GoModifiers)) m_Editors.Add(m_GoModifiers, new List<Editor>());
        
        for (int i = 0; i < m_MeshModifiers.arraySize; i++)
        {
            m_Editors[m_MeshModifiers].Add(CreateEditor(m_MeshModifiers.GetArrayElementAtIndex(i).objectReferenceValue));
        }
        for (int i = 0; i < m_GoModifiers.arraySize; i++)
        {
            m_Editors[m_GoModifiers].Add(CreateEditor(m_GoModifiers.GetArrayElementAtIndex(i).objectReferenceValue));
        }
    }
    
    private void ClearEditorsList()
    {
        foreach (var perProp in m_Editors.Values)
        {
            for (int i = perProp.Count - 1; i >= 0; --i)
            {
                DestroyImmediate(perProp[i]);
            }
        }
        
        m_Editors.Clear();
    }
        
    private void ForceSave()
    {
        EditorUtility.SetDirty(target);
    }
    
    private string ValidateName(string name)
    {
        name = Regex.Replace(name, @"[^a-zA-Z0-9 ]", "");
        return name;
    }
    

}
