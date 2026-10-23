// @role Day, sunset and night cycle: sky color, sun light and ambient light
// @tune dayTime:float:5-300:Seconds of full day
// @tune nightTime:float:5-300:Seconds of full night
// @tune transitionTime:float:1-60:Seconds of sunset or sunrise
// @tune dayLightIntensity:float:0-3:Sun brightness in the day
// @tune nightLightIntensity:float:0-1:Moon brightness at night
using UnityEngine;

public class DayNightController : MonoBehaviour
{
    [Header("主光源与时间配比")]
    public Light mainLight; // 场景中的 Directional Light

    [Header("雾气开关")]
    public bool enableFog = true;

    [Header("时间段控制 (单位：秒)")]
    public float dayTime = 40f;        // 纯白天：40秒
    public float nightTime = 40f;      // 纯黑夜：40秒
    public float transitionTime = 10f; // 昼夜交替（黄昏/朝霞）：10秒

    [Header("1. 白天颜色（天空鲜艳，环境光中性洁净）")]
    public Color daySkyColor = new Color(0.50f, 0.78f, 0.98f);     // 亮丽卡通天蓝色
    public Color dayLightColor = new Color(1.0f, 0.99f, 0.96f);    // 自然米白阳光
    public float dayLightIntensity = 1.25f;                       // 白天光强
    public Color dayAmbientColor = new Color(0.88f, 0.90f, 0.92f); // 极其干净的中性浅灰白色环境光

    [Header("2. 火烧云黄昏/朝霞（天空鲜艳火烧橙，场景仅微弱暖化）")]
    public Color sunsetSkyColor = new Color(1.0f, 0.50f, 0.25f);   // 极度鲜艳活泼的火烧马卡龙橙天空！
    public Color sunsetLightColor = new Color(1.0f, 0.90f, 0.80f); // 微暖的淡白金色阳光（不强行染色）
    public Color sunsetAmbientColor = new Color(0.90f, 0.82f, 0.78f); // 极淡微暖环境光（雪和树依然保持原色）

    [Header("3. 黑夜颜色（天空藏青夜色，场景适当压暗）")]
    public Color nightSkyColor = new Color(0.08f, 0.12f, 0.22f);   // 深邃藏青夜空
    public Color nightLightColor = new Color(0.40f, 0.55f, 0.75f); // 微弱淡蓝月光
    public float nightLightIntensity = 0.18f;                      // 压低黑夜光强
    public Color nightAmbientColor = new Color(0.20f, 0.25f, 0.35f); // 低饱淡蓝环境光

    [Range(0, 1)]
    public float timeOfDay = 0f;

    private float totalCycleTime;

    void Start()
    {
        totalCycleTime = dayTime + transitionTime + nightTime + transitionTime;

        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
        }

        RenderSettings.fog = enableFog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 600f; 
        RenderSettings.fogEndDistance = 2000f;
    }

    void Update()
    {
        if (totalCycleTime > 0)
        {
            timeOfDay += (Time.deltaTime / totalCycleTime);
            if (timeOfDay >= 1f) timeOfDay = 0f;
        }

        UpdateSunRotation();
        UpdateEnvironment();
    }

    void UpdateSunRotation()
    {
        if (mainLight == null) return;
        
        float sunAngle = (timeOfDay * 360f) - 90f;
        mainLight.transform.localRotation = Quaternion.Euler(sunAngle, 45f, 0f);
    }

    void UpdateEnvironment()
    {
        Color currentSky, currentLight, currentAmbient;
        float currentIntensity;

        // 时间段比例划分 (总长 100 秒: 白天40s / 黄昏10s / 黑夜40s / 朝霞10s)
        float pDay = dayTime / totalCycleTime;                          // 0.0 -> 0.4
        float pSunset = pDay + (transitionTime / totalCycleTime);       // 0.4 -> 0.5
        float pNight = pSunset + (nightTime / totalCycleTime);          // 0.5 -> 0.9

        if (timeOfDay < pDay) // 1. 纯白天 (40秒)
        {
            currentSky = daySkyColor;
            currentLight = dayLightColor;
            currentIntensity = dayLightIntensity;
            currentAmbient = dayAmbientColor;
        }
        else if (timeOfDay < pSunset) // 2. 白天 -> 黄昏 -> 黑夜 (10秒)
        {
            float t = (timeOfDay - pDay) / (transitionTime / totalCycleTime);
            if (t < 0.5f)
            {
                float subT = t * 2f;
                currentSky = Color.Lerp(daySkyColor, sunsetSkyColor, subT);
                currentLight = Color.Lerp(dayLightColor, sunsetLightColor, subT);
                currentAmbient = Color.Lerp(dayAmbientColor, sunsetAmbientColor, subT);
            }
            else
            {
                float subT = (t - 0.5f) * 2f;
                currentSky = Color.Lerp(sunsetSkyColor, nightSkyColor, subT);
                currentLight = Color.Lerp(sunsetLightColor, nightLightColor, subT);
                currentAmbient = Color.Lerp(sunsetAmbientColor, nightAmbientColor, subT);
            }
            currentIntensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, t);
        }
        else if (timeOfDay < pNight) // 3. 纯黑夜 (40秒)
        {
            currentSky = nightSkyColor;
            currentLight = nightLightColor;
            currentIntensity = nightLightIntensity;
            currentAmbient = nightAmbientColor;
        }
        else // 4. 黑夜 -> 朝霞 -> 白天 (10秒)
        {
            float t = (timeOfDay - pNight) / (transitionTime / totalCycleTime);
            if (t < 0.5f)
            {
                float subT = t * 2f;
                currentSky = Color.Lerp(nightSkyColor, sunsetSkyColor, subT);
                currentLight = Color.Lerp(nightLightColor, sunsetLightColor, subT);
                currentAmbient = Color.Lerp(nightAmbientColor, sunsetAmbientColor, subT);
            }
            else
            {
                float subT = (t - 0.5f) * 2f;
                currentSky = Color.Lerp(sunsetSkyColor, daySkyColor, subT);
                currentLight = Color.Lerp(sunsetLightColor, dayLightColor, subT);
                currentAmbient = Color.Lerp(sunsetAmbientColor, dayAmbientColor, subT);
            }
            currentIntensity = Mathf.Lerp(nightLightIntensity, dayLightIntensity, t);
        }

        // 应用属性
        if (Camera.main != null) Camera.main.backgroundColor = currentSky;

        if (mainLight != null)
        {
            mainLight.color = currentLight;
            mainLight.intensity = currentIntensity;
            mainLight.shadows = LightShadows.Soft;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = currentAmbient;

        RenderSettings.fog = enableFog;
        if (enableFog)
        {
            RenderSettings.fogColor = currentSky; 
        }
    }
}