using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom inspector for the CaveGenerator component.
/// </summary>
[CustomEditor(typeof(CaveGenerator))]
public class CaveGeneratorEditor : Editor
{
    // Foldout states
    private bool _basicSettingsFoldout = true;
    private bool _smoothingFoldout = true;
    private bool _featuresFoldout = true;
    private bool _visualisationFoldout = true;
    private bool _seedFoldout = true;
    private bool _presetsFoldout = true;
    
    // Properties
    private SerializedProperty _parametersProperty;
    private SerializedProperty _visualiserProperty;
    
    // References
    private CaveGenerator _caveGenerator;
    private CaveGenerationPreset _selectedPreset;
    
    private void OnEnable()
    {
        _caveGenerator = (CaveGenerator)target;
        
        // Get serialized properties
        _parametersProperty = serializedObject.FindProperty("parameters");
        _visualiserProperty = serializedObject.FindProperty("visualiser");
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space();
        DrawPresetSection();
        EditorGUILayout.Space();
        
        // Basic settings
        _basicSettingsFoldout = EditorGUILayout.Foldout(_basicSettingsFoldout, "Basic Settings", true);
        if (_basicSettingsFoldout)
        {
            EditorGUI.indentLevel++;
            
            SerializedProperty widthProp = _parametersProperty.FindPropertyRelative("width");
            SerializedProperty heightProp = _parametersProperty.FindPropertyRelative("height");
            
            EditorGUILayout.PropertyField(widthProp);
            EditorGUILayout.PropertyField(heightProp);
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("fillProbability"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("smoothIterations"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("minRoomSize"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("minFloorPercentage"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("generateOnStart"));
            
            EditorGUI.indentLevel--;
        }
        
        // Seed settings
        _seedFoldout = EditorGUILayout.Foldout(_seedFoldout, "Seed Settings", true);
        if (_seedFoldout)
        {
            EditorGUI.indentLevel++;
            
            SerializedProperty useCustomSeedProp = _parametersProperty.FindPropertyRelative("useCustomSeed");
            EditorGUILayout.PropertyField(useCustomSeedProp);
            
            if (useCustomSeedProp.boolValue)
            {
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("customSeed"));
            }
            
            EditorGUI.indentLevel--;
        }
        
        // Smoothing settings
        _smoothingFoldout = EditorGUILayout.Foldout(_smoothingFoldout, "Smoothing Settings", true);
        if (_smoothingFoldout)
        {
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("birthLimit"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("deathLimit"));
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("useWeightedSmoothing"));
            
            SerializedProperty useWeightedProp = _parametersProperty.FindPropertyRelative("useWeightedSmoothing");
            if (useWeightedProp.boolValue)
            {
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("cardinalWeight"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("diagonalWeight"));
            }
            
            EditorGUI.indentLevel--;
        }
        
        // Features settings
        _featuresFoldout = EditorGUILayout.Foldout(_featuresFoldout, "Cave Features", true);
        if (_featuresFoldout)
        {
            EditorGUI.indentLevel++;
            
            // Rooms
            SerializedProperty generateRoomsProp = _parametersProperty.FindPropertyRelative("generateRooms");
            EditorGUILayout.PropertyField(generateRoomsProp);
            
            if (generateRoomsProp.boolValue)
            {
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("numberOfRooms"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("roomSizeRange"));
            }
            
            // Chambers
            SerializedProperty generateChambersProp = _parametersProperty.FindPropertyRelative("generateChambers");
            EditorGUILayout.PropertyField(generateChambersProp);
            
            if (generateChambersProp.boolValue)
            {
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("numberOfChambers"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("chamberRadiusRange"));
            }
            
            EditorGUI.indentLevel--;
        }
        
        // Visualisation settings
        _visualisationFoldout = EditorGUILayout.Foldout(_visualisationFoldout, "Visualisation", true);
        if (_visualisationFoldout)
        {
            EditorGUI.indentLevel++;
            
            SerializedProperty visualise3DProp = _parametersProperty.FindPropertyRelative("visualise3D");
            EditorGUILayout.PropertyField(visualise3DProp);
            
            if (visualise3DProp.boolValue)
            {
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("cellSize"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("wallHeight"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("wallPrefab"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("floorPrefab"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("startPointPrefab"));
                EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("endPointPrefab"));
                EditorGUILayout.PropertyField(_visualiserProperty);
            }
            
            EditorGUILayout.PropertyField(_parametersProperty.FindPropertyRelative("saveMapToFile"));
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        // Buttons
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Generate Cave", GUILayout.Height(30)))
        {
            _caveGenerator.GenerateCave();
            
            // This will refresh the scene view to show the changes
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(target);
                SceneView.RepaintAll();
            }
        }
        
        if (GUILayout.Button("Save As Preset", GUILayout.Height(30)))
        {
            SaveCurrentAsPreset();
        }
        
        EditorGUILayout.EndHorizontal();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawPresetSection()
    {
        _presetsFoldout = EditorGUILayout.Foldout(_presetsFoldout, "Presets", true);
        if (_presetsFoldout)
        {
            EditorGUI.indentLevel++;
            
            // Preset selection field
            _selectedPreset = EditorGUILayout.ObjectField("Selected Preset", _selectedPreset, typeof(CaveGenerationPreset), false) as CaveGenerationPreset;
            
            // Apply preset button
            EditorGUI.BeginDisabledGroup(_selectedPreset == null);
            if (GUILayout.Button("Apply Preset"))
            {
                if (_selectedPreset != null)
                {
                    // Apply the preset
                    Undo.RecordObject(_caveGenerator, "Apply Cave Preset");
                    _selectedPreset.ApplyToGenerator(_caveGenerator);
                }
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUI.indentLevel--;
        }
    }
    
    private void SaveCurrentAsPreset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Cave Preset",
            "New Cave Preset",
            "asset",
            "Save the current cave generation parameters as a preset"
        );
        
        if (string.IsNullOrEmpty(path))
            return;
            
        // Create a new preset asset
        CaveGenerationPreset preset = CreateInstance<CaveGenerationPreset>();
        
        // TODO: Copy the current parameters to the preset
        // This would need to be implemented in CaveGenerationPreset
        
        // Save the asset
        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();
        
        // Select the new asset
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = preset;
    }
}