
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

struct GrassData
{
    public Vector3 position;
    public float noise;
    public Vector3 wind;
    public float angle;
    public float2 grondUV;
}

struct FlowerData
{
    public Vector3 position;
    public float noise;
    public Vector3 wind;
    public float angle;
    public float2 grondUV;
}


public class GrassController : MonoBehaviour
{
    [Header("Compute Shader")]
    [SerializeField]
    ComputeShader grassCompute;
    [SerializeField]
    ComputeShader flowerCompute;


    [Header("Material")]
    [SerializeField]
    Material mat;

    [SerializeField]
    Material FlowerMat;


    [Header("Mesh")]
    [SerializeField]
    Mesh mesh;

    [SerializeField]
    Mesh flowerMesh;


    [Header("Parameter")]
    [SerializeField]
    int grassAmount = 900;
    [SerializeField]
    int flowerAmount = 100;

    [SerializeField]
    float grassDensity = 1.5f;

    [SerializeField]
    float flowerDensity = 1f;


    [Header("Noise")]
    [SerializeField]
    float noiseScale;

    [SerializeField]
    float flowerNoiseScale;


    [Header("wind")]
    [SerializeField, Range(-180.0f, 180.0f)]
    float windDirection = 90.0f;

    [SerializeField]
    float windStrength = 1.0f;


    [SerializeField]
    RenderTexture heightRT;

    GrassData[] grassDatas;
    FlowerData[] flowerDatas;

    private int subMeshIndex = 0;


    private ComputeBuffer grassBuffer;
    private ComputeBuffer flowerBuffer;

    private ComputeBuffer argsBuffer;
    private ComputeBuffer flowerArgsBuffer;
    private Bounds renderBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private uint[] flowerArgs = new uint[5] { 0, 0, 0, 0, 0 };


    void Awake()
    {
        grassDatas = new GrassData[grassAmount];
        flowerDatas = new FlowerData[flowerAmount];
    }

    private void Start()
    {
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(int), ComputeBufferType.IndirectArguments);

        // Indirect args
        if (mesh != null)
        {
            args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)grassAmount;
            args[2] = (uint)mesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)mesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }
        argsBuffer.SetData(args);

        flowerArgsBuffer = new ComputeBuffer(1, flowerArgs.Length * sizeof(int), ComputeBufferType.IndirectArguments);

        if (flowerMesh != null)
        {
            flowerArgs[0] = (uint)flowerMesh.GetIndexCount(subMeshIndex);
            flowerArgs[1] = (uint)flowerAmount;
            flowerArgs[2] = (uint)flowerMesh.GetIndexStart(subMeshIndex);
            flowerArgs[3] = (uint)flowerMesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            flowerArgs[0] = flowerArgs[1] = flowerArgs[2] = flowerArgs[3] = 0;
        }
        flowerArgsBuffer.SetData(flowerArgs);

        mat.SetTexture("_HeightRT", heightRT);
        UpdateGrassBuffer();
        UpdateFlowerBuffer();
    }

    private void UpdateGrassBuffer()
    {
        // Ensure submesh index is in range
        if (mesh != null)
            subMeshIndex = Mathf.Clamp(subMeshIndex, 0, mesh.subMeshCount - 1);

        // Positions
        if (grassBuffer != null)
            grassBuffer.Release();

        int positionSize = sizeof(float) * 3;
        int noiseSize = sizeof(float);
        int windSize = sizeof(float) * 3;
        int angleSize = sizeof(float);
        int groundUVSize = sizeof(float) * 2;
        int totalSize = positionSize + noiseSize + windSize + angleSize + groundUVSize;

        grassBuffer = new ComputeBuffer(grassDatas.Length, totalSize);
        grassBuffer.SetData(grassDatas);

        grassCompute.SetBuffer(0, "_datas", grassBuffer);
        grassCompute.SetInt("_grassAmountPerRow", (int)Mathf.Sqrt(grassAmount));
        grassCompute.SetFloat("_grassDensity", grassDensity);
        grassCompute.SetFloat("_noiseScale", noiseScale);
        grassCompute.SetFloat("_time", Time.time);
        grassCompute.SetVector(
            "_windDirection",
            new Vector4(
                Mathf.Cos(Mathf.Deg2Rad * windDirection),
                0f,
                Mathf.Sin(Mathf.Deg2Rad * windDirection),
                0f
            )
        );
        grassCompute.SetFloat("_windStrength", windStrength);

        grassCompute.Dispatch(0, grassAmount / 8, 1, 1);

        mat.SetBuffer("GrassBuffer", grassBuffer);
    }


    private void UpdateFlowerBuffer()
    {
        // Positions
        if (flowerBuffer != null)
            flowerBuffer.Release();

        int positionSize = sizeof(float) * 3;
        int noiseSize = sizeof(float);
        int windSize = sizeof(float) * 3;
        int angleSize = sizeof(float);
        int groundUVSize = sizeof(float) * 2;
        int totalSize = positionSize + noiseSize + windSize + angleSize + groundUVSize;

        flowerBuffer = new ComputeBuffer(flowerDatas.Length, totalSize);
        flowerBuffer.SetData(flowerDatas);

        flowerCompute.SetBuffer(0, "_FlowerBuffer", flowerBuffer);
        flowerCompute.SetInt("_flowerAmountPerRow", (int)Mathf.Sqrt(flowerAmount));
        flowerCompute.SetFloat("_flowerDensity", flowerDensity);
        flowerCompute.SetFloat("_noiseScale", flowerNoiseScale);
        flowerCompute.SetFloat("_time", Time.time);
        flowerCompute.SetVector(
            "_windDirection",
            new Vector4(
                Mathf.Cos(Mathf.Deg2Rad * windDirection),
                0f,
                Mathf.Sin(Mathf.Deg2Rad * windDirection),
                0f
            )
        );
        flowerCompute.SetFloat("_windStrength", windStrength);

        flowerCompute.Dispatch(0, flowerAmount / 8, 1, 1);

        FlowerMat.SetBuffer("_FlowerBuffer", flowerBuffer);
    }

    void OnDestroy()
    {
        grassBuffer.Release();
        flowerBuffer.Release();
    }

    void Update()
    {
        // RenderParams rp = new RenderParams(mat);
        UpdateGrassBuffer();
        UpdateFlowerBuffer();
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, renderBounds, argsBuffer);
        Graphics.DrawMeshInstancedIndirect(flowerMesh, 0, FlowerMat, renderBounds, flowerArgsBuffer);
    }
}
