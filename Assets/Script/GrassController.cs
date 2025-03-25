
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering;

struct GrassData
{
    public Vector3 position;
}
public struct GrassInstanceData
{
    Matrix4x4 objectToWorld;
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
    int grassAmount = 30 * 30;

    [SerializeField]
    float grassDensity;

    GrassData[] grassDatas;

    void Awake()
    {
        grassDatas = new GrassData[grassAmount];
    }


    void ComputePosition(bool debug = false)
    {
        int positionSize = sizeof(float) * 3;
        int totalSize = positionSize;

        ComputeBuffer buffer = new ComputeBuffer(grassDatas.Length, totalSize, ComputeBufferType.IndirectArguments);
        buffer.SetData(grassDatas);

        grassCompute.SetBuffer(0, "_datas", buffer);
        grassCompute.SetInt("_grassAmountPerRow", (int)Mathf.Sqrt(grassAmount));
        grassCompute.SetFloat("_grassDensity", grassDensity);

        grassCompute.Dispatch(0, grassAmount / 8, 1, 1);

        buffer.GetData(grassDatas);

        if (debug)
        {
            for (int i = 0; i < grassDatas.Length; i++)
            {
                Debug.Log(grassDatas[i].position);
            }
        }

        buffer.Dispose();
    }

    void Start()
    {
        ComputePosition();
    }

    void Update()
    {
        RenderParams rp = new RenderParams(mat);
        Matrix4x4[] instData = new Matrix4x4[grassAmount];
        for (int i = 0; i < grassAmount; ++i)
        {
            // Debug.Log(grassDatas[i].position);
            instData[i] = Matrix4x4.TRS(grassDatas[i].position, Quaternion.identity, new Vector3(1.0f, 1.0f, 1.0f));
        }
        Graphics.RenderMeshInstanced(rp, mesh, 0, instData);
    }
}
