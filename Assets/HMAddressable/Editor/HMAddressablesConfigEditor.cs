using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Presets;
using UnityEditorInternal;
using UnityEngine;

namespace HM
{
    [CustomEditor(typeof(HMAddressablesConfig))]
    public class HMAddressablesConfigEditor : UnityEditor.Editor
    {
        private const string PresetAssetDirectory = "Assets/HMAddressables";

        private static readonly HashSet<string> ReadonlyFields = new HashSet<string>
        {
            nameof(HMAddressablesConfig.SeparatelyPackAssetsPaths),
            nameof(HMAddressablesConfig.LabelPackAssetsPaths),
        };

        private ReorderableList _unassignedList;
        private ReorderableList _aaAssetsList;
        private ReorderableList _localPathsList;
        private ReorderableList _remotePathsList;
        private bool _showUnassignedList;
        private readonly Dictionary<string, string> _folderPaths = new Dictionary<string, string>();

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
                float rowHeight = EditorGUIUtility.singleLineHeight;
                Rect groupValueRect = new Rect(rect.x, rect.y, rect.width, rowHeight);
                rect.y += rowHeight + EditorGUIUtility.standardVerticalSpacing;
                rect.height = rowHeight;
                float rightStart = rect.x;
                Rect localLabelRect = new Rect(rightStart, rect.y, toggleLabelWidth, rect.height);
                Rect localToggleRect = new Rect(localLabelRect.xMax, rect.y, toggleWidth, rect.height);
                Rect remoteLabelRect = new Rect(localToggleRect.xMax, rect.y, toggleLabelWidth, rect.height);
                Rect remoteToggleRect = new Rect(remoteLabelRect.xMax, rect.y, toggleWidth, rect.height);
                Rect packRect = new Rect(remoteToggleRect.xMax + 5, rect.y, PackOptionsWidth, rect.height);

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

