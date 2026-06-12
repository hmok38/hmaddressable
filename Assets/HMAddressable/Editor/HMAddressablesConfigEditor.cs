using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace HM
{
    [CustomEditor(typeof(HMAddressablesConfig))]
    public class HMAddressablesConfigEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> ReadonlyFields = new HashSet<string>
        {
            "SeparatelyPackAssetsPaths",
        };

        private ReorderableList _unassignedList;
        private ReorderableList _aaAssetsList;
        private ReorderableList _localPathsList;
        private ReorderableList _remotePathsList;
        private bool _showUnassignedList;

        private void OnEnable()
        {
            _unassignedList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("UnassignedAssetsPath"), false, false, false, false);
            _unassignedList.drawHeaderCallback = rect => { };
            _unassignedList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = _unassignedList.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty groupName = element.FindPropertyRelative("GroupName");
                SerializedProperty beLocal = element.FindPropertyRelative("BeLocal");
                SerializedProperty beRemote = element.FindPropertyRelative("BeRemote");

                float toggleLabelWidth = 35;
                float toggleWidth = 20;
                float groupValueWidth = rect.width - toggleLabelWidth * 3 - toggleWidth * 3 - 15;

                Rect groupValueRect = new Rect(rect.x, rect.y, groupValueWidth, rect.height);

                float rightStart = groupValueRect.xMax + 5;
                Rect localLabelRect = new Rect(rightStart, rect.y, toggleLabelWidth, rect.height);
                Rect localToggleRect = new Rect(localLabelRect.xMax, rect.y, toggleWidth, rect.height);
                Rect remoteLabelRect = new Rect(localToggleRect.xMax, rect.y, toggleLabelWidth, rect.height);
                Rect remoteToggleRect = new Rect(remoteLabelRect.xMax, rect.y, toggleWidth, rect.height);
                Rect separateLabelRect = new Rect(remoteToggleRect.xMax, rect.y, toggleLabelWidth, rect.height);
                Rect separateToggleRect = new Rect(separateLabelRect.xMax, rect.y, toggleWidth, rect.height);

                GUI.enabled = false;
                EditorGUI.TextField(groupValueRect, groupName.stringValue);
                GUI.enabled = true;

                EditorGUI.LabelField(localLabelRect, "本地:");
                EditorGUI.BeginChangeCheck();
                beLocal.boolValue = EditorGUI.Toggle(localToggleRect, beLocal.boolValue);
                if (EditorGUI.EndChangeCheck() && beLocal.boolValue)
                    beRemote.boolValue = false;

                EditorGUI.LabelField(remoteLabelRect, "远程:");
                EditorGUI.BeginChangeCheck();
                beRemote.boolValue = EditorGUI.Toggle(remoteToggleRect, beRemote.boolValue);
                if (EditorGUI.EndChangeCheck() && beRemote.boolValue)
                    beLocal.boolValue = false;

                string groupPath = groupName.stringValue;
                bool isSeparate = IsSeparatelyPack(groupPath);
                EditorGUI.LabelField(separateLabelRect, "分散:");
                EditorGUI.BeginChangeCheck();
                bool newSeparate = EditorGUI.Toggle(separateToggleRect, isSeparate);
                if (EditorGUI.EndChangeCheck())
                    SetSeparatelyPack(groupPath, newSeparate);
            };
            _unassignedList.elementHeight = EditorGUIUtility.singleLineHeight;

            _aaAssetsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("AAAssetsPath"), true, true, true, true);
            _aaAssetsList.drawHeaderCallback = rect => { };
            _aaAssetsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                SerializedProperty element = _aaAssetsList.serializedProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element, GUIContent.none);
            };
            _aaAssetsList.elementHeight = EditorGUIUtility.singleLineHeight;

            _localPathsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("LocalAseetsPaths"), true, true, false, false);
            _localPathsList.drawHeaderCallback = rect => { };
            _localPathsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index >= _localPathsList.serializedProperty.arraySize)
                    return;
                SerializedProperty element = _localPathsList.serializedProperty.GetArrayElementAtIndex(index);
                if (element == null) return;
                float btnWidth = 50;
                float separateLabelWidth = 35;
                float separateToggleWidth = 20;
                float separateTotalWidth = separateLabelWidth + separateToggleWidth + 5;
                float propWidth = rect.width - btnWidth - separateTotalWidth - 10;
                Rect propRect = new Rect(rect.x, rect.y, propWidth, EditorGUIUtility.singleLineHeight);
                Rect separateLabelRect = new Rect(propRect.xMax + 5, rect.y, separateLabelWidth,
                    EditorGUIUtility.singleLineHeight);
                Rect separateToggleRect = new Rect(separateLabelRect.xMax, rect.y, separateToggleWidth,
                    EditorGUIUtility.singleLineHeight);
                Rect btnRect = new Rect(separateToggleRect.xMax + 5, rect.y, btnWidth,
                    EditorGUIUtility.singleLineHeight);

                GUI.enabled = false;
                EditorGUI.PropertyField(propRect, element, GUIContent.none);
                GUI.enabled = true;

                string localPath = element.stringValue;
                bool localIsSeparate = IsSeparatelyPack(localPath);
                EditorGUI.LabelField(separateLabelRect, "分散:");
                EditorGUI.BeginChangeCheck();
                bool localNewSeparate = EditorGUI.Toggle(separateToggleRect, localIsSeparate);
                if (EditorGUI.EndChangeCheck())
                    SetSeparatelyPack(localPath, localNewSeparate);

                if (GUI.Button(btnRect, "移除"))
                {
                    _localPathsList.serializedProperty.DeleteArrayElementAtIndex(index);
                    AddToUnassigned(localPath, isLocal: true);
                }
            };
            _localPathsList.elementHeight = EditorGUIUtility.singleLineHeight;

            _remotePathsList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("RemoteAseetsPaths"), true, true, false, false);
            _remotePathsList.drawHeaderCallback = rect => { };
            _remotePathsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index >= _remotePathsList.serializedProperty.arraySize)
                    return;
                SerializedProperty element = _remotePathsList.serializedProperty.GetArrayElementAtIndex(index);
                float btnWidth = 50;
                float separateLabelWidth = 35;
                float separateToggleWidth = 20;
                float separateTotalWidth = separateLabelWidth + separateToggleWidth + 5;
                float propWidth = rect.width - btnWidth - separateTotalWidth - 10;
                Rect propRect = new Rect(rect.x, rect.y, propWidth, EditorGUIUtility.singleLineHeight);
                Rect separateLabelRect = new Rect(propRect.xMax + 5, rect.y, separateLabelWidth,
                    EditorGUIUtility.singleLineHeight);
                Rect separateToggleRect = new Rect(separateLabelRect.xMax, rect.y, separateToggleWidth,
                    EditorGUIUtility.singleLineHeight);
                Rect btnRect = new Rect(separateToggleRect.xMax + 5, rect.y, btnWidth,
                    EditorGUIUtility.singleLineHeight);

                GUI.enabled = false;
                EditorGUI.PropertyField(propRect, element, GUIContent.none);
                GUI.enabled = true;

                string remotePath = element.stringValue;
                bool remoteIsSeparate = IsSeparatelyPack(remotePath);
                EditorGUI.LabelField(separateLabelRect, "分散:");
                EditorGUI.BeginChangeCheck();
                bool remoteNewSeparate = EditorGUI.Toggle(separateToggleRect, remoteIsSeparate);
                if (EditorGUI.EndChangeCheck())
                    SetSeparatelyPack(remotePath, remoteNewSeparate);

                if (GUI.Button(btnRect, "移除"))
                {
                    _remotePathsList.serializedProperty.DeleteArrayElementAtIndex(index);
                    AddToUnassigned(remotePath, isRemote: true);
                }
            };
            _remotePathsList.elementHeight = EditorGUIUtility.singleLineHeight;
        }

        private bool IsAllSame(string propertyName, bool targetValue)
        {
            SerializedProperty unassignedProp = serializedObject.FindProperty("UnassignedAssetsPath");
            if (unassignedProp.arraySize == 0) return false;
            for (int i = 0; i < unassignedProp.arraySize; i++)
            {
                if (unassignedProp.GetArrayElementAtIndex(i).FindPropertyRelative(propertyName).boolValue !=
                    targetValue)
                    return false;
            }

            return true;
        }

        private void SetAllUnassigned(bool beLocal, bool beRemote)
        {
            SerializedProperty unassignedProp = serializedObject.FindProperty("UnassignedAssetsPath");
            for (int i = 0; i < unassignedProp.arraySize; i++)
            {
                SerializedProperty element = unassignedProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("BeLocal").boolValue = beLocal;
                element.FindPropertyRelative("BeRemote").boolValue = beRemote;
            }
        }

        private void AddToUnassigned(string path, bool isLocal = false, bool isRemote = false)
        {
            SerializedProperty unassignedProp = serializedObject.FindProperty("UnassignedAssetsPath");

            for (int i = 0; i < unassignedProp.arraySize; i++)
            {
                if (unassignedProp.GetArrayElementAtIndex(i).FindPropertyRelative("GroupName").stringValue == path)
                    return;
            }

            int newIndex = unassignedProp.arraySize;
            unassignedProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = unassignedProp.GetArrayElementAtIndex(newIndex);
            newElement.FindPropertyRelative("GroupName").stringValue = path;
            newElement.FindPropertyRelative("BeLocal").boolValue = false;
            newElement.FindPropertyRelative("BeRemote").boolValue = false;
        }

        private bool IsSeparatelyPack(string path)
        {
            SerializedProperty separatelyProp = serializedObject.FindProperty("SeparatelyPackAssetsPaths");
            for (int i = 0; i < separatelyProp.arraySize; i++)
            {
                if (separatelyProp.GetArrayElementAtIndex(i).stringValue == path)
                    return true;
            }

            return false;
        }

        private void SetSeparatelyPack(string path, bool pack)
        {
            SerializedProperty separatelyProp = serializedObject.FindProperty("SeparatelyPackAssetsPaths");
            if (pack)
            {
                for (int i = 0; i < separatelyProp.arraySize; i++)
                {
                    if (separatelyProp.GetArrayElementAtIndex(i).stringValue == path)
                        return;
                }

                int newIndex = separatelyProp.arraySize;
                separatelyProp.InsertArrayElementAtIndex(newIndex);
                separatelyProp.GetArrayElementAtIndex(newIndex).stringValue = path;
            }
            else
            {
                for (int i = separatelyProp.arraySize - 1; i >= 0; i--)
                {
                    if (separatelyProp.GetArrayElementAtIndex(i).stringValue == path)
                    {
                        separatelyProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }
            }
        }

        private void DrawStringArrayWithReorderableList(ReorderableList list, string foldoutTitle,
            SerializedProperty prop)
        {
            prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, foldoutTitle);
            if (prop.isExpanded)
            {
                EditorGUI.indentLevel++;
                list.DoLayoutList();
                EditorGUI.indentLevel--;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    EditorGUILayout.Space(10);

                    if (prop.name == "UnassignedAssetsPath")
                    {
                        int count = prop.arraySize;
                        if (count > 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUI.contentColor = Color.red;
                            _showUnassignedList = EditorGUILayout.Foldout(_showUnassignedList, "尚未分配本地/远程的资源目录");
                            GUILayout.FlexibleSpace();

                            EditorGUILayout.LabelField($"({count})", GUILayout.Width(30));
                            GUI.contentColor = Color.white;
                            EditorGUILayout.EndHorizontal();

                            GUI.contentColor = Color.red;
                            EditorGUILayout.LabelField("以下资源目录尚未分配至本地或远程,请及时处理！");
                            EditorGUILayout.LabelField("设置完毕后,请点击上方的整理资源目录按钮,以保存设置并刷新资源目录列表");
                            GUI.contentColor = Color.white;

                            if (_showUnassignedList)
                            {
                                bool allLocal = IsAllSame("BeLocal", true);
                                bool allRemote = IsAllSame("BeRemote", true);

                                EditorGUILayout.BeginHorizontal();
                                EditorGUI.BeginChangeCheck();
                                bool newAllLocal = EditorGUILayout.Toggle("全部本地", allLocal);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetAllUnassigned(beLocal: newAllLocal, beRemote: false);
                                }

                                EditorGUI.BeginChangeCheck();
                                bool newAllRemote = EditorGUILayout.Toggle("全部远程", allRemote);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SetAllUnassigned(beLocal: false, beRemote: newAllRemote);
                                }

                                EditorGUILayout.EndHorizontal();
                            }

                            if (_showUnassignedList)
                            {
                                EditorGUI.indentLevel++;
                                _unassignedList.DoLayoutList();
                                EditorGUI.indentLevel--;
                            }
                        }

                        continue;
                    }

                    if (prop.name == "AAAssetsPath")
                    {
                        EditorGUILayout.Space(20);

                        GUI.contentColor = Color.red;
                        if (GUILayout.Button("整理资源目录") && !Application.isPlaying)
                        {
                            (this.target as HMAddressablesConfig)?.OrganizeAssetsPaths();
                        }

                        GUI.contentColor = Color.white;

                        EditorGUILayout.Space(20);

                        prop.isExpanded = EditorGUILayout.Foldout(prop.isExpanded, "AA资源目录");
                        if (prop.isExpanded)
                        {
                            EditorGUI.indentLevel++;
                            _aaAssetsList.DoLayoutList();
                            EditorGUI.indentLevel--;
                        }

                        continue;
                    }

                    if (prop.name == "LocalAseetsPaths")
                    {
                        DrawStringArrayWithReorderableList(_localPathsList,
                            $"要包含在APP中的资源目录:{_localPathsList.serializedProperty.arraySize}", prop);
                        continue;
                    }

                    if (prop.name == "RemoteAseetsPaths")
                    {
                        DrawStringArrayWithReorderableList(_remotePathsList,
                            $"要远程下载的资源目录:{_remotePathsList.serializedProperty.arraySize}", prop);
                        continue;
                    }

                    bool readOnly = ReadonlyFields.Contains(prop.name);
                    if (readOnly)
                        GUI.enabled = false;

                    EditorGUILayout.PropertyField(prop, true);

                    if (readOnly)
                        GUI.enabled = true;
                } while (prop.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}