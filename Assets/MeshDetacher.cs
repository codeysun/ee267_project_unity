using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(RayInteractorSphereSpawner))]
public class ColoredMeshDetacher : MonoBehaviour
{
    [Header("Input Configuration")]
    [SerializeField] private InputActionProperty detachButtonAction; // Right controller button
    [SerializeField] private XRRayInteractor rayInteractor;

    [Header("Mesh Manipulation")]
    [SerializeField] private Material detachedMeshMaterial;
    [SerializeField] private bool makeDetachedMeshesGrabbable = true;
    [SerializeField] private float manipulationScale = 1.0f;

    // Reference to the companion vertex colorer script
    private RayInteractorSphereSpawner vertexColorer;

    // Track created submeshes
    private List<GameObject> detachedMeshes = new List<GameObject>();

    private bool wasDetachButtonPressed = false;

    private void Awake()
    {
        // Get reference to the vertex colorer script
        vertexColorer = GetComponent<RayInteractorSphereSpawner>();

        if (rayInteractor == null)
        {
            rayInteractor = GetComponent<XRRayInteractor>();
        }

        // Create default material if none assigned
        if (detachedMeshMaterial == null)
        {
            detachedMeshMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            detachedMeshMaterial.color = Color.white;
        }
    }

    private void OnEnable()
    {
        if (detachButtonAction.action != null)
        {
            detachButtonAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (detachButtonAction.action != null)
        {
            detachButtonAction.action.Disable();
        }
    }

    private void Update()
    {
        // Check for detach button press
        if (detachButtonAction.action != null)
        {
            bool isDetachButtonPressed = detachButtonAction.action.IsPressed();

            if (isDetachButtonPressed && !wasDetachButtonPressed)
            {
                TryDetachColoredVertices();
            }

            wasDetachButtonPressed = isDetachButtonPressed;
        }
    }

    private void TryDetachColoredVertices()
    {
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            MeshFilter meshFilter = hit.collider.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                Mesh mesh = meshFilter.mesh;

                // Get the currently visible colors of the mesh
                Color[] colors = mesh.colors;
                if (colors == null || colors.Length == 0)
                {
                    Debug.LogWarning("Mesh has no vertex colors. Nothing to detach.");
                    return;
                }

                // Create a dictionary to map colors to submeshes
                Dictionary<Color, List<int>> colorToIndices = new Dictionary<Color, List<int>>();

                // Process each vertex
                for (int i = 0; i < colors.Length; i++)
                {
                    // Skip white (assumed default) vertices
                    if (colors[i] == Color.white)
                        continue;

                    // Group vertices by color
                    if (!colorToIndices.ContainsKey(colors[i]))
                    {
                        colorToIndices[colors[i]] = new List<int>();
                    }

                    colorToIndices[colors[i]].Add(i);
                }

                // Process each color group
                foreach (var colorGroup in colorToIndices)
                {
                    Color groupColor = colorGroup.Key;
                    List<int> vertexIndices = colorGroup.Value;

                    DetachVerticesAsSubmesh(mesh, vertexIndices, groupColor, hit.collider.gameObject);
                }

                // Optionally update the original mesh by removing the detached vertices
                // (This is complex and depends on your needs - could leave "holes" in the mesh)
            }
        }
    }

    private void DetachVerticesAsSubmesh(Mesh originalMesh, List<int> vertexIndices, Color color, GameObject sourceObject)
    {
        if (vertexIndices.Count == 0) return;

        Vector3[] originalVertices = originalMesh.vertices;
        Vector2[] originalUVs = originalMesh.uv;
        Vector3[] originalNormals = originalMesh.normals;

        // Get triangles that contain only the selected vertices
        int[] originalTriangles = originalMesh.triangles;
        List<int> newTriangles = new List<int>();
        Dictionary<int, int> oldToNewVertexMap = new Dictionary<int, int>();

        // First, find all triangles that contain only vertices from our selected set
        HashSet<int> vertexIndexSet = new HashSet<int>(vertexIndices);
        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int v1 = originalTriangles[i];
            int v2 = originalTriangles[i + 1];
            int v3 = originalTriangles[i + 2];

            // Only include triangles where all three vertices are in our set
            if (vertexIndexSet.Contains(v1) && vertexIndexSet.Contains(v2) && vertexIndexSet.Contains(v3))
            {
                // Map old vertex indices to new ones
                for (int j = 0; j < 3; j++)
                {
                    int oldIndex = originalTriangles[i + j];
                    if (!oldToNewVertexMap.ContainsKey(oldIndex))
                    {
                        oldToNewVertexMap[oldIndex] = oldToNewVertexMap.Count;
                    }
                    newTriangles.Add(oldToNewVertexMap[oldIndex]);
                }
            }
        }

        // If we don't have any valid triangles, bail out
        if (newTriangles.Count == 0)
        {
            Debug.Log($"No complete triangles found for color {color}");
            return;
        }

        // Create new mesh
        Mesh newMesh = new Mesh();

        // Create new vertices, UVs, and normals arrays
        Vector3[] newVertices = new Vector3[oldToNewVertexMap.Count];
        Vector2[] newUVs = originalUVs.Length > 0 ? new Vector2[oldToNewVertexMap.Count] : null;
        Vector3[] newNormals = originalNormals.Length > 0 ? new Vector3[oldToNewVertexMap.Count] : null;

        foreach (var kvp in oldToNewVertexMap)
        {
            int oldIndex = kvp.Key;
            int newIndex = kvp.Value;

            newVertices[newIndex] = originalVertices[oldIndex];

            if (newUVs != null && oldIndex < originalUVs.Length)
                newUVs[newIndex] = originalUVs[oldIndex];

            if (newNormals != null && oldIndex < originalNormals.Length)
                newNormals[newIndex] = originalNormals[oldIndex];
        }

        // Assign to new mesh
        newMesh.vertices = newVertices;
        if (newUVs != null) newMesh.uv = newUVs;
        if (newNormals != null) newMesh.normals = newNormals;
        newMesh.triangles = newTriangles.ToArray();

        // Recalculate bounds and possibly normals if needed
        newMesh.RecalculateBounds();
        if (newNormals == null) newMesh.RecalculateNormals();

        // Create a new GameObject for the detached mesh
        GameObject detachedObject = new GameObject($"Detached_Mesh_{color}");
        detachedObject.transform.position = sourceObject.transform.position;
        detachedObject.transform.rotation = sourceObject.transform.rotation;
        detachedObject.transform.localScale = sourceObject.transform.lossyScale * manipulationScale;

        // Add components
        MeshFilter newMeshFilter = detachedObject.AddComponent<MeshFilter>();
        MeshRenderer newMeshRenderer = detachedObject.AddComponent<MeshRenderer>();
        MeshCollider newMeshCollider = detachedObject.AddComponent<MeshCollider>();

        // Assign mesh
        newMeshFilter.mesh = newMesh;
        newMeshCollider.sharedMesh = newMesh;

        // Assign material (use color of vertices)
        Material instanceMaterial = new Material(detachedMeshMaterial);
        instanceMaterial.color = color;
        newMeshRenderer.material = instanceMaterial;

        // Make it interactable if required
        if (makeDetachedMeshesGrabbable)
        {
            // Add XR interactable components
            XRGrabInteractable grabInteractable = detachedObject.AddComponent<XRGrabInteractable>();
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = true;
            grabInteractable.throwOnDetach = true;

            // Add rigidbody for physics
            Rigidbody rb = detachedObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
        }

        // Track the detached mesh
        detachedMeshes.Add(detachedObject);

        Debug.Log($"Detached mesh created with {newVertices.Length} vertices and {newTriangles.Count / 3} triangles");
    }

    // Optional method to clean up all detached meshes
    public void ClearDetachedMeshes()
    {
        foreach (var mesh in detachedMeshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        detachedMeshes.Clear();
    }
}