                DrawPackOptions(packRect, groupName.stringValue);
            };
            _unassignedList.elementHeight = EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;

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
                const float btnWidth = 50;
                var btnRect = new Rect(rect.xMax - btnWidth, rect.y, btnWidth, rect.height);
                var packRect = new Rect(btnRect.x - PackOptionsWidth - 5, rect.y, PackOptionsWidth, rect.height);
                var propRect = new Rect(rect.x, rect.y, Mathf.Max(0, packRect.x - rect.x - 5), rect.height);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.PropertyField(propRect, element, GUIContent.none);
                }
                string localPath = element.stringValue;
                DrawPackOptions(packRect, localPath);

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
                const float btnWidth = 50;
                var btnRect = new Rect(rect.xMax - btnWidth, rect.y, btnWidth, rect.height);
                var packRect = new Rect(btnRect.x - PackOptionsWidth - 5, rect.y, PackOptionsWidth, rect.height);
                var propRect = new Rect(rect.x, rect.y, Mathf.Max(0, packRect.x - rect.x - 5), rect.height);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.PropertyField(propRect, element, GUIContent.none);
                }
                string remotePath = element.stringValue;
                DrawPackOptions(packRect, remotePath);

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

        private const float PackOptionsWidth = 130;
        private static readonly GUIContent SeparateOption = new GUIContent("分散", "每个直属资源单独打包。");
        private static readonly GUIContent LabelOption = new GUIContent("按 Label", "母包按大小分片，并替换直属资源的全部 Label。");

        private void DrawPackOptions(Rect rect, string path)
        {
            var separate = serializedObject.FindProperty(nameof(HMAddressablesConfig.SeparatelyPackAssetsPaths));
            var labels = serializedObject.FindProperty(nameof(HMAddressablesConfig.LabelPackAssetsPaths));
            bool oldSeparate = FindPackFolder(separate, path) >= 0;
            bool oldLabel = FindPackFolder(labels, path) >= 0;
            bool useSeparate = GUI.Toggle(new Rect(rect.x, rect.y, 56, rect.height), oldSeparate, SeparateOption);
            bool useLabel = GUI.Toggle(new Rect(rect.x + 56, rect.y, rect.width - 56, rect.height), oldLabel, LabelOption);
            if (useSeparate != oldSeparate)
            {
                SetPackFolder(separate, path, useSeparate);
                if (useSeparate)
                {
                    SetPackFolder(labels, path, false);
                }
            }
            else if (useLabel != oldLabel)
            {
                SetPackFolder(labels, path, useLabel);
                if (useLabel)
                {
                    SetPackFolder(separate, path, false);
                }
            }
        }

        private string GetFolderPath(string path)
        {
            path = path ?? string.Empty;
            if (!_folderPaths.TryGetValue(path, out var normalized))
            {
                normalized = HMAddressablesConfig.NormalizeFolderForComparison(path);
                _folderPaths.Add(path, normalized);
            }
            return normalized;
        }

        private int FindPackFolder(SerializedProperty list, string path)
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var value = list.GetArrayElementAtIndex(i).stringValue;
                if (string.Equals(GetFolderPath(value), GetFolderPath(path), System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void SetPackFolder(SerializedProperty list, string path, bool enabled)
        {
            int index;
            while ((index = FindPackFolder(list, path)) >= 0)
            {
                list.DeleteArrayElementAtIndex(index);
            }
            if (enabled)
            {
                int next = list.arraySize;
                list.arraySize = next + 1;
                list.GetArrayElementAtIndex(next).stringValue = path;
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

        private void DrawBundledAssetGroupSchemaPreset(SerializedProperty presetProperty)
        {
            EditorGUILayout.PropertyField(presetProperty,
                new GUIContent("BundledAssetGroupSchema Preset"));

            var preset = presetProperty.objectReferenceValue as Preset;
            if (preset == null)
            {
                EditorGUILayout.HelpBox(
                    "未指定 Preset，将使用 Addressables 默认的 BundledAssetGroupSchema 配置。",
                    MessageType.Warning);

                if (GUILayout.Button("生成 BundledAssetGroupSchema Preset"))
                {
                    CreateBundledAssetGroupSchemaPreset(presetProperty);
                }
            }
            else if (!CanApplyToBundledAssetGroupSchema(preset))
            {
                EditorGUILayout.HelpBox(
                    "当前 Preset 不能应用于 BundledAssetGroupSchema，将使用 Addressables 默认配置。",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "创建或刷新 Addressable Group 时，将先应用当前平台指定的 Preset。",
                    MessageType.Info);
            }

        }

        private static bool CanApplyToBundledAssetGroupSchema(Preset preset)
        {
            var schema = CreateInstance<BundledAssetGroupSchema>();
            try
            {
                return preset.CanBeAppliedTo(schema);
            }
            finally
            {
                DestroyImmediate(schema);
            }
        }

        private void CreateBundledAssetGroupSchemaPreset(SerializedProperty presetProperty)
        {
            if (!AssetDatabase.IsValidFolder(PresetAssetDirectory))
            {
                AssetDatabase.CreateFolder("Assets", "HMAddressables");
            }

            var config = target as HMAddressablesConfig;
            var schema = CreateInstance<BundledAssetGroupSchema>();
            try
            {
                schema.name = nameof(BundledAssetGroupSchema);
                schema.OnAfterDeserialize();
                var preset = new Preset(schema)
                {
                    excludedProperties = new[] { "m_Group" }
                };
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{PresetAssetDirectory}/{config.name}_BundledAssetGroupSchema.preset");

                AssetDatabase.CreateAsset(preset, assetPath);
                presetProperty.objectReferenceValue = preset;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(preset);
                GUIUtility.ExitGUI();
            }
            finally
            {
                DestroyImmediate(schema);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _folderPaths.Clear();

            SerializedProperty prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    EditorGUILayout.Space(10);

                    if (prop.name == "BundledAssetGroupSchemaPreset")
                    {
                        DrawBundledAssetGroupSchemaPreset(prop);
                        continue;
                    }

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

                    if (prop.name == nameof(HMAddressablesConfig.LabelSizeK))
                    {
                        EditorGUILayout.PropertyField(prop, new GUIContent("每 Label 资源大小（K）", prop.tooltip));
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

    [CustomEditor(typeof(BundledAssetGroupSchema))]
    [CanEditMultipleObjects]
    public class HMBundledAssetGroupSchemaEditor : UnityEditor.Editor
    {
        private static readonly GUIContent AssetBundleCrcLabel = new GUIContent(
            "Asset Bundle CRC",
            "设置 AssetBundle 加载时的 CRC 完整性校验方式。");

        private static readonly GUIContent[] AssetBundleCrcOptions =
        {
            new GUIContent("关闭"),
            new GUIContent("开启，包含已缓存 Bundle"),
            new GUIContent("开启，排除已缓存 Bundle")
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var property = serializedObject.GetIterator();
            if (property.NextVisible(true))
            {
                do
                {
                    if (property.name == "m_Group" ||
                        property.name == "m_UseAssetBundleCrcForCachedBundles")
                    {
                        continue;
                    }

                    if (property.name == "m_UseAssetBundleCrc")
                    {
                        DrawAssetBundleCrc();
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                } while (property.NextVisible(false));
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetBundleCrc()
        {
            var useCrcProperty = serializedObject.FindProperty("m_UseAssetBundleCrc");
            var useCrcForCachedBundlesProperty =
                serializedObject.FindProperty("m_UseAssetBundleCrcForCachedBundles");
            if (useCrcProperty == null || useCrcForCachedBundlesProperty == null)
            {
                return;
            }

            var currentValue = 0;
            if (useCrcProperty.boolValue)
            {
                currentValue = useCrcForCachedBundlesProperty.boolValue ? 1 : 2;
            }

            EditorGUI.showMixedValue = useCrcProperty.hasMultipleDifferentValues ||
                                       (useCrcProperty.boolValue &&
                                        useCrcForCachedBundlesProperty.hasMultipleDifferentValues);
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Popup(AssetBundleCrcLabel, currentValue, AssetBundleCrcOptions);
            if (EditorGUI.EndChangeCheck())
            {
                useCrcProperty.boolValue = newValue != 0;
                if (newValue != 0)
                {
                    useCrcForCachedBundlesProperty.boolValue = newValue == 1;
                }
            }

            EditorGUI.showMixedValue = false;
        }
    }
}
