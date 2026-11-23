using UnityEngine;

namespace Apex {
[CreateAssetMenu(menuName="Apex/Engine power map")]
public sealed class EnginePowerMap : ScriptableObject {
    [Min(0)] public float peakMechanicalHp;
    [Tooltip("Engine rpm on X, fraction of peak full-throttle shaft power on Y.")]
    public AnimationCurve fullThrottlePower;

    public float TorqueNm(float rpm){
        if(rpm<=0)return 0;
        const float wattsPerMechanicalHp=745.6999f;
        float power=peakMechanicalHp*wattsPerMechanicalHp*Mathf.Clamp01(fullThrottlePower.Evaluate(rpm));
        return power/(rpm*(2*Mathf.PI/60));
    }
}
}
