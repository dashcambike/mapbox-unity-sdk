using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mapbox.VectorModule.Filters;
using Mapbox.VectorModule.MeshGeneration.Unity;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mapbox.VectorModule.Editor
{
    [CustomEditor(typeof(VectorFilterStackObject), true)]
    public class VectorFilterStackObjectEditor : UnityEditor.Editor
    {
        class Styles
        {
            public static readonly GUIContent RenderFeatures =
                new GUIContent("Vector Feature Filters",
                    "Filters for the modifier stack (styling) to use for choosing eligible features.");

            public static readonly GUIContent PassNameField =
                new GUIContent("Name", "Name of the filter");

            public static readonly GUIContent MissingFeature = new GUIContent("Missing filter",
                "Missing reference, due to compilation issues or missing files. you can attempt auto fix or choose to remove the feature.");

            public static GUIStyle BoldLabelSimple;

            static Styles()
            {
                BoldLabelSimple = new GUIStyle(EditorStyles.label);
                BoldLabelSimple.fontStyle = FontStyle.Bold;
            }
        }
        
        private SerializedProperty m_filters;
        List<UnityEditor.Editor> m_Editors = new List<UnityEditor.Editor>();
        [SerializeField] private bool falseBool = false;
        private SerializedProperty m_FalseBool;
        
        private void OnEnable()
        {
            m_filters = serializedObject.FindProperty(nameof(VectorFilterStackObject.Filters));
            var editorObj = new SerializedObject(this);
            m_FalseBool = editorObj.FindProperty(nameof(falseBool));
            UpdateEditorList();
        }
        
        public override void OnInspectorGUI()
        {
            if (m_filters == null)
                OnEnable();
            else if (m_filters.arraySize != m_Editors.Count)
                UpdateEditorList();

            serializedObject.Update();
            DrawFilterList();
        }
        
        private void DrawFilterList()
        {
            EditorGUILayout.LabelField(Styles.RenderFeatures, EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (m_filters.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No feature filters added", MessageType.Info);
            }
            else
            {
                //Draw List
                CoreEditorUtils.DrawSplitter();
                for (int i = 0; i < m_filters.arraySize; i++)
                {
                    SerializedProperty renderFeaturesProperty = m_filters.GetArrayElementAtIndex(i);
                    DrawFilter(i, ref renderFeaturesProperty);
                    CoreEditorUtils.DrawSplitter();
                }
            }
            EditorGUILayout.Space();

            //Add filter
            if (GUILayout.Button("Add filter", EditorStyles.miniButton))
            {
                AddPassMenu();
            }
        }
        
        private void AddPassMenu()
        {
            GenericMenu menu = new GenericMenu();
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<FilterBaseObject>();
            foreach (Type type in types)
            {
                var data = target as VectorFilterStackObject;
                // if (data.DuplicateFeatureCheck(type))
                // {
                //     continue;
                // }

                string path = type.Name;
                menu.AddItem(new GUIContent(path), false, AddComponent, type.Name);
            }
            menu.ShowAsContext();
        }
        
        private void AddComponent(object type)
        {
            serializedObject.Update();

            ScriptableObject component = CreateInstance((string)type);
            component.name = $"New{(string)type}";
            Undo.RegisterCreatedObjectUndo(component, "Add filter");

            // Store this new effect as a sub-asset so we can reference it safely afterwards
            // Only when we're not dealing with an instantiated asset
            if (EditorUtility.IsPersistent(target))
            {
                AssetDatabase.AddObjectToAsset(component, target);
            }
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out var guid, out long localId);

            // Grow the list first, then add - that's how serialized lists work in Unity
            m_filters.arraySize++;
            SerializedProperty componentProp = m_filters.GetArrayElementAtIndex(m_filters.arraySize - 1);
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
        
        private void UpdateEditorList()
        {
            ClearEditorsList();
            for (int i = 0; i < m_filters.arraySize; i++)
            {
                m_Editors.Add(CreateEditor(m_filters.GetArrayElementAtIndex(i).objectReferenceValue));
            }
        }
        
        private void ClearEditorsList()
        {
            for (int i = m_Editors.Count - 1; i >= 0; --i)
            {
                DestroyImmediate(m_Editors[i]);
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
        
        private void DrawFilter(int index, ref SerializedProperty renderFeatureProperty)
        {
            Object filterObjRef = renderFeatureProperty.objectReferenceValue;
            if (filterObjRef != null)
            {
                bool hasChangedProperties = false;
                string title = ObjectNames.GetInspectorTitle(filterObjRef);

                // Get the serialized object for the editor script & update it
                UnityEditor.Editor filterEditor = m_Editors[index];
                SerializedObject serializedFilterEditor = filterEditor.serializedObject;
                serializedFilterEditor.Update();

                // Foldout header
                EditorGUI.BeginChangeCheck();
                SerializedProperty activeProperty = serializedFilterEditor.FindProperty("m_Active");
                bool displayContent = CoreEditorUtils.DrawHeaderToggle(title, renderFeatureProperty, activeProperty, pos => OnContextClick(pos, index));
                hasChangedProperties |= EditorGUI.EndChangeCheck();

                // ObjectEditor
                if (displayContent)
                {
                    EditorGUI.BeginChangeCheck();
                    SerializedProperty nameProperty = serializedFilterEditor.FindProperty("m_Name");
                    nameProperty.stringValue = ValidateName(EditorGUILayout.DelayedTextField(Styles.PassNameField, nameProperty.stringValue));
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        hasChangedProperties = true;

                        // We need to update sub-asset name
                        filterObjRef.name = nameProperty.stringValue;
                        AssetDatabase.SaveAssets();

                        // Triggers update for sub-asset name change
                        ProjectWindowUtil.ShowCreatedAsset(target);
                    }

                    EditorGUI.BeginChangeCheck();
                    filterEditor.OnInspectorGUI();
                    hasChangedProperties |= EditorGUI.EndChangeCheck();

                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                }

                // Apply changes and save if the user has modified any settings
                if (hasChangedProperties)
                {
                    serializedFilterEditor.ApplyModifiedProperties();
                    serializedObject.ApplyModifiedProperties();
                    ForceSave();
                }
            }
            else
            {
                CoreEditorUtils.DrawHeaderToggle(Styles.MissingFeature,renderFeatureProperty, m_FalseBool,pos => OnContextClick(pos, index));
                m_FalseBool.boolValue = false; // always make sure false bool is false
                EditorGUILayout.HelpBox(Styles.MissingFeature.tooltip, MessageType.Error);
                if (GUILayout.Button("Attempt Fix", EditorStyles.miniButton))
                {
                    VectorFilterStackObject data = target as VectorFilterStackObject;
                }
            }
        }
        
        private void OnContextClick(Vector2 position, int id)
        {
            var menu = new GenericMenu();

            if (id == 0)
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
            else
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false, () => MoveComponent(id, -1));

            if (id == m_filters.arraySize - 1)
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
            else
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false, () => MoveComponent(id, 1));

            menu.AddSeparator(string.Empty);
            menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false, () => RemoveComponent(id));

            menu.DropDown(new Rect(position, Vector2.zero));
        }
        
        private void RemoveComponent(int id)
        {
            SerializedProperty property = m_filters.GetArrayElementAtIndex(id);
            Object component = property.objectReferenceValue;
            property.objectReferenceValue = null;

            Undo.SetCurrentGroupName(component == null ? "Remove Filter" : $"Remove {component.name}");

            // remove the array index itself from the list
            m_filters.DeleteArrayElementAtIndex(id);
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
        
        private void MoveComponent(int id, int offset)
        {
            Undo.SetCurrentGroupName("Move Render Feature");
            serializedObject.Update();
            m_filters.MoveArrayElement(id, id + offset);
            UpdateEditorList();
            serializedObject.ApplyModifiedProperties();

            // Force save / refresh
            ForceSave();
        }
    }
}
