using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Represents the grid data structure of the cave.
/// Handles operations on the cave map data.
/// </summary>
public class CaveGrid
{
    // Constants for cell types
    public const int WALL = 1;
    public const int PATH = 0;
    public const int START_POINT = 2;
    public const int END_POINT = 3;
    
    // Private data
    private int[,] _map;
    private int _width;
    private int _height;
    
    /// <summary>
    /// Create a new empty cave grid.
    /// </summary>
    public CaveGrid()
    {
        _map = new int[0, 0];
        _width = 0;
        _height = 0;
    }
    
    /// <summary>
    /// Set the map data.
    /// </summary>
    public void SetMap(int[,] map, int width, int height)
    {
        _map = map;
        _width = width;
        _height = height;
    }
    
    /// <summary>
    /// Get the current map data.
    /// </summary>
    public int[,] GetMap()
    {
        return _map;
    }
    
    /// <summary>
    /// Get the width of the map.
    /// </summary>
    public int GetWidth()
    {
        return _width;
    }
    
    /// <summary>
    /// Get the height of the map.
    /// </summary>
    public int GetHeight()
    {
        return _height;
    }
    
    /// <summary>
    /// Get the cell type at the specified coordinates.
    /// </summary>
    public int GetCell(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            return WALL; // Out of bounds cells are walls
        }
        
        return _map[y, x];
    }
    
    /// <summary>
    /// Set the cell type at the specified coordinates.
    /// </summary>
    public void SetCell(int x, int y, int cellType)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
        {
            return; // Ignore out of bounds cells
        }
        
        _map[y, x] = cellType;
    }
    
    /// <summary>
    /// Save the map to a text file.
    /// </summary>
    public void SaveMapToFile(string filename)
    {
        Debug.Log($"Saving {_width}x{_height} map to {filename}...");
        
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
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    char symbol;
                    switch (_map[y, x])
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
    
    /// <summary>
    /// Count the number of floor tiles in the map.
    /// </summary>
    public int CountFloorTiles()
    {
        int count = 0;
        
        for (int y = 1; y < _height - 1; y++)
        {
            for (int x = 1; x < _width - 1; x++)
            {
                if (_map[y, x] == PATH)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
}