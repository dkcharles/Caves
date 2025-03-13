using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static class containing algorithms for cave generation.
/// </summary>
public static class CaveAlgorithms
{
    /// <summary>
    /// Apply cellular automata smoothing to the map.
    /// </summary>
    public static int[,] SmoothMap(int[,] map, int birthLimit, int deathLimit, 
                                bool useWeightedSmoothing, float cardinalWeight, float diagonalWeight)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        int[,] newMap = new int[height, width];
        
        // Copy borders
        for (int x = 0; x < width; x++)
        {
            newMap[0, x] = CaveGrid.WALL;
            newMap[height - 1, x] = CaveGrid.WALL;
        }
        
        for (int y = 0; y < height; y++)
        {
            newMap[y, 0] = CaveGrid.WALL;
            newMap[y, width - 1] = CaveGrid.WALL;
        }
        
        // Apply smoothing rules
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float neighborScore = useWeightedSmoothing ? 
                    CalculateWeightedNeighbors(map, x, y, cardinalWeight, diagonalWeight) : 
                    CountAdjacentWalls(map, x, y);
                
                // Current cell is a wall
                if (map[y, x] == CaveGrid.WALL)
                {
                    // Wall survives if it has enough neighbors
                    newMap[y, x] = neighborScore >= deathLimit ? CaveGrid.WALL : CaveGrid.PATH;
                }
                // Current cell is a path
                else
                {
                    // Wall is born if there are enough neighbors
                    newMap[y, x] = neighborScore > birthLimit ? CaveGrid.WALL : CaveGrid.PATH;
                }
            }
        }
        
        return newMap;
    }
    
    /// <summary>
    /// Calculate weighted neighbor score for smoothing.
    /// </summary>
    private static float CalculateWeightedNeighbors(int[,] map, int x, int y, 
                                                 float cardinalWeight, float diagonalWeight)
    {
        float score = 0;
        
        // Check cardinal neighbors (N,S,E,W)
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };
        
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx[i];
            int ny = y + dy[i];
            
            if (map[ny, nx] == CaveGrid.WALL)
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
            
            if (map[ny, nx] == CaveGrid.WALL)
            {
                score += diagonalWeight;
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// Count adjacent wall cells.
    /// </summary>
    private static int CountAdjacentWalls(int[,] map, int x, int y)
    {
        int count = 0;
        
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                
                int nx = x + dx;
                int ny = y + dy;
                
                if (map[ny, nx] == CaveGrid.WALL)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Ensure connectivity between all major rooms in the cave.
    /// </summary>
    public static void EnsureConnectivity(int[,] map, int minRoomSize, System.Random random)
    {
        Debug.Log("Ensuring connectivity between cave regions...");
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Find all separate rooms
        List<(int x, int y, int size)> rooms = FindRooms(map);
        
        if (rooms.Count <= 1)
        {
            Debug.Log("Map is already connected or contains no rooms.");
        }
        else
        {
            // Sort rooms by size (largest first)
            rooms.Sort((a, b) => b.size.CompareTo(a.size));
            
            Debug.Log($"Found {rooms.Count} separate regions. Largest region size: {rooms[0].size}");
            
            // Handle each room
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
            
            Debug.Log($"Filled {filledRooms} small rooms, connected {connectedRooms} larger rooms.");
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
                    map[y, x + j] = CaveGrid.PATH;
                }
            }
            else
            {
                for (int j = 0; j < length && y + j < height - 1; j++)
                {
                    map[y + j, x] = CaveGrid.PATH;
                }
            }
        }
        
        Debug.Log($"Added {hallways} additional connectivity hallways");
    }
    
    /// <summary>
    /// Find all separate rooms in the map.
    /// </summary>
    public static List<(int x, int y, int size)> FindRooms(int[,] map)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        bool[,] visited = new bool[height, width];
        List<(int x, int y, int size)> rooms = new List<(int x, int y, int size)>();
        
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[y, x] == CaveGrid.PATH && !visited[y, x])
                {
                    // Start of a new room
                    int roomSize = FloodFill(map, x, y, visited);
                    rooms.Add((x, y, roomSize));
                }
            }
        }
        
        return rooms;
    }
    
    /// <summary>
    /// Flood fill algorithm to mark a room and return its size.
    /// </summary>
    private static int FloodFill(int[,] map, int startX, int startY, bool[,] visited)
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
                    map[ny, nx] == CaveGrid.PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return roomSize;
    }
    
    /// <summary>
    /// Fill a room (convert it back to walls).
    /// </summary>
    private static void FillRoom(int[,] map, int startX, int startY)
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
            map[y, x] = CaveGrid.WALL; // Convert to wall
            
            // Check 4 adjacent cells
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && 
                    map[ny, nx] == CaveGrid.PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }
    
    /// <summary>
    /// Connect two rooms by creating a path between them.
    /// </summary>
    private static void ConnectRooms(int[,] map, int x1, int y1, int x2, int y2)
    {
        // Create an L-shaped corridor between the rooms
        // First horizontal part
        int xMin = Math.Min(x1, x2);
        int xMax = Math.Max(x1, x2);
        
        for (int x = xMin; x <= xMax; x++)
        {
            map[y1, x] = CaveGrid.PATH;
        }
        
        // Then vertical part
        int yMin = Math.Min(y1, y2);
        int yMax = Math.Max(y1, y2);
        
        for (int y = yMin; y <= yMax; y++)
        {
            map[y, x2] = CaveGrid.PATH;
        }
    }
    
    /// <summary>
    /// Remove small wall clusters.
    /// </summary>
    public static void RemoveSmallWallClusters(int[,] map, int maxSize)
    {
        Debug.Log($"Removing small wall clusters (size < {maxSize})...");
        
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
                if (map[y, x] == CaveGrid.WALL && !visited[y, x])
                {
                    List<(int x, int y)> cluster = new List<(int x, int y)>();
                    
                    // Find the entire connected wall cluster
                    FindConnectedWallCluster(map, x, y, visited, cluster);
                    
                    // If the cluster is smaller than the threshold, remove it
                    if (cluster.Count > 0 && cluster.Count < maxSize)
                    {
                        foreach (var (cx, cy) in cluster)
                        {
                            map[cy, cx] = CaveGrid.PATH; // Convert wall to path
                        }
                        clustersRemoved++;
                    }
                }
            }
        }
        
        Debug.Log($"Removed {clustersRemoved} small wall clusters");
    }
    
    /// <summary>
    /// Find all connected wall cells in a cluster.
    /// </summary>
    private static void FindConnectedWallCluster(int[,] map, int startX, int startY, bool[,] visited, List<(int x, int y)> cluster)
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
                        map[ny, nx] == CaveGrid.WALL && !visited[ny, nx])
                    {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Get all accessible cells in a room starting from a seed point.
    /// </summary>
    public static List<(int x, int y)> GetAccessibleCells(int[,] map, int startX, int startY)
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
                    map[ny, nx] == CaveGrid.PATH && !visited[ny, nx])
                {
                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        
        return cells;
    }
    
    /// <summary>
    /// Find two points that are far apart in the accessible cells.
    /// </summary>
    public static (int startX, int startY, int endX, int endY) FindDistantPoints(List<(int x, int y)> cells, System.Random random)
    {
        // Simple approach - take random cells from opposite parts of the list
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
    
    /// <summary>
    /// Count the number of floor tiles in the map.
    /// </summary>
    public static int CountFloorTiles(int[,] map)
    {
        int count = 0;
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Count only interior floor tiles (excluding borders)
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                if (map[y, x] == CaveGrid.PATH)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Force clear a central area to ensure some floor space.
    /// </summary>
    public static void ForceClearCentralArea(int[,] map, int width, int height, System.Random random)
    {
        Debug.LogWarning("Force clearing central area to ensure some floor space");
        
        int centerX = width / 2;
        int centerY = height / 2;
        
        // Create a central chamber
        DrawCircle(map, centerX, centerY, Mathf.Min(width, height) / 6, width, height);
        
        // Create a few random rooms nearby
        for (int i = 0; i < 3; i++)
        {
            int offsetX = random.Next(-width/4, width/4);
            int offsetY = random.Next(-height/4, height/4);
            int size = random.Next(5, 15);
            
            DrawRectangle(map, centerX + offsetX, centerY + offsetY, size, size);
        }
        
        // Create pathways connecting these areas
        ConnectRooms(map, centerX, centerY, centerX + width/5, centerY);
        ConnectRooms(map, centerX, centerY, centerX, centerY + height/5);
        ConnectRooms(map, centerX, centerY, centerX - width/5, centerY);
        ConnectRooms(map, centerX, centerY, centerX, centerY - height/5);
    }
    
    /// <summary>
    /// Draw a rectangle of floor tiles.
    /// </summary>
    public static void DrawRectangle(int[,] map, int startX, int startY, int width, int height)
    {
        int mapHeight = map.GetLength(0);
        int mapWidth = map.GetLength(1);
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int drawX = startX + x;
                int drawY = startY + y;
                
                if (drawX > 0 && drawX < mapWidth - 1 && drawY > 0 && drawY < mapHeight - 1)
                {
                    map[drawY, drawX] = CaveGrid.PATH;
                }
            }
        }
    }
    
    /// <summary>
    /// Draw a circle of floor tiles.
    /// </summary>
    public static void DrawCircle(int[,] map, int centerX, int centerY, int radius, int mapWidth, int mapHeight)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int drawX = centerX + x;
                    int drawY = centerY + y;
                    
                    if (drawX > 0 && drawX < mapWidth - 1 && drawY > 0 && drawY < mapHeight - 1)
                    {
                        map[drawY, drawX] = CaveGrid.PATH;
                    }
                }
            }
        }
    }
}