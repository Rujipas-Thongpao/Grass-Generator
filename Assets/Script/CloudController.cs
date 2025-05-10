using UnityEngine;

public class CloudController : MonoBehaviour
{
    [SerializeField]
    private ComputeShader cloudCompute;

    [SerializeField]
    private RenderTexture cloudRT;

    [SerializeField]
    private WindSO windSO;

    [SerializeField]
    private float scale = .5f;

    [SerializeField] private int octave = 3;

    [SerializeField] private Vector2 smoothStep = new Vector2(0.0f, 1.0f);

    [SerializeField] private Color lightColor = Color.white;
    [SerializeField] private Color darkColor = Color.blue;

    void Awake()
    {
        cloudRT.enableRandomWrite = true;
        cloudRT.Create();
    }

    void Update()
    {
        cloudCompute.SetTexture(0, "_CloudRT", cloudRT);
        cloudCompute.SetFloat("_time", Time.time);
        cloudCompute.SetFloat("_windDirection", windSO.windDirection);
        cloudCompute.SetFloat("_windStrength", windSO.windStrength);
        cloudCompute.SetFloat("_scale", scale);
        cloudCompute.SetInt("_octave", octave);
        cloudCompute.SetFloat("_step", smoothStep.x);
        cloudCompute.SetVector("_smoothStep", smoothStep);
        cloudCompute.SetVector("_lightColor", lightColor);
        cloudCompute.SetVector("_darkColor", darkColor);


        cloudCompute.Dispatch(0, cloudRT.width / 8, cloudRT.height / 8, 1);
    }
}
