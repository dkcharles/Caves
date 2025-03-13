using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main controller for the cave generation system.
/// Coordinates between parameters, grid generation, and visualisation.
/// </summary>
public class CaveGenerator : MonoBehaviour
{
    // Configuration
    [SerializeField] private CaveParameters parameters;
    [SerializeField] private CaveVisualiser visualiser;
    
    // Events
    public event Action<int[,]> OnCaveGenerated;
    public event Action<int, int> OnGenerationStarted;
    
    // Runtime references
    private CaveGrid _grid;
    private System.Random _random;
    private int _seed;
    
    private void Awake()
    {
        _grid = new CaveGrid();
        ValidateReferences();
    }
    
    private void Start()
    {
        if (parameters.generateOnStart)
        {
            GenerateCave();
        }
    }
    
    private void ValidateReferences()
    {
        if (parameters == null)
        {
            Debug.LogWarning("CaveParameters not assigned. Creating default parameters.");
            parameters = new CaveParameters();
        }
        
        if (visualiser == null)
        {
            visualiser = GetComponent<CaveVisualiser>();
            if (visualiser == null && parameters.visualise3D)
            {
                Debug.LogWarning("CaveVisualiser not found but visualisation is enabled. Adding component.");
                visualiser = gameObject.AddComponent<CaveVisualiser>();
            }
        }
    }
    
    /// <summary>
    /// Public method to generate a new cave.
    /// </summary>
    public void GenerateCave()
    {
        Debug.Log("Starting cave generation...");
        
        // Initialise the random number generator
        InitialiseRandomSeed();
        
        // Notify that generation has started
        OnGenerationStarted?.Invoke(parameters.width, parameters.height);
        
        // Clean up existing visualisation if necessary
        if (parameters.visualise3D && visualiser != null)
        {
            visualiser.ClearVisualisation();
        }
        
        // Generate the cave
        int[,] map = GenerateCaveMap();
        
        // Process the generated map
        map = ProcessCaveMap(map);
        
        // Store the result in the grid
        _grid.SetMap(map, parameters.width, parameters.height);
        
        // Place start and end points
        PlaceStartAndEndPoints();
        
        // Save the map to a file if requested
        if (parameters.saveMapToFile)
        {
            SaveMapToFile();
        }
        
        // Visualise the cave
        if (parameters.visualise3D && visualiser != null)
        {
            visualiser.VisualiseCave(_grid.GetMap(), parameters);
        }
        
        // Notify that generation is complete
        OnCaveGenerated?.Invoke(_grid.GetMap());
        
        Debug.Log("Cave generation complete.");
    }
    
    private void InitialiseRandomSeed()
    {
        if (parameters.useCustomSeed)
        {
            _seed = parameters.customSeed;
            Debug.Log($"Using custom seed: {_seed}");
        }
        else
        {
            // Use current time-based seed for better randomisation
            _seed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            Debug.Log($"Using generated seed: {_seed}");
        }
        
        _random = new System.Random(_seed);
    }
    
    private int[,] GenerateCaveMap()
    {
        Debug.Log($"Generating initial cave map ({parameters.width}x{parameters.height})...");
        
        // Create and initialise the map
        int[,] map = new int[parameters.height, parameters.width];
        
        // Fill the map borders with walls
        FillMapBorders(map);
        
        // Randomly fill the interior with walls based on fill probability
        FillMapInterior(map);
        
        // Apply cellular automata smoothing
        for (int i = 0; i < parameters.smoothIterations; i++)
        {
            map = CaveAlgorithms.SmoothMap(
                map, 
                parameters.birthLimit, 
                parameters.deathLimit, 
                parameters.useWeightedSmoothing,
                parameters.cardinalWeight,
                parameters.diagonalWeight
            );
            
            Debug.Log($"Smoothing iteration {i+1}/{parameters.smoothIterations} complete");
        }
        
        return map;
    }
    
    private void FillMapBorders(int[,] map)
    {
        // Fill horizontal borders
        for (int x = 0; x < parameters.width; x++)
        {
            map[0, x] = CaveGrid.WALL;
            map[parameters.height - 1, x] = CaveGrid.WALL;
        }
        
        // Fill vertical borders
        for (int y = 0; y < parameters.height; y++)
        {
            map[y, 0] = CaveGrid.WALL;
            map[y, parameters.width - 1] = CaveGrid.WALL;
        }
    }
    
    private void FillMapInterior(int[,] map)
    {
        for (int y = 1; y < parameters.height - 1; y++)
        {
            for (int x = 1; x < parameters.width - 1; x++)
            {
                map[y, x] = _random.NextDouble() < parameters.fillProbability ? 
                    CaveGrid.WALL : CaveGrid.PATH;
            }
        }
    }
    
