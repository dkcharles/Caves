using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CaveGenerator : MonoBehaviour
{
    // Constants for cell types
    private const int WALL = 1;
    private const int PATH = 0;
    private const int START_POINT = 2;
    private const int END_POINT = 3;

    // Inspector-visible parameters
    [Header("Cave Generation Parameters")]
    [SerializeField] private int width = 128;
    [SerializeField] private int height = 128;
    [Range(0.1f, 0.9f)]
    [SerializeField] private float fillProbability = 0.45f;
    [Range(1, 10)]
    [SerializeField] private int smoothIterations = 5;
    [SerializeField] private int minRoomSize = 20;
    [SerializeField] private bool useCustomSeed = false;
    [SerializeField] private int customSeed = 0;
    [Range(0.1f, 0.5f)]
    [SerializeField] private float minFloorPercentage = 0.2f; // Minimum percentage of floor tiles
    [SerializeField] private int maxRegenerationAttempts = 5; // Maximum number of regeneration attempts

    // Add new smoothing parameters
    [Header("Smoothing Parameters")]
    [Range(0, 8)]
    [SerializeField] private int birthLimit = 4; // Number of neighbors needed for a wall to be created
    [Range(0, 8)]
    [SerializeField] private int deathLimit = 4; // Number of neighbors needed for a wall to survive
    [SerializeField] private bool useWeightedSmoothing = true;
    [Range(0.5f, 1.5f)]
    [SerializeField] private float cardinalWeight = 1.0f; // Weight for N,S,E,W neighbors
    [Range(0.5f, 1.5f)]
    [SerializeField] private float diagonalWeight = 0.7f; // Weight for diagonal neighbors

    // New variables for 3D visualization
    [Header("3D Visualization")]
    [SerializeField] private bool visualize3D = true;
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private GameObject startPointPrefab;
    [SerializeField] private GameObject endPointPrefab;
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private float wallHeight = 2.0f;
    [SerializeField] private Transform caveParent;
    
    
    // Terrain generation parameters ~ not used yet as its not working well
    // [Header("Terrain Generation")]
    private bool generateTerrain = false;
    private float terrainHeight = 20f;
    [Range(0f, 1f)]
    private float noiseScale = 0.1f;
    [Range(0f, 1f)]
    private float smoothness = 0.5f;
    private Material terrainMaterial;
    private bool addTerrainCollider = true;

    [Header("Cave Features")]
    [SerializeField] private bool generateRooms = false;
    [SerializeField] private int numberOfRooms = 3;
    [SerializeField] private Vector2Int roomSizeRange = new Vector2Int(5, 15);
    [SerializeField] private bool generateChambers = false;
    [SerializeField] private int numberOfChambers = 2;
    [SerializeField] private Vector2Int chamberRadiusRange = new Vector2Int(3, 8);

    // Private variables
    private System.Random random;
    private int seed;
    private int[,] map;
    private Terrain caveTerrain;
    private GameObject terrainObject;

    void Start()
    {
        GenerateCave();
    }
    
    // Public method to regenerate the cave (can be called from UI)
    public void RegenerateCave()
    {
        // Clean up existing objects
        if (generateTerrain)
        {
            CleanupTerrain();
        }
        else if (visualize3D && caveParent != null)
        {
            // Clean up 3D visualization objects
            while (caveParent.childCount > 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(caveParent.GetChild(0).gameObject);
                }
                else
                {
                    DestroyImmediate(caveParent.GetChild(0).gameObject);
                }
            }
        }
        
        // Generate new cave
        GenerateCave();
    }
    
    private void CleanupTerrain()
    {
        // Destroy existing terrain object if it exists
        if (terrainObject != null)
        {
            if (Application.isPlaying)
            {
                Destroy(terrainObject);
            }
            else
            {
                DestroyImmediate(terrainObject);
            }
            terrainObject = null;
            caveTerrain = null;
        }
    }

    public void GenerateCave()
    {
        Debug.Log("Cave Map Generator");
        Debug.Log("------------------");
        
        InitializeRandomSeed();
        
        // Track generation attempts
        int totalAttempts = 0;
        const int MAX_TOTAL_ATTEMPTS = 10;
        bool validMapGenerated = false;
        
        while (!validMapGenerated && totalAttempts < MAX_TOTAL_ATTEMPTS)
        {
            // Generate and process the map
            map = GenerateCaveMap(width, height, fillProbability, smoothIterations);
            
            // First ensure connectivity to get a usable map structure
            EnsureConnectivity(map);
            RemoveSmallWallClusters(map, 3);
            
            // Generate features AFTER connectivity is ensured
            if (generateRooms || generateChambers)
            {
                Debug.Log("Carving out rooms and chambers...");
                GenerateFeatures();
            }
            
            // FINAL VALIDATION: Check if we have enough floor area after all processing
            int totalInteriorCells = (width - 2) * (height - 2);
            int floorTiles = CountFloorTiles(map);
            float floorPercentage = (float)floorTiles / totalInteriorCells;
            
            Debug.Log($"Final floor percentage: {floorPercentage:P2} ({floorTiles}/{totalInteriorCells})");
            
            if (floorPercentage >= minFloorPercentage)
            {
                validMapGenerated = true;
                Debug.Log("Generated valid map with sufficient floor space.");
            }
            else
            {
                totalAttempts++;
                Debug.LogWarning($"Map failed final validation. Floor percentage too low: {floorPercentage:P2}. Attempt {totalAttempts}/{MAX_TOTAL_ATTEMPTS}");
                
                // Reduce fill probability even more for really bad cases
                fillProbability = Mathf.Max(0.15f, fillProbability - 0.1f);
            }
        }
        
        if (!validMapGenerated)
        {
            Debug.LogError($"Failed to generate a valid map after {MAX_TOTAL_ATTEMPTS} attempts. Using last generated map, but it may not be ideal.");
            // Last resort - force create some open areas
            ForceClearCentralArea();
        }
        
        // Add start and end points
        PlaceStartAndEndPoints(map);
        
        // Save the map
        string filename = $"cave_map_seed{seed}.txt";
        SaveMapToFile(map, filename);
        
        // Visualize
        if (visualize3D)
        {
            Visualize3DCave();
        }
    }

    private void InitializeRandomSeed()
    {
        if (useCustomSeed)
        {
            seed = customSeed;
            Debug.Log($"Using custom seed: {seed}");
        }
        else
        {
            // Get current time-based seed using DateTime ticks for better randomization
            seed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
            Debug.Log($"Using system-generated seed: {seed}");
        }
        
        random = new System.Random(seed);
    }
    
    private int[,] GenerateCaveMap(int width, int height, float fillProbability, int iterations)
    {
        return GenerateCaveMap(width, height, fillProbability, iterations, 0);
    }

    private int[,] GenerateCaveMap(int width, int height, float fillProbability, int iterations, int attempt)
    {
        if (attempt >= maxRegenerationAttempts)
        {
            Debug.LogWarning($"Failed to generate cave with sufficient floor space after {maxRegenerationAttempts} attempts. Using last generated map.");
            // Adjust fill probability for the last attempt to ensure more floor tiles
            fillProbability = Mathf.Max(0.3f, fillProbability - 0.1f);
        }

        // Step 1: Initialize with random walls
        int[,] map = new int[height, width];
        
        // Fill borders
        for (int x = 0; x < width; x++)
        {
            map[0, x] = WALL;
            map[height - 1, x] = WALL;
        }
        
        for (int y = 0; y < height; y++)
        {
            map[y, 0] = WALL;
            map[y, width - 1] = WALL;
        }
        
        // Randomly fill interior
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                map[y, x] = random.NextDouble() < fillProbability ? WALL : PATH;
            }
        }
        
        // Step 2: Apply cellular automata smoothing
        for (int i = 0; i < iterations; i++)
        {
            map = SmoothMap(map);
            // Progress reporting for large maps
            Debug.Log($"Smoothing iteration {i+1}/{iterations} complete");
        }

        // Check if we have enough floor tiles
        int totalInteriorCells = (width - 2) * (height - 2); // Exclude borders
        int minRequiredFloorTiles = Mathf.RoundToInt(totalInteriorCells * minFloorPercentage);
        int floorTiles = CountFloorTiles(map);

        Debug.Log($"Floor tiles: {floorTiles}/{totalInteriorCells} (minimum required: {minRequiredFloorTiles})");

        if (floorTiles < minRequiredFloorTiles && attempt < maxRegenerationAttempts)
        {
            Debug.Log($"Insufficient floor space ({floorTiles} < {minRequiredFloorTiles}). Regenerating map (attempt {attempt + 1}/{maxRegenerationAttempts})...");
            // Adjust fill probability to create more floor tiles
            float newFillProbability = Mathf.Max(0.1f, fillProbability - 0.05f);
            return GenerateCaveMap(width, height, newFillProbability, iterations, attempt + 1);
        }
        
        return map;
    }

    private int CountFloorTiles(int[,] map)
    {
        int count = 0;
        int height = map.GetLength(0);
        int width = map.GetLength(1);

        // Count only interior floor tiles (excluding borders)
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[y, x] == PATH)
                {
                    count++;
                }
            }
        }

        return count;
    }
    
    private int[,] SmoothMap(int[,] map)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int[,] newMap = new int[height, width];
        
        // Copy borders
        for (int x = 0; x < width; x++)
        {
            newMap[0, x] = WALL;
            newMap[height - 1, x] = WALL;
        }
        
        for (int y = 0; y < height; y++)
        {
            newMap[y, 0] = WALL;
            newMap[y, width - 1] = WALL;
        }
        
        // Apply smoothing rules
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float neighborScore = useWeightedSmoothing ? 
                    CalculateWeightedNeighbors(map, x, y) : 
                    CountAdjacentWalls(map, x, y);
                
                // Current cell is a wall
                if (map[y, x] == WALL)
                {
                    // Wall survives if it has enough neighbors
                    newMap[y, x] = neighborScore >= deathLimit ? WALL : PATH;
                }
                // Current cell is a path
                else
                {
                    // Wall is born if there are enough neighbors
                    newMap[y, x] = neighborScore > birthLimit ? WALL : PATH;
                }
            }
        }
        
        return newMap;
    }
    
    private float CalculateWeightedNeighbors(int[,] map, int x, int y)
    {
        float score = 0;
        
        // Check cardinal neighbors (N,S,E,W)
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            
            if (map[ny, nx] == WALL)
            {
                score += cardinalWeight;
            }
        }
        
        // Check diagonal neighbors
        int[] diagX = { -1, 1, -1, 1 };
        int[] diagY = { -1, -1, 1, 1 };
        
        for (int i = 0; i < 4; i++)
        {
            int nx = x + diagX[i];
            int ny = y + diagY[i];
            
            if (map[ny, nx] == WALL)
            {
                score += diagonalWeight;
            }
        }
        
        return score;
    }

    // Update the original CountAdjacentWalls to be used when weighted smoothing is disabled
    private int CountAdjacentWalls(int[,] map, int x, int y)
    {
        int count = 0;
        
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                
                int nx = x + dx;
                int ny = y + dy;
                
                if (map[ny, nx] == WALL)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    private void EnsureConnectivity(int[,] map)
    {
        Debug.Log("Identifying disconnected rooms and ensuring connectivity...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Step 1: Find all separate rooms using flood fill
        List<(int x, int y, int size)> rooms = FindRooms(map);
        
        if (rooms.Count <= 1)
        {
            Debug.Log("Map is already connected or contains no rooms.");
        }
        else
        {
            // Step 2: Sort rooms by size (largest first)
            rooms.Sort((a, b) => b.size.CompareTo(a.size));
            
            Debug.Log($"Found {rooms.Count} separate rooms. Largest room size: {rooms[0].size}");
            
            // Step 3: Handle isolated rooms
            int filledRooms = 0;
            int connectedRooms = 0;
            
            for (int i = 1; i < rooms.Count; i++)
            {
                (int x, int y, int size) = rooms[i];
                
                if (size < minRoomSize)
                {
                    // Fill in small rooms (convert back to walls)
                    FillRoom(map, x, y);
                    filledRooms++;
                }
                else
                {
                    // Connect this room to the main room
                    ConnectRooms(map, rooms[0].x, rooms[0].y, x, y);
                    connectedRooms++;
                }
            }
            
            Debug.Log($"Filled in {filledRooms} small rooms, connected {connectedRooms} larger rooms");
        }
        
        // Create additional random hallways to increase connectivity
        int hallways = width * height / 5000;
        
        for (int i = 0; i < hallways; i++)
        {
            int x = random.Next(1, width - 1);
            int y = random.Next(1, height - 1);
            
            // Create a small random hallway
            int direction = random.Next(2); // 0 = horizontal, 1 = vertical
            int length = random.Next(5, 20); // Longer hallways for larger maps
            
            if (direction == 0)
            {
                for (int j = 0; j < length && x + j < width - 1; j++)
                {
                    map[y, x + j] = PATH;
                }
            }
            else
            {
                for (int j = 0; j < length && y + j < height - 1; j++)
                {
                    map[y + j, x] = PATH;
                }
            }
        }
        
        Debug.Log($"Added {hallways} additional connectivity hallways");
    }
    
    // Find all separate rooms in the map using flood fill
    private List<(int x, int y, int size)> FindRooms(int[,] map)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        List<(int x, int y, int size)> rooms = new List<(int x, int y, int size)>();
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[y, x] == PATH && !visited[y, x])
                {
                    // Start of a new room
                    int roomSize = FloodFill(map, x, y, visited);
                    rooms.Add((x, y, roomSize));
                }
            }
        }
        
        return rooms;
    }
    
    // Flood fill algorithm to mark a room and return its size
    private int FloodFill(int[,] map, int startX, int startY, bool[,] visited)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int roomSize = 0;
        
        // Using queue for breadth-first search
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            roomSize++;
            
            // Check 4 adjacent cells (non-diagonal neighbors)
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return roomSize;
    }
    
    // Fill a room (convert it back to walls)
    private void FillRoom(int[,] map, int startX, int startY)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            map[y, x] = WALL; // Convert to wall
            
            // Check 4 adjacent cells
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }
    
    // Connect two rooms by creating a path between them
    private void ConnectRooms(int[,] map, int x1, int y1, int x2, int y2)
    {
        // Create an L-shaped corridor between the rooms
        // First horizontal part
        int xMin = Math.Min(x1, x2);
        int xMax = Math.Max(x1, x2);
        
        for (int x = xMin; x <= xMax; x++)
        {
            map[y1, x] = PATH;
        }
        
        // Then vertical part
        int yMin = Math.Min(y1, y2);
        int yMax = Math.Max(y1, y2);
        
        for (int y = yMin; y <= yMax; y++)
        {
            map[y, x2] = PATH;
        }
    }
    
    private void SaveMapToFile(int[,] map, string filename)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        Debug.Log($"Saving {width}x{height} map to {filename}...");
        
        string mapDirectory;
        string filePath;
        
        // Check if we're running in the editor
        #if UNITY_EDITOR
            // Save to a "map" folder in the Assets directory
            mapDirectory = Path.Combine(Application.dataPath, "map");
        #else
            // Save to a "map" folder in the persistent data path when running a build
            mapDirectory = Path.Combine(Application.persistentDataPath, "map");
        #endif
        
        // Create the directory if it doesn't exist
        if (!Directory.Exists(mapDirectory))
        {
            Directory.CreateDirectory(mapDirectory);
            Debug.Log($"Created directory: {mapDirectory}");
        }
        
        // Save the file in the map directory
        filePath = Path.Combine(mapDirectory, filename);
        
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char symbol;
                    switch (map[y, x])
                    {
                        case WALL: symbol = '#'; break;
                        case PATH: symbol = '.'; break;
                        case START_POINT: symbol = 'S'; break;
                        case END_POINT: symbol = 'E'; break;
                        default: symbol = '?'; break;
                    }
                    writer.Write(symbol);
                }
                writer.WriteLine();
            }
        }
        
        Debug.Log($"Map saved successfully to {filePath}");
    }
    
    private void PrintMapPreview(int[,] map)
    {
        // For Unity, we'll just log a smaller preview to the console
        int previewSize = Math.Min(50, Math.Min(map.GetLength(0), map.GetLength(1)));
        Debug.Log($"Preview of top-left {previewSize}x{previewSize} section:");
        
        string preview = "";
        for (int y = 0; y < previewSize; y++)
        {
            string line = "";
            for (int x = 0; x < previewSize; x++)
            {
                switch (map[y, x])
                {
                    case WALL: line += "X"; break;
                    case PATH: line += " "; break;
                    case START_POINT: line += "S"; break;
                    case END_POINT: line += "E"; break;
                    default: line += "?"; break;
                }
            }
            preview += line + "\n";
        }
        
        Debug.Log(preview);
    }
    
    // Place start and end points
    private void PlaceStartAndEndPoints(int[,] map)
    {
        Debug.Log("Placing start and end points...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Find the largest room to place points in (using existing room finding code)
        bool[,] visited = new bool[height, width];
        List<(int x, int y, int size)> rooms = FindRooms(map);
        
        if (rooms.Count == 0)
        {
            Debug.LogError("Error: No suitable rooms found for placing start/end points");
            return;
        }
        
        // Sort rooms by size (largest first)
        rooms.Sort((a, b) => b.size.CompareTo(a.size));
        
        // Get a list of accessible path cells in the largest room
        List<(int x, int y)> accessibleCells = GetAccessibleCells(map, rooms[0].x, rooms[0].y);
        
        if (accessibleCells.Count < 2)
        {
            Debug.LogError("Error: Not enough accessible cells for start/end points");
            return;
        }
        
        // Find two points that are far apart
        (int startX, int startY, int endX, int endY) = FindDistantPoints(map, accessibleCells);
        
        // Mark the points on the map
        map[startY, startX] = START_POINT;
        map[endY, endX] = END_POINT;
        
        Debug.Log($"Start point placed at ({startX}, {startY})");
        Debug.Log($"End point placed at ({endX}, {endY})");
        
        float distance = Mathf.Sqrt(Mathf.Pow(endX - startX, 2) + Mathf.Pow(endY - startY, 2));
        Debug.Log($"Straight-line distance between points: {distance:F1} cells");
    }
    
    // Get all accessible cells in a room starting from a seed point
    private List<(int x, int y)> GetAccessibleCells(int[,] map, int startX, int startY)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        List<(int x, int y)> cells = new List<(int x, int y)>();
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            cells.Add((x, y));
            
            // Check 4 adjacent cells (non-diagonal neighbors)
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1 && 
                    map[ny, nx] == PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return cells;
    }
    
    // Find two points that are far apart in the accessible cells
    private (int startX, int startY, int endX, int endY) FindDistantPoints(int[,] map, List<(int x, int y)> cells)
    {
        // Option 1: Simple approach - take random cells from opposite parts of the map
        int n = cells.Count;
        int quarterIndex = n / 4;
        int threeQuarterIndex = 3 * n / 4;
        
        // Get a point from first quarter
        int startIndex = random.Next(0, quarterIndex);
        (int startX, int startY) = cells[startIndex];
        
        // Get a point from fourth quarter
        int endIndex = random.Next(threeQuarterIndex, n);
        (int endX, int endY) = cells[endIndex];
        
        // Ensure minimum distance between points
        float distance = Mathf.Sqrt(Mathf.Pow(endX - startX, 2) + Mathf.Pow(endY - startY, 2));
        int attempts = 0;
        const int MIN_DISTANCE = 30; // Minimum distance between start and end
        
        // Try to find better points up to 20 times
        while (distance < MIN_DISTANCE && attempts < 20)
        {
            startIndex = random.Next(0, n);
            (startX, startY) = cells[startIndex];
            
            endIndex = random.Next(0, n);
            (endX, endY) = cells[endIndex];
            
            distance = Mathf.Sqrt(Mathf.Pow(endX - startX, 2) + Mathf.Pow(endY - startY, 2));
            attempts++;
        }
        
        return (startX, startY, endX, endY);
    }
    
    // Remove small wall clusters
    private void RemoveSmallWallClusters(int[,] map, int maxSize)
    {
        Debug.Log($"Identifying and removing wall clusters smaller than size {maxSize}...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        int clustersRemoved = 0;
        
        // Skip the border cells - we always want to keep those as walls
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                // If this is a wall cell and hasn't been visited yet
                if (map[y, x] == WALL && !visited[y, x])
                {
                    List<(int x, int y)> cluster = new List<(int x, int y)>();
                    
                    // Find the entire connected wall cluster
                    FindConnectedWallCluster(map, x, y, visited, cluster);
                    
                    // If the cluster is smaller than the threshold, remove it
                    if (cluster.Count > 0 && cluster.Count < maxSize)
                    {
                        foreach (var (cx, cy) in cluster)
                        {
                            map[cy, cx] = PATH; // Convert wall to path
                        }
                        clustersRemoved++;
                    }
                }
            }
        }
        
        Debug.Log($"Removed {clustersRemoved} small wall clusters");
    }
    
    // Find all connected wall cells in a cluster
    private void FindConnectedWallCluster(int[,] map, int startX, int startY, bool[,] visited, List<(int x, int y)> cluster)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;
        
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            cluster.Add((x, y));
            
            // Check all 8 neighbors (including diagonals)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    // Skip the center cell
                    if (dx == 0 && dy == 0) continue;
                    
                    int nx = x + dx;
                    int ny = y + dy;
                    
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                        map[ny, nx] == WALL && !visited[ny, nx])
                    {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }
    }
    
    // Public method to get the generated map
    public int[,] GetMap()
    {
        return map;
    }
    
    // Public method to get the seed used for generation
    public int GetSeed()
    {
        return seed;
    }

    private void GenerateCaveTerrain()
    {
        Debug.Log("Generating cave terrain...");
        
        // Clean up any existing terrain
        CleanupTerrain();
        
        // Create a new terrain game object
        terrainObject = new GameObject("CaveTerrain");
        terrainObject.transform.position = Vector3.zero;
        
        // Add Terrain component
        caveTerrain = terrainObject.AddComponent<Terrain>();
        TerrainCollider terrainCollider = null;
        
        if (addTerrainCollider)
        {
            terrainCollider = terrainObject.AddComponent<TerrainCollider>();
        }
        
        // Create and configure terrain data
        TerrainData terrainData = null;
        
        #if UNITY_EDITOR
        // In editor, create the asset properly
        terrainData = new TerrainData();
        string assetPath = "Assets/TerrainData";
        
        // Create directory if it doesn't exist
        if (!System.IO.Directory.Exists(assetPath))
        {
            System.IO.Directory.CreateDirectory(assetPath);
        }
        
        // Save the terrain data asset
        string fullPath = $"{assetPath}/CaveTerrainData_{seed}.asset";
        UnityEditor.AssetDatabase.CreateAsset(terrainData, fullPath);
        Debug.Log($"Created terrain data asset at {fullPath}");
        #else
        // In runtime, just create the terrain data in memory
        terrainData = new TerrainData();
        #endif
        
        terrainData.name = $"CaveTerrainData_{seed}";
        
        // Set terrain size
        terrainData.size = new Vector3(width, terrainHeight, height);
        
        // Set heightmap resolution (same as map dimensions for 1:1 mapping)
        terrainData.heightmapResolution = Mathf.Max(width, height);
        
        // Pre-compute edge cells for better performance
        bool[,] edgeCells = PrecomputeEdgeCells();
        
        // Create heightmap
        float[,] heights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];
        
        // Calculate the scaling factors if the heightmap resolution doesn't match the map dimensions
        float xScale = (float)(width - 1) / (terrainData.heightmapResolution - 1);
        float zScale = (float)(height - 1) / (terrainData.heightmapResolution - 1);
        
        // Generate heightmap with Perlin noise for smoothing
        for (int z = 0; z < terrainData.heightmapResolution; z++)
        {
            for (int x = 0; x < terrainData.heightmapResolution; x++)
            {
                // Map heightmap coordinates to map coordinates
                int mapX = Mathf.FloorToInt(x * xScale);
                int mapZ = Mathf.FloorToInt(z * zScale);
                
                // Clamp to valid map indices
                mapX = Mathf.Clamp(mapX, 0, width - 1);
                mapZ = Mathf.Clamp(mapZ, 0, height - 1);
                
                // Base height from map (walls are high, paths are low)
                float baseHeight = map[mapZ, mapX] == WALL ? 1.0f : 0.0f;
                
                // Apply Perlin noise for smoothing
                if (smoothness > 0)
                {
                    // Generate noise based on position and seed
                    float noise = Mathf.PerlinNoise(
                        (x * noiseScale) + seed % 1000, 
                        (z * noiseScale) + seed % 1000
                    );
                    
                    // Adjust noise range from [0,1] to [-0.5,0.5]
                    noise = noise - 0.5f;
                    
                    // Apply smoothing only at the edges between wall and path
                    if (mapX < width && mapZ < height && edgeCells[mapZ, mapX])
                    {
                        // Blend between base height and noise based on smoothness
                        baseHeight = Mathf.Lerp(baseHeight, baseHeight + noise, smoothness);
                    }
                }
                
                // Ensure height is within valid range [0,1]
                heights[z, x] = Mathf.Clamp01(baseHeight);
            }
        }
        
        // Apply the heightmap to the terrain
        terrainData.SetHeights(0, 0, heights);
        
        // Assign the terrain data
        caveTerrain.terrainData = terrainData;
        
        if (addTerrainCollider && terrainCollider != null)
        {
            terrainCollider.terrainData = terrainData;
        }
        
        // Set material if provided
        if (terrainMaterial != null)
        {
            caveTerrain.materialTemplate = terrainMaterial;
        }
        
        // Mark start and end points with objects
        if (startPointPrefab != null || endPointPrefab != null)
        {
            MarkStartAndEndPoints();
        }
        
        Debug.Log("Cave terrain generation complete");
    }
    
    private bool[,] PrecomputeEdgeCells()
    {
        bool[,] edgeCells = new bool[height, width];
        
        for (int z = 1; z < height - 1; z++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                edgeCells[z, x] = IsEdgeCell(x, z);
            }
        }
        
        return edgeCells;
    }
    
    private bool IsEdgeCell(int x, int z)
    {
        // Check if this cell is at the edge between wall and path
        if (x <= 0 || x >= width - 1 || z <= 0 || z >= height - 1)
            return false;
            
        int cellType = map[z, x];
        
        // Check all 4 adjacent cells
        int[] dx = { -1, 1, 0, 0 };
        int[] dz = { 0, 0, -1, 1 };
        
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int nz = z + dz[i];
            
            if (nx >= 0 && nx < width && nz >= 0 && nz < height)
            {
                // If any adjacent cell is different from this cell, it's an edge
                if (map[nz, nx] != cellType)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private void MarkStartAndEndPoints()
    {
        // Check if terrain exists
        if (caveTerrain == null)
        {
            Debug.LogError("Cannot mark start/end points: Terrain is null");
            return;
        }
        
        // Find start and end points
        Vector3 startPos = Vector3.zero;
        Vector3 endPos = Vector3.zero;
        bool foundStart = false;
        bool foundEnd = false;
        
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (map[z, x] == START_POINT)
                {
                    // Get terrain height at this position
                    float terrainHeight = caveTerrain.SampleHeight(new Vector3(x, 0, z));
                    startPos = new Vector3(x, terrainHeight, z);
                    foundStart = true;
                }
                else if (map[z, x] == END_POINT)
                {
                    // Get terrain height at this position
                    float terrainHeight = caveTerrain.SampleHeight(new Vector3(x, 0, z));
                    endPos = new Vector3(x, terrainHeight, z);
                    foundEnd = true;
                }
                
                if (foundStart && foundEnd)
                    break;
            }
            
            if (foundStart && foundEnd)
                break;
        }
        
        // Place markers
        if (foundStart && startPointPrefab != null)
        {
            GameObject startMarker = Instantiate(startPointPrefab, startPos, Quaternion.identity);
            startMarker.name = "StartPoint";
            startMarker.transform.SetParent(terrainObject.transform);
        }
        
        if (foundEnd && endPointPrefab != null)
        {
            GameObject endMarker = Instantiate(endPointPrefab, endPos, Quaternion.identity);
            endMarker.name = "EndPoint";
            endMarker.transform.SetParent(terrainObject.transform);
        }
    }

    private void Visualize3DCave()
    {
        if (!visualize3D || map == null)
            return;
            
        // Clear any existing cave objects
        if (caveParent != null)
        {
            while (caveParent.childCount > 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(caveParent.GetChild(0).gameObject);
                }
                else
                {
                    DestroyImmediate(caveParent.GetChild(0).gameObject);
                }
            }
        }
        else
        {
            // Create a parent object for all cave elements
            GameObject parent = new GameObject("Cave");
            caveParent = parent.transform;
        }
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Center the cave
        Vector3 offset = new Vector3(width * cellSize * 0.5f, 0, height * cellSize * 0.5f);
        
        // Check if we should use the optimized combined mesh approach
        bool canCombineMeshes = wallPrefab != null && floorPrefab != null;
        
        if (canCombineMeshes)
        {
            // Create combined meshes for walls and floors
            CombineCaveMeshes(offset);
        }
        else
        {
            // Fall back to the original method of instantiating individual prefabs
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate position (centered)
                    Vector3 position = new Vector3(x * cellSize, 0, y * cellSize) - offset;
                    
                    GameObject prefabToUse = null;
                    
                    switch (map[y, x])
                    {
                        case WALL:
                            if (wallPrefab != null)
                            {
                                prefabToUse = wallPrefab;
                                position.y = wallHeight * 0.5f; // Center the wall vertically
                            }
                            break;
                            
                        case PATH:
                            if (floorPrefab != null)
                            {
                                prefabToUse = floorPrefab;
                            }
                            break;
                            
                        case START_POINT:
                            if (startPointPrefab != null)
                            {
                                prefabToUse = startPointPrefab;
                            }
                            else if (floorPrefab != null)
                            {
                                prefabToUse = floorPrefab;
                                // Maybe add a marker or different color later
                            }
                            break;
                            
                        case END_POINT:
                            if (endPointPrefab != null)
                            {
                                prefabToUse = endPointPrefab;
                            }
                            else if (floorPrefab != null)
                            {
                                prefabToUse = floorPrefab;
                                // Maybe add a marker or different color later
                            }
                            break;
                    }
                    
                    if (prefabToUse != null)
                    {
                        GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity, caveParent);
                        
                        // If it's a wall, scale it to the desired height
                        if (map[y, x] == WALL && wallPrefab != null)
                        {
                            Vector3 scale = instance.transform.localScale;
                            scale.y = wallHeight;
                            instance.transform.localScale = scale;
                        }
                        
                        // Name the object based on its position and type
                        instance.name = $"{GetCellTypeName(map[y, x])}_{x}_{y}";
                    }
                }
            }
        }
        
        // Always place start and end points separately
        PlaceStartAndEndMarkers(offset);
        
        Debug.Log($"3D cave visualization complete with dimensions {width}x{height}");
    }
    
    private void CombineCaveMeshes(Vector3 offset)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Create lists to hold the mesh instances for walls and floors
        List<CombineInstance> wallCombines = new List<CombineInstance>();
        List<CombineInstance> floorCombines = new List<CombineInstance>();
        
        // Get the meshes from the prefabs
        Mesh wallMesh = GetMeshFromPrefab(wallPrefab);
        Mesh floorMesh = GetMeshFromPrefab(floorPrefab);
        
        // Get the materials from the prefabs
        Material wallMaterial = GetMaterialFromPrefab(wallPrefab);
        Material floorMaterial = GetMaterialFromPrefab(floorPrefab);
        
        if (wallMesh == null || floorMesh == null)
        {
            Debug.LogError("Failed to get meshes from prefabs. Make sure your prefabs have MeshFilter components.");
            return;
        }
        
        // Create transform matrices for each instance
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip start and end points - we'll place them separately
                if (map[z, x] == START_POINT || map[z, x] == END_POINT)
                    continue;
                
                // Calculate position (centered)
                Vector3 position = new Vector3(x * cellSize, 0, z * cellSize) - offset;
                
                if (map[z, x] == WALL)
                {
                    // Position wall with proper height
                    position.y = wallHeight * 0.5f;
                    
                    // Create a matrix for this wall instance
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        position,
                        Quaternion.identity,
                        new Vector3(1, wallHeight, 1)
                    );
                    
                    // Add to the wall combines list
                    CombineInstance ci = new CombineInstance();
                    ci.mesh = wallMesh;
                    ci.transform = matrix;
                    wallCombines.Add(ci);
                }
                else if (map[z, x] == PATH)
                {
                    // Create a matrix for this floor instance
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        position,
                        Quaternion.identity,
                        Vector3.one
                    );
                    
                    // Add to the floor combines list
                    CombineInstance ci = new CombineInstance();
                    ci.mesh = floorMesh;
                    ci.transform = matrix;
                    floorCombines.Add(ci);
                }
            }
        }
        
        // Create the combined wall mesh
        if (wallCombines.Count > 0)
        {
            GameObject combinedWalls = new GameObject("CombinedWalls");
            combinedWalls.transform.SetParent(caveParent);
            combinedWalls.transform.localPosition = Vector3.zero;
            
            MeshFilter meshFilter = combinedWalls.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = combinedWalls.AddComponent<MeshRenderer>();
            
            // Create the combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support large meshes
            combinedMesh.CombineMeshes(wallCombines.ToArray(), true);
            
            meshFilter.sharedMesh = combinedMesh;
            meshRenderer.sharedMaterial = wallMaterial;
            
            // Optionally add a mesh collider
            if (addTerrainCollider)
            {
                MeshCollider collider = combinedWalls.AddComponent<MeshCollider>();
                collider.sharedMesh = combinedMesh;
            }
            
            Debug.Log($"Combined {wallCombines.Count} wall instances into a single mesh");
        }
        
        // Create the combined floor mesh
        if (floorCombines.Count > 0)
        {
            GameObject combinedFloors = new GameObject("CombinedFloors");
            combinedFloors.transform.SetParent(caveParent);
            combinedFloors.transform.localPosition = Vector3.zero;
            
            MeshFilter meshFilter = combinedFloors.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = combinedFloors.AddComponent<MeshRenderer>();
            
            // Create the combined mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support large meshes
            combinedMesh.CombineMeshes(floorCombines.ToArray(), true);
            
            meshFilter.sharedMesh = combinedMesh;
            meshRenderer.sharedMaterial = floorMaterial;
            
            Debug.Log($"Combined {floorCombines.Count} floor instances into a single mesh");
        }
    }
    
    private Mesh GetMeshFromPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;
            
        MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            return meshFilter.sharedMesh;
        }
        
        return null;
    }
    
    private Material GetMaterialFromPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;
            
        MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            return renderer.sharedMaterial;
        }
        
        return null;
    }
    
    private void PlaceStartAndEndMarkers(Vector3 offset)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Place start and end markers
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate position (centered)
                Vector3 position = new Vector3(x * cellSize, 0, z * cellSize) - offset;
                
                if (map[z, x] == START_POINT)
                {
                    GameObject prefabToUse = startPointPrefab != null ? startPointPrefab : floorPrefab;
                    if (prefabToUse != null)
                    {
                        GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity, caveParent);
                        instance.name = $"StartPoint_{x}_{z}";
                    }
                }
                else if (map[z, x] == END_POINT)
                {
                    GameObject prefabToUse = endPointPrefab != null ? endPointPrefab : floorPrefab;
                    if (prefabToUse != null)
                    {
                        GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity, caveParent);
                        instance.name = $"EndPoint_{x}_{z}";
                    }
                }
            }
        }
    }
    
    private string GetCellTypeName(int cellType)
    {
        switch (cellType)
        {
            case WALL: return "Wall";
            case PATH: return "Floor";
            case START_POINT: return "Start";
            case END_POINT: return "End";
            default: return "Unknown";
        }
    }

    private void GenerateFeatures()
    {
        if (generateRooms)
        {
            Debug.Log($"Generating {numberOfRooms} rectangular rooms...");
            for (int i = 0; i < numberOfRooms; i++)
            {
                int roomWidth = random.Next(roomSizeRange.x, roomSizeRange.y + 1);
                int roomHeight = random.Next(roomSizeRange.x, roomSizeRange.y + 1);
                int startX = random.Next(1, width - roomWidth - 1);
                int startY = random.Next(1, height - roomHeight - 1);
                DrawRectangle(startX, startY, roomWidth, roomHeight);
                Debug.Log($"Created room {i + 1}: {roomWidth}x{roomHeight} at ({startX}, {startY})");
            }
        }

        if (generateChambers)
        {
            Debug.Log($"Generating {numberOfChambers} circular chambers...");
            for (int i = 0; i < numberOfChambers; i++)
            {
                int radius = random.Next(chamberRadiusRange.x, chamberRadiusRange.y + 1);
                int centerX = random.Next(radius + 1, width - radius - 1);
                int centerY = random.Next(radius + 1, height - radius - 1);
                DrawCircle(centerX, centerY, radius);
                Debug.Log($"Created chamber {i + 1}: radius {radius} at ({centerX}, {centerY})");
            }
        }
    }

    private void DrawRectangle(int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width; x++)
        {
            for (int y = startY; y < startY + height; y++)
            {
                if (IsInMapRange(x, y))
                {
                    map[y, x] = PATH;
                }
            }
        }
    }

    private void DrawCircle(int centerX, int centerY, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int drawX = centerX + x;
                    int drawY = centerY + y;
                    if (IsInMapRange(drawX, drawY))
                    {
                        map[drawY, drawX] = PATH;
                    }
                }
            }
        }
    }

    private bool IsInMapRange(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private void ForceClearCentralArea()
    {
        // Force create open areas in the center of the map as a last resort
        Debug.LogWarning("Force clearing central area to ensure some floor space");
        
        int centerX = width / 2;
        int centerY = height / 2;
        
        // Create a central chamber
        DrawCircle(centerX, centerY, Mathf.Min(width, height) / 6);
        
        // Create a few random rooms nearby
        for (int i = 0; i < 3; i++)
        {
            int offsetX = random.Next(-width/4, width/4);
            int offsetY = random.Next(-height/4, height/4);
            int size = random.Next(5, 15);
            
            DrawRectangle(centerX + offsetX, centerY + offsetY, size, size);
        }
        
        // Create pathways connecting these areas
        ConnectRooms(map, centerX, centerY, centerX + width/5, centerY);
        ConnectRooms(map, centerX, centerY, centerX, centerY + height/5);
        ConnectRooms(map, centerX, centerY, centerX - width/5, centerY);
        ConnectRooms(map, centerX, centerY, centerX, centerY - height/5);
    }
}
