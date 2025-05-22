using UnityEngine;

public struct Ground
{
    public float displace;
}

public enum E_Resolution
{
    _8 = 8,
    _16 = 16,
    _32 = 32,
    _64 = 64,
    _128 = 128,
    _256 = 256,
    _512 = 512
}

public class GroundController : MonoBehaviour
{
    [SerializeField]
    ComputeShader groundCompute;
    [SerializeField]
    Mesh groundMesh;

    [SerializeField]
    RenderTexture heightRT;

    [SerializeField]
    E_Resolution resolution;

    [SerializeField]
    Material mat;


    int vertCount;
    int vertCountWidth;


    void Awake()
    {
        heightRT.enableRandomWrite = true;
        heightRT.Create();
    }

    void Start()
    {
        groundCompute.SetTexture(0, "_HeightRT", heightRT);
        groundCompute.Dispatch(0, heightRT.width / 8, heightRT.height / 8, 1);
        mat.SetTexture("_HeightRT", heightRT);
    }
}
