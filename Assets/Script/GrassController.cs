using UnityEngine;


struct GrassData
{
    public Vector3 position;
}
public struct GrassInstanceData
{
    Matrix4x4 objectToWorld;
};



public class GrassController : MonoBehaviour
{
    [SerializeField]
    ComputeShader grassCompute;

    [SerializeField]
    Material mat;

    [SerializeField]
    Mesh mesh;

    GrassData[] grassDatas;


    void Awake()
    {
        grassDatas = new GrassData[100];
        for (int i = 0; i < 100; i++)
        {
            GrassData grass = new GrassData();
            grass.position = Vector3.zero;
            grassDatas[i] = grass;
        }
    }





    void ComputePosition(bool debug = false)
    {
        int positionSize = sizeof(float) * 3;
        int totalSize = positionSize;

        ComputeBuffer buffer = new ComputeBuffer(grassDatas.Length, positionSize);
        buffer.SetData(grassDatas);

        grassCompute.SetBuffer(0, "datas", buffer);

        grassCompute.Dispatch(0, 100 / 10, 1, 1);

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

    void Update()
    {
        int numInstances = 100;
        RenderParams rp = new RenderParams(mat);
        Matrix4x4[] instData = new Matrix4x4[numInstances];
        ComputePosition();
        for (int i = 0; i < numInstances; ++i)
        {
            Debug.Log(grassDatas[i].position);
            instData[i] = Matrix4x4.TRS(grassDatas[i].position, Quaternion.identity, new Vector3(1.0f, 1.0f, 1.0f));
        }
        Graphics.RenderMeshInstanced(rp, mesh, 0, instData);
    }
}
