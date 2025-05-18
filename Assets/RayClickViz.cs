using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RayInteractorSphereSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private XRRayInteractor rayInteractor;
    [SerializeField] private InputActionProperty triggerAction;

    [Header("Button Controls for Color Selection")]
    [SerializeField] private InputActionProperty primaryButtonAction; // Usually 'A' or 'X' button
    [SerializeField] private InputActionProperty secondaryButtonAction; // Usually 'B' or 'Y' button
    [SerializeField] private InputActionProperty gripButtonAction; // Controller grip
    [SerializeField] private InputActionProperty thumbstickButtonAction; // Pressing thumbstick

    [Header("Vertex Coloring")]
    [SerializeField] private float maxVertexSearchDistance = 0.5f;
    [SerializeField] private float vertexColorRadius = 0.5f; // For multi-vertex coloring
    [SerializeField] private bool colorMultipleVertices = true;
    [SerializeField] private bool resetColorsOnNewSelection = false;

    [Header("Color Options")]
    [SerializeField] private List<ColorOption> colorOptions = new List<ColorOption>();
    [SerializeField] private int currentColorIndex = 0;
    Color backgroundColor = Color.white;

    [System.Serializable]
    public class ColorOption
    {
        public string name = "Color";
        public Color color = Color.red;
    }

    private bool wasPressed = false;
    private bool wasPrimaryPressed = false;
    private bool wasSecondaryPressed = false;
    private bool wasGripPressed = false;
    private bool wasThumbstickPressed = false;

    private Dictionary<Mesh, Color[]> originalColorDict = new Dictionary<Mesh, Color[]>();
    private Dictionary<Mesh, GameObject> currentlyColoredMeshes = new Dictionary<Mesh, GameObject>();

    private void Awake()
    {
        // If no ray interactor assigned, try to get it from this GameObject
        if (rayInteractor == null)
        {
            rayInteractor = GetComponent<XRRayInteractor>();
        }

        // Initialize with default colors if none are defined
        if (colorOptions.Count == 0)
        {
            colorOptions.Add(new ColorOption { name = "Red", color = Color.red });
            colorOptions.Add(new ColorOption { name = "Green", color = Color.green });
            colorOptions.Add(new ColorOption { name = "Blue", color = Color.blue });
            colorOptions.Add(new ColorOption { name = "Yellow", color = Color.yellow });
            colorOptions.Add(new ColorOption { name = "Cyan", color = Color.cyan });
            //colorOptions.Add(new ColorOption { name = "Magenta", color = Color.magenta });
            //colorOptions.Add(new ColorOption { name = "White", color = Color.white });
            //colorOptions.Add(new ColorOption { name = "Black", color = Color.black });
        }
    }

    private void OnEnable()
    {
        triggerAction.action.Enable();

        primaryButtonAction.action.Enable();
        secondaryButtonAction.action.Enable();
    }

    private void OnDisable()
    {
        triggerAction.action.Disable();

        primaryButtonAction.action.Disable();
        secondaryButtonAction.action.Disable();
    }

    private void Update()
    {
        // Handle color selection
        HandleColorSelection();

        // Handle vertex coloring
        bool isPressed = triggerAction.action.ReadValue<float>() > 0.5f;
        if (isPressed && !wasPressed)
        {
            ColorClosestVertexAtRaycastHit();
        }
        wasPressed = isPressed;
    }

    private void HandleColorSelection()
    {
        // Primary button (typically A or X) - next color
        if (primaryButtonAction.action != null)
        {
            bool isPrimaryPressed = primaryButtonAction.action.ReadValue<float>() > 0.5f;
            if (isPrimaryPressed && !wasPrimaryPressed)
            {
                currentColorIndex = (currentColorIndex + 1) % colorOptions.Count;
                Debug.Log($"Color changed to: {colorOptions[currentColorIndex].name}");
            }
            wasPrimaryPressed = isPrimaryPressed;
        }

    }

    private void ColorClosestVertexAtRaycastHit()
    {
        // Check if ray hits anything
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Get the mesh from the hit object
            MeshFilter meshFilter = hit.collider.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                // Reset colors on previously colored meshes if needed
                if (resetColorsOnNewSelection)
                {
                    RestoreAllOriginalColors();
                }

                Mesh mesh = meshFilter.mesh;
                Transform hitTransform = hit.collider.transform;

                // Make the mesh readable and writable at runtime
                Mesh instancedMesh = Instantiate(mesh);
                meshFilter.mesh = instancedMesh;
                mesh = instancedMesh;

                // Store and initialize vertex colors if needed
                if (!originalColorDict.ContainsKey(mesh))
                {
                    StoreOriginalColors(mesh);
                }

                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;

                // If the mesh doesn't have colors yet, create them
                if (colors == null || colors.Length == 0)
                {
                    colors = new Color[vertices.Length];
                    for (int i = 0; i < colors.Length; i++)
                    {
                        colors[i] = Color.white;
                    }
                }

                // Find the closest vertex
                float closestDistance = float.MaxValue;
                int closestVertexIndex = -1;

                for (int i = 0; i < vertices.Length; i++)
                {
                    // Convert local vertex position to world space
                    Vector3 worldVertex = hitTransform.TransformPoint(vertices[i]);

                    // Calculate distance to hit point
                    float distance = Vector3.Distance(worldVertex, hit.point);

                    // If this vertex is closer than the previous closest
                    if (distance < closestDistance && distance <= maxVertexSearchDistance)
                    {
                        closestDistance = distance;
                        closestVertexIndex = i;
                    }
                }

                // Get the current selected color
                Color selectedColor = colorOptions[currentColorIndex].color;
                if (secondaryButtonAction.action.ReadValue<float>() > 0.5f)
                {
                    selectedColor = backgroundColor;
                }

                if (closestVertexIndex >= 0)
                {
                    

                    // Track this mesh as currently colored
                    currentlyColoredMeshes[mesh] = hit.collider.gameObject;

                    if (colorMultipleVertices)
                    {
                        // Color the closest vertex and nearby vertices based on radius
                        Vector3 closestVertexPos = vertices[closestVertexIndex];

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            float localDistance = Vector3.Distance(vertices[i], closestVertexPos);
                            if (localDistance <= vertexColorRadius)
                            {
                                colors[i] = selectedColor;
                            }
                        }
                    }
                    else
                    {
                        // Just color the single closest vertex
                        colors[closestVertexIndex] = selectedColor;
                    }

                    // Apply the updated colors
                    mesh.colors = colors;

                    Debug.Log($"Vertex colored with {colorOptions[currentColorIndex].name} at index: {closestVertexIndex}");
                }
                else
                {
                    Debug.Log("No vertices found within max search distance");
                }
            }
            else
            {
                Debug.LogWarning("Hit object doesn't have a MeshFilter or Mesh");
            }
        }
    }

    private void StoreOriginalColors(Mesh mesh)
    {
        // Store original vertex colors for later restoration
        Color[] originalColors;

        if (mesh.colors != null && mesh.colors.Length > 0)
        {
            originalColors = mesh.colors;
        }
        else
        {
            // If no colors existed, create default white colors
            originalColors = new Color[mesh.vertexCount];
            for (int i = 0; i < originalColors.Length; i++)
            {
                originalColors[i] = Color.white;
            }
        }

        originalColorDict[mesh] = originalColors;
    }

    private void RestoreOriginalColors(Mesh mesh)
    {
        if (originalColorDict.TryGetValue(mesh, out Color[] originalColors))
        {
            mesh.colors = originalColors;
            originalColorDict.Remove(mesh);
        }
    }

    private void RestoreAllOriginalColors()
    {
        foreach (var entry in currentlyColoredMeshes)
        {
            if (entry.Key != null)
            {
                RestoreOriginalColors(entry.Key);
            }
        }
        currentlyColoredMeshes.Clear();
    }

    private void OnDestroy()
    {
        // Clean up and restore colors
        RestoreAllOriginalColors();
    }
}