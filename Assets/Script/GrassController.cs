using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

struct VegetationData
{
    public Vector3 position;
    public float noise;
    public Vector3 wind;
    public float angle;
    public float2 groundUV;
}

public class GrassController : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField]
    private ComputeShader vegCompute;

    [Header("Material")]
    [SerializeField]
    private Material grassMaterial;

    [SerializeField]
    private Material flowerMaterial;

    [Header("Mesh")]
    [SerializeField]
    private Mesh grassMesh;

    [SerializeField]
    private Mesh flowerMesh;

    [Header("Parameter")]
    [SerializeField]
    private int grassAmount = 900;

    [SerializeField]
    private int flowerAmount = 100;

    [SerializeField]
    private float grassDensity = 1.5f;

    [SerializeField]
    private float flowerDensity = 1f;

    [Header("Noise")]
    [SerializeField]
    private float grassNoiseScale;

    [SerializeField]
    private float flowerNoiseScale;

    [SerializeField]
    private WindSO windSO;

    [SerializeField]
    private RenderTexture heightRT;

    [SerializeField]
    private float groundHeightScale = 1.0f;

    [SerializeField]
    private RenderTexture cloudRT;


    private VegetationData[] grassData;
    private VegetationData[] flowerData;

    private ComputeBuffer grassBuffer,
        flowerBuffer,
        grassArgsBuffer,
        flowerArgsBuffer;

    private Bounds renderBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private uint[] grassArgs = new uint[5];
    private uint[] flowerArgs = new uint[5];
    private int subMeshIndex = 0;

    private void Awake()
    {
        grassData = new VegetationData[grassAmount];
        flowerData = new VegetationData[flowerAmount];
    }

    private void Start()
    {
        grassArgsBuffer = CreateArgsBuffer(grassMesh, grassAmount, grassArgs);
        flowerArgsBuffer = CreateArgsBuffer(flowerMesh, flowerAmount, flowerArgs);

        // set up grass and flower render textures
        grassMaterial.SetTexture("_HeightRT", heightRT);
        grassMaterial.SetTexture("_CloudRT", cloudRT);
        flowerMaterial.SetTexture("_HeightRT", heightRT);
        flowerMaterial.SetTexture("_CloudRT", cloudRT);

        UpdateVegetationBuffer(
            ref grassBuffer,
            vegCompute,
            grassData,
            grassAmount,
            grassDensity,
            grassNoiseScale,
            grassMaterial,
            "GrassBuffer"
        );
        UpdateVegetationBuffer(
            ref flowerBuffer,
            vegCompute,
            flowerData,
            flowerAmount,
            flowerDensity,
            flowerNoiseScale,
            flowerMaterial,
            "_FlowerBuffer"
        );
    }

    private ComputeBuffer CreateArgsBuffer(Mesh mesh, int instanceCount, uint[] args)
    {
        ComputeBuffer buffer = new ComputeBuffer(
            1,
            args.Length * sizeof(int),
            ComputeBufferType.IndirectArguments
        );

        if (mesh != null)
        {
            args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)instanceCount;
            args[2] = (uint)mesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)mesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            for (int i = 0; i < args.Length; i++)
                args[i] = 0;
        }

        buffer.SetData(args);
        return buffer;
    }

    private void UpdateVegetationBuffer(
        ref ComputeBuffer buffer,
        ComputeShader compute,
        VegetationData[] data,
        int amount,
        float density,
        float noiseScale,
        Material material,
        string bufferName
    )
    {
        buffer?.Release();

        int totalSize = sizeof(float) * (3 + 1 + 3 + 1 + 2); // position, noise, wind, angle, groundUV

        // set buffer
        buffer = new ComputeBuffer(data.Length, totalSize);
        buffer.SetData(data);

        compute.SetBuffer(0, "_Buffer", buffer);
        compute.SetInt("_amountPerRow", (int)Mathf.Sqrt(amount));
        compute.SetFloat("_density", density);
        compute.SetFloat("_noiseScale", noiseScale);
        compute.SetFloat("_time", Time.time);

        compute.SetVector(
            "_windDirection",
            new Vector4(
                Mathf.Cos(Mathf.Deg2Rad * windSO.windDirection),
                0f,
                Mathf.Sin(Mathf.Deg2Rad * windSO.windDirection),
                0f
            )
        );

        compute.SetFloat("_windStrength", windSO.windStrength);
        compute.Dispatch(0, amount / 8, 1, 1);

        material.SetBuffer(bufferName, buffer);
    }

    private void Update()
    {
        // grassMaterial.SetFloat("_groundHeightFactor", groundHeightScale);
        UpdateVegetationBuffer(
            ref grassBuffer,
            vegCompute,
            grassData,
            grassAmount,
            grassDensity,
            grassNoiseScale,
            grassMaterial,
            "GrassBuffer"
        );
        UpdateVegetationBuffer(
            ref flowerBuffer,
            vegCompute,
            flowerData,
            flowerAmount,
            flowerDensity,
            flowerNoiseScale,
            flowerMaterial,
            "_FlowerBuffer"
        );

        Graphics.DrawMeshInstancedIndirect(
            grassMesh,
            0,
            grassMaterial,
            renderBounds,
            grassArgsBuffer
        );

        Graphics.DrawMeshInstancedIndirect(
            flowerMesh,
            0,
            flowerMaterial,
            renderBounds,
            flowerArgsBuffer
        );
    }

    private void OnDestroy()
    {
        grassBuffer?.Release();
        flowerBuffer?.Release();
        grassArgsBuffer?.Release();
        flowerArgsBuffer?.Release();
    }
}
