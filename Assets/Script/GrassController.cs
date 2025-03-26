
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering;

struct GrassData
{
    public Vector3 position;
    public float noise;
    public Vector3 wind;
    public float angle;
}

public class GrassController : MonoBehaviour
{
    [SerializeField]
    ComputeShader grassCompute;

    [SerializeField]
    Material mat;

    [SerializeField]
    Mesh mesh;

    [SerializeField]
    int grassAmount = 900;

    [SerializeField]
    float grassDensity;

    [SerializeField]
    float noiseScale;

    [SerializeField, Range(-180.0f, 180.0f)]
    float windDirection = 90.0f;

    [SerializeField]
    float windStrength = 1.0f;

    GrassData[] grassDatas;

    private int subMeshIndex = 0;
    private int cachedInstanceCount = -1;
    private int cachedSubMeshIndex = -1;

    private ComputeBuffer grassBuffer;
    private ComputeBuffer argsBuffer;
    private Bounds renderBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };


    void Awake()
    {
        grassDatas = new GrassData[grassAmount];
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

        UpdateBuffer();
    }

    private void UpdateBuffer()
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
        int totalSize = positionSize + noiseSize + windSize + angleSize;

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

        cachedInstanceCount = grassAmount;
        cachedSubMeshIndex = subMeshIndex;
    }

    void Update()
    {
        RenderParams rp = new RenderParams(mat);
        UpdateBuffer();
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, renderBounds, argsBuffer);
    }
}