    private int[,] ProcessCaveMap(int[,] map)
    {
        // Ensure connectivity between areas
        CaveAlgorithms.EnsureConnectivity(map, parameters.minRoomSize, _random);
        
        // Remove small wall clusters
        CaveAlgorithms.RemoveSmallWallClusters(map, 3);
        
        // Generate features if requested
        if (parameters.generateRooms || parameters.generateChambers)
        {
            Debug.Log("Generating additional cave features...");
            GenerateFeatures(map);
        }
        
        // Validate floor percentage
        int totalInteriorCells = (parameters.width - 2) * (parameters.height - 2);
        int floorTiles = CaveAlgorithms.CountFloorTiles(map);
        float floorPercentage = (float)floorTiles / totalInteriorCells;
        
        Debug.Log($"Floor percentage: {floorPercentage:P2} ({floorTiles}/{totalInteriorCells})");
        
        // If the floor percentage is too low, create some open areas
        if (floorPercentage < parameters.minFloorPercentage)
        {
            Debug.LogWarning("Floor percentage too low. Creating additional open areas.");
            CaveAlgorithms.ForceClearCentralArea(map, parameters.width, parameters.height, _random);
        }
        
        return map;
    }
    
    private void GenerateFeatures(int[,] map)
    {
        if (parameters.generateRooms)
        {
            Debug.Log($"Generating {parameters.numberOfRooms} rectangular rooms...");
            for (int i = 0; i < parameters.numberOfRooms; i++)
            {
                int roomWidth = _random.Next(parameters.roomSizeRange.x, parameters.roomSizeRange.y + 1);
                int roomHeight = _random.Next(parameters.roomSizeRange.x, parameters.roomSizeRange.y + 1);
                int startX = _random.Next(1, parameters.width - roomWidth - 1);
                int startY = _random.Next(1, parameters.height - roomHeight - 1);
                
                CaveAlgorithms.DrawRectangle(map, startX, startY, roomWidth, roomHeight);
                Debug.Log($"Created room {i + 1}: {roomWidth}x{roomHeight} at ({startX}, {startY})");
            }
        }
        
        if (parameters.generateChambers)
        {
            Debug.Log($"Generating {parameters.numberOfChambers} circular chambers...");
            for (int i = 0; i < parameters.numberOfChambers; i++)
            {
                int radius = _random.Next(parameters.chamberRadiusRange.x, parameters.chamberRadiusRange.y + 1);
                int centerX = _random.Next(radius + 1, parameters.width - radius - 1);
                int centerY = _random.Next(radius + 1, parameters.height - radius - 1);
                
                CaveAlgorithms.DrawCircle(map, centerX, centerY, radius, parameters.width, parameters.height);
                Debug.Log($"Created chamber {i + 1}: radius {radius} at ({centerX}, {centerY})");
            }
        }
    }
    
    private void PlaceStartAndEndPoints()
    {
        Debug.Log("Placing start and end points...");
        
        int[,] map = _grid.GetMap();
        List<(int x, int y, int size)> rooms = CaveAlgorithms.FindRooms(map);
        
        if (rooms.Count == 0)
        {
            Debug.LogError("No suitable rooms found for placing start/end points");
            return;
        }
        
        // Sort rooms by size (largest first)
        rooms.Sort((a, b) => b.size.CompareTo(a.size));
        
        // Get a list of accessible cells in the largest room
        List<(int x, int y)> accessibleCells = CaveAlgorithms.GetAccessibleCells(map, rooms[0].x, rooms[0].y);
        
        if (accessibleCells.Count < 2)
        {
            Debug.LogError("Not enough accessible cells for start/end points");
            return;
        }
        
        // Find two points that are far apart
        (int startX, int startY, int endX, int endY) = CaveAlgorithms.FindDistantPoints(accessibleCells, _random);
        
        // Mark the points on the map
        map[startY, startX] = CaveGrid.START_POINT;
        map[endY, endX] = CaveGrid.END_POINT;
        
        Debug.Log($"Start point placed at ({startX}, {startY})");
        Debug.Log($"End point placed at ({endX}, {endY})");
    }
    
    private void SaveMapToFile()
    {
        string filename = $"cave_map_seed{_seed}.txt";
        _grid.SaveMapToFile(filename);
    }
    
    /// <summary>
    /// Get the current cave grid.
    /// </summary>
    public CaveGrid GetGrid()
    {
        return _grid;
    }
    
    /// <summary>
    /// Get the seed used for the current generation.
    /// </summary>
    public int GetSeed()
    {
        return _seed;
    }
}