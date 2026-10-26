public class SiloPlantWorldBuilder : MonoBehaviour
{
    public bool buildOnStart = true;
    public bool createCameraIfMissing = true;

    private Transform plantRoot;

    private readonly Color groundColor = new Color(0.20f, 0.32f, 0.18f);
    private readonly Color steelColor = new Color(0.65f, 0.70f, 0.66f);
    private readonly Color darkGreen = new Color(0.10f, 0.25f, 0.15f);
    private readonly Color goldColor = new Color(0.76f, 0.57f, 0.20f);

    private void Start()
    {
        if (buildOnStart)
            BuildPlant();
    }

    [ContextMenu("Build Plant")]
    public void BuildPlant()
    {
        if (plantRoot != null)
            Destroy(plantRoot.gameObject);

        GameObject root = new GameObject("Generated Grain Plant");
        plantRoot = root.transform;

        CreateObject("Plant Yard", PrimitiveType.Plane,
            Vector3.zero, new Vector3(3.6f, 1f, 2.4f), groundColor);

        CreateObject("Receiving Port", PrimitiveType.Cube,
            new Vector3(-10f, 0.8f, -5f),
            new Vector3(3f, 1.6f, 3f), new Color(0.35f, 0.43f, 0.31f));

        CreateObject("Holding Bin", PrimitiveType.Cylinder,
            new Vector3(-7f, 1.5f, 0f),
            new Vector3(2.2f, 1.5f, 2.2f), steelColor);

        CreateObject("Dryer Housing", PrimitiveType.Cube,
            new Vector3(-2f, 1.1f, -5f),
            new Vector3(4.5f, 2.2f, 2.2f), goldColor);

        CreateObject("Furnace Housing", PrimitiveType.Cube,
            new Vector3(1.5f, 1.4f, -5f),
            new Vector3(1.3f, 2.8f, 1.4f), new Color(0.45f, 0.37f, 0.30f));

        CreateObject("Dispatch Bin", PrimitiveType.Cylinder,
            new Vector3(10f, 1.6f, -3.5f),
            new Vector3(2.4f, 1.6f, 2.4f), steelColor);

        for (int i = 0; i < 7; i++)
        {
            float x = -5.4f + i * 1.8f;

            CreateObject("Silo " + (i + 1), PrimitiveType.Cylinder,
                new Vector3(x, 2.3f, 2.2f),
                new Vector3(1.35f, 2.3f, 1.35f), steelColor);

            CreateObject("Silo Roof " + (i + 1), PrimitiveType.Cylinder,
                new Vector3(x, 4.7f, 2.2f),
                new Vector3(1.42f, 0.3f, 1.42f), darkGreen);

            CreateObject("Silo Support " + (i + 1), PrimitiveType.Cube,
                new Vector3(x, 0.15f, 2.2f),
                new Vector3(1.5f, 0.3f, 1.5f), darkGreen);
        }

        CreateObject("Receiving Conveyor", PrimitiveType.Cube,
            new Vector3(-8.6f, 1.4f, -2.4f),
            new Vector3(4f, 0.18f, 0.45f), darkGreen);

        CreateObject("Silo Gallery Conveyor", PrimitiveType.Cube,
            new Vector3(0f, 1.4f, 1.1f),
            new Vector3(13.8f, 0.18f, 0.45f), darkGreen);

        CreateObject("Dryer Feed Conveyor", PrimitiveType.Cube,
            new Vector3(-3.8f, 1.2f, -1.2f),
            new Vector3(0.45f, 0.18f, 5f), darkGreen);

        CreateObject("Dispatch Conveyor", PrimitiveType.Cube,
            new Vector3(8.2f, 1.4f, -1.3f),
            new Vector3(0.45f, 0.18f, 4.3f), darkGreen);

        CreateObject("Bucket Elevator", PrimitiveType.Cube,
            new Vector3(-7.7f, 3.5f, -0.8f),
            new Vector3(0.65f, 5.7f, 0.8f), new Color(0.30f, 0.39f, 0.31f));

        CreateObject("Truck Chassis", PrimitiveType.Cube,
            new Vector3(-11f, 0.5f, -7.3f),
            new Vector3(3f, 0.6f, 1.2f), goldColor);

        CreateObject("Truck Cab", PrimitiveType.Cube,
            new Vector3(-12f, 1.15f, -7.3f),
            new Vector3(0.9f, 0.8f, 1.1f), steelColor);

        CreateObject("Truck Wheel 1", PrimitiveType.Cylinder,
            new Vector3(-12f, 0.28f, -6.65f),
            new Vector3(0.48f, 0.2f, 0.48f), Color.black)
            .transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        CreateObject("Truck Wheel 2", PrimitiveType.Cylinder,
            new Vector3(-12f, 0.28f, -7.95f),
            new Vector3(0.48f, 0.2f, 0.48f), Color.black)
            .transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        CreateObject("Truck Wheel 3", PrimitiveType.Cylinder,
            new Vector3(-9.9f, 0.28f, -6.65f),
            new Vector3(0.48f, 0.2f, 0.48f), Color.black)
            .transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        CreateObject("Truck Wheel 4", PrimitiveType.Cylinder,
            new Vector3(-9.9f, 0.28f, -7.95f),
            new Vector3(0.48f, 0.2f, 0.48f), Color.black)
            .transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        CreateObject("Control Building", PrimitiveType.Cube,
            new Vector3(6f, 1f, -7f),
            new Vector3(3f, 2f, 2f), darkGreen);

        CreateLighting();
        PositionCamera();
    }

    private GameObject CreateObject(
        string objectName,
        PrimitiveType primitive,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(primitive);

        obj.name = objectName;
        obj.transform.SetParent(plantRoot);
        obj.transform.position = position;
        obj.transform.localScale = scale;

        Renderer renderer = obj.GetComponent<Renderer>();

        if (renderer != null)
        {
            // Renderer.material creates a per-object material instance.
            renderer.material.color = color;
        }

        return obj;
    }

    private void CreateLighting()
    {
        Light existing = FindObjectOfType<Light>();

        if (existing == null)
        {
            GameObject lightObject = new GameObject("Plant Sun");
            Light light = lightObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.52f);
    }

    private void PositionCamera()
    {
        Camera cam = Camera.main;

        if (cam == null && createCameraIfMissing)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cam = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        if (cam == null)
            return;

        cam.transform.position = new Vector3(0f, 15f, -23f);
        cam.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
        cam.fieldOfView = 55f;
        cam.backgroundColor = new Color(0.16f, 0.24f, 0.17f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }
}
