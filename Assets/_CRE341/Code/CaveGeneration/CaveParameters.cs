using System;
using UnityEngine;

/// <summary>
/// Holds all parameters for cave generation.
/// </summary>
[Serializable]
public class CaveParameters
{
    // Basic generation parameters
    [Header("Basic Generation")]
    public int width = 128;
    public int height = 128;
    
    [Range(0.1f, 0.9f)]
    public float fillProbability = 0.45f;
    
    [Range(1, 10)]
    public int smoothIterations = 5;
    
    public int minRoomSize = 20;
    public bool generateOnStart = true;
    
    [Range(0.1f, 0.5f)]
    public float minFloorPercentage = 0.2f;
    
    // Seed settings
    [Header("Random Seed")]
    public bool useCustomSeed = false;
    public int customSeed = 0;
    
    // Smoothing parameters
    [Header("Smoothing Parameters")]
    [Range(0, 8)]
    public int birthLimit = 4;
    
    [Range(0, 8)]
    public int deathLimit = 4;
    
    public bool useWeightedSmoothing = true;
    
    [Range(0.5f, 1.5f)]
    public float cardinalWeight = 1.0f;
    
    [Range(0.5f, 1.5f)]
    public float diagonalWeight = 0.7f;
    
    // 3D Visualisation settings
    [Header("Visualisation")]
    public bool visualise3D = true;
    public float cellSize = 1.0f;
    public float wallHeight = 2.0f;
    
    // Prefab references (these should be assigned in the Inspector)
    [Header("Prefabs")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject startPointPrefab;
    public GameObject endPointPrefab;
    
    // Feature generation
    [Header("Cave Features")]
    public bool generateRooms = false;
    public int numberOfRooms = 3;
    public Vector2Int roomSizeRange = new Vector2Int(5, 15);
    
    public bool generateChambers = false;
    public int numberOfChambers = 2;
    public Vector2Int chamberRadiusRange = new Vector2Int(3, 8);
    
    // File options
    [Header("File Options")]
    public bool saveMapToFile = true;
    
    /// <summary>
    /// Validate parameters to ensure they make sense.
    /// </summary>
    public void Validate()
    {
        // Ensure width and height are positive
        width = Mathf.Max(10, width);
        height = Mathf.Max(10, height);
        
        // Ensure room size range is valid
        roomSizeRange.x = Mathf.Max(3, roomSizeRange.x);
        roomSizeRange.y = Mathf.Max(roomSizeRange.x, roomSizeRange.y);
        
        // Ensure chamber radius range is valid
        chamberRadiusRange.x = Mathf.Max(2, chamberRadiusRange.x);
        chamberRadiusRange.y = Mathf.Max(chamberRadiusRange.x, chamberRadiusRange.y);
        
        // Ensure the number of rooms and chambers is reasonable
        numberOfRooms = Mathf.Clamp(numberOfRooms, 0, 20);
        numberOfChambers = Mathf.Clamp(numberOfChambers, 0, 20);
    }
}