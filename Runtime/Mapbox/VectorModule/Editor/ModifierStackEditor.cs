using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mapbox.Editor;
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
    private Texture2D _magnifier;
    private Editor _filterEditor;
    
    private void OnEnable()
    {
        m_MeshModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.MeshModifiers));
        m_Editors.Add(m_MeshModifiers, new List<Editor>());
        m_GoModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.GoModifiers));
        m_Editors.Add(m_GoModifiers, new List<Editor>());
        _magnifier = EditorGUIUtility.FindTexture("d_ViewToolZoom");
        
        var editorObj = new SerializedObject(this);
        m_FalseBool = editorObj.FindProperty(nameof(falseBool));
        UpdateEditorList();
        
        ScriptableCreatorWindow.WindowClosed += () =>
        {
            m_MeshModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.MeshModifiers));
            m_GoModifiers = serializedObject.FindProperty(nameof(ModifierStackObject.GoModifiers));
            UpdateEditorList();
        };
    }

    private void CreateFilterStack()
    {
        m_FilterStack = serializedObject.FindProperty(nameof(ModifierStackObject.Filters));
        if(m_FilterStack == null || m_FilterStack.objectReferenceValue == null)
        {
            ScriptableObject component = CreateInstance(nameof(VectorFilterStackObject));
            component.name = $"FilterStack";
            if (EditorUtility.IsPersistent(target))
            {
                AssetDatabase.AddObjectToAsset(component, target);
            }

            _filterEditor = CreateEditor(component);
            m_FilterStack.objectReferenceValue = component;
            serializedObject.ApplyModifiedProperties();
        }
    }

    public override void OnInspectorGUI()
    {
        if (!EditorUtility.IsPersistent(target))
            return;
        
        
        if (m_FilterStack == null)
            CreateFilterStack();

        serializedObject.Update();
        
        EditorGUILayout.Space();
        SerializedProperty nameProperty = serializedObject.FindProperty("m_Name");
        EditorGUILayout.LabelField(nameProperty.stringValue, EditorStyles.whiteLargeLabel);
        
        if(_filterEditor == null) 
            _filterEditor = CreateEditor(m_FilterStack.objectReferenceValue);
        if (_filterEditor != null)
        {
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            EditorGUILayout.Space();
            CoreEditorUtils.DrawSplitter();
            EditorGUILayout.Space();
           
            EditorGUILayout.LabelField("Filters");
            {
                CoreEditorUtils.DrawSplitter();
                _filterEditor.OnInspectorGUI();
                
            }
        }
        EditorGUILayout.Space();
        CoreEditorUtils.DrawSplitter();
        EditorGUILayout.Space();
        CoreEditorUtils.DrawSplitter();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mesh Modifiers");
        {
            DrawMeshModifiers(m_MeshModifiers, typeof(ScriptableMeshModifierObject));
        }

        EditorGUILayout.Space();
        CoreEditorUtils.DrawSplitter();
        EditorGUILayout.Space();
        CoreEditorUtils.DrawSplitter();
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Gameobject Modifiers");
        {
            DrawMeshModifiers(m_GoModifiers, typeof(ScriptableGameObjectModifierObject));
        }
        EditorGUILayout.Space();
        CoreEditorUtils.DrawSplitter();

    }
    
    private void DrawMeshModifiers(SerializedProperty property, Type type)
    {
        try
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
        }
        catch (Exception e)
        {
            
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Modifier", (GUIStyle)"minibuttonleft"))
        {
            AddPassMenu(property, type);
        }
        if (GUILayout.Button(_magnifier, (GUIStyle)"minibuttonright", GUILayout.Width(30)))
        {
            ScriptableCreatorWindow.Open(type, property);
        }
        EditorGUILayout.EndHorizontal();
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
        component.name = $"{(string)type}";
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
                EditorGUILayout.Space();
                
                //if (AssetDatabase.IsMainAsset(modifierObjRef))
                {
                    EditorGUILayout.ObjectField(renderFeatureProperty);
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
    
    private void RemoveComponent(SerializedProperty arrayProperty, int id)
    {
        SerializedProperty property = arrayProperty.GetArrayElementAtIndex(id);
        Object component = property.objectReferenceValue;
        property.objectReferenceValue = null;

        Undo.SetCurrentGroupName(component == null ? "Remove Modifier" : $"Remove {component.name}");

        // remove the array index itself from the list
        arrayProperty.DeleteArrayElementAtIndex(id);
        UpdateEditorList();
        serializedObject.ApplyModifiedProperties();

        
        // var isAssetFile = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);
        // var isStackFile = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(serializedObject.targetObject, out var stackGuid, out long stackLocalId);
        
        // Destroy the setting object after ApplyModifiedProperties(). If we do it before, redo
        // actions will be in the wrong order and the reference to the setting object in the
        // list will be lost.
        if (component != null && !AssetDatabase.IsMainAsset(component)) //guid == stackGuid
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