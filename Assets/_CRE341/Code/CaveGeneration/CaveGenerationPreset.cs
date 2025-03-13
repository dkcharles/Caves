using UnityEngine;

/// <summary>
/// ScriptableObject for storing and reusing cave generation parameters.
/// </summary>
[CreateAssetMenu(fileName = "New Cave Preset", menuName = "Cave Generation/Cave Preset")]
public class CaveGenerationPreset : ScriptableObject
{
    [SerializeField] private CaveParameters parameters;
    
    /// <summary>
    /// Get the parameters from this preset.
    /// </summary>
    public CaveParameters GetParameters()
    {
        return parameters;
    }
    
    /// <summary>
    /// Create a copy of the parameters from this preset.
    /// </summary>
    public CaveParameters GetParametersCopy()
    {
        // Create a new instance and copy values
        CaveParameters copy = new CaveParameters
        {
            // Basic generation
            width = parameters.width,
            height = parameters.height,
            fillProbability = parameters.fillProbability,
            smoothIterations = parameters.smoothIterations,
            minRoomSize = parameters.minRoomSize,
            generateOnStart = parameters.generateOnStart,
            minFloorPercentage = parameters.minFloorPercentage,
            
            // Seed settings
            useCustomSeed = parameters.useCustomSeed,
            customSeed = parameters.customSeed,
            
            // Smoothing parameters
            birthLimit = parameters.birthLimit,
            deathLimit = parameters.deathLimit,
            useWeightedSmoothing = parameters.useWeightedSmoothing,
            cardinalWeight = parameters.cardinalWeight,
            diagonalWeight = parameters.diagonalWeight,
            
            // Visualisation
            visualise3D = parameters.visualise3D,
            cellSize = parameters.cellSize,
            wallHeight = parameters.wallHeight,
            
            // Prefabs
            wallPrefab = parameters.wallPrefab,
            floorPrefab = parameters.floorPrefab,
            startPointPrefab = parameters.startPointPrefab,
            endPointPrefab = parameters.endPointPrefab,
            
            // Features
            generateRooms = parameters.generateRooms,
            numberOfRooms = parameters.numberOfRooms,
            roomSizeRange = parameters.roomSizeRange,
            generateChambers = parameters.generateChambers,
            numberOfChambers = parameters.numberOfChambers,
            chamberRadiusRange = parameters.chamberRadiusRange,
            
            // File options
            saveMapToFile = parameters.saveMapToFile
        };
        
        return copy;
    }
    
    /// <summary>
    /// Apply the parameters from this preset to a CaveGenerator.
    /// </summary>
    public void ApplyToGenerator(CaveGenerator generator)
    {
        if (generator == null)
            return;
            
        // Apply the parameters
        CaveParameters currentParams = generator.GetComponent<CaveParameters>();
        if (currentParams != null)
        {
            // Copy parameters values
            CaveParameters copy = GetParametersCopy();
            
            // Apply to the generator
            generator.SendMessage("SetParameters", copy, SendMessageOptions.DontRequireReceiver);
        }
    }
}