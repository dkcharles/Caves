using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the 3D visualization of the cave.
/// </summary>
public class CaveVisualiser : MonoBehaviour
{
    [SerializeField] private Transform visualisationParent;
    
    // Cache for optimisation
    private Dictionary<int, Material> _cellMaterials = new Dictionary<int, Material>();
    private Dictionary<int, Mesh> _cellMeshes = new Dictionary<int, Mesh>();
    
    /// <summary>
    /// Clear the existing visualization.
    /// </summary>
    public void ClearVisualisation()
    {
        if (visualisationParent == null)
        {
            // Create a parent object if it doesn't exist
            GameObject parent = new GameObject("Cave Visualization");
            visualisationParent = parent.transform;
            visualisationParent.SetParent(transform);
        }
        else
        {
            // Clear existing objects
            while (visualisationParent.childCount > 0)
            {
                if (Application.isPlaying)
                {
                    Destroy(visualisationParent.GetChild(0).gameObject);
                }
                else
                {
                    DestroyImmediate(visualisationParent.GetChild(0).gameObject);
                }
            }
        }
    }
    
    /// <summary>
    /// Visualise the cave in 3D.
    /// </summary>
    public void VisualiseCave(int[,] map, CaveParameters parameters)
    {
        if (!parameters.visualise3D)
            return;
            
        Debug.Log("Starting 3D visualization...");
        
        // Check if required prefabs are assigned
        if (parameters.wallPrefab == null || parameters.floorPrefab == null)
        {
            Debug.LogError("Cannot visualize cave: Wall or floor prefab is not assigned");
            return;
        }
        
        // Ensure the visualization parent exists
        if (visualisationParent == null)
        {
            GameObject parent = new GameObject("Cave Visualization");
            visualisationParent = parent.transform;
            visualisationParent.SetParent(transform);
        }
        
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Center the cave
        Vector3 offset = new Vector3(width * parameters.cellSize * 0.5f, 0, height * parameters.cellSize * 0.5f);
        
        // Use optimized mesh combining
        VisualiseCaveWithCombinedMeshes(map, parameters, offset);
        
        // Place start and end points separately
        PlaceStartAndEndMarkers(map, parameters, offset);
        
        Debug.Log("3D cave visualization complete");
    }
    
    private void VisualiseCaveWithCombinedMeshes(int[,] map, CaveParameters parameters, Vector3 offset)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Get the meshes and materials from the prefabs
        CachePrefabData(parameters);
        
        // Create separate lists for each cell type
        Dictionary<int, List<CombineInstance>> combineInstancesMap = new Dictionary<int, List<CombineInstance>>
        {
            { CaveGrid.WALL, new List<CombineInstance>() },
            { CaveGrid.PATH, new List<CombineInstance>() }
        };
        
        // Create transform matrices for each instance
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Skip start and end points - we'll place them separately
                if (map[z, x] == CaveGrid.START_POINT || map[z, x] == CaveGrid.END_POINT)
                    continue;
                
                int cellType = map[z, x];
                
                // Only process walls and floors
                if (cellType != CaveGrid.WALL && cellType != CaveGrid.PATH)
                    continue;
                
                // Calculate position (centered)
                Vector3 position = new Vector3(x * parameters.cellSize, 0, z * parameters.cellSize) - offset;
                Vector3 scale = Vector3.one;
                
                // Handle special case for walls (they need height adjustment)
                if (cellType == CaveGrid.WALL)
                {
                    position.y = parameters.wallHeight * 0.5f;
                    scale = new Vector3(1, parameters.wallHeight, 1);
                }
                
                // Create a matrix for this instance
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, scale);
                
                // Add to the appropriate list
                CombineInstance ci = new CombineInstance
                {
                    mesh = _cellMeshes[cellType],
                    transform = matrix
                };
                
                combineInstancesMap[cellType].Add(ci);
            }
        }
        
        // Create the combined meshes for each cell type
        foreach (var kvp in combineInstancesMap)
        {
            int cellType = kvp.Key;
            List<CombineInstance> instances = kvp.Value;
            
            if (instances.Count == 0)
                continue;
            
            string typeName = GetCellTypeName(cellType);
            GameObject combinedObject = new GameObject($"Combined{typeName}");
            combinedObject.transform.SetParent(visualisationParent);
            combinedObject.transform.localPosition = Vector3.zero;
            
            MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
            
            // Create the combined mesh
            Mesh combinedMesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 // Support large meshes
            };
            combinedMesh.CombineMeshes(instances.ToArray(), true);
            
            meshFilter.sharedMesh = combinedMesh;
            meshRenderer.sharedMaterial = _cellMaterials[cellType];
            
            // Add a mesh collider for walls
            if (cellType == CaveGrid.WALL)
            {
                MeshCollider collider = combinedObject.AddComponent<MeshCollider>();
                collider.sharedMesh = combinedMesh;
            }
            
            Debug.Log($"Combined {instances.Count} {typeName} instances into a single mesh");
        }
    }
    
    private void CachePrefabData(CaveParameters parameters)
    {
        // Clear the cache
        _cellMeshes.Clear();
        _cellMaterials.Clear();
        
        // Cache the wall mesh and material
        if (parameters.wallPrefab != null)
        {
            MeshFilter meshFilter = parameters.wallPrefab.GetComponent<MeshFilter>();
            MeshRenderer renderer = parameters.wallPrefab.GetComponent<MeshRenderer>();
            
            if (meshFilter != null && meshFilter.sharedMesh != null &&
                renderer != null && renderer.sharedMaterial != null)
            {
                _cellMeshes[CaveGrid.WALL] = meshFilter.sharedMesh;
                _cellMaterials[CaveGrid.WALL] = renderer.sharedMaterial;
            }
        }
        
        // Cache the floor mesh and material
        if (parameters.floorPrefab != null)
        {
            MeshFilter meshFilter = parameters.floorPrefab.GetComponent<MeshFilter>();
            MeshRenderer renderer = parameters.floorPrefab.GetComponent<MeshRenderer>();
            
            if (meshFilter != null && meshFilter.sharedMesh != null &&
                renderer != null && renderer.sharedMaterial != null)
            {
                _cellMeshes[CaveGrid.PATH] = meshFilter.sharedMesh;
                _cellMaterials[CaveGrid.PATH] = renderer.sharedMaterial;
            }
        }
    }
    
    private void PlaceStartAndEndMarkers(int[,] map, CaveParameters parameters, Vector3 offset)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        
        // Place start and end markers
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // Calculate position (centered)
                Vector3 position = new Vector3(x * parameters.cellSize, 0, z * parameters.cellSize) - offset;
                
                if (map[z, x] == CaveGrid.START_POINT)
                {
                    GameObject prefabToUse = parameters.startPointPrefab != null ? 
                        parameters.startPointPrefab : parameters.floorPrefab;
                    
                    if (prefabToUse != null)
                    {
                        GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity, visualisationParent);
                        instance.name = $"StartPoint_{x}_{z}";
                    }
                }
                else if (map[z, x] == CaveGrid.END_POINT)
                {
                    GameObject prefabToUse = parameters.endPointPrefab != null ? 
                        parameters.endPointPrefab : parameters.floorPrefab;
                    
                    if (prefabToUse != null)
                    {
                        GameObject instance = Instantiate(prefabToUse, position, Quaternion.identity, visualisationParent);
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
            case CaveGrid.WALL: return "Wall";
            case CaveGrid.PATH: return "Floor";
            case CaveGrid.START_POINT: return "Start";
            case CaveGrid.END_POINT: return "End";
            default: return "Unknown";
        }
    }
